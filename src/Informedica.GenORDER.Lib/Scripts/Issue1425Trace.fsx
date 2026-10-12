// Issue #1421: why a continuous order needs the products of an item orderable concentration with
// an orderable total. The variables on the way from the morfine item to the orderable rate of
// morfCont with NaCl 0,9%, after each stage. Run it as Issue1425Measure.fsx is run, without a
// define and with --define:VARIANT --define:WITHCONTINUOUS, and diff the output.

#I __SOURCE_DIRECTORY__
System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

#load "Issue1425Measure.fsx"

open Informedica.GenOrder.Lib
open Issue1425Measure


let chain =
    [
        "]_orb_qty"
        "]_orb_cnc"
        "]_cmp_cnc"
        "]_dos_qty"
        "]_dos_rte"
        "]_dos_rte_adj"
    ]


let show stage (ord: Order) =
    printfn $"--- %s{stage}"

    for ovar in ord |> Order.toOrdVars do
        let s = ovar |> OrderVariable.toString false

        if
            (s.Contains "morfin" || s.StartsWith "[morfine]" || s.Contains "NaCl" || s.Contains "gluc")
            && chain |> List.exists s.Contains
        then
            printfn $"  %s{s}"


for name, med in [ "morfCont with NaCl", Scenarios.morfCont |> withNaCl; "morfine timed with NaCl", Scenarios.morfTimedWithNaCl ] do
    printfn $"\n===== %s{name}"

    match med |> Medication.toOrder Scenarios.testStart with
    | Error e -> printfn $"could not build: %A{e}"
    | Ok ord ->
        [
            "calc-minmax", run CalcMinMax
            "increase-increments", run IncreaseIncrements
            "calc-values", run CalcValues
            "solve-order", run SolveOrder
        ]
        |> List.fold
            (fun acc (stage, step) ->
                acc
                |> Result.bind step
                |> Result.map (fun o ->
                    o |> show stage
                    o
                )
            )
            (Ok ord)
        |> ignore
