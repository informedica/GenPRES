namespace Views

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types


module Patient =

    open Elmish


    module private Elmish =

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
                    None
                    None

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
                    None
                    None

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
                    None
                    None

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
                    None
                    None

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


    open Elmish


    [<JSX.Component>]
    let View (props :
            {|
                patient : Shared.Types.Patient option
                updatePatient : Shared.Types.Patient option -> unit
            |}
        ) =
        let isExpanded, setExpanded = React.useState true
        let depArr = [| box props.patient; box props.updatePatient |]
        let pat, dispatch =
            React.useElmish(
                    init props.patient,
                    update props.updatePatient,
                    depArr)

        let handleChange = fun _ -> isExpanded |> not |> setExpanded

        let createSelect label sel changeValue vs =
            Components.SimpleSelect.View({|
                label = label
                selected = sel |> Option.map string
                values = vs
                updateSelected = changeValue
                isLoading = false
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

        let items =
            [|
                [|0..19|]
                |> Array.map (fun k -> $"{k}", if k > 18 then "> 18" else $"{k}")
                |> createSelect
                    "jaren"
                    (pat |> Option.bind Shared.Patient.getAgeYears)
                    (UpdateYear >> dispatch)

                [|1..11|]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    "maanden"
                    (pat |> Option.bind Shared.Patient.getAgeMonths |> zeroToNone)
                    (UpdateMonth >> dispatch)

                [|1..3|]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    "weken"
                    (pat |> Option.bind Shared.Patient.getAgeWeeks |> zeroToNone)
                    (UpdateWeek >> dispatch)

                [|1..6|]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    "dagen"
                    (pat |> Option.bind Shared.Patient.getAgeDays |> zeroToNone)
                    (UpdateDay >> dispatch)

                wghts
                |> Array.map (fun k -> $"{k}", $"{(k |> float)/1000.}")
                |> createSelect
                    "gewicht (kg)"
                    (pat |> Option.bind (Shared.Patient.getWeight >> weightToNone))
                    (UpdateWeight >> dispatch)

                [|40..220|]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    "lengte (cm)"
                    (pat |> Option.bind (Shared.Patient.getHeight >> heightToNone))
                    (UpdateHeight >> dispatch)

            |]
            |> Array.map (fun el ->
                JSX.jsx
                    $"""
                <Grid item xs={2}>{el}</Grid>
                """
            )


        JSX.jsx
            $"""
        import React from "react";
        import Stack from '@mui/material/Stack';
        import Box from '@mui/material/Box';
        import Button from '@mui/material/Button';
        import Grid from '@mui/material/Grid';
        import Accordion from '@mui/material/Accordion';
        import AccordionDetails from '@mui/material/AccordionDetails';
        import AccordionSummary from '@mui/material/AccordionSummary';
        import Typography from '@mui/material/Typography';
        import ExpandMoreIcon from '@mui/icons-material/ExpandMore';

        <React.Fragment>
            <Accordion expanded={isExpanded} onChange={handleChange}>
                <AccordionSummary
                expandIcon={{ <ExpandMoreIcon /> }}
                aria-controls="patient"
                id="patient-details"
                >
                { pat |> show }
                </AccordionSummary>
                <AccordionDetails>
                    <Grid container spacing={2}>
                    {React.fragment (items |> unbox)}
                    </Grid>
                    <Box sx={ {| mt=2 |} }>
                        <Button variant="text" onClick={fun _ -> Clear |> dispatch} fullWidth startIcon={Mui.Icons.Delete} >
                            verwijderen
                        </Button>
                    </Box>
                </AccordionDetails>
            </Accordion>
        </React.Fragment>
        """

