namespace Components

module StatusBar =

    open Feliz.MaterialUI
    open Fable.MaterialUI
    open Feliz
    open Elmish
    open Global

    // let styles (theme : ITheme) : IStyles list = []
    let useStyles =
        Styles.makeStyles (fun styles theme -> {|  |}

        )

    [<ReactComponent>]
    let View
        (input: {| isOpen: bool
                   txt: string |})
        =
        let classes = useStyles ()

        Mui.bottomNavigation [
            prop.style [
                style.flexGrow 1
                style.alignItems.center
                style.backgroundColor Colors.grey.``100``
            ]
            bottomNavigation.showLabels true
            bottomNavigation.children [
                Mui.bottomNavigationAction [
                    bottomNavigationAction.icon (Icons.copyrightIcon [])
                ]
                Mui.typography "Informedica 2020"
                Mui.bottomNavigationAction [
                    bottomNavigationAction.icon (Icons.helpIcon [])
                ]
                Mui.bottomNavigationAction [
                    bottomNavigationAction.icon (Icons.verifiedUserIcon [])
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