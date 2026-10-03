// The step the TPN intake slider needs: set the dose quantity of an order at a percentage of its
// range, also when the order was solved to one dose quantity before. The order goes back to its
// calculated constraints, the rate to the min and increment of its definition, then the percentage picks a
// dose quantity. The change-property pipeline then solves the order, as for every property change.
// Run from this directory: dotnet fsi DoseQuantityPerc.fsx (build first, so load.fsx finds the DLLs).

#I __SOURCE_DIRECTORY__

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

#load "load.fsx"
#load "../../../tests/Informedica.GenORDER.Tests/Scenarios.fs"

#r "nuget: Expecto"

open Expecto
open Expecto.Flip
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


module OrderVariable =

    module Count =

        open Informedica.GenOrder.Lib.OrderVariable
        open Informedica.GenOrder.Lib.OrderVariable.Count


        let minOne =
            Units.Count.times
            |> ValueUnit.one
            |> Variable.ValueRange.Minimum.create true
            |> Some


        /// Set a Count to min one in its values and in both its constraints, so neither brings back a
        /// count the rule fixed.
        let setMinToOneWithConstraints cnt =
            let ovar = cnt |> setMinToOne |> toOrdVar

            { ovar with
                DefinedConstraints =
                    { ovar.DefinedConstraints with
                        Min = minOne
                        Max = None
                        Values = None
                    }
                CalculatedConstraints =
                    { ovar.CalculatedConstraints with
                        Min = minOne
                        Max = None
                        Values = None
                    }
            }
            |> count


    module Quantity =

        open Informedica.GenOrder.Lib.OrderVariable
        open Informedica.GenOrder.Lib.OrderVariable.Quantity


        /// Apply only the defined min and increment of a Quantity, up to another quantity when that
        /// holds one value: a dose up to the whole orderable.
        let applyOnlyMinIncrConstraintsUpTo upTo qty =
            let max =
                (upTo |> toOrdVar).Variable
                |> Variable.getValueRange
                |> Variable.ValueRange.getValSet
                |> Option.map (Variable.ValueRange.ValueSet.toValueUnit >> Variable.ValueRange.Maximum.create true)

            let ovar = qty |> toOrdVar

            let values =
                { ovar.DefinedConstraints with
                    Max = max
                    Values = None
                }
                |> Constraints.toValueRange

            { ovar with Variable = { ovar.Variable with Values = values } } |> Quantity


    module Rate =

        open Informedica.GenOrder.Lib.OrderVariable
        open Informedica.GenOrder.Lib.OrderVariable.Rate


        /// Apply only the defined min and increment of a Rate; any positive value without them.
        let applyOnlyMinIncrConstraints = toOrdVar >> applyOnlyMinIncrConstraints >> Rate


module OrderProcessor =

    open Informedica.GenOrder.Lib.OrderProcessor
    open Order

    module Time = Informedica.GenOrder.Lib.OrderVariable.Time
    module Dose = Orderable.Dose


    /// Set the dose quantity of an order at a percentage of its range: the doses are cleared to any
    /// positive value, the orderable dose quantity to the min and increment of its definition up to
    /// the orderable quantity, the rate to the min and increment of its definition, the time back to
    /// its calculated constraints and the dose count to min one, so the orderable and component
    /// orderable quantities keep the composition the user set. Then the step picks a dose quantity.
    let orderPropertySetPercOrderableDoseQuantity step ord =
        let upToOrderable (dos: Dose) =
            let qty =
                dos.Quantity
                |> OrderVariable.Quantity.applyOnlyMinIncrConstraintsUpTo ord.Orderable.OrderableQuantity

            { dos with Quantity = qty }

        ord
        // clear the doses, the dose quantity up to the orderable, the composition stays
        |> OrderPropertyChange.proc
            [
                if ord.Schedule |> Schedule.hasTime then
                    ScheduleTime Time.applyConstraints

                OrderableDoseCount OrderVariable.Count.setMinToOneWithConstraints

                OrderableDose Dose.setToNonZeroPositive
                OrderableDose upToOrderable
                ComponentDose("", Dose.setToNonZeroPositive)
                ItemDose("", "", Dose.setToNonZeroPositive)

                OrderableDose(fun dos -> { dos with Rate = dos.Rate |> OrderVariable.Rate.applyOnlyMinIncrConstraints })
            ]
        // set the percentage
        |> OrderPropertyChange.proc [ OrderableDose step ]


