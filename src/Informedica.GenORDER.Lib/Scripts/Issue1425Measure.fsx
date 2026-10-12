// Issue #1425 with #1421: the whole processing and every pick of the dose dialog, today and with
// the changes. Run it twice and compare:
//
//   dotnet fsi Issue1425Measure.fsx                    the sources as they are today
//   dotnet fsi --define:VARIANT Issue1425Measure.fsx   the sources with the changes:
//     - with --define:RATEWINDOW, Issue1425Order.fsx: the rate step of a timed order fits the
//       rate window (#1425)
//     - Issue1425EquationMapping.fsx: the item orderable concentrations of a non-continuous
//       order with more than two components through the tie alone, and the component rate tied
//       to the orderable rate for timed orders (#1421)
//   with --define:DROPALL or --define:NOCMPRATE in addition: the rejected versions, see
//   Issue1425EquationMapping.fsx

#I __SOURCE_DIRECTORY__
System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

#load "Issue1425Load.fsx"

open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib

module Quantity = Informedica.GenOrder.Lib.OrderVariable.Quantity
module Rate = Informedica.GenOrder.Lib.OrderVariable.Rate
module Concentration = Informedica.GenOrder.Lib.OrderVariable.Concentration


let logger = Logging.noOp


let parse text =
    match text |> Medication.fromString with
    | Ok med -> med
    | Error errs -> invalidOp $"could not parse: %A{errs}"


/// Amfo as a timed order over one to two hours: two components, the dose quantity a range.
let amfoTimed =
    { Scenarios.amfo with
        OrderType = TimedOrder
        Time =
            { MinMax.empty with
                Min = 1N |> ValueUnit.singleWithUnit Units.Time.hour |> Limit.inclusive |> Some
                Max = 2N |> ValueUnit.singleWithUnit Units.Time.hour |> Limit.inclusive |> Some
            }
    }


/// Amfo as a timed order over exactly two hours: a fixed time, so the rate alone sets the dose
/// quantity.
let amfoTimedFixed =
    { amfoTimed with
        Time =
            { MinMax.empty with
                Min = 2N |> ValueUnit.singleWithUnit Units.Time.hour |> Limit.inclusive |> Some
                Max = 2N |> ValueUnit.singleWithUnit Units.Time.hour |> Limit.inclusive |> Some
            }
    }


/// The tpn with the kalium of KCl 7,4% at most 0.04 mmol/mL in the orderable, below the 0.02 to
/// 0.06 mmol/mL the component list allows: the limit binds.
let tpnBinding =
    let bind (sub: Types.SubstanceItem) =
        match sub.Solution with
        | Some sl when sub.Name = "kalium" ->
            { sub with
                Solution =
                    { sl with
                        Concentration =
                            { sl.Concentration with
                                Max =
                                    (4N / 100N)
                                    |> ValueUnit.singleWithUnit (
                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                    )
                                    |> Limit.inclusive
                                    |> Some
                            }
                    }
                    |> Some
            }
        | _ -> sub

    { Scenarios.tpn with
        Components =
            Scenarios.tpn.Components
            |> List.map (fun c ->
                if c.Name = "KCl 7,4%" then
                    { c with Substances = c.Substances |> List.map bind }
                else
                    c
            )
    }


/// The tpn with the kalium of KCl 7,4% at most 0.09 mmol/kg/uur: its 2 mL/kg over 20 to 24 uur runs at 0.083 to 0.1 mmol/kg/uur,
/// so the limit binds the time and the rate of a timed order.
let tpnRateBinding =
    let du =
        Units.Molar.milliMole
        |> ValueUnit.per Units.Weight.kiloGram
        |> ValueUnit.per Units.Time.hour

    let bind (sub: Types.SubstanceItem) =
        if sub.Name = "kalium" then
            { sub with
                Dose =
                    { DoseLimit.limit with
                        DoseLimitTarget = "kalium" |> LimitTarget.SubstanceLimitTarget
                        AdjustUnit = Units.Weight.kiloGram |> Some
                        RateAdjust =
                            { MinMax.empty with
                                Max = (9N / 100N) |> ValueUnit.singleWithUnit du |> Limit.inclusive |> Some
                            }
                    }
                    |> Some
            }
        else
            sub

    { Scenarios.tpn with
        Components =
            Scenarios.tpn.Components
            |> List.map (fun c ->
                if c.Name = "KCl 7,4%" then
                    { c with Substances = c.Substances |> List.map bind }
                else
                    c
            )
    }


