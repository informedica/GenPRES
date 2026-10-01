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
    open PatientDraftPolicy


    module private Elmish =


        module Patient = Patient


        /// The summary: the draft's data, and under it, while the draft is no patient yet, what is
        /// missing: an age, or a weight and a height. Nothing entered asks for the data. The name
        /// and the birthdate stay in the title bar: the panel is about the data, and says only
        /// the id under which it is held.
        let show lang terms pat =
            let term fallback t =
                terms
                |> Deferred.map (fun terms -> Localization.getTerm terms lang t |> Option.defaultValue fallback)
                |> Deferred.defaultValue fallback

            let toString =
                match terms with
                | Resolved terms -> Patient.toString terms lang true
                | _ -> fun _ -> ""

            // what the draft misses to be a patient, in the words every page uses
            let missing =
                pat
                |> PatientReadiness.readiness
                |> PatientReadiness.missing
                |> Option.map (fun t -> term (PatientReadiness.english t) t)

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
        // the summary shows the full estimates of the age, also for a weight or height that was
        // entered or cleared; the fields show the draft
        let estimated = envPatient.Estimated
        let updatePatient = envPatient.UpdatePatient
        let editPatient = envPatient.EditPatient

        let localizationTerms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms
        let settings = (AppEnv.asEnv<AppEnv.ISettings> props.appEnv).Settings
        let envSession = AppEnv.asEnv<AppEnv.ISession> props.appEnv
        let session = envSession.Session
        let envPlan = AppEnv.asEnv<AppEnv.IOrderPlan> props.appEnv

        // the patient is the subject of every workbench and plan request: while one is under
        // way the panel is greyed, so that the patient cannot change under it
        let busy =
            match (AppEnv.asEnv<AppEnv.IOrderContext> props.appEnv).OrderContext, envPlan.OrderPlan with
            | OrderContextView.Changing _, _
            | _, OrderPlanView.Changing _ -> true
            | _ -> false

        let context: Global.Context = React.useContext Global.context
        let lang = context.Localization

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        let isExpanded, setExpanded = React.useState (patient |> canCalculate |> not)

        // the panel shows the App's draft and keeps no copy of it: an edit is applied to that
        // draft and sent to the App. A copy would go stale whenever the App's answer equals the
        // draft it had, and show values the App no longer holds
        let pat = patient

        let dispatch msg =
            let edited = pat |> update msg

            match estimates msg with
            | Estimates.Renewed -> edited |> updatePatient
            | Estimates.Kept -> edited |> keepEstimates msg pat |> editPatient

        let getTerm = Global.getLocalizedTerm localizationTerms lang

        // whom the patient is when the EHR said: the panel is in identified mode by it, not by
        // an open Session, since a launch without data opens a Session without one, and one
        // with a signed head opens identified from the head. Identified, the age is the
        // platform's, shown, not chosen, and the rest of the data is changed, never cleared
        let patientContext =
            match session with
            | SessionMachine.SessionView.Open opened
            | SessionMachine.SessionView.Closing opened -> opened.PatientContext
            | _ -> None

        let identity = patientContext |> Option.bind _.Identity

        let identified = identity.IsSome

        // the patient context is held, for an identified patient only, while the plan has an
        // order that is new or changed since the version last opened or signed: every order a
        // signed version adds rests on one patient context, so the panel cannot change it until
        // the plan is signed or those orders are removed
        let held = identified && envPlan.Changed |> Array.isEmpty |> not

        // held, the fields stay as they are but take no change: an attempt asks instead, with the
        // way out that removes the new and changed orders; signing is the plan's own button
        let heldOpen, setHeldOpen = React.useState false

        // not while a request is under way: the panel is greyed then, and the orders cannot be
        // removed until the plan is settled
        let onAttempt (e: Browser.Types.Event) =
            if held then
                e.preventDefault ()
                e.stopPropagation ()

                if not busy then
                    setHeldOpen true

        // every edit of the panel goes out here, so that one the pointer does not make, from the
        // keyboard or assistive technology, is held too: it asks instead of changing the panel
        let dispatch msg =
            if held then
                if not busy then
                    setHeldOpen true
            else
                dispatch msg

        // the summary: the data, and above it, identified, the id the data is held under; the
        // name and the birthdate are the title bar's alone
        let summary =
            let data = estimated |> show lang localizationTerms |> toJsx

            match identity, patientContext with
            | Some _, Some context ->
                let idLabel = Terms.``Patient Id`` |> getTerm "Patiënt-ID"
                let idLine = $"{idLabel} {context.PatientId}"

                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                import Typography from '@mui/material/Typography';

                <Box>
                    <Typography variant="caption" color="text.secondary" component="div">{idLine}</Typography>
                    {data}
                </Box>
                """
            | _ -> data

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
        // again, which is why this reset asks where the prescribing page's does not. Only an
        // anonymous patient has a reset: an identified patient's data is changed, never cleared
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

        // a launched patient's department is the platform's, as its age is: shown, not chosen
        let launched =
            match session with
            | SessionMachine.SessionView.Open _
            | SessionMachine.SessionView.Closing _ -> true
            | _ -> false

        // the department chosen, or the server's default preselected while none is: the pick
        // that filters the solution rules is on the panel, so it is never a hidden one
        let departmentField =
            let own = pat |> Option.bind _.Department

            let names =
                match settings with
                | Resolved s -> s.Departments
                | _ -> [||]

            // the department in force is always among the options, so that the field can show
            // it when the url or the launch named one the rules do not, and so that a field
            // left with one option never picks that one over the one the patient has
            let options =
                match own with
                | Some d when names |> Array.contains d |> not -> Array.append names [| d |]
                | _ -> names
                |> Array.map (fun n -> n, n)

            let selected =
                match own, settings with
                | Some d, _ -> Some d
                | None, Resolved s -> Some s.DefaultDepartment
                | None, _ -> None

            let changeDepartment =
                fun s ->
                    keepOpen ()
                    s |> Msg.UpdateDepartment |> dispatch

            Components.PickField.View
                {|
                    label = Terms.``Patient Department`` |> getTerm "Afdeling"
                    options = options
                    selected = selected
                    onChange = changeDepartment
                    // the cross returns to the default, so it is offered only while there is a
                    // choice to take back; clearing a default already shown would change nothing
                    clearable = own.IsSome
                    isLoading = settings |> Deferred.toOption |> Option.isNone
                    enabled = not busy && not launched
                    shape = Components.PickField.Shape.Scroll
                |}

        // where the department in force came from: the launch when a session is open, the
        // panel or the url otherwise, or else the server's default, said so
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
                                |> getTerm "De standaardafdeling: er is geen afdeling gekozen"
                            elif launched then
                                Terms.``Patient Department Launched``
                                |> getTerm "Meegegeven door het systeem dat GenPRES opende"
                            else
                                Terms.``Patient Department Chosen`` |> getTerm "Gekozen voor deze patiënt"
                        action = None
                        onClose = None
                    |}

        // only over a settled plan; while a request is under way the question stays open, to be
        // confirmed once it has landed
        let onRemoveChanged () =
            match envPlan.OrderPlan with
            | OrderPlanView.Settled(tp, _) ->
                setHeldOpen false

                Api.OrderPlanCommand.RemoveOrderContexts(tp, envPlan.Changed)
                |> envPlan.OrderPlanCommand
            | OrderPlanView.NoPatient -> setHeldOpen false
            | OrderPlanView.Changing _ -> ()

        // the EHR read again and the head reopened on it; the new and changed orders go with the
        // reopen
        let onRefresh () =
            setHeldOpen false
            envSession.Refresh()

        let onHeldClose = fun _ -> setHeldOpen false

        // the question, with the two ways out that drop the new and changed orders: remove them,
        // or read the patient data from the EHR again; signing is the plan's own button
        let heldDialog =
            let title =
                Terms.``Patient Context Held Title``
                |> getTerm "Patiëntgegevens kunnen niet worden gewijzigd"

            let text =
                Terms.``Patient Context Held``
                |> getTerm
                    "Het orderplan heeft nieuwe of gewijzigde orders. Onderteken het orderplan om de patiëntgegevens te wijzigen, of laat die orders vervallen: verwijder ze, of ververs de patiëntgegevens uit het EPD."

            let actions =
                Components.ActionBar.View
                    {|
                        actions =
                            [|
                                {|
                                    label = Terms.Cancel |> getTerm "Annuleren"
                                    kind = Components.ActionBar.Kind.Secondary
                                    onClick = fun () -> setHeldOpen false
                                    disabled = false
                                    icon = None
                                |}
                                {|
                                    label =
                                        Terms.``Patient Context Held Remove``
                                        |> getTerm "Verwijder nieuwe en gewijzigde orders"
                                    kind = Components.ActionBar.Kind.Destructive
                                    onClick = onRemoveChanged
                                    disabled = false
                                    icon = None
                                |}
                                {|
                                    label = Terms.``Patient Context Held Refresh`` |> getTerm "Ververs uit het EPD"
                                    kind = Components.ActionBar.Kind.Primary
                                    onClick = onRefresh
                                    disabled = false
                                    icon = None
                                |}
                            |]
                    |}

            JSX.jsx
                $"""
            import Dialog from '@mui/material/Dialog';
            import DialogTitle from '@mui/material/DialogTitle';
            import DialogContent from '@mui/material/DialogContent';
            import DialogContentText from '@mui/material/DialogContentText';
            import DialogActions from '@mui/material/DialogActions';

            <Dialog open={heldOpen} onClose={onHeldClose} fullWidth={true} maxWidth="sm">
                <DialogTitle>{title}</DialogTitle>
                <DialogContent>
                    <DialogContentText>{text}</DialogContentText>
                </DialogContent>
                <DialogActions>{actions}</DialogActions>
            </Dialog>
            """

        // bounded and to the left, so the button is not as wide as the panel it sits in and is
        // not where you click by default
        let resetBar =
            if identified then
                null
            else
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

        // a read-only field cannot be opened and has no cross: what it holds is not the user's;
        // a field that is not clearable can be changed, but not emptied
        let createField readOnly clearable label sel changeValue vs =
            Components.SimpleSelect.View
                {|
                    label = label
                    selected = sel |> Option.map string
                    values = vs
                    updateSelected = changeValue
                    isLoading = false
                    disabled = busy
                    readOnly = readOnly || held
                    hasClear = clearable && not (readOnly || held)
                    canStep = false
                    severity = Severity.Normal
                    minWidth = None
                    description = None
                    reopen = None
                    restore = ignore
                    busy = false
                    placeholder = None
                |}

        // renal function has no "unknown" among its options, so the cross is the only way back
        // to none, for an identified patient too
        let createSelect label sel changeValue vs = createField false true label sel changeValue vs

        // weight, height and gestational age: an identified patient's are changed, never
        // cleared; an anonymous patient's are fictitious and may be
        let createMeasureSelect label sel changeValue vs = createField false (not identified) label sel changeValue vs

        // the age fields: read-only for an identified patient, whose age the platform computes
        let createAgeSelect label sel changeValue vs = createField identified true label sel changeValue vs

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

                    ev?target?value |> string |> Msg.UpdateGender |> dispatch

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
                |> createAgeSelect
                    (Terms.``Patient Age years`` |> getTerm "jaren")
                    (pat |> Option.bind Patient.getAgeYears)
                    (fun s ->
                        keepOpen ()
                        s |> Msg.UpdateYear |> dispatch
                    )

                [| 1..11 |]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createAgeSelect
                    (Terms.``Patient Age months`` |> getTerm "maanden")
                    (pat |> Option.bind Patient.getAgeMonths |> zeroToNone)
                    (fun s ->
                        keepOpen ()
                        s |> Msg.UpdateMonth |> dispatch
                    )

                [| 1..3 |]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createAgeSelect
                    (Terms.``Patient Age weeks`` |> getTerm "weken")
                    (pat |> Option.bind Patient.getAgeWeeks |> zeroToNone)
                    (fun s ->
                        keepOpen ()
                        s |> Msg.UpdateWeek |> dispatch
                    )

                [| 1..6 |]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createAgeSelect
                    (Terms.``Patient Age days`` |> getTerm "dagen")
                    (pat |> Option.bind Patient.getAgeDays |> zeroToNone)
                    (fun s ->
                        keepOpen ()
                        s |> Msg.UpdateDay |> dispatch
                    )

                wghts
                |> Array.map (fun k -> $"{k}", $"{(k |> float) / 1000.}")
                |> createMeasureSelect
                    (Terms.``Patient Weight`` |> getTerm "gewicht" |> (fun s -> $"{s} (kg)"))
                    (pat |> Option.bind (Patient.getWeight >> weightToNone))
                    (fun s ->
                        keepOpen ()
                        s |> Msg.UpdateWeight |> dispatch
                    )

                [| 40..220 |]
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createMeasureSelect
                    (Terms.``Patient Length`` |> getTerm "lengte" |> (fun s -> $"{s} (cm)"))
                    (pat |> Option.bind (Patient.getHeight >> heightToNone))
                    (fun s ->
                        keepOpen ()
                        s |> Msg.UpdateHeight |> dispatch
                    )

                if
                    pat |> Option.isSome
                    && pat
                       |> Option.map (fun p -> p |> Patient.getAgeInYears |> Option.defaultValue 0. < 1)
                       |> Option.defaultValue false
                then
                    [| 24..42 |]
                    |> Array.map (fun k -> $"{k}", $"{k}")
                    |> createMeasureSelect
                        (Terms.``Patient Age weeks`` |> getTerm "weken" |> (fun s -> $"GA {s}"))
                        (pat |> Option.bind Patient.getGAWeeks |> zeroToNone)
                        (fun s ->
                            keepOpen ()
                            s |> Msg.UpdateGAWeek |> dispatch
                        )

                    [| 1..6 |]
                    |> Array.map (fun k -> $"{k}", $"{k}")
                    |> createMeasureSelect
                        (Terms.``Patient Age days`` |> getTerm "dagen" |> (fun s -> $"GA {s}"))
                        (pat |> Option.bind Patient.getGADays |> zeroToNone)
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

                let accessLabel = Terms.``Patient Access`` |> getTerm "Toegangen"
                let tubeLabel = Terms.``Patient Enteral Tube`` |> getTerm "Sonde"

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

                Patient.RenalFunction.options
                |> Array.map (fun k -> $"{k}", $"{k}")
                |> createSelect
                    (Terms.``Patient Renal Function`` |> getTerm "Nierfunctie")
                    (pat |> Option.bind Patient.getRenalFunction)
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
                <Box onClickCapture={onAttempt} onMouseDownCapture={onAttempt}>
                    <Grid container spacing={2}>
                        {React.Fragment(items1 |> unbox<seq<ReactElement>>)}
                    </Grid>
                    <Grid container spacing={2} sx={ {| marginTop = 2 |} } >
                        {React.Fragment(items2 |> unbox<seq<ReactElement>>)}
                    </Grid>
                </Box>
                {departmentNotice}
                {resetBar}
                {confirmResetDialog}
                {heldDialog}
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
