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
    let context (language: Shared.Localization.Locales) el =
        React.contextProvider (
            Global.languageContext,
            language,
            React.fragment [ el ]
        )