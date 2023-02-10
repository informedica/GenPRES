namespace Components

module StatusBar =

    open Feliz
    open MaterialUI.BottomNavigation
    open MaterialUI.Typography
    open MaterialUI.Icons
    open MaterialUI.Button

    open MaterialUI.Colors

    // let styles (theme : ITheme) : IStyles list = []
    // let useStyles =
    //     Styles.makeStyles (fun styles theme -> {|  |}

    //     )

    [<ReactComponent>]
    let View
        (input: {| isOpen: bool
                   txt: string |})
        =
        //let classes = useStyles ()

        MaterialBottomNavigation.create [
            prop.style [
                style.flexGrow 1
                style.alignItems.center
                style.backgroundColor Colors.grey.``100``
            ]
           // MaterialBottomNavigation.showLabels true
            prop.children [
                MaterialBottomNavigationAction.create [
                    prop.children[
                        MaterialButton.create[
                            prop.children[
                                MaterialCopyrightIcon.create[]
                            ]
                        ]
                    ]
                ]
                MaterialTypography.create[
                    prop.text "Informedica 2020"
                ]
                MaterialBottomNavigationAction.create [
                    prop.children[
                        MaterialButton.create[
                            prop.children[
                                MaterialHelpIcon.create[]
                            ]
                        ]
                    ]
                ]
                MaterialBottomNavigationAction.create [
                    prop.children[
                        MaterialButton.create[
                            prop.children[
                                MaterialGppGoodIcon.create[]
                            ]
                        ]
                    ]
                ]
            ]
        ]


    let render isOpen txt =
        View(
            {|
                isOpen = isOpen
                txt = txt
            |}
        )