/// The medication with a third component, NaCl 0,9%, a copy of its last component with natrium
/// 0.154 mmol/mL: an order with more than two components on another schedule than timed.
let withNaCl (med: Types.Medication) =
    let last = med.Components |> List.last

    let natrium =
        { (last.Substances |> List.head) with
            Name = "natrium"
            Concentrations =
                (154N / 1000N)
                |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter)
                |> Some
            Dose = None
            Solution = None
        }

    { med with
        Components =
            med.Components
            @ [
                { last with
                    Name = "NaCl 0,9%"
                    Dose = None
                    Substances = [ natrium ]
                }
            ]
    }


/// Amfo with NaCl 0,9% and the amfotericine b liposomaal of its first component in two strengths,
/// 4 and 2 mg/mL: a discontinuous order with more than two components and an item with more than
/// one component concentration.
let amfoTwoStrengths =
    let med = Scenarios.amfo |> withNaCl
    let first = med.Components |> List.head

    let twoStrengths (sub: Types.SubstanceItem) =
        if sub.Name = "amfotericine b liposomaal" then
            { sub with
                Concentrations =
                    [| 2N; 4N |]
                    |> ValueUnit.withUnit (Units.Mass.milliGram |> ValueUnit.per Units.Volume.milliLiter)
                    |> Some
            }
        else
            sub

    { med with
        Components =
            { first with Substances = first.Substances |> List.map twoStrengths }
            :: (med.Components |> List.tail)
    }


/// The same, as a timed order over one to two hours.
let amfoTwoStrengthsTimed =
    { amfoTwoStrengths with
        OrderType = TimedOrder
        Time = amfoTimed.Time
    }


/// morfCont with NaCl 0,9% as a timed order, once a day over 20 to 24 uur: a fixed orderable
/// quantity of 50 mL, a morfine quantity fixed at 10 mg by its solution limit, morfine in two
/// strengths and a morfine rate limit, on a schedule the rule applies to.
let morfTimedWithNaCl =
    { (Scenarios.morfCont |> withNaCl) with
        OrderType = TimedOrder
        Frequencies =
            Units.Count.times
            |> ValueUnit.per Units.Time.day
            |> ValueUnit.singleWithValue 1N
            |> Some
        Time =
            { MinMax.empty with
                Min = 20N |> ValueUnit.singleWithUnit Units.Time.hour |> Limit.inclusive |> Some
                Max = 24N |> ValueUnit.singleWithUnit Units.Time.hour |> Limit.inclusive |> Some
            }
    }


/// morfCont with NaCl 0,9% without the exact morfine quantity of its solution limit: continuous with
/// more than two components, no item orderable quantity fixed.
let morfContWithNaClFree =
    let med = Scenarios.morfCont |> withNaCl
    let first = med.Components |> List.head

    let free (sub: Types.SubstanceItem) =
        match sub.Solution with
        | Some sl -> { sub with Solution = { sl with Quantity = MinMax.empty } |> Some }
        | None -> sub

    { med with
        Components =
            { first with Substances = first.Substances |> List.map free }
            :: (med.Components |> List.tail)
    }


/// morfCont with NaCl 0,9% and morfine in one strength, 1 mg/mL: continuous with more than two
/// components, no item in more than one strength.
let morfContWithNaClOneStrength =
    let med = Scenarios.morfCont |> withNaCl
    let first = med.Components |> List.head

    let one (sub: Types.SubstanceItem) =
        { sub with
            Concentrations =
                1N
                |> ValueUnit.singleWithUnit (Units.Mass.milliGram |> ValueUnit.per Units.Volume.milliLiter)
                |> Some
        }

    { med with
        Components =
            { first with Substances = first.Substances |> List.map one }
            :: (med.Components |> List.tail)
    }


/// The fixtures of the dose dialog tests, and the timed ones.
let fixtures () =
    [
        "amox/clav", Scenarios.amoxClavText |> parse
        "amfo", Scenarios.amfo
        "amfo timed", amfoTimed
        "amfo over 2 uur", amfoTimedFixed
        "morfCont", Scenarios.morfCont
        "morfCont with NaCl", Scenarios.morfCont |> withNaCl
        "morfine timed with NaCl", morfTimedWithNaCl
        "morfCont with NaCl, no fixed quantity", morfContWithNaClFree
        "morfCont with NaCl, one strength", morfContWithNaClOneStrength
        "amfo with NaCl", Scenarios.amfo |> withNaCl
        "amfo with NaCl, two strengths", amfoTwoStrengths
        "amfo with NaCl, two strengths, timed", amfoTwoStrengthsTimed
        "tpn without KCl",
        { Scenarios.tpn with Components = Scenarios.tpn.Components |> List.filter (fun c -> c.Name <> "KCl 7,4%") }
        "tpn", Scenarios.tpn
        "tpnComplete", Scenarios.tpnComplete
        "tpn, kalium limit binding", tpnBinding
        "tpn, kalium rate limit binding", tpnRateBinding
        "kcl once timed", Scenarios.kaliumchlorideOnceTimedText |> parse
        "pcmSupp", Scenarios.pcmSupp
        "pcmDrink", Scenarios.pcmDrink
        "cotrim", Scenarios.cotrim
        "prednisolon", Scenarios.prednisolonText |> parse
    ]


