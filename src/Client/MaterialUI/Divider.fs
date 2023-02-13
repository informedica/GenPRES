module MaterialUI.Divider

open Fable.Core.JsInterop
open Feliz

let materialDivider: obj = importDefault "@mui/material/Divider"

type MaterialDivider =

     static member inline create (props: IReactProperty list) =
        Interop.reactApi.createElement(materialDivider, createObj !!props)