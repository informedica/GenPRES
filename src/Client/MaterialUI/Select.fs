module MaterialUI.Select

open Fable.Core.JsInterop
open Feliz

let  materialSelect: obj = importDefault "@mui/material/Select"


type MaterialSelect =

    static member inline create (props:  IReactProperty list) =
       Interop.reactApi.createElement(materialSelect, createObj !!props)

    static member inline labelId(s : string) = Interop.mkAttr "labelId" s
    static member inline label(s : string) = Interop.mkAttr "label" s
