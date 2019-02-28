namespace Views

module PatientForm =
    open System
    open Elmish
    open Fable.Core.JsInterop
    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Fable.Import.React
    open Fable.MaterialUI.Core
    open Fable.MaterialUI.Props
    open Fable.MaterialUI.Themes
    open GenPres
    open Components
    open Utils.Utils

    type Patient = Shared.Types.Patient.Patient

    type Model =
        { Patient : Patient Option
          Year : Select.Model
          Month : Select.Model
          Weight : Select.Model
          Height : Select.Model }

    type Msg =
        | PatientLoaded of Shared.Types.Response.Result
        | ClearPatient
        | YearChange of (bool * Select.Msg)
        | MonthChange of (bool * Select.Msg)
        | WeightChange of (bool * Select.Msg)
        | HeightChange of (bool * Select.Msg)

    let getPatient msg =
        msg
        |> Shared.Types.Request.PatientMsg
        |> Utils.Request.requestToResponseCommand PatientLoaded

    let processResponse model resp =
        fun model resp ->
            match resp with
            | Shared.Types.Response.Patient pat ->
                { model with Patient = Some pat
                             Year =
                                 model.Year
                                 |> Select.updateModel (pat.Age.Years |> string)
                             Month =
                                 model.Month
                                 |> Select.updateModel
                                        (pat.Age.Months |> string)
                             Weight =
                                 model.Weight
                                 |> Select.updateModel (pat
                                                        |> Domain.Patient.getWeight
                                                        |> string)
                             Height =
                                 model.Height
                                 |> Select.updateModel (pat
                                                        |> Domain.Patient.getHeight
                                                        |> string) }, Cmd.none
            | _ -> model, Cmd.none
        |> Utils.Response.processResponse model resp

    let init (yrs : int list) (mos : int list) (whts : float list)
        (hths : int list) =
        let model =
            { Patient = None
              Year = Select.init "Jaren" (yrs |> List.map string)
              Month = Select.init "Maanden" (mos |> List.map string)
              Weight =
                  Select.init "Gewicht (kg)"
                      (whts |> List.map ((Math.fixPrecision 2) >> string))
              Height = Select.init "Lengte (cm)" (hths |> List.map string) }

        let loadPatient = getPatient Shared.Types.Request.Patient.Init
        model, loadPatient

    let show pat =
        match pat with
        | Some p -> p |> Domain.Patient.show
        | None -> ""

    let setModelYear msg model =
        let (Select.Select(yr)) = msg
        match yr |> Int32.TryParse with
        | (true, n) ->
            { model with Year = Select.update msg model.Year
                         Patient =
                             match model.Patient with
                             | Some p ->
                                 { p with Age = { p.Age with Years = n } }
                                 |> Some
                             | None -> None }
        | (false, _) -> model

    let setModelMonth msg model =
        let (Select.Select(mo)) = msg
        match mo |> Int32.TryParse with
        | (true, n) ->
            { model with Month = Select.update msg model.Month
                         Patient =
                             match model.Patient with
                             | Some p ->
                                 { p with Age = { p.Age with Months = n } }
                                 |> Some
                             | None -> None }
        | (false, _) -> model

    let setModelWeight msg model =
        let (Select.Select(wt)) = msg
        printfn "setting weight to %s" wt
        match wt |> Double.TryParse with
        | (true, n) ->
            printfn "could parse weight to %f" n
            { model with Weight = Select.update msg model.Weight
                         Patient =
                             match model.Patient with
                             | Some p ->
                                 { p with Weight =
                                              { p.Weight with Measured = n } }
                                 |> Some
                             | None -> None }
        | (false, _) -> model

    let setModelHeight msg model =
        let (Select.Select(ht)) = msg
        match ht |> Double.TryParse with
        | (true, n) ->
            { model with Height = Select.update msg model.Height
                         Patient =
                             match model.Patient with
                             | Some p ->
                                 { p with Height =
                                              { p.Height with Measured = n } }
                                 |> Some
                             | None -> None }
        | (false, _) -> model

    let update msg model =
        let change set calc msg model =
            let model = model |> set msg

            let cmd =
                if not calc then Cmd.none
                else
                    match model.Patient with
                    | Some pat ->
                        getPatient (Shared.Types.Request.Patient.Calculate pat)
                    | None -> Cmd.none
            model, cmd
        match msg with
        | PatientLoaded(resp) -> resp |> processResponse model
        | ClearPatient -> model, (getPatient Shared.Types.Request.Patient.Init)
        | YearChange(calc, msg) -> model |> change setModelYear calc msg
        | MonthChange(calc, msg) -> model |> change setModelMonth calc msg
        | WeightChange(calc, msg) -> model |> change setModelWeight calc msg
        | HeightChange(calc, msg) -> model |> change setModelHeight calc msg

    let private styles (theme : ITheme) : IStyles list =
        [ Styles.Form [ CSSProp.Flex "1" ]

          Styles.Button
              [ CSSProp.FlexBasis "auto"
                CSSProp.Flex "1"
                CSSProp.MarginTop "10px"
                CSSProp.BackgroundColor Fable.MaterialUI.Colors.green.``50`` ]
          Styles.Custom("show", [ CSSProp.PaddingTop "20px" ]) ]

    let private view' (classes : IClasses) model dispatch =
        let toMsg msg s =
            (true, s)
            |> msg
            |> dispatch
        form [ Id "patientform"
               Class classes?form ]
            [ formGroup [ FormGroupProp.Row true ]
                  [ Select.view model.Year (YearChange |> toMsg)
                    Select.view model.Month (MonthChange |> toMsg)
                    Select.view model.Weight (WeightChange |> toMsg)
                    Select.view model.Height (HeightChange |> toMsg) ]

              div [ Style [ CSSProp.Display "flex" ] ]
                  [ button [ OnClick(fun _ -> ClearPatient |> dispatch)
                             ButtonProp.Variant ButtonVariant.Contained
                             Class classes?button ]
                        [ typography
                              [ TypographyProp.Color TypographyColor.Inherit
                                TypographyProp.Variant TypographyVariant.Body1 ]
                              [ str "verwijder" ] ] ] ]

    // Boilerplate code
    // Workaround for using JSS with Elmish
    // https://github.com/mvsmal/fable-material-ui/issues/4#issuecomment-422781471
    type private IProps =
        abstract model : Model with get, set
        abstract dispatch : (Msg -> unit) with get, set
        inherit IClassesProps

    type private Component(p) =
        inherit PureStatelessComponent<IProps>(p)
        let viewFun (p : IProps) = view' p.classes p.model p.dispatch
        let viewWithStyles = withStyles (StyleType.Func styles) [] viewFun
        override this.render() =
            ReactElementType.create !!viewWithStyles this.props []

    let view (model : Model) (dispatch : Msg -> unit) : ReactElement =
        let props =
            jsOptions<IProps> (fun p ->
                p.model <- model
                p.dispatch <- dispatch)
        ofType<Component, _, _> props []
