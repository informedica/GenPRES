module Client

open Elmish
open Elmish.React

open Views

#if DEBUG

open Elmish.Debug
open Elmish.HMR
#endif

let init = Main.init
let update = Main.update
let view = Main.view

Program.mkProgram init update view
#if DEBUG
|> Program.withConsoleTrace
|> Program.withHMR
#endif

|> Program.withReact "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif

|> Program.run
