module App

open Elmish
open Feliz
open Feliz.Router
open Shared
open Types
open Global
open Utils

type State =
    {
        Configuration: Configuration Option
        Patient: Patient option
        ContinuousMedication: Deferred<ContinuousMedication list>
        BolusMedication: Deferred<BolusMedication list>
    }


type Msg =
    | UpdatePatient of Patient option
    | UrlChanged of string list
    | LoadContinuousMedication of
        AsyncOperationStatus<Result<ContinuousMedication list, string>>
    | LoadBolusMedication of
        AsyncOperationStatus<Result<BolusMedication list, string>>


let parseUrlToPatient sl =
    Utils.Logging.log "url" sl

    match sl with
    | [] -> None
    | [ "pat"
        Route.Query [ "ay", Route.Int ay; "am", Route.Int am; "ad", Route.Int ad ] ] ->
        Patient.create (Some ay) (Some am) (Some ad) None None None
    | _ -> None


let init () : State * Cmd<Msg> =

    let initialState =
        {
            Configuration = None
            Patient = Router.currentUrl () |> parseUrlToPatient
            ContinuousMedication = HasNotStartedYet
            BolusMedication = HasNotStartedYet
        }

    let cmds =
        Cmd.batch [
            Cmd.ofMsg (LoadBolusMedication Started)
            Cmd.ofMsg (LoadContinuousMedication Started)
        ]

    initialState, cmds


let update (msg: Msg) (state: State) =
    match msg with
    | UpdatePatient p -> { state with Patient = p }, Cmd.none
    | UrlChanged sl ->
        Logging.log "url changed" sl

        { state with
            Patient = sl |> parseUrlToPatient
        },
        Cmd.none
    | LoadBolusMedication Started ->
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
    | LoadContinuousMedication (Finished (Error s)) -> state, Cmd.none


let render state dispatch =
    let main =
        Main.render
            state.Patient
            (UpdatePatient >> dispatch)
            state.BolusMedication
            state.ContinuousMedication

    React.router [
        router.onUrlChanged (UrlChanged >> dispatch)
        router.children main
    ]


//-:cnd:noEmit
#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif

//+:cnd:noEmit
Program.mkProgram Main.init Main.update Main.render
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