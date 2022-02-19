namespace Components

module NavBar =

    open Fable.MaterialUI
    open Fable.MaterialUI.Icons
    open Feliz.MaterialUI
    open Feliz
    open Elmish
    open Global


    let useStyles =
        Styles.makeStyles (fun styles theme ->
            {|
                flex1 = styles.create [ style.flexGrow 1 ]
            |}
        )


    [<ReactComponent>]
    let private View
        (input: {| title: string
                   menuClick: unit -> unit |})
        =
        let classes = useStyles ()

        Mui.appBar [
            appBar.position.fixed'
            appBar.children [
                Mui.toolbar [
                    toolbar.children [
                        Mui.typography input.title
                        Html.div [
                            prop.className classes.flex1
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