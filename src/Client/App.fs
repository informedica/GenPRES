module App

open Fable.Core
open Browser
open Fable.React


module private Elmish =

    open Elmish
    open Feliz
    open Feliz.Router
    open Fable.Remoting.Client
    open Shared
    open Types
    open Global
    open Utils
    open System


    type Model =
        {
            Page : Global.Pages
            Patient: Patient option
            BolusMedication: Deferred<BolusMedication list>
            ContinuousMedication: Deferred<ContinuousMedication list>
            Products: Deferred<Product list>
            Scenarios: Deferred<ScenarioResult>
            Formulary: Deferred<Formulary>
        }


    type Msg =
        | UpdatePatient of Patient option
        | UpdatePage of Global.Pages
        | UrlChanged of string list
        | LoadBolusMedication of
            AsyncOperationStatus<Result<BolusMedication list, string>>
        | LoadContinuousMedication of
            AsyncOperationStatus<Result<ContinuousMedication list, string>>
        | LoadProducts of AsyncOperationStatus<Result<Product list, string>>
        | LoadScenarios of AsyncOperationStatus<Result<ScenarioResult, string>>
        | UpdateScenarios of ScenarioResult
        | LoadFormulary of AsyncOperationStatus<Result<Formulary, string>>
        | UpdateFormulary of Formulary


    let serverApi =
        Remoting.createApi ()
        |> Remoting.withRouteBuilder Api.routerPaths
        |> Remoting.buildProxy<Api.IServerApi>


    // url needs to be in format: http://localhost:8080/#pat?ay=2&am=0&ad=1
    // * pg : el (emergency list) cm (continuous medication)
    // * by: birth year
    // * bm: birth month
    // * bd: birth day
    // * wt: weight (gram)
    // * ht: height (cm)
    let parseUrl sl =
        match sl with
        | [] -> None, None
        | [ "patient"; Route.Query queryParams ] ->
            let queryParamsMap = Map.ofList queryParams

            let pat =
                match Map.tryFind "by" queryParamsMap with
                | Some (Route.Int year) ->
                    // birthday year is required
                    let month =
                        match Map.tryFind "bm" queryParamsMap with
                        | Some (Route.Int months) -> months
                        | _ -> 1 // january is the default

                    let day =
                        match Map.tryFind "bd" queryParamsMap with
                        | Some (Route.Int days) -> days
                        | _ -> 1 // first day of the month is the default

                    let weight =
                        match Map.tryFind "wt" queryParamsMap with
                        | Some (Route.Number weight) -> Some (weight / 1000.)
                        | _ -> None

                    let height =
                        match Map.tryFind "ht" queryParamsMap with
                        | Some (Route.Number weight) -> Some weight
                        | _ -> None

                    let age = Patient.Age.fromBirthDate DateTime.Now (DateTime(year, month, day))

                    let patient =
                        Patient.create
                            (Some age.Years)
                            (Some age.Months)
                            (Some age.Weeks)
                            (Some age.Days)
                            weight
                            height
                    Logging.log "parsed: " patient
                    patient
                | _ ->
                    Logging.warning "could not parse url to patient" sl
                    None

            let page =
                match queryParamsMap |> Map.tryFind "pg" with
                | Some s when s = "el" -> Some Global.LifeSupport
                | Some s when s = "cm" -> Some Global.ContinuousMeds
                | Some s when s = "pr" -> Some Global.Prescribe
                | Some s when s = "fm" -> Some Global.Formulary
                | Some s when s = "pe" -> Some Global.Parenteralia
                | _ -> None

            pat, page

        | _ ->
            sl
            |> String.concat ""
            |> Logging.warning "could not parse url"

            None, None


    let init () : Model * Cmd<Msg> =
        let pat, page = Router.currentUrl () |> parseUrl

        let initialState =
            {
                Page = page |> Option.defaultValue Global.LifeSupport
                Patient = pat
                BolusMedication = HasNotStartedYet
                ContinuousMedication = HasNotStartedYet
                Products = HasNotStartedYet
                Scenarios = HasNotStartedYet
                Formulary = HasNotStartedYet
            }

        let cmds =
            Cmd.batch [
                Cmd.ofMsg (LoadBolusMedication Started)
                Cmd.ofMsg (LoadContinuousMedication Started)
                Cmd.ofMsg (LoadProducts Started)
            ]

        initialState, cmds


    let update (msg: Msg) (state: Model) =
        match msg with

        | UpdatePatient p ->
            { state with Patient = p },
            Cmd.ofMsg (LoadScenarios Started)

        | UrlChanged sl ->
            let pat, page = sl |> parseUrl

            { state with
                Page = page |> Option.defaultValue LifeSupport
                Patient = pat
            },
            Cmd.none

        | UpdatePage page ->
            printfn $"update page: {page}"
            let cmd =
                match page with
                | p when p = Prescribe ->
                    match state.Scenarios with
                    | Resolved _ -> Cmd.none
                    | _ ->
                        printfn "load scenarios started"
                        LoadScenarios Started |> Cmd.ofMsg
                | p when p = Formulary ->
                    match state.Formulary with
                    | Resolved _ -> Cmd.none
                    | _ ->
                        printfn "load formulary started"
                        LoadFormulary Started |> Cmd.ofMsg

                | _ -> Cmd.none

            { state with
                Page = page
            }
            , cmd

        | LoadBolusMedication Started ->
            { state with
                BolusMedication = InProgress
            },
            Cmd.fromAsync (GoogleDocs.loadBolusMedication LoadBolusMedication)

        | LoadBolusMedication (Finished (Ok meds)) ->

            { state with
                BolusMedication = meds |> Resolved
            },
            Cmd.none

        | LoadBolusMedication (Finished (Error s)) ->
            Logging.error "cannot load emergency treatment" s
            state, Cmd.none

        | LoadContinuousMedication Started ->
            { state with
                ContinuousMedication = InProgress
            },
            Cmd.fromAsync (
                GoogleDocs.loadContinuousMedication LoadContinuousMedication
            )

        | LoadContinuousMedication (Finished (Ok meds)) ->

            { state with
                ContinuousMedication = meds |> Resolved
            },
            Cmd.none

        | LoadContinuousMedication (Finished (Error s)) ->
            Logging.error "cannot load continuous medication" s
            state, Cmd.none

        | LoadProducts Started ->
            { state with Products = InProgress },
            Cmd.fromAsync (GoogleDocs.loadProducts LoadProducts)

        | LoadProducts (Finished (Ok prods)) ->

            { state with
                Products = prods |> Resolved
            },
            Cmd.none

        | LoadProducts (Finished (Error s)) ->
            Logging.error "cannot load products" s
            state, Cmd.none

        | LoadScenarios Started ->
            let scenarios =
                match state.Scenarios with
                | Resolved sc when state.Patient.IsSome ->
                    { sc with
                        Age =
                            match state.Patient with
                            | Some pat -> pat |> Patient.getAgeInDays
                            | None -> sc.Age
                        Weight =
                            match state.Patient with
                            | Some pat -> pat |> Patient.getWeight
                            | None -> sc.Weight
                        Height =
                            match state.Patient with
                            | Some pat -> pat |> Patient.getHeight
                            | None -> sc.Height
                    }
                | _ -> ScenarioResult.empty

            let load =
                async {
                    let! result = serverApi.getScenarioResult scenarios
                    return Finished result |> LoadScenarios
                }

            { state with Scenarios = InProgress }, Cmd.fromAsync load

        | LoadScenarios (Finished (Ok result)) ->
            { state with
                Scenarios = Resolved result
            },
            Cmd.none

        | LoadScenarios (Finished (Error msg)) ->
            Logging.log "scenarios" msg
            state, Cmd.none

        | UpdateScenarios sc ->
            let sc =
                { sc with
                    Weight =
                        match state.Patient with
                        | Some pat -> pat |> Patient.getWeight
                        | None -> sc.Weight
                }

            { state with Scenarios = Resolved sc },
            Cmd.ofMsg (LoadScenarios Started)

        | LoadFormulary Started ->
            let form =
                match state.Formulary with
                | Resolved form when state.Patient.IsSome ->
                    { form with
                        Age =
                            match state.Patient with
                            | Some pat -> pat |> Patient.getAgeInDays
                            | None -> form.Age
                        Weight =
                            match state.Patient with
                            | Some pat -> pat |> Patient.getWeight
                            | None -> form.Weight
                    }
                | Resolved form -> form
                | _ -> Formulary.empty

            let load =
                async {
                    let! result = serverApi.getFormulary form
                    return Finished result |> LoadFormulary
                }

            { state with Scenarios = InProgress }, Cmd.fromAsync load

        | LoadFormulary (Finished (Ok form)) ->
            { state with
                Formulary = Resolved form
            },
            Cmd.none

        | UpdateFormulary form ->
            let state =
                { state with
                    Formulary = Resolved form
                }
            state, Cmd.ofMsg (LoadFormulary Started)


    let calculatInterventions calc meds pat =
        meds
        |> Deferred.bind (fun xs ->
            match pat with
            | None -> InProgress
            | Some p ->
                let a = p |> Patient.getAgeInYears
                let w = p |> Patient.getWeight
                xs |> calc a w |> Resolved
        )

