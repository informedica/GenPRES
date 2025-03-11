module PrescriptionResult


open Informedica.Utils.Lib
open ConsoleWriter.NewLineTime
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


open Shared.Types
open Shared
open Mappers

let toString stage (pr: PrescriptionResult)=
        $"""
{stage}:
Patient: {pr |> mapPatient |> Patient.toString}
Indications: {pr.Filter.Indications |> Array.length}
Medications: {pr.Filter.Medications |> Array.length}
Routes: {pr.Filter.Routes |> Array.length}
Indication: {pr.Filter.Indication |> Option.defaultValue ""}
Medication: {pr.Filter.Medication |> Option.defaultValue ""}
Route: {pr.Filter.Route |> Option.defaultValue ""}
DoseType: {pr.Filter.DoseType}
Diluent: {pr.Filter.Diluent}
Scenarios: {pr.Scenarios |> Array.length}
"""


let get (pr: PrescriptionResult) =
    let pat =
        pr
        |> mapPatient
        |> Patient.calcPMAge

    try
        let newResult =
            let patResult = PrescriptionResult.create pat

            let dil =
                if pr.Filter.Diluent.IsSome then pr.Filter.Diluent
                else
                    pr.Scenarios
                    |> Array.tryExactlyOne
                    |> Option.bind _.Diluent

            { patResult with
                Filter =
                    { patResult.Filter with
                        Indications =
                            if pr.Filter.Indication |> Option.isSome then
                                [| pr.Filter.Indication |> Option.defaultValue "" |]
                            else
                                patResult.Filter.Indications
                        Generics =
                            if pr.Filter.Medication |> Option.isSome then
                                [| pr.Filter.Medication |> Option.defaultValue "" |]
                            else
                                patResult.Filter.Generics
                        Shapes =
                            if pr.Filter.Shape |> Option.isSome then
                                [| pr.Filter.Shape |> Option.defaultValue "" |]
                            else
                                patResult.Filter.Shapes
                        Routes =
                            if pr.Filter.Route |> Option.isSome then
                                [| pr.Filter.Route |> Option.defaultValue "" |]
                            else
                                patResult.Filter.Routes
                        DoseTypes =
                            if pr.Filter.DoseType |> Option.isSome then
                                [| pr.Filter.DoseType |> Option.defaultValue NoDoseType |]
                                |> Array.map mapFromSharedDoseType
                            else
                                patResult.Filter.DoseTypes
                        Indication = pr.Filter.Indication
                        Generic = pr.Filter.Medication
                        Shape = pr.Filter.Shape
                        Route = pr.Filter.Route
                        DoseType = pr.Filter.DoseType |> Option.map mapFromSharedDoseType
                        Diluent = dil
                    }
            }
            |> Api.evaluate

        { pr with
            Filter =
                { pr.Filter with
                    Indications = newResult.Filter.Indications
                    Medications = newResult.Filter.Generics
                    Routes = newResult.Filter.Routes
                    DoseTypes = newResult.Filter.DoseTypes |> Array.map mapToSharedDoseType
                    Diluents = newResult.Filter.Diluents
                    Indication = newResult.Filter.Indication
                    Medication = newResult.Filter.Generic
                    Shape = newResult.Filter.Shape
                    Route = newResult.Filter.Route
                    DoseType = newResult.Filter.DoseType |> Option.map mapToSharedDoseType
                    Diluent = newResult.Filter.Diluent
                }

            Scenarios =
                newResult.Scenarios
                |> Array.map (fun sc ->
                    Models.OrderScenario.create
                        sc.Indication
                        sc.Diluents
                        sc.Components
                        sc.Items
                        sc.Shape
                        sc.Diluent
                        (sc.DoseType |> mapToSharedDoseType)
                        sc.Component
                        sc.Item
                        sc.Prescription
                        sc.Preparation
                        sc.Administration
                        (sc.Order |> (Order.Dto.toDto >> mapToOrder sc.Items >> Constrained))
                        sc.UseAdjust
                        sc.UseRenalRule
                        sc.RenalRule
                )
        }
    with
    | e ->
        writeErrorMessage $"errored:\n{e}"
        pr //|> Ok
    |> fun sc ->
        { sc with
            DemoVersion =
                Env.getItem "GENPRES_PROD"
                |> Option.map (fun v -> v <> "1")
                |> Option.defaultValue true
        }
        |> Ok


let print (pr: PrescriptionResult) =
    { pr with
        Scenarios =
            pr.Scenarios
            |> Array.map (fun sc ->
                let ord = sc.Order |> Models.OrderState.getOrder

                let prs, prp, adm =
                    // only print the item quantities of the principal component
                    let sns =
                        match ord.Orderable.Components |> Array.tryHead with
                        | Some c ->
                            c.Items
                            |> Array.map _.Name
                            |> Array.filter (fun n -> Array.exists ((=) n) sc.Items)
                        | None -> [||]

                    ord
                    |> mapFromOrder
                    |> Order.Dto.fromDto
                    |> Order.Print.printOrderToTableFormat sc.UseAdjust true sns
                    |> fun (prs, prp, adm) ->
                        prs |> Array.map (Array.map (OrderScenario.replace >> Models.OrderScenario.parseTextItem)),
                        prp |> Array.map (Array.map (OrderScenario.replace >> Models.OrderScenario.parseTextItem)),
                        adm |> Array.map (Array.map (OrderScenario.replace >> Models.OrderScenario.parseTextItem))
                { sc with
                    Prescription = prs
                    Preparation = prp
                    Administration = adm
                }
            )
    }