module MaterialUI.Toolbar

open Fable.Core.JsInterop
open Feliz

let materialToolbar: obj = importDefault "@mui/material/Toolbar"

type MaterialToolbar =
     static member inline variant (s : string) = Interop.mkAttr "variant" s
     static member inline create (props: IReactProperty list) =
        Interop.reactApi.createElement(materialToolbar, createObj !!props)