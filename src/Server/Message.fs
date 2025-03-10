module Message

open Shared
open Shared.Types
open Shared.Api


let processOrders (pr : PrescriptionResult) =
    let mutable errors = [||]

    let scenarios =
        pr.Scenarios
        |> Array.map (fun sc ->
            { sc with
                Order =
                    let ord, f =
                        match sc.Order with
                        | Constrained o -> o, Order.calcValues
                        | Calculated o  -> o, Order.solveOrder
                        | Solved o      -> o, Order.calcValues

                    match ord |> f with
                    | Ok ord -> ord
                    | Error errs ->
                        errors <- Array.append errors [| errs |]
                        sc.Order
            }
        )

    let calculated =
        { pr with
            Scenarios = scenarios

            Intake =
                let w = pr.Patient |> Models.Patient.getWeight

                scenarios
                |> Array.map (_.Order >> Models.OrderState.getOrder)
                |> Order.getIntake w
        }

    if errors |> Array.isEmpty then calculated |> Ok
    else errors |> Error


let checkDiluent (pr: PrescriptionResult) =
    pr.Scenarios
    |> Array.tryExactlyOne
    |> Option.map (fun sc ->
        let ord =
            match sc.Order with
            | Constrained o
            | Calculated o
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
    | PrescriptionResultMsg pr ->
        if pr.Scenarios |> Array.isEmpty then
            pr
            |> PrescriptionResult.get

        else
            if pr |> checkDiluent then
                pr
                |> processOrders
                |> Result.map PrescriptionResult.print
            else
                pr
                |> PrescriptionResult.get

        |> Result.map (fun sr ->
            { sr with
                Intake =
                    let w = sr.Patient |> Models.Patient.getWeight
                    sr.Scenarios
                    |> Array.map (_.Order >> Models.OrderState.getOrder)
                    |> Order.getIntake w
            }
        )
        |> Result.map PrescriptionResultMsg

    | TreatmentPlanMsg tp ->
        match tp.Selected with
        | Some os ->
            os
            |> Models.PrescriptionResult.fromOrderScenario
            |> processOrders
            |> Result.map (fun pr ->
                let newOsc = pr.Scenarios |> Array.tryExactlyOne

                { tp with
                    Selected = newOsc
                    Scenarios =
                        match newOsc with
                        | None -> tp.Scenarios
                        | Some newOsc ->
                            tp.Scenarios
                            |> Array.map (fun sc ->
                                if sc |> Models.OrderScenario.eqs newOsc then newOsc
                                else sc
                            )
                }
            )
            |> Result.defaultValue tp
        | None -> tp
        |> fun tp ->
            { tp with
                Intake =
                    let w = tp.Patient |> Models.Patient.getWeight

                    tp.Scenarios
                    |> Array.map (_.Order >> Models.OrderState.getOrder)
                    |> Order.getIntake w
            }
            |> TreatmentPlanMsg
            |> Ok

    | FormularyMsg form ->
        form
        |> Formulary.get
        |> Result.map FormularyMsg

    | ParenteraliaMsg par ->
        par
        |> Parenteralia.get
        |> Result.mapError Array.singleton
        |> Result.map ParenteraliaMsg