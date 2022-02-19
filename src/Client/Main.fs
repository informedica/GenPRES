module Main

open Feliz
open Feliz.UseElmish
open Elmish
open Feliz.MaterialUI
open Shared
open Global
open Types


type State =
    {
        Configuration: Configuration Option
        PatientModel: Patient option
        CurrentPage: Pages Option
        SideMenuItems : (string * bool) list
        SideMenuIsOpen: bool
    }


type Msg =
    | SideMenuClick of string
    | ToggleMenu
    | UpdatePatient of Patient option


let pages = [ LifeSupport ]


let init () : State * Cmd<Msg> =

    let initialState =
        {
            Configuration = None
            PatientModel = None
            CurrentPage = LifeSupport |> Some
            SideMenuItems = [LifeSupport] |> List.map (fun p -> p |> parsePages, false)
            SideMenuIsOpen = false
        }

    initialState, Cmd.none


let update (msg: Msg) (state: State) =
    match msg with
    | ToggleMenu ->
        printfn "toggle menu"
        { state with
            SideMenuIsOpen = not state.SideMenuIsOpen
        },
        Cmd.none
    | SideMenuClick msg ->

        { state with
            CurrentPage =
                pages
                |> List.map (fun p -> p |> parsePages, p)
                |> List.tryFind (fst >> ((=) msg))
                |> Option.map snd
            SideMenuItems =
                state.SideMenuItems
                |> List.map (fun (s, b) ->
                    if s = msg then (s, true)
                    else (s, false)
                )


        },
        Cmd.none
    | UpdatePatient p -> { state with PatientModel = p }, Cmd.none

let useStyles =
    Styles.makeStyles (fun styles theme ->
        {|
            container =
                styles.create [
                    //                    style.height 100
                    style.boxSizing.borderBox
                ]
            mainview =
                styles.create [
                    //                    style.height 100
                    style.display.flex
                    style.overflow.hidden
                    style.flexDirection.column
                    style.marginLeft 20
                    style.marginRight 20
                ]
            patientpanel =
                styles.create [
                    style.marginTop 70
                    style.top 0
                ]
            patientdetails =
                styles.create [
                    style.display.flex
                    style.flexDirection.row
                ]
        |}
    )


[<ReactComponent>]
let View
    (input: {| state: State
               dispatch: Msg -> unit |})
    =
    let state = input.state
    let dispatch = input.dispatch

    let classes_ = useStyles ()

    let sidemenu =
        Components.SideMenu.render
            state.SideMenuIsOpen
            (fun _ -> ToggleMenu |> dispatch)
            (SideMenuClick >> dispatch)
            state.SideMenuItems

    let header =
        Components.NavBar.render
            "GenPRES Noodlijst"
            (fun _ -> ToggleMenu |> dispatch)

    let footer =
        Html.div [
            prop.style [
                style.display.flex
                style.left 0
                style.right 0
                style.bottom 0
                style.position.absolute
            ]
            prop.children [
                Components.StatusBar.render true "footer"
            ]
        ]

    let currentPage =
        match state.CurrentPage with
        | None -> Html.div []
        | Some page ->
            match page with
            | Pages.LifeSupport ->
                Html.div [
                    // prop.className classes.patientdetails
                    prop.style [ style.flexGrow 1 ]
                    prop.children [
                        Pages.LifeSupport.render
                            state.PatientModel
                            (UpdatePatient >> dispatch) //input.dispatch
                    ]
                ]
            | _ -> Html.div []

    let theme =
        Styles.createMuiTheme [
            theme.palette.primary Colors.blue
        ]

    Mui.themeProvider [
        themeProvider.theme theme
        themeProvider.children [
            Mui.cssBaseline []
            Html.div [
                prop.id "main"
                prop.style [
                    style.display.flex
                    style.custom ("height", "100vh")
                    style.overflowY.hidden
                    style.margin 0
                    style.padding 0
                    style.boxSizing.borderBox
                ]
                prop.children [
                    Mui.container [
                        sidemenu
                        header
                        currentPage
                        footer
                    ]
                ]
            ]
        ]
    ]


let render state dispatch =
    View({| state = state; dispatch = dispatch |})