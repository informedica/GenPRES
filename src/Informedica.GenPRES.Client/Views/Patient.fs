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
    open OrderPlanMachine
    open OrderContextMachine


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
        let settings = (AppEnv.asEnv<AppEnv.ISettings> props.appEnv).Settings
        let session = (AppEnv.asEnv<AppEnv.ISession> props.appEnv).Session

        // the patient is the subject of every workbench and plan request: while one is under
        // way the panel is greyed, so that the patient cannot change under it
        let busy =
            match
                (AppEnv.asEnv<AppEnv.IOrderContext> props.appEnv).OrderContext,
                (AppEnv.asEnv<AppEnv.IOrderPlan> props.appEnv).OrderPlan
            with
            | OrderContextView.Changing _, _
            | _, OrderPlanView.Changing _ -> true
            | _ -> false

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        let isExpanded, setExpanded = React.useState (patient |> canCalculate |> not)

        // Use a ref so useElmish closures always call the latest updatePatient
        // without needing the function in the deps array (which would cause infinite re-renders)
        let updatePatientRef = React.useRef updatePatient
        updatePatientRef.current <- updatePatient

        let depArr = [| box patient; box lang |]

        let pat, dispatch =
            React.useElmish (init patient, (fun msg state -> update updatePatientRef.current msg state), depArr)

        let getTerm = Global.getLocalizedTerm localizationTerms lang

        // the summary click opens or folds the panel; while the patient cannot be calculated
        // the panel stays open, since there is nothing to fold it over
        let toggle =
            fun _ ->
                if patient |> canCalculate |> not then
                    true |> setExpanded
                else
                    isExpanded |> not |> setExpanded

        // an edit of a field keeps the panel open, whatever it was: editing never takes the
        // controls away from under the hand
        let keepOpen = fun () -> true |> setExpanded

        // the reset is asked first: the button opens the question, confirming discards the
        // draft. An age, a weight and a height typed at the bedside are not rebuilt by picking
        // again, which is why this reset asks where the prescribing page's does not
        let confirmResetOpen, setConfirmResetOpen = React.useState false

        let onReset = fun () -> setConfirmResetOpen true

        let onResetConfirmed =
            fun () ->
                Clear |> dispatch
                setConfirmResetOpen false

        let confirmResetDialog =
            Components.ConfirmDialog.View
                {|
                    isOpen = confirmResetOpen
                    title = Terms.``Patient Reset Dialog Title`` |> getTerm "Patiëntgegevens wissen"
                    text =
                        Terms.``Patient Reset Dialog Text``
                        |> getTerm
                            "De leeftijd, het gewicht, de lengte en de overige gegevens van de patiënt worden gewist. Wilt u doorgaan?"
                    confirmLabel = Terms.Reset |> getTerm "Reset"
                    cancelLabel = Terms.Cancel |> getTerm "Annuleren"
                    onConfirm = onResetConfirmed
                    onCancel = fun () -> setConfirmResetOpen false
                |}

        // the department in force and where it came from: the patient's own, given by the
        // launch when a session is open and chosen in the url otherwise, or else the server's
        // default, said so, since a filter the user can see is not a hidden one
        let departmentNotice =
            let launched =
                match session with
                | SessionMachine.SessionView.Open _
                | SessionMachine.SessionView.Closing _ -> true
                | _ -> false

            let inForce =
                match pat |> Option.bind _.Department, settings with
                | Some d, _ -> Some(d, false)
                | None, Resolved s -> Some(s.DefaultDepartment, true)
                | None, _ -> None

            match inForce with
            | None -> null
            | Some(department, isDefault) ->
                Components.Notice.View
                    {|
                        kind = Components.Notice.Kind.Info
                        title =
                            let label = Terms.``Patient Department`` |> getTerm "Afdeling"
                            Some(label + ": " + department)
                        message =
                            if isDefault then
                                Terms.``Patient Department Default``
                                |> getTerm "De standaardafdeling: er is geen afdeling gekozen"
                            elif launched then
                                Terms.``Patient Department Launched``
                                |> getTerm "Meegegeven door het systeem dat GenPRES opende"
                            else
                                Terms.``Patient Department Chosen`` |> getTerm "Gekozen in de url"
                        action = None
                        onClose = None
                    |}

        // bounded and to the left, so the button is not as wide as the panel it sits in and is
        // not where you click by default
        let resetBar =
            Components.ActionBar.View
                {|
                    actions =
                        [|
                            {|
                                label = Terms.Reset |> getTerm "Reset"
                                kind = Components.ActionBar.Kind.Secondary
                                onClick = onReset
                                disabled = busy
                                icon = Some Mui.Icons.RefreshIcon
                            |}
                        |]
                |}

        let createSelect label sel changeValue vs =
            Components.SimpleSelect.View
                {|
                    label = label
                    selected = sel |> Option.map string
                    values = vs
                    updateSelected = changeValue
                    isLoading = false
                    disabled = busy
                    readOnly = false
                    hasClear = true
                    canStep = false
                    severity = Severity.Normal
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
                keepOpen ()
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
                    keepOpen ()

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
                        keepOpen ()
                        s |> UpdateYear |> dispatch
                    )

                [| 1..11 |]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    (Terms.``Patient Age months`` |> getTerm "maanden")
                    (pat |> Option.bind Patient.getAgeMonths |> zeroToNone)
                    (fun s ->
                        keepOpen ()
                        s |> UpdateMonth |> dispatch
                    )

                [| 1..3 |]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    (Terms.``Patient Age weeks`` |> getTerm "weken")
                    (pat |> Option.bind Patient.getAgeWeeks |> zeroToNone)
                    (fun s ->
                        keepOpen ()
                        s |> UpdateWeek |> dispatch
                    )

                [| 1..6 |]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    (Terms.``Patient Age days`` |> getTerm "dagen")
                    (pat |> Option.bind Patient.getAgeDays |> zeroToNone)
                    (fun s ->
                        keepOpen ()
                        s |> UpdateDay |> dispatch
                    )

                wghts
                |> Array.map (fun k -> $"{k}", $"{(k |> float) / 1000.}")
                |> createSelect
                    (Terms.``Patient Weight`` |> getTerm "gewicht" |> (fun s -> $"{s} (kg)"))
                    (pat |> Option.bind (Patient.getWeight >> weightToNone))
                    (fun s ->
                        keepOpen ()
                        s |> UpdateWeight |> dispatch
                    )

                [| 40..220 |]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    (Terms.``Patient Length`` |> getTerm "lengte" |> (fun s -> $"{s} (cm)"))
                    (pat |> Option.bind (Patient.getHeight >> heightToNone))
                    (fun s ->
                        keepOpen ()
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
                            keepOpen ()
                            s |> UpdateGAWeek |> dispatch
                        )

                    [| 1..6 |]
                    |> Array.map (fun k -> $"{k}", $"{k}")
                    |> createSelect
                        (Terms.``Patient Age days`` |> getTerm "dagen" |> (fun s -> $"GA {s}"))
                        (pat |> Option.bind Patient.getGADays |> zeroToNone)
                        (fun s ->
                            keepOpen ()
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
                        keepOpen ()
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

            <React.Fragment>
                <Grid container spacing={2}>
                    {React.Fragment(items1 |> unbox<seq<ReactElement>>)}
                </Grid>
                <Grid container spacing={2} sx={ {| marginTop = 2 |} } >
                    {React.Fragment(items2 |> unbox<seq<ReactElement>>)}
                </Grid>
                {departmentNotice}
                {resetBar}
                {confirmResetDialog}
            </React.Fragment>
            """

        Components.Disclosure.View
            {|
                isOpen = isExpanded
                onToggle = toggle
                summary = pat |> show lang localizationTerms |> toJsx
                children = children
                isMobile = isMobile
                detailsPaddingTop = None
                ariaControls = Some "patient"
                summaryId = Some "patient-details"
            |}
