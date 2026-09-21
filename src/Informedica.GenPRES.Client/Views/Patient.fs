namespace Views


module Patient =

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


        let init pat : State * Cmd<Msg> = pat, Cmd.none


        let update dispatch msg (state: State) : State * Cmd<Msg> =
            let state =
                match msg with
                | Clear -> None
                | UpdateYear s -> state |> Patient.setYear s
                | UpdateMonth s -> state |> Patient.setMonth s
                | UpdateWeek s -> state |> Patient.setWeek s
                | UpdateDay s -> state |> Patient.setDay s
                | UpdateWeight s -> state |> Patient.setWeight s
                | UpdateHeight s -> state |> Patient.setHeight s
                | UpdateGAWeek s -> state |> Patient.setGAWeek s
                | UpdateGADay s -> state |> Patient.setGADay s
                | UpdateRenal s -> state |> Patient.setRenal s
                | UpdateGender s -> state |> Patient.setGender s
                | ToggleCVL -> state |> Patient.toggleCVL
                | TogglePVL -> state |> Patient.togglePVL
                | ToggleET -> state |> Patient.toggleET

            state |> dispatch
            state, Cmd.none


        /// Whether the draft is a patient: an age, or a measured weight and height; the estimate
        /// no longer stands in for a measurement, so the minimum decides.
        let canCalculate (pat: Patient option) : bool =
            pat |> Option.bind (Patient.validate >> Result.toOption) |> Option.isSome


        /// The summary: the draft's data, and under it, while the draft is no patient yet, what is
        /// missing: an age, or a weight and a height. Nothing entered asks for the data.
        let show lang terms pat =
            let term fallback t =
                terms
                |> Deferred.map (fun terms -> Localization.getTerm terms lang t |> Option.defaultValue fallback)
                |> Deferred.defaultValue fallback

            let toString =
                match terms with
                | Resolved terms -> Patient.toString terms lang true
                | _ -> fun _ -> ""

            let missing =
                term
                    "Voer een leeftijd in, of een gewicht en een lengte"
                    Terms.``Patient enter age or weight and height``

            match pat with
            | Some p when p |> Patient.validate |> Result.isOk -> [ p |> toString ]
            | Some p -> [ p |> toString; missing ]
            | None -> [ term "Voer patient gegevens in" Terms.``Patient enter patient data`` ]
            |> List.filter (fun s -> s <> "")
            |> String.concat "\n\n"
            |> Markdown.markdown.children
            |> List.singleton
            |> Markdown.Markdown.markdown


    open Elmish


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        let envPatient = AppEnv.asEnv<AppEnv.IPatient> props.appEnv
        let patient = envPatient.Draft
        let updatePatient = envPatient.UpdatePatient

        let localizationTerms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        // the patient is the subject of every workbench and plan request: while one is under
        // way the panel is greyed, so that the patient cannot change under it
        let busy =
            (AppEnv.asEnv<AppEnv.IOrderContext> props.appEnv).OrderContext
            |> Deferred.inProgress
            || (AppEnv.asEnv<AppEnv.IOrderPlan> props.appEnv).OrderPlan |> Deferred.inProgress

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        let isExpanded, setExpanded = React.useState (patient |> canCalculate |> not)

        // True while any MUI dropdown / menu / popover / dialog is actually open anywhere
        // on the page. Collapsing the accordion while one is open reflows the page and
        // strands the popup (in this view or a child view), so we postpone the close.
        // A kept-mounted-but-closed overlay (e.g. the language menu uses keepMounted)
        // stays in the DOM with the MuiModal-hidden class, so it must be excluded;
        // open Poppers (DataGrid column/filter panels) are mounted only while open.
        let anyOverlayOpen () =
            Browser.Dom.document.querySelectorAll(".MuiModal-root:not(.MuiModal-hidden), .MuiPopper-root").length > 0

        // Auto-close the accordion after 5 seconds of inactivity, but postpone the close
        // while a dropdown is open so the collapse never leaves a dangling select list.
        // Once everything is closed the user gets a fresh 5s idle window before collapse.
        React.useEffect (
            (fun () ->
                if isExpanded then
                    let mutable timeoutId = Unchecked.defaultof<_>

                    let rec arm () =
                        JS.setTimeout
                            (fun () ->
                                if anyOverlayOpen () then
                                    timeoutId <- arm ()
                                else
                                    setExpanded false
                            )
                            5000

                    timeoutId <- arm ()
                    fun () -> JS.clearTimeout timeoutId
                else
                    fun () -> ()
            ),
            [| box isExpanded |]
        )

        // Use a ref so useElmish closures always call the latest updatePatient
        // without needing the function in the deps array (which would cause infinite re-renders)
        let updatePatientRef = React.useRef updatePatient
        updatePatientRef.current <- updatePatient

        let depArr = [| box patient; box lang |]

        let pat, dispatch =
            React.useElmish (init patient, (fun msg state -> update updatePatientRef.current msg state), depArr)

        let getTerm = Global.getLocalizedTerm localizationTerms lang

        let handleChange =
            fun _ ->
                if patient |> canCalculate |> not then
                    true |> setExpanded
                else
                    isExpanded |> not |> setExpanded

        let createSelect label sel changeValue vs =
            Components.SimpleSelect.View
                {|
                    label = label
                    selected = sel |> Option.map string
                    values = vs
                    updateSelected = changeValue
                    stepper = None
                    isLoading = false
                    disabled = busy
                    hasClear = true
                    warning = None
                    minWidth = None
                |}

        let wghts =
            [| 21000..1000..100000 |]
            |> Array.append [| 10500..500..20000 |]
            |> Array.append [| 2000..100..10000 |]
            |> Array.append [| 400..50..1950 |]

        let hghts = [| 40..220 |]

        let inline zeroToNone opt =
            match opt with
            | Some v -> if int v = 0 then None else v |> int |> Some
            | None -> None

        let weightToNone =
            function
            | Some v -> wghts |> Array.tryFind ((=) (int v))
            | None -> None

        let heightToNone =
            function
            | Some v -> hghts |> Array.tryFind ((=) (int v))
            | None -> None

        let checkBox (name: string) item ev =
            let handleAccessChange _ =
                handleChange ()
                ev |> dispatch

            JSX.jsx
                $"""
            import Checkbox from '@mui/material/Checkbox';

            <Checkbox
                id={name}
                name={name}
                disabled={busy}
                checked={patient
                         |> Option.map (fun p -> p.Access |> List.exists ((=) item))
                         |> Option.defaultValue false}
                onChange={handleAccessChange} >
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
                JSX.jsx
                    $"""
                import Radio from '@mui/material/Radio';
                <Radio />
                """

            let changeGender =
                fun ev ->
                    handleChange ()

                    ev?target?value |> string |> UpdateGender |> dispatch

            let genderLabel = Terms.``Patient Gender`` |> getTerm "Geslacht"
            let maleLabel = Terms.``Patient Male`` |> getTerm "Man"
            let femaleLabel = Terms.``Patient Female`` |> getTerm "Vrouw"
            let unknownLabel = Terms.``Patient Unknown Gender`` |> getTerm "Onbekend"

            JSX.jsx
                $"""
            import RadioGroup from '@mui/material/RadioGroup';
            import FormControlLabel from '@mui/material/FormControlLabel';
            import FormControl from '@mui/material/FormControl';
            import FormLabel from '@mui/material/FormLabel';

            <FormControl>
                <FormLabel id="demo-row-radio-buttons-group-label">{genderLabel}</FormLabel>
                <RadioGroup
                    row
                    aria-labelledby="demo-row-radio-buttons-group-label"
                    name="row-radio-buttons-group"
                    value={value}
                    onChange={changeGender}
                >
                    <FormControlLabel value="male" control={radio} label={maleLabel} disabled={busy} />
                    <FormControlLabel value="female" control={radio} label={femaleLabel} disabled={busy} />
                    <FormControlLabel value="other" control={radio} label={unknownLabel} disabled={busy} />
                </RadioGroup>
            </FormControl>
            """

        let items1 =
            [|
                [| 0..19 |]
                |> Array.map (fun k -> $"{k}", if k > 18 then "> 18" else $"{k}")
                |> createSelect
                    (Terms.``Patient Age years`` |> getTerm "jaren")
                    (pat |> Option.bind Patient.getAgeYears)
                    (fun s ->
                        handleChange ()
                        s |> UpdateYear |> dispatch
                    )

                [| 1..11 |]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    (Terms.``Patient Age months`` |> getTerm "maanden")
                    (pat |> Option.bind Patient.getAgeMonths |> zeroToNone)
                    (fun s ->
                        handleChange ()
                        s |> UpdateMonth |> dispatch
                    )

                [| 1..3 |]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    (Terms.``Patient Age weeks`` |> getTerm "weken")
                    (pat |> Option.bind Patient.getAgeWeeks |> zeroToNone)
                    (fun s ->
                        handleChange ()
                        s |> UpdateWeek |> dispatch
                    )

                [| 1..6 |]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    (Terms.``Patient Age days`` |> getTerm "dagen")
                    (pat |> Option.bind Patient.getAgeDays |> zeroToNone)
                    (fun s ->
                        handleChange ()
                        s |> UpdateDay |> dispatch
                    )

                wghts
                |> Array.map (fun k -> $"{k}", $"{(k |> float) / 1000.}")
                |> createSelect
                    (Terms.``Patient Weight`` |> getTerm "gewicht" |> (fun s -> $"{s} (kg)"))
                    (pat |> Option.bind (Patient.getWeight >> weightToNone))
                    (fun s ->
                        handleChange ()
                        s |> UpdateWeight |> dispatch
                    )

                [| 40..220 |]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    (Terms.``Patient Length`` |> getTerm "lengte" |> (fun s -> $"{s} (cm)"))
                    (pat |> Option.bind (Patient.getHeight >> heightToNone))
                    (fun s ->
                        handleChange ()
                        s |> UpdateHeight |> dispatch
                    )

                if
                    pat |> Option.isSome
                    && pat
                       |> Option.map (fun p -> p |> Patient.getAgeInYears |> Option.defaultValue 0. < 1)
                       |> Option.defaultValue false
                then
                    [| 24..42 |]
                    |> Array.map (fun k -> $"{k}", $"{k}")
                    |> createSelect
                        (Terms.``Patient Age weeks`` |> getTerm "weken" |> (fun s -> $"GA {s}"))
                        (pat |> Option.bind Patient.getGAWeeks |> zeroToNone)
                        (fun s ->
                            handleChange ()
                            s |> UpdateGAWeek |> dispatch
                        )

                    [| 1..6 |]
                    |> Array.map (fun k -> $"{k}", $"{k}")
                    |> createSelect
                        (Terms.``Patient Age days`` |> getTerm "dagen" |> (fun s -> $"GA {s}"))
                        (pat |> Option.bind Patient.getGADays |> zeroToNone)
                        (fun s ->
                            handleChange ()
                            s |> UpdateGADay |> dispatch
                        )
            |]
            |> Array.map (fun el ->
                let gridSize =
                    {|
                        xs = 6
                        lg = 2
                    |}

                JSX.jsx
                    $"""
                <Grid size={gridSize}>{el}</Grid>
                """
            )

        let items2 =
            [|
                gender

                let accessLabel = Terms.``Patient Access`` |> getTerm "Toegangen"
                let tubeLabel = Terms.``Patient Enteral Tube`` |> getTerm "Sonde"

                JSX.jsx
                    $"""
                import Checkbox from '@mui/material/Checkbox';
                import FormGroup from '@mui/material/FormGroup';

                <Box>
                    <FormLabel component="legend">{accessLabel}</FormLabel>
                    <FormGroup row>
                        <FormControl>
                            <FormControlLabel
                                control={checkBox "access-cvl" CVL ToggleCVL}
                                label="CVL" />
                        </FormControl>
                        <FormControl>
                            <FormControlLabel
                                control={checkBox "access-pvl" PVL TogglePVL}
                                label="PVL" />
                        </FormControl>
                        <FormControl>
                            <FormControlLabel
                                control={checkBox "access-et" EnteralTube ToggleET}
                                label={tubeLabel} />
                        </FormControl>
                    </FormGroup>
                </Box>
                """

                Patient.RenalFunction.options
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    (Terms.``Patient Renal Function`` |> getTerm "Nierfunctie")
                    (pat |> Option.bind Patient.getRenalFunction)
                    (fun s ->
                        handleChange ()
                        s |> UpdateRenal |> dispatch
                    )

            |]
            |> Array.map (fun el ->
                let gridSize =
                    {|
                        xs = 6
                        md = 4
                        lg = 4
                    |}

                JSX.jsx
                    $"""
                <Grid size={gridSize}>{el}</Grid>
                """
            )

        let children =
            JSX.jsx
                $"""
            import React from 'react';
            import Grid from '@mui/material/Grid';
            import Box from '@mui/material/Box';
            import Button from '@mui/material/Button';

            <React.Fragment>
                <Grid container spacing={2}>
                    {React.Fragment(items1 |> unbox<seq<ReactElement>>)}
                </Grid>
                <Grid container spacing={2} sx={ {| marginTop = 2 |} } >
                    {React.Fragment(items2 |> unbox<seq<ReactElement>>)}
                </Grid>
                <Box sx={ {| marginTop = 2 |} }>
                    <Button variant="text" onClick={fun _ -> Clear |> dispatch} disabled={busy} fullWidth startIcon={Mui.Icons.Delete} >
                        {Terms.Delete |> getTerm "Verwijder"}
                    </Button>
                </Box>
            </React.Fragment>
            """

        Components.Accordion.View
            {|
                expanded = isExpanded
                onChange = handleChange
                summary = pat |> show lang localizationTerms |> toJsx
                children = children
                isMobile = isMobile
                detailsPaddingTop = None
                ariaControls = Some "patient"
                summaryId = Some "patient-details"
            |}
