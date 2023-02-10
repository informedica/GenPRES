module MaterialUI.Accordion

open Fable.Core.JsInterop
open Feliz
open Microsoft.FSharp.Core

let  materialAccordion: obj = importDefault "@mui/material/Accordion"
let  materialAccordionSummary: obj = importDefault "@mui/material/AccordionSummary"
let  materialAccordionDetails: obj = importDefault "@mui/material/AccordionDetails"

type MaterialAccordion =

    static member inline defaultExpanded(b: bool) = Interop.mkAttr "defaultExpanded" b
    static member inline create (props:  IReactProperty list) =
        Interop.reactApi.createElement(materialAccordion, createObj !!props)

type MaterialAccordionSummary =

    static member inline create (props:  IReactProperty list) =
        Interop.reactApi.createElement(materialAccordionSummary, createObj !!props)

    static member inline expandIcon(e :ReactElement) = Interop.mkAttr "expandIcon"  e

    static member inline focusVisibleClassName(s: string) = Interop.mkAttr "focusVisibleClassName"  s

type MaterialAccordionDetails =

    static member inline create (props:  IReactProperty list) =
        Interop.reactApi.createElement(materialAccordionDetails, createObj !!props)