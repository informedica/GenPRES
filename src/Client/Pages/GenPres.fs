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
                SideMenuItems: (JSX.Element option * string * bool) []
                SideMenuIsOpen: bool
                Configuration: Configuration Option
            }


        type Msg =
            | SideMenuClick of string
            | ToggleMenu


        let pages =
            [
                LifeSupport
                ContinuousMeds
                Prescribe
                Formulary
                Parenteralia
            ]


        let init lang terms page : State * Cmd<Msg> =

            let state =
                {
                    SideMenuItems =
                        pages
                        |> List.toArray
                        |> Array.map (fun p ->
                            let b = p = page
                            match p |> pageToString terms lang with
                            | s when p = LifeSupport -> (Mui.Icons.FireExtinguisher |> Some), s, b
                            | s when p = ContinuousMeds -> (Mui.Icons.Vaccines |> Some), s, b
                            | s when p = Prescribe -> (Mui.Icons.Message |> Some), s, b
                            | s when p = Formulary -> (Mui.Icons.LocalPharmacy |> Some), s, b
                            | s when p = Parenteralia -> (Mui.Icons.Bloodtype |> Some), s, b
                            | s -> None, s, b
                        )

                    SideMenuIsOpen = false
                    Configuration = None
                }

            state, Cmd.none


        let update lang terms updatePage (msg: Msg) (state: State) =
            match msg with

            | ToggleMenu ->
                { state with
                    SideMenuIsOpen = not state.SideMenuIsOpen
                },
                Cmd.none

            | SideMenuClick s ->
                pages
                |> List.map (fun p -> p |> pageToString terms lang, p)
                |> List.tryFind (fst >> ((=) s))
                |> Option.map snd
                |> Option.defaultValue LifeSupport
                |> updatePage

                { state with

                    SideMenuItems =
                        state.SideMenuItems
                        |> Array.map (fun (icon, item, _) ->
                            if item = s then
                                (icon, item, true)
                            else
                                (icon, item, false)
                        )
                },
                Cmd.none


    open Elmish



    [<JSX.Component>]
    let View
        (props: {|
            showDisclaimer: bool
            isDemo: bool
            acceptDisclaimer: bool -> unit
            patient: Patient option
            updatePatient: Patient option -> unit
            updatePage: Global.Pages -> unit
            bolusMedication: Deferred<Intervention list>
            continuousMedication: Deferred<Intervention list>
            products: Deferred<Product list>
            scenarioResult: Deferred<ScenarioResult>
            updateScenario : ScenarioResult -> unit
            selectOrder : (Scenario * Order option) -> unit
            order: Deferred<(bool * string option * Order) option>
            intake : Deferred<Intake>
            loadOrder: (string option * Order) -> unit
            updateScenarioOrder : unit -> unit
            formulary: Deferred<Formulary>
            updateFormulary : Formulary -> unit
            parenteralia : Deferred<Parenteralia>
            updateParenteralia : Parenteralia -> unit
            page : Global.Pages
            localizationTerms : Deferred<string[][]>
            languages : Localization.Locales []
            hospitals : Deferred<string []>
            switchLang : Localization.Locales -> unit
            switchHosp : string -> unit |}) =

        let context = React.useContext(Global.context)
        let lang = context.Localization
        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        let deps =
            [|
                box props.page
                box props.updatePage
                box lang
                box props.scenarioResult
            |]
        let state, dispatch = React.useElmish (init lang props.localizationTerms props.page, update lang props.localizationTerms props.updatePage, deps)

        let notFound =
            JSX.jsx
                $"""
            <React.Fragment>
                <Typography>
                    Nog niet geimplementeerd
                </Typography>
            </React.Fragment>
            """

        let modalStyle =
            {|
                position="absolute"
                top= "50%"
                left= "50%"
                transform= "translate(-50%, -50%)"
                width= 400
                bgcolor= "background.paper"
                boxShadow= 24
            |}

        let sxPageBox =
            {|
                mt= 3
                (*
                mb =
                    match props.page with
                    | Global.Pages.Prescribe -> 26 |> box
                    | _ -> 0 |> box
                *)
                overflowY =
                    match props.page with
                    | Global.Pages.Prescribe
                    | Global.Pages.Parenteralia
                    | Global.Pages.Formulary -> "auto"
                    | _ when not isMobile -> "hidden"
                    | _ -> "auto"
            |}

        JSX.jsx
            $"""
        import {{ ThemeProvider }} from '@mui/material/styles';
        import CssBaseline from '@mui/material/CssBaseline';
        import React from "react";
        import Stack from '@mui/material/Stack';
        import Box from '@mui/material/Box';
        import Container from '@mui/material/Container';
        import Typography from '@mui/material/Typography';
        import Modal from '@mui/material/Modal';

        <React.Fragment>
            <Box>
                {Components.TitleBar.View({|
                    title =
                        let s = $"GenPRES 2023 {props.page |> (Global.pageToString props.localizationTerms lang)}"
                        if props.isDemo then $"{s} - DEMO VERSION!" else s

                    toggleSideMenu = fun _ -> ToggleMenu |> dispatch
                    languages = props.languages
                    hospitals = props.hospitals
                    switchLang = props.switchLang
                    switchHosp = props.switchHosp
                |})}
            </Box>
            <React.Fragment>
                {
                    Components.SideMenu.View({|
                        anchor = "left"
                        isOpen = state.SideMenuIsOpen
                        toggle = (fun _ -> ToggleMenu |> dispatch)
                        menuClick = SideMenuClick >> dispatch
                        items =  state.SideMenuItems
                    |})
                }
            </React.Fragment>
            <Container id="page-container" sx={ {| height="87%"; mt= 3 |} } >
                <Stack sx={ {| height="100%" |} }>
                    <Box sx={ {| flexBasis=1 |} } >
                        {
                            Views.Patient.View({|
                                patient = props.patient
                                updatePatient = props.updatePatient
                                localizationTerms = props.localizationTerms
                            |})
                        }
                    </Box>
                    <Box id="page-box" sx={sxPageBox}>
                        {
                            match props.page with
                            | Global.Pages.LifeSupport ->
                                Views.EmergencyList.View ({|
                                    interventions = props.bolusMedication
                                    localizationTerms = props.localizationTerms
                                |})
                            | Global.Pages.ContinuousMeds ->
                                Views.ContinuousMeds.View ({|
                                    interventions = props.continuousMedication
                                    localizationTerms = props.localizationTerms
                                |})
                            | Global.Pages.Prescribe ->
                                Views.Prescribe.View ({|
                                    order = props.order
                                    scenarios = props.scenarioResult
                                    updateScenario = props.updateScenario
                                    selectOrder = props.selectOrder
                                    loadOrder = props.loadOrder
                                    updateScenarioOrder = props.updateScenarioOrder
                                    localizationTerms = props.localizationTerms
                                |})
                            | Global.Pages.Formulary ->
                                Views.Formulary.View ({|
                                    formulary = props.formulary
                                    updateFormulary = props.updateFormulary
                                    localizationTerms = props.localizationTerms
                                |})
                            | Global.Pages.Parenteralia ->
                                Views.Parenteralia.View ({|
                                    parenteralia = props.parenteralia
                                    updateParenteralia = props.updateParenteralia
                                |})
                        }
                    </Box>
                    <Box>
                        {
                            match props.page with
                            | Global.Pages.Prescribe ->
                                Views.Intake.View props.intake
                            | _ -> JSX.jsx "<></>"
                        }
                    </Box>
                </Stack>
            </Container>
            <Modal open={props.showDisclaimer} onClose={fun () -> ()} >
                <Box sx={modalStyle}>
                    {
                        Views.Disclaimer.View {|
                            accept = props.acceptDisclaimer
                            languages = props.languages
                            switchLang = props.switchLang
                            localizationTerms = props.localizationTerms
                        |}
                    }
                </Box>
            </Modal>

        </React.Fragment>
        """