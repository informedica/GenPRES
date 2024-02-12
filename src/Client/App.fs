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
            SelectedScenarioOrder : (Scenario * Order) option
            CalculatedOrder : Deferred<(bool * Order) option>
            Formulary: Deferred<Formulary>
            Parenteralia: Deferred<Parenteralia>
            Localization : Deferred<string [][]>
            Hospitals : Deferred<string []>
            Context : Global.Context
            ShowDisclaimer: bool
            IsDemo : bool
        }

    type Msg =
        | AcceptDisclaimer
        | UpdatePatient of Patient option
        | UpdatePage of Global.Pages
        | UrlChanged of string list
        | LoadBolusMedication of
            AsyncOperationStatus<Result<BolusMedication list, string>>
        | LoadContinuousMedication of
            AsyncOperationStatus<Result<ContinuousMedication list, string>>
        | LoadProducts of AsyncOperationStatus<Result<Product list, string>>
        | LoadScenarios of AsyncOperationStatus<Result<ScenarioResult, string>>
        | PrintScenarios of AsyncOperationStatus<Result<ScenarioResult, string>>
        | UpdateScenarios of ScenarioResult
        | SelectOrder of (Scenario * Order option)
        | UpdateScenarioOrder
        | LoadOrder of Order
        | CalculateOrder of AsyncOperationStatus<Result<Order, string>>
        | LoadFormulary of AsyncOperationStatus<Result<Formulary, string>>
        | UpdateFormulary of Formulary
        | LoadParenteralia of AsyncOperationStatus<Result<Parenteralia, string>>
        | UpdateParenteralia of Parenteralia
        | LoadLocalization of AsyncOperationStatus<Result<string [][], string>>
        | UpdateLanguage of Localization.Locales
        | UpdateHospital of string


    let serverApi =
        Remoting.createApi ()
        |> Remoting.withRouteBuilder Api.routerPaths
        |> Remoting.buildProxy<Api.IServerApi>


    // url needs to be in format: http://localhost:8080/#patient?ay=2&am=0&ad=1
    // * pg : el (emergency list) cm (continuous medication)
    // * by: birth year
    // * bm: birth month
    // * bd: birth day
    // * wt: weight (gram)
    // * ht: height (cm)
    // * gw: gestational age weeks
    // * gd: gestational age days
    // * la: language (en; du; fr; ge; sp; it; ch)
    // * dc: show disclaimer (n;_)
    // * cv: central venous line (y;_)
    // * dp: department
    let parseUrl sl =
        printfn $"parsing url with {sl}"
        match sl with
        | [] -> None, None, None, true
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

                    let gaWeeks =
                        match Map.tryFind "gw" queryParamsMap with
                        | Some (Route.Number weeks) -> weeks |> int |> Some
                        | _ -> None

                    let gaDays =
                        match Map.tryFind "gd" queryParamsMap with
                        | Some (Route.Number days) -> days |> int |> Some
                        | _ -> None

                    let cvl =
                        match Map.tryFind "cv" queryParamsMap with
                        | Some s when s = "y" -> true
                        | _ -> false

                    let dep =  Map.tryFind "dp"queryParamsMap 

                    let age = Patient.Age.fromBirthDate DateTime.Now (DateTime(year, month, day))

                    let patient =
                        Patient.create
                            (Some age.Years)
                            (Some age.Months)
                            (Some age.Weeks)
                            (Some age.Days)
                            weight
                            height
                            gaWeeks 
                            gaDays
                            cvl
                            dep

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

            let lang = 
                match queryParamsMap |> Map.tryFind "la" with
                | Some s when s = "en" -> Some Localization.English
                | Some s when s = "du" -> Some Localization.Dutch
                | Some s when s = "fr" -> Some Localization.French
                | Some s when s = "gr" -> Some Localization.German
                | Some s when s = "sp" -> Some Localization.Spanish
                | Some s when s = "it" -> Some Localization.Italian
