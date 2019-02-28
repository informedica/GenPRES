namespace Components

module Select =
    open Fable.Core.JsInterop
    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Fable.MaterialUI.Core
    open Fable.Import.React
    open Fable.MaterialUI

    type SelectItem =
        { Key : string
          Value : string }

    type Model =
        { Label : string
          Selected : SelectItem option
          Items : SelectItem list }

    let init lbl items =
        { Label = lbl
          Selected = None
          Items =
              items
              |> List.map (fun i ->
                     { Key = i
                       Value = i }) }

    type Msg = Select of string

    let updateModel s model =
        { model with Selected =
                         model.Items |> List.tryFind (fun i -> i.Value = s) }

    let update msg model =
        match msg with
        | Select s -> model |> updateModel s

    let private styles (theme : ITheme) : IStyles list =
        [ Styles.FormControl [ MinWidth "115px"
                               CSSProp.Margin "10px" ] ]

    let private selectItem e =
        menuItem [ HTMLAttr.Value e.Value
                   Key(e.Key) ]
            [ typography [ TypographyProp.Variant TypographyVariant.H6 ]
                  [ e.Value |> str ] ]

    let view' (classes : IClasses) model dispatch =
        formControl [ MaterialProp.Margin FormControlMargin.Dense
                      Class classes?formControl ]
            [ inputLabel [] [ str model.Label ]
              select [ HTMLAttr.Value(model.Selected
                                      |> Option.map (fun i -> i.Value)
                                      |> Option.defaultValue "")
                       DOMAttr.OnChange(fun ev ->
                           ev.Value
                           |> Select
                           |> dispatch) ] [ model.Items
                                            |> List.map selectItem
                                            |> ofList ] ]

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
