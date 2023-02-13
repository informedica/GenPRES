module Main

open Feliz
open Feliz.Router
open Feliz.UseElmish
open Elmish
open Feliz.MaterialUI
open Shared
open Types
open Utils
open MaterialUI.Container


type Locales = Localization.Locales
type Pages = Global.Pages

type State =
    {
        CurrentPage: Pages Option
        SideMenuItems: (string * bool) list
        SideMenuIsOpen: bool
    }


type Msg =
    | SideMenuClick of string
    | ToggleMenu
    | UpdatePatient of Patient option
    | LanguageChange of string


let pages =
    [
        Pages.LifeSupport
        Pages.ContinuousMeds
        Pages.Prescribe
    ]


let init lang : State * Cmd<Msg> =
    let pageToString = Global.pageToString lang

    let initialState =
        {
            CurrentPage = Pages.LifeSupport |> Some
            SideMenuItems =
                pages
                |> List.map (fun p -> p |> pageToString, false)
            SideMenuIsOpen = false
        }

    initialState, Cmd.none


let update lang updateLang updatePatient (msg: Msg) (state: State) =
    match msg with
    | ToggleMenu ->
        { state with
            SideMenuIsOpen = not state.SideMenuIsOpen
        },
        Cmd.none
    | SideMenuClick msg ->
        let pageToString = Global.pageToString lang

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
    | UpdatePatient p -> state, Cmd.ofSub (fun _ -> p |> updatePatient)
    | LanguageChange s ->
        state, Cmd.ofSub (fun _ -> s |> Localization.fromString |> updateLang)


[<ReactComponent>]
let View
    (input: {| updateLang: Localization.Locales -> unit
               patient: Patient option
               updatePatient: Patient option -> unit
               bolusMedication: Deferred<Intervention list>
               continuousMedication: Deferred<Intervention list>
               products: Deferred<Product list>
               scenarios: Deferred<ScenarioResult>
               updateScenarios : ScenarioResult -> unit |})
    =
    let lang =
        React.useContext (Global.languageContext)

    let state, dispatch =
        React.useElmish (
            init lang,
            update lang input.updateLang input.updatePatient,
            [| box lang |]
        )

    let sidemenu =
        Components.SideMenu.render
            state.SideMenuIsOpen
            (fun _ -> ToggleMenu |> dispatch)
            (SideMenuClick >> dispatch)
            state.SideMenuItems

    let header =
        let title =
            $"""GenPRES {state.CurrentPage
                         |> Option.map (Global.pageToString lang)
                         |> Option.defaultValue ""}"""

        let menuClick _ = ToggleMenu |> dispatch

        let languages =
            Localization.languages
            |> List.map Localization.toString

        let langChange = LanguageChange >> dispatch
        Components.NavBar.render title menuClick languages langChange

    let footer =
        Components.StatusBar.render true "footer"

    let patientView =
        Html.div [
            prop.id "patient-view"
            prop.style [ style.marginBottom 20 ]
            prop.children [
                Views.Patient.render input.patient (UpdatePatient >> dispatch)
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
                            | Pages.LifeSupport ->
                                Pages.LifeSupport.render input.bolusMedication
                            | Pages.ContinuousMeds ->
                                match input.continuousMedication, input.products
                                    with
                                | Resolved meds, Resolved prods ->
                                    Pages.ContinuousMeds.render
                                        input.patient
                                        meds
                                        prods
                                | _ ->
                                    Pages.ContinuousMeds.render
                                        input.patient
                                        []
                                        []
                            | Pages.Prescribe ->
                                Pages.Prescribe.render input.scenarios input.updateScenarios
                        ]
                    ]
                ]
            ]

    let theme =
        Styles.createTheme [
            theme.overrides.muiDialogTitle.root [
                style.backgroundColor.lightGray
            ]
            theme.palette.primary Colors.blue
        ]

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
                    MaterialContainer.create [
                        prop.style [
                            style.display.flex
                            style.flexGrow 1
                            style.overflow.hidden
                            style.marginTop 10
                            style.marginBottom 10
                        ]
                        prop.children[currentPage]
                    ]
                    Html.div [
                        prop.style [ style.flexShrink 0 ]
                        prop.children [ footer ]
                    ]
                ]
            ]
        ]
    ]


let render
    updateLang
    patient
    updatePatient
    bolusMedication
    continuousMedication
    products
    scenarios
    updateScenarios
    =
    View(
        {|
            updateLang = updateLang
            patient = patient
            updatePatient = updatePatient
            bolusMedication = bolusMedication
            continuousMedication = continuousMedication
            products = products
            scenarios = scenarios
            updateScenarios = updateScenarios
        |}
    )