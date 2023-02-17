namespace Components

module NavBar =

    open MaterialUI5
    open Feliz
    open Shared


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

        Mui.appBar [
            appBar.position.sticky
            appBar.children [
                Mui.toolbar [
                    toolbar.children [
                        Utils.Typography.h6 input.selected
                        Html.div [
                            prop.style [ style.flexGrow 1 ]
                        ]
                        langMenu
                        Mui.iconButton [
                            iconButton.color.inherit'
                            prop.onClick (fun _ -> input.menuClick ())
                            prop.children [
                                Icons.menuIcon [
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