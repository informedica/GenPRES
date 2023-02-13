module MaterialUI.Container

open Fable.Core.JsInterop
open Feliz
let  materialContainer: obj = importDefault "@mui/material/Container"

type MaterialContainer =
    static member inline maxWidth (O : obj) = Interop.mkAttr "maxWidth" O
    static member inline ``fixed`` (b : bool) = Interop.mkAttr "fixed" b
    static member inline disableGutters (b: bool) = Interop.mkAttr "disableGutters" b
    static member inline create (props:  IReactProperty list) =
       Interop.reactApi.createElement(materialContainer, createObj !!props)