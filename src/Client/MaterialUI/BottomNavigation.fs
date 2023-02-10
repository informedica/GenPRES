module MaterialUI.BottomNavigation

open Fable.Core.JsInterop
open Feliz

let materialBottomNavigation: obj = importDefault "@mui/material/BottomNavigation"
let materialBottomNavigationAction: obj = importDefault "@mui/material/BottomNavigationAction"

type MaterialBottomNavigation =
     static member inline showLabels (b : bool) = Interop.mkAttr "showLabels" b
     static member inline sx (s: (string * obj) list) = Interop.mkAttr "sx" (createObj s)
     static member inline create (props: IReactProperty list) =
        Interop.reactApi.createElement(materialBottomNavigation, createObj !!props)

type MaterialBottomNavigationAction =
     static member inline label (s : string) = Interop.mkAttr "label"  s
     static member inline icon (e : ReactElement) = Interop.mkAttr "icon"  e
     static member inline create (props: IReactProperty list) =
        Interop.reactApi.createElement(materialBottomNavigation, createObj !!props)