module Dose = Order.Orderable.Dose


// a pipeline error returns the order as far as it got, so it is a failure, not an order
let run cmd ord =
    ord
    |> cmd
    |> OrderProcessor.processPipeline OrderLogging.noOp
    |> function
        | Ok ord -> ord
        | Error(_, msgs) ->
            let msg = $"%A{msgs}"
            invalidOp msg[.. 300]


// the slider's command as the processor would run it: the new step, then the solve the
// change-property pipeline runs after every property change
let perc p (ord: Order) =
    ord
    |> OrderProcessor.orderPropertySetPercOrderableDoseQuantity (Dose.setPercValue p ord.Schedule false)
    |> Order.calcMinMax OrderLogging.noOp
    |> function
        | Ok ord -> ord
        | Error(_, msgs) ->
            let msg = $"%A{msgs}"
            invalidOp msg[.. 300]


let doseQty (ord: Order) =
    ord.Orderable.Dose.Quantity |> OrderVariable.Quantity.toOrdVar


let show ord =
    ord |> doseQty |> OrderVariable.toString false


// the value of the dose quantity, when it holds one
let value ord =
    (ord |> doseQty).Variable
    |> Variable.getValueRange
    |> Variable.ValueRange.getValSet
    |> Option.map (Variable.ValueRange.ValueSet.toValueUnit >> ValueUnit.getValue)


// The per-kg limits of a component belong to its orderable quantity, the composition, not to its
// dose: a part of the orderable may be given. So each component dose limit becomes a solution limit
// on the component, made absolute by the weight as Medication.create does for a solution rule.
let withComponentSolutionLimits (med: Medication) =
    let toSolution (pc: ProductComponent) =
        match pc.Dose, med.Adjust with
        | Some dl, Some weight ->
            { pc with
                Dose = None
                Solution =
                    { SolutionLimit.limit with
                        SolutionLimitTarget = pc.Name |> ComponentLimitTarget
                        Quantity = dl.QuantityAdjust |> MinMax.apply ((*) weight)
                    }
                    |> Some
            }
        | _ -> pc

    { med with Components = med.Components |> List.map toSolution }


// the orderable dose limited by nothing per kg: the composition and the time decide the dose
let withoutOrderableDoseQuantityAdjust (med: Medication) =
    { med with Dose = med.Dose |> Option.map (fun dl -> { dl with QuantityAdjust = MinMax.empty }) }


// gluc 10% fills the TPN up, between 65 and 80 mL/kg, made absolute by the weight
let withGlucoseSolutionLimit (med: Medication) =
    let mlPerKg n =
        n
        |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> ValueUnit.per Units.Weight.kiloGram)
        |> Limit.inclusive
        |> Some

    let limit (pc: ProductComponent) =
        match med.Adjust with
        | Some weight when pc.Name = "gluc 10%" ->
            { pc with
                Solution =
                    { SolutionLimit.limit with
                        SolutionLimitTarget = pc.Name |> ComponentLimitTarget
                        Quantity =
                            { MinMax.empty with
                                Min = mlPerKg 65N
                                Max = mlPerKg 80N
                            }
                            |> MinMax.apply ((*) weight)
                    }
                    |> Some
            }
        | _ -> pc

    { med with Components = med.Components |> List.map limit }


let tpn =
    Scenarios.tpnComplete
    |> withComponentSolutionLimits
    |> withGlucoseSolutionLimit
    |> withoutOrderableDoseQuantityAdjust


// the orderable dose quantity defined in steps of 0.1 mL, finer than the 1 mL the products give
let withOrderableDoseQuantityIncrement (ord: Order) =
    let incr = 1N / 10N |> ValueUnit.singleWithUnit Units.Volume.milliLiter |> Some

    let qty =
        ord.Orderable.Dose.Quantity
        |> OrderVariable.Quantity.toOrdVar
        |> OrderVariable.mapConstraints (OrderVariable.Constraints.setIncr incr)
        |> Quantity

    { ord with
        Orderable =
            { ord.Orderable with
                Dose = { ord.Orderable.Dose with Quantity = qty }
            }
    }


