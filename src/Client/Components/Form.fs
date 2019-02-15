namespace Components

module Form =
    open Fable.Core.JsInterop
    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Fable.Import.React
    open Fable.MaterialUI.Core
    open Fable.MaterialUI.Props
    open Fable.MaterialUI.Themes

    type Model =
        { Year : Select.Model
          Month : Select.Model
          Weight : Select.Model
          Height : Select.Model }

    let init() =
        { Year = Select.init "Jaren" ([ 0..18 ] |> List.map string)
          Month = Select.init "Maanden" ([ 0..11 ] |> List.map string)
          Weight = Select.init "Gewicht (kg)" ([ 0..100 ] |> List.map string)
          Height = Select.init "Lengte (cm)" ([ 0..200 ] |> List.map string) }

    type Msg =
        | YearChange of Select.Msg
        | MonthChange of Select.Msg
        | WeightChange of Select.Msg
        | HeightChange of Select.Msg

    let update msg model =
        match msg with
        | YearChange msg -> { model with Year = Select.update msg model.Year }
        | MonthChange msg ->
            { model with Month = Select.update msg model.Month }
        | WeightChange msg ->
            { model with Weight = Select.update msg model.Weight }
        | HeightChange msg ->
            { model with Height = Select.update msg model.Height }

    let private styles (theme : ITheme) : IStyles list =
        [ Styles.Form [ CSSProp.Padding "20px" ]
          Styles.Button [ CSSProp.FlexGrow "1" ] ]

    let private view' (classes : IClasses) model dispatch =
        form [ Id "patientform"
               Class classes?form ]
            [ formGroup [ FormGroupProp.Row true ]
                  [ Select.view model.Year (YearChange >> dispatch)
                    Select.view model.Month (MonthChange >> dispatch)
                    Select.view model.Weight (WeightChange >> dispatch)
                    Select.view model.Height (HeightChange >> dispatch) ]
              button [ Class classes?button ] [ str "verwijder" ] ]

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
