namespace Components

module NavBar =
    open Fable.Core.JsInterop
    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Fable.Import.React
    open Fable.MaterialUI.Core
    open Fable.MaterialUI.Props
    open Fable.MaterialUI.Themes

    type Model =
        { Title : string }

    type Msg = MenuClick

    let init() = { Title = "GenPRES" }

    let private styles (theme : ITheme) : IStyles list =
        [ Styles.Root [ CSSProp.Display "flex"
                        CSSProp.FlexGrow 1 ]
          Styles.Custom("flex", [ FlexGrow 1 ]) ]

    let private menubutton dispatch =
        iconButton [ Id "menubutton"
                     MaterialProp.Color ComponentColor.Inherit
                     OnClick(fun _ ->
                         printfn "menu clicked"
                         MenuClick |> dispatch) ] [ icon [] [ str "menu" ] ]

    let private view' (classes : IClasses) model dispatch =
        div [ Id "navbar"
              Class !!classes?root ]
            [ appBar [ AppBarProp.Position AppBarPosition.Fixed ]
                  [ toolbar [] [ typography
                                     [ Id "title"

                                       TypographyProp.Color
                                           TypographyColor.Inherit

                                       TypographyProp.Variant
                                           TypographyVariant.H6 ]
                                     [ str model.Title ]
                                 div [ Class !!classes?flex ] []
                                 menubutton dispatch ] ] ]

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
