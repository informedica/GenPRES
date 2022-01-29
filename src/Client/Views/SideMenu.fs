namespace Views

module SideMenu =
    open Feliz.MaterialUI
    open Feliz
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

    let useStyles =
        Styles.makeStyles (fun styles theme ->
            {|
                list = 
                    styles.create [
                        style.display.flex
                        style.flexDirection.column
                    ]
            |}
        )


    let listItemButton sel p dispatch =
        let txt = p |> parsePages
        Mui.listItem [ 
            prop.text txt
            listItem.button true
            prop.onClick (fun _ -> p |> MenuClick |> dispatch)
        ]
            // ListItemProp.Button true
            // ListItemProp.Divider true
            // OnClick(fun _ ->
            //     p
            //     |> MenuClick
            //     |> dispatch)
            // HTMLAttr.Selected sel ] [ str txt ]


    let menuList model dispatch =
        model.Items
        |> List.map (fun i -> listItemButton i.Selected i.Item dispatch)
        |> List.append [ 
            Mui.listItem [
                Mui.typography [ typography.variant.h6; prop.text "Menu" ]
            ]
        ]
            // listItem []
            //                  [ typography
            //                        [ TypographyProp.Variant TypographyVariant.H6 ]
            //                        [ str "Menu" ] ]
            //              divider [] ]

    [<ReactComponent>]
    let private View(input: {| model : Model; dispatch : Msg -> unit |}) =
        let classes = useStyles()
        Mui.drawer [ 
            prop.className classes.list
            drawer.variant.temporary
            drawer.onClose (fun _ -> ToggleMenu |> input.dispatch)
            drawer.open' input.model.Open
            drawer.children (menuList input.model input.dispatch)
        ]
            
            // DrawerProp.Variant DrawerVariant.Temporary
            //      MaterialProp.OnClose(fun _ -> ToggleMenu |> dispatch)
            //      MaterialProp.Open model.Open ]
            // [ list [ Class !!classes?list ] (menuList model dispatch) ]


    let render (model : Model) dispatch = View({| model = model; dispatch = dispatch |})
