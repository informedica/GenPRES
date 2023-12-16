namespace Components

open System
open Fable.Core
open Fable.React
open Feliz
open Feliz.Router
open Browser.Types


open Elmish
open Fable.Core.JsInterop


module Context =


    [<ReactComponent>]
    let Context (context: Global.Context) el =
        React.contextProvider (
            Global.context,
            context,
            React.fragment [ el ]
        )