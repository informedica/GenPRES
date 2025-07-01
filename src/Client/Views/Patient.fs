namespace Views



module Patient =

    open System
    open Fable.Core
    open Fable.React
    open Feliz
    open Fable.Core.JsInterop
    open Elmish
    open Shared
    open Shared.Types
    open Shared.Models


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
            | UpdateGender of string
            | UpdateRenal of string option
            | ToggleCVL
            | TogglePVL
            | ToggleET


        let tryParse (s : string) = match Int32.TryParse(s) with | false, _ -> None | true, v -> v |> Some


        let init pat : State * Cmd<Msg> = pat, Cmd.none


        let setYear s (p : Patient option) =
            match p with
            | None ->
                Patient.create
                    (s |> Option.bind tryParse)
                    None None None None None None None UnknownGender [] None None
            | Some p ->
                Patient.create
                    (s |> Option.bind tryParse)
                    (p |> Patient.getAgeMonths)
                    (p |> Patient.getAgeWeeks)
                    (p |> Patient.getAgeDays)
                    None
                    None
                    None
                    None
                    p.Gender
                    p.Access
                    p.RenalFunction
                    p.Department


        let setMonth s (p : Patient option) =
            match p with
            | None ->
                Patient.create
                    None
                    (s |> Option.bind tryParse)
                    None None None None None None UnknownGender [] None None
            | Some p ->
                Patient.create
                    (p |> Patient.getAgeYears)
                    (s |> Option.bind tryParse)
                    (p |> Patient.getAgeWeeks)
                    (p |> Patient.getAgeDays)
                    None
                    None
                    None
                    None
                    p.Gender
                    p.Access
                    p.RenalFunction
                    p.Department


        let setWeek s (p : Patient option) =
            match p with
            | None ->
                Patient.create
                    None None
                    (s |> Option.bind tryParse)
                    None None None None None UnknownGender [] None None
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
                    p.Gender
                    p.Access
                    p.RenalFunction
                    p.Department


        let setDay s (p : Patient option) =
            match p with
            | None ->
                Patient.create
                    None None None
                    (s |> Option.bind tryParse)
                    None None None None UnknownGender [] None None
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
                    p.Gender
                    p.Access
                    p.RenalFunction
                    p.Department


        let setWeight s (p : Patient option) =
            match p with
            | None ->
                Patient.create
                    None None None None
                    (s |> Option.bind tryParse)
                    None None None UnknownGender [] None None
            | Some p ->
                Patient.create
                    (p |> Patient.getAgeYears)
                    (p |> Patient.getAgeMonths)
                    (p |> Patient.getAgeWeeks)
                    (p |> Patient.getAgeDays)
                    (s |> Option.bind tryParse)
                    (p |> Patient.getHeight |> Option.map int)
                    (p |> Patient.getGAWeeks)
                    (p |> Patient.getGADays)
                    p.Gender
                    p.Access
                    p.RenalFunction
                    p.Department


        let setHeight s (p : Patient option) =
            match p with
            | None ->
                Patient.create
                    None None None None None
                    (s |> Option.bind tryParse)
                    None None UnknownGender [] None None
            | Some p ->
                Patient.create
                    (p |> Patient.getAgeYears)
                    (p |> Patient.getAgeMonths)
                    (p |> Patient.getAgeWeeks)
                    (p |> Patient.getAgeDays)
                    (p |> Patient.getWeight |> Option.map int)
                    (s |> Option.bind tryParse)
                    (p |> Patient.getGAWeeks)
                    (p |> Patient.getGADays)
                    p.Gender
                    p.Access
                    p.RenalFunction
                    p.Department


        let setGAWeek s (p : Patient option) =
            match p with
            | None ->
                Patient.create
                    None None None None None None
                    (s |> Option.bind tryParse |> Option.map int)
                    None UnknownGender [] None None
            | Some p ->
                Patient.create
                    (p |> Patient.getAgeYears)
                    (p |> Patient.getAgeMonths)
                    (p |> Patient.getAgeWeeks)
                    (p |> Patient.getAgeDays)
                    None None
                    (s |> Option.bind tryParse |> Option.map int)
                    (p |> Patient.getGADays)
                    p.Gender
                    p.Access
                    p.RenalFunction
                    p.Department


        let setGADay s (p : Patient option) =
            match p with
            | None ->
                Patient.create
                    None None None None None None None
                    (s |> Option.bind tryParse |> Option.map int)
                    UnknownGender [] None None
            | Some p ->
                Patient.create
                    (p |> Patient.getAgeYears)
                    (p |> Patient.getAgeMonths)
                    (p |> Patient.getAgeWeeks)
                    (p |> Patient.getAgeDays)
                    None None
                    (p |> Patient.getGAWeeks)
                    (s |> Option.bind tryParse |> Option.map int)
                    p.Gender
                    p.Access
                    p.RenalFunction
                    p.Department


        let toggle item (p: Patient option) : Patient option =
            p |> Option.map (fun p ->
                { p with
                    Access =
                        if p.Access |> List.exists((=) item) then
                            p.Access
                            |> List.filter ((<>) item)
                        else
                            p.Access
                            |> List.append [ item ]
                }
            )


        let toggleCVL = toggle CVL


        let togglePVL = toggle PVL


        let toggleET = toggle EnteralTube


        let setRenal (s: string option) (p: Patient option) : Patient option =
            let set rf (p : Patient option) =
                match p with
                | None -> p
                | Some p ->
                    { p with
                        RenalFunction = rf
                    }
                    |> Some

            match s with
            | None ->
                p
                |> set None
            | Some s ->
                let rf =
                    s
                    |> Patient.RenalFunction.optionToRenal
                    |> Some
                p
                |> set rf


        let update dispatch msg (state : State) : State * Cmd<Msg> =
            let state =
                match msg with
                | Clear          -> None
                | UpdateYear s   -> state |> setYear s
                | UpdateMonth s  -> state |> setMonth s
                | UpdateWeek s   -> state |> setWeek s
                | UpdateDay s    -> state |> setDay s
                | UpdateWeight s -> state |> setWeight s
                | UpdateHeight s -> state |> setHeight s
                | UpdateGAWeek s -> state |> setGAWeek s
                | UpdateGADay s  -> state |> setGADay s
                | UpdateRenal s  -> state |> setRenal s
                | UpdateGender s ->
                    state
                    |> Option.defaultValue Patient.empty
                    |> (fun p ->
                        { p with
                            Patient.Weight.Measured = None
                            Patient.Height.Measured = None

                            Patient.Weight.Estimated = None
                            Patient.Height.Estimated = None

                            Gender =
                                match s with
                                | "male" -> Male
                                | "female" -> Female
                                | _ -> UnknownGender
                        }
                    )
                    |> Some
                | ToggleCVL      -> state |> toggleCVL
                | TogglePVL      -> state |> togglePVL
                | ToggleET       -> state |> toggleET

            state |> dispatch
            state, Cmd.none


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
                patient : Patient option
                updatePatient : Patient option -> unit
                localizationTerms : Deferred<string [] []>
            |}
        ) =

        let context = React.useContext Global.context
        let lang = context.Localization

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
            | Some v -> wghts |> Array.tryFind ((=) (int v))
            | None -> None

        let heightToNone = function
            | Some v -> hghts |> Array.tryFind ((=) (int v))
            | None -> None

        let checkBox item ev =
            JSX.jsx $"""
            import Checkbox from '@mui/material/Checkbox';

            <Checkbox
                checked={props.patient |> Option.map (fun p -> p.Access |> List.exists ((=) item)) |> Option.defaultValue false }
                onChange={fun _ -> handleChange (); ev |> dispatch} >
            </Checkbox>
            """

        let gender =
            let value =
                pat
                |> Option.map (fun p ->
                    p.Gender
                    |> function
                    | Male -> "male"
                    | Female -> "female"
                    | _ -> "other"
                )
                |> Option.defaultValue ""

            let radio =
                JSX.jsx $"""
                import Radio from '@mui/material/Radio';
                <Radio />
                """

            let handleChange =
                fun ev ->
                    handleChange ()

                    ev?target?value
                    |> string
                    |> UpdateGender |> dispatch

            JSX.jsx $"""
            import RadioGroup from '@mui/material/RadioGroup';
            import FormControlLabel from '@mui/material/FormControlLabel';
            import FormControl from '@mui/material/FormControl';
            import FormLabel from '@mui/material/FormLabel';

            <FormControl>
                <FormLabel id="demo-row-radio-buttons-group-label">Geslacht</FormLabel>
                <RadioGroup
                    row
                    aria-labelledby="demo-row-radio-buttons-group-label"
                    name="row-radio-buttons-group"
                    value={ value }
                    onChange={ handleChange }
                >
                    <FormControlLabel value="male" control={ radio } label="Man" />
                    <FormControlLabel value="female" control={ radio } label="Vrouw" />
                    <FormControlLabel value="other" control={ radio } label="Onbekend" />
                </RadioGroup>
            </FormControl>
            """

        let items1 =
            [|
                [|0..19|]
                |> Array.map (fun k -> $"{k}", if k > 18 then "> 18" else $"{k}")
                |> createSelect
                    (Terms.``Patient Age years`` |> getTerm "jaren")
                    (pat |> Option.bind Patient.getAgeYears)
                    (fun s -> handleChange (); s |> UpdateYear |> dispatch)

                [|1..11|]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    (Terms.``Patient Age months`` |> getTerm "maanden")
                    (pat |> Option.bind Patient.getAgeMonths |> zeroToNone)
                    (fun s -> handleChange (); s |> UpdateMonth |> dispatch)

                [|1..3|]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    (Terms.``Patient Age weeks`` |> getTerm "weken")
                    (pat |> Option.bind Patient.getAgeWeeks |> zeroToNone)
                    (fun s -> handleChange (); s |> UpdateWeek |> dispatch)

                [|1..6|]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    (Terms.``Patient Age days`` |> getTerm "dagen")
                    (pat |> Option.bind Patient.getAgeDays |> zeroToNone)
                    (fun s -> handleChange (); s |> UpdateDay |> dispatch)

                wghts
                |> Array.map (fun k -> $"{k}", $"{(k |> float)/1000.}")
                |> createSelect
                    (Terms.``Patient Weight`` |> getTerm "gewicht" |> fun s -> $"{s} (kg)")
                    (pat |> Option.bind (Patient.getWeight >> weightToNone))
                    (fun s -> handleChange (); s |> UpdateWeight |> dispatch)

                [|40..220|]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    (Terms.``Patient Length`` |> getTerm "lengte" |> fun s -> $"{s} (cm)")
                    (pat |> Option.bind (Patient.getHeight >> heightToNone))
                    (fun s -> handleChange (); s |> UpdateHeight |> dispatch)

                if pat |> Option.isSome &&
                   pat |> Option.map (fun p -> p |> Patient.getAgeInYears |> Option.defaultValue 0. < 1)
                       |> Option.defaultValue false then
                    [| 24 .. 42 |]
                    |> Array.map (fun k -> $"{k}", $"{k}")
                    |> createSelect
                        (Terms.``Patient Age weeks`` |> getTerm "weken" |> fun s -> $"GA {s}")
                        (pat |> Option.bind Patient.getGAWeeks |> zeroToNone)
                        (fun s -> handleChange (); s |> UpdateGAWeek |> dispatch)

                    [|1..6|]
                    |> Array.map (fun k -> $"{k}", $"{k}")
                    |> createSelect
                        (Terms.``Patient Age days`` |> getTerm "dagen" |> fun s -> $"GA {s}")
                        (pat |> Option.bind Patient.getGADays |> zeroToNone)
                        (fun s -> handleChange (); s |> UpdateGADay |> dispatch)
            |]
            |> Array.map (fun el ->
                JSX.jsx
                    $"""
                <Grid size = { {| xs = 6; lg = 2 |} }>{el}</Grid>
                """
            )

        let items2 =
            [|
                gender

                JSX.jsx
                    $"""
                import Checkbox from '@mui/material/Checkbox';
                import FormGroup from '@mui/material/FormGroup';

                <Box>
                    <FormLabel component="legend">Toegangen</FormLabel>
                    <FormGroup row>
                        <FormControl>
                            <FormControlLabel
                                control={ checkBox CVL ToggleCVL }
                                label="CVL" />
                        </FormControl>
                        <FormControl>
                            <FormControlLabel
                                control={ checkBox PVL TogglePVL }
                                label="PVL" />
                        </FormControl>
                        <FormControl>
                            <FormControlLabel
                                control={ checkBox EnteralTube ToggleET }
                                label="Sonde" />
                        </FormControl>
                    </FormGroup>
                </Box>
                """

                Patient.RenalFunction.options
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    "Nierfunctie"
                    (pat |> Option.bind (Patient.getRenalFunction))
                    (fun s -> handleChange (); s |> UpdateRenal |> dispatch)

            |]
            |> Array.map (fun el ->
                JSX.jsx
                    $"""
                <Grid size = { {| xs = 6; md = 4; lg = 4 |} }>{el}</Grid>
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
                sx={ {| bgcolor=Mui.Colors.Grey.``100`` |} }
                expandIcon={{ <ExpandMoreIcon /> }}
                aria-controls="patient"
                id="patient-details"
                >
                { pat |> show lang props.localizationTerms }
                </AccordionSummary>
                <AccordionDetails >
                    <Grid container spacing={2}>
                        {React.fragment (items1 |> unbox)}
                    </Grid>
                    <Grid container spacing={2} sx={ {| mt=2 |} } >
                        {React.fragment (items2 |> unbox)}
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

