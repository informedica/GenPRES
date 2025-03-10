module PrescriptionResult


open Informedica.Utils.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


open Shared.Types
open Shared
open Mappers


let get (sr: PrescriptionResult) =
    let msg stage (sr: PrescriptionResult)=
        $"""
{stage}:
Patient: {sr |> mapPatient |> Patient.toString}
Indications: {sr.Filter.Indications |> Array.length}
Medications: {sr.Filter.Medications |> Array.length}
Routes: {sr.Filter.Routes |> Array.length}
Indication: {sr.Filter.Indication |> Option.defaultValue ""}
Medication: {sr.Filter.Medication |> Option.defaultValue ""}
Route: {sr.Filter.Route |> Option.defaultValue ""}
DoseType: {sr.Filter.DoseType}
Diluent: {sr.Filter.Diluent}
Scenarios: {sr.Scenarios |> Array.length}
"""

    ConsoleWriter.writeInfoMessage $"""{msg "processing" sr}""" true true

    let pat =
        sr
        |> mapPatient
        |> Patient.calcPMAge

    try
        let newResult =
            let patResult = Api.scenarioResult pat

            let dil =
                if sr.Filter.Diluent.IsSome then sr.Filter.Diluent
                else
                    sr.Scenarios
                    |> Array.tryExactlyOne
                    |> Option.bind _.Diluent

            { patResult with
                Filter =
                    { patResult.Filter with
                        Indications =
                            if sr.Filter.Indication |> Option.isSome then
                                [| sr.Filter.Indication |> Option.defaultValue "" |]
                            else
                                patResult.Filter.Indications
                        Generics =
                            if sr.Filter.Medication |> Option.isSome then
                                [| sr.Filter.Medication |> Option.defaultValue "" |]
                            else
                                patResult.Filter.Generics
                        Shapes =
                            if sr.Filter.Shape |> Option.isSome then
                                [| sr.Filter.Shape |> Option.defaultValue "" |]
                            else
                                patResult.Filter.Shapes
                        Routes =
                            if sr.Filter.Route |> Option.isSome then
                                [| sr.Filter.Route |> Option.defaultValue "" |]
                            else
                                patResult.Filter.Routes
                        DoseTypes =
                            if sr.Filter.DoseType |> Option.isSome then
                                [| sr.Filter.DoseType |> Option.defaultValue NoDoseType |]
                                |> Array.map mapFromSharedDoseType
                            else
                                patResult.Filter.DoseTypes
                        Indication = sr.Filter.Indication
                        Generic = sr.Filter.Medication
                        Shape = sr.Filter.Shape
                        Route = sr.Filter.Route
                        DoseType = sr.Filter.DoseType |> Option.map mapFromSharedDoseType
                        Diluent = dil
                    }
            }
            |> Api.filter

        { sr with
            Filter =
                { sr.Filter with
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
        |> fun sr ->
            ConsoleWriter.writeInfoMessage $"""{msg "finished" sr}""" true true
            sr //|> Ok
    with
    | e ->
        ConsoleWriter.writeErrorMessage $"errored:\n{e}" true true
        sr //|> Ok
    |> fun sc ->
        { sc with
            DemoVersion =
                Env.getItem "GENPRES_PROD"
                |> Option.map (fun v -> v <> "1")
                |> Option.defaultValue true
        }
        |> Ok


let print (sc: PrescriptionResult) =
    let msg stage (sc: PrescriptionResult)=
        let s =
            sc.Scenarios
            |> Array.collect _.Prescription
        $"""
{stage}: {s}
"""

    ConsoleWriter.writeInfoMessage $"""{msg "printing" sc}""" true true

    let sc =
        { sc with
            Scenarios =
                sc.Scenarios
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
                            prs |> Array.map (Array.map (Api.replace >> Models.OrderScenario.parseTextItem)),
                            prp |> Array.map (Array.map (Api.replace >> Models.OrderScenario.parseTextItem)),
                            adm |> Array.map (Array.map (Api.replace >> Models.OrderScenario.parseTextItem))
                    { sc with
                        Prescription = prs
                        Preparation = prp
                        Administration = adm
                    }
                )
        }
    ConsoleWriter.writeInfoMessage $"""{msg "finished printing" sc}""" true true
    sc