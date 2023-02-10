namespace Components

module Menu =

    open Feliz
    open Elmish
    open MaterialUI.Menu
    open MaterialUI.MenuItem
    open MaterialUI.Button
    open MaterialUI.Icons


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
                MaterialMenuItem.create [
                    prop.text i
                    prop.onClick (fun _ ->
                        setSelected i
                        handleClose ()
                        input.menuClick i
                    )
                ]
            )


        Html.div [
            MaterialButton.create [
                prop.onClick (fun evt -> evt |> Some |> handleMenu)
                MaterialButton.color "inherit"
                prop.children[
                    MaterialLanguageIcon.create[]
                ]
                prop.text selected
            ]
            MaterialMenu.create [
                MaterialMenu.keepMounted true
                MaterialMenu.``open`` (anchorEl |> Option.isSome)
                MaterialMenu.anchorEl anchorEl
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