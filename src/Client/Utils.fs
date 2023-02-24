[<AutoOpen>]
module Utils

open Fable.Core
open Feliz
open Browser.Types

let inline toJsx (el: ReactElement) : JSX.Element = unbox el
let inline toReact (el: JSX.Element) : ReactElement = unbox el

/// Enables use of Feliz styles within a JSX hole
let inline toStyle (styles: IStyleAttribute list) : obj = JsInterop.createObj (unbox styles)


let toClass (classes: (string * bool) list) : string =
    classes
    |> List.choose (fun (c, b) ->
        match c.Trim(), b with
        | "", _
        | _, false -> None
        | c, true -> Some c)
    |> String.concat " "


let onEnterOrEscape dispatchOnEnter dispatchOnEscape (ev: KeyboardEvent) =
    let el = ev.target :?> HTMLInputElement

    match ev.key with
    | "Enter" ->
        dispatchOnEnter el.value
        el.value <- ""
    | "Escape" ->
        dispatchOnEscape ()
        el.value <- ""
        el.blur ()
    | _ -> ()