module GenPres

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types



module private Api =

    open Fable.Remoting.Client

    let serverApi =
        Remoting.createApi ()
        |> Remoting.withRouteBuilder
            Shared.Api.routerPaths
        |> Remoting.buildProxy<Shared.Api.IServerApi>




module private Components =

    open Elmish
    open Fable.Core.JsInterop


    module private Select =


        type State = string option


        type Msg = | Select of string | Clear


        let init s : State * Cmd<Msg> = s, Cmd.none


        let update dispatch (msg : Msg) _ : State * Cmd<Msg> =
            printfn "handle change is called"
            match msg with 
            | Clear    -> None, Cmd.none
            | Select s -> Some s, Cmd.none
            |> fun (s, c) -> 
                s |> dispatch
                s, c


    let Select (props : {| label : string; selected : string option; values : (int *string) []; dispatch : string option -> unit |}) =
        let state, dispatch = React.useElmish(Select.init props.selected, Select.update props.dispatch, [||])

        let handleChange = 
            fun ev ->
                let value = ev?target?value

                value 
                |> string
                |> Select.Select
                |> dispatch

        let clear = fun _ -> Select.Clear |> dispatch

        let items = 
            props.values
            |> Array.map (fun (k, v) ->
                JSX.jsx 
                    $"""
                <MenuItem key={k} value={k}>{v}</MenuItem>
                """
            )

        let clearButton =
            JSX.jsx 
                $"""
            import ClearIcon from '@mui/icons-material/Clear';
            import IconButton from "@mui/material/IconButton";

            <IconButton sx={ {| visibility = if state |> Option.isNone then "hidden" else "visible" |} } onClick={clear}>
                <ClearIcon/>
            </IconButton>
            """

        JSX.jsx 
            $"""
        import * as React from 'react';
        import InputLabel from '@mui/material/InputLabel';
        import MenuItem from '@mui/material/MenuItem';
        import FormControl from '@mui/material/FormControl';
        import Select from '@mui/material/Select';
        
        <div>
        <FormControl variant="standard" sx={ {| m = 1; minWidth = 120 |} }>
            <InputLabel id="demo-simple-select-standard-label">{props.label}</InputLabel>
            <Select
            labelId="demo-simple-select-standard-label"
            id="demo-simple-select-standard"
            value={state |> Option.defaultValue ""}
            onChange={handleChange}
            label={props.label}
            sx={ {| ``& .MuiSelect-icon`` = {| visibility = if state |> Option.isNone then "visible" else "hidden" |} |} }
            endAdornment={clearButton}
            >
                {items}
            </Select>
        </FormControl>
        </div>
        """



    [<JSX.Component>]
    let List (prop : {| items : string[] |}) =
        let items =
            prop.items
            |> Array.map (fun text ->
                JSX.jsx
                    $"""
                <ListItem key={text} disablePadding>
                    <ListItemButton>
                    <ListItemText primary={text} />
                    </ListItemButton>
                </ListItem>
                """
            )

        JSX.jsx
            $"""
        import List from '@mui/material/List';
        import Divider from '@mui/material/Divider';
        import ListItem from '@mui/material/ListItem';
        import ListItemButton from '@mui/material/ListItemButton';
        import ListItemIcon from '@mui/material/ListItemIcon';
        import ListItemText from '@mui/material/ListItemText';

        <List>
            {items}
        </List>
        """


    [<JSX.Component>]
    let AppBar (props: {| title: string; onMenuClick : unit -> unit |}) =
        JSX.jsx
            $"""
        import AppBar from '@mui/material/AppBar';
        import Box from '@mui/material/Box';
        import Toolbar from '@mui/material/Toolbar';
        import Typography from '@mui/material/Typography';
        import Button from '@mui/material/Button';
        import IconButton from '@mui/material/IconButton';
        import MenuIcon from '@mui/icons-material/Menu';

        <Box sx={ {| flexGrow = 1 |} }>
            <AppBar position="static">
                <Toolbar>
                    <IconButton
                        size="large"
                        edge="start"
                        color="inherit"
                        aria-label="menu"
                        sx={ {| mr = 2 |} }
                        onClick={props.onMenuClick}
                        >
                        <MenuIcon />

                    </IconButton>
                    <Typography variant="h6" component="div" sx={ {| flexGrow = 1 |} }>
                        {props.title}
                    </Typography>
                    <Button color="inherit">Login</Button>
                </Toolbar>
            </AppBar>
        </Box>
        """


    [<JSX.Component>]
    let SideMenu (props : {| anchor : string; isOpen : bool; toggle : unit -> unit |}) =
        let drawerWidth = 240

        let menu =
            {|
                items = [|
                    "Noodlijst"
                    "Continue Medicatie"
                    "Voorschrijven"
                    "Voeding"
                    "Formularium"
                |]
            |}
            |> List

        JSX.jsx
            $"""
        import Drawer from '@mui/material/Drawer';
        import Typography from '@mui/material/Typography';

        <div>
            <Drawer
                anchor={props.anchor}
                width={drawerWidth}
                open={props.isOpen}
                onClose={props.toggle}
            >
            {menu}
            </Drawer>
        </div>
        """



