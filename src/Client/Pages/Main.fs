namespace Pages

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
    open Global
    open Views

    type Model =
        { Configuration : Shared.Types.Configuration.Configuration Option
          SideMenuModel : SideMenu.Model
          PatientFormModel : PatientForm.Model Option
          CurrentPage : Pages Option
          StatusText : string
          StatusIsOpen : bool }

    type Msg =
        | SideMenuMsg of SideMenu.Msg
        | NavBarMsg
        | PatientFormMsg of PatientForm.Msg
        | ResponseMsg of Shared.Types.Response.Result
        | StatusBarMsg of bool

    let cmdNone model = model, Cmd.none

    let setStatus txt model =
        { model with StatusText = txt
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
              SideMenuModel = SideMenu.init()
              CurrentPage = None
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
                SideMenu.update SideMenu.ToggleMenu model.SideMenuModel
            { model with SideMenuModel = menuModel } |> cmdNone
        | PatientFormMsg msg ->
            printfn "form message"
            match model.PatientFormModel with
            | None -> model |> cmdNone
            | Some patFormMod ->
                let model =
                    match msg with
                    | PatientForm.PatientLoaded(Ok resp) ->
                    resp
                    |> processResponse model
                    |> fst
                    | _ -> model
                let patFormMod, cmd = patFormMod |> Views.PatientForm.update msg
                { model with PatientFormModel = Some patFormMod },
                Cmd.map Msg.PatientFormMsg cmd
        | StatusBarMsg b -> { model with StatusIsOpen = b }, Cmd.none
        | SideMenuMsg msg ->
            let menuModel, cmd = SideMenu.update msg model.SideMenuModel
            let page =
                match msg with
                | SideMenu.MenuClick p -> p |> Some
                | _ -> model.CurrentPage
            { model with SideMenuModel = menuModel
                         CurrentPage = page }, Cmd.map SideMenuMsg cmd

    let view model dispatch =
        muiThemeProvider
            [ MuiThemeProviderProp.Theme(ProviderTheme.Theme Styles.theme) ]
            [ div [ Id "homepage" ]
                  [ yield SideMenu.view model.SideMenuModel
                              (SideMenuMsg >> dispatch)
                    yield NavBar.view "GenPRES" NavBarMsg dispatch

                    match model.PatientFormModel with
                    | None -> ()
                    | Some m ->
                        yield Views.PatientForm.view m
                                  (PatientFormMsg >> dispatch)

                    match model.CurrentPage with
                    | None -> ()
                    | Some p ->
                        match p with
                        | EmergencyList ->
                            match model.PatientFormModel with
                            | Some formMod ->
                                match formMod.Patient with
                                | Some pat ->
                                    let a = pat |> Domain.Patient.getAgeInYears
                                    let w = pat |> Domain.Patient.getWeight

                                    yield div [ Style [ MarginTop "20px"  ] ] [ EmergencyList.view a w dispatch ]
                                | None -> ()
                            | None -> ()  
                        | _ -> () 

                    yield StatusBar.view model.StatusIsOpen model.StatusText
                              StatusBarMsg dispatch ] ]