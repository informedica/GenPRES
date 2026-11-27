/// Renders the app into the page. This is a separate file, so a hot reload of App re-renders the view
/// and never creates the root a second time.
module Main

open Browser
open Fable.React
open Utils


let root = ReactDomClient.createRoot (document.getElementById "genpres-app")
root.render (App.View() |> toReact)
