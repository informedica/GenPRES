namespace Components

module StatusBar =
    open Fable.Core.JsInterop
    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Fable.Import.React
    open Fable.MaterialUI.Core
    open Fable.MaterialUI.Props
    open Fable.MaterialUI.Themes

    type Model = Status of string

    let init() = Status "Offline"

    type Msg =
        | IsOnline of string
        | IsOffLine

    let update msg model =
        match msg with
        | IsOnline s -> Status s
        | IsOffLine -> Status "Offline"

    let styles (theme : ITheme) : IStyles list = []

    let view' (classes : IClasses) model dispatch =
        let (Status s) = model
        snackbar [ Open true
                   SnackbarProp.Message(str s) ] []

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
