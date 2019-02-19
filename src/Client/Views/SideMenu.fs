namespace Views

module SideMenu =
    open Fable.Core.JsInterop
    open Fable.Helpers.React
    open Fable.Helpers.React.Props
    open Fable.Import.React
    open Fable.MaterialUI
    open Fable.MaterialUI.Core
    open Elmish

    open Global

    type Msg =
        | ToggleMenu
        | MenuClick of Pages

    type ListItemButton =
        { Item : Pages
          Selected : bool }

    type Model =
        { Open : bool
          Items : ListItemButton list }

    let init() =
        { Open = false
          Items =
              [ { Item = EmergencyList
                  Selected = false }
                { Item = ContinuousMeds
                  Selected = false } ] }

    let update msg model =
        match msg with
        | ToggleMenu -> { model with Open = not model.Open }, Cmd.none
        | MenuClick p ->
            
            { model with Items =
                             [ for item in model.Items do
                                   if item.Item = p then
                                       yield { item with Selected = true }
                                   else yield { item with Selected = false } ] },
            Cmd.ofMsg ToggleMenu

    let styles (theme : ITheme) : IStyles list =
        [ Styles.Custom("list",
                        [ Display "flex"
                          FlexDirection "column" ]) ]

    let listItemButton sel p dispatch =
        let txt = p |> parsePages
        listItem [ ListItemProp.Button true
                   ListItemProp.Divider true
                   OnClick(fun _ ->
                       p
                       |> MenuClick
                       |> dispatch)
                   HTMLAttr.Selected sel ] [ str txt ]

    let menuList model dispatch =
        model.Items
        |> List.map (fun i -> listItemButton i.Selected i.Item dispatch)
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
