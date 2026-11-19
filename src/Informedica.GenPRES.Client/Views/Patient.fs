namespace Views


module Patient =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Client.Core.StateMachines
    open Informedica.GenPRES.Client.Core.MachinePolicies

    open Fable.Core
    open Fable.React
    open Feliz
    open Fable.Core.JsInterop
    open Elmish
    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models
    open OrderPlanMachine
    open OrderContextMachine
    open PatientDataPolicy


    module private Elmish =


        module Patient = PatientData


        /// Shows the patient data and, while the draft is not yet a patient, what is missing: an age, or
        /// a weight and a height. The id, the name and the birthdate are shown in the title bar instead.
        let show lang terms pat =
            let term fallback t =
                terms
                |> Deferred.map (fun terms -> LocalizationText.getTerm terms lang t |> Option.defaultValue fallback)
                |> Deferred.defaultValue fallback

            let toString =
                match terms with
                | Resolved terms -> PatientText.toString terms lang true
                | _ -> fun _ -> ""

            // What the draft needs to be a patient, in the same text as on the other pages.
            let missing =
                pat
                |> PatientReadinessPolicy.readiness
                |> PatientReadinessPolicy.missing
                |> Option.map (fun t -> term (PatientReadinessPolicy.english t) t)

            [ pat |> Option.map toString; missing ]
            |> List.choose id
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
        // The summary shows the patient from the server answer, with estimates next to an entered or
        // cleared weight or height. The fields show the draft.
        let current = envPatient.Current
        let clearPatient = envPatient.ClearPatient
        let editPatient = envPatient.EditPatient

        let localizationTerms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms
        let settings = (AppEnv.asEnv<AppEnv.ISettings> props.appEnv).Settings
        let envSession = AppEnv.asEnv<AppEnv.ISession> props.appEnv
        let session = envSession.Session

        // Every request reads the patient. While a request is under way the panel is disabled, so the
        // patient cannot change during the request and the next edit starts from the answer.
        let busy = (AppEnv.asEnv<AppEnv.IBusy> props.appEnv).Any

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization

        let isMobile = Mui.Hooks.useIsMobile ()

        let isExpanded, setExpanded = React.useState (patient |> canCalculate |> not)

        // The panel edits the draft of the App and keeps no copy. A copy would go stale when the
        // answer of the App equals the draft, and show values the App no longer has.
        let pat = patient

        // The edit of another patient while the user is asked whether to remove the orders. The draft
        // is unchanged, so cancel keeps the previous data.
        let newPatient, setNewPatient = React.useState (None: Api.PatientDataEdit option)

        // The server applies each edit; the reset, which has its own confirmation dialog, clears the draft.
        let dispatch msg =
            match msg |> toEdit pat with
            | None -> clearPatient ()
            | Some edit when envPatient.AsksNewPatient -> Some edit |> setNewPatient
            | Some edit -> edit |> editPatient

        let getTerm = Global.getLocalizedTerm localizationTerms lang

        // The panel is in identified mode when the EHR identified the patient, not when a Session is
        // open: a launch without patient data opens a Session without a patient. For an identified
        // patient the platform sets the age, and the other data can be changed but not cleared.
        let identified = SessionPatientPolicy.identified session

        // The summary shows the data only; the id, the name and the birthdate are in the title bar.
        let summary = current |> show lang localizationTerms |> toJsx

        // A click on the summary opens or closes the panel. While the patient cannot be calculated
        // the panel stays open.
        let toggle =
            fun _ ->
                if patient |> canCalculate |> not then
                    true |> setExpanded
                else
                    isExpanded |> not |> setExpanded

        // An edit of a field opens the panel, so the controls do not disappear while editing.
        let keepOpen = fun () -> true |> setExpanded

        // The reset asks for confirmation, because an age, a weight and a height typed at the bedside
        // cannot be restored by picking again; the reset on the prescribing page does not ask. Only an
        // anonymous patient has a reset; the data of an identified patient cannot be cleared.
        let confirmResetOpen, setConfirmResetOpen = React.useState false

        let onReset = fun () -> setConfirmResetOpen true

        let onResetConfirmed =
            fun () ->
                Msg.Clear |> dispatch
                setConfirmResetOpen false

        let confirmResetDialog =
            if identified then
                null
            else
                Components.ConfirmDialog.View
                    {|
                        isOpen = confirmResetOpen
                        title = Terms.``Patient Reset Dialog Title`` |> getTerm "Clear patient data"
                        text =
                            Terms.``Patient Reset Dialog Text``
                            |> getTerm
                                "The age, the weight, the height and the other data of the patient are cleared. Do you want to continue?"
                        confirmLabel = Terms.Reset |> getTerm "Reset"
                        cancelLabel = Terms.Cancel |> getTerm "Cancel"
                        onConfirm = onResetConfirmed
                        onCancel = fun () -> setConfirmResetOpen false
                    |}

        let onNewPatientConfirmed () =
            newPatient |> Option.iter envPatient.NewPatient
            setNewPatient None

        let newPatientDialog =
            Components.ConfirmDialog.View
                {|
                    isOpen = newPatient.IsSome
                    title = Terms.``Patient New Dialog Title`` |> getTerm "New patient"
                    text =
                        Terms.``Patient New Dialog Text``
                        |> getTerm "This starts a new patient. All orders are removed."
                    confirmLabel = Terms.Confirm |> getTerm "Confirm"
                    cancelLabel = Terms.Cancel |> getTerm "Cancel"
                    onConfirm = onNewPatientConfirmed
                    onCancel = fun () -> setNewPatient None
                |}

        // While a session is open, the platform sets the department, like the age: shown, not chosen.
        let launched =
            match session with
            | SessionMachine.SessionView.Open _
            | SessionMachine.SessionView.Closing _ -> true
            | _ -> false

        // Shows the chosen department, or the server default as placeholder when none is chosen. The
        // department filters the solution rules, so it is always visible on the panel.
        let departmentField =
            let own = pat |> Option.bind _.Department

            let names =
                match settings with
                | Resolved s -> s.Departments
                | _ -> [||]

            // The department of the patient is always among the options, so the field can show a
            // department from the url or the launch that the settings do not list, and a list of one
            // option never replaces the department of the patient.
            let options =
                match own with
                | Some d when names |> Array.contains d |> not -> Array.append names [| d |]
                | _ -> names
                |> Array.map (fun n -> n, n)

            // The default is a greyed placeholder, not a selection, so that picking it is a choice
            // of the user that the field sends.
            let placeholder =
                match own, settings with
                | None, Resolved s -> Some s.DefaultDepartment
                | _ -> None

            let changeDepartment =
                fun s ->
                    keepOpen ()
                    s |> Msg.UpdateDepartment |> dispatch

            // A list of a single department selects it, because the server never selects one.
            let only =
                match options with
                | [| (key, _) |] -> Some key
                | _ -> None

            let enabled = not busy && not launched

            React.useEffect (
                (fun () ->
                    match only with
                    | Some key when own <> Some key && enabled -> changeDepartment (Some key)
                    | _ -> ()
                ),
                [|
                    box (only |> Option.defaultValue "")
                    box (own |> Option.defaultValue "")
                    box enabled
                |]
            )

            Components.PickField.View
                {|
                    label = Terms.``Patient Department`` |> getTerm "Department"
                    options = options
                    selected = own
                    onChange = changeDepartment
                    // Clearing returns to the default, so the clear button shows only when a department
                    // is chosen.
                    clearable = own.IsSome
                    isLoading = settings |> Deferred.toOption |> Option.isNone
                    enabled = enabled
                    shape = Components.PickField.Shape.Scroll
                    placeholder = placeholder
                |}

        // Says where the department comes from: the launch while a session is open, the panel or the
        // url otherwise, or the server default when none is chosen.
        let departmentNotice =
            let inForce =
                match pat |> Option.bind _.Department, settings with
                | Some d, _ -> Some(d, false)
                | None, Resolved s -> Some(s.DefaultDepartment, true)
                | None, _ -> None

            match inForce with
            | None -> null
            | Some(_, isDefault) ->
                Components.Notice.View
                    {|
                        kind = Components.Notice.Kind.Info
                        title = None
                        message =
                            if isDefault then
                                Terms.``Patient Department Default``
                                |> getTerm "The default department: no department was chosen"
                            elif launched then
                                Terms.``Patient Department Launched``
                                |> getTerm "Passed on by the system that opened GenPRES"
                            else
                                Terms.``Patient Department Chosen`` |> getTerm "Chosen for this patient"
                        action = None
                        onClose = None
                    |}

        // The action bar is narrow and on the left, so a button is not as wide as the panel. Only an
        // anonymous patient has a reset. The buttons are disabled while a request is under way.
        let resetBar =
            let actions =
                [|
                    if not identified then
                        {|
                            label = Terms.Reset |> getTerm "Reset"
                            kind = Components.ActionBar.Kind.Secondary
                            onClick = onReset
                            disabled = busy
                            icon = Some Mui.Icons.Delete
                        |}
                    if SessionPatientPolicy.canRefresh session then
                        {|
                            label = Terms.``Patient Refresh`` |> getTerm "Refresh from the EHR"
                            kind = Components.ActionBar.Kind.Secondary
                            onClick = fun () -> envSession.Refresh()
                            disabled = busy
                            icon = Some Mui.Icons.RefreshIcon
                        |}
                |]

            if Array.isEmpty actions then
                null
            else
                Components.ActionBar.View {| actions = actions |}

        // A read-only field cannot be opened and has no clear button, because the user does not set
        // its value. A field that is not clearable can be changed but not emptied. The placeholder is
        // a key and a text shown greyed while the user has chosen no value, and the list opens on it.
        let createField readOnly clearable placeholder label sel changeValue vs =
            Components.SimpleSelect.View
                {|
                    label = label
                    selected = sel |> Option.map string
                    values = vs
                    updateSelected = changeValue
                    isLoading = false
                    disabled = busy
                    readOnly = readOnly
                    hasClear = clearable && not readOnly
                    canStep = false
                    severity = Severity.Normal
                    minWidth = None
                    description = None
                    reCalc = None
                    restore = ignore
                    busy = false
                    placeholder = placeholder |> Option.map snd
                    placeholderValue = placeholder |> Option.map fst
                |}

        // Renal function has no unknown option, so the clear button is the only way back to no
        // value, also for an identified patient.
        let createSelect label sel changeValue vs = createField false true None label sel changeValue vs

        // Weight, height and gestational age can be cleared only for an anonymous patient. The field
        // holds only a measured value; an estimate is the placeholder, so picking the estimated value
        // records it as measured.
        let createMeasureSelect placeholder label sel changeValue vs =
            createField false (not identified) placeholder label sel changeValue vs

        // the age fields: read-only for an identified patient, whose age the platform computes
        let createAgeSelect label sel changeValue vs = createField identified true None label sel changeValue vs

        // The lists of the selects come from the options the server sends with the patient data; without
        // patient data the lists are empty until the answer arrives.
        let options = pat |> Option.bind _.Options

        // The option values of a field as whole numbers; none without options.
        let optionValues get = options |> Option.map get |> Option.defaultValue []

        let wghts = optionValues (_.Weights >> Array.map int >> Array.toList)

        let hghts = optionValues (_.Heights >> Array.map int >> Array.toList)

        // The select list of whole numbers, each value with its label.
        let selectList label (values: int list) = values |> List.toArray |> Array.map (fun k -> $"%i{k}", label k)

        let whole (k: int) = $"%i{k}"

        // A 0 is not listed in a field that shows a cleared value, which is 0, as empty.
        let withoutZero = List.filter ((<>) 0)

        let inline zeroToNone opt =
            match opt with
            | Some v -> if int v = 0 then None else v |> int |> Some
            | None -> None

        let weightToNone =
            function
            | Some v -> wghts |> List.tryFind ((=) (int v))
            | None -> None

        let heightToNone =
            function
            | Some v -> hghts |> List.tryFind ((=) (int v))
            | None -> None

        // The placeholder of a field from its estimate, as a key and a text marked as estimated.
        // Returns None when a value is measured.
        let estimatePlaceholder toKey toText measured estimated =
            let word = Terms.``Patient Estimated`` |> getTerm "estimated"

            match measured with
            | Some _ -> None
            | None -> estimated |> Option.map (fun v -> toKey v, $"%s{toText v} (%s{word})")

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

                    // the radio values written above: male and female
                    match ev?target?value |> string with
                    | "male" -> Male
                    | "female" -> Female
                    | _ -> UnknownGender
                    |> Msg.UpdateGender
                    |> dispatch

            let genderLabel = Terms.``Patient Gender`` |> getTerm "Gender"
            let maleLabel = Terms.``Patient Male`` |> getTerm "Male"
            let femaleLabel = Terms.``Patient Female`` |> getTerm "Female"
            let unknownLabel = Terms.``Patient Unknown Gender`` |> getTerm "Unknown"

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

        // The adult mark set by the server: no switch below 16 years, a choice of the user from 16 to
        // 18 years and without an age, and checked and fixed from 18 years.
        let adultMark =
            pat
            |> Option.map _.AdultMark
            |> Option.defaultValue (AdultMark.MarkableAdult false)

        let adultOffered = adultMark <> AdultMark.NotAdult

        let adultSwitch =
            let adultChecked = pat |> Option.exists PatientData.isAdult
            let adultDisabled = adultMark = AdultMark.Adult

            let changeMarkedAdult =
                fun ev ->
                    keepOpen ()
                    ev?target?``checked`` |> unbox<bool> |> Msg.UpdateMarkedAdult |> dispatch

            let adultLabel = Terms.``Patient Adult`` |> getTerm "Adult"
            let disabled = busy || adultDisabled

            let switch =
                JSX.jsx
                    $"""
                import Switch from '@mui/material/Switch';

                <Switch id="patient-adult" checked={adultChecked} disabled={disabled} onChange={changeMarkedAdult} />
                """

            JSX.jsx
                $"""
            import FormControl from '@mui/material/FormControl';
            import FormControlLabel from '@mui/material/FormControlLabel';

            <FormControl>
                <FormControlLabel control={switch} label={adultLabel} />
            </FormControl>
            """

        let items1 =
            [|
                optionValues (_.Years >> List.map int)
                |> selectList whole
                |> createAgeSelect
                    (Terms.``Patient Age years`` |> getTerm "years")
                    (pat |> Option.bind PatientData.getAgeYears)
                    (fun s ->
                        keepOpen ()
                        s |> Msg.UpdateYear |> dispatch
                    )

                optionValues (_.Months >> List.map int)
                |> withoutZero
                |> selectList whole
                |> createAgeSelect
                    (Terms.``Patient Age months`` |> getTerm "months")
                    (pat |> Option.bind PatientData.getAgeMonths |> zeroToNone)
                    (fun s ->
                        keepOpen ()
                        s |> Msg.UpdateMonth |> dispatch
                    )

                optionValues (_.Weeks >> List.map int)
                |> withoutZero
                |> selectList whole
                |> createAgeSelect
                    (Terms.``Patient Age weeks`` |> getTerm "weeks")
                    (pat |> Option.bind PatientData.getAgeWeeks |> zeroToNone)
                    (fun s ->
                        keepOpen ()
                        s |> Msg.UpdateWeek |> dispatch
                    )

                optionValues (_.Days >> List.map int)
                |> withoutZero
                |> selectList whole
                |> createAgeSelect
                    (Terms.``Patient Age days`` |> getTerm "days")
                    (pat |> Option.bind PatientData.getAgeDays |> zeroToNone)
                    (fun s ->
                        keepOpen ()
                        s |> Msg.UpdateDay |> dispatch
                    )

                wghts
                |> selectList (fun g -> $"%g{float g / 1000.}")
                |> createMeasureSelect
                    (pat
                     |> Option.bind (fun p ->
                         p.Weight.Estimated
                         |> estimatePlaceholder
                             (fun g -> $"%i{int g}")
                             (fun g -> $"%g{float g / 1000.}")
                             p.Weight.Measured
                     ))
                    (Terms.``Patient Weight`` |> getTerm "Weight" |> (fun s -> $"{s} (kg)"))
                    (pat |> Option.bind (_.Weight.Measured >> weightToNone))
                    (fun s ->
                        keepOpen ()
                        s |> Msg.UpdateWeight |> dispatch
                    )

                hghts
                |> selectList whole
                |> createMeasureSelect
                    (pat
                     |> Option.bind (fun p ->
                         let cm c = $"%i{int c}"

                         p.Height.Estimated |> estimatePlaceholder cm cm p.Height.Measured
                     ))
                    (Terms.``Patient Length`` |> getTerm "Length" |> (fun s -> $"{s} (cm)"))
                    (pat |> Option.bind (_.Height.Measured >> heightToNone))
                    (fun s ->
                        keepOpen ()
                        s |> Msg.UpdateHeight |> dispatch
                    )

                // the server says from the age whether the gestational age may be given
                if pat |> Option.exists (fun p -> p.GestationalAge |> GestationalAge.mayBeGiven) then
                    optionValues (_.GestationalWeeks >> List.map int)
                    |> withoutZero
                    |> selectList whole
                    |> createMeasureSelect
                        None
                        (Terms.``Patient Age weeks`` |> getTerm "weeks" |> (fun s -> $"GA {s}"))
                        (pat |> Option.bind PatientRead.getGAWeeks |> zeroToNone)
                        (fun s ->
                            keepOpen ()
                            s |> Msg.UpdateGAWeek |> dispatch
                        )

                    optionValues (_.GestationalDays >> List.map int)
                    |> withoutZero
                    |> selectList whole
                    |> createMeasureSelect
                        None
                        (Terms.``Patient Age days`` |> getTerm "days" |> (fun s -> $"GA {s}"))
                        (pat |> Option.bind PatientRead.getGADays |> zeroToNone)
                        (fun s ->
                            keepOpen ()
                            s |> Msg.UpdateGADay |> dispatch
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

                if adultOffered then
                    adultSwitch

                let accessLabel = Terms.``Patient Access`` |> getTerm "Access"
                let tubeLabel = Terms.``Patient Enteral Tube`` |> getTerm "Enteral tube"

                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                import Checkbox from '@mui/material/Checkbox';
                import FormGroup from '@mui/material/FormGroup';

                <Box>
                    <FormLabel component="legend">{accessLabel}</FormLabel>
                    <FormGroup row>
                        <FormControl>
                            <FormControlLabel
                                control={checkBox "access-cvl" CVL Msg.ToggleCVL}
                                label="CVL" />
                        </FormControl>
                        <FormControl>
                            <FormControlLabel
                                control={checkBox "access-pvl" PVL Msg.TogglePVL}
                                label="PVL" />
                        </FormControl>
                        <FormControl>
                            <FormControlLabel
                                control={checkBox "access-et" EnteralTube Msg.ToggleET}
                                label={tubeLabel} />
                        </FormControl>
                    </FormGroup>
                </Box>
                """

                options
                |> Option.map (
                    RenalFunctionText.ofOptions (fun term -> term |> getTerm (RenalFunctionText.english term))
                )
                |> Option.defaultValue [||]
                |> createSelect
                    (Terms.``Patient Renal Function`` |> getTerm "Renal function")
                    (pat |> Option.bind PatientRead.getRenalFunction)
                    (fun s ->
                        keepOpen ()
                        s |> Msg.UpdateRenal |> dispatch
                    )

                departmentField
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
            import Box from '@mui/material/Box';
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
                {newPatientDialog}
            </React.Fragment>
            """

        Components.Disclosure.View
            {|
                isOpen = isExpanded
                onToggle = toggle
                summary = summary
                children = children
                isMobile = isMobile
                detailsPaddingTop = None
                ariaControls = Some "patient"
                summaryId = Some "patient-details"
            |}