let run cmd ord =
    ord |> cmd |> OrderProcessor.processPipeline logger


let valuesOf ovar =
    ovar
    |> OrderVariable.getVar
    |> Variable.getValueRange
    |> Variable.ValueRange.getValSet
    |> Option.map (Variable.ValueRange.ValueSet.toValueUnit >> ValueUnit.getValue)
    |> Option.defaultValue [||]


/// The lists the dialog offers as picks, as ReachablePickTests takes them.
let offered (ord: Order) =
    let cmps = ord.Orderable.Components

    [
        for cmp in cmps do
            cmp.OrderableQuantity |> Quantity.toOrdVar

            if cmps |> List.length <= 2 then
                for itm in cmp.Items do
                    itm.OrderableConcentration |> Concentration.toOrdVar
        ord.Orderable.Dose.Rate |> Rate.toOrdVar
    ]
    |> List.filter (fun ovar -> ovar |> valuesOf |> Array.length > 1)


let nameOf ovar =
    ovar |> OrderVariable.getName |> Variable.Name.toString


let short (s: string) =
    if s.Length <= 90 then s else s[.. 86] + "..."


let measure name med =
    match med |> Medication.toOrder Scenarios.testStart with
    | Error e -> printfn $"%s{name}: could not build the order: %A{e}"
    | Ok ord ->
        let sw = System.Diagnostics.Stopwatch.StartNew()
        let minMax = ord |> run CalcMinMax

        let solved =
            minMax
            |> Result.bind (run IncreaseIncrements)
            |> Result.bind (run CalcValues)
            |> Result.bind (run SolveOrder)

        sw.Stop()

        printfn $"\n===== %s{name}: processing %i{sw.ElapsedMilliseconds} ms"

        match minMax with
        | Ok o ->
            printfn
                $"  after calc-minmax, dose qty: %s{o.Orderable.Dose.Quantity |> Quantity.toOrdVar |> OrderVariable.toString false |> short}"
        | Error _ -> printfn "  calc-minmax failed"

        match solved with
        | Error(_, msgs) -> printfn $"  processing failed: %A{msgs |> List.truncate 1}"
        | Ok ord ->
            printfn
                $"  dose qty: %s{ord.Orderable.Dose.Quantity |> Quantity.toOrdVar |> OrderVariable.toString false |> short}"

            printfn $"  rate:     %s{ord.Orderable.Dose.Rate |> Rate.toOrdVar |> OrderVariable.toString false |> short}"

            let results =
                [
                    for ovar in ord |> offered do
                        for nth in 0 .. (ovar |> valuesOf |> Array.length) - 1 do
                            let picked = ovar |> OrderVariable.setNthValue nth
                            let sw = System.Diagnostics.Stopwatch.StartNew()

                            let res =
                                ord |> Order.fromOrdVars [ picked ] |> Order.solveOrder "pick" false logger

                            sw.Stop()
                            nameOf ovar, res |> Result.isOk, sw.ElapsedMilliseconds
                ]

            for ovar in ord |> offered do
                printfn $"  offers %3i{ovar |> valuesOf |> Array.length} of %s{nameOf ovar |> short}"

            let failing = results |> List.filter (fun (_, ok, _) -> not ok)
            let ms = results |> List.map (fun (_, _, ms) -> ms)

            if ms.IsEmpty then
                printfn "  no picks"
            else
                printfn
                    $"  %i{results.Length} picks, %i{failing.Length} failing, slowest %i{List.max ms} ms, mean %i{List.sum ms / int64 ms.Length} ms"

            for n in failing |> List.map (fun (n, _, _) -> n) |> List.distinct do
                printfn $"    failing: %s{n |> short}"


// the measurement only when this script runs itself, not when Issue1425Compare.fsx loads it
if fsi.CommandLineArgs |> Array.exists (fun a -> a.EndsWith "Issue1425Measure.fsx") then
    for name, med in fixtures () do
        measure name med
