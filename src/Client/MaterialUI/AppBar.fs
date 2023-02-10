module MaterialUI.AppBar

open Fable.Core.JsInterop
open Feliz

let appbar: obj = importDefault "@mui/material/AppBar"

type MaterialAppBar =
    static member inline position (s : string) = Interop.mkAttr "position" s

    static member inline create (props: IReactProperty list) =
        Interop.reactApi.createElement(appbar, createObj !!props)