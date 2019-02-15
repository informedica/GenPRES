module Types

open Fable.MaterialUI.Core
open GenPres
open Fable.Helpers.Isomorphic

// The model holds data that you want to keep track of while the application is running
// in this case, we are keeping track of a counter
// we mark it as optional, because initially it will not be available from the client
// the initial value will be requested from server
type Model =
    { NavBarModel : Components.NavBar.Model
      SideMenuModel : Components.SideMenu.Model
      StatusBarModel : Components.StatusBar.Model
      FormModel : Components.Form.Model }

// The Msg type defines what events/actions can occur while the application is running
// the state of the application changes *only* in reaction to these events
type Msg =
    | SideMenuMsg of Components.SideMenu.Msg
    | NavBarMsg of Components.NavBar.Msg
    | StatusBarMsg of Components.StatusBar.Msg
    | FormMsg of Components.Form.Msg
    | SettingsLoaded of Result<Shared.Types.Response.Response Option, exn>

type IAppProps =
    abstract model : Model with get, set
    abstract dispatch : (Msg -> unit) with get, set
    inherit IClassesProps
