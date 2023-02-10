module MaterialUI.MenuItem

open Fable.Core.JsInterop
open Feliz

let materialMenuItem: obj = importDefault "@mui/material/MenuItem"

type MaterialMenuItem =
     static member inline create (props: IReactProperty list) =
        Interop.reactApi.createElement(materialMenuItem, createObj !!props)