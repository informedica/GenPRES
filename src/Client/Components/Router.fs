namespace Components

open System
open Fable.Core
open Fable.React
open Feliz
open Feliz.Router
open Browser.Types


open Elmish
open Fable.Core.JsInterop


module Router =
    [<ReactComponent>]

    let View(props: {| onUrlChanged: string list -> unit |}) = 
        React.router [
            router.onUrlChanged props.onUrlChanged
        ]
