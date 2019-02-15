module Types

open Fable.MaterialUI.Core
open GenPres
open Fable.Helpers.Isomorphic


type Model =
    { Configuration : Shared.Types.Configuration.Configuration Option
      Patient : Shared.Types.Patient.Patient Option
      NavBarModel : Components.NavBar.Model
      SideMenuModel : Components.SideMenu.Model
      StatusBarModel : Components.StatusBar.Model
      FormModel : Components.Form.Model }

type ResponseLoaded = Result<Shared.Types.Response.Response Option, exn>

type Msg =
    | SideMenuMsg of Components.SideMenu.Msg
    | NavBarMsg of Components.NavBar.Msg
    | StatusBarMsg of Components.StatusBar.Msg
    | FormMsg of Components.Form.Msg
    | ResponseMsg of ResponseLoaded

type IAppProps =
    abstract model : Model with get, set
    abstract dispatch : (Msg -> unit) with get, set
    inherit IClassesProps
