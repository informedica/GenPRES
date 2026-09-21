#time

#load "load.fsx"

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
            match cmds with
            | [] -> ord |> fun ord -> if usePrintTable then ord |> printOrderTable else ord

            | cmd :: rest ->
                match ord with
                | Error(_, msgs) -> failwith $"Errors occured: {msgs}"
                | Ok ord -> ord |> cmd |> OrderProcessor.processPipeline logger |> loop rest

        med
        |> Informedica.GenOrder.Lib.Medication.toOrderDto System.DateTime.UtcNow
        |> Order.Dto.fromDto
        |> function
            | Error msg -> failwith $"{msg}"
            | Ok ord ->
                ord
                |> Ok
                |> fun ord -> if usePrintTable then ord |> printOrderTable else ord
                |> loop cmds


module GenFormResult = Utils.GenFormResult


module UnitValidation = Medication.UnitValidation


module FeedingTexts =


    let feedingWithPowder =
        """
Id: 4f6671d8-30f2-4bcc-a9a3-76bb931f15e5
Name: mm met bmf
Quantity:
Quantities:
Route: ORAAL
OrderType: DiscontinuousOrder
Adjust: 4 kg
Frequencies: 3;4;5;6;7;8;12 x/day
Time:
Dose: [qty-adj] max 20 ml/kg/dosis, [qty] max 1000 ml/dosis
Div:
DoseCount: 1 x
Components:

	Name: MM
	Form: voeding
	Quantities: 1 ml
	Divisible: 10
	Dose: MM, [dun] ml, [per-time-adj] 10 ml/kg/day - 150 ml/kg/day, [per-time] max 2500 ml/day, [qty-adj] max 20 ml/kg/dosis, [qty] max 1000 ml/dosis
	Solution:
	Substances:

		Name: energie
		Concentrations: 0.68 kCal/ml
		Dose:
		Solution:

		Name: eiwit
		Concentrations: 0.01 g/ml
		Dose:
		Solution:

	Name: Nutrilon Nenatal BMF pdr
	Form: voeding
	Quantities: 1 g
	Divisible: 10
	Dose: Nutrilon Nenatal BMF pdr, [dun] g, [per-time-adj] 0.1 g/kg/day - 0.3 g/kg/day
	Solution:
	Substances:

		Name: energie
		Concentrations: 3.47 kCal/g
		Dose:
		Solution:

		Name: eiwit
		Concentrations: 0.252 x
		Dose:
		Solution:
"""


module FeedingScenarios =

    open HelperFunctions
    open Informedica.Utils.Lib


    let createTest logger txt =
        test txt {
            txt
            |> Medication.fromString
            |> Result.bind (fun med -> [ OrderCommand.CalcMinMax ] |> run logger med |> Result.mapError (fun _ -> []))
            |> function
                | Ok _ -> true
                | Error _ -> false
            |> Expect.isTrue $"should be able to run: {txt}"
        }

    let scenarioTests logger =
        testList
            "Scenario Tests"
            [
                yield! [ FeedingTexts.feedingWithPowder ] |> List.map (createTest logger)
            ]


let logger = OrderLogging.createConsoleLogger ()


testList
    "Feeding"
    [
        //MedicationTests.tests
        FeedingScenarios.scenarioTests (Some logger)
    ]
|> runTestsWithCLIArgs [ CLIArguments.Sequenced ] [||]


FeedingTexts.feedingWithPowder
|> Medication.fromString
|> function
    | Error _ -> "cannot create feeding" |> failwith
    | Ok med ->
        med
        |> Medication.OrderBuilder.divisibility (Some med.Components[1])
        |> Option.map (ValueUnit.getUnit >> ValueUnit.unitToString)
        |> printfn "%A"

        [ OrderCommand.CalcMinMax ] |> HelperFunctions.run (Some logger) med
