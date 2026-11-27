namespace ServerApi

open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Api


/// Whether a command needs the formulary loaded.
[<RequireQualifiedAccess>]
type Gate =
    | RequiresLoaded
    | Open


/// Wraps the server api functions with logging, the session check and the loaded-resources check.
module Compute =

    /// Runs a command that computes. Marks the session of the cookie as seen and returns its notice:
    /// a newer version of the record or the end of the session. Refuses a command that needs the
    /// formulary while it is not loaded, with the messages of the provider. An exception becomes an
    /// Error with its message. The token is never logged.
    let bound
        (env: AppEnv)
        (cookie: SessionCookie)
        (name: 'cmd -> string)
        (gate: 'cmd -> Gate)
        (handler: 'cmd -> Async<Result<'resp, string[]>>)
        (request: Request<'cmd>)
        : Async<Result<Reply<'resp>, string[]>>
        =
        let cmd = request.Command

        async {
            try
                Logging.ServerLogging.Info $"Processing command: {name cmd}"
                |> Informedica.Logging.Lib.Logging.logInfo env.logger

                let! notice =
                    match cookie.read () with
                    | None -> async { return None }
                    | Some id -> env.session.seen id request.Opened

                // An open command does not call requireLoaded, because asking the provider may start a load.
                let! result =
                    match gate cmd with
                    | Gate.Open -> handler cmd
                    | Gate.RequiresLoaded ->
                        match env.requireLoaded () with
                        | Some msgs -> async { return Error msgs }
                        | None -> handler cmd

                let told =
                    match notice with
                    | Some(RecordNotice.NewerVersion _) -> ", the record moved on"
                    | Some(RecordNotice.Ended SessionEnding.InactiveTab) -> ", the tab is not the active one"
                    | Some(RecordNotice.Ended _) -> ", the Session ended"
                    | None -> ""

                Logging.ServerLogging.Info $"Finished processing command: {name cmd}{told}"
                |> Informedica.Logging.Lib.Logging.logInfo env.logger

                return
                    result
                    |> Result.map (fun response ->
                        {
                            Response = response
                            Notice = notice
                        }
                    )
            with ex ->
                Logging.ServerLogging.Error $"Error processing command: {name cmd}\n{ex}"
                |> Informedica.Logging.Lib.Logging.logError env.logger

                return Error [| ex.Message |]
        }


    /// Runs a command that does not compute, with only a log line before and after it.
    let logged (env: AppEnv) (what: string) (name: 'cmd -> string) (run: 'cmd -> Async<'resp>) (cmd: 'cmd) =
        async {
            Logging.ServerLogging.Info $"Processing {what}: {name cmd}"
            |> Informedica.Logging.Lib.Logging.logInfo env.logger

            let! response = run cmd

            Logging.ServerLogging.Info $"Finished processing {what}: {name cmd}"
            |> Informedica.Logging.Lib.Logging.logInfo env.logger

            return response
        }
