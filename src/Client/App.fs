module Client

open Elmish
open Elmish.React
open Views
open GenPres
#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif


type Model = MainModel of Main.Model

type Msg = MainMsg of Main.Msg

let init() =
    let model, cmd = Main.init()
    model |> MainModel, cmd |> Cmd.map MainMsg

let update msg model =
    match msg with
    | MainMsg msg ->
        match model with
        | MainModel model ->
            let model, cmd = Main.update msg model
            model |> MainModel, cmd |> Cmd.map MainMsg

let view model dispatch =
    match model with
    | MainModel m -> Main.view m (MainMsg >> dispatch)

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
