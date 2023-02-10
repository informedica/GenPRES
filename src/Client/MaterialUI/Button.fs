module MaterialUI.Button

open Fable.Core.JsInterop
open Feliz

let materialButton: obj = importDefault "@mui/material/Button"

type MaterialButton =
    static member inline color (s : string) = Interop.mkAttr "color" s
    static member inline variant (s : string) = Interop.mkAttr "variant"  s
    static member inline fullWidth(b: bool) = Interop.mkAttr "fullWidth" b
    static member inline create (props: IReactProperty list) =
        Interop.reactApi.createElement(materialButton, createObj !!props)
