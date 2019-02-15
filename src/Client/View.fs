
module View

open Elmish.React
open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.Helpers.Isomorphic

open Fable.MaterialUI.Props
open Fable.MaterialUI.Core
open Fable.MaterialUI.Themes

open Types

let safeComponents =
    let components =
        span []
            [ a [ Href "https://saturnframework.github.io" ] [ str "Saturn" ]
              str ", "
              a [ Href "http://fable.io" ] [ str "Fable" ]
              str ", "
              a [ Href "https://elmish.github.io/elmish/" ] [ str "Elmish" ] ]
    p [] [ strong [] [ str "SAFE Template" ]
           str " powered by: "
           components ]

let bottomBar = div [ Id "bottombar" ] [ safeComponents ]
let body = form [] [ str "input" ]
let menu = []

let view model dispatch =
    muiThemeProvider
        [ MuiThemeProviderProp.Theme(ProviderTheme.Theme Styles.theme) ]
        [ div [ Id "homepage" ]
                [ yield Components.SideMenu.view model.SideMenuModel
                            (SideMenuMsg >> dispatch)

                  yield Components.NavBar.view model.NavBarModel
                            (NavBarMsg >> dispatch)

                  yield Components.Form.view model.FormModel (FormMsg >> dispatch)

                  yield Components.StatusBar.view model.StatusBarModel
                            (StatusBarMsg >> dispatch) ] ]
