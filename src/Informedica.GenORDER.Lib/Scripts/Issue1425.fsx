// Issue #1425 with #1421: timed orders.
//
// Probe 1: does the time times the rate narrow the orderable dose quantity of a timed order? Each
// timed fixture is processed as is, and again with the defined constraints of the time and the
// orderable dose rate removed, so only the component and item restrictions bound the dose quantity.

#I __SOURCE_DIRECTORY__
System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

#load "load.fsx"

open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib

module Quantity = Informedica.GenOrder.Lib.OrderVariable.Quantity
module Rate = Informedica.GenOrder.Lib.OrderVariable.Rate
module Time = Informedica.GenOrder.Lib.OrderVariable.Time


let logger = Logging.noOp


let parse text =
    match text |> MedicationText.fromString with
    | Ok med -> med
    | Error errs -> invalidOp $"could not parse: %A{errs}"



let fixtures () =
    [
        "tpn", Scenarios.tpn
        "tpn without KCl",
        { Scenarios.tpn with Components = Scenarios.tpn.Components |> List.filter (fun c -> c.Name <> "KCl 7,4%") }
        "tpnComplete", Scenarios.tpnComplete
        "fullMedication", Scenarios.fullMedication
        "kcl once timed", Scenarios.kaliumchlorideOnceTimedText |> parse
        "amfo timed", Scenarios.amfoTimed
    ]


let run cmd ord = ord |> cmd |> OrderProcessor.processPipeline logger


let stages =
    [
        "calc-minmax", run CalcMinMax
        "increase-increments", run IncreaseIncrements
        "calc-values", run CalcValues
        "solve-order", run SolveOrder
    ]


/// The order with no defined constraints on the time and the orderable dose rate.
let freeTimeAndRate (ord: Order) =
    let none = OrderVariable.Constraints.create None None None None
    let free (ovar: OrderVariable) = { ovar with DefinedConstraints = none }

    let tme =
        ord.Schedule |> Order.Schedule.toOrdVars |> snd |> Option.map free |> Option.toList

    let rte = ord.Orderable.Dose.Rate |> Rate.toOrdVar |> free
    ord |> Order.fromOrdVars (rte :: tme)


let show (ord: Order) =
    let s ovar = ovar |> OrderVariable.toString false

    [
        "  dose qty: " + (ord.Orderable.Dose.Quantity |> Quantity.toOrdVar |> s)
        "  rate:     " + (ord.Orderable.Dose.Rate |> Rate.toOrdVar |> s)
        "  time:     " + (ord.Schedule |> Order.Schedule.toOrdVars |> snd |> Option.map s |> Option.defaultValue "-")
    ]
    |> String.concat "\n"


let probe label (ord: Order) =
    printfn $"--- %s{label}"

    stages
    |> List.fold
        (fun acc (name, step) ->
            match acc with
            | Error _ -> acc
            | Ok ord ->
                let sw = System.Diagnostics.Stopwatch.StartNew()
                let res = ord |> step
                sw.Stop()

                match res with
                | Ok o ->
                    printfn $" %s{name} (%i{sw.ElapsedMilliseconds} ms)\n%s{o |> show}"
                    Ok o
                | Error(_, msgs) ->
                    printfn $" %s{name} FAILED (%i{sw.ElapsedMilliseconds} ms): %A{msgs |> List.truncate 1}"
                    Error()
        )
        (Ok ord)
    |> ignore


for name, med in fixtures () do
    match med |> Medication.toOrder Scenarios.testStart with
    | Error e -> printfn $"%s{name}: could not build the order: %A{e}"
    | Ok ord ->
        printfn $"\n===== %s{name}"
        ord |> probe "as is"
        ord |> freeTimeAndRate |> probe "time and rate free"


// Probe 2 (#1421): the cost of the item orderable concentrations in an order with more than two
// components. Every value of each component quantity is picked on the solved order and solved with
// every equation, and with the equations that hold an item orderable concentration left out.

module Mapping = EquationMapping


let solveWith keep (ord: Order) =
    let mapping =
        EquationMapping.Literals.timed
        |> Mapping.getEquations
        |> List.filter keep
        |> Mapping.getEqsMapping ord

    let oEqs = ord |> Order.mapToOrderEquations mapping

    oEqs
    |> Solver.mapToSolverEqs
    |> Solver.solve logger
    |> Result.map (Solver.mapToOrderEqs oEqs >> Order.mapFromOrderEquations ord)


let all (_: string) = true
let noItemOrbCnc (e: string) = e.Contains "[itm]_orb_cnc" |> not


let valuesOf ovar =
    ovar
    |> OrderVariable.getVar
    |> Variable.getValueRange
    |> Variable.ValueRange.getValSet
    |> Option.map (Variable.ValueRange.ValueSet.toValueUnit >> ValueUnit.getValue)
    |> Option.defaultValue [||]


let timePicks label keep (ord: Order) =
    let results =
        [
            for cmp in ord.Orderable.Components do
                let ovar = cmp.OrderableQuantity |> Quantity.toOrdVar

                for nth in 0 .. (ovar |> valuesOf |> Array.length) - 1 do
                    let picked = ovar |> OrderVariable.setNthValue nth
                    let sw = System.Diagnostics.Stopwatch.StartNew()
                    let res = ord |> Order.fromOrdVars [ picked ] |> solveWith keep
                    sw.Stop()
                    res |> Result.isOk, sw.ElapsedMilliseconds
        ]

    let ms = results |> List.map snd
    let failed = results |> List.filter (fst >> not) |> List.length

    printfn
        $"  %s{label}: %i{results.Length} picks, %i{failed} failed, max %i{List.max ms} ms, mean %i{List.sum ms / int64 ms.Length} ms"


