/// The formulary and the parenteralia over a provider with a few rules: a chosen field lists the
/// options it could take given the other choices, and what the page shows matches the selection.
module Informedica.GenPRES.Server.Tests.FilterListsTests

open System
open Expecto
open Expecto.Flip
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
open ServerApi


/// A record or union with every field a default, built by reflection: the rule types are too
/// deep to write by hand, and the lists read only a few fields.
let rec defaultOf (t: Type) : obj =
    if t = typeof<string> then
        box ""
    elif t = typeof<bool> then
        box false
    elif t = typeof<int> then
        box 0
    elif t = typeof<decimal> then
        box 0m
    elif t = typeof<float> then
        box 0.0
    elif t = typeof<DateTime> then
        box DateTime.MinValue
    elif t.IsArray then
        box (Array.CreateInstance(t.GetElementType(), 0))
    elif t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>> then
        null
    elif t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<list<_>> then
        t.GetProperty("Empty").GetValue(null)
    elif Reflection.FSharpType.IsRecord t then
        Reflection.FSharpValue.MakeRecord(
            t,
            Reflection.FSharpType.GetRecordFields t
            |> Array.map (fun f -> defaultOf f.PropertyType)
        )
    elif Reflection.FSharpType.IsUnion t then
        let case = (Reflection.FSharpType.GetUnionCases t)[0]
        Reflection.FSharpValue.MakeUnion(case, case.GetFields() |> Array.map (fun f -> defaultOf f.PropertyType))
    else
        null


let doseRule indication generic route : Types.DoseRule =
    let dr = defaultOf typeof<Types.DoseRule> :?> Types.DoseRule

    { dr with
        Indication = indication
        Generic =
            { dr.Generic with
                Label = Types.Canonical [ generic ]
                Form = Types.Solution "injectievloeistof"
            }
        Route = route
    }


let solutionRule generic route : Types.SolutionRule =
    let sr = defaultOf typeof<Types.SolutionRule> :?> Types.SolutionRule

    { sr with
        Generic = generic
        Route = route
    }


/// A provider holding these dose and solution rules; the departments answer, since the filter
/// reads them, and every other resource raises, so a test reaches nothing else.
type WithRules(doseRules: Types.DoseRule[], solutionRules: Types.SolutionRule[]) =
    interface Resources.IResourceProvider with
        member _.Get(key: Resources.ResourceKey<'T>) : 'T =
            if key.Name = Resources.Keys.departments.Name then
                box (Resources.Departments.ofNamed []) :?> 'T
            else
                raise (NotImplementedException())

        member _.GetData() = raise (NotImplementedException())
        member _.GetUnitMappings() = raise (NotImplementedException())
        member _.GetRouteMappings() = [||]
        member _.GetValidForms() = raise (NotImplementedException())
        member _.GetFormRoutes() = raise (NotImplementedException())
        member _.GetFormularyProducts() = raise (NotImplementedException())
        member _.GetReconstitution() = raise (NotImplementedException())
        member _.GetParenteralMeds() = raise (NotImplementedException())
        member _.GetEnteralFeeding() = raise (NotImplementedException())
        member _.GetProducts() = raise (NotImplementedException())
        member _.GetDoseRules() = doseRules
        member _.GetSolutionRules() = solutionRules
        member _.GetRenalRules() = [||]
        member _.GetTotals() = raise (NotImplementedException())
        member _.GetGStandProvider() = raise (NotImplementedException())
        member _.GetResourceInfo() = raise (NotImplementedException())


[<Tests>]
let tests =
    let provider =
        WithRules(
            [|
                doseRule "Ernstige pijn" "morfine" "INTRAVENEUS"
                doseRule "Ernstige pijn" "hydromorfon" "INTRAVENEUS"
                doseRule "Koorts" "paracetamol" "RECTAAL"
            |],
            [|
                solutionRule "morfine" "INTRAVENEUS"
                solutionRule "hydromorfon" "INTRAVENEUS"
                solutionRule "paracetamol" "ORAAL"
            |]
        )
        :> Resources.IResourceProvider

    // the lists alone: the formulary's own get also checks the doses against the G-Standaard
    // once a medication, an indication and a route are chosen, which this provider does not hold
    let formulary (sent: Shared.Types.Formulary) =
        let filter = sent |> FormularyService.mapFormularyToFilter (Resources.Departments.ofNamed [])
        let held = Formulary.getDoseRules provider filter

        { sent with
            Generics = held |> DoseRule.generics
            Indications = held |> DoseRule.indications
            Routes = held |> DoseRule.routes
        }
        |> FormularyService.withAlternatives provider sent filter

    let parenteralia (sent: Shared.Types.Parenteralia) =
        ParenteraliaService.get Informedica.GenOrder.Lib.OrderLogging.noOp provider sent
        |> Result.defaultWith failtest

    testList
        "the lists of a chosen field"
        [
            test "formulary: a chosen medication lists the others for the chosen indication, and stays chosen" {
                let form =
                    formulary
                        { Shared.Models.Formulary.empty with
                            Indication = Some "Ernstige pijn"
                            Generic = Some "morfine"
                        }

                form.Generics |> Expect.equal "the alternatives" [| "hydromorfon"; "morfine" |]
                form.Indications
                |> Expect.equal "the indications of the medication" [| "Ernstige pijn" |]
            }

            test "formulary: a field without a choice lists what every choice allows" {
                let form = formulary { Shared.Models.Formulary.empty with Generic = Some "morfine" }

                form.Routes |> Expect.equal "the routes of the medication" [| "INTRAVENEUS" |]
                form.Generics |> Array.length |> Expect.equal "every medication" 3
            }

            test "parenteralia: a chosen medication lists the others for the chosen route, its rules shown" {
                let par =
                    parenteralia
                        { Shared.Models.Parenteralia.empty with
                            Generic = Some "morfine"
                            Route = Some "INTRAVENEUS"
                        }

                par.Generics |> Expect.equal "the alternatives" [| "hydromorfon"; "morfine" |]
                par.Generic |> Expect.equal "the choice kept" (Some "morfine")
                par.Markdown |> Expect.isNotEmpty "the solution rules of the choice"
            }

            test "parenteralia: a choice the other choices exclude goes, and what is shown matches the selection" {
                let par =
                    parenteralia
                        { Shared.Models.Parenteralia.empty with
                            Generic = Some "paracetamol"
                            Route = Some "INTRAVENEUS"
                        }

                par.Generics
                |> Expect.equal "the medications for the route" [| "hydromorfon"; "morfine" |]
                par.Generic
                |> Expect.isNone "paracetamol has no rule for the route, and there is no one option"
                par.Markdown |> Expect.equal "nothing shown without a medication" ""
            }

            test
                "parenteralia: a choice is never swapped for the one medication that fits, so the page never shows another's rules" {
                let onlyMorphine =
                    WithRules([||], [| solutionRule "morfine" "INTRAVENEUS"; solutionRule "paracetamol" "ORAAL" |])
                    :> Resources.IResourceProvider

                let sent =
                    { Shared.Models.Parenteralia.empty with
                        Generic = Some "paracetamol"
                        Route = Some "INTRAVENEUS"
                    }

                let par =
                    ParenteraliaService.get OrderLogging.noOp onlyMorphine sent
                    |> Result.defaultWith failtest

                par.Generics
                |> Expect.equal "the one medication for the route listed" [| "morfine" |]
                par.Generic |> Expect.isNone "the choice dropped, not swapped"
                par.Route |> Expect.isNone "the route dropped, not swapped for the choice's own"
                par.Markdown |> Expect.equal "nothing shown without a medication" ""
            }
        ]
