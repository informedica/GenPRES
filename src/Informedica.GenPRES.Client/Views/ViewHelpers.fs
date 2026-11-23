namespace Views


#nowarn "1104"

module ViewHelpers =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.Policies

    open System
    open Fable.Core
    open Feliz
    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models.Order


    /// Shows a clearable filter select with the options xs, disabled when enabled is false.
    let filterSelectWhen enabled lbl selected dispatch xs =
        Components.PickField.View
            {|
                label = lbl
                options = xs
                selected = selected
                onChange = dispatch
                clearable = true
                isLoading = false
                enabled = enabled
                shape = Components.PickField.Shape.Scroll
            |}


    /// A filter select that can always be used.
    let filterSelect lbl selected dispatch xs = filterSelectWhen true lbl selected dispatch xs


    /// Sends the command of a pick. Without a command the field did not offer the value picked: that is a
    /// bug, written to the console as a warning, and nothing is sent.
    let sendPick send (picked: 'a option) cmd =
        match cmd with
        | Some cmd -> send cmd
        | None -> Logging.warning "a pick the field does not offer is not sent" picked


    /// The severity of a field value and, for a value outside the rules, the reason shown on hover.
    type Mark =
        {|
            severity: Severity
            reason: string option
        |}


    /// No mark: a value within the rules.
    let noMark: Mark =
        {|
            severity = Severity.Normal
            reason = None
        |}


    /// Describes the bound a value crosses, in the unit of the bound and without rounding it. An
    /// inclusive bound reads "max 15 mg" or "min 2 mg", an exclusive bound "< 15 mg" or "> 2 mg".
    /// Returns None when the value is outside the rules without a known bound.
    let reasonText (reason: SeverityReasonPolicy.Reason) =
        let show (b: SeverityReasonPolicy.Bound) =
            $"{b.Value |> Decimal.toStringNumberNLWithoutTrailingZeros} {b.Unit}"

        match reason with
        | SeverityReasonPolicy.Reason.AboveMax b when b.Inclusive -> Some $"max {show b}"
        | SeverityReasonPolicy.Reason.AboveMax b -> Some $"< {show b}"
        | SeverityReasonPolicy.Reason.BelowMin b when b.Inclusive -> Some $"min {show b}"
        | SeverityReasonPolicy.Reason.BelowMin b -> Some $"> {show b}"
        | SeverityReasonPolicy.Reason.Outside -> None


    /// Builds the mark of an order variable: the severity of its level and the bound its values cross.
    let markOf (ovar: OrderVariable) : Mark =
        {|
            severity = ovar.Level |> Severity.ofLevel
            reason = ovar |> SeverityReasonPolicy.ofOrderVariable |> Option.bind reasonText
        |}


    /// The caption of the intake slider and the sentence that tells the percentage of the total, in
    /// the language of getTerm.
    let intakeSliderTexts (getTerm: string -> Terms -> string) : Components.IntakeSlider.Texts =
        {|
            caption =
                Terms.``Nutrition Intake Caption``
                |> getTerm "dose quantity as a percentage of the total"
            shareOfTotal =
                fun pct ->
                    Terms.``Nutrition Share Of Total``
                    |> getTerm "{0}% of total"
                    |> TermText.fill [ pct ]
        |}


    /// The quantity field's texts in the user's language, English where the sheet has no
    /// translation.
    let quantityFieldTexts (getTerm: string -> Terms -> string) : Components.QuantityField.Texts =
        {|
            pickValue = Terms.``Pick a value`` |> getTerm "pick a value"
            pickMedian = Terms.``Pick the median`` |> getTerm "to the median"
            navigableTitles =
                Terms.``Step to minimum`` |> getTerm "to the minimum",
                Terms.``Step lower`` |> getTerm "lower",
                Terms.``Step higher`` |> getTerm "higher",
                Terms.``Step to maximum`` |> getTerm "to the maximum"
            stepableTitles =
                Terms.``Step large down`` |> getTerm "large step down",
                Terms.``Step down`` |> getTerm "step down",
                Terms.``Step up`` |> getTerm "step up",
                Terms.``Step large up`` |> getTerm "large step up"
        |}


    /// A field without step buttons: the value is only chosen from the dropdown.
    let noSteps = Components.QuantityField.Selectable


    /// The recalculation of a field with a single value. allowed is true when the intent policy allows it
    /// for the order shown, reCalc sends it, restore runs when the list closes without a pick, and busy is
    /// true while a request of the page is under way, which blocks the recalculation.
    type ReCalc =
        {|
            allowed: bool
            reCalc: unit -> unit
            restore: unit -> unit
            busy: bool
        |}


    /// Shows a field of an order. It has no clear button, because the solver fills an emptied value again.
    /// Several values open as a list. A single value is recalculated first when the intent policy allows
    /// it, and the list shows the server answer; otherwise the field is read-only. Without reCalc the field
    /// only opens a list of several values.
    let orderField
        (reCalc: ReCalc option)
        (texts: Components.QuantityField.Texts)
        alwaysShow
        disabled
        isLoading
        lbl
        selected
        updateSelected
        mode
        (mark: Mark)
        minWidth
        xs
        =
        // a field with steps is never empty: its buttons can still give it a value
        let hasSteps =
            match mode with
            | Components.QuantityField.Navigable _
            | Components.QuantityField.Stepable _ -> true
            | Components.QuantityField.Selectable
            | Components.QuantityField.Fixed -> false

        if not alwaysShow && xs |> Array.isEmpty && not hasSteps then
            null
        else
            let isEmpty = xs |> Array.isEmpty && not hasSteps

            let shown =
                if xs |> Array.length = 1 then
                    xs[0] |> fst |> Some
                else
                    selected

            // A single range entry counts as no values to list.
            let values =
                match xs with
                | [| ("range", _) |] -> 0
                | _ -> xs.Length

            let allowed = reCalc |> Option.exists _.allowed

            let selectable = FieldOpenPolicy.orderVariable values allowed (not (disabled || isEmpty))

            let trace event effects =
                StepTrail.event
                    "Field"
                    $"%s{event} %s{lbl}"
                    effects
                    (Components.SimpleSelect.fieldState shown values false)

            let reCalcField =
                match selectable, reCalc with
                | FieldOpenPolicy.Selectable.ReCalc, Some r ->
                    Some(fun () ->
                        trace "open" [ "reCalc" ]
                        r.reCalc ()
                    )
                | _ -> None

            let onChange (value: string option) =
                match value with
                | Some _ -> trace "pick" [ "onChange" ]
                | None -> trace "clear" [ "onChange none" ]

                updateSelected value

            // The server decides which field is the lead, so no field is marked as the lead here.
            Components.QuantityField.View
                {|
                    onChange = if isEmpty then ignore else onChange
                    label = lbl
                    selected = shown
                    values = xs
                    isLoading = isLoading
                    disabled = disabled || isEmpty
                    hasClear = false
                    readOnly = selectable = FieldOpenPolicy.Selectable.No && not (disabled || isEmpty)
                    reCalc = reCalcField
                    restore = reCalc |> Option.map _.restore |> Option.defaultValue ignore
                    busy = reCalc |> Option.exists _.busy
                    severity = mark.severity
                    reason = mark.reason
                    mode = mode
                    texts = texts
                    minWidth = minWidth
                    isLead = false
                |}


    /// A value the rules narrowed, which the user may narrow further, or recalculate when it holds one.
    let orderSelect
        texts
        alwaysShow
        disabled
        isLoading
        lbl
        selected
        updateSelected
        mode
        (mark: Mark)
        reCalc
        minWidth
        xs
        =
        orderField (Some reCalc) texts alwaysShow disabled isLoading lbl selected updateSelected mode mark minWidth xs


    /// A field that is never recalculated: a choice among the parts of the order, or a value that is only
    /// shown.
    let orderFixed texts alwaysShow disabled isLoading lbl selected updateSelected mode (mark: Mark) minWidth xs =
        orderField None texts alwaysShow disabled isLoading lbl selected updateSelected mode mark minWidth xs


    /// The field mode of a decided quantity mode: Navigable and Stepable carry the steps,
    /// Selectable and Fixed have none.
    let stepsMode (mode: QuantityModePolicy.Mode) (steps: Components.QuantityField.Steps) =
        match mode with
        | QuantityModePolicy.Mode.Selectable -> Components.QuantityField.Selectable
        | QuantityModePolicy.Mode.Fixed -> Components.QuantityField.Fixed
        | QuantityModePolicy.Mode.Navigable -> Components.QuantityField.Navigable steps
        | QuantityModePolicy.Mode.Stepable -> Components.QuantityField.Stepable steps


    /// Build the steps of a field for its decided mode. Navigable: first and last jump to the
    /// min and the max, and a click on the range picks the median; Stepable: decrease and
    /// increase step the value, and first and last make a large step. hasLarge: the large step
    /// differs from the small one; without it a stepable field shows only the inner buttons.
    let createStepper
        dispatch
        (mode: QuantityModePolicy.Mode)
        hasLarge
        setMin
        (decr: int * bool -> 'Msg)
        setMed
        (incr: int * bool -> 'Msg)
        setMax
        step
        large
        =
        let navigable = mode = QuantityModePolicy.Mode.Navigable
        let solved = mode = QuantityModePolicy.Mode.Stepable

        {|
            step = step
            large = large
            hasLarge = hasLarge
            first =
                if navigable then
                    (fun (_: int) -> setMin |> dispatch) |> Some
                elif solved then
                    (fun n -> (n, true) |> decr |> dispatch) |> Some
                else
                    None
            decrease =
                if solved then
                    (fun n -> (n, false) |> decr |> dispatch) |> Some
                else
                    None
            median =
                if navigable then
                    (fun () -> setMed |> dispatch) |> Some
                else
                    None
            increase =
                if solved then
                    (fun n -> (n, false) |> incr |> dispatch) |> Some
                else
                    None
            last =
                if navigable then
                    (fun (_: int) -> setMax |> dispatch) |> Some
                elif solved then
                    (fun n -> (n, true) |> incr |> dispatch) |> Some
                else
                    None
            useDebounce = not navigable && solved
        |}
        |> stepsMode mode


    let ovarLabel (name: string) (ovar: OrderVariable) =
        ovar.Variable.Vals
        |> Option.map (fun v -> $"{name} ({v.Unit})")
        |> Option.defaultValue name


    let ovarVals (format: decimal -> string) (ovar: OrderVariable) =
        ovar.Variable.Vals
        |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> format} {v.Unit}"))
        |> Option.defaultValue [||]


    let ovarValsWithRange (format: decimal -> string) (prec: int) (ovar: OrderVariable) =
        ovar.Variable.Vals
        |> Option.map (fun v -> v.Value |> Array.map (fun (s, d) -> s, $"{d |> format} {v.Unit}"))
        |> Option.defaultValue (
            match OrderDisplay.Variable.renderValue prec ovar.Variable with
            | "" -> [||]
            | s -> [| "range", s |]
        )


    /// The value of the first (key, value) pair of a ValueUnit, or None when it is empty.
    let firstSnd (vu: Types.ValueUnit) = vu.Value |> Array.tryHead |> Option.map snd


    /// The defined (small-step) increment: the defined constraint's increment, else the
    /// solved variable's own increment.
    let definedIncrement (ovar: OrderVariable) : decimal option =
        [ ovar.DefinedConstraints.Incr; ovar.Variable.Incr ]
        |> List.tryPick (Option.bind firstSnd)


    /// The large step of a value as its buttons show it: the server's large increment, or the
    /// defined increment when the server sends none.
    let largeStepText (ovar: OrderVariable) =
        ovar.LargeIncr
        |> Option.bind firstSnd
        |> Option.orElse (definedIncrement ovar)
        |> Option.map Decimal.toStringNumberNLWithoutTrailingZeros


    /// Whether the large step of a value differs from its small one: the server's large
    /// increment, when it sends one, against the defined increment.
    let hasLargeStep (ovar: OrderVariable) =
        match ovar.LargeIncr |> Option.bind firstSnd, definedIncrement ovar with
        | Some large, Some small -> large <> small
        | Some _, None -> true
        | None, _ -> false


    /// Builds the step function of a solved order variable. It takes the net clicks of the small step (the
    /// defined increment) and of the large step (LargeIncr), and returns the key and label of the predicted
    /// value. Like the server step, the prediction is not bounded by the defined range, which would make it
    /// lower than the server answer. Only the ceiling, which the solver enforces, and a floor of one
    /// increment apply. Returns None without a current value or a positive defined increment.
    let ovarStepTo
        (ceiling: decimal option)
        (format: decimal -> string)
        (ovar: OrderVariable)
        : (int * int -> string * string) option
        =
        let definedIncr = definedIncrement ovar

        match ovar.Variable.Vals, definedIncr with
        | Some vals, Some smallIncr when smallIncr > 0M ->
            match firstSnd vals with
            | Some cur ->
                let unit = vals.Unit

                // The large step uses LargeIncr, or the defined increment when the server sends none.
                let largeIncr = ovar.LargeIncr |> Option.bind firstSnd |> Option.defaultValue smallIncr

                // The server step has no upper bound, so only the ceiling and a floor of one increment
                // apply. The server enforces both; the floor keeps the value above zero.
                let applyBounds v =
                    let v = ceiling |> Option.map (min v) |> Option.defaultValue v
                    max v smallIncr

                (fun (smallDelta, largeDelta) ->
                    // The server sends each value as a decimal converted from a float, so the value has about
                    // 15 significant digits. Adding the increments to the current value can then give noise,
                    // such as 0.2499999999999998 for 0.25. Rounding to 10 decimals removes the noise.
                    let next =
                        cur + decimal smallDelta * smallIncr + decimal largeDelta * largeIncr
                        |> applyBounds
                        |> fun v -> Math.Round(v, 10)

                    string next, $"{format next} {unit}"
                )
                |> Some
            | None -> None
        | _ -> None


    /// <summary>Like <see cref="ovarStepTo"/> without a ceiling.</summary>
    let ovarStep (format: decimal -> string) (ovar: OrderVariable) : (int * int -> string * string) option =
        ovarStepTo None format ovar


    /// The ceiling of the orderable dose quantity: the largest prepared orderable quantity when the
    /// orderable has more than one component, because the components cannot grow with the dose. Returns
    /// None for a single component, whose orderable quantity follows the dose, or without a quantity.
    let orderableDoseQuantityCeiling (ord: Types.Order) : decimal option =
        if ord.Orderable.Components |> Array.length > 1 then
            ord.Orderable.OrderableQuantity.Variable.Vals
            |> Option.bind (fun vu ->
                if vu.Value |> Array.isEmpty then
                    None
                else
                    vu.Value |> Array.map snd |> Array.max |> Some
            )
        else
            None


    /// Counts the whole steps of incr between the current value and the ceiling, at least 0. Returns None
    /// without a ceiling, a current value or a positive increment.
    let stepsToCeiling (ceiling: decimal option) (incr: decimal option) (ovar: OrderVariable) : int option =
        match ceiling, ovar.Variable.Vals |> Option.bind firstSnd, incr with
        | Some ceil, Some cur, Some i when i > 0M -> System.Math.Floor((ceil - cur) / i) |> int |> max 0 |> Some
        | _ -> None


    /// Counts the defined-increment steps that fit below the ceiling. An increase capped at this count
    /// does not pass the ceiling, which the solver would reject by reverting the value. Returns 0 at the
    /// maximum, and None without a ceiling or a positive increment.
    let incrementStepsToCeiling (ceiling: decimal option) (ovar: OrderVariable) : int option =
        ovar |> stepsToCeiling ceiling (definedIncrement ovar)


    /// <summary>
    /// Like <see cref="incrementStepsToCeiling"/>, but counts steps of LargeIncr, or of the defined
    /// increment when the server sends none.
    /// </summary>
    let largeIncrementStepsToCeiling (ceiling: decimal option) (ovar: OrderVariable) : int option =
        let largeIncr = ovar.LargeIncr |> Option.bind firstSnd |> Option.orElse (definedIncrement ovar)

        ovar |> stepsToCeiling ceiling largeIncr


    /// The steps of a frequency field: one increment per click, so no large step and no step
    /// text. The five messages come from the caller, as for createStepper.
    let frequencyStepper dispatch mode setMin (decr: 'Msg) setMed (incr: 'Msg) setMax =
        createStepper dispatch mode false setMin (fun _ -> decr) setMed (fun _ -> incr) setMax None None


    /// The steps of the orderable dose rate field, its large step and step text taken from the
    /// rate. The five messages come from the caller, as for createStepper.
    let doseRateStepper dispatch mode (rate: OrderVariable) setMin decr setMed incr setMax =
        createStepper
            dispatch
            mode
            (rate |> hasLargeStep)
            setMin
            decr
            setMed
            incr
            setMax
            (rate |> ovarStep string)
            (rate |> largeStepText)


    /// Builds the steps and the mode of the orderable dose quantity field for the Order and Nutrition
    /// views. The value shown follows the clicks up to the ceiling, and the steps sent are capped at the
    /// ceiling, so the solver does not revert them. QuantityModePolicy decides the mode, which has no steps
    /// unless every component has a single orderable quantity. The caller passes the five messages.
    let createDoseQtyStepper
        dispatch
        (ord: Order)
        (setMin: 'Msg)
        (decr: int * bool -> 'Msg)
        (setMed: 'Msg)
        (incr: int * bool -> 'Msg)
        (setMax: 'Msg)
        =
        let mode =
            ord.Orderable.Dose.Quantity
            |> QuantityModePolicy.decideFor QuantityModePolicy.Field.DoseQuantity ord

        match mode with
        | QuantityModePolicy.Mode.Selectable -> Components.QuantityField.Selectable
        | QuantityModePolicy.Mode.Fixed -> Components.QuantityField.Fixed
        | QuantityModePolicy.Mode.Navigable
        | QuantityModePolicy.Mode.Stepable ->
            let canIncr =
                ord.Orderable.Components |> Array.length = 1
                || ord.Orderable.DoseCount.Variable.Vals
                   |> Option.map (fun vu -> vu.Value |> Array.map snd |> Array.forall (fun v -> v > 1m))
                   |> Option.defaultValue false

            let navigable = mode = QuantityModePolicy.Mode.Navigable
            let solved = mode = QuantityModePolicy.Mode.Stepable

            // A multi-component orderable has the prepared orderable quantity as ceiling; a single
            // component has none. An increase past the ceiling would be reverted by the solver.
            let doseQtyCeiling = ord |> orderableDoseQuantityCeiling

            let saturateInc n =
                ord.Orderable.Dose.Quantity
                |> incrementStepsToCeiling doseQtyCeiling
                |> Option.map (min n)
                |> Option.defaultValue n

            // Caps the large steps sent at the ceiling, like saturateInc.
            let saturateLarge n =
                ord.Orderable.Dose.Quantity
                |> largeIncrementStepsToCeiling doseQtyCeiling
                |> Option.map (min n)
                |> Option.defaultValue n

            // The increment grid seldom lands on the ceiling, so the server value stays just below it and
            // DoseCount stays above 1. Without this check the increase buttons stay enabled while the field
            // shows the maximum. With a ceiling, an increase needs one more step to fit; when the steps
            // cannot be counted, the buttons are disabled. Without a ceiling they stay enabled.
            let canStepUp =
                match doseQtyCeiling with
                | None -> true
                | Some _ ->
                    ord.Orderable.Dose.Quantity
                    |> incrementStepsToCeiling doseQtyCeiling
                    |> Option.exists (fun steps -> steps > 0)

            // Like canStepUp, for the large increment.
            let canStepUpLarge =
                match doseQtyCeiling with
                | None -> true
                | Some _ ->
                    ord.Orderable.Dose.Quantity
                    |> largeIncrementStepsToCeiling doseQtyCeiling
                    |> Option.exists (fun steps -> steps > 0)

            {|
                step = ord.Orderable.Dose.Quantity |> ovarStepTo doseQtyCeiling string
                large = ord.Orderable.Dose.Quantity |> largeStepText
                hasLarge = ord.Orderable.Dose.Quantity |> hasLargeStep
                first =
                    if navigable then
                        (fun (_: int) -> setMin |> dispatch) |> Some
                    elif solved then
                        (fun n -> (n, true) |> decr |> dispatch) |> Some
                    else
                        None
                decrease =
                    if solved then
                        (fun n -> (n, false) |> decr |> dispatch) |> Some
                    else
                        None
                median =
                    if navigable then
                        (fun () -> setMed |> dispatch) |> Some
                    else
                        None
                increase =
                    if solved && canIncr && canStepUp then
                        (fun n -> (saturateInc n, false) |> incr |> dispatch) |> Some
                    else
                        None
                last =
                    if navigable then
                        (fun (_: int) -> setMax |> dispatch) |> Some
                    elif solved && canIncr && canStepUpLarge then
                        (fun n -> (saturateLarge n, true) |> incr |> dispatch) |> Some
                    else
                        None
                useDebounce = not navigable && solved
            |}
            |> stepsMode mode


    /// Shows an order variable that cannot be changed, or its range when it has no value. The caller
    /// passes orderFixed as display.
    let ovarDisplay display (name: string) (format: decimal -> string) minWidth (ovar: OrderVariable) =
        let mark = ovar |> markOf
        let label = ovar |> ovarLabel name
        let vals = ovar |> ovarValsWithRange format 3
        display false label None ignore noSteps mark minWidth vals


    /// A filter select that the user types into instead of scrolling, for a long list.
    let autoCompleteWhen enabled lbl selected dispatch xs =
        Components.PickField.View
            {|
                label = lbl
                options = xs |> Array.map (fun s -> s, s)
                selected = selected
                onChange = dispatch
                clearable = true
                isLoading = false
                enabled = enabled
                shape = Components.PickField.Shape.Type
            |}


    /// The typed filter select that can always be used.
    let autoComplete lbl selected dispatch xs = autoCompleteWhen true lbl selected dispatch xs


    /// A filter, enabled or not, scrolled on a small screen and typed into on a large one.
    let responsiveFilterWhen enabled isMobile lbl selected dispatch xs =
        if isMobile then
            xs
            |> Array.map (fun s -> s, s)
            |> filterSelectWhen enabled lbl selected dispatch
        else
            xs |> autoCompleteWhen enabled lbl selected dispatch


    /// A filter that can always be used, scrolled on a small screen and typed into on a large one.
    let responsiveFilter isMobile lbl selected dispatch xs = responsiveFilterWhen true isMobile lbl selected dispatch xs


    /// The text of an item, without the emphasis it is drawn with.
    let textItemText item =
        match item with
        | Normal s
        | Bold s
        | Italic s -> s


    /// The text of a block, its items joined, without their emphasis or the block's severity.
    let textBlockText block =
        block |> Severity.items |> Array.map textItemText |> String.concat ""


    /// The command that picks the nth value of the variable the target addresses.
    let setNthCommand target n =
        match target with
        | Models.OrderContext.Target.Schedule prop -> Api.OrderViewCommand.SetNthScheduleProperty(prop, n)
        | Models.OrderContext.Target.Orderable prop -> Api.OrderViewCommand.SetNthOrderableProperty(prop, n)
        | Models.OrderContext.Target.Component(cmp, prop) -> Api.OrderViewCommand.SetNthComponentProperty(cmp, prop, n)
        | Models.OrderContext.Target.Item(cmp, itm, prop) -> Api.OrderViewCommand.SetNthItemProperty(cmp, itm, prop, n)


    /// The command that recalculates the variable the target addresses.
    let reCalcCommand target =
        match target with
        | Models.OrderContext.Target.Schedule prop -> Api.OrderViewCommand.ReCalcScheduleProperty prop
        | Models.OrderContext.Target.Orderable prop -> Api.OrderViewCommand.ReCalcOrderableProperty prop
        | Models.OrderContext.Target.Component(cmp, prop) -> Api.OrderViewCommand.ReCalcComponentProperty(cmp, prop)
        | Models.OrderContext.Target.Item(cmp, itm, prop) -> Api.OrderViewCommand.ReCalcItemProperty(cmp, itm, prop)


    /// A small spinner below a form's button while its request is under way.
    let busyProgress isBusy =
        if isBusy then
            let progressSx = {| marginTop = 2 |}

            JSX.jsx
                $"""
            import CircularProgress from '@mui/material/CircularProgress';
            import Box from '@mui/material/Box';
            <Box sx={progressSx}>
                <CircularProgress size={24} />
            </Box>
            """
        else
            null


    let circularProgress =
        let circularProgressSx =
            {|
                marginTop = 5
                display = "flex"
                padding = 20
            |}

        JSX.jsx
            $"""
        import CircularProgress from '@mui/material/CircularProgress';
        import Box from '@mui/material/Box';
        <Box sx={circularProgressSx}>
            <CircularProgress />
        </Box>
        """


    let backdropProgress isOpen (message: string) =
        let backdropBoxSx =
            {|
                display = "flex"
                flexDirection = "column"
                alignItems = "center"
                gap = 2
            |}

        let backdropSx =
            {|
                color = "#fff"
                zIndex = 9999
            |}

        JSX.jsx
            $"""
        import Backdrop from '@mui/material/Backdrop';
        import CircularProgress from '@mui/material/CircularProgress';
        import Box from '@mui/material/Box';
        import Typography from '@mui/material/Typography';

        <Backdrop
            sx={backdropSx}
            open={isOpen}>
            <Box sx={backdropBoxSx}>
                <CircularProgress color="inherit" />
                <Typography variant="h6" color="inherit">
                    {message}
                </Typography>
            </Box>
        </Backdrop>
        """


    module PrintView =


        let appBarSx = {| ``@media print`` = {| display = "none" |} |}


        let printSx =
            {|
                padding = 3
                ``@media print`` = {| padding = 1 |}
            |}


        let headerCellSx =
            {|
                fontWeight = "bold"
                borderBottom = "none"
                paddingY = "2px"
                width = "25%"
            |}


        let valueCellSx =
            {|
                borderBottom = "1px dotted #ccc"
                paddingY = "2px"
                width = "25%"
            |}


        /// The weight of the patient for the print, or the text unknown when there is no weight.
        let patientWeight (unknown: string) (patient: PatientData option) =
            patient
            |> Option.bind PatientRead.getWeightInKg
            |> Option.map (fun w ->
                let s = decimal w |> Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision 1
                s + " kg"
            )
            |> Option.defaultValue unknown


        /// The heading of a section on paper: bold on a light band.
        [<JSX.Component>]
        let SectionHeader
            (props:
                {|
                    label: string
                    marginBottom: int
                |})
            =
            let headerSx =
                {|
                    fontWeight = "bold"
                    backgroundColor = "#f5f5f5"
                    padding = "4px 8px"
                    borderRadius = 1
                    marginBottom = props.marginBottom
                |}

            JSX.jsx
                $"""
            import Typography from '@mui/material/Typography';

            <Typography variant="subtitle1" sx={headerSx}>
                {props.label}
            </Typography>
            """


        [<JSX.Component>]
        let PatientHeader
            (props:
                {|
                    weightKg: string
                    labels: Global.PrintLabels
                |})
            =
            let labels = props.labels

            let currentDate =
                let dt = DateTime.Now
                let pad (n: int) = if n < 10 then $"0{n}" else $"{n}"
                $"{pad dt.Day} - {pad dt.Month} - {dt.Year}"

            let patientTableSx =
                {|
                    tableLayout = "fixed"
                    width = "100%"
                    marginBottom = 3
                |}

            JSX.jsx
                $"""
            import Table from '@mui/material/Table';
            import TableBody from '@mui/material/TableBody';
            import TableRow from '@mui/material/TableRow';
            import TableCell from '@mui/material/TableCell';

            <Table size="small" sx={patientTableSx}>
                <TableBody>
                    <TableRow>
                        <TableCell sx={headerCellSx}>{labels.date}</TableCell>
                        <TableCell sx={valueCellSx}>{currentDate}</TableCell>
                        <TableCell sx={headerCellSx}>{labels.patientNumber}</TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                    </TableRow>
                    <TableRow>
                        <TableCell sx={headerCellSx}>{labels.department}</TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                        <TableCell sx={headerCellSx}>{labels.name}</TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                    </TableRow>
                    <TableRow>
                        <TableCell sx={headerCellSx}>{labels.bed}</TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                        <TableCell sx={headerCellSx}>{labels.birthDate}</TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                    </TableRow>
                    <TableRow>
                        <TableCell sx={headerCellSx}>{labels.physician}</TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                        <TableCell sx={headerCellSx}>{labels.weight}</TableCell>
                        <TableCell sx={valueCellSx}>{props.weightKg}</TableCell>
                    </TableRow>
                    <TableRow>
                        <TableCell sx={headerCellSx}>{labels.pager}</TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                        <TableCell sx={headerCellSx}></TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                    </TableRow>
                </TableBody>
            </Table>
            """


        [<JSX.Component>]
        let PatientSignature (props: {| label: string |}) =
            let signatureSx =
                {|
                    marginTop = 4
                    borderTop = "1px solid #ccc"
                    paddingTop = 2
                |}

            JSX.jsx
                $"""
            import Box from '@mui/material/Box';
            import Typography from '@mui/material/Typography';

            <Box sx={signatureSx}>
                <Typography variant="body2">{props.label}</Typography>
            </Box>
            """


        [<JSX.Component>]
        let PrintDialog
            (props:
                {|
                    isOpen: bool
                    onClose: unit -> unit
                    title: string
                    children: ReactElement
                |})
            =
            let handlePrint = fun _ -> Browser.Dom.window.print ()

            let isOpen = props.isOpen

            let titleSx =
                {|
                    marginLeft = 2
                    flex = 1
                |}

            JSX.jsx
                $"""
            import Dialog from '@mui/material/Dialog';
            import AppBar from '@mui/material/AppBar';
            import Toolbar from '@mui/material/Toolbar';
            import IconButton from '@mui/material/IconButton';
            import Button from '@mui/material/Button';
            import Typography from '@mui/material/Typography';
            import Box from '@mui/material/Box';
            import CloseIcon from '@mui/icons-material/Close';
            import PrintIcon from '@mui/icons-material/Print';

            <Dialog fullScreen open={isOpen} onClose={fun _ -> props.onClose ()}>
                    <AppBar sx={appBarSx} position="static">
                        <Toolbar>
                            <IconButton edge="start" color="inherit" onClick={fun _ -> props.onClose ()} aria-label="close">
                                <CloseIcon />
                            </IconButton>
                            <Typography sx={titleSx} variant="h6" component="div">
                                {props.title}
                            </Typography>
                            <Button color="inherit" onClick={handlePrint} startIcon={{ <PrintIcon /> }}>
                                Print
                            </Button>
                        </Toolbar>
                    </AppBar>
                    <Box sx={printSx}>
                        {props.children}
                    </Box>
                </Dialog>
                """