module private Views =

    open Elmish


    module private Patient =

        open Shared.Types
        module Patient = Shared.Patient
        
        type State = Patient option


        type Msg = 
            | Clear    
            | UpdateYear of string option
            | UpdateMonth of string option
            | UpdateWeek of string option
            | UpdateDay of string option
            | UpdateWeight of string option
            | UpdateHeight of string option


        let tryParse (s : string) = match Int32.TryParse(s) with | false, _ -> None | true, v -> v |> Some


        let init pat : State * Cmd<Msg> = pat, Cmd.none 


        let setYear s (p : Patient option) = 
            match p with 
            | None -> 
                Patient.create
                    (s |> Option.bind tryParse)
                    None None None None None
            | Some p -> 
                Patient.create
                    (s |> Option.bind tryParse)
                    (p |> Patient.getAgeMonths)
                    (p |> Patient.getAgeWeeks)
                    (p |> Patient.getAgeDays)
                    (p |> Patient.getWeight)
                    (p |> Patient.getHeight)

        let setMonth s (p : Patient option) = 
            match p with 
            | None -> 
                Patient.create
                    None
                    (s |> Option.bind tryParse)
                    None None None None
            | Some p -> 
                Patient.create
                    (p |> Patient.getAgeYears)
                    (s |> Option.bind tryParse)
                    (p |> Patient.getAgeWeeks)
                    (p |> Patient.getAgeDays)
                    (p |> Patient.getWeight)
                    (p |> Patient.getHeight)

        let setWeek s (p : Patient option) = 
            match p with 
            | None -> 
                Patient.create
                    None None
                    (s |> Option.bind tryParse)
                    None None None
            | Some p -> 
                Patient.create
                    (p |> Patient.getAgeYears)
                    (p |> Patient.getAgeMonths)
                    (s |> Option.bind tryParse)
                    (p |> Patient.getAgeDays)
                    (p |> Patient.getWeight)
                    (p |> Patient.getHeight)

        let setDay s (p : Patient option) = 
            match p with 
            | None -> 
                Patient.create
                    None None None
                    (s |> Option.bind tryParse)
                    None None
            | Some p -> 
                Patient.create
                    (p |> Patient.getAgeYears)
                    (p |> Patient.getAgeMonths)
                    (p |> Patient.getAgeWeeks)
                    (s |> Option.bind tryParse)
                    (p |> Patient.getWeight)
                    (p |> Patient.getHeight)

        let setWeight s (p : Patient option) = 
            match p with 
            | None -> 
                Patient.create
                    None None None None
                    (s |> Option.bind tryParse |> Option.map (fun v -> (v |> float) / 1000.))
                    None
            | Some p -> 
                Patient.create
                    (p |> Patient.getAgeYears)
                    (p |> Patient.getAgeMonths)
                    (p |> Patient.getAgeWeeks)
                    (p |> Patient.getAgeDays)
                    (s |> Option.bind tryParse |> Option.map (fun v -> (v |> float) / 1000.))
                    (p |> Patient.getHeight)

        let setHeight s (p : Patient option) = 
            match p with 
            | None -> 
                Patient.create
                    None None None None None
                    (s |> Option.bind tryParse |> Option.map float)
            | Some p -> 
                Patient.create
                    (p |> Patient.getAgeYears)
                    (p |> Patient.getAgeMonths)
                    (p |> Patient.getAgeWeeks)
                    (p |> Patient.getAgeDays)
                    (p |> Patient.getWeight)
                    (s |> Option.bind tryParse |> Option.map float)


        let update dispatch msg (state : State) : State * Cmd<Msg> =
            printfn "update was called"
            match msg with
            | Clear    -> None, Cmd.none
            | UpdateYear s -> state |> setYear s, Cmd.none
            | UpdateMonth s -> state |> setMonth s, Cmd.none
            | UpdateWeek s -> state |> setWeek s, Cmd.none
            | UpdateDay s -> state |> setDay s, Cmd.none
            | UpdateWeight s -> state |> setWeight s, Cmd.none
            | UpdateHeight s -> state |> setHeight s, Cmd.none
            |> fun (state, cmd) ->
                state |> dispatch
                state, cmd


        let show pat =
            match pat with
            | Some p -> p |> Shared.Patient.toString Shared.Localization.Dutch true
            | None -> "Voer patient gegevens in"


    [<JSX.Component>]
    let Patient (props : {| patient : Shared.Types.Patient option; dispatch : Shared.Types.Patient option -> unit |}) =
        let isExpanded, setExpanded = React.useState true
        let pat, dispatch = React.useElmish(Patient.init props.patient, Patient.update props.dispatch, [| |])

        let handleChange = fun _ -> isExpanded |> not |> setExpanded

        let createSelect label changeValue vs =
            Components.Select({| 
                label = label
                selected = None
                values = vs
                dispatch = changeValue
            |})

        let wghts =
            [|21000..1000..100000|]
            |> Array.append [|10500..500..20000|]
            |> Array.append [|2000..100..10000|] 
        
        JSX.jsx
            $"""
        import * as React from 'react';
        import Stack from '@mui/material/Stack';
        import Accordion from '@mui/material/Accordion';
        import AccordionDetails from '@mui/material/AccordionDetails';
        import AccordionSummary from '@mui/material/AccordionSummary';
        import Typography from '@mui/material/Typography';
        import ExpandMoreIcon from '@mui/icons-material/ExpandMore';

        <div>
        <Accordion expanded={isExpanded} onChange={handleChange}>
            <AccordionSummary
            expandIcon={{ <ExpandMoreIcon /> }}
            aria-controls="panel1bh-content"
            id="panel1bh-header"
            >
            { pat|> Patient.show }
            </AccordionSummary>
            <AccordionDetails>
                <Stack spacing={3}>
                    <Stack direction={ {| sm = "column"; md = "row"  |} } spacing={3}>
                        {[|0..19|] 
                        |> Array.map (fun k -> k, if k > 18 then "> 18" else $"{k}") 
                        |> createSelect "jaren" (Patient.UpdateYear >> dispatch)}
                        
                        {[|1..11|] 
                        |> Array.map (fun k -> k, $"{k}") 
                        |> createSelect "maanden" (Patient.UpdateMonth >> dispatch)}
                        
                        {[|1..3|] 
                        |> Array.map (fun k -> k, $"{k}") 
                        |> createSelect "weken" (Patient.UpdateWeek >> dispatch)}
                        
                        {[|1..6|] 
                        |> Array.map (fun k -> k, $"{k}") 
                        |> createSelect "dagen" (Patient.UpdateDay >> dispatch)}
                        
                        { wghts 
                        |> Array.map (fun k -> k, $"{(k |> float)/1000.}") 
                        |> createSelect "gewicht (kg)"(Patient.UpdateWeight >> dispatch)}
                        
                        {[|40..220|] 
                        |> Array.map (fun k -> k, $"{k}") 
                        |> createSelect "lengte (cm)" (Patient.UpdateHeight >> dispatch)}
                    </Stack>
                    <Button variant="contained" >
                        Verwijderen
                    </Button>
                </Stack>
            </AccordionDetails>
        </Accordion>
        </div>
        """




