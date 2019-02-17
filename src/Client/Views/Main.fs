namespace Views

module Main =
    open Elmish
    open Elmish.React
    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Fable.Helpers.Isomorphic

    open Fable.MaterialUI.Props
    open Fable.MaterialUI.Core
    open Fable.MaterialUI.Themes

    open GenPres

    type Model =
        { Configuration : Shared.Types.Configuration.Configuration Option
          Patient : Shared.Types.Patient.Patient Option
          NavBarModel : Components.NavBar.Model
          SideMenuModel : Components.SideMenu.Model
          StatusBarModel : Components.StatusBar.Model
          PatientFormModel : Views.PatientForm.Model Option }

    type ResponseLoaded = Result<Shared.Types.Response.Response Option, exn>

    type Msg =
        | SideMenuMsg of Components.SideMenu.Msg
        | NavBarMsg of Components.NavBar.Msg
        | StatusBarMsg of Components.StatusBar.Msg
        | PatientFormMsg of Views.PatientForm.Msg
        | ResponseMsg of ResponseLoaded


    let cmdNone model = model, Cmd.none

    let setStatus msg model =
        { model with StatusBarModel =
                        { Components.StatusBar.Message = msg; Open = true } }    

    let processResponse model resp =
        match resp with
        | None -> model, Cmd.none
        | Some resp ->
            match resp with
            | Shared.Types.Response.Configuration config ->
                let yrs, mos, wths, hths =
                    config
                    |> Domain.Configuration.calculateSelects "pediatrie"
                let patFormMod,cmd =
                    Views.PatientForm.init yrs mos wths hths
                { model with Configuration = Some config
                             PatientFormModel = Some patFormMod }
                |> setStatus "configuratie ontvangen"
                |> (fun m -> m, cmd |> Cmd.map Msg.PatientFormMsg)
            | _ -> model, Cmd.none
        

    let getConfiguration () =
        Shared.Types.Request.Configuration.Get
        |> Shared.Types.Request.ConfigMsg
        |> Utils.Request.post

    let init() : Model * Cmd<Msg> =
        let initialModel =
            { Configuration = None
              Patient = None
              NavBarModel = Components.NavBar.init()
              SideMenuModel = Components.SideMenu.init()
              StatusBarModel = Components.StatusBar.init()
              PatientFormModel = None }

        let loadConfig =
            Cmd.ofPromise
                getConfiguration
                ()
                (Ok >> ResponseMsg)
                (Result.Error >> ResponseMsg)

        initialModel, (Cmd.batch [loadConfig])

    let update (msg : Msg) (model : Model) : Model * Cmd<Msg> =
        match msg with
        | ResponseMsg(Ok resp) ->
            printfn "response received: %A" resp
            resp
            |> processResponse model
        | ResponseMsg(Result.Error err) ->
            printfn "error: %s" err.Message
            model |> cmdNone
        | NavBarMsg _ ->
            printfn "navbar message"
            { model with SideMenuModel =
                            model.SideMenuModel
                            |> Components.SideMenu.update
                                    Components.SideMenu.ToggleMenu }
            |> cmdNone
        | PatientFormMsg msg ->
            printfn "form message"
            match model.PatientFormModel with
            | None -> model |> cmdNone
            | Some patFormMod ->
                let patFormMod, cmd =
                    patFormMod
                    |> Views.PatientForm.update msg
                { model with PatientFormModel = Some patFormMod }, Cmd.map Msg.PatientFormMsg cmd
        | StatusBarMsg msg->
            { model with StatusBarModel =
                            model.StatusBarModel
                            |> Components.StatusBar.update msg },  Cmd.none
        | SideMenuMsg msg ->
            { model with SideMenuModel =
                            model.SideMenuModel
                            |> Components.SideMenu.update msg }, Cmd.none

    let view model dispatch =
        muiThemeProvider
            [ MuiThemeProviderProp.Theme(ProviderTheme.Theme Styles.theme) ]
            [ div [ Id "homepage" ]
                    [ yield Components.SideMenu.view model.SideMenuModel
                                (SideMenuMsg >> dispatch)

                      yield Components.NavBar.view model.NavBarModel
                                (NavBarMsg >> dispatch)

                      match model.PatientFormModel with
                      | None -> ()
                      | Some m ->
                        yield Views.PatientForm.view m (PatientFormMsg >> dispatch)

                      yield Components.StatusBar.view model.StatusBarModel
                                (StatusBarMsg >> dispatch) ] ]
