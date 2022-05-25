module App

open Elmish
open Elmish.HMR
open Feliz
open Feliz.Router
open Shared
open Types
open Global
open Utils
open System

type Model =
    {
        Configuration: Configuration Option
        Language: Localization.Locales
        Patient: Patient option
        BolusMedication: Deferred<BolusMedication list>
        ContinuousMedication: Deferred<ContinuousMedication list>
        Products: Deferred<Product list>
    }


type Msg =
    | UpdateLanguage of Localization.Locales
    | UpdatePatient of Patient option
    | UrlChanged of string list
    | LoadBolusMedication of
        AsyncOperationStatus<Result<BolusMedication list, string>>
    | LoadContinuousMedication of
        AsyncOperationStatus<Result<ContinuousMedication list, string>>
    | LoadProducts of AsyncOperationStatus<Result<Product list, string>>

// url needs to be in format: http://localhost:8080/#pat?ay=2&am=0&ad=1
// * by: birth year
// * bm: birth month
// * bd: birth day
// * wt: weight (kg)
// * ln: length (cm)
let parseUrlToPatient sl =
    Logging.log "url" sl

    match sl with
    | [] -> None
    | [ "pat"
        Route.Query [ "by", Route.Int by
                      "bm", Route.Int bm
                      "bd", Route.Int bd
                      "wt", Route.Number wt ] ] ->
        Logging.log "query params:" $"by: {by}"

        let age =
            Patient.Age.fromBirthDate DateTime.Now (DateTime(by, bm, bd))

        Patient.create
            (Some age.Years)
            (Some age.Months)
            (Some age.Weeks)
            (Some age.Days)
            (wt |> Some)
            None
        |> fun p ->
            Logging.log "parsed: " p
            p
    | _ ->
        sl
        |> String.concat ""
        |> Logging.warning "could not parse url"

        None


let init () : Model * Cmd<Msg> =

    let initialState =
        {
            Configuration = None
            Language = Localization.Dutch
            Patient = Router.currentUrl () |> parseUrlToPatient
            BolusMedication = HasNotStartedYet
            ContinuousMedication = HasNotStartedYet
            Products = HasNotStartedYet
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
    | UpdateLanguage l -> { state with Language = l }, Cmd.none
    | UpdatePatient p -> { state with Patient = p }, Cmd.none
    | UrlChanged sl ->
        Logging.log "url changed" sl

        { state with
            Patient = sl |> parseUrlToPatient
        },
        Cmd.none
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



[<ReactComponent>]
let render state dispatch =
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

    let main =
        Main.render
            (UpdateLanguage >> dispatch)
            state.Patient
            (UpdatePatient >> dispatch)
            bm
            cm
            state.Products

    React.router [
        router.onUrlChanged (UrlChanged >> dispatch)
        router.children main
    ]

[<ReactComponent>]
let context (state: Model) dispatch =
    React.contextProvider (
        Global.languageContext,
        state.Language,
        React.fragment [ render state dispatch ]
    )


//-:cnd:noEmit
#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif

//+:cnd:noEmit
Program.mkProgram init update context
//-:cnd:noEmit
#if DEBUG
|> Program.withConsoleTrace
#endif
|> Program.withReactSynchronous "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
//+:cnd:noEmit
|> Program.run