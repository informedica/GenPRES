namespace Views

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types


module Patient =

    open Elmish
    open Shared

    module private Elmish =

        module Patient = Patient


        type State = Patient option


        type Msg =
            | Clear
            | UpdateYear of string option
            | UpdateMonth of string option
            | UpdateWeek of string option
            | UpdateDay of string option
            | UpdateWeight of string option
            | UpdateHeight of string option
            | UpdateGAWeek of string option
            | UpdateGADay of string option
            | ToggleCVL


        let tryParse (s : string) = match Int32.TryParse(s) with | false, _ -> None | true, v -> v |> Some


        let init pat : State * Cmd<Msg> = pat, Cmd.none


        let setYear s (p : Patient option) =
            match p with
            | None ->
                Patient.create
                    (s |> Option.bind tryParse)
                    None None None None None None None false None
            | Some p ->
                Patient.create
                    (s |> Option.bind tryParse)
                    (p |> Patient.getAgeMonths)
                    (p |> Patient.getAgeWeeks)
                    (p |> Patient.getAgeDays)
                    None
                    None
                    (p |> Patient.getGAWeeks)
                    (p |> Patient.getGADays)
                    (p.CVL)
                    p.Department


        let setMonth s (p : Patient option) =
            match p with
            | None ->
                Patient.create
                    None
                    (s |> Option.bind tryParse)
                    None None None None None None false None
            | Some p ->
                Patient.create
                    (p |> Patient.getAgeYears)
                    (s |> Option.bind tryParse)
                    (p |> Patient.getAgeWeeks)
                    (p |> Patient.getAgeDays)
                    None
                    None
                    (p |> Patient.getGAWeeks)
                    (p |> Patient.getGADays)
                    p.CVL
                    p.Department


        let setWeek s (p : Patient option) =
            match p with
            | None ->
                Patient.create
                    None None
                    (s |> Option.bind tryParse)
                    None None None None None false None
            | Some p ->
                Patient.create
                    (p |> Patient.getAgeYears)
                    (p |> Patient.getAgeMonths)
                    (s |> Option.bind tryParse)
                    (p |> Patient.getAgeDays)
                    None
                    None
                    (p |> Patient.getGAWeeks)
                    (p |> Patient.getGADays)
                    p.CVL
                    p.Department


        let setDay s (p : Patient option) =
            match p with
            | None ->
                Patient.create
                    None None None
                    (s |> Option.bind tryParse)
                    None None None None false None
            | Some p ->
                Patient.create
                    (p |> Patient.getAgeYears)
                    (p |> Patient.getAgeMonths)
                    (p |> Patient.getAgeWeeks)
                    (s |> Option.bind tryParse)
                    None
                    None
                    (p |> Patient.getGAWeeks)
                    (p |> Patient.getGADays)
                    p.CVL
                    p.Department


        let setWeight s (p : Patient option) =
            match p with
            | None ->
                Patient.create
                    None None None None
                    (s |> Option.bind tryParse |> Option.map (fun v -> (v |> float) / 1000.))
                    None None None false None
            | Some p ->
                Patient.create
                    (p |> Patient.getAgeYears)
                    (p |> Patient.getAgeMonths)
                    (p |> Patient.getAgeWeeks)
                    (p |> Patient.getAgeDays)
                    (s |> Option.bind tryParse |> Option.map (fun v -> (v |> float) / 1000.))
                    (p |> Patient.getHeight)
                    (p |> Patient.getGAWeeks)
                    (p |> Patient.getGADays)
                    p.CVL
                    p.Department


        let setHeight s (p : Patient option) =
            match p with
            | None ->
                Patient.create
                    None None None None None
                    (s |> Option.bind tryParse |> Option.map float)
                    None None false None
            | Some p ->
                Patient.create
                    (p |> Patient.getAgeYears)
                    (p |> Patient.getAgeMonths)
                    (p |> Patient.getAgeWeeks)
                    (p |> Patient.getAgeDays)
                    (p |> Patient.getWeight)
                    (s |> Option.bind tryParse |> Option.map float)
                    (p |> Patient.getGAWeeks)
                    (p |> Patient.getGADays)
                    p.CVL
                    p.Department


        let setGAWeek s (p : Patient option) =
            match p with
            | None ->
                Patient.create
                    None None None None None None
                    (s |> Option.bind tryParse |> Option.map int)
                    None false None
            | Some p ->
                Patient.create
                    (p |> Patient.getAgeYears)
                    (p |> Patient.getAgeMonths)
                    (p |> Patient.getAgeWeeks)
                    (p |> Patient.getAgeDays)
                    None None
                    (s |> Option.bind tryParse |> Option.map int)
                    (p |> Patient.getGADays)
                    p.CVL
                    p.Department


        let setGADay s (p : Patient option) =
            match p with
            | None ->
                Patient.create
                    None None None None None None None
                    (s |> Option.bind tryParse |> Option.map int)
                    false None
            | Some p ->
                Patient.create
                    (p |> Patient.getAgeYears)
                    (p |> Patient.getAgeMonths)
                    (p |> Patient.getAgeWeeks)
                    (p |> Patient.getAgeDays)
                    None None
                    (p |> Patient.getGAWeeks)
                    (s |> Option.bind tryParse |> Option.map int)
                    p.CVL
                    p.Department


        let toggleCVL (p: Patient option) : Patient option =
            printfn "toggleCVL was called"
            p |> Option.map (fun p ->
                { p with CVL = not p.CVL}
            )


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
            | UpdateGAWeek s -> state |> setGAWeek s, Cmd.none
            | UpdateGADay s  -> state |> setGADay s, Cmd.none
            | ToggleCVL     -> state |> toggleCVL, Cmd.none
            |> fun (state, cmd) ->
                state |> dispatch
                state, cmd


        let show lang terms pat =
            let toString = 
                match terms with
                | Resolved terms -> Patient.toString terms lang true
                | _ -> fun _ -> ""
            match pat with
            | Some p ->
                p
                |> toString
                |> Markdown.markdown.children
            | None ->
                terms
                |> Deferred.map (fun terms -> 
                    Terms.``Patient enter patient data``
                    |> Localization.getTerm terms lang
                    |> Option.defaultValue "Voer patient gegevens in"
                )
                |> Deferred.defaultValue "Voer patient gegevens in"
                |> Markdown.markdown.children
            |> List.singleton
            |> Markdown.Markdown.markdown


    open Elmish


    [<JSX.Component>]
    let View (props :
            {|
                patient : Types.Patient option
                updatePatient : Types.Patient option -> unit
                localizationTerms : Deferred<string [] []>
            |}
        ) =

        let lang = React.useContext(Global.languageContext)
        let isExpanded, setExpanded = React.useState true
        let depArr = [| box props.patient; box props.updatePatient; box lang |]
        let pat, dispatch =
            React.useElmish(
                    init props.patient,
                    update props.updatePatient,
                    depArr)

        let getTerm defVal term = 
            props.localizationTerms
            |> Deferred.map (fun terms ->
                Localization.getTerm terms lang term
                |> Option.defaultValue defVal
            )
            |> Deferred.defaultValue defVal

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
            |> Array.append [|400..50..1950|]

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

        let checkBox () =
            JSX.jsx $"""
            import Checkbox from '@mui/material/Checkbox';

            <Checkbox
                checked={props.patient |> Option.map (fun p -> p.CVL) |> Option.defaultValue false}
                onChange={fun _ -> ToggleCVL |> dispatch} >
            </Checkbox>
            """

        let items =
            [|
                [|0..19|]
                |> Array.map (fun k -> $"{k}", if k > 18 then "> 18" else $"{k}")
                |> createSelect
                    (Terms.``Patient Age years`` |> getTerm "jaren")
                    (pat |> Option.bind Patient.getAgeYears)
                    (UpdateYear >> dispatch)

                [|1..11|]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    (Terms.``Patient Age months`` |> getTerm "maanden")
                    (pat |> Option.bind Patient.getAgeMonths |> zeroToNone)
                    (UpdateMonth >> dispatch)

                [|1..3|]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    (Terms.``Patient Age weeks`` |> getTerm "weken")
                    (pat |> Option.bind Patient.getAgeWeeks |> zeroToNone)
                    (UpdateWeek >> dispatch)

                [|1..6|]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    (Terms.``Patient Age days`` |> getTerm "dagen")
                    (pat |> Option.bind Patient.getAgeDays |> zeroToNone)
                    (UpdateDay >> dispatch)

                wghts
                |> Array.map (fun k -> $"{k}", $"{(k |> float)/1000.}")
                |> createSelect
                    (Terms.``Patient Weight`` |> getTerm "gewicht" |> fun s -> $"{s} (kg)")
                    (pat |> Option.bind (Patient.getWeight >> weightToNone))
                    (UpdateWeight >> dispatch)

                [|40..220|]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    (Terms.``Patient Length`` |> getTerm "lengte" |> fun s -> $"{s} (cm)")
                    (pat |> Option.bind (Patient.getHeight >> heightToNone))
                    (UpdateHeight >> dispatch)

                if pat |> Option.isSome &&
                   pat |> Option.map (fun p -> p |> Shared.Patient.getAgeInYears |> Option.defaultValue 0. < 1)
                       |> Option.defaultValue false then
                    [| 24 .. 42 |]
                    |> Array.map (fun k -> $"{k}", $"{k}")
                    |> createSelect
                        (Terms.``Patient Age weeks`` |> getTerm "weken" |> fun s -> $"GA {s}")
                        (pat |> Option.bind Shared.Patient.getGAWeeks |> zeroToNone)
                        (UpdateGAWeek >> dispatch)

                    [|1..6|]
                    |> Array.map (fun k -> $"{k}", $"{k}")
                    |> createSelect
                        (Terms.``Patient Age days`` |> getTerm "dagen" |> fun s -> $"GA {s}")
                        (pat |> Option.bind Shared.Patient.getGADays |> zeroToNone)
                        (UpdateGADay >> dispatch)

                JSX.jsx
                    $"""
                import Checkbox from '@mui/material/Checkbox';

                <FormControlLabel
                    control={{{ checkBox () }}}
                    label="CVL" />
                """

            |]
            |> Array.map (fun el ->
                JSX.jsx
                    $"""
                <Grid item xs={12} md={3} lg={2}>{el}</Grid>
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
        import FormControlLabel from '@mui/material/FormControlLabel';

        <React.Fragment>
            <Accordion expanded={isExpanded} onChange={handleChange}>
                <AccordionSummary
                expandIcon={{ <ExpandMoreIcon /> }}
                aria-controls="patient"
                id="patient-details"
                >
                { pat |> show lang props.localizationTerms }
                </AccordionSummary>
                <AccordionDetails>
                    <Grid container spacing={2}>
                    {React.fragment (items |> unbox)}
                    </Grid>
                    <Box sx={ {| mt=2 |} }>
                        <Button variant="text" onClick={fun _ -> Clear |> dispatch} fullWidth startIcon={Mui.Icons.Delete} >
                            {Terms.Delete |> getTerm "Verwijder"}
                        </Button>
                    </Box>
                </AccordionDetails>
            </Accordion>
        </React.Fragment>
        """

