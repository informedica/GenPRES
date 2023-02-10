namespace Components

module NavBar =

    open Feliz
    open Elmish
    open Global
    open Shared
    open MaterialUI.AppBar
    open MaterialUI.Toolbar
    open MaterialUI.IconButton
    open MaterialUI.Icons


    [<ReactComponent>]
    let private View
        (input: {| selected: string
                   menuClick: unit -> unit
                   languages: string list
                   langChange: string -> unit |})
        =

        let langMenu =
            let selected =
                Localization.Dutch |> Localization.toString

            let items = input.languages

            Menu.render selected items input.langChange

        MaterialAppBar.create [
            prop.style [style.position.sticky]
            prop.children [
                MaterialToolbar.create [
                    prop.children [
                        Utils.Typography.h6 input.selected
                        Html.div [
                            prop.style [ style.flexGrow 1 ]
                        ]
                        langMenu
                        MaterialIconButton.create [
                            MaterialIconButton.color "inherit"
                            prop.onClick (fun _ -> input.menuClick ())
                            prop.children [
                                MaterialMenuIcon.create [
                                    prop.style [ style.color color.white ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        ]


    let render title menuClick languages langChange =
        View(
            {|
                selected = title
                menuClick = menuClick
                languages = languages
                langChange = langChange
            |}
        )