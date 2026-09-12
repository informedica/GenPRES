// The computing wrapper (plan 654, step 5): what `compose` does around `processCommand` today,
// as one function every computing member goes through. `bound` logs the command, marks the
// Session the cookie names seen and takes what it is told (the record moved on, the Session
// ended), applies the gate (the formulary loaded, or not needed), runs the handler and answers
// the reply with the notice; an exception is an `Error` with its message. `logged` wraps the
// members that are not computing (launch, session, signing, admin) with the same two log
// lines. The gate moves out of `Command.processCmd`, which becomes a plain dispatcher, into a
// `Command.gate` that `compose` passes: the drug names open, everything else behind the
// formulary. No visible change.
//
// Script-first draft (script-only policy) of:
//   - `Gate`, `Compute.bound`, `Compute.logged` → new `ServerApi.Compute.fs`, compiled after
//     `Adapters.fs`, before `AdminCommand.fs` (fsproj and both `Scripts/load.fsx`);
//   - `Command.gate` and `processCmd` without its own gating → `ServerApi.Command.fs`;
//   - `compose` building `processCommand` with `bound` and the four others with `logged`
//     → `CompositionRoot.fs`;
//   - `Request<'cmd>` / `Reply<'resp>` with abbreviations → `Shared/Api.fs` (drafted and
//     round-tripped through Fable.Remoting.Json in `Shared/Scripts/Api.fsx`).
//
// The shared envelope is not generic while this script runs, so `bound` is drafted over the
// abbreviated shape with an unbox at each end; the migration makes it generic and the body
// does not change. Run: `dotnet fsi Compute.fsx` from this directory (build first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open Shared.Types
open Shared.Api
open ServerApi
open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime


// ---------------------------------------------------------------------------------------------
// The wrapper (→ ServerApi.Compute.fs)
// ---------------------------------------------------------------------------------------------

/// Whether a command needs the formulary loaded.
[<RequireQualifiedAccess>]
type Gate =
    | RequiresLoaded
    | Open


