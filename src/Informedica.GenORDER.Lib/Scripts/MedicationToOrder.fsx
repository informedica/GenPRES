#I __SOURCE_DIRECTORY__

// # MedicationToOrder
//
// Plan 831 (issue #831, plan 725 trailing phase O2): build an Order from a Medication
// without going out to a Dto and back.
//
// This script holds two things:
//
// 1. `Harness` — the equivalence proof. Both paths over every scenario fixture, compared
//    unsolved and solved. It is written before any of the new code and is the gate every
//    later step is measured against.
// 2. `ToOrder` — the new pipeline, grown one step at a time (plan steps B0 to B5).
//
// Run it after `dotnet run Build`. The FSI session must be restarted after a rebuild,
// because a referenced dll cannot be unloaded.

#load "load.fsx"

open System
open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
open Informedica.GenOrder.Lib.Types
open Informedica.GenSolver.Lib.Variable.ValueRange



/// The fixtures the proof runs over: every scenario in the test project, which between them
/// cover all five order types, single and multiple components, solution limits and
/// divisibility.
module Fixtures =


    /// A medication parsed from one of the text fixtures, or a failure to parse it.
    let ofText name text =
        match text |> Medication.fromString with
        | Ok med -> Some(name, med)
        | Error errs ->
            printfn $"could not parse %s{name}: %A{errs}"
            None


    /// Every fixture, by name.
    let all =
        [
            "pcmSupp", Scenarios.pcmSupp
            "amfo", Scenarios.amfo
            "morfCont", Scenarios.morfCont
            "pcmDrink", Scenarios.pcmDrink
            "cotrim", Scenarios.cotrim
            "tpn", Scenarios.tpn
            "tpnComplete", Scenarios.tpnComplete
            "fullMedication", Scenarios.fullMedication
        ]
        @ ([
            "pcmSuppText", Scenarios.pcmSuppText
            "kaliumchlorideOnceTimedText", Scenarios.kaliumchlorideOnceTimedText
            "fullMedicationText", Scenarios.fullMedicationText
           ]
           |> List.choose (fun (n, t) -> ofText n t))



/// The equivalence proof: the old path and the new one, compared over every fixture.
module Harness =


    /// The old path, the one plan 831 replaces: a Medication out to an Order Dto and back.
    let viaDto (med: Medication) =
        med |> Medication.toOrderDto |> Order.Dto.fromDto


    /// Both paths read the clock, so an order's start and stop say nothing about the build
    /// that made it. Normalising them leaves a comparison of everything that does.
    let normalize (ord: Order) =
        { ord with
            StartStop = DateTime.MinValue |> StartStop.Start
        }


    /// Whether two orders are the same order, the start and stop aside.
    let sameOrder (a: Order) (b: Order) = (a |> normalize) = (b |> normalize)


    /// The canonical serialization of an order, the form the signing digest is taken over.
    /// Two of these diffed name the variable that moved.
    let canonical (ord: Order) =
        ord |> normalize |> Order.Dto.toDto |> Canonical.serialize


    /// The first line on which two canonical forms differ, with a little of both around it.
    let firstDifference (a: string) (b: string) =
        let split (s: string) = s |> String.replace "," ",\n" |> String.split "\n"
        let xs, ys = a |> split, b |> split

        List.zip3 [ 0 .. (min xs.Length ys.Length) - 1 ] (xs |> List.truncate ys.Length) (ys |> List.truncate xs.Length)
        |> List.tryFind (fun (_, x, y) -> x <> y)
        |> function
            | Some(i, x, y) -> $"line %i{i}:\n  old: %s{x |> String.trim}\n  new: %s{y |> String.trim}"
            | None -> "no line differs; the forms differ in length"


    /// The full solve every scenario goes through before it reaches a prescriber.
    let solve (ord: Order) =
        let run cmd o =
            o |> cmd |> OrderProcessor.processPipeline Logging.noOp |> function
                | Ok o -> o
                | Error(o, _) -> o

        ord
        |> run CalcMinMax
        |> run IncreaseIncrements
        |> run CalcValues
        |> run SolveOrder


    /// Compare one fixture through both paths, unsolved and then solved, and say what was
    /// found. `toOrder` is the new path under test.
    let check toOrder (name: string, med: Medication) =
        match med |> viaDto, med |> toOrder with
        | Error e, _ -> $"%-28s{name} SKIP  the old path fails: %A{e}"
        | _, Error e -> $"%-28s{name} FAIL  the new path fails: %A{e}"
        | Ok oldOrd, Ok newOrd ->
            if oldOrd |> sameOrder newOrd |> not then
                let d = firstDifference (canonical oldOrd) (canonical newOrd)
                $"%-28s{name} FAIL  unsolved orders differ\n%s{d}"
            else
                let oldSolved, newSolved = oldOrd |> solve, newOrd |> solve

                if oldSolved |> sameOrder newSolved then
                    $"%-28s{name} ok    unsolved and solved equal"
                else
                    let a = oldSolved |> normalize |> Order.toString
                    let b = newSolved |> normalize |> Order.toString

                    let d =
                        List.zip (a |> List.truncate b.Length) (b |> List.truncate a.Length)
                        |> List.filter (fun (x, y) -> x <> y)
                        |> List.map (fun (x, y) -> $"  old: %s{x}\n  new: %s{y}")
                        |> String.concat "\n"

                    $"%-28s{name} FAIL  solved orders differ\n%s{d}"


    /// Run the proof over every fixture and print the result of each.
    let run toOrder =
        Fixtures.all |> List.map (check toOrder) |> List.iter (printfn "%s")


    /// The proof run against the old path itself: every line must read ok. This is the
    /// self-test of the harness, and the baseline the new path is measured against.
    let selfTest () = run viaDto



