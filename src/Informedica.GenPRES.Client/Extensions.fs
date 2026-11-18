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
    /// Logs an error to the console in a debug build.
    let developmentError (error: exn) =
        if isDevelopment then
            Browser.Dom.console.error error

module Cmd =
    /// Converts an asynchronous operation that returns a message into a command of that message.
    /// An exception is logged to the console in a debug build, and no message is dispatched.
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
