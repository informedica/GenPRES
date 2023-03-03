namespace Components

module Menu =

    open MaterialUI5
    open Feliz
    open Elmish


    [<ReactComponent>]
    let private View
        (input: {| selected: string
                   items: string list
                   menuClick: string -> unit |})
        =
        let anchorEl, setAnchorEl =
            React.useState (None)

        let selected, setSelected =
            React.useState input.selected

        let handleMenu (evt: Browser.Types.MouseEvent option) =
            evt
            |> Option.map (fun e -> e.currentTarget :?> Browser.Types.Element)
            |> setAnchorEl

        let handleClose _ = setAnchorEl None

        let menuItems =
            input.items
            |> List.map (fun i ->
                Mui.menuItem [
                    prop.text i
                    prop.onClick (fun _ ->
                        setSelected i
                        handleClose ()
                        input.menuClick i
                    )
                ]
            )

        Html.div [
            Mui.button [
                prop.onClick (fun evt -> evt |> Some |> handleMenu)
                button.color.inherit'
                button.startIcon (Icons.languageIcon [])
                prop.text selected
            ]
            Mui.menu [
                menu.keepMounted true
                menu.open' (anchorEl |> Option.isSome)
                menu.anchorEl anchorEl
                prop.children menuItems
            ]
        ]


    let render selected items menuClick =
        View(
            {|
                selected = selected
                items = items
                menuClick = menuClick
            |}
        )