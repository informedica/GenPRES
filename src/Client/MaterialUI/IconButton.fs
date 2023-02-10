module MaterialUI.IconButton

open Fable.Core.JsInterop
open Feliz

let materialIconButton: obj = importDefault "@mui/material/IconButton"

type MaterialIconButton =
     static member inline variant (s : string) = Interop.mkAttr "variant" s
     static member inline color (s : string) = Interop.mkAttr "color" s

     static member inline create (props: IReactProperty list) =
        Interop.reactApi.createElement(materialIconButton, createObj !!props)