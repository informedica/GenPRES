module Formulary


open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


open Shared.Types


module ValueUnit = Informedica.GenUnits.Lib.ValueUnit


let mapFormularyToFilter (form: Formulary)=
    { Filter.doseFilter with
        Generic = form.Generic
        Indication = form.Indication
        Route = form.Route
        Patient =
            match form.Patient with
            | Some pat ->
                { Patient.patient with
                    Age =
                        pat
                        |> Shared.Patient.getAgeInDays
                        |> Option.bind BigRational.fromFloat
                        |> Option.map (ValueUnit.singleWithUnit Units.Time.day)
                    Weight =
                        pat
                        |> Shared.Patient.getWeightInKg
                        |> Option.bind BigRational.fromFloat
                        |> Option.map (ValueUnit.singleWithUnit Units.Weight.kiloGram)
                    GestAge =
                        pat
                        |> Shared.Patient.getHeight
                        |> Option.map BigRational.fromInt
                        |> Option.map (ValueUnit.singleWithUnit Units.Time.day)
                }
            | None -> Patient.patient
    }
    |> Filter.calcPMAge


let selectIfOne sel xs =
    match sel, xs with
    | None, [|x|] -> Some x
    | _ -> sel


let checkDoseRules pat (dsrs : DoseRule []) =
    let empt, rs =
        dsrs
        |> Array.distinctBy (fun dr -> dr.Generic, dr.Shape, dr.Route, dr.DoseType)
        |> Array.map (Check.checkDoseRule pat)
        |> Array.partition (fun c ->
            c.didPass |> Array.isEmpty &&
            c.didNotPass |> Array.isEmpty
        )

    rs
    |> Array.filter (_.didNotPass >> Array.isEmpty >> not)
    |> Array.collect _.didNotPass
    |> Array.filter String.notEmpty
    |> Array.distinct
    |> function
        | [||] ->
            [|
                for e in empt do
                    $"geen doseer bewaking gevonden voor {e.doseRule.Generic}"
            |]
            |> Array.distinct

        | xs -> xs


let get (form : Formulary) =
    let filter = form |> mapFormularyToFilter
    //ConsoleWriter.writeInfoMessage $"getting formulary with filter: {filter}" true false

    let dsrs = Api.getDoseRules filter

    printfn $"found: {dsrs |> Array.length} dose rules"
    let form =
        { form with
            Generics = dsrs |> DoseRule.generics
            Indications = dsrs |> DoseRule.indications
            Routes = dsrs |> DoseRule.routes
            PatientCategories = dsrs |> DoseRule.patientCategories
        }
        |> fun form ->
            { form with
                Generic = form.Generics |> selectIfOne form.Generic
                Indication = form.Indications |> selectIfOne form.Indication
                Route = form.Routes |> selectIfOne form.Route
                PatientCategory = form.PatientCategories |> selectIfOne form.PatientCategory
            }
        |> fun form ->
            { form with
                Markdown =
                    match form.Generic, form.Indication, form.Route with
                    | Some _, Some _, Some _ ->
                        ConsoleWriter.writeInfoMessage $"start checking {dsrs |> Array.length} rules" true true
                        let s =
                            dsrs
                            |> checkDoseRules filter.Patient
                            |> Array.map (fun s ->
                                match s |> String.split "\t" with
                                | [s1; _; p; s2] ->
                                    if dsrs |> Array.length = 1 then $"{s1} {s2}"
                                    else
                                        $"{s1} {p} {s2}"
                                | _ -> s
                            )
                            |> Array.map (fun s -> $"* {s}")
                            |> String.concat "\n"
                            |> fun s -> if s |> String.isNullOrWhiteSpace then "Ok!" else s

                        ConsoleWriter.writeInfoMessage $"finished checking {dsrs |> Array.length} rules" true true

                        dsrs
                        |> DoseRule.Print.toMarkdown
                        |> fun md ->
                            $"{md}\n\n### Doseer controle volgens de G-Standaard\n\n{s}"

                    | _ -> ""
            }

    Ok form