/// A gauge for the steps between the shape pass and the flip: which order variables still
/// carry constraints the old path gives them and the new one does not, or the other way
/// round. The list shrinks as each constraint pass lands; when it is empty, the pipeline is
/// done and the harness above is the proof.
module Gauge =


    /// Every order variable of an order, by name.
    let byName (ord: Order) =
        ord
        |> Order.toOrdVars
        |> List.map (fun ovar -> ovar |> OrderVariable.getName |> Name.toString, ovar)


    /// The constraints of an order variable, as the one line that says all four bounds.
    let constraintsOf (ovar: OrderVariable) =
        let vuStr = ValueUnit.toStringDutchShort

        ovar
        |> OrderVariable.getConstraints
        |> fun cs ->
            let incl b = if b then "=" else " "

            let min =
                cs.Min
                |> Option.map (fun m -> $"%s{m |> Minimum.toValueUnit |> vuStr}%s{m |> Minimum.isIncl |> incl}")
                |> Option.defaultValue "."

            let max =
                cs.Max
                |> Option.map (fun m -> $"%s{m |> Maximum.toValueUnit |> vuStr}%s{m |> Maximum.isIncl |> incl}")
                |> Option.defaultValue "."
            let incr = cs.Incr |> Option.map (Increment.toValueUnit >> vuStr) |> Option.defaultValue "."
            let vals = cs.Values |> Option.map (ValueSet.toValueUnit >> vuStr) |> Option.defaultValue "."

            $"min %s{min} | incr %s{incr} | max %s{max} | vals %s{vals}"


    /// The order variables whose constraints the two paths disagree about, by name.
    let diff (oldOrd: Order) (newOrd: Order) =
        let olds = oldOrd |> byName |> Map.ofList
        let news = newOrd |> byName |> Map.ofList

        olds
        |> Map.toList
        |> List.choose (fun (n, o) ->
            match news |> Map.tryFind n with
            | None -> Some(n, o |> constraintsOf, "(no such variable)")
            | Some v ->
                let a, b = o |> constraintsOf, v |> constraintsOf
                if a = b then None else Some(n, a, b)
        )


    /// Print, per fixture, how many order variables still disagree and the first few of them.
    let report toOrder =
        Fixtures.all
        |> List.iter (fun (name, med) ->
            match med |> Harness.viaDto, med |> toOrder with
            | Ok oldOrd, Ok newOrd ->
                let d = diff oldOrd newOrd
                printfn $"%-28s{name} %3i{d |> List.length} of %3i{oldOrd |> byName |> List.length} variables differ"

                d
                |> List.truncate 4
                |> List.iter (fun (n, a, b) -> printfn $"    %s{n}\n      old: %s{a}\n      new: %s{b}")
            | Error e, _ -> printfn $"%-28s{name} the old path fails: %A{e}"
            | _, Error e -> printfn $"%-28s{name} the new path fails: %A{e}"
        )


