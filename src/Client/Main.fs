module Main

open Feliz
open Feliz.Router
open Feliz.UseElmish
open Elmish
open Feliz.MaterialUI
open Shared
open Global
open Types
open Utils

type State =
    {
        Configuration: Configuration Option
        Patient: Patient option
        ContinuousMeds: Continuous list
        CurrentPage: Pages Option
        SideMenuItems: (string * bool) list
        SideMenuIsOpen: bool
    }


type Msg =
    | SideMenuClick of string
    | ToggleMenu
    | UpdatePatient of Patient option
    | UrlChanged of string list
    | LoadData
    | ReceivedData of string


let pages = [ LifeSupport; ContinuousMeds ]


let parseUrl sl =
    Utils.Logging.log "url" sl

    match sl with
    | [] -> None
    | [ "pat"
        Route.Query [ "ay", Route.Int ay; "am", Route.Int am; "ad", Route.Int ad ] ] ->
        Patient.create (Some ay) (Some am) (Some ad) None None None
    | _ -> None



let init () : State * Cmd<Msg> =

    let initialState =
        {
            Configuration = None
            Patient = Router.currentUrl () |> parseUrl
            ContinuousMeds = []
            CurrentPage = LifeSupport |> Some
            SideMenuItems =
                [ LifeSupport; ContinuousMeds ]
                |> List.map (fun p -> p |> pageToString, false)
            SideMenuIsOpen = false
        }

    initialState, Cmd.ofMsg LoadData


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
                |> List.map (fun p -> p |> pageToString, p)
                |> List.tryFind (fst >> ((=) msg))
                |> Option.map snd
            SideMenuItems =
                state.SideMenuItems
                |> List.map (fun (s, _) ->
                    if s = msg then
                        (s, true)
                    else
                        (s, false)
                )
        },
        Cmd.none
    | UpdatePatient p -> { state with Patient = p }, Cmd.none
    | UrlChanged sl ->
        Logging.log "url changed" sl
        state, Cmd.none
    | LoadData ->
        let load = Google.getContMeds (ReceivedData)
        state, Cmd.OfAsync.result load
    | ReceivedData s ->
        Logging.log "received data" (s |> Shared.ContMeds.getContMed)

        { state with
            ContinuousMeds = s |> ContMeds.getContMed
        },
        Cmd.none


[<ReactComponent>]
let View
    (input: {| state: State
               dispatch: Msg -> unit |})
    =
    let state = input.state
    let dispatch = input.dispatch

    let sidemenu =
        Components.SideMenu.render
            state.SideMenuIsOpen
            (fun _ -> ToggleMenu |> dispatch)
            (SideMenuClick >> dispatch)
            state.SideMenuItems

    let header =
        Components.NavBar.render
            $"""GenPRES {state.CurrentPage
                         |> Option.map pageToString
                         |> Option.defaultValue ""}"""
            (fun _ -> ToggleMenu |> dispatch)

    let footer =
        Components.StatusBar.render true "footer"

    let patientView =
        Html.div [
            prop.id "patient-view"
            prop.style [ style.marginBottom 20 ]
            prop.children [
                Views.Patient.render state.Patient (UpdatePatient >> dispatch)
            ]
        ]

    let currentPage =
        match state.CurrentPage with
        | None ->
            Html.div [
                Utils.Typography.h1 "No page"
            ]
        | Some page ->
            Html.div [
                prop.style [
                    style.display.flex
                    style.flexDirection.column
                ]
                prop.children [
                    patientView
                    Html.div [
                        prop.style [
                            style.flexGrow 1
                            style.overflowY.scroll
                        ]
                        prop.children [
                            match page with
                            | LifeSupport ->
                                Pages.LifeSupport.render
                                    state.Patient
                                    (UpdatePatient >> dispatch) //input.dispatch

                            | ContinuousMeds ->
                                Pages.ContinuousMeds.render
                                    state.Patient
                                    state.ContinuousMeds


                            ]
                    ]
                ]
            ]

    let theme =
        Styles.createTheme [
            theme.palette.primary Colors.blue
        ]

    let main =
        Mui.themeProvider [
            themeProvider.theme theme
            themeProvider.children [
                Mui.cssBaseline []
                Html.div [
                    prop.style [
                        //                    style.custom ("width", "100vh")
                        style.custom ("height", "100vh")
                        style.display.flex
                        style.flexDirection.column
                        style.flexWrap.nowrap
                    ]
                    prop.children [
                        Html.div [
                            prop.style [ style.flexShrink 0 ]
                            prop.children [ header ]
                        ]
                        sidemenu
                        Mui.container [
                            prop.style [
                                style.display.flex
                                style.flexGrow 1
                                style.overflow.hidden
                                style.marginTop 10
                                style.marginBottom 10
                            ]
                            container.children[currentPage]
                        ]
                        Html.div [
                            prop.style [ style.flexShrink 0 ]
                            prop.children [ footer ]
                        ]
                    ]
                ]
            ]
        ]

    React.router [
        router.onUrlChanged (UrlChanged >> dispatch)
        router.children main
    ]


let render state dispatch =
    View({| state = state; dispatch = dispatch |})