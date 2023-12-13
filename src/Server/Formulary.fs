module Formulary


open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


open Shared.Types


module ValueUnit = Informedica.GenUnits.Lib.ValueUnit


let mapFormularyToFilter (form: Formulary)=
    { Filter.filter with
        Generic = form.Generic
        Indication = form.Indication
        Route = form.Route
        Patient =
            { Patient.patient with
                Age =
                    form.Age
                    |> Option.bind BigRational.fromFloat
                    |> Option.map (ValueUnit.singleWithUnit Units.Time.day)
                Weight =
                    form.Weight
                    |> Option.bind BigRational.fromFloat
                    |> Option.map (ValueUnit.singleWithUnit Units.Weight.kiloGram)
                GestAge =
                    form.GestAge
                    |> Option.map BigRational.fromInt
                    |> Option.map (ValueUnit.singleWithUnit Units.Time.day)

            }
    }
    |> Filter.calcPMAge


let selectIfOne sel xs =
    match sel, xs with
    | None, [|x|] -> Some x
    | _ -> sel


let get (form : Formulary) =
    let filter = form |> mapFormularyToFilter
    ConsoleWriter.writeInfoMessage $"getting formulary with filter: {filter}" true true

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
                    match form.Generic, form.Indication, form.Route with
                    | Some _, Some _, Some _ ->
                        let s =
                            dsrs
                            |> Check.checkAll
                            |> String.concat "\n"
                            |> fun s -> if s |> String.isNullOrWhiteSpace then "Ok!" else s

                        dsrs
                        |> DoseRule.filter filter
                        |> DoseRule.Print.toMarkdown
                        |> fun md ->
                            $"{md}\n\n## Dose Check\n\n{s}"

                    | _ -> ""
            }

    Ok form
