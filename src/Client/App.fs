module App

open System
open Fable.Core
open Browser
open Fable.React
open Elmish
open Feliz.Router
open Fable.Remoting.Client
open Utils
open Shared
open Shared.Types
open Shared.Models
open Global


module private Elmish =


    type State =
        {
            Page : Global.Pages
            Patient: Patient option
            BolusMedication: Deferred<BolusMedication list>
            ContinuousMedication: Deferred<ContinuousMedication list>
            Products: Deferred<Product list>
            PrescriptionContext: Deferred<PrescriptionContext>
            TreatmentPlan : Deferred<TreatmentPlan>
            Formulary: Deferred<Formulary>
            Parenteralia: Deferred<Parenteralia>
            Localization : Deferred<string [][]>
            Hospitals : Deferred<string []>
            Context : Global.Context
            ShowDisclaimer: bool
            IsDemo : bool
            SnackbarMsg : string
            SnackbarOpen : bool
        }


    type Msg =
        | UrlChanged of string list
        | AcceptDisclaimer

        | UpdatePage of Global.Pages
        | UpdatePatient of Patient option

        | LoadBolusMedication of AsyncOperationStatus<Result<BolusMedication list, string>>
        | LoadContinuousMedication of AsyncOperationStatus<Result<ContinuousMedication list, string>>
        | LoadProducts of AsyncOperationStatus<Result<Product list, string>>

        | UpdatePrescriptionContext of PrescriptionContext
        | LoadPrescriptionContext of AsyncOperationStatus<Result<Api.Message, string []>>

        | UpdateTreatmentPlan of TreatmentPlan
        | LoadTreatmentPlan of AsyncOperationStatus<Result<Api.Message, string []>>

        | UpdateFormulary of Formulary
        | LoadFormulary of AsyncOperationStatus<Result<Api.Message, string []>>

        | UpdateParenteralia of Parenteralia
        | LoadParenteralia of AsyncOperationStatus<Result<Api.Message, string []>>

        | UpdateLanguage of Localization.Locales
        | LoadLocalization of AsyncOperationStatus<Result<string [][], string>>

        | UpdateHospital of string
        | CloseSnackbar


    let serverApi =
        Remoting.createApi ()
        |> Remoting.withRouteBuilder Api.routerPaths
        |> Remoting.buildProxy<Api.IServerApi>


    let createApiMsg msg cmd =
        async {
            let! result = serverApi.processMessage cmd
            return Finished result |> msg
        }
        |> Cmd.fromAsync


    let processApiMsg state msg =
        match msg with
        | Api.PrescriptionContextMsg pr ->
                { state with
                    PrescriptionContext = Resolved pr
                }, Cmd.none
        | Api.TreatmentPlanMsg tp ->
            {  state with
                TreatmentPlan = Resolved tp
            }, Cmd.none
        | Api.FormularyMsg form ->
            { state with
                Formulary = Resolved form
            }, Cmd.none
        | Api.ParenteraliaMsg par ->
            { state with
                Parenteralia = Resolved par
            }, Cmd.none


    let loadPresciptionContext = Api.PrescriptionContextMsg >> createApiMsg LoadPrescriptionContext


    let loadTreatmentPlan = Api.TreatmentPlanMsg >> createApiMsg LoadTreatmentPlan


    let loadFormuarly = Api.FormularyMsg >> createApiMsg LoadFormulary


    let loadParenteralia = Api.ParenteraliaMsg >> createApiMsg LoadParenteralia



    // url needs to be in format: http://localhost:8080/#patient?by=2&bm=0&bd=1
    // * pg : el (emergency list) cm (continuous medication) pr (prescribe)
    // * ad: age in days
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
    // * md: medication
    // * rt: route
    // * in: indication
    // * dt: dosetype
    let parseUrl sl =
        Logging.log $"parsing url" sl
        match sl with
        | [] -> None, None, None, true, None
        | [ "patient"; Route.Query queryParams ] ->
            let paramsMap = Map.ofList queryParams

            let pat =
                match Map.tryFind "by" paramsMap, Map.tryFind "ad" paramsMap with
                | Some (Route.Int year), _ ->
                    // birthday year is required
                    let month =
                        match Map.tryFind "bm" paramsMap with
                        | Some (Route.Int months) -> months
                        | _ -> 1 // january is the default

                    let day =
                        match Map.tryFind "bd" paramsMap with
                        | Some (Route.Int days) -> days
                        | _ -> 1 // first day of the month is the default

                    let weight =
                        match Map.tryFind "wt" paramsMap with
                        | Some (Route.Int weight) -> Some weight
                        | _ -> None

                    let height =
                        match Map.tryFind "ht" paramsMap with
                        | Some (Route.Int height) -> Some height
                        | _ -> None

                    let gaWeeks =
                        match Map.tryFind "gw" paramsMap with
                        | Some (Route.Int weeks) -> weeks |> Some
                        | _ -> None

                    let gaDays =
                        match Map.tryFind "gd" paramsMap with
                        | Some (Route.Int days) -> days |> Some
                        | _ -> None

                    let cvl =
                        match Map.tryFind "cv" paramsMap with
                        | Some s when s = "y" -> true
                        | _ -> false

                    let dep =  Map.tryFind "dp"paramsMap

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
                            UnknownGender
                            [ CVL ]
                            None
                            dep

                    Logging.log "parsed: " patient
                    patient
                | _, Some (Route.Int days) ->
                    let weight =
                        match Map.tryFind "wt" paramsMap with
                        | Some (Route.Int weight) -> Some weight
                        | _ -> None

                    let height =
                        match Map.tryFind "ht" paramsMap with
                        | Some (Route.Int height) -> Some height
                        | _ -> None

                    let gaWeeks =
                        match Map.tryFind "gw" paramsMap with
                        | Some (Route.Int weeks) -> weeks |> Some
                        | _ -> None

                    let gaDays =
                        match Map.tryFind "gd" paramsMap with
                        | Some (Route.Int days) -> days |> Some
                        | _ -> None

                    let cvl =
                        match Map.tryFind "cv" paramsMap with
                        | Some s when s = "y" -> [ CVL ]
                        | _ -> []

                    let dep =  Map.tryFind "dp"paramsMap

                    let age = Patient.Age.fromDays days

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
                            UnknownGender
                            cvl
                            None
                            dep

                    Logging.log "parsed: " patient
                    patient

                | _ ->
                    Logging.warning "could not parse url to patient" sl
                    None

            let page =
                match paramsMap |> Map.tryFind "pg" with
                | Some s when s = "el" -> Some Global.LifeSupport
                | Some s when s = "cm" -> Some Global.ContinuousMeds
                | Some s when s = "pr" -> Some Global.Prescribe
                | Some s when s = "fm" -> Some Global.Formulary
                | Some s when s = "pe" -> Some Global.Parenteralia
                | _ -> None

            let lang =
                match paramsMap |> Map.tryFind "la" with
                | Some s when s = "en" -> Some Localization.English
                | Some s when s = "du" -> Some Localization.Dutch
                | Some s when s = "fr" -> Some Localization.French
                | Some s when s = "gr" -> Some Localization.German
                | Some s when s = "sp" -> Some Localization.Spanish
                | Some s when s = "it" -> Some Localization.Italian
