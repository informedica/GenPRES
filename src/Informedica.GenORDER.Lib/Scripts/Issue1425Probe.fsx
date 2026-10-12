// Issue #1425: does a solve that is not a min and max solve turn the orderable rate of a timed
// order into values while the rate stage is held back? Per timed and once timed fixture of
// Issue1425Measure.fsx: today's calc-minmax, the quantity values with the rate held back, two
// solves, a pick of the middle dose quantity, and the rate values. Run from this directory:
//
//   dotnet fsi Issue1425Probe.fsx

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
