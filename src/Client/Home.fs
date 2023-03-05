module Home

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


    module ElmishSelect =


        type State = string option


        type Msg = | Select of string | Clear


        let init s : State * Cmd<Msg> = s, Cmd.none


        let update updateSelected (msg : Msg) _ : State * Cmd<Msg> =
            printfn "handle change is called"
            match msg with
            | Clear    -> None, Cmd.none
            | Select s -> Some s, Cmd.none
            |> fun (s, c) ->
                s |> updateSelected
                s, Cmd.none


    [<JSX.Component>]
    let ElmishSelect (props :
            {|
                label : string
                selected : string option
                values : (int *string) []
                updateSelected : string option -> unit
            |}
        ) =
        let depArr = [||] //[| box props.dispatch |]
        let state, dispatch = React.useElmish(ElmishSelect.init props.selected, ElmishSelect.update props.updateSelected, depArr)

        let handleChange =
            fun ev ->
                let value = ev?target?value

                value
                |> string
                |> ElmishSelect.Select
                |> dispatch

        let clear = fun _ -> ElmishSelect.Clear |> dispatch

        let items =
            props.values
            |> Array.map (fun (k, v) ->
                JSX.jsx
                    $"""
                <MenuItem key={k} value={k}>{v}</MenuItem>
                """
            )

        let isClear = state |> Option.defaultValue "" |> String.IsNullOrWhiteSpace

        let clearButton =
            JSX.jsx
                $"""
            import ClearIcon from '@mui/icons-material/Clear';
            import IconButton from "@mui/material/IconButton";

            <IconButton sx={ {| visibility = if isClear then "hidden" else "visible" |} } onClick={clear}>
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
            sx={ {| ``& .MuiSelect-icon`` = {| visibility = if isClear then "visible" else "hidden" |} |} }
            endAdornment={clearButton}
            >
                {items}
            </Select>
        </FormControl>
        </div>
        """


    [<JSX.Component>]
    let StateSelect (props :
            {|
                label : string
                selected : string option
                values : (int *string) []
                updateSelected : string option -> unit
            |}
        ) =
        let state, setState = React.useState props.selected

        let applyState v =
            v |> setState
            v |> props.updateSelected

        let handleChange =
            fun ev ->
                ev?target?value
                |> string
                |> function
                | s when s |> String.IsNullOrWhiteSpace -> None
                | s -> s |> Some
                |> applyState

        let clear =
            fun _ -> None |> applyState

        let items =
            props.values
            |> Array.map (fun (k, v) ->
                JSX.jsx
                    $"""
                <MenuItem key={k} value={v}>{v}</MenuItem>
                """
            )

        let isClear = state |> Option.defaultValue "" |> String.IsNullOrWhiteSpace

        let clearButton =
            JSX.jsx
                $"""
            import ClearIcon from '@mui/icons-material/Clear';
            import IconButton from "@mui/material/IconButton";

            <IconButton sx={ {| visibility = if isClear then "hidden" else "visible" |} } onClick={clear}>
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
            sx={ {| ``& .MuiSelect-icon`` = {| visibility = if isClear then "visible" else "hidden" |} |} }
            endAdornment={clearButton}
            >
                {items}
            </Select>
        </FormControl>
        </div>
        """


    [<JSX.Component>]
    let SimpleSelect (props :
            {|
                label : string
                selected : string option
                values : (string * string) []
                updateSelected : string option -> unit
            |}
        ) =

        let handleChange =
            fun ev ->
                let value = ev?target?value

                value
                |> string
                |> function
                | s when s |> String.IsNullOrWhiteSpace -> None
                | s -> s |> Some
                |> props.updateSelected

        let clear = fun _ -> None |> props.updateSelected

        let items =
            props.values
            |> Array.mapi (fun i (k, v) ->
                JSX.jsx
                    $"""
                <MenuItem key={i} value={k} sx = { {| maxWidth = 300 |} }>
                    {v}
                </MenuItem>
                """
            )

        let isClear = props.selected |> Option.defaultValue "" |> String.IsNullOrWhiteSpace

        let clearButton =
            JSX.jsx
                $"""
            import ClearIcon from '@mui/icons-material/Clear';
            import IconButton from "@mui/material/IconButton";

            <IconButton sx={ {| visibility = if isClear then "hidden" else "visible" |} } onClick={clear}>
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
        <FormControl variant="standard" sx={ {| m = 1; minWidth = 120; maxWidth = 200 |} }>
            <InputLabel id="demo-simple-select-standard-label">{props.label}</InputLabel>
            <Select
            labelId="demo-simple-select-standard-label"
            id="demo-simple-select-standard"
            value={props.selected |> Option.defaultValue ""}
            onChange={handleChange}
            label={props.label}
            sx={ {| ``& .MuiSelect-icon`` = {| visibility = if isClear then "visible" else "hidden" |} |} }
            endAdornment={clearButton}
            >
                {items}
            </Select>
        </FormControl>
        </div>
        """


    [<JSX.Component>]
    let List (props : {| updateSelected : string -> unit; items : (string * bool)[] |}) =
        let items =
            props.items
            |> Array.mapi (fun i (text, selected) ->
                JSX.jsx
                    $"""
                <React.Fragment key={i} >
                    <ListItem value={text} >
                        <ListItemButton selected={selected} onClick={fun _ -> text |> props.updateSelected}>
                        <ListItemText primary={text} />
                        </ListItemButton>
                    </ListItem>
                </React.Fragment>
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
    let AppBar (props: {| title: string; toggleSideMenu : unit -> unit |}) =
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
                        onClick={props.toggleSideMenu}
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
    let SideMenu (props :
            {|
                anchor : string
                isOpen : bool
                toggle : unit -> unit
                menuClick : string -> unit
                items : (string * bool)[]
            |}
        ) =
        let drawerWidth = 240

        let menu =
            {|
                updateSelected = props.menuClick
                items = props.items
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


    module ResponsiveTable =


        [<JSX.Component>]
        let CardRows (props : 
            {| 
                columns : {|  field : string; headerName : string; width : int; filterable : bool; sortable : bool |}[]
                rows : (string * string) [][]
            |}) =
            let cards =
                props.rows
                |> Array.map (fun row ->
                    let content = 
                        row 
                        |> Array.skip 1
                        |> Array.map (fun (n, s) ->
                            if String.IsNullOrWhiteSpace(s) then JSX.jsx "<></>"
                            else
                                let n = $"{n}: "
                                JSX.jsx 
                                    $"""
                                <Stack direction="row" spacing={2} sx={ {| ``align-items``="center" |} } >
                                    <Typography variant="body2">
                                        {n}
                                    </Typography>
                                    <Typography variant="subtitle1">
                                        {s}
                                    </Typography>
                                </Stack>
                                """
//                            |> toReact
                        )

                    JSX.jsx 
                        $"""
                    import Card from '@mui/material/Card';
                    import CardActions from '@mui/material/CardActions';
                    import CardContent from '@mui/material/CardContent';
                    import Button from '@mui/material/Button';
                    import Typography from '@mui/material/Typography';                    

                    <Grid item width={380}>
                        <Card raised={true} >
                            <CardContent>
                                {content}
                            </CardContent>
                        </Card>
                    </Grid>
                    """
                )
        
            JSX.jsx
                $"""
            import Grid from '@mui/material/Grid';

            <Box sx={ {| p=2; display="flex"; flexDirection="column"; mt=5; overflowY="scroll"; height=800 |} }>
                <Grid container rowSpacing={4} columnSpacing={ {| xs=1; sm=2; md=3 |} } >
                    {cards}
                </Grid>            
            </Box>
            """


    [<JSX.Component>]
    let ResponsiveTable (props : 
        {| 
            columns : {|  field : string; headerName : string; width : int; filterable : bool; sortable : bool |}[]
            rows : (string * string) [][]
            rowCreate : string[] -> obj
        |}) =

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"    

        if isMobile then
            {| columns = props.columns; rows = props.rows |} 
            |> ResponsiveTable.CardRows 
        else
            let rows = 
                props.rows 
                |> Array.map (Array.map fst)
                |> Array.map props.rowCreate 

            JSX.jsx
                $"""
            import * as React from 'react';
            import {{DataGrid}} from '@mui/x-data-grid';

            <Box sx={ {| height=600; width="100%"; mt=2; mb=2 |} }>
                <DataGrid
                    rows={rows}
                    columns=
                        {
                            props.columns
                            |> Array.map (fun c ->
                                match c.headerName with
                                | s when s = "id" -> {| c with hide = true |} |> box
                                | _ -> c |> box
                            )
                        }
                    pageSize={100}
                    autoPageSize={true}
                />
            </Box>
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
            | Clear          -> None, Cmd.none
            | UpdateYear s   -> state |> setYear s, Cmd.none
            | UpdateMonth s  -> state |> setMonth s, Cmd.none
            | UpdateWeek s   -> state |> setWeek s, Cmd.none
            | UpdateDay s    -> state |> setDay s, Cmd.none
            | UpdateWeight s -> state |> setWeight s, Cmd.none
            | UpdateHeight s -> state |> setHeight s, Cmd.none
            |> fun (state, cmd) ->
                state |> dispatch
                state, cmd


        let show pat =
            match pat with
            | Some p ->
                p
                |> Patient.toString Shared.Localization.Dutch true
                //TODO: use markdown
                |> fun s -> s.Replace("*", "")
            | None -> "Voer patient gegevens in"


    [<JSX.Component>]
    let Patient (props :
            {|
                patient : Shared.Types.Patient option
                updatePatient : Shared.Types.Patient option -> unit
            |}
        ) =
        let isExpanded, setExpanded = React.useState true
        let depArr = [| box props.updatePatient |]
        let pat, dispatch =
            React.useElmish(
                    Patient.init props.patient,
                    Patient.update props.updatePatient,
                    depArr)

        let handleChange = fun _ -> isExpanded |> not |> setExpanded

        let createSelect label sel changeValue vs =
            Components.SimpleSelect({|
                label = label
                selected = sel |> Option.map string
                values = vs
                updateSelected = changeValue
            |})

        let wghts =
            [|21000..1000..100000|]
            |> Array.append [|10500..500..20000|]
            |> Array.append [|2000..100..10000|]

        let hghts = [|40..220|]

        let zeroToNone = function
            | Some v -> if v = 0 then None else v |> Some
            | None -> None

        let weightToNone = function
            | Some v -> wghts |> Array.tryFind ((=) (int (v * 1000.)))
            | None -> None

        let heightToNone = function
            | Some v -> hghts |> Array.tryFind ((=) (int v))
            | None -> None

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
                        |> Array.map (fun k -> $"{k}", if k > 18 then "> 18" else $"{k}")
                        |> createSelect
                            "jaren"
                            (pat |> Option.bind Shared.Patient.getAgeYears)
                            (Patient.UpdateYear >> dispatch)}

                        {[|1..11|]
                        |> Array.map (fun k -> $"{k}", $"{k}")
                        |> createSelect
                            "maanden"
                            (pat |> Option.bind Shared.Patient.getAgeMonths |> zeroToNone)
                            (Patient.UpdateMonth >> dispatch)}

                        {[|1..3|]
                        |> Array.map (fun k -> $"{k}", $"{k}")
                        |> createSelect
                            "weken"
                            (pat |> Option.bind Shared.Patient.getAgeWeeks |> zeroToNone)
                            (Patient.UpdateWeek >> dispatch)}

                        {[|1..6|]
                        |> Array.map (fun k -> $"{k}", $"{k}")
                        |> createSelect
                            "dagen"
                            (pat |> Option.bind Shared.Patient.getAgeDays |> zeroToNone)
                            (Patient.UpdateDay >> dispatch)}

                        { wghts
                        |> Array.map (fun k -> $"{k}", $"{(k |> float)/1000.}")
                        |> createSelect
                            "gewicht (kg)"
                            (pat |> Option.bind (Shared.Patient.getWeight >> weightToNone))
                            (Patient.UpdateWeight >> dispatch)}

                        {[|40..220|]
                        |> Array.map (fun k -> $"{k}", $"{k}")
                        |> createSelect
                            "lengte (cm)"
                            (pat |> Option.bind (Shared.Patient.getHeight >> heightToNone))
                            (Patient.UpdateHeight >> dispatch)}
                    </Stack>
                    <Button variant="contained" onClick={fun _ -> Patient.Clear |> dispatch}>
                        Verwijderen
                    </Button>
                </Stack>
            </AccordionDetails>
        </Accordion>
        </div>
        """


    [<JSX.Component>]
    let EmergencyList (props : {| interventions: Deferred<Shared.Types.Intervention list> |}) =

        let columns = [|
            {|  field = "id"; headerName = "id"; width = 0; filterable = false; sortable = false |}
            {|  field = "indication"; headerName = "Indicatie"; width = 200; filterable = true; sortable = true |}
            {|  field = "intervention"; headerName = "Interventie"; width = 200; filterable = true; sortable = true |}
            {|  field = "calculated"; headerName = "Berekend"; width = 200; filterable = false; sortable = false |}
            {|  field = "preparation"; headerName = "Bereiding"; width = 200; filterable = false; sortable = false |} //``type`` = "number"
            {|  field = "advice"; headerName = "Advice"; width = 200; filterable = false; sortable = false |}
        |]

        let rows =
            match props.interventions with
            | Resolved items ->
                items
                |> List.toArray
                |> Array.mapi (fun i m ->
                    {|
                        id = i + 1
                        indication = m.Indication
                        intervention = m.Name
                        calculated = m.SubstanceDoseText
                        preparation = m.InterventionDoseText
                        advice = m.Text
                    |}
                )
            | _ -> [||]

        JSX.jsx
            $"""
        import * as React from 'react';
        import {{DataGrid}} from '@mui/x-data-grid';

        <Box sx={ {| height=600; width="100%"; mt=2; mb=2 |} }>
            <DataGrid
                rows={rows}
                columns=
                    {
                        columns
                        |> Array.map (fun c ->
                            match c.headerName with
                            | s when s = "id" -> {| c with hide = true |} |> box
                            | _ -> c |> box
                        )
                    }
                pageSize={100}
                autoPageSize={true}
            />
        </Box>
        """



    [<JSX.Component>]
    let ResponsiveEmergencyList (props : {| interventions: Deferred<Shared.Types.Intervention list> |}) =

        let columns = [|
            {|  field = "id"; headerName = "id"; width = 0; filterable = false; sortable = false |}
            {|  field = "indication"; headerName = "Indicatie"; width = 200; filterable = true; sortable = true |}
            {|  field = "intervention"; headerName = "Interventie"; width = 200; filterable = true; sortable = true |}
            {|  field = "calculated"; headerName = "Berekend"; width = 200; filterable = false; sortable = false |}
            {|  field = "preparation"; headerName = "Bereiding"; width = 200; filterable = false; sortable = false |} //``type`` = "number"
            {|  field = "advice"; headerName = "Advice"; width = 200; filterable = false; sortable = false |}
        |]

        let rows =
            match props.interventions with
            | Resolved items ->
                items
                |> List.toArray
                |> Array.mapi (fun i m ->
                    [|
                        "id", $"{i + 1}"
                        "indication", $"{m.Indication}"
                        "intervention", $"{m.Name}"
                        "calculated", $"{m.SubstanceDoseText}"
                        "preparation", $"{m.InterventionDoseText}"
                        "advice", $"{m.Text}"
                    |]
                )
            | _ -> [||]

        let rowCreate (fields : string []) =
            if fields |> Array.length <> 6 then
                failwith $"cannot create row with {fields}"
            else
                {|
                    id = fields[0]
                    indication = fields[1]
                    intervention = fields[2]
                    calculated = fields[3]
                    preparation = fields[4]
                    advice = fields[5]
                |}
            |> box

        Components.ResponsiveTable({|
            columns = columns
            rows = rows 
            rowCreate = rowCreate
        |})



    [<JSX.Component>]
    let ContinuousMedication (props : {| interventions: Deferred<Shared.Types.Intervention list> |}) =


        let columns = [|
            {|  field = "id"; headerName = "id"; width = 0; filterable = false; sortable = false |}
            {|  field = "indication"; headerName = "Indicatie"; width = 200; filterable = true; sortable = true |}
            {|  field = "medication"; headerName = "Medicatie"; width = 200; filterable = true; sortable = true |}
            {|  field = "quantity"; headerName = "Hoeveelheid"; width = 150; filterable = false; sortable = false |}
            {|  field = "solution"; headerName = "Oplossing"; width = 150; filterable = false; sortable = false |} //``type`` = "number"
            {|  field = "dose"; headerName = "Dosering"; width = 230; filterable = false; sortable = false |} //``type`` = "number"
            {|  field = "advice"; headerName = "Advies"; width = 190; filterable = false; sortable = false |}
        |]

        let rows =
            match props.interventions with
            | Resolved items ->
                items
                |> List.toArray
                |> Array.mapi (fun i m ->
                    {|
                        id = i + 1
                        indication = m.Indication
                        medication = m.Name
                        quantity = $"{m.Quantity} {m.QuantityUnit}"
                        solution =  $"{m.Total} ml {m.Solution}"
                        dose = m.SubstanceDoseText
                        advice = m.Text
                    |}
                )
            | _ -> [||]

        JSX.jsx
            $"""
        import * as React from 'react';
        import {{DataGrid}} from '@mui/x-data-grid';

        <Box sx={ {| height=600; width="100%"; mt=2; mb=2 |} }>
            <DataGrid
                rows={rows}
                columns=
                    {
                        columns
                        |> Array.map (fun c ->
                            match c.headerName with
                            | s when s = "id" -> {| c with hide = true |} |> box
                            | _ -> c |> box
                        )
                    }
                pageSize={100}
                autoPageSize={true}
            />
        </Box>
        """


    module Prescribe =

        open Feliz
        open Feliz.UseElmish
        open Elmish
        open Shared
        open Utils
        open FSharp.Core


        type State =
            {
                Dialog: string list
                Indication: string option
                Medication: string option
                Route: string option
            }


        type Msg =
            | RowClick of int * string list
            | CloseDialog
            | IndicationChange of string option
            | MedicationChange of string option
            | RouteChange of string option


        let init (scenarios: Deferred<ScenarioResult>) =
            let state =
                match scenarios with
                | Resolved sc ->
                    {
                        Dialog = []
                        Indication = sc.Indication
                        Medication = sc.Medication
                        Route = sc.Route
                    }
                | _ ->
                    {
                        Dialog = []
                        Indication = None
                        Medication = None
                        Route = None
                    }
            state, Cmd.none


        let update
            (scenarios: Deferred<ScenarioResult>)
            updateScenario
            (msg: Msg)
            state
            =
            match msg with
            | RowClick (i, xs) ->
                Logging.log "rowclick:" i
                { state with Dialog = xs }, Cmd.none

            | CloseDialog -> { state with Dialog = [] }, Cmd.none

            | IndicationChange s ->
                printfn $"indication change {s}"
                match scenarios with
                | Resolved sc ->
                    { sc with Indication = s }
                    |> updateScenario
                | _ -> ()

                { state with Indication = s }, Cmd.none

            | MedicationChange s ->
                match scenarios with
                | Resolved sc ->
                    { sc with Medication = s }
                    |> updateScenario
                | _ -> ()

                { state with Medication = s }, Cmd.none

            | RouteChange s ->
                match scenarios with
                | Resolved sc ->
                    { sc with Route = s }
                    |> updateScenario
                | _ -> ()

                { state with Route = s }, Cmd.none


    [<JSX.Component>]
    let Prescribe (props:
        {|
            scenarios: Deferred<Shared.Types.ScenarioResult>
            updateScenario: Shared.Types.ScenarioResult -> unit
        |}) =
        let state, dispatch =
            React.useElmish (
                Prescribe.init props.scenarios,
                Prescribe.update props.scenarios props.updateScenario,
                [| box props.scenarios; box props.updateScenario |]
            )

        let select lbl selected dispatch xs =
            Components.SimpleSelect ({|
                updateSelected = dispatch
                label = lbl
                selected = selected
                values = xs
            |})

        let progress =
            match props.scenarios with
            | Resolved _ -> JSX.jsx $"<></>"
            | _ ->
                JSX.jsx
                    $"""
                import CircularProgress from '@mui/material/CircularProgress';

                <Box sx={ {| mt = 5; display = "flex"; p = 20 |} }>
                    <CircularProgress />
                </Box>
                """

        let typoGraphy (s : string) =
            JSX.jsx
                $"""
            import Typography from '@mui/material/Typography';

            <Typography>{s}</Typography>
            """

        let displayScenarios med (sc : Shared.Types.Scenario) =
            if med |> Option.isNone then JSX.jsx $"""<>/</>"""
            else
                let med = med |> Option.defaultValue ""

                let list =
                    Components.List({|
                        updateSelected = ignore
                        items =
                            [|
                                $"{med} {sc.Shape}", false
                                $"{sc.Prescription}", false
                                $"{sc.Preparation}", false
                                $"{sc.Administration}", false
                            |]
                    |})

                JSX.jsx
                    $"""
                <Box>
                    {list}
                </Box>
                """

        let content =
            JSX.jsx
                $"""
            <CardContent>
                <Typography sx={ {| fontSize=14 |} } color="text.secondary" gutterBottom>
                Medicatie scenario's
                </Typography>
                <Stack direction="row" spacing={3} >
                    {
                        match props.scenarios with
                        | Resolved scrs -> scrs.Indication, scrs.Indications
                        | _ -> None, [||]
                        |> fun (sel, items) ->
                            items
                            |> Array.map (fun s -> s, s)
                            |> select "Indicaties" sel (Prescribe.IndicationChange >> dispatch)
                    }
                    {
                        match props.scenarios with
                        | Resolved scrs -> scrs.Medication, scrs.Medications
                        | _ -> None, [||]
                        |> fun (sel, items) ->
                            items
                            |> Array.map (fun s -> s, s)
                            |> select "Medicatie" sel (Prescribe.MedicationChange >> dispatch)
                    }
                    {
                        match props.scenarios with
                        | Resolved scrs -> scrs.Route, scrs.Routes
                        | _ -> None, [||]
                        |> fun (sel, items) ->
                            items
                            |> Array.map (fun s -> s, s)
                            |> select "Routes" sel (Prescribe.RouteChange >> dispatch)
                    }
                </Stack>
                <Stack direction="column" sx={ {| mt = 1 |} } >
                    {
                        match props.scenarios with
                        | Resolved sc ->
                            sc.Medication,
                            sc.Scenarios
                        | _ -> None, [||]
                        |> fun (med, scs) ->
                            scs
                            |> Array.map (displayScenarios med)
                    }
                </Stack>
            </CardContent>
            """

        JSX.jsx
            $"""
        import * as React from 'react';
        import Box from '@mui/material/Box';
        import Card from '@mui/material/Card';
        import CardActions from '@mui/material/CardActions';
        import CardContent from '@mui/material/CardContent';
        import Button from '@mui/material/Button';
        import Typography from '@mui/material/Typography';

        <Box sx={ {| mt=5 |} }>
            <Card sx={ {| minWidth = 275 |}  }>
                {content}
                {progress}
            <CardActions>
                <Button size="small">Bewerken</Button>
            </CardActions>
            </Card>
        </Box>
        """



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
    import Box from '@mui/material/Box';
    import Container from '@mui/material/Container';
    import Typography from '@mui/material/Typography';

    <React.StrictMode>
        <ThemeProvider theme={theme}>
            <Box sx={ {| height="100vh"; ``overflowY``="hidden" |} }>
                <CssBaseline />
                <Box>
                    {Components.AppBar ({|
                        title = $"GenPRES 2023 {props.currentPage |> Option.map (Global.pageToString Localization.Dutch)}"
                        toggleSideMenu = fun _ -> props.toggleMenu ()
                    |})}
                </Box>
                {Components.SideMenu ({|
                        anchor = "left"
                        isOpen = props.sideMenuIsOpen
                        toggle = props.toggleMenu
                        menuClick = props.sideMenuClick
                        items =  props.sideMenuItems
                    |})}
                <Container sx={ {| mt= 5 |} } >
                    <Stack>
                    { Views.Patient ({| patient = props.patient; updatePatient = props.updatePatient |}) }
                    {
                        match props.currentPage with
                        | Some Global.Pages.LifeSupport ->
                            Views.ResponsiveEmergencyList ({| interventions = props.bolusMedication |})
                        | Some Global.Pages.ContinuousMeds ->
                            Views.ContinuousMedication ({| interventions = props.continuousMedication |})
                        | Some Global.Pages.Prescribe ->
                            Views.Prescribe ({| scenarios = props.scenario; updateScenario = props.updateScenario |})
                        | _ -> notFound
                    }
                    </Stack>
                </Container>
            </Box>
        </ThemeProvider>
    </React.StrictMode>
    """