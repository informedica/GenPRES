module MaterialUI.TableContainer

open Fable.Core.JsInterop
open Feliz

let materialTableContainer: obj = importDefault "@mui/material/TableContainer"

type MaterialTableContainer =
     static member inline create (props: IReactProperty list) =
        Interop.reactApi.createElement(materialTableContainer, createObj !!props)