for name, med in fixtures () |> List.filter (fun (n, _) -> n.StartsWith "tpn") do
    match med |> Medication.toOrder Scenarios.testStart with
    | Error e -> printfn $"%s{name}: could not build the order: %A{e}"
    | Ok ord ->
        let solved =
            stages
            |> List.fold (fun acc (_, step) -> acc |> Result.bind step) (Ok ord)

        match solved with
        | Error _ -> printfn $"%s{name}: the processing failed"
        | Ok ord ->
            printfn $"\n===== %s{name}, component quantity picks"
            ord |> timePicks "every equation" all
            ord |> timePicks "no item orderable concentration" noItemOrbCnc


// Probe 3: the same picks, every other variable compared with the full solve. Without the item
// orderable concentrations a timed order ties an item's rate to the orderable rate only through the
// component rate, which is mapped for continuous orders alone; the variant adds it for timed.

let cmpRate = "[cmp]_dos_rte = [cmp]_orb_cnc * [orb]_dos_rte"

let solveWithExtra keep extra (ord: Order) =
    let mapping =
        (EquationMapping.Literals.timed |> Mapping.getEquations |> List.filter keep) @ extra
        |> Mapping.getEqsMapping ord

    let oEqs = ord |> Order.mapToOrderEquations mapping

    oEqs
    |> Solver.mapToSolverEqs
    |> Solver.solve logger
    |> Result.map (Solver.mapToOrderEqs oEqs >> Order.mapFromOrderEquations ord)


let varText (ord: Order) =
    ord
    |> Order.toOrdVars
    |> List.map (fun ov -> ov |> OrderVariable.getName |> Variable.Name.toString, ov |> OrderVariable.toString false)
    |> List.filter (fun (n, _) -> n.EndsWith "_orb_cnc" |> not)
    |> Map.ofList


let compare label solveB (ord: Order) =
    let diffs =
        [
            for cmp in ord.Orderable.Components do
                let ovar = cmp.OrderableQuantity |> Quantity.toOrdVar

                for nth in 0 .. (ovar |> valuesOf |> Array.length) - 1 do
                    let picked = ord |> Order.fromOrdVars [ ovar |> OrderVariable.setNthValue nth ]

                    match picked |> solveWith all, picked |> solveB with
                    | Ok a, Ok b ->
                        let a, b = varText a, varText b

                        for KeyValue(n, s) in a do
                            if b[n] <> s then
                                n, s, b[n]
                    | ra, rb -> "solve", $"%b{Result.isOk ra}", $"%b{Result.isOk rb}"
        ]

    printfn $"  %s{label}: %i{diffs.Length} differences"

    for n, a, b in diffs |> List.distinctBy (fun (n, _, _) -> n) |> List.truncate 8 do
        printfn $"    %s{n}\n      full:    %s{a}\n      variant: %s{b}"


for name, med in fixtures () |> List.filter (fun (n, _) -> n = "tpn" || n = "tpnComplete") do
    match med |> Medication.toOrder Scenarios.testStart with
    | Error _ -> ()
    | Ok ord ->
        match stages |> List.fold (fun acc (_, step) -> acc |> Result.bind step) (Ok ord) with
        | Error _ -> ()
        | Ok ord ->
            printfn $"\n===== %s{name}, compared with the full solve"
            ord |> compare "no item orderable concentration" (solveWith noItemOrbCnc)
            ord |> compare "no item orderable concentration, component rate" (solveWithExtra noItemOrbCnc [ cmpRate ])


for name, med in fixtures () |> List.filter (fun (n, _) -> n = "tpn" || n = "tpnComplete") do
    match med |> Medication.toOrder Scenarios.testStart with
    | Error _ -> ()
    | Ok ord ->
        match stages |> List.fold (fun acc (_, step) -> acc |> Result.bind step) (Ok ord) with
        | Error _ -> ()
        | Ok ord ->
            printfn $"\n===== %s{name}, the variant timed"
            ord |> timePicks "every equation" all

            let variant = solveWithExtra noItemOrbCnc [ cmpRate ]

            // timePicks with the variant's solve
            let results =
                [
                    for cmp in ord.Orderable.Components do
                        let ovar = cmp.OrderableQuantity |> Quantity.toOrdVar

                        for nth in 0 .. (ovar |> valuesOf |> Array.length) - 1 do
                            let picked = ord |> Order.fromOrdVars [ ovar |> OrderVariable.setNthValue nth ]
                            let sw = System.Diagnostics.Stopwatch.StartNew()
                            let ok = picked |> variant |> Result.isOk
                            sw.Stop()
                            ok, sw.ElapsedMilliseconds
                ]

            let ms = results |> List.map snd

            printfn
                $"  variant: %i{results.Length} picks, %i{results |> List.filter (fst >> not) |> List.length} failed, max %i{List.max ms} ms, mean %i{List.sum ms / int64 ms.Length} ms"
