module Message

open Informedica.Utils.Lib
open ScenarioResult
open Shared.Types
open Shared.Api


let processOrders f state (sr : ScenarioResult) =
    let mutable errors = [||]

    let calculated =
        { sr with
            Scenarios =
                sr.Scenarios
                |> Array.map (fun sc ->
                    { sc with
                        Order =
                            match sc.Order with
                            | Constrained ord
                            | Values ord
                            | Solved ord ->
                                match ord |> f with
                                | Ok ord -> ord |> state
                                | Error errs ->
                                    errors <- Array.append errors [| errs |]
                                    sc.Order
                    }
                )
        }

    if errors |> Array.isEmpty then calculated |> Ok
    else errors |> Error


let processMsg msg =
    match msg with
    | ScenarioResultMsg msg ->
        match msg with
        | GetScenarioResult sr ->
            ConsoleWriter.writeWarningMessage "get scenario result" true false

            sr
            |> ScenarioResult.get
            |> Result.map GetScenarioResult

        | CalcValues sr ->
            ConsoleWriter.writeWarningMessage "calc values" true false

            sr
            |> processOrders calcValues Values
            |> Result.map print
            |> Result.map CalcValues

        | SolveOrder sr ->
            ConsoleWriter.writeWarningMessage "solve order" true false

            sr
            |> processOrders solveOrder Solved
            |> Result.map print
            |> Result.map SolveOrder

        |> Result.map ScenarioResultMsg

    | IntakeMsg intakeMessage -> failwith "todo"
    | FormularyMsg formularyMessage -> failwith "todo"
    | Test -> failwith "todo"