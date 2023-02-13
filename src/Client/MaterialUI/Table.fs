module MaterialUI.Table

open Fable.Core.JsInterop
open Feliz

let materialTable: obj = importDefault "@mui/material/Table"

type MaterialTable =
     static member inline variant (s : string) = Interop.mkAttr "variant" s
     static member inline create (props: IReactProperty list) =
        Interop.reactApi.createElement(materialTable, createObj !!props)