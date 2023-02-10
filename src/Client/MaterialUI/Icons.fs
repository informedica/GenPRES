module MaterialUI.Icons

open Fable.Core.JsInterop
open Feliz

let materialMenuIcon: obj = importDefault "@mui/icons-material/Menu"
let materialHelpIcon: obj = importDefault "@mui/icons-material/Help"
let materialCopyrightIcon: obj = importDefault "@mui/icons-material/Copyright"
let materialGppGoodIcon: obj = importDefault "@mui/icons-material/GppGood"
let materialExpandMoreIcon: obj = importDefault "@mui/icons-material/ExpandMore"

let materialLanguageIcon : obj = importDefault "@mui/icons-material/Language"


type MaterialMenuIcon =
     static member inline create (props: IReactProperty list) =
        Interop.reactApi.createElement(materialMenuIcon, createObj !!props)

type MaterialHelpIcon =
    static member inline create (props: IReactProperty list) =
            Interop.reactApi.createElement(materialHelpIcon, createObj !!props)

type MaterialCopyrightIcon =
    static member inline create (props: IReactProperty list) =
            Interop.reactApi.createElement(materialCopyrightIcon, createObj !!props)

type MaterialGppGoodIcon =
    static member inline create (props: IReactProperty list) =
            Interop.reactApi.createElement(materialGppGoodIcon, createObj !!props)

type MaterialExpandMoreIcon =
    static member inline create (props: IReactProperty list) =
            Interop.reactApi.createElement(materialExpandMoreIcon, createObj !!props)

type MaterialLanguageIcon =
    static member inline create (props: IReactProperty list) =
            Interop.reactApi.createElement(materialLanguageIcon, createObj !!props)