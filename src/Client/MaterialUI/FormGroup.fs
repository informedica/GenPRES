module MaterialUI.FormGroup

open Fable.Core.JsInterop
open Feliz

let  materialFormGroup: obj = importDefault "@mui/material/FormGroup"

type MaterialFormGroup =

    static member inline row(b: bool) = Interop.mkAttr "row" b
    static member inline sx (s: (string * obj) list) = Interop.mkAttr "sx", (createObj s)
    static member inline create (props:  IReactProperty list) =
       Interop.reactApi.createElement(materialFormGroup, createObj !!props)