/// Plan 831 step B0: the constraint setters the pipeline writes through.
///
/// They are field-wise on purpose. Several passes write only a minimum, or only an increment,
/// and rely on the rest surviving, and the pipeline is last-write-wins: a pass may put an
/// increment on a quantity whose values an earlier pass set. A whole-record set would silently
/// drop the exclusive zero minimum that `createNew` installs.
///
/// The guards are those of the Dto path they replace: an empty value unit is no value set and
/// no increment, while a minimum and a maximum are built from whatever they are given.
module Constrain =


    /// A value unit that holds no value constrains nothing.
    let private nonEmpty vu =
        vu |> Option.filter (ValueUnit.isEmpty >> not)


    /// The values an order variable may take.
    let setVals vu (cs: Constraints) =
        { cs with
            Values = vu |> nonEmpty |> Option.map ValueSet.create
        }


    /// The step between the values an order variable may take.
    let setIncr vu (cs: Constraints) =
        { cs with
            Incr = vu |> nonEmpty |> Option.map Increment.create
        }


    /// The lower bound, inclusive or not.
    let setMin incl vu (cs: Constraints) =
        { cs with
            Min = vu |> Option.map (Minimum.create incl)
        }


    /// The upper bound, inclusive or not.
    let setMax incl vu (cs: Constraints) =
        { cs with
            Max = vu |> Option.map (Maximum.create incl)
        }


    /// The bounds of a dose limit. A limit that names one value rather than a range is a norm
    /// dose, and is widened by a tenth either way so that the solver has somewhere to go.
    /// A bound the limit does not give is left as it was.
    let setMinMax calcNormDose (minMax: MinMax) (cs: Constraints) =
        let isNormDose =
            calcNormDose
            && (match minMax.Min, minMax.Max with
                | Some minLimit, Some maxLimit -> minLimit |> Limit.eq maxLimit
                | _ -> false)

        let widen f vu =
            if isNormDose then
                vu * (f |> ValueUnit.singleWithUnit Units.Count.times)
            else
                vu

        let bound f = Option.map Limit.getValueUnit >> Option.map (widen f)

        let min = minMax.Min |> bound (90N / 100N)
        let max = minMax.Max |> bound (11N / 10N)

        cs
        |> fun cs -> if min |> Option.isSome then cs |> setMin true min else cs
        |> fun cs -> if max |> Option.isSome then cs |> setMax true max else cs


    /// A single value in a unit, or nothing when there is no unit to give it.
    let single u br =
        if u = NoUnit then
            None
        else
            br |> ValueUnit.singleWithUnit u |> Some



/// The order variable wrappers, each mapped over its order variable. The library has
/// `applyConstraints` and friends per wrapper but no general map, so plan 831 step B0 adds one.
module Map =

    let qty f (Quantity ovar) = ovar |> f |> Quantity
    let cnc f (Concentration ovar) = ovar |> f |> Concentration
    let cnt f (Count ovar) = ovar |> f |> Count
    let frq f (Frequency ovar) = ovar |> f |> Frequency
    let tme f (Time ovar) = ovar |> f |> Time
    let rte f (Rate ovar) = ovar |> f |> Rate
    let ptm f (PerTime ovar) = ovar |> f |> PerTime
    let tot f (Total ovar) = ovar |> f |> Total
    let qtyAdj f (QuantityAdjust ovar) = ovar |> f |> QuantityAdjust
    let ptmAdj f (PerTimeAdjust ovar) = ovar |> f |> PerTimeAdjust
    let rteAdj f (RateAdjust ovar) = ovar |> f |> RateAdjust
    let totAdj f (TotalAdjust ovar) = ovar |> f |> TotalAdjust


    /// Change the constraints of an order variable, leaving everything else alone.
    let con f (ovar: OrderVariable) =
        ovar |> OrderVariable.setConstraints (ovar |> OrderVariable.getConstraints |> f)


