namespace Components

module NavBar =
    open Fable.Core.JsInterop
    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Fable.Import.React
    open Fable.MaterialUI.Core
    open Fable.MaterialUI.Props
    open Fable.MaterialUI.Themes

    let private styles (theme : ITheme) : IStyles list =
        [ Styles.Root [ CSSProp.Display "flex"
                        CSSProp.FlexGrow 1 ]
          Styles.Custom("flex", [ FlexGrow 1 ]) ]

    let private menubutton msg dispatch =
        iconButton [ Id "menubutton"
                     MaterialProp.Color ComponentColor.Inherit
                     OnClick(fun _ ->
                         printfn "menu clicked"
                         msg |> dispatch) ] [ icon [] [ str "menu" ] ]

    let private view' (classes : IClasses) title msg dispatch =
        div [ Id "navbar"
              Class !!classes?root ]
            [ appBar [ AppBarProp.Position AppBarPosition.Fixed ]
                  [ toolbar [] [ typography
                                     [ Id "title"

                                       TypographyProp.Color
                                           TypographyColor.Inherit

                                       TypographyProp.Variant
                                           TypographyVariant.H6 ] [ str title ]
                                 div [ Class !!classes?flex ] []
                                 menubutton msg dispatch ] ] ]

    // Boilerplate code
    // Workaround for using JSS with Elmish
    // https://github.com/mvsmal/fable-material-ui/issues/4#issuecomment-422781471
    type private IProps =
        abstract title : string with get, set
        abstract msg : 'M with get, set
        abstract dispatch : ('M -> unit) with get, set
        inherit IClassesProps

    type private Component(p) =
        inherit PureStatelessComponent<IProps>(p)
        let viewFun (p : IProps) = view' p.classes p.title p.msg p.dispatch
        let viewWithStyles = withStyles (StyleType.Func styles) [] viewFun
        override this.render() =
            ReactElementType.create !!viewWithStyles this.props []

    let view title msg dispatch : ReactElement =
        let props =
            jsOptions<IProps> (fun p ->
                p.title <- title
                p.msg <- msg
                p.dispatch <- dispatch)
        ofType<Component, _, _> props []
