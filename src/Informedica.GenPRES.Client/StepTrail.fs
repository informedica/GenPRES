/// The trail of the machine steps: one line per step, logged to the console and kept in memory.
/// Debug build only, with GENPRES_LOG on, GENPRES_PROD not 1, and only after the server settings
/// confirm the demo data. A release build compiles every name as a no-op. The lines are described
/// by the Trail module in Client.Core.
[<RequireQualifiedAccess>]
module StepTrail

open System

#if DEBUG
open Fable.Core


/// GENPRES_LOG as Vite read it at start-up.
[<Emit("__GENPRES_LOG__")>]
let genpresLog: string = jsNative


/// GENPRES_PROD as Vite read it at start-up.
[<Emit("__GENPRES_PROD__")>]
let genpresProd: string = jsNative


/// Logging is on for the levels d, i, w and e.
let isLogging (log: string) =
    match log.Trim().ToLowerInvariant() with
    | "d"
    | "i"
    | "w"
    | "e" -> true
    | _ -> false


/// Production is GENPRES_PROD=1.
let isProduction (prod: string) = prod.Trim() = "1"


/// Logging on and not production.
let isTraceOn () = isLogging genpresLog && not (isProduction genpresProd)


/// How many lines the trail keeps.
let size = 500

let mutable private lines: string list = []

let mutable private count = 0

let mutable private demo = false


/// Whether the server settings said demo data; nothing is recorded before they do.
let confirmDemo (isDemo: bool) = demo <- isDemo


/// Records and logs the step as the next line.
let record (describe: int -> DateTime -> Trail.Step) =
    if isTraceOn () && demo then
        count <- count + 1
        let line = describe count DateTime.Now |> Trail.format
        Browser.Dom.console.log line
        lines <- lines |> Trail.append size line


/// Records a step described by the caller: machine, message, effects, state. The caller keeps the
/// text free of a key, an identity, a PIN or a token.
let event (machine: string) (msg: string) (effects: string list) (state: string) =
    record (fun no at ->
        {
            Trail.Step.No = no
            At = at
            Machine = machine
            Msg = msg
            Effects = effects
            State = state
        }
    )


/// The lines kept, oldest first.
let text () = lines |> String.concat "\n"

#else

let isTraceOn () = false


let confirmDemo (_: bool) = ()


let record (_: int -> DateTime -> Trail.Step) = ()


let event (_: string) (_: string) (_: string list) (_: string) = ()


let text () = ""

#endif
