namespace ServerApi

open Shared.Types
open Shared.Api


/// Whether a command needs the formulary loaded.
[<RequireQualifiedAccess>]
type Gate =
    | RequiresLoaded
    | Open


/// What every computing member goes through, and what the other members share of it.
module Compute =

    /// Every computing member: the Session the cookie names is marked seen and told whether the
    /// record moved on or the Session ended, and every patient the command carries is put at
    /// the age the Session holds, before the command is computed; without a cookie the request
    /// computes as it always did. The gate refuses a command that needs the formulary while it
    /// is not loaded, with the provider's messages. An exception is an Error with its message.
    /// The token is never logged.
    let bound
        (env: AppEnv)
        (cookie: SessionCookie)
        (name: 'cmd -> string)
        (gate: 'cmd -> Gate)
        (aged: Age option -> 'cmd -> 'cmd)
        (handler: 'cmd -> Async<Result<'resp, string[]>>)
        (request: Request<'cmd>)
        : Async<Result<Reply<'resp>, string[]>>
        =
        let cmd = request.Command

        async {
            try
                Logging.ServerLogging.Info $"Processing command: {name cmd}"
                |> Informedica.Logging.Lib.Logging.logInfo env.logger

                let! notice, age =
                    match cookie.read () with
                    | None -> async { return None, None }
                    | Some id -> env.session.seen id request.Opened

                // an identified Session's age, never the client's, on every patient the command
                // carries, before anything reads it
                let cmd = cmd |> aged age

                // an open command never asks the provider: asking may load
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


    /// A member that is not computing: the same two log lines around it, nothing else.
    let logged (env: AppEnv) (what: string) (name: 'cmd -> string) (run: 'cmd -> Async<'resp>) (cmd: 'cmd) =
        async {
            Logging.ServerLogging.Info $"Processing {what}: {name cmd}"
            |> Informedica.Logging.Lib.Logging.logInfo env.logger

            let! response = run cmd

            Logging.ServerLogging.Info $"Finished processing {what}: {name cmd}"
            |> Informedica.Logging.Lib.Logging.logInfo env.logger

            return response
        }
