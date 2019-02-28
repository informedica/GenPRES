namespace Pages

module Main =
    open Elmish
    open Elmish.React
    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Fable.Import.React
    open Fable.Core.JsInterop
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
          EmergencyListModel : EmergencyList.Model Option
          CurrentPage : Pages Option
          StatusText : string
          StatusIsOpen : bool }

    type Msg =
        | SideMenuMsg of SideMenu.Msg
        | NavBarMsg
        | PatientFormMsg of PatientForm.Msg
        | EmergencyListMsg of EmergencyList.Msg
        | ResponseMsg of Shared.Types.Response.Result
        | StatusBarMsg of bool

    let cmdNone model = model, Cmd.none

    let setStatus txt model =
        { model with StatusText = txt
                     StatusIsOpen = true }

    let processResponse model resp =
        fun model resp ->
            match resp with
            | Shared.Types.Response.Configuration config ->
                let yrs, mos, wths, hths =
                    config |> Domain.Configuration.calculateSelects "pediatrie"
                let patFormMod, patCmd =
                    PatientForm.init yrs mos wths hths
                    |> (fun (m, c) -> m, c |> Cmd.map PatientFormMsg)
                let eListMod, eListCmd =
                    EmergencyList.init()
                    |> (fun (m, c) -> m |> Some, c |> Cmd.map EmergencyListMsg)
                { model with Configuration = Some config
                             PatientFormModel = Some patFormMod
                             EmergencyListModel = eListMod }
                |> setStatus "configuratie ontvangen"
                |> (fun m -> m, Cmd.batch [ patCmd; eListCmd ])
            | _ -> model, Cmd.none
        |> Utils.Response.processResponse model resp

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
              PatientFormModel = None
              EmergencyListModel = None }

        let loadConfig =
            Cmd.ofPromise getConfiguration () (Ok >> ResponseMsg)
                (Result.Error >> ResponseMsg)
        initialModel, (Cmd.batch [ loadConfig ])

    let update (msg : Msg) (model : Model) : Model * Cmd<Msg> =
        match msg with
        | ResponseMsg(resp) ->
            resp |> processResponse model
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
                    | PatientForm.PatientLoaded(resp) ->
                        resp
                        |> processResponse model
                        |> fst
                    | _ -> model

                let patFormMod, cmd = patFormMod |> Views.PatientForm.update msg
                let eListCmd =
                    match patFormMod.Patient with
                    | Some p -> 
                        let a, w = p |> Domain.Patient.getAgeInYears, p |> Domain.Patient.getWeight
                        EmergencyList.CalculateTreatment(a, w)
                        |> EmergencyListMsg
                        |> Cmd.ofMsg
                { model with PatientFormModel = Some patFormMod },
                Cmd.batch [ Cmd.map Msg.PatientFormMsg cmd; eListCmd ]
        | EmergencyListMsg msg ->
            match model.EmergencyListModel with
            | Some lmodel ->
                let lmodel, _ = EmergencyList.update lmodel msg
                let cmd = 
                    match msg with
                    | EmergencyList.CalculateTreatment _ -> Cmd.none
                    | _ ->
                        match model.PatientFormModel with
                        | Some fm ->
                            match fm.Patient with
                            | Some p -> 
                                let a, w = p |> Domain.Patient.getAgeInYears, p |> Domain.Patient.getWeight
                                EmergencyList.CalculateTreatment(a, w) 
                                |> EmergencyListMsg
                                |> Cmd.ofMsg
                            | None -> Cmd.none
                        | None -> Cmd.none
                    
                { model with EmergencyListModel = lmodel |> Some },
                cmd
            | None -> model, Cmd.none
        | StatusBarMsg b -> { model with StatusIsOpen = b }, Cmd.none
        | SideMenuMsg msg ->
            let menuModel, cmd = SideMenu.update msg model.SideMenuModel

            let page =
                match msg with
                | SideMenu.MenuClick p -> p |> Some
                | _ -> model.CurrentPage
            { model with SideMenuModel = menuModel
                         CurrentPage = page }, Cmd.map SideMenuMsg cmd

    let styles = Styles.styles
    let node x = ReactNode.Case1(ReactChild.Case1 x)

    let view' (classes : IClasses) model dispatch =
        let sidemenu =
            SideMenu.view model.SideMenuModel (SideMenuMsg >> dispatch)
        let navbar = NavBar.view "GenPRES" NavBarMsg dispatch

        let panel (m : PatientForm.Model) =
            let sum =
                match m.Patient with
                | Some p -> p |> Domain.Patient.show
                | None -> ""
                |> sprintf "Patient: %s"
                |> Views.Typography.caption
            Views.ExpansionPanel.panel classes (sum)
                (Views.PatientForm.view m (PatientFormMsg >> dispatch))

        let mainview =
            div [ Class !!classes?mainview ]
                [ match model.PatientFormModel with
                  | None -> ()
                  | Some m ->
                      yield div [ Class !!classes?patientpaneldiv ] [ panel m ]

                  yield div [ Style [ CSSProp.Flex "1"
                                      CSSProp.Display "flex"
                                      CSSProp.OverflowX "scroll"
                                      CSSProp.Top "0"
                                      CSSProp.MarginTop "20px"
                                      CSSProp.MarginBottom "10px" ] ]
                            [ match model.CurrentPage with
                              | None ->
                                  yield div [ Style [ CSSProp.Flex "1" ] ] []
                              | Some p ->
                                  match p with
                                  | EmergencyList ->
                                      match model.EmergencyListModel with
                                      | Some listMod ->
                                          yield EmergencyList.view listMod
                                                    (EmergencyListMsg
                                                     >> dispatch)
                                      | None -> ()
                                  | _ ->
                                      yield div [ Style [ CSSProp.Flex "1" ] ]
                                                [] ] ]

        let body =
            div [ Class !!classes?container
                  Style [ CSSProp.Height "100vh"
                          CSSProp.Display "flex"
                          CSSProp.Overflow "hidden"
                          CSSProp.FlexDirection "column" ] ]
                [ navbar; sidemenu; mainview ]

        muiThemeProvider
            [ MuiThemeProviderProp.Theme(ProviderTheme.Theme Styles.theme) ]
            [ div [ Class !!classes?container
                    Style [ CSSProp.Height "100vh"
                            CSSProp.Display "flex"
                            CSSProp.Overflow "hidden"
                            CSSProp.FlexDirection "column" ] ] [ body ] ]

    // Boilerplate code
    // Workaround for using JSS with Elmish
    // https://github.com/mvsmal/fable-material-ui/issues/4#issuecomment-422781471
    type private IProps =
        abstract model : Model with get, set
        abstract dispatch : ('M -> unit) with get, set
        inherit IClassesProps

    type private Component(p) =
        inherit PureStatelessComponent<IProps>(p)
        let viewFun (p : IProps) = view' p.classes p.model p.dispatch
        let viewWithStyles = withStyles (StyleType.Func styles) [] viewFun
        override this.render() =
            ReactElementType.create !!viewWithStyles this.props []

    let view model dispatch : ReactElement =
        let props =
            jsOptions<IProps> (fun p ->
                p.model <- model
                p.dispatch <- dispatch)
        ofType<Component, _, _> props []
