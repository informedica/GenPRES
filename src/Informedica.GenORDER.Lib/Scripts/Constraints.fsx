#time

#load "load.fsx"
#r "nuget: YamlDotNet, 16.3.0"
#load "../../../tests/Informedica.GenORDER.Tests/MedicationTextTests.fs"

// load demo or product cache

open System

Informedica.Utils.Lib.Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable("GENPRES_DEBUG", "0")
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__


open MathNet.Numerics
open Informedica.Utils.Lib.BCL
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip


module HelperFunctions =

    open Informedica.GenOrder.Lib
    open Informedica.Utils.Lib


    let print sl = sl |> List.iter (printfn "%s")


    let inline printOrderTable order =
        order |> Result.iter (Order.printTable ConsoleTables.Format.Minimal)

        order


    let solveOrder order =
        match order with
        | Error e -> $"Error solving order: {e}" |> failwith
        | Ok o -> o |> Order.solveMinMax "Solve Order" true OrderLogging.noOp


    let run logger med cmds =
        let logger, usePrintTable =
            logger |> Option.defaultValue OrderLogging.noOp, logger.IsNone

        let rec loop cmds ord =
            ord |> Result.iter (OrderProcessor.classify >> printfn "%A")

            match cmds with
            | [] -> ord |> fun ord -> if usePrintTable then ord |> printOrderTable else ord
            | cmd :: rest ->
                match ord with
                | Error(_, msgs) -> failwith $"Errors occured: {msgs}"
                | Ok ord ->
                    if usePrintTable then
                        ord |> Ok |> printOrderTable |> ignore

                    ord |> cmd |> OrderProcessor.processPipeline logger |> loop rest

        med
        |> Medication.toOrderDto System.DateTime.UtcNow
        |> Order.Dto.fromDto
        |> function
            | Error msg -> failwith $"{msg}"
            | Ok ord ->
                ord
                |> Ok
                |> fun ord -> if usePrintTable then ord |> printOrderTable else ord
                |> loop cmds


module MedicationTexts = MedicationTextTests.MedicationTexts


MedicationTexts.continuousMultipleComponent
|> MedicationText.fromString
|> function
    | Error _ -> "fail" |> failwith
    | Ok med -> [ CalcMinMax ] |> HelperFunctions.run None med
|> Result.map (fun ord ->
    ord
    |> Order.Dto.toDto
    |> Order.Dto.fromDto
    |> function
        | Error _ -> "fail" |> failwith
        | Ok ord ->
            ord |> Order.printTable ConsoleTables.Format.Minimal

            ord |> Ok
)
|> ignore
