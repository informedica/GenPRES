module MaterialUI.FormControl

open Fable.Core.JsInterop
open Feliz

let  materialFormControl: obj = importDefault "@mui/material/FormControl"

type MaterialFormControl =

    static member inline create (props:  IReactProperty list) =
        Interop.reactApi.createElement(materialFormControl, createObj !!props)

    static member inline variant(s : string) = Interop.mkAttr "variant" s

    static member inline fullWidth(b: bool) = Interop.mkAttr "fullWidth" b
    static member inline margin (s : string) = Interop.mkAttr "margin" s

    static member inline size (s : string) = Interop.mkAttr "size" s
     static member inline sx (s: (string * obj) list) = Interop.mkAttr "sx" (createObj s)