//                | Some s when s = "ch" -> Some Localization.Chinees // refact: to Chinese
                | _ -> None

            let discl =
                match paramsMap |> Map.tryFind "dc" with
                | Some s when s = "n" -> false
                | _ -> true

            let med =
                {|
                    medication = paramsMap |> Map.tryFind "md"
                    route = paramsMap |> Map.tryFind "rt"
                    indication = paramsMap |> Map.tryFind "in"
                    dosetype =
                        paramsMap
                        |> Map.tryFind "dt"
                        |> Option.map PrescriptionContext.doseTypeFromString
                |}
                |> Some

            pat, page, lang, discl, med

        | _ ->
            sl
            |> String.concat ""
            |> Logging.warning "could not parse url"

            None, None, None, true, None


    let initialState pat page lang discl (med : {| medication: string option; route: string option; indication: string option; dosetype: DoseType option |} option) =
        {
            ShowDisclaimer = discl
            Page = page |> Option.defaultValue Global.LifeSupport
            Patient = pat
            BolusMedication = HasNotStartedYet
            ContinuousMedication = HasNotStartedYet
            Products = HasNotStartedYet
            PrescriptionContext = HasNotStartedYet
            TreatmentPlan =
                match pat with
                | None -> HasNotStartedYet
                | Some p -> TreatmentPlan.create p [||] |> Resolved
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
            SnackbarMsg = ""
            SnackbarOpen = false
        }

    let init () : State * Cmd<Msg> =
        let pat, page, lang, discl, med = Router.currentUrl () |> parseUrl

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

        initialState pat page lang discl med
        , cmds


    let update (msg: Msg) (state: State) =
        let processOk = processApiMsg state
        let processError s =
            Logging.error "error" s
            state, Cmd.none


        match msg with
        | CloseSnackbar ->
            { state with
                SnackbarMsg = ""
                SnackbarOpen = false
            }, Cmd.none

        | AcceptDisclaimer ->
            { state with
                ShowDisclaimer = false
            },
            Cmd.none

        | UpdateLanguage lang ->
            { state with
                ShowDisclaimer = true
                Context =  { state.Context with Localization = lang }
            }, Cmd.none

        | UpdateHospital hosp ->
            { state with
                ShowDisclaimer = true
                Context =  { state.Context with Hospital = hosp }
            }, Cmd.none

        | UpdatePage p ->
            { state with
                Page = p
            }, Cmd.none

        | UpdatePatient p ->
            { state with
                Patient = p
                PrescriptionContext =
                    match p with
                    | None -> HasNotStartedYet
                    | Some p ->
                        PrescriptionContext.empty
                        |> PrescriptionContext.setPatient p
                        |> Resolved
                TreatmentPlan =
                    match p with
                    | None -> HasNotStartedYet
                    | Some p ->
                        let tp = TreatmentPlan.create p [||]
                        state.TreatmentPlan
                        |> Deferred.map (fun tp ->
                            { tp with Patient = p }
                        )
                        |> Deferred.defaultValue tp
                        |> Resolved
                Formulary =
                    {Formulary.empty with Patient = p }
                    |> Resolved
                Parenteralia =
                    Parenteralia.empty
                    |> Resolved
            },
            Cmd.batch [
                Cmd.ofMsg (LoadPrescriptionContext Started)
                Cmd.ofMsg (LoadTreatmentPlan Started)
                Cmd.ofMsg (LoadFormulary Started)
                Cmd.ofMsg (LoadParenteralia Started)
            ]

        | UrlChanged sl ->
            let pat, page, lang, discl, med = sl |> parseUrl

            { state with
                ShowDisclaimer = discl
                Page = page |> Option.defaultValue LifeSupport
                Patient = pat
                Context =
                    { state.Context with
                        Localization =
                            lang |> Option.defaultValue Localization.English
                    }
            },
            Cmd.ofMsg (pat |> UpdatePatient)

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


        | UpdatePrescriptionContext pr ->
            let pr =
                { pr with
                    Patient =
                        state.Patient
                        |> Option.defaultValue pr.Patient
                }

            { state with
                PrescriptionContext = Resolved pr
                Formulary =
                    state.Formulary
                    |> Deferred.map (fun form ->
                        { form with
                            Indication = pr.Filter.Indication
                            Generic = pr.Filter.Medication
                            Route = pr.Filter.Route
                        }
                    )
                Parenteralia =
                    state.Parenteralia
                    |> Deferred.map (fun par ->
                        { par with
                            Generic = pr.Filter.Medication
                            Shape = pr.Filter.Shape
                            Route = pr.Filter.Route
                        }
                    )
            },
            Cmd.batch [
                Cmd.ofMsg (LoadPrescriptionContext Started)
                Cmd.ofMsg (LoadFormulary Started)
                Cmd.ofMsg (LoadParenteralia Started)
            ]

        | UpdateTreatmentPlan tp ->
            let cmd =
                if state.Page = TreatmentPlan then
                    Cmd.ofMsg (LoadTreatmentPlan Started)
                else
                    Cmd.batch [
                        Cmd.ofMsg (UpdatePrescriptionContext PrescriptionContext.empty)
                        Cmd.ofMsg (LoadTreatmentPlan Started)
                    ]

            { state with
                Page = TreatmentPlan
                TreatmentPlan = Resolved tp
            }, cmd

        | LoadFormulary Started ->
            let form =
                match state.Formulary with
                | Resolved form ->
                    { form with
                        Patient =
                            state.Patient
                    }
                | _ -> Formulary.empty

            let cmd = form |> loadFormuarly

            { state with Formulary = InProgress }, cmd

        | LoadFormulary (Finished (Ok msg)) -> processOk msg

        | LoadFormulary (Finished(Error err)) ->
            Logging.error "LoadFormulary error:" err
            state,
            Cmd.none

        | UpdateFormulary form ->
            let state =
                { state with
                    Formulary = Resolved form
                    PrescriptionContext =
                        state.PrescriptionContext
                        |> Deferred.map (fun scr ->
                            { scr with
                                Filter =
                                    { scr.Filter with
                                        Indication = form.Indication
                                        Medication = form.Generic
                                        Route = form.Route
                                    }
                            }
                        )
                }
            state,
            Cmd.batch [
                Cmd.ofMsg (LoadFormulary Started)
                Cmd.ofMsg (LoadPrescriptionContext Started)
            ]

        | LoadParenteralia Started ->
            let cmd =
                let par =
                    state.Parenteralia
                    |> Deferred.defaultValue Parenteralia.empty

                loadParenteralia par
            { state with Parenteralia = InProgress }, cmd

        | LoadParenteralia (Finished(Ok msg)) -> msg |> processOk

        | LoadParenteralia (Finished (Error err)) ->
            Logging.error "LoadParenteralia finished with error:" err
            state, Cmd.none

        | UpdateParenteralia par ->
            let state =
                { state with
                    Parenteralia = Resolved par
                }
            state, Cmd.ofMsg(LoadParenteralia Started)

        | LoadPrescriptionContext Started ->
            match state.Patient with
            | None -> { state with PrescriptionContext = HasNotStartedYet }, Cmd.none
            | Some pat ->
                match state.PrescriptionContext with
                | HasNotStartedYet ->
                        { state with PrescriptionContext = InProgress },
                        PrescriptionContext.empty
                        |> PrescriptionContext.setPatient pat
                        |> loadPresciptionContext
                | InProgress -> state, Cmd.none
                | Resolved pr ->
                    let pr = { pr with Patient = pat }

                    { state with PrescriptionContext = InProgress },
                    pr |> loadPresciptionContext

        | LoadPrescriptionContext (Finished (Ok msg)) -> msg |> processOk
        | LoadPrescriptionContext (Finished (Error err)) -> err |> processError

        | LoadTreatmentPlan Started ->
            match state.Patient with
            | None -> { state with TreatmentPlan = HasNotStartedYet }, Cmd.none
            | Some pat ->
                match state.TreatmentPlan with
                | HasNotStartedYet ->
                    { state with TreatmentPlan = InProgress },
                    TreatmentPlan.create pat [||]
                    |> loadTreatmentPlan
                | InProgress -> state, Cmd.none
                | Resolved tp ->
                    { state with TreatmentPlan = InProgress },
                    tp |> loadTreatmentPlan

        | LoadTreatmentPlan (Finished (Ok msg)) -> msg |> processOk
        | LoadTreatmentPlan (Finished (Error err)) -> err |> processError


    let calculatInterventions calc meds pat =
        meds
        |> Deferred.bind (fun xs ->
            match pat with
            | None -> InProgress
            | Some p ->
                let a = p |> Patient.getAgeInYears
                let w = p |> Patient.getWeightInKg
                xs |> calc a w |> Resolved
        )


