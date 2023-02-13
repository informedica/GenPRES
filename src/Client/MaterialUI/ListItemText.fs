module MaterialUI.ListItemText

open Fable.Core.JsInterop
open Feliz

let  materialListItemText: obj = importDefault "@mui/material/ListItemText"

type MaterialListItemText =

    static member inline primary(s:string)  = Interop.mkAttr "primary" s
    static member inline secondary(s:string)  = Interop.mkAttr "secondary" s
    static member inline secondary(e:ReactElement)  = Interop.mkAttr "secondary" e

    static member inline create (props:  IReactProperty list) =
       Interop.reactApi.createElement(materialListItemText, createObj !!props)
