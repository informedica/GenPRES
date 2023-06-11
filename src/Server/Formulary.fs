module Formulary


open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


open Shared.Types
open Shared.Api

module ValueUnit = Informedica.GenUnits.Lib.ValueUnit


let mapFormularyToFilter (form: Formulary)=
    { Filter.filter with
        Generic = form.Generic
        Indication = form.Indication
        Route = form.Route
        Age = form.Age |> Option.bind BigRational.fromFloat
        Weight = form.Weight |> Option.map ((*) 1000.) |> Option.bind BigRational.fromFloat
    }

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
                        dsrs
                        |> DoseRule.filter filter
                        |> DoseRule.Print.toMarkdown
                    | _ -> ""
            }

    Ok form
