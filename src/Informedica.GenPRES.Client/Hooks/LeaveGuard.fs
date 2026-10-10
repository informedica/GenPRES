namespace Hooks


/// The browser's own question before it leaves the page (back, a closed tab, a reload) while there
/// is work that would be lost.
module LeaveGuard =


    open Browser
    open Fable.Core.JsInterop
    open Feliz


    /// Asks before the page is left while unsignedWork is true. The listener is added once and
    /// reads the latest value through a ref.
    let useLeaveGuard unsignedWork =
        let workRef = React.useRef unsignedWork
        workRef.current <- unsignedWork

        React.useEffectOnce (fun () ->
            let guard (ev: Browser.Types.Event) =
                if workRef.current then
                    ev.preventDefault ()
                    // the browser shows its own dialog; older browsers need a returnValue for it
                    ev?returnValue <- ""

            window.addEventListener ("beforeunload", guard)

            fun () -> window.removeEventListener ("beforeunload", guard)
        )
