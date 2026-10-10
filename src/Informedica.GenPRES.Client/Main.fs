/// The entry point: the app rendered into the page. A file of its own, so that a hot swap of App
/// re-renders the view and never creates the root a second time.
module Main

open Browser
open Fable.React
open Utils


let root = ReactDomClient.createRoot (document.getElementById "genpres-app")
root.render (App.View() |> toReact)
