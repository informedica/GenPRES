module MaterialUI.Drawer

open Fable.Core.JsInterop
open Feliz
open Browser.Types

let  materialDrawer: obj = importDefault "@mui/material/Drawer"

type MaterialDrawer =
    static member inline variant (s : string) = Interop.mkAttr "variant" s
    static member inline ``open`` (b: bool) = Interop.mkAttr "open"  b
    static member inline onClose (handler: Event -> unit) = Interop.mkAttr "onClose" handler
    static member inline sx (s: (string * obj) list) = Interop.mkAttr "sx" (createObj s)
    static member inline create (props:  IReactProperty list) =
       Interop.reactApi.createElement(materialDrawer, createObj !!props)