open Elmish
open Shared



[<Literal>]
let private themeDef = """createTheme({
})"""

[<Import("createTheme", from="@mui/material/styles")>]
[<Emit(themeDef)>]
let private theme : obj = jsNative


// Entry point must be in a separate file
// for Vite Hot Reload to work

[<JSX.Component>]
let View () =
    let state, dispatch = React.useElmish (init, update, [||])

    let bm =
        calculatInterventions
            EmergencyTreatment.calculate
            state.BolusMedication
            state.Patient

    let cm =
        let calc =
            fun _ w meds ->
                match w with
                | Some w' -> ContinuousMedication.calculate w' meds
                | None -> []

        calculatInterventions calc state.ContinuousMedication state.Patient

    JSX.jsx
        $"""
    import {{ ThemeProvider }} from '@mui/material/styles';
    import CssBaseline from '@mui/material/CssBaseline';
    import React from "react";
    import Box from '@mui/material/Box';

    <React.StrictMode>
        <ThemeProvider theme={theme}>
            <Box sx={ {| height= "100vh"; overflowY = "hidden" |} }>
                <CssBaseline />
                {
                    Components.Router.View {| onUrlChanged = UrlChanged >> dispatch |}
                }
                {
                    Pages.GenPres.View({|
                        patient = state.Patient
                        updatePage = UpdatePage >> dispatch
                        updatePatient = UpdatePatient >> dispatch
                        bolusMedication = bm
                        continuousMedication = cm
                        products = state.Products
                        scenario = state.Scenarios
                        updateScenario = UpdateScenarios >> dispatch
                        formulary = state.Formulary
                        updateFormulary = UpdateFormulary >> dispatch
                        page = state.Page
                    |})
                }
            </Box>
        </ThemeProvider>
    </React.StrictMode>
    """


let root = ReactDomClient.createRoot (document.getElementById ("genpres-app"))
root.render (View() |> toReact)
