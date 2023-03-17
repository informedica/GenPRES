module Home

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types


open Elmish
open Shared


[<Literal>]
let private themeDef = """createTheme({
})"""

[<Import("createTheme", from="@mui/material/styles")>]
[<Emit(themeDef)>]
let private theme : obj = jsNative



[<JSX.Component>]
let GenPres
    (props: {| updateLang: Localization.Locales -> unit
               patient: Patient option
               updatePatient: Patient option -> unit
               bolusMedication: Deferred<Intervention list>
               continuousMedication: Deferred<Intervention list>
               products: Deferred<Product list>
               scenario: Deferred<ScenarioResult>
               updateScenario : ScenarioResult -> unit
               toggleMenu : unit -> unit
               currentPage : Global.Pages option
               sideMenuIsOpen : bool
               sideMenuClick : string -> unit
               sideMenuItems : (string * bool) array |}) =

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

    <React.StrictMode>
        <ThemeProvider theme={theme}>
            <Box sx={ {| height= "100vh"; overflowY = "hidden" |} }>
                <CssBaseline />
                <Box>
                    {Components.AppBar.View({|
                        title = $"GenPRES 2023 {props.currentPage |> Option.map (Global.pageToString Localization.Dutch)}"
                        toggleSideMenu = fun _ -> props.toggleMenu ()
                    |})}
                </Box>
                {Components.SideBar.View({|
                        anchor = "left"
                        isOpen = props.sideMenuIsOpen
                        toggle = props.toggleMenu
                        menuClick = props.sideMenuClick
                        items =  props.sideMenuItems
                    |})}
                <Container sx={ {| height="87%"; mt= 4 |} } >
                    <Stack sx={ {| height="100%" |} }>
                    <Box sx={ {| flexBasis=1 |} } >
                        { Views.Patient.View({| patient = props.patient; updatePatient = props.updatePatient |}) }
                    </Box>
                    <Box sx={ {| maxHeight = "80%"; mt=4; overflowY="auto" |} }>
                        {
                            match props.currentPage with
                            | Some Global.Pages.LifeSupport ->
                                Views.EmergencyList.View ({| interventions = props.bolusMedication |})
                            | Some Global.Pages.ContinuousMeds ->
                                Views.ContinuousMeds.View ({| interventions = props.continuousMedication |})
                            | Some Global.Pages.Prescribe ->
                                Views.Prescribe.View ({| scenarios = props.scenario; updateScenario = props.updateScenario |})
                            | _ -> notFound
                        }
                    </Box>
                    </Stack>
                </Container>
            </Box>
        </ThemeProvider>
    </React.StrictMode>
    """