module Compute =

    /// Every computing member: the Session the cookie names is marked seen and told whether the
    /// record moved on or the Session ended, before the command is computed; without a cookie
    /// the request computes as it always did. The gate refuses a command that needs the
    /// formulary while it is not loaded, with the provider's messages. An exception is an
    /// Error with its message. The token is never logged.
    let bound
        (env: AppEnv)
        (cookie: SessionCookie)
        (name: 'cmd -> string)
        (gate: 'cmd -> Gate)
        (handler: 'cmd -> Async<Result<'resp, string[]>>)
        (request: Request)
        : Async<Result<Reply, string[]>>
        =
        // the migration makes the envelope generic: `request: Request<'cmd>`, `Reply<'resp>`
        let cmd: 'cmd = unbox request.Command

        async {
            try
                writeInfoMessage $"Processing command: {name cmd}"

                let! notice =
                    match cookie.read () with
                    | None -> async { return None }
                    | Some id -> env.session.seen id request.Opened

                let! result =
                    match gate cmd, env.requireLoaded () with
                    | Gate.RequiresLoaded, Some msgs -> async { return Error msgs }
                    | _ -> handler cmd

                let told =
                    match notice with
                    | Some(RecordNotice.NewerVersion _) -> ", the record moved on"
                    | Some(RecordNotice.Ended _) -> ", the Session ended"
                    | None -> ""

                writeInfoMessage $"Finished processing command: {name cmd}{told}"

                return
                    result
                    |> Result.map (fun response ->
                        {
                            Response = unbox<Response> response
                            Notice = notice
                        }
                    )
            with ex ->
                writeErrorMessage $"Error processing command: {name cmd}\n{ex}"
                return Error [| ex.Message |]
        }


    /// A member that is not computing: the same two log lines around it, nothing else.
    let logged (what: string) (name: 'cmd -> string) (run: 'cmd -> Async<'resp>) (cmd: 'cmd) =
        async {
            writeInfoMessage $"Processing {what}: {name cmd}"
            let! response = run cmd
            writeInfoMessage $"Finished processing {what}: {name cmd}"
            return response
        }


// ---------------------------------------------------------------------------------------------
// The gate (→ ServerApi.Command.fs; processCmd loses its `gated` and becomes a plain dispatcher)
// ---------------------------------------------------------------------------------------------

module Command =

    /// The drug names come from the interaction source, not the formulary; everything else
    /// needs the formulary loaded.
    let gate =
        function
        | InteractionCmd GetDrugNames -> Gate.Open
        | _ -> Gate.RequiresLoaded


// and in `compose` (→ CompositionRoot.fs):
//
//     processCommand = Compute.bound env cookie Command.toString Command.gate (Command.processCmd env)
//     processLaunch = Compute.logged "launch" LaunchCommand.toString (LaunchCommand.processCmd env cookie stateCookie)
//     processSession = Compute.logged "session" SessionCommand.toString (SessionCommand.processCmd env cookie enrolment)
//     processSigning = Compute.logged "signing" SigningCommand.toString (SigningCommand.processCmd env cookie)
//     processAdmin = Compute.logged "admin" AdminCommand.toString (AdminCommand.processCmd env)


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip
open Informedica.GenForm.Lib


let cookieOf (id: string option) : SessionCookie =
    {
        read = fun () -> id
        write = ignore
        delete = ignore
    }


/// An env over a provider whose load failed, the formulary stubbed, the Session's answer given.
let envWith (loaded: bool) (told: RecordNotice option) =
    let env =
        Adapters.makeAppEnv (
            Resources.CachedResourceProvider(
                (fun () -> Error [ Informedica.GenForm.Lib.Types.ErrorMsg("load failed", None) ]),
                None
            )
        )

    { env with
        requireLoaded = if loaded then (fun () -> None) else env.requireLoaded
        formulary =
            {
                getFormulary = fun f -> async { return Ok { f with Markdown = "stubbed" } }
                getParenteralia = fun _ -> async { return Ok Shared.Models.Parenteralia.empty }
            }
        session =
            { env.session with
                seen = fun _ _ -> async { return told }
            }
    }


let request cmd : Request = { Opened = None; Command = cmd }


/// A handler that is not a dispatcher: the formulary only.
let formularyOnly (env: AppEnv) cmd =
    match cmd with
    | FormularyCmd f ->
        async {
            let! result = env.formulary.getFormulary f
            return result |> Result.map FormularyResp
        }
    | other -> failwithf "not under test: %A" other


let run env cookie gate handler cmd =
    Compute.bound env cookie Command.toString gate handler (request cmd)
    |> Async.RunSynchronously


let formulary = FormularyCmd Shared.Models.Formulary.empty


let tests =
    testList
        "Compute.bound"
        [
            test "without a cookie: computed, nothing told" {
                let env = envWith true (Some(RecordNotice.Ended SessionEnding.SupersededByLaunch))

                match run env (cookieOf None) Command.gate (formularyOnly env) formulary with
                | Ok reply ->
                    reply.Notice |> Expect.isNone "nothing told without a cookie"

                    match reply.Response with
                    | FormularyResp f -> f.Markdown |> Expect.equal "computed" "stubbed"
                    | other -> failtest $"expected FormularyResp, got {other}"
                | Error errs -> failtest $"expected Ok, got {errs}"
            }

            test "with a cookie: what the Session is told rides on the reply, still computed" {
                let env = envWith true (Some(RecordNotice.Ended SessionEnding.SupersededByLaunch))

                match run env (cookieOf (Some "s-1")) Command.gate (formularyOnly env) formulary with
                | Ok reply ->
                    reply.Notice
                    |> Expect.equal "the ending" (Some(RecordNotice.Ended SessionEnding.SupersededByLaunch))

                    match reply.Response with
                    | FormularyResp f -> f.Markdown |> Expect.equal "still computed" "stubbed"
                    | other -> failtest $"expected FormularyResp, got {other}"
                | Error errs -> failtest $"expected Ok, got {errs}"
            }

            test "a command that needs the formulary is refused while it is not loaded" {
                let env = envWith false None

                match run env (cookieOf None) Command.gate (formularyOnly env) formulary with
                | Error msgs -> msgs |> Array.exists (fun m -> m.Contains "load failed") |> Expect.isTrue "the messages"
                | Ok _ -> failtest "expected Error"
            }

            test "an open command runs while the formulary is not loaded" {
                let env = envWith false None
                let names _ = async { return Ok(InteractionResp(DrugNamesLoaded [| "paracetamol" |])) }

                match run env (cookieOf None) Command.gate names (InteractionCmd GetDrugNames) with
                | Ok reply ->
                    reply.Response
                    |> Expect.equal "the names" (InteractionResp(DrugNamesLoaded [| "paracetamol" |]))
                | Error errs -> failtest $"expected Ok, got {errs}"

                Command.gate (InteractionCmd GetDrugNames) |> Expect.equal "open" Gate.Open
                Command.gate (InteractionCmd(CheckInteractions [])) |> Expect.equal "gated" Gate.RequiresLoaded
            }

            test "a throwing handler answers an Error with its message" {
                let env = envWith true None
                let throwing _ = async { return invalidOp "boom" }

                run env (cookieOf None) Command.gate throwing formulary
                |> Expect.equal "the message" (Error [| "boom" |])
            }

            test "logged: the answer passes through" {
                Compute.logged "admin" AdminCommand.toString (fun _ -> async { return 42 }) (AdminCommand.ValidatePassword "x")
                |> Async.RunSynchronously
                |> Expect.equal "through" 42
            }
        ]


runTestsWithCLIArgs [] [||] tests |> ignore
