namespace Views

#nowarn "1104"

module ViewHelpers =

    open System
    open Fable.Core
    open Feliz
    open Shared
    open Shared.Types
    open Shared.Models.Order


    /// A filter select: what it offers is the pick rule's answer.
    let filterSelect disabled isLoading lbl selected dispatch xs =
        Components.PickField.View
            {|
                label = lbl
                options = xs
                selected = selected
                onChange = dispatch
                clearable = true
                isLoading = isLoading
                enabled = not disabled
                shape = Components.PickField.Shape.Scroll
            |}


    /// What a field shows of a value outside the rules: its severity, and the reason as the
    /// field words it on hover.
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


    /// The reason in words: the bound crossed, in the value's unit and at the bound's own
    /// precision, so it is the bound the value crossed and not a rounding of it. A bound that
    /// is itself allowed reads "max 15 mg" or "min 2 mg"; one that is not reads "< 15 mg" or
    /// "> 2 mg". Nothing for a mark the bounds do not explain.
    let reasonText (reason: SeverityReasonPolicy.Reason) =
        let show (b: SeverityReasonPolicy.Bound) =
            $"{b.Value |> Decimal.toStringNumberNLWithoutTrailingZeros} {b.Unit}"

        match reason with
        | SeverityReasonPolicy.Reason.AboveMax b when b.Inclusive -> Some $"max {show b}"
        | SeverityReasonPolicy.Reason.AboveMax b -> Some $"< {show b}"
        | SeverityReasonPolicy.Reason.BelowMin b when b.Inclusive -> Some $"min {show b}"
        | SeverityReasonPolicy.Reason.BelowMin b -> Some $"> {show b}"
        | SeverityReasonPolicy.Reason.Outside -> None


    /// The mark an order variable carries: its level as a severity, and the bound its values
    /// cross as the reason.
    let markOf (ovar: OrderVariable) : Mark =
        {|
            severity = ovar.Level |> Models.Severity.ofLevel
            reason = ovar |> SeverityReasonPolicy.ofOrderVariable |> Option.bind reasonText
        |}


    /// The quantity field's texts in the user's language, today's Dutch where the sheet has no
    /// translation.
    let quantityFieldTexts (getTerm: string -> Terms -> string) : Components.QuantityField.Texts =
        {|
            pickValue = Terms.``Pick a value`` |> getTerm "kies een waarde"
            pickMedian = Terms.``Pick the median`` |> getTerm "naar mediaan"
            navigableTitles =
                Terms.``Step to minimum`` |> getTerm "naar minimum",
                Terms.``Step lower`` |> getTerm "lager",
                Terms.``Step higher`` |> getTerm "hoger",
                Terms.``Step to maximum`` |> getTerm "naar maximum"
            stepableTitles =
                Terms.``Step large down`` |> getTerm "grote stap omlaag",
                Terms.``Step down`` |> getTerm "stap omlaag",
                Terms.``Step up`` |> getTerm "stap omhoog",
                Terms.``Step large up`` |> getTerm "grote stap omhoog"
        |}


    /// A field without step buttons: the value is only chosen from the dropdown.
    let noSteps = Components.QuantityField.Selectable


    /// How a field the user may have narrowed reopens: whether the user constrained its variable,
    /// what the page does before the field's choice is cleared, what it does when the list of a
    /// reopen closes without a pick, and whether a request of the page is under way, during which
    /// the arrow does not reopen.
    type Reopen =
        {|
            constrained: FieldOpenPolicy.Constrained
            reopening: unit -> unit
            restore: unit -> unit
            busy: bool
        |}


    /// A field of an order. It never offers the cross, since the solver fills an emptied value
    /// again; the arrow opens the list, and on a value the user narrowed it clears that value
    /// first and the list shows what the server answers. A value the solver determined has no
    /// arrow. A field without a reopen is one whose values are the order's own parts or only
    /// shown: it opens a list of several, never reopens.
    let orderField
        (reopen: Reopen option)
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

            // a lone range holds no value to list
            let values =
                match xs with
                | [| ("range", _) |] -> 0
                | _ -> xs.Length

            let policyMode =
                match mode with
                | Components.QuantityField.Selectable -> QuantityModePolicy.Mode.Selectable
                | Components.QuantityField.Navigable _ -> QuantityModePolicy.Mode.Navigable
                | Components.QuantityField.Stepable _ -> QuantityModePolicy.Mode.Stepable
                | Components.QuantityField.Fixed -> QuantityModePolicy.Mode.Fixed

            let constrained =
                reopen
                |> Option.map _.constrained
                |> Option.defaultValue FieldOpenPolicy.Constrained.No

            let offer = FieldOpenPolicy.orderVariable values policyMode constrained (not (disabled || isEmpty))

            let trace event effects =
                StepTrail.event
                    "Field"
                    $"%s{event} %s{lbl}"
                    effects
                    (Components.SimpleSelect.fieldState shown values false)

            let reopenField =
                match offer.Arrow, reopen with
                | FieldOpenPolicy.Arrow.ReopenCleared, Some r ->
                    Some(fun () ->
                        trace "open" [ "reopen" ]
                        r.reopening ()
                        updateSelected None
                    )
                | _ -> None

            let onChange (value: string option) =
                match value with
                | Some _ -> trace "pick" [ "onChange" ]
                | None -> trace "clear" [ "onChange none" ]

                updateSelected value

            // no field is the lead yet: which one the user starts from is the server's to say
            Components.QuantityField.View
                {|
                    onChange = if isEmpty then ignore else onChange
                    label = lbl
                    selected = shown
                    values = xs
                    isLoading = isLoading
                    disabled = disabled || isEmpty
                    hasClear = false
                    readOnly = offer.Arrow = FieldOpenPolicy.Arrow.NoArrow && not (disabled || isEmpty)
                    reopen = reopenField
                    restore = reopen |> Option.map _.restore |> Option.defaultValue ignore
                    busy = reopen |> Option.exists _.busy
                    severity = mark.severity
                    reason = mark.reason
                    mode = mode
                    texts = texts
                    minWidth = minWidth
                    isLead = false
                |}


    /// A value the rules narrowed, which the user may narrow further and reopen by its arrow.
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
        reopen
        minWidth
        xs
        =
        orderField (Some reopen) texts alwaysShow disabled isLoading lbl selected updateSelected mode mark minWidth xs


    /// A field that never reopens: a choice among the order's own parts, which always holds one of
    /// them, or a value that is only shown.
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
        revision
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
            revision = revision
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
            match Variable.renderValue prec ovar.Variable with
            | "" -> [||]
            | s -> [| "range", s |]
        )


    /// The value of the first (key, value) pair of a ValueUnit, if any. Shared helper for
    /// the increment/step calculations below.
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


    /// Build a per-click step function for a solved order variable. Given the net small-step
    /// click delta (single-step buttons, the DEFINED increment) and the net large-step click
    /// delta (jump buttons, the server's CALCULATED LargeIncr) it returns the (key, label) of
    /// the predicted value. This mirrors the server step (Informedica.GenORDER.Lib.OrderVariable.step),
    /// which moves freely along the increment grid with NO upper bound and is then re-solved —
    /// so the optimistic value is deliberately NOT bounded to the defined range (doing so would
    /// make it undershoot what the server returns). The only ceiling applied is the structural
    /// feasibility `ceiling`: a bound the solver genuinely enforces (e.g. a
    /// multi-component dose quantity cannot exceed the prepared orderable quantity). A floor of
    /// one increment mirrors the server keeping the value non-zero positive. Returns None when
    /// no increment is available (cannot step locally).
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

                // The large step follows the server-provided LargeIncr; when the server emits
                // none, it falls back to the defined increment (behaving like the small step).
                let largeIncr = ovar.LargeIncr |> Option.bind firstSnd |> Option.defaultValue smallIncr

                // The server's step applies no upper bound (it explores freely and re-solves),
                // so the prediction follows the increment grid freely. Apply only the structural
                // feasibility ceiling plus a one-increment floor — both of which the server
                // genuinely enforces (the floor keeps the value non-zero positive).
                let applyBounds v =
                    let v = ceiling |> Option.map (min v) |> Option.defaultValue v
                    max v smallIncr

                (fun (smallDelta, largeDelta) ->
                    let next =
                        cur + decimal smallDelta * smallIncr + decimal largeDelta * largeIncr
                        |> applyBounds

                    string next, $"{format next} {unit}"
                )
                |> Some
            | None -> None
        | _ -> None


    /// <summary>
    /// Like <see cref="ovarStepTo"/> but without a feasibility ceiling — the prediction
    /// follows the increment grid freely (mirroring the server's unbounded step).
    /// </summary>
    let ovarStep (format: decimal -> string) (ovar: OrderVariable) : (int * int -> string * string) option =
        ovarStepTo None format ovar


    /// Upper bound for the orderable dose quantity. For a multi-component orderable the
    /// dose quantity cannot exceed the prepared orderable quantity ("you cannot give more
    /// than you have", and the individual components cannot be grown to follow a larger
    /// dose). For a single component the orderable quantity follows the dose, so there is
    /// no extra ceiling and stepping stays unconstrained (beyond the defined range).
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


    /// How many whole `incr`-sized steps fit between the variable's current value and a
    /// feasibility ceiling, never negative. Shared core of incrementStepsToCeiling and
    /// largeIncrementStepsToCeiling — the two differ only in which increment they pass.
    let stepsToCeiling (ceiling: decimal option) (incr: decimal option) (ovar: OrderVariable) : int option =
        match ceiling, ovar.Variable.Vals |> Option.bind firstSnd, incr with
        | Some ceil, Some cur, Some i when i > 0M -> System.Math.Floor((ceil - cur) / i) |> int |> max 0 |> Some
        | _ -> None


    /// How many whole defined-increment steps fit between the current value and a
    /// feasibility ceiling. Used to cap an increase so it lands on the last step below the
    /// ceiling rather than overshooting it — an overshoot is rejected by the solver, which
    /// reverts the value to where it started. Returns 0 when less than one step fits (already
    /// at the max). None when there is no ceiling or no usable increment.
    let incrementStepsToCeiling (ceiling: decimal option) (ovar: OrderVariable) : int option =
        ovar |> stepsToCeiling ceiling (definedIncrement ovar)


    /// <summary>
    /// Like <see cref="incrementStepsToCeiling"/> but counts steps of the LARGE increment
    /// (the server's calculated LargeIncr used by the jump buttons), falling back to the
    /// defined increment when the server emits none. Lets a large step saturate at the
    /// feasibility ceiling exactly like a small step, so the larger increment does not
    /// overshoot the ceiling and get reverted by the solver. None when there is no ceiling
    /// or no usable increment.
    /// </summary>
    let largeIncrementStepsToCeiling (ceiling: decimal option) (ovar: OrderVariable) : int option =
        let largeIncr = ovar.LargeIncr |> Option.bind firstSnd |> Option.orElse (definedIncrement ovar)

        ovar |> stepsToCeiling ceiling largeIncr


    /// The steps of a frequency field: one increment per click, so no large step and no step
    /// text. The five messages come from the caller, as for createStepper.
    let frequencyStepper dispatch revision mode setMin (decr: 'Msg) setMed (incr: 'Msg) setMax =
        createStepper dispatch revision mode false setMin (fun _ -> decr) setMed (fun _ -> incr) setMax None None


    /// The steps of the orderable dose rate field, its large step and step text taken from the
    /// rate. The five messages come from the caller, as for createStepper.
    let doseRateStepper dispatch revision mode (rate: OrderVariable) setMin decr setMed incr setMax =
        createStepper
            dispatch
            revision
            mode
            (rate |> hasLargeStep)
            setMin
            decr
            setMed
            incr
            setMax
            (rate |> ovarStep string)
            (rate |> largeStepText)


    /// Build the steps and the mode of the orderable dose-quantity select, shared by the Order
    /// and Nutrition views. Handles the optimistic stepping with feasibility-ceiling
    /// saturation: the displayed value follows the click count up to the prepared orderable
    /// quantity, and dispatched steps are saturated at that ceiling so an overshoot is not
    /// reverted by the solver. The five message constructors (setMin/decr/setMed/incr/setMax)
    /// are supplied by each view from its own Msg type. The mode is the quantity mode rule's,
    /// which gives no steps unless every component has a single orderable quantity.
    let createDoseQtyStepper
        dispatch
        revision
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

            // For a multi-component orderable the dose quantity cannot exceed the prepared
            // orderable quantity. Use it as a feasibility ceiling: the optimistic value stays
            // within it, and an overflowing increase is saturated at the max (saturateInc)
            // instead of overshooting, which the solver would reject — reverting the value.
            // Single component: orderable quantity follows the dose, so no ceiling.
            let doseQtyCeiling = ord |> orderableDoseQuantityCeiling

            let saturateInc n =
                ord.Orderable.Dose.Quantity
                |> incrementStepsToCeiling doseQtyCeiling
                |> Option.map (min n)
                |> Option.defaultValue n

            // Large-step counterpart of saturateInc: clamp the dispatched large steps so the
            // larger increment lands on the last grid point at or below the ceiling instead of
            // overshooting and being reverted by the solver.
            let saturateLarge n =
                ord.Orderable.Dose.Quantity
                |> largeIncrementStepsToCeiling doseQtyCeiling
                |> Option.map (min n)
                |> Option.defaultValue n

            // Whether a full defined-increment step still fits below the feasibility ceiling.
            // The increment grid cannot generally land exactly on the ceiling, so the server
            // value settles just below it while the optimistic display clamps to the ceiling —
            // leaving DoseCount > 1 (so canIncr stays true) and the increase buttons permanently
            // active despite the field showing the max. Gating on remaining ceiling room
            // disables them once no further step fits.
            //
            // No ceiling (single component) → stepping is unconstrained, so stay enabled. With a
            // ceiling, require a positive step count; if the step count can't be computed (ceiling
            // known but increment unavailable) disable rather than dispatch an unsaturated step the
            // solver would revert.
            let canStepUp =
                match doseQtyCeiling with
                | None -> true
                | Some _ ->
                    ord.Orderable.Dose.Quantity
                    |> incrementStepsToCeiling doseQtyCeiling
                    |> Option.exists (fun steps -> steps > 0)

            // Large-step counterpart of canStepUp, measured against the large increment so the
            // last button disables exactly when no further large step fits below the ceiling.
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
                revision = revision
            |}
            |> stepsMode mode


    /// A value shown and not changed. `display` is built from `orderFixed`, since a field that
    /// takes no change has nothing for a cross to clear.
    let ovarDisplay display (name: string) (format: decimal -> string) minWidth (ovar: OrderVariable) =
        let mark = ovar |> markOf
        let label = ovar |> ovarLabel name
        let vals = ovar |> ovarVals format
        display false label None ignore noSteps mark minWidth vals


    /// The same filter select, typed into rather than scrolled, for a list long enough that
    /// reading it is slower than naming it. The same rule decides what it offers.
    let autoComplete disabled isLoading lbl selected dispatch xs =
        Components.PickField.View
            {|
                label = lbl
                options = xs |> Array.map (fun s -> s, s)
                selected = selected
                onChange = dispatch
                clearable = true
                isLoading = isLoading
                enabled = not disabled
                shape = Components.PickField.Shape.Type
            |}


    let inlineProgress isLoading =
        if isLoading then
            let progressSx =
                {|
                    display = "flex"
                    justifyContent = "center"
                    padding = 2
                |}

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


    /// The progress while a value is fetched, or fetched again over what is shown; nothing once
    /// it is answered.
    let progressOrEmpty (deferred: Deferred<'a>) =
        match deferred with
        | Resolved _ -> null
        | HasNotStartedYet
        | InProgress
        | Refreshing _ -> circularProgress


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


        /// The patient's weight for the print, or the caller's word for not known.
        let patientWeight (unknown: string) (patient: Patient option) =
            patient
            |> Option.bind Models.Patient.getWeightInKg
            |> Option.map (fun w ->
                let s = decimal w |> Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision 1
                s + " kg"
            )
            |> Option.defaultValue unknown


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