// the order as the server selects it
let selected =
    tpn
    |> Medication.toOrder Scenarios.testStart
    |> Result.get
    |> withOrderableDoseQuantityIncrement
    |> run CalcMinMax
    |> run CalcValues


// the TPN as the user composes it: every component orderable quantity solved to one value, here
// its median
let composed =
    let setMedian ord (cmp: Component) =
        ord
        |> run (fun o -> ChangeProperty(o, SetMedianComponentOrderableQuantity(cmp.Name |> Name.toString)))

    selected.Orderable.Components |> List.fold setMedian selected


// the composed TPN solved to one dose quantity by a first move
let once = composed |> perc 100


// the value of the orderable quantity, when it holds one
let orderableValue (ord: Order) =
    (ord.Orderable.OrderableQuantity |> OrderVariable.Quantity.toOrdVar).Variable
    |> Variable.getValueRange
    |> Variable.ValueRange.getValSet
    |> Option.map (Variable.ValueRange.ValueSet.toValueUnit >> ValueUnit.getValue)


/// Print the test fixture: the medication with its limits, and the composed order as a table.
let printFixture () =
    printfn "== The medication"
    tpn |> Medication.toString |> List.iter (printfn "%s")
    printfn ""
    printfn "== The composed order"
    composed |> Order.toConsoleTableString |> printfn "%s"


let percentages = [ 10..10..100 ]


let tests =
    testList "orderPropertySetPercOrderableDoseQuantity on a TPN order" [
        test "the TPN order is timed, so the dose quantity is set and not the rate" {
            selected.Schedule.IsTimed |> Expect.isTrue "should be timed"
        }

        test "the fixture limits the component orderable quantities, not the component doses" {
            tpn.Components
            |> List.forall (fun pc -> pc.Dose.IsNone && pc.Solution.IsSome)
            |> Expect.isTrue "every component should have a solution limit and no dose limit"
        }

        test "the fixture is within its constraints" {
            composed |> Order.checkConstraints false
            |> Expect.isEmpty "no order variable should be outside its constraints"
        }

        for p in percentages do
            test $"a move to %i{p}%% keeps the order within its constraints" {
                composed |> perc p |> Order.checkConstraints false
                |> List.map (OrderVariable.toString false)
                |> Expect.isEmpty "no order variable should be outside its constraints"
            }

        test "the fixture has every component orderable quantity solved to one value" {
            composed.Orderable.Components
            |> List.forall (fun cmp ->
                (cmp.OrderableQuantity |> OrderVariable.Quantity.toOrdVar).Variable |> Variable.count = 1
            )
            |> Expect.isTrue "every component orderable quantity should be one value"
        }

        for name, start in [ "a first move", composed; "a move after 100%", once ] do
            for p in percentages do
                test $"%s{name} to %i{p}%% sets one dose quantity" {
                    (start |> perc p |> doseQty).Variable
                    |> Variable.count
                    |> Expect.equal "should be one value" 1
                }

            test $"%s{name}: a higher percentage gives a higher dose quantity" {
                percentages
                |> List.map (fun p -> start |> perc p |> value)
                |> List.pairwise
                |> List.forall (fun (lower, higher) -> lower < higher)
                |> Expect.isTrue "the dose quantities should rise with the percentage"
            }

        test "a move after 100% gives the dose quantity of a first move" {
            let firstMoves = percentages |> List.map (fun p -> composed |> perc p |> value)

            percentages
            |> List.map (fun p -> once |> perc p |> value)
            |> Expect.equal "should be the same" firstMoves
        }

        test "100% gives the whole orderable quantity" {
            composed |> perc 100 |> value
            |> Expect.equal "should be the orderable quantity" (composed |> orderableValue)
        }

        test "the composition stays as the user set it" {
            let quantities (ord: Order) =
                ord.Orderable.Components
                |> List.map (_.OrderableQuantity >> OrderVariable.Quantity.toOrdVar >> _.Variable)

            composed |> perc 50 |> quantities
            |> Expect.equal "the component orderable quantities should not change" (composed |> quantities)
        }
    ]


printFixture ()


runTestsWithCLIArgs [] [||] tests