/// Plan 831: an Order built from a Medication, without a Dto.
///
/// The pipeline is shape first and then one pass per constraint concern, each pass
/// `Medication -> Order -> Order` and pure.
module ToOrder =


    /// The schedule an order type asks for. An order that is neither prescribed nor
    /// administered has no schedule to build, and no order either.
    let scheduleOf (med: Medication) =
        match med.OrderType with
        | OnceOrder -> Order.Schedule.once NoUnit NoUnit
        | OnceTimedOrder -> Order.Schedule.onceTimed NoUnit NoUnit
        | ContinuousOrder -> Order.Schedule.continuous NoUnit NoUnit
        | DiscontinuousOrder -> Order.Schedule.discontinuous NoUnit NoUnit
        | TimedOrder -> Order.Schedule.timed NoUnit NoUnit
        | AnyOrder
        | ProcessOrder ->
            $"a medication order cannot have the order type %A{med.OrderType}"
            |> NotSupportedException
            |> raise


    /// The empty order an order type asks for: its schedule, its id, its name and its route,
    /// and no component yet.
    let newOrder (med: Medication) =
        med |> scheduleOf |> Order.createNew med.Id med.Name <| med.Route


    /// The components of the medication and their items, as shape alone: every order variable
    /// is the empty one its name and level give it, and no constraint is set here.
    let withComponents (med: Medication) (ord: Order) =
        let components =
            med.Components
            |> List.map (fun pc ->
                let cmp = Order.Orderable.Component.createNew med.Id med.Name pc.Name pc.Form

                { cmp with
                    Items =
                        pc.Substances
                        |> List.map (fun si -> Order.Orderable.Item.createNew med.Id med.Name pc.Name si.Name)
                }
            )

        { ord with
            Orderable = { ord.Orderable with Components = components }
        }




/// Plan 831 step B2: the item pass.
module ToOrderItems =

    open ToOrder


    /// Walk the medication and the order together. Both lists came from the same medication in
    /// the shape pass, so they pair up by position; a mismatch means the shape pass changed
    /// and the build should stop rather than quietly constrain the wrong item.
    let mapItems (f: ProductComponent -> SubstanceItem -> Types.Item -> Types.Item) (med: Medication) (ord: Order) =
        let components =
            List.zip med.Components ord.Orderable.Components
            |> List.map (fun (pc, cmp) ->
                { cmp with
                    Items = List.zip pc.Substances cmp.Items |> List.map (fun (si, itm) -> f pc si itm)
                }
            )

        { ord with
            Orderable = { ord.Orderable with Components = components }
        }


    /// What a solution asks of an item: how much of it the orderable may hold, and in what
    /// concentration.
    let withSolution (sl: SolutionLimit) (itm: Types.Item) =
        { itm with
            OrderableQuantity = itm.OrderableQuantity |> Map.qty (Map.con (Constrain.setMinMax false sl.Quantity))
            OrderableConcentration =
                itm.OrderableConcentration
                |> Map.cnc (Map.con (Constrain.setMinMax true sl.Concentration))
        }


    /// The quantities and concentrations the products give an item. With one component the
    /// orderable is the component, so the concentration in the one is the concentration in
    /// the other.
    let withQtyConc (med: Medication) (si: SubstanceItem) (itm: Types.Item) =
        let single = med.Components |> List.length = 1

        { itm with
            ComponentConcentration = itm.ComponentConcentration |> Map.cnc (Map.con (Constrain.setVals si.Concentrations))
            ComponentQuantity = itm.ComponentQuantity |> Map.qty (Map.con (Constrain.setVals si.Quantities))
            OrderableConcentration =
                if single then
                    itm.OrderableConcentration |> Map.cnc (Map.con (Constrain.setVals si.Concentrations))
                else
                    itm.OrderableConcentration
        }
        |> fun itm ->
            match si.Solution with
            | None -> itm
            | Some sl -> itm |> withSolution sl


    /// What the dose rule allows of the substance, in the terms the order type dose in.
    let withDose (med: Medication) (si: SubstanceItem) (itm: Types.Item) =
        let rate (dl: DoseLimit) (dos: Dose) =
            { dos with
                Rate = dos.Rate |> Map.rte (Map.con (Constrain.setMinMax false dl.Rate))
                RateAdjust = dos.RateAdjust |> Map.rteAdj (Map.con (Constrain.setMinMax true dl.RateAdjust))
            }

        let quantity (dl: DoseLimit) (dos: Dose) =
            { dos with
                Quantity =
                    dos.Quantity
                    |> Map.qty (
                        Map.con (fun cs ->
                            // a dose quantity without a limit still needs a unit, so that it
                            // can be added up with the others
                            if dl.Quantity |> MinMax.isEmpty then
                                cs |> Constrain.setMin false (0N |> Constrain.single dl.DoseUnit)
                            else
                                cs |> Constrain.setMinMax false dl.Quantity
                        )
                    )
                QuantityAdjust =
                    dos.QuantityAdjust
                    |> Map.qtyAdj (Map.con (Constrain.setMinMax true dl.QuantityAdjust))
                PerTime = dos.PerTime |> Map.ptm (Map.con (Constrain.setMinMax false dl.PerTime))
                PerTimeAdjust =
                    dos.PerTimeAdjust
                    |> Map.ptmAdj (Map.con (Constrain.setMinMax true dl.PerTimeAdjust))
            }

        let apply =
            match med.OrderType with
            | AnyOrder
            | ProcessOrder -> fun _ dos -> dos
            | ContinuousOrder -> rate
            | OnceOrder
            | DiscontinuousOrder -> quantity
            | OnceTimedOrder
            | TimedOrder -> fun dl dos -> dos |> rate dl |> quantity dl

        match si.Dose with
        | None -> itm
        | Some dl -> { itm with Dose = itm.Dose |> apply dl }


    /// Every constraint an item carries.
    let withItemConstraints (med: Medication) (ord: Order) =
        ord |> mapItems (fun _ si itm -> itm |> withQtyConc med si |> withDose med si) med