//                | Some s when s = "ch" -> Some Localization.Chinees // refact: to Chinese
                | _ -> None

            let discl =
                match queryParamsMap |> Map.tryFind "dc" with
                | Some s when s = "n" -> false
                | _ -> true

            pat, page, lang, discl

        | _ ->
            sl
            |> String.concat ""
            |> Logging.warning "could not parse url"

            None, None, None, true

    let initialState pat page lang discl =
        {
            ShowDisclaimer = discl
            Page = page |> Option.defaultValue Global.LifeSupport
            Patient = pat
            BolusMedication = HasNotStartedYet
            ContinuousMedication = HasNotStartedYet
            Products = HasNotStartedYet
            Scenarios = HasNotStartedYet
            CalculatedOrder = HasNotStartedYet
            SelectedScenarioOrder = None
            Formulary = HasNotStartedYet
            Parenteralia = HasNotStartedYet
            Localization = HasNotStartedYet
            Hospitals = HasNotStartedYet
            Context =
                { 
                    Localization = lang |> Option.defaultValue Localization.Dutch
                    Hospital = "UMCU"
                }
            IsDemo = false
        }

    let init () : Model * Cmd<Msg> =
        let pat, page, lang, discl = Router.currentUrl () |> parseUrl

        let cmds =
            Cmd.batch [
                Cmd.ofMsg (pat |> UpdatePatient)
                Cmd.ofMsg (LoadBolusMedication Started)
                Cmd.ofMsg (LoadContinuousMedication Started)
                Cmd.ofMsg (LoadProducts Started)
                Cmd.ofMsg (LoadLocalization Started)
                Cmd.ofMsg (LoadFormulary Started)
                Cmd.ofMsg (LoadParenteralia Started)
            ]

        initialState pat page lang discl
        , cmds


    let update (msg: Msg) (state: Model) =
        match msg with
        | AcceptDisclaimer ->
            { state with
                ShowDisclaimer = false
            },
            Cmd.none

        | UpdateLanguage lang ->
            printfn $"update language to: {lang}"
            { state with 
                ShowDisclaimer = true
                Context =  { state.Context with Localization = lang } 
            }, Cmd.none

        | UpdateHospital hosp ->
            printfn $"update hospital to: {hosp}"
            { state with 
                ShowDisclaimer = true
                Context =  { state.Context with Hospital = hosp } 
            }, Cmd.none

        | UpdatePatient p ->
            printfn $"load patient: {p}"
            { state with Patient = p },
            Cmd.batch [
                Cmd.ofMsg (LoadScenarios Started)
                Cmd.ofMsg (LoadFormulary Started)
            ]

        | UrlChanged sl ->
            printfn "url changed"
            let pat, page, lang, discl = sl |> parseUrl

            { state with
                ShowDisclaimer = discl
                Page = page |> Option.defaultValue LifeSupport
                Patient = pat
                Context =  
                    {state.Context with 
                        Localization =
                            lang |> Option.defaultValue Localization.English 
                    } 
            },
            Cmd.ofMsg (pat |> UpdatePatient)

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

        | LoadLocalization Started ->
            { state with
                Localization = InProgress
            },
            Cmd.fromAsync (GoogleDocs.loadLocalization LoadLocalization)

        | LoadLocalization (Finished (Ok terms)) ->

            { state with
                Localization = terms |> Resolved
            },
            Cmd.none

        | LoadLocalization (Finished (Error s)) ->
            Logging.error "cannot load localization" s
            state, Cmd.none

        | LoadBolusMedication Started ->
            { state with
                BolusMedication = InProgress
            },
            Cmd.fromAsync (GoogleDocs.loadBolusMedication LoadBolusMedication)

        | LoadBolusMedication (Finished (Ok meds)) ->

            { state with
                BolusMedication = meds |> Resolved
                Hospitals =
                    meds
                    |> List.map _.Hospital
                    |> List.distinct
                    |> List.filter (String.isNullOrWhiteSpace >> not)
                    |> List.toArray
                    |> Resolved
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
                match state.Scenarios, state.Patient with
                | Resolved sc, Some _ -> sc
                | _ -> ScenarioResult.empty
                |> fun sc ->
                    { sc with
                        AgeInDays =
                            state.Patient 
                            |> Option.bind Patient.getAgeInDays
                        GestAgeInDays =
                            state.Patient 
                            |> Option.bind Patient.getGestAgeInDays
                        WeightInKg =
                            state.Patient 
                            |> Option.bind Patient.getWeight
                        HeightInCm =
                            state.Patient 
                            |> Option.bind Patient.getHeight
                        CVL =
                            match state.Patient with
                            | Some pat -> pat.CVL
                            | None -> false
                        Department = 
                            state.Patient 
                            |> Option.bind (fun p -> p.Department)
                    }

            let load =
                async {
                    let! result = serverApi.getScenarioResult scenarios
                    return Finished result |> LoadScenarios
                }

            { state with Scenarios = InProgress }, Cmd.fromAsync load

        | LoadScenarios (Finished (Ok result)) ->
            { state with
                Scenarios = Resolved result
                IsDemo = result.DemoVersion
            },
            Cmd.none

        | LoadScenarios (Finished (Error msg)) ->
            Logging.error "error" msg
            state, Cmd.none

        | PrintScenarios Started ->
            let scenarios =
                match state.Scenarios with
                | Resolved sc when state.Patient.IsSome -> sc
                | _ -> ScenarioResult.empty

            let load =
                async {
                    let! result = serverApi.printScenarioResult scenarios
                    return Finished result |> PrintScenarios
                }

            { state with Scenarios = InProgress }, Cmd.fromAsync load

        | PrintScenarios (Finished (Ok result)) ->
            { state with
                Scenarios = Resolved result
            },
            Cmd.none

        | PrintScenarios (Finished (Error msg)) ->
            Logging.error "error" msg
            state, Cmd.none

        | UpdateScenarios sc ->
            let sc =
                { sc with
                    WeightInKg =
                        match state.Patient with
                        | Some pat -> pat |> Patient.getWeight
                        | None -> sc.WeightInKg
                }

            { state with 
                Scenarios = Resolved sc 
                Formulary = 
                    state.Formulary
                    |> Deferred.map (fun form ->
                        { form with
                            Indication = sc.Indication
                            Generic = sc.Medication
                            Route = sc.Route
                        }
                    )
                Parenteralia =
                    state.Parenteralia
                    |> Deferred.map (fun par ->
                        { par with
                            Generic = sc.Medication
                            Shape = sc.Shape
                            Route = sc.Route
                        }
                    )
            },
            Cmd.batch [
                Cmd.ofMsg (LoadScenarios Started)
                Cmd.ofMsg (LoadFormulary Started)
                Cmd.ofMsg (LoadParenteralia Started)
            ]

        | UpdateScenarioOrder ->
            match state.SelectedScenarioOrder with
            | Some (sc, o) ->
                { state with
                    Scenarios =
                        state.Scenarios
                        |> Deferred.map (fun scr ->
                            { scr with
                                Scenarios =
                                    scr.Scenarios
                                    |> Array.map (fun scr ->
                                        if scr <> sc then scr
                                        else
                                            printfn "found scenario and update order"
                                            { scr with
                                                Order = Some o
                                            }
                                    )
                            }
                        )
                },
                Cmd.ofMsg (PrintScenarios Started)
            | None -> state, Cmd.none

        | SelectOrder (sc, o) ->
            { state with
                SelectedScenarioOrder =
                    if o |> Option.isNone then state.SelectedScenarioOrder
                    else
                        (sc, o |> Option.get) |> Some
                CalculatedOrder = 
                    o
                    |> Option.map (fun o ->
                        sc.UseAdjust, o
                    ) 
                    |> Resolved
            },
            Cmd.ofMsg (CalculateOrder Started)

        | LoadOrder o ->
            let load =
                async {
                    let! order = o |> serverApi.solveOrder
                    return Finished order |> CalculateOrder
                }

            { state with CalculatedOrder = InProgress }, Cmd.fromAsync load

        | CalculateOrder Started ->
            match state.CalculatedOrder with
            | Resolved (Some order) ->
                let load =
                    async {
                        let! order = order |> snd |> serverApi.calcMinIncrMax
                        return Finished order |> CalculateOrder
                    }
                { state with CalculatedOrder = InProgress }, Cmd.fromAsync load
            | _ -> state, Cmd.none

        | CalculateOrder (Finished r) ->
            match r with
            | Ok o ->
                printfn "success calculating order"
                { state with
                    SelectedScenarioOrder =
                        state.SelectedScenarioOrder
                        |> Option.map (fun (sc, _) -> sc, o)
                    CalculatedOrder = 
                        o 
                        |> Some 
                        |> Option.map (fun o ->
                            match state.SelectedScenarioOrder with
                            | None -> false, o
                            | Some (sc, _) -> sc.UseAdjust, o
                        )
                        |> Resolved
                    // show only the calculated order scenario
                    Scenarios = 
                        state.Scenarios
                        |> Deferred.map (fun scr ->
                            { scr with
                                Scenarios =
                                    scr.Scenarios
                                    |> Array.filter (fun sc ->
                                        sc.Order
                                        |> Option.map (fun so ->
                                            printfn $"comparing {so.Orderable} = {o.Orderable}"
                                            so.Id = o.Id
                                        )
                                        |> Option.defaultValue true
                                    )
                            }
                        )
                }, Cmd.none
            | Error s ->
                printfn "eror calculating order"
                { state with CalculatedOrder = None |> Resolved }, Cmd.none

        | LoadFormulary Started ->
            let form =
                match state.Formulary with
                | Resolved form ->
                    { form with
                        Age =
                            state.Patient 
                            |> Option.bind Patient.getAgeInDays
                        Weight =
                            state.Patient 
                            |> Option.bind Patient.getWeight
                        Height =
                            state.Patient
                            |> Option.bind Patient.getHeight
                        GestAge =
                            state.Patient
                            |> Option.bind Patient.getGestAgeInDays
                    }
                | _ -> Formulary.empty

            let load =
                async {
                    let! result = serverApi.getFormulary form
                    return Finished result |> LoadFormulary
                }

            { state with Formulary = InProgress }, Cmd.fromAsync load

        | LoadFormulary (Finished (Ok form)) ->
            { state with
                Formulary = Resolved form
            },
            Cmd.none

        | LoadFormulary (Finished(Error err)) ->
            printfn $"LoadFormulary error: {err}"
            state,
            Cmd.none

        | UpdateFormulary form ->
            let state =
                { state with
                    Formulary = Resolved form
                    Scenarios =
                        state.Scenarios
                        |> Deferred.map (fun scr ->
                            { scr with
                                Indication = form.Indication
                                Medication = form.Generic
                                Route = form.Route
                            }
                        )
                }
            state,
            Cmd.batch [
                Cmd.ofMsg (LoadFormulary Started)
                Cmd.ofMsg (LoadScenarios Started)
            ]

        | LoadParenteralia Started ->
            let load = 
                let par =
                    state.Parenteralia
                    |> Deferred.defaultValue Parenteralia.empty

                async {
                    let! result = par |> serverApi.getParenteralia
                    return Finished result |> LoadParenteralia
                }
            { state with Parenteralia = InProgress }, Cmd.fromAsync load

        | LoadParenteralia (Finished(Ok par)) ->

            { state with Parenteralia = Resolved par }, Cmd.none

        | LoadParenteralia (Finished (Error err)) ->
            printfn $"LoadParenteralia finished with error: {err}"
            state, Cmd.none

        | UpdateParenteralia par ->
            let state = 
                { state with
                    Parenteralia = Resolved par
                }
            state, Cmd.ofMsg(LoadParenteralia Started)


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
let private themeDef = """
responsiveFontSizes(createTheme())
"""


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
    import {{ responsiveFontSizes }} from '@mui/material/styles';
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
                        showDisclaimer = state.ShowDisclaimer
                        isDemo = state.IsDemo
                        acceptDisclaimer = fun _ -> AcceptDisclaimer |> dispatch
                        patient = state.Patient
                        updatePage = UpdatePage >> dispatch
                        updatePatient = UpdatePatient >> dispatch
                        bolusMedication = bm
                        continuousMedication = cm
                        products = state.Products
                        scenarioResult = state.Scenarios
                        updateScenario = UpdateScenarios >> dispatch
                        formulary = state.Formulary
                        updateFormulary = UpdateFormulary >> dispatch
                        parenteralia = state.Parenteralia
                        updateParenteralia = UpdateParenteralia >> dispatch
                        selectOrder = SelectOrder >> dispatch
                        order = state.CalculatedOrder
                        loadOrder = LoadOrder >> dispatch
                        updateScenarioOrder = (fun () -> UpdateScenarioOrder |> dispatch)
                        page = state.Page
                        localizationTerms = state.Localization
                        languages = Localization.languages
                        hospitals = state.Hospitals
                        switchLang = UpdateLanguage >> dispatch
                        switchHosp = UpdateHospital >> dispatch
                    |}) 
                    |> toReact |> Components.Context.Context state.Context
                }
            </Box>
        </ThemeProvider>
    </React.StrictMode>
    """


let root = ReactDomClient.createRoot (document.getElementById ("genpres-app"))
root.render (View() |> toReact)
