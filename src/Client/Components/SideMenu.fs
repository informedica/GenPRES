namespace Components

module SideMenu =

    open Feliz
    open Feliz.UseElmish
    open Elmish
    open Global
    open MaterialUI.Drawer
    open MaterialUI.ListItem
    open MaterialUI.Typography


    type Msg =
        | ToggleMenu
        | MenuClick of string


    let init () =
        (),
        Cmd.none


    let update menuClick onClose msg state =
        match msg with
        | ToggleMenu ->
            (),
            Cmd.ofSub (fun _ -> onClose ())
        | MenuClick x ->
            (),
            Cmd.ofSub (fun _ -> x |> menuClick)


    //TODO: Fix Styles
    // let useStyles =
    //     Styles.makeStyles (fun styles theme ->
    //         {|
    //             list =
    //                 styles.create [
    //                     style.display.flex
    //                     style.flexDirection.column
    //                 ]
    //         |}
    //     )


    let listItemButton (txt: string) sel dispatch =
        MaterialListItem.create [
            prop.text txt
            MaterialListItem.button true
            MaterialListItem.selected sel
            prop.onClick (fun _ -> txt |> MenuClick |> dispatch)
        ]


    let menuList items dispatch =
        items
        |> List.map (fun (i, b) -> listItemButton i b dispatch)
        |> List.append [
            MaterialListItem.create [
                MaterialListItem.disableGutters false
                prop.children [
                    MaterialTypography.create [
                        MaterialTypography.variant "h6"
                        prop.text "Menu"
                    ]
                ]
            ]
           ]


    [<ReactComponent>]
    let private View
        (input: {| isOpen: bool
                   menuItems: (string * bool) list
                   menuClick: string -> unit
                   onClose: unit -> unit |})
        =

        //let classes = useStyles ()

        let state, dispatch =
            React.useElmish (
                init,
                update input.menuClick input.onClose,
                [| box input.isOpen |]
            )

        MaterialDrawer.create [
            //prop.className classes.list
            MaterialDrawer.variant "temporary"
            MaterialDrawer.onClose (fun _ -> ToggleMenu |> dispatch)
            MaterialDrawer.``open`` input.isOpen
            prop.children [
                Html.div [
                    prop.style [ style.padding 10]
                    prop.children (menuList input.menuItems dispatch)
                ]
            ]
        ]


    let render isOpen onClose menuClick menuItems =
        View(
            {|
                isOpen = isOpen
                menuItems = menuItems
                menuClick = menuClick
                onClose = onClose
            |}
        )