/// Plan 831 step B3: the component pass.
module ToOrderComponents =


    /// The unit the orderable is measured in: the one the first component is measured in.
    let orderableUnit (med: Medication) =
        med.Components
        |> List.tryHead
        |> Option.bind (fun pc -> pc.Quantities |> Option.map ValueUnit.getUnit)


    /// The smallest step a product can be divided into. A medication that says how divisible
    /// it is says it for the orderable; otherwise the coarsest of what its components say.
    let divisibility (pc: ProductComponent option) (med: Medication) =
        let ou = med |> orderableUnit
        let pu = pc |> Option.bind (_.Quantities >> Option.map ValueUnit.getUnit)

        match ou, med.Div with
        | Some ou, Some br when Some ou = pu -> 1N / br |> Constrain.single ou
        | Some ou, None ->
            let incrs =
                med.Components
                |> List.choose (fun pc ->
                    if pc.Quantities |> Option.map ValueUnit.getUnit = pu || pu.IsNone then
                        pc.Divisible |> Option.map (fun d -> 1N / d)
                    else
                        None
                )

            if incrs |> List.isEmpty then
                None
            else
                let u = pu |> Option.defaultValue ou
                incrs |> List.max |> Constrain.single u
        | _ -> None


    /// Walk the medication's components and the order's together.
    let mapComponents (f: ProductComponent -> Types.Component -> Types.Component) (med: Medication) (ord: Order) =
        { ord with
            Orderable =
                { ord.Orderable with
                    Components = List.zip med.Components ord.Orderable.Components |> List.map (fun (pc, cmp) -> f pc cmp)
                }
        }


    /// How much of a component the orderable holds, and in what concentration. A component can
    /// never be more than all of the orderable, and where it is the only one it is all of it.
    let withQtyConc (med: Medication) (pc: ProductComponent) (cmp: Types.Component) =
        let single = med.Components |> List.length = 1
        let incr = med |> divisibility (Some pc)
        let all = Units.Count.times |> ValueUnit.singleWithValue 1N |> Some

        { cmp with
            OrderableConcentration =
                cmp.OrderableConcentration
                |> Map.cnc (
                    Map.con (fun cs ->
                        cs
                        |> Constrain.setMax single all
                        |> fun cs -> if single then cs |> Constrain.setVals all else cs
                    )
                )
            ComponentQuantity = cmp.ComponentQuantity |> Map.qty (Map.con (Constrain.setVals pc.Quantities))
            OrderableQuantity =
                cmp.OrderableQuantity
                |> Map.qty (
                    Map.con (fun cs ->
                        cs
                        |> Constrain.setIncr incr
                        |> fun cs ->
                            match pc.Solution with
                            | None -> cs
                            | Some sol -> cs |> Constrain.setVals sol.Quantities |> Constrain.setMinMax false sol.Quantity
                    )
                )
            Dose =
                if single then
                    { cmp.Dose with
                        Quantity = cmp.Dose.Quantity |> Map.qty (Map.con (Constrain.setIncr incr))
                    }
                else
                    cmp.Dose
        }


    /// What the dose rule allows of the component. Where it allows nothing, the dose quantity
    /// still gets a unit, so that it can be added up with the others.
    let withDose (med: Medication) (pc: ProductComponent) (cmp: Types.Component) =
        let zero =
            pc.Quantities
            |> Option.map ValueUnit.getUnit
            |> Option.bind (fun u -> 0N |> Constrain.single u)

        let ifGiven mm f cs =
            if mm |> MinMax.isEmpty then cs else cs |> f

        let rate (dl: DoseLimit) (dos: Dose) =
            { dos with
                Rate = dos.Rate |> Map.rte (Map.con (ifGiven dl.Rate (Constrain.setMinMax false dl.Rate)))
                RateAdjust =
                    dos.RateAdjust
                    |> Map.rteAdj (Map.con (ifGiven dl.RateAdjust (Constrain.setMinMax true dl.RateAdjust)))
            }

        let quantity (dl: DoseLimit) (dos: Dose) =
            { dos with
                Quantity =
                    dos.Quantity
                    |> Map.qty (
                        Map.con (fun cs ->
                            if dl.Quantity |> MinMax.isEmpty then
                                cs |> Constrain.setMin false zero
                            else
                                cs |> Constrain.setMinMax false dl.Quantity
                        )
                    )
                QuantityAdjust =
                    dos.QuantityAdjust
                    |> Map.qtyAdj (Map.con (ifGiven dl.QuantityAdjust (Constrain.setMinMax true dl.QuantityAdjust)))
                PerTime = dos.PerTime |> Map.ptm (Map.con (ifGiven dl.PerTime (Constrain.setMinMax false dl.PerTime)))
                PerTimeAdjust =
                    dos.PerTimeAdjust
                    |> Map.ptmAdj (Map.con (ifGiven dl.PerTimeAdjust (Constrain.setMinMax true dl.PerTimeAdjust)))
            }

        let apply =
            match med.OrderType with
            | AnyOrder
            | ProcessOrder -> fun _ dos -> dos
            | ContinuousOrder -> rate
            | OnceOrder
            | DiscontinuousOrder -> quantity
            | OnceTimedOrder
            | TimedOrder -> fun dl dos -> dos |> rate dl |> quantity dl

        match pc.Dose with
        | None -> cmp
        | Some dl -> { cmp with Dose = cmp.Dose |> apply dl }


    /// Every constraint a component carries.
    let withComponentConstraints (med: Medication) (ord: Order) =
        ord |> mapComponents (fun pc cmp -> cmp |> withQtyConc med pc |> withDose med pc) med


