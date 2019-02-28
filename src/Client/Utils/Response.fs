namespace Utils

module Response =
    open Elmish
    open GenPres

    let processResponse model (resp : Shared.Types.Response.Result) procf =
        match resp with
        | Ok(resp) ->
            match resp with
            | None -> model, Cmd.none
            | Some resp -> resp |> procf model
        | Error(exn) ->
            printfn "%s" exn.Message
            model, Cmd.none
