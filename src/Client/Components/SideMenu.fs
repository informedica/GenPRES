namespace Components

module SideMenu =

    open MaterialUI5
    open Feliz
    open Feliz.UseElmish
    open Elmish
    open FSharp.Core


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


    let listItemButton (txt: string) sel dispatch =
        Mui.listItem [
            prop.text txt
            listItem.button true
            listItem.selected sel
            prop.onClick (fun _ -> txt |> MenuClick |> dispatch)
        ]


    let menuList items dispatch =
        items
        |> List.map (fun (i, b) -> listItemButton i b dispatch)
        |> List.append [
            Mui.listItem [
                listItem.disableGutters false
                prop.children [
                    Mui.typography [
                        typography.variant.h6
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
        let classes = useStyles ()

        let state, dispatch =
            React.useElmish (
                init,
                update input.menuClick input.onClose,
                [| box input.isOpen |]
            )

        Mui.drawer [
            prop.className classes.list
            drawer.variant.temporary
            drawer.onClose (fun _ -> ToggleMenu |> dispatch)
            drawer.open' input.isOpen
            drawer.children [
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