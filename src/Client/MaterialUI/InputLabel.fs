module MaterialUI.InputLabel

open Fable.Core.JsInterop
open Feliz

let materialInputLabel: obj = importDefault "@mui/material/InputLabel"

type MaterialInputLabel =
     static member inline create (props: IReactProperty list) =
        Interop.reactApi.createElement(materialInputLabel, createObj !!props)