module private Elmish =

    open Elmish
    open Shared

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


    let update lang updateLang (msg: Msg) (state: State) =
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
        

        | LanguageChange s ->
            //TODO: doesn't work anymore
            state, Cmd.ofEffect (fun _ -> s |> Localization.fromString |> updateLang)



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
               scenarios: Deferred<ScenarioResult>
               updateScenarios : ScenarioResult -> unit |}) =

    let init = Elmish.init Localization.Dutch
    let update = Elmish.update Localization.Dutch props.updateLang

    let model, dispatch = React.useElmish (init, update, [| box props.patient |])

    JSX.jsx
        $"""
    import {{ ThemeProvider}} from '@mui/material/styles';
    import CssBaseline from '@mui/material/CssBaseline';
    import React from "react";
    import Box from '@mui/material/Box';
    import Container from '@mui/material/Container';

    <React.StrictMode>
        <ThemeProvider theme={theme}>
            <Box height="100vh">
                <CssBaseline />
                <Box>
                    {Components.AppBar ({| title = "GenPRES 2023"; onMenuClick = (fun _ -> "" |> SideMenuClick |> dispatch) |})}
                </Box>
                {Components.SideMenu ({| anchor = "left"; isOpen = model.SideMenuIsOpen; toggle = fun _ -> ToggleMenu |> dispatch |})}
                <Container sx={ {| mt= 5 |} } >
                    { Views.Patient ({| patient = props.patient; dispatch = props.updatePatient |}) }
                </Container>
            </Box>
        </ThemeProvider>
    </React.StrictMode>
    """