/// Plan 831 step B4: the orderable pass, and step B5: the prescription and the adjustment.
module ToOrderOrderable =

    open ToOrderComponents


    /// Several values in a unit, or nothing when there is no unit to give them.
    let values u brs =
        if u = NoUnit then
            None
        else
            brs |> ValueUnit.withUnit u |> Some


    /// The time unit the frequency is counted over: the denominator of a frequency's unit.
    let frequencyTimeUnit (med: Medication) =
        med.Frequencies
        |> Option.map (ValueUnit.getUnit >> ValueUnit.getUnits)
        |> function
            | Some [ _; tu ] -> Some tu
            | _ -> None


    /// How much of the orderable is given, and how often it is counted. A medication that
    /// names its quantities takes those; one that does not takes anything above nothing.
    let withQuantity (med: Medication) (ord: Order) =
        let zero = med |> orderableUnit |> Option.bind (fun u -> 0N |> Constrain.single u)

        { ord with
            Orderable =
                { ord.Orderable with
                    DoseCount = ord.Orderable.DoseCount |> Map.cnt (Map.con (Constrain.setMinMax false med.DoseCount))
                    OrderableQuantity =
                        ord.Orderable.OrderableQuantity
                        |> Map.qty (
                            Map.con (fun cs ->
                                cs
                                |> Constrain.setMinMax false med.Quantity
                                |> fun cs ->
                                    match med.Quantities with
                                    | None -> cs |> Constrain.setMin false zero
                                    | Some _ -> cs |> Constrain.setVals med.Quantities
                            )
                        )
                }
        }


    /// What the dose rule allows of the orderable as a whole. Where it allows nothing, a dose
    /// quantity and a dose per time still get a unit, so that they can take part in the sums.
    let withDose (med: Medication) (ord: Order) =
        let ou = med |> orderableUnit
        let rateUnit = ou |> Option.map (ValueUnit.per Units.Time.hour)
        let freqTimeUnit = med |> frequencyTimeUnit
        let incr = med |> divisibility None

        let rate (dl: DoseLimit option) (dos: Dose) =
            { dos with
                Rate =
                    dos.Rate
                    |> Map.rte (
                        Map.con (fun cs ->
                            // an infusion rate steps by a tenth unless something says otherwise
                            cs
                            |> Constrain.setIncr (rateUnit |> Option.bind (fun ru -> [| 1N / 10N |] |> values ru))
                            |> fun cs ->
                                match dl with
                                | None -> cs
                                | Some dl -> cs |> Constrain.setMinMax false dl.Rate
                        )
                    )
                RateAdjust =
                    match dl with
                    | None -> dos.RateAdjust
                    | Some dl -> dos.RateAdjust |> Map.rteAdj (Map.con (Constrain.setMinMax false dl.RateAdjust))
            }

        let quantity isOnce (dl: DoseLimit option) (dos: Dose) =
            let zeroQty = ou |> Option.bind (fun u -> 0N |> Constrain.single u)

            let zeroPerTime =
                match ou, freqTimeUnit with
                | Some u, Some tu -> 0N |> Constrain.single (u |> ValueUnit.per tu)
                | _ -> None

            { dos with
                Quantity =
                    dos.Quantity
                    |> Map.qty (
                        Map.con (fun cs ->
                            cs
                            |> Constrain.setIncr incr
                            |> fun cs ->
                                match dl with
                                | None -> cs |> Constrain.setMin false zeroQty
                                | Some dl ->
                                    cs
                                    |> Constrain.setMinMax false dl.Quantity
                                    |> fun cs ->
                                        if dl.Quantity |> MinMax.isEmpty then
                                            cs |> Constrain.setMin false zeroQty
                                        else
                                            cs
                        )
                    )
                QuantityAdjust =
                    match dl with
                    | None -> dos.QuantityAdjust
                    | Some dl ->
                        dos.QuantityAdjust
                        |> Map.qtyAdj (Map.con (Constrain.setMinMax true dl.QuantityAdjust))
                PerTime =
                    match dl, isOnce with
                    | None, _ -> dos.PerTime |> Map.ptm (Map.con (Constrain.setMin false zeroPerTime))
                    | Some _, true -> dos.PerTime
                    | Some dl, false ->
                        dos.PerTime
                        |> Map.ptm (
                            Map.con (fun cs ->
                                cs
                                |> Constrain.setMinMax false dl.PerTime
                                |> fun cs ->
                                    if dl.PerTime |> MinMax.isEmpty then
                                        cs |> Constrain.setMin false zeroPerTime
                                    else
                                        cs
                            )
                        )
                PerTimeAdjust =
                    match dl, isOnce with
                    | Some dl, false ->
                        dos.PerTimeAdjust
                        |> Map.ptmAdj (Map.con (Constrain.setMinMax true dl.PerTimeAdjust))
                    | _ -> dos.PerTimeAdjust
            }

        // a timed order is assumed to be a solution: what is not given a value is stepped by
        // the coarsest step the products allow
        let timed (orb: Orderable) =
            { orb with
                Dose =
                    { orb.Dose with
                        Quantity =
                            orb.Dose.Quantity
                            |> Map.qty (
                                Map.con (fun cs -> if cs.Values |> Option.isSome then cs else cs |> Constrain.setIncr incr)
                            )
                    }
                OrderableQuantity =
                    orb.OrderableQuantity
                    |> Map.qty (Map.con (fun cs -> if cs.Values |> Option.isSome then cs else cs |> Constrain.setIncr incr))
            }

        // with one component and no dose of its own, the orderable doses as that component
        let dose =
            match med.Dose, med.Components with
            | None, [ single ] -> single.Dose
            | _ -> med.Dose

        let withDoseOf f (orb: Orderable) = { orb with Dose = orb.Dose |> f }

        let orderable =
            ord.Orderable
            |> fun orb ->
                { orb with
                    OrderableQuantity = orb.OrderableQuantity |> Map.qty (Map.con (Constrain.setIncr incr))
                }
            |> fun orb ->
                match med.OrderType with
                | AnyOrder
                | ProcessOrder -> orb
                | ContinuousOrder -> orb |> withDoseOf (rate dose)
                | OnceOrder -> orb |> withDoseOf (quantity true dose)
                | OnceTimedOrder -> orb |> withDoseOf (rate dose) |> withDoseOf (quantity true dose)
                | DiscontinuousOrder -> orb |> withDoseOf (quantity false dose)
                | TimedOrder -> orb |> timed |> withDoseOf (rate dose) |> withDoseOf (quantity false dose)

        { ord with Orderable = orderable }


    /// Every constraint the orderable carries.
    let withOrderableConstraints (med: Medication) (ord: Order) =
        ord |> withQuantity med |> withDose med


    /// How often the order is given and how long an administration takes. A schedule only
    /// carries what its kind has: a once order has neither, a continuous one no frequency.
    let withPrescription (med: Medication) (ord: Order) =
        let onFrequency (frq: Frequency) =
            frq
            |> Map.frq (
                Map.con (fun cs ->
                    cs
                    |> Constrain.setVals med.Frequencies
                    |> fun cs ->
                        match med.Frequencies with
                        | None -> cs
                        // a frequency is a whole number of times
                        | Some fu -> cs |> Constrain.setIncr (1N |> Constrain.single (fu |> ValueUnit.getUnit))
                )
            )

        let onTime (tme: Time) =
            tme
            |> Map.tme (
                Map.con (fun cs ->
                    cs
                    // a lower bound the medication does not give leaves the one the empty
                    // order variable was built with, which is nothing below nothing
                    |> fun cs ->
                        match med.Time.Min with
                        | None -> cs
                        | Some _ -> cs |> Constrain.setMin true (med.Time.Min |> Option.map Limit.getValueUnit)
                    |> Constrain.setMax med.Time.Max.IsSome (med.Time.Max |> Option.map Limit.getValueUnit)
                )
            )

        { ord with
            Schedule =
                match ord.Schedule with
                | Once -> Once
                | OnceTimed tme -> tme |> onTime |> OnceTimed
                | Continuous tme -> tme |> onTime |> Continuous
                | Discontinuous frq -> frq |> onFrequency |> Discontinuous
                | Timed(frq, tme) -> (frq |> onFrequency, tme |> onTime) |> Timed
        }


    /// What the dose is adjusted to: the patient's weight or body surface. A weight is bounded
    /// by what a patient can weigh, so that a typo cannot pass for a dose.
    let withAdjustment (med: Medication) (ord: Order) =
        let bounds cs =
            match med.Adjust with
            | None -> cs
            | Some vu ->
                let u = vu |> ValueUnit.getUnit

                if u |> ValueUnit.Group.eqsGroup Units.Weight.kiloGram then
                    cs
                    |> Constrain.setMin false (200N / 1000N |> Constrain.single u)
                    |> Constrain.setMax false (150N |> Constrain.single u)
                else
                    cs

        { ord with
            Adjust = ord.Adjust |> Map.qty (Map.con (bounds >> Constrain.setVals med.Adjust))
        }


