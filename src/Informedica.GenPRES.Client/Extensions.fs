[<AutoOpen>]
module Extensions

open Elmish

let isDevelopment =
#if DEBUG
    true
#else
    false
#endif

module Log =
    /// Logs error to the console during development
    let developmentError (error: exn) =
        if isDevelopment then
            Browser.Dom.console.error error

module Cmd =
    /// Converts an asynchronous operation that returns a message into into a command of that message.
    /// Logs unexpected errors to the console while in development mode.
    let fromAsync (operation: Async<'msg>) : Cmd<'msg> =
        let delayedCmd (dispatch: 'msg -> unit) : unit =
            let delayedDispatch =
                async {
                    match! Async.Catch operation with
                    | Choice1Of2 msg -> dispatch msg
                    | Choice2Of2 error -> Log.developmentError error
                }

            Async.StartImmediate delayedDispatch

        Cmd.ofEffect delayedCmd
