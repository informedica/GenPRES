namespace ServerApi

open Shared.Types
open Shared.Api
open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime


/// Whether a command needs the formulary loaded.
[<RequireQualifiedAccess>]
type Gate =
    | RequiresLoaded
    | Open


/// What every computing member goes through, and what the other members share of it.
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
        (request: Request<'cmd>)
        : Async<Result<Reply<'resp>, string[]>>
        =
        let cmd = request.Command

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
                            Response = response
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
