module MaterialUI.ListItem

open Fable.Core.JsInterop
open Feliz

let  materialListItem: obj = importDefault "@mui/material/ListItem"

type MaterialListItem =

    static member inline divider(b: bool) = Interop.mkAttr "divider"
    static member inline button(b:bool) = Interop.mkAttr "button"
    static member inline disableGutters (b: bool) = Interop.mkAttr "disableGutters" b
    static member inline selected (b: bool) = Interop.mkAttr "selected" b
    static member inline button (b: bool) = Interop.mkAttr "button" b
    static member inline sx (s: (string * obj) list) = Interop.mkAttr "sx", createObj s
    static member inline create (props:  IReactProperty list) =
       Interop.reactApi.createElement(materialListItem, createObj !!props)
