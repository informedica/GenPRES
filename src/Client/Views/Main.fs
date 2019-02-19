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
    open Components

    type Model =
        { Configuration : Shared.Types.Configuration.Configuration Option
          Patient : Shared.Types.Patient.Patient Option
          SideMenuModel : Components.SideMenu.Model
          PatientFormModel : Views.PatientForm.Model Option
          StatusText : string
          StatusIsOpen : bool }

    type Msg =
        | SideMenuMsg of Components.SideMenu.Msg
        | NavBarMsg
        | PatientFormMsg of Views.PatientForm.Msg
        | ResponseMsg of Shared.Types.Response.Result
        | StatusBarMsg of bool

    let cmdNone model = model, Cmd.none

    let setStatus msg model =
        { model with StatusText = msg
                     StatusIsOpen = true }

    let processResponse model resp =
        match resp with
        | None -> model, Cmd.none
        | Some resp ->
            match resp with
            | Shared.Types.Response.Configuration config ->
                let yrs, mos, wths, hths =
                    config |> Domain.Configuration.calculateSelects "pediatrie"
                let patFormMod, cmd = Views.PatientForm.init yrs mos wths hths
                { model with Configuration = Some config
                             PatientFormModel = Some patFormMod }
                |> setStatus "configuratie ontvangen"
                |> (fun m -> m, cmd |> Cmd.map Msg.PatientFormMsg)
            | _ -> model, Cmd.none

    let getConfiguration() =
        Shared.Types.Request.Configuration.Get
        |> Shared.Types.Request.ConfigMsg
        |> Utils.Request.post

    let init() : Model * Cmd<Msg> =
        let initialModel =
            { Configuration = None
              Patient = None
              SideMenuModel = Components.SideMenu.init()
              StatusText = ""
              StatusIsOpen = false
              PatientFormModel = None }

        let loadConfig =
            Cmd.ofPromise getConfiguration () (Ok >> ResponseMsg)
                (Result.Error >> ResponseMsg)
        initialModel, (Cmd.batch [ loadConfig ])

    let update (msg : Msg) (model : Model) : Model * Cmd<Msg> =
        match msg with
        | ResponseMsg(Ok resp) ->
            printfn "response received: %A" resp
            resp |> processResponse model
        | ResponseMsg(Result.Error err) ->
            printfn "error: %s" err.Message
            model |> cmdNone
        | NavBarMsg _ ->
            let menuModel, _ =
                Components.SideMenu.update Components.SideMenu.ToggleMenu
                    model.SideMenuModel
            { model with SideMenuModel = menuModel } |> cmdNone
        | PatientFormMsg msg ->
            printfn "form message"
            match model.PatientFormModel with
            | None -> model |> cmdNone
            | Some patFormMod ->
                let patFormMod, cmd = patFormMod |> Views.PatientForm.update msg
                { model with PatientFormModel = Some patFormMod },
                Cmd.map Msg.PatientFormMsg cmd
        | StatusBarMsg b -> { model with StatusIsOpen = b }, Cmd.none
        | SideMenuMsg msg ->
            let menuModel, cmd =
                Components.SideMenu.update msg model.SideMenuModel
            { model with SideMenuModel = menuModel }, Cmd.map SideMenuMsg cmd

    let view model dispatch =
        muiThemeProvider
            [ MuiThemeProviderProp.Theme(ProviderTheme.Theme Styles.theme) ]
            [ div [ Id "homepage" ]
                  [ yield Components.SideMenu.view model.SideMenuModel
                              (SideMenuMsg >> dispatch)
                    yield Components.NavBar.view "GenPRES" NavBarMsg dispatch

                    match model.PatientFormModel with
                    | None -> ()
                    | Some m ->
                        yield Views.PatientForm.view m
                                  (PatientFormMsg >> dispatch)

                    yield StatusBar.view model.StatusIsOpen model.StatusText
                              StatusBarMsg dispatch ] ]
