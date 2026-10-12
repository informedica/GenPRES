// Issue #1425: does a solve that is not a min and max solve turn the orderable rate of a timed
// order into values while the rate stage is held back? Per timed and once timed fixture of
// Issue1425Measure.fsx: calc-minmax, the quantity values with the rate held back, two
// solves, a pick of the middle dose quantity, and the rate values. Then the same fixtures through
// the pipelines: CalcMinMax, CalcValues and SolveOrder, and a dose quantity pick solved by
// SolveOrder. Run from this directory, the sources as they are and with the staged rate:
//
//   dotnet fsi Issue1425Probe.fsx
//   dotnet fsi --define:STAGED Issue1425Probe.fsx

#I __SOURCE_DIRECTORY__
System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

#load "Issue1425Measure.fsx"

open Informedica.GenSolver.Lib
open Informedica.GenOrder.Lib
open Issue1425Measure

module Quantity = Informedica.GenOrder.Lib.OrderVariable.Quantity
module Rate = Informedica.GenOrder.Lib.OrderVariable.Rate
module Time = Informedica.GenOrder.Lib.OrderVariable.Time


let kind ovar =
    let vr = ovar |> OrderVariable.getVar |> Variable.getValueRange

    match vr |> Variable.ValueRange.getValSet with
    | Some _ -> $"list of %i{ovar |> valuesOf |> Array.length}"
    | None -> "range"


let show label (ord: Order) =
    let tme =
        match ord.Schedule with
        | Timed(_, t)
        | OnceTimed t -> t |> Time.toOrdVar |> fun o -> $"%s{kind o}: %s{o |> OrderVariable.toString false}"
        | _ -> "-"

    let qty = ord.Orderable.Dose.Quantity |> Quantity.toOrdVar
    let rte = ord.Orderable.Dose.Rate |> Rate.toOrdVar
    printfn $"  %s{label}"
    printfn $"    qty  %s{kind qty}: %s{qty |> OrderVariable.toString false |> short}"
    printfn $"    rate %s{kind rte}: %s{rte |> OrderVariable.toString false |> short}"
    printfn $"    time %s{tme |> short}"


let caps = Order.ValueCaps.defaults

let timed =
    fixtures ()
    |> List.filter (fun (_, med) ->
        med.OrderType = Types.TimedOrder || med.OrderType = Types.OnceTimedOrder
    )

for name, med in timed do
    printfn $"\n===== %s{name}"

    match med |> Medication.toOrder Scenarios.testStart with
    | Error _ -> printfn "could not build"
    | Ok ord ->
        match ord |> run CalcMinMax with
        | Error _ -> printfn "  calc-minmax failed"
        | Ok ord ->
            ord |> show "after calc-minmax"

            match ord |> Order.minIncrMaxToValues caps true true logger with
            | Error _ -> printfn "  quantity values failed"
            | Ok ord ->
                ord |> show "after the quantity values, rate held back (solveOrder ran)"

                match ord |> Order.solveOrder "again" false logger with
                | Error _ -> printfn "  second solve failed"
                | Ok ord ->
                    ord |> show "after a second solveOrder"

                    let qty = ord.Orderable.Dose.Quantity |> Quantity.toOrdVar
                    let n = qty |> valuesOf |> Array.length

                    if n > 1 then
                        match
                            ord
                            |> Order.fromOrdVars [ qty |> OrderVariable.setNthValue (n / 2) ]
                            |> Order.solveOrder "pick qty" false logger
                        with
                        | Error _ -> printfn "  dose quantity pick failed"
                        | Ok ord ->
                            ord |> show "after a dose quantity pick (solveOrder ran)"

                            match ord |> Order.minIncrMaxToValues caps true false logger with
                            | Error _ -> printfn "  rate values failed"
                            | Ok ord -> ord |> show "after the rate values"


// The same fixtures through the pipelines, as the server runs them: CalcMinMax when the scenario
// is made, CalcValues when it is selected, SolveOrder after it; then a pick of the middle dose
// quantity, solved by SolveOrder, as a pick from the dose dialog is.
printfn "\n\n######## the pipelines"

for name, med in timed do
    printfn $"\n===== %s{name}"

    match med |> Medication.toOrder Scenarios.testStart with
    | Error _ -> printfn "could not build"
    | Ok ord ->
        match ord |> run CalcMinMax with
        | Error _ -> printfn "  CalcMinMax failed"
        | Ok ord ->
            ord |> show "after CalcMinMax"

            match ord |> run CalcValues |> Result.bind (run SolveOrder) with
            | Error _ -> printfn "  CalcValues and SolveOrder failed"
            | Ok ord ->
                ord |> show "after CalcValues and SolveOrder"

                let qty = ord.Orderable.Dose.Quantity |> Quantity.toOrdVar
                let n = qty |> valuesOf |> Array.length

                if n > 1 then
                    match
                        ord
                        |> Order.fromOrdVars [ qty |> OrderVariable.setNthValue (n / 2) ]
                        |> run SolveOrder
                    with
                    | Error _ -> printfn "  dose quantity pick failed"
                    | Ok ord -> ord |> show "after a dose quantity pick and SolveOrder"


// Completed as the dose dialog completes an order: each component quantity picked in turn, each
// pick solved by SolveOrder, so the rate stage runs once the dose quantity has one value.
printfn "\n\n######## completed through SolveOrder"

let completeBySolveOrder (ord: Order) =
    ord.Orderable.Components
    |> List.map _.Name
    |> List.fold
        (fun (o: Order) name ->
            let cmp = o.Orderable.Components |> List.find (fun c -> c.Name = name)
            let ovar = cmp.OrderableQuantity |> Quantity.toOrdVar

            if ovar |> valuesOf |> Array.length <= 1 then
                o
            else
                o
                |> Order.fromOrdVars [ ovar |> OrderVariable.setNthValue 0 ]
                |> run SolveOrder
                |> Result.defaultValue o
        )
        ord


for name, med in timed do
    printfn $"\n===== %s{name}"

    match
        med
        |> Medication.toOrder Scenarios.testStart
        |> Result.mapError ignore
        |> Result.bind (run CalcMinMax >> Result.mapError ignore)
        |> Result.bind (run CalcValues >> Result.mapError ignore)
        |> Result.bind (run SolveOrder >> Result.mapError ignore)
    with
    | Error _ -> printfn "  processing failed"
    | Ok ord ->
        let ord = ord |> completeBySolveOrder
        ord |> show $"completed, solved: %b{ord |> Order.isSolved}"


// Reset, as the dose dialog resets a completed order: ReCalcValues gives the values again from the
// calculated constraints, and a rate that gets its list there keeps it to pick from.
printfn "\n\n######## completed, then reset by ReCalcValues"

for name, med in timed do
    printfn $"\n===== %s{name}"

    match
        med
        |> Medication.toOrder Scenarios.testStart
        |> Result.mapError ignore
        |> Result.bind (run CalcMinMax >> Result.mapError ignore)
        |> Result.bind (run CalcValues >> Result.mapError ignore)
        |> Result.bind (run SolveOrder >> Result.mapError ignore)
        |> Result.map completeBySolveOrder
        |> Result.bind (run ReCalcValues >> Result.mapError ignore)
    with
    | Error _ -> printfn "  processing failed"
    | Ok ord -> ord |> show $"reset, solved: %b{ord |> Order.isSolved}"
