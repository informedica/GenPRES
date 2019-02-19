namespace Components

module SideMenu =
    open Fable.Core.JsInterop
    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Fable.Import.React
    open Fable.MaterialUI
    open Fable.MaterialUI.Core
    open Fable.MaterialUI.Props
    open Fable.MaterialUI.Themes
    open GenPres.Shared.Types.Request.Configuration
    open Elmish

    type Msg =
        | ToggleMenu
        | MenuClick of string

    type ListItemButton =
        { Text : string
          Selected : bool }

    type Model =
        { Open : bool
          Items : ListItemButton list }

    let init() =
        { Open = false
          Items =
              [ { Text = "Noodlijst"
                  Selected = false }
                { Text = "Continue Medicatie"
                  Selected = false } ] }

    let update msg model =
        match msg with
        | ToggleMenu -> { model with Open = not model.Open }, Cmd.none
        | MenuClick s ->
            { model with Items =
                             [ for item in model.Items do
                                   if item.Text = s then
                                       yield { item with Selected = true }
                                   else yield { item with Selected = false } ] },
            Cmd.ofMsg ToggleMenu

    let styles (theme : ITheme) : IStyles list =
        [ Styles.Custom("list",
                        [ Display "flex"
                          FlexDirection "column" ]) ]

    let listItemButton sel txt dispatch =
        listItem [ ListItemProp.Button true
                   ListItemProp.Divider true
                   OnClick(fun _ ->
                       txt
                       |> MenuClick
                       |> dispatch)
                   HTMLAttr.Selected sel ] [ str txt ]

    let menuList model dispatch =
        model.Items
        |> List.map (fun i -> listItemButton i.Selected i.Text dispatch)
        |> List.append [ listItem []
                             [ typography
                                   [ TypographyProp.Variant TypographyVariant.H6 ]
                                   [ str "Menu" ] ]
                         divider [] ]

    let private view' (classes : IClasses) (model : Model) dispatch =
        drawer [ DrawerProp.Variant DrawerVariant.Temporary
                 MaterialProp.OnClose(fun _ -> ToggleMenu |> dispatch)
                 MaterialProp.Open model.Open ]
            [ list [ Class !!classes?list ] (menuList model dispatch) ]

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
