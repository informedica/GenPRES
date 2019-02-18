namespace Components

module StatusBar =
    open Fable.Core.JsInterop
    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Fable.Import.React
    open Fable.MaterialUI.Core
    open Fable.MaterialUI.Props
    open Fable.MaterialUI.Themes


    let styles (theme : ITheme) : IStyles list = []

    let view' (classes : IClasses) isOpen txt msg dispatch =
        snackbar [ Open isOpen
                   OnClose(fun _ _ ->
                        false
                        |> msg
                        |> dispatch)
                   SnackbarProp.AutoHideDuration 1000
                   SnackbarProp.Message(str txt) ] []

    // Boilerplate code
    // Workaround for using JSS with Elmish
    // https://github.com/mvsmal/fable-material-ui/issues/4#issuecomment-422781471
    type private IProps =
        abstract isOpen : bool with get, set
        abstract text : string with get, set
        abstract msg : ('T -> 'M) with get, set
        abstract dispatch : ('M -> unit) with get, set
        inherit IClassesProps

    type private Component(p) =
        inherit PureStatelessComponent<IProps>(p)
        let viewFun (p : IProps) = view' p.classes p.isOpen p.text p.msg p.dispatch
        let viewWithStyles = withStyles (StyleType.Func styles) [] viewFun
        override this.render() =
            ReactElementType.create !!viewWithStyles this.props []

    let view isOpen txt msg dispatch : ReactElement =
        let props =
            jsOptions<IProps> (fun p ->
                p.isOpen <- isOpen
                p.text <- txt
                p.msg <- msg
                p.dispatch <- dispatch)
        ofType<Component, _, _> props []
