namespace Components

module NavBar =

    open Fable.MaterialUI
    open Fable.MaterialUI.Icons
    open Feliz.MaterialUI
    open Feliz
    open Elmish
    open Global


    [<ReactComponent>]
    let private View
        (input: {| title: string
                   menuClick: unit -> unit |})
        =

        Mui.appBar [
            appBar.position.sticky
            appBar.children [
                Mui.toolbar [
                    toolbar.children [
                        Utils.Typography.h6 input.title
                        Html.div [
                            prop.style [
                                style.flexGrow 1
                            ]
                        ]
                        Mui.iconButton [
                            iconButton.color.inherit'
                            prop.onClick (fun _ -> input.menuClick ())
                            prop.children [
                                menuIcon [
                                    prop.style [ style.color color.white ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        ]


    let render title menuClick =
        View(
            {|
                title = title
                menuClick = menuClick
            |}
        )