module MaterialUI.List

open Fable.Core.JsInterop
open Feliz

let materialList: obj = importDefault "@mui/material/List"

type MaterialList =
     static member inline create (props: IReactProperty list) =
        Interop.reactApi.createElement(materialList, createObj !!props)