open Elmish


[<Literal>]
let private themeDef = """
responsiveFontSizes(createTheme({ typography: { fontSize : 14, } }), { factor : 2 })
"""


[<Import("createTheme", from="@mui/material/styles")>]
[<Emit(themeDef)>]
let private theme : obj = jsNative


[<Literal>]
let private mobileDef = """
responsiveFontSizes(createTheme({ typography: { fontSize : 12, } }), { factor : 2 })
"""


[<Import("createTheme", from="@mui/material/styles")>]
[<Emit(mobileDef)>]
let private mobile : obj = jsNative


// Entry point must be in a separate file
// for Vite Hot Reload to work

[<JSX.Component>]
let View () =
    let state, dispatch = React.useElmish (init, update, [||])
    let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

    let handleClose = fun _ -> CloseSnackbar |> dispatch

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

    let sx =
        if isMobile
        then
            {| height= "100vh"; overflowY = "hidden"; mb=5 |}
        else
            {| height= "100vh"; overflowY = "hidden"; mb=0 |}

    let theme = if isMobile then mobile else theme

    JSX.jsx
        $"""
    import {{ ThemeProvider }} from '@mui/material/styles';
    import {{ responsiveFontSizes }} from '@mui/material/styles';
    import CssBaseline from '@mui/material/CssBaseline';
    import React from "react";
    import Box from '@mui/material/Box';
    import Snackbar from '@mui/material/Snackbar';
    import IconButton from '@mui/material/IconButton';
    import CloseIcon from '@mui/icons-material/Close';

    <React.StrictMode>
        <ThemeProvider theme={theme}>
            <Box sx={ sx }>
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
                        prescriptionContext = state.PrescriptionContext
                        updatePrescriptionContext = UpdatePrescriptionContext >> dispatch
                        treatmentPlan = state.TreatmentPlan
                        updateTreatmentPlan = UpdateTreatmentPlan >> dispatch
                        formulary = state.Formulary
                        updateFormulary = UpdateFormulary >> dispatch
                        parenteralia = state.Parenteralia
                        updateParenteralia = UpdateParenteralia >> dispatch
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
            <div>
                <Snackbar
                    open={ state.SnackbarOpen }
                    autoHideDuration={3000}
                    message={ state.SnackbarMsg }
                    onClose={handleClose}
                />
            </div>
        </ThemeProvider>
    </React.StrictMode>
    """


let root = ReactDomClient.createRoot (document.getElementById ("genpres-app"))
root.render (View() |> toReact)
