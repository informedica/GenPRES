namespace Pages


open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types


open Elmish
open Shared


module GenPres =


    module private Elmish =


        open Global


        type State =
            {
                CurrentPage: Pages
                SideMenuItems: (string * bool) []
                SideMenuIsOpen: bool
                Configuration: Configuration Option
                Language: Localization.Locales
            }


        type Msg =
            | SideMenuClick of string
            | ToggleMenu
            | LanguageChange of string
            | UpdateLanguage of Localization.Locales


        let pages =
            [
                LifeSupport
                ContinuousMeds
                Prescribe
                Formulary
                Parenteralia
            ]


        let init page : State * Cmd<Msg> =

            let state =
                {
                    CurrentPage = page |> Option.defaultValue LifeSupport
                    SideMenuItems =
                        pages
                        |> List.toArray
                        |> Array.map (fun p ->
                            p
                            |> pageToString Localization.Dutch,
                            false
                        )
                    SideMenuIsOpen = false
                    Configuration = None
                    Language = Localization.Dutch
                }

            state, Cmd.none


        let update (msg: Msg) (state: State) =
            match msg with
            | ToggleMenu ->
                { state with
                    SideMenuIsOpen = not state.SideMenuIsOpen
                },
                Cmd.none

            | SideMenuClick s ->
                let pageToString = Global.pageToString Localization.Dutch

                { state with
                    CurrentPage =
                        pages
                        |> List.map (fun p -> p |> pageToString, p)
                        |> List.tryFind (fst >> ((=) s))
                        |> Option.map snd
                        |> Option.defaultValue LifeSupport

                    SideMenuItems =
                        state.SideMenuItems
                        |> Array.map (fun (item, _) ->
                            if item = s then
                                printfn $"{s} true"
                                (item, true)
                            else
                                printfn $"{s} false"
                                (item, false)
                        )
                },
                Cmd.none

            | LanguageChange s ->
                //TODO: doesn't work anymore
                state, Cmd.none

            | UpdateLanguage l -> { state with Language = l }, Cmd.none


    open Elmish



    [<JSX.Component>]
    let View
        (props: {|
            patient: Patient option
            updatePatient: Patient option -> unit
            bolusMedication: Deferred<Intervention list>
            continuousMedication: Deferred<Intervention list>
            products: Deferred<Product list>
            scenario: Deferred<ScenarioResult>
            updateScenario : ScenarioResult -> unit
            page : Global.Pages option |}) =

        let state, dispatch = React.useElmish (init props.page, update, [| box props.page |])

        let notFound =
            JSX.jsx
                $"""
            <React.Fragment>
                <Typography>
                    Nog niet geimplementeerd
                </Typography>
            </React.Fragment>
            """

        JSX.jsx
            $"""
        import {{ ThemeProvider }} from '@mui/material/styles';
        import CssBaseline from '@mui/material/CssBaseline';
        import React from "react";
        import Stack from '@mui/material/Stack';
        import Box from '@mui/material/Box';
        import Container from '@mui/material/Container';
        import Typography from '@mui/material/Typography';

        <React.Fragment>
            <Box>
                {Components.AppBar.View({|
                    title = $"GenPRES 2023 {state.CurrentPage |> (Global.pageToString Localization.Dutch)}"
                    toggleSideMenu = fun _ -> ToggleMenu |> dispatch
                |})}
            </Box>
            <React.Fragment>
                {
                    Components.SideBar.View({|
                        anchor = "left"
                        isOpen = state.SideMenuIsOpen
                        toggle = (fun _ -> ToggleMenu |> dispatch)
                        menuClick = SideMenuClick >> dispatch
                        items =  state.SideMenuItems
                    |})
                }
            </React.Fragment>
            <Container sx={ {| height="87%"; mt= 4 |} } >
                <Stack sx={ {| height="100%" |} }>
                    <Box sx={ {| flexBasis=1 |} } >
                        { Views.Patient.View({| patient = props.patient; updatePatient = props.updatePatient |}) }
                    </Box>
                    <Box sx={ {| maxHeight = "80%"; mt=4; overflowY="auto" |} }>
                        {
                            match state.CurrentPage with
                            | Global.Pages.LifeSupport ->
                                Views.EmergencyList.View ({| interventions = props.bolusMedication |})
                            | Global.Pages.ContinuousMeds ->
                                Views.ContinuousMeds.View ({| interventions = props.continuousMedication |})
                            | Global.Pages.Prescribe ->
                                Views.Prescribe.View ({| scenarios = props.scenario; updateScenario = props.updateScenario |})
                            | _ -> notFound
                        }
                    </Box>
                </Stack>
            </Container>
        </React.Fragment>
        """