/// The pipeline, whole.
module Build =


    /// Build an Order from a Medication. Every failure is a value, the one the Dto path
    /// returned before it.
    let toOrder (med: Medication) : Result<Order, Exceptions.Message> =
        try
            med
            |> ToOrder.newOrder
            |> ToOrder.withComponents med
            |> ToOrderItems.withItemConstraints med
            |> ToOrderComponents.withComponentConstraints med
            |> ToOrderOrderable.withOrderableConstraints med
            |> ToOrderOrderable.withPrescription med
            |> ToOrderOrderable.withAdjustment med
            |> Ok
        with exn ->
            exn |> Exceptions.OrderCouldNotBeCreated |> Error


/// The nutrition path, the one place where an order has property changes applied and is
/// solved the moment it is built, rather than going through the processing pipeline.
module NutritionCheck =


    /// What `Nutrition.proc` does to an order once it has one.
    let proc (ord: Order) =
        ord
        |> Order.OrderPropertyChange.proc Nutrition.tpnConstraints
        |> Order.solveMinMax "check" true Logging.noOp
        |> function
            | Ok ord -> ord
            | Error(ord, _) -> ord


    /// Both paths through the nutrition processing, compared.
    let run toOrder =
        [ "tpn"; "tpnComplete" ]
        |> List.iter (fun name ->
            let med = Fixtures.all |> List.find (fst >> (=) name) |> snd

            match med |> Harness.viaDto, med |> toOrder with
            | Ok oldOrd, Ok newOrd ->
                let a, b = oldOrd |> proc |> Harness.normalize, newOrd |> proc |> Harness.normalize

                if a = b then
                    printfn $"%-28s{name} ok    the nutrition path agrees"
                else
                    let d =
                        List.zip (a |> Order.toString) (b |> Order.toString)
                        |> List.filter (fun (x, y) -> x <> y)
                        |> List.map (fun (x, y) -> $"  old: %s{x}\n  new: %s{y}")
                        |> String.concat "\n"

                    printfn $"%-28s{name} FAIL  the nutrition path differs\n%s{d}"
            | _ -> printfn $"%-28s{name} SKIP  a path failed"
        )


// The proof, printed when the script is run: how many order variables still disagree, then
// the orders themselves unsolved and solved, then the nutrition path.

Gauge.report Build.toOrder

Harness.run Build.toOrder

NutritionCheck.run Build.toOrder
