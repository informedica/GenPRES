[<AutoOpen>]
module ServerApiImpl

open DocumentFormat.OpenXml.EMMA
open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


open Shared.Types
open Shared.Api


let mapToDto (dto : Order.Dto.Dto) : Shared.Types.Dto.Dto =
    let mappedDto = Dto.Dto()

    mappedDto
/// An implementation of the Shared IServerApi protocol.


let mapFromDto (dto: Dto.Dto) : Order.Dto.Dto =
    let mappedDto = Order.Dto.Dto(dto.Id, dto.Orderable.Name)

    mappedDto


let serverApi: IServerApi =
    let mapFormularyToFilter (form: Formulary)=
        { Filter.filter with
            Generic = form.Generic
            Indication = form.Indication
            Route = form.Route
            Age = form.Age |> Option.bind BigRational.fromFloat
            Weight = form.Weight |> Option.bind BigRational.fromFloat
        }

    let selectIfOne sel xs =
        match sel, xs with
        | None, [|x|] -> Some x
        | _ -> sel

    {
        test =
            fun () ->
                async {
                    return "Hello world!"
                }

        getFormulary =
            fun (form : Formulary) ->
                ConsoleWriter.writeInfoMessage "getting formulary" true true

                async {
                    let filter = form |> mapFormularyToFilter

                    let dsrs =
                        DoseRule.get ()
                        |> DoseRule.filter filter

                    let form =
                        { form with
                            Generics = dsrs |> DoseRule.generics
                            Indications = dsrs |> DoseRule.indications
                            Routes = dsrs |> DoseRule.routes
                            Patients = dsrs |> DoseRule.patients
                        }
                        |> fun form ->
                            { form with
                                Generic = form.Generics |> selectIfOne form.Generic
                                Indication = form.Indications |> selectIfOne form.Indication
                                Route = form.Routes |> selectIfOne form.Route
                                Patient = form.Patients |> selectIfOne form.Patient
                            }
                        |> fun form ->
                            { form with
                                Markdown =
                                    match form.Generic, form.Indication, form.Route, form.Patient with
                                    | Some _, Some _, Some _, Some _ ->
                                        dsrs
                                        |> DoseRule.filter filter
                                        |> DoseRule.Print.toMarkdown
                                    | _ -> ""
                            }

                    return Ok form
                }

        getScenarioResult =
            fun (sc: ScenarioResult) ->

                async {
                    let msg stage (sc: ScenarioResult)=
                        $"""
{stage}:
Patient: {sc.Age} days, {sc.Weight} kg, {sc.Height} cm
Indications: {sc.Indications |> Array.length}
Medications: {sc.Medications |> Array.length}
Routes: {sc.Routes |> Array.length}
Indication: {sc.Indication |> Option.defaultValue ""}
Medication: {sc.Medication |> Option.defaultValue ""}
Route: {sc.Route |> Option.defaultValue ""}
Scenarios: {sc.Scenarios |> Array.length}
"""

                    ConsoleWriter.writeInfoMessage $"""{msg "processing" sc}""" true true

                    let pat =
                        { Patient.patient with
                            Department = "ICK"
                            Age =
                                sc.Age
                                |> Option.bind BigRational.fromFloat
                            Weight =
                                sc.Weight
                                |> Option.map (fun w -> w * 1000.)
                                |> Option.bind BigRational.fromFloat
                            Height =
                                sc.Height
                                |> Option.bind BigRational.fromFloat
                        }

                    try
                        let newSc =
                            let r = Demo.scenarioResult pat
                            { Demo.scenarioResult pat with
                                Indications =
                                    if sc.Indication |> Option.isSome then
                                        [| sc.Indication |> Option.defaultValue "" |]
                                    else
                                        r.Indications
                                Generics =
                                    if sc.Medication |> Option.isSome then
                                        [| sc.Medication |> Option.defaultValue "" |]
                                    else
                                        r.Generics
                                Shapes =
                                    if sc.Shape |> Option.isSome then
                                        [| sc.Shape |> Option.defaultValue "" |]
                                    else
                                        r.Shapes
                                Routes =
                                    if sc.Route |> Option.isSome then
                                        [| sc.Route |> Option.defaultValue "" |]
                                    else
                                        r.Routes
                                Indication = sc.Indication
                                Generic = sc.Medication
                                Shape = sc.Shape
                                Route = sc.Route
                            }
                            |> Demo.filter

                        let sc =
                            { sc with
                                Indications = newSc.Indications
                                Medications = newSc.Generics
                                Routes = newSc.Routes
                                Indication = newSc.Indication
                                Medication = newSc.Generic
                                Shape = newSc.Shape
                                Route = newSc.Route
                                Scenarios =
                                    newSc.Scenarios
                                    |> Array.map (fun sc ->
                                        Shared.ScenarioResult.createScenario sc.Shape sc.DoseType sc.Prescription sc.Preparation sc.Administration
                                    )
                            }
                        ConsoleWriter.writeInfoMessage $"""{msg "finished" sc}""" true true
                        return sc |> Ok
                    with
                    | e ->
                        ConsoleWriter.writeErrorMessage $"errored:\n{e}" true true
                        return sc |> Ok
                }
    }