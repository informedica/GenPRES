// Issue #1425 with #1421: every variable of every fixture printed, once processed and once
// completed by picking the first value of each component quantity. Run it as Issue1425Measure.fsx
// is run, with and without --define:VARIANT, and diff the output.

#I __SOURCE_DIRECTORY__
System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

#load "Issue1425Measure.fsx"

open Informedica.GenOrder.Lib
open Issue1425Measure


let printVars label (ord: Order) =
    printfn $"--- %s{label}, solved: %b{ord |> Order.isSolved}"

    for ovar in ord |> Order.toOrdVars do
        printfn $"  %s{ovar |> OrderVariable.toString false}"


let complete (ord: Order) =
    ord.Orderable.Components
    |> List.map _.Name
    |> List.fold
        (fun (o: Order) name ->
            let cmp = o.Orderable.Components |> List.find (fun c -> c.Name = name)
            let ovar = cmp.OrderableQuantity |> OrderVariable.Quantity.toOrdVar

            if ovar |> valuesOf |> Array.length <= 1 then
                o
            else
                o
                |> Order.fromOrdVars [ ovar |> OrderVariable.setNthValue 0 ]
                |> Order.solveOrder "complete" false logger
                |> Result.defaultValue o
        )
        ord


for name, med in fixtures () do
    printfn $"\n===== %s{name}"

    match med |> Medication.toOrder Scenarios.testStart with
    | Error _ -> printfn "could not build"
    | Ok ord ->
        match
            ord
            |> run CalcMinMax
            |> Result.bind (run IncreaseIncrements)
            |> Result.bind (run CalcValues)
            |> Result.bind (run SolveOrder)
        with
        | Error _ -> printfn "processing failed"
        | Ok ord ->
            ord |> printVars "processed"
            ord |> complete |> printVars "completed"
