module MaterialUI.Menu

open Fable.Core.JsInterop
open Feliz
open Browser.Types

let materialMenu: obj = importDefault "@mui/material/Menu"

type MaterialMenu =

     static member inline keepMounted (b: bool) = Interop.mkAttr "keepMounted"  b
     static member inline ``open`` (b: bool) = Interop.mkAttr "open"  b

     static member inline variant (s : string) = Interop.mkAttr "variant" s
     static member inline anchorEl (value: #Element option) = Interop.mkAttr "anchorEl" value
     static member inline create (props: IReactProperty list) =
        Interop.reactApi.createElement(materialMenu, createObj !!props)