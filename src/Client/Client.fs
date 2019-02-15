module Client

open Elmish
open Elmish.React
open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.PowerPack.Fetch
open Fable.PowerPack.PromiseImpl
open Thoth.Json
open GenPres
open Types
open Fable.Helpers.Isomorphic
open Elmish.ReactNative
open System.Security.Claims

module MPR = Fable.MaterialUI.Props
module MUI = Fable.MaterialUI.Core
module MTH = Fable.MaterialUI.Themes

let theme =
    MUI.createMuiTheme
        [ MPR.ThemeProp.Palette
              [ MPR.PaletteProp.Primary
                    [ MPR.PaletteIntentionProp.Main
                          Fable.MaterialUI.Colors.blue.``500`` ] ]

          MPR.ThemeProp.Typography
              [ MPR.ThemeTypographyProp.UseNextVariants true ] ]


let getSettings () =
    promise {
        let req =
            Shared.Types.Request.Configuration.Get
            |> Shared.Types.Request.ConfigMsg
//            |> encode
        let! res =
            postRecord "/api/request" req []
        let! settings =
            res.text ()
            
        return settings |> Decode.Auto.unsafeFromString<Shared.Types.Response.Response Option>
    }

// defines the initial state and initial command (= side-effect) of the application
let init() : Model * Cmd<Msg> =
    let initialModel =
        { NavBarModel = Components.NavBar.init()
          SideMenuModel = Components.SideMenu.init()
          StatusBarModel = Components.StatusBar.init()
          FormModel = Components.Form.init() }

    let loadCountCmd =
        Cmd.ofPromise getSettings () (Ok >> SettingsLoaded)
            (Error >> SettingsLoaded)
    initialModel, loadCountCmd

// The update function computes the next state of the application based on the current state and the incoming events/messages
// It can also run side-effects (encoded as commands) like calling the server via Http.
// these commands in turn, can dispatch messages to which the update function will react.
let update (msg : Msg) (currentModel : Model) : Model * Cmd<Msg> =
    match currentModel.StatusBarModel, msg with
    | _, SettingsLoaded(Ok settings) ->
        printfn "settings received: %A" settings
        currentModel, Cmd.none
    | _, SettingsLoaded(Error err) ->
        printfn "Error: %s" err.Message
        currentModel, Cmd.none
    | _, NavBarMsg _ ->
        printfn "navbar message"
        let nextModel =
            { currentModel with SideMenuModel =
                                    currentModel.SideMenuModel
                                    |> Components.SideMenu.update
                                           Components.SideMenu.ToggleMenu }
        nextModel, Cmd.none
    | _, SideMenuMsg msg ->
        printfn "sidemenu message"
        let nextModel =
            { currentModel with SideMenuModel =
                                    currentModel.SideMenuModel
                                    |> Components.SideMenu.update msg }
        nextModel, Cmd.none
    | _, FormMsg msg ->
        printfn "sidemenu message"
        let nextModel =
            { currentModel with FormModel =
                                    currentModel.FormModel
                                    |> Components.Form.update msg }
        nextModel, Cmd.none
    | _  -> currentModel, Cmd.none

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

let view (model : Model) (dispatch : Msg -> unit) =
    MUI.muiThemeProvider
        [ MPR.MuiThemeProviderProp.Theme(MPR.ProviderTheme.Theme theme) ]
        [ div [ Id "homepage" ]
              [ yield Components.SideMenu.view model.SideMenuModel
                          (SideMenuMsg >> dispatch)

                yield Components.NavBar.view model.NavBarModel
                          (NavBarMsg >> dispatch)
                yield Components.Form.view model.FormModel (FormMsg >> dispatch)

                yield Components.StatusBar.view model.StatusBarModel
                          (StatusBarMsg >> dispatch) ] ]
#if DEBUG

open Elmish.Debug
open Elmish.HMR
#endif


Program.mkProgram init update view
#if DEBUG
|> Program.withConsoleTrace
|> Program.withHMR
#endif

|> Program.withReact "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif

|> Program.run
