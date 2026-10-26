/// The development trace of the app: the console trace and the Redux DevTools debugger, both silent
/// until the server confirms the demo data. A debug build only; a release build compiles this file
/// as an empty module.
[<RequireQualifiedAccess>]
module Tracing

open Informedica.GenPRES.Client.Core.Helpers
open Informedica.GenPRES.Client.Core.StateMachines

module Client = Informedica.GenPRES.Client.Core.Client

#if DEBUG
open Elmish
open Elmish.Debug
open Thoth.Json


type private State = Client.ClientState


type private Msg = Client.ClientMsg


/// A state may be traced only once the server has said it serves the demo data: never before the
/// settings arrive, and never against production. The trail keeps the same gate through
/// StepTrail.confirmDemo.
let isTraceable (state: State) =
    match Client.settings state with
    | Resolved settings
    | Refreshing settings -> settings.IsDemo
    | HasNotStartedYet
    | InProgress -> false


/// What the trace shows in place of the admin password.
let redacted = "***"


/// The message as the trace records it, with the admin password redacted: a development password may be the one
/// a production server takes. The admin token stays, because a demo server signs it under its own mode, so it
/// never opens a production server.
let redactMsg (msg: Msg) =
    match msg with
    | Msg.Admin(AdminMachine.AdminMsg.Login _) -> Msg.Admin(AdminMachine.AdminMsg.Login redacted)
    | _ -> msg


/// The console trace, silent while the state is not traceable.
let consoleTrace (msg: Msg) (state: State) _ =
    if isTraceable state then
        Browser.Dom.console.log ("New message:", redactMsg msg)
        Browser.Dom.console.log ("Updated state:", state)


/// A connection to the Redux DevTools extension that passes nothing on while the state is not traceable: the
/// deflater hands it None for such a state. The history starts with the first traceable state, and every
/// message it passes on is redacted.
let gatedConnection (inner: Fable.Import.RemoteDev.Connection) =
    let mutable started = false

    { new Fable.Import.RemoteDev.Connection with
        member _.init(_, _) = ()
        member _.subscribe listener = inner.subscribe listener
        member _.unsubscribe = inner.unsubscribe
        member _.error e = inner.error e

        member _.send(msg, state) =
            match unbox<obj option> state with
            | None -> ()
            | Some json when not started ->
                started <- true
                inner.init (json, None)
            | Some json -> inner.send (unbox<Msg> msg |> redactMsg |> box, json)
    }


/// The Redux DevTools debugger over the gated connection, with the coders the debugger itself uses.
let withGatedDebugger (program: Program<unit, State, Msg, unit>) =
    let coders = Extra.empty |> Extra.withDecimal |> Extra.withInt64 |> Extra.withUInt64

    let encoder = Encode.Auto.generateEncoder<State>(extra = coders)
    let decoder = Decode.Auto.generateDecoder<State>(extra = coders)

    let deflate (state: State) =
        if isTraceable state then Some(encoder state) else None
        |> box

    let inflate (json: obj) =
        match Decode.fromValue "$" decoder json with
        | Ok state -> state
        | Error err -> invalidOp err

    try
        let connection =
            Debugger.connectViaExtension<Msg>(Fable.Import.RemoteDev.ExtensionOptions())
            |> gatedConnection
        program |> Program.withDebuggerUsing deflate inflate connection
    with ex ->
        Browser.Dom.console.error ("[ELMISH DEBUGGER] continuing without the debugger", ex.Message)
        program
#endif
