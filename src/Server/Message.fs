module Message

open Giraffe.HttpStatusCodeHandlers.Successful
open Informedica.Utils.Lib
open ScenarioResult
open Shared
open Shared.Types
open Shared.Api


let processOrders f state (sr : ScenarioResult) =
    let mutable errors = [||]

    let scenarios =
        sr.Scenarios
        |> Array.map (fun sc ->
            { sc with
                Order =
                    let ord =
                        sc.Order
                        |> Models.OrderState.getOrder
                    match ord |> f with
                    | Ok ord -> ord |> state
                    | Error errs ->
                        errors <- Array.append errors [| errs |]
                        sc.Order
            }
        )

    let calculated =
        { sr with
            Scenarios = scenarios

            Intake =
                let w = sr.Patient |> Models.Patient.getWeight

                scenarios
                |> Array.map (_.Order >> Models.OrderState.getOrder)
                |> getIntake w
        }

    if errors |> Array.isEmpty then calculated |> Ok
    else errors |> Error


let checkDiluent (sr: ScenarioResult) =
    sr.Scenarios
    |> Array.tryExactlyOne
    |> Option.map (fun sc ->
        let ord =
            match sc.Order with
            | Constrained o
            | Values o
            | Solved o -> o

        match sc.Diluent with
        | None -> true
        | Some dil ->
            // check if diluent is used in order
            ord.Orderable.Components
            |> Array.map _.Name
            |> Array.exists ((=) dil)
    )
    |> Option.defaultValue true


let processMsg msg =
    match msg with
    | ScenarioResultMsg msg ->
        match msg with
        | GetScenarioResult sr ->
            sr
            |> ScenarioResult.get
            |> Result.map (fun sr ->
                { sr with
                    Intake =
                        let w = sr.Patient |> Models.Patient.getWeight
                        sr.Scenarios
                        |> Array.map (_.Order >> Models.OrderState.getOrder)
                        |> getIntake w
                }
            )
            |> Result.map GetScenarioResult

        | CalcValues sr ->
            if sr |> checkDiluent then
                sr
                |> processOrders calcValues Values
                |> Result.map print
                |> Result.map CalcValues
            else
                sr
                |> get
                |> Result.map GetScenarioResult

        | SolveOrder sr ->
            if sr |> checkDiluent then
                sr
                |> processOrders solveOrder Solved
                |> Result.map print
                |> Result.map SolveOrder
            else
                sr
                |> get
                |> Result.map GetScenarioResult

        |> Result.map ScenarioResultMsg

    | TreatmentPlanMsg (CalcTreatmentPlan tp) ->
        { tp with
            Intake =
                let w = tp.Patient |> Models.Patient.getWeight

                tp.Scenarios
                |> Array.map (_.Order >> Models.OrderState.getOrder)
                |> getIntake w
        }
        |> CalcTreatmentPlan
        |> TreatmentPlanMsg
        |> Ok

    | FormularyMsg (GetFormulary form) ->
        form
        |> Formulary.get
        |> Result.map (GetFormulary >> FormularyMsg)

    | FormularyMsg (GetParenteralia par) ->
        par
        |> Parenteralia.get
        |> Result.mapError Array.singleton
        |> Result.map (GetParenteralia >> FormularyMsg)