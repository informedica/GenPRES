namespace Views

#nowarn "1104"

module ViewHelpers =

    open System
    open Fable.Core
    open Feliz
    open Shared
    open Shared.Types
    open Shared.Models.Order


    let filterSelect disabled isLoading lbl selected dispatch xs =
        let isEmpty = xs |> Array.isEmpty

        Components.SimpleSelect.View
            {|
                updateSelected = if isEmpty then ignore else dispatch
                label = lbl
                selected = if isEmpty then None else selected
                values = xs
                isLoading = isLoading
                disabled = disabled || isEmpty
                hasClear = true
                stepper = None
                warning = None
                minWidth = None
            |}


    let getWarning warning =
        match warning with
        | IsNormal -> None
        | IsCaution -> Some Mui.Colors.Blue.``600``
        | IsWarning -> Some Mui.Colors.Orange.``700``
        | IsAlert -> Some Mui.Colors.Red.``700``


    let orderSelect alwaysShow disabled isLoading lbl selected updateSelected stepper hasClear warning minWidth xs =

        if not alwaysShow && xs |> Array.isEmpty && stepper |> Option.isNone then
            null
        else
            let isEmpty = xs |> Array.isEmpty && stepper |> Option.isNone

            Components.SimpleSelect.View
                {|
                    updateSelected = if isEmpty then ignore else updateSelected
                    label = lbl
                    selected =
                        if xs |> Array.length = 1 then
                            xs[0] |> fst |> Some
                        else
                            selected
                    values = xs
                    isLoading = isLoading
                    disabled = disabled || isEmpty
                    hasClear = hasClear
                    warning = warning
                    stepper = stepper
                    minWidth = minWidth
                |}


    /// Build the stepper record for a select. `navigable` = can jump to the min, median, or max
    /// (gates the first/median/last buttons); `solved` enables single-step decrease/increase.
    let createStepper
        dispatch
        revision
        navigable
        solved
        setMin
        (decr: int * bool -> 'Msg)
        setMed
        (incr: int * bool -> 'Msg)
        setMax
        step
        =
        {|
            step = step
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
        |> Some


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
    let firstSnd (vu: Types.ValueUnit) =
        vu.Value |> Array.tryHead |> Option.map snd


    /// The defined (small-step) increment: the defined constraint's increment, else the
    /// solved variable's own increment.
    let definedIncrement (ovar: OrderVariable) : decimal option =
        [ ovar.DefinedConstraints.Incr; ovar.Variable.Incr ]
        |> List.tryPick (Option.bind firstSnd)


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
                let largeIncr =
                    ovar.LargeIncr |> Option.bind firstSnd |> Option.defaultValue smallIncr

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
        let largeIncr =
            ovar.LargeIncr |> Option.bind firstSnd |> Option.orElse (definedIncrement ovar)

        ovar |> stepsToCeiling ceiling largeIncr


    /// Build the stepper record for the orderable dose-quantity select, shared by the Order
    /// and Nutrition views. Handles the optimistic stepping with feasibility-ceiling
    /// saturation: the displayed value follows the click count up to the prepared orderable
    /// quantity, and dispatched steps are saturated at that ceiling so an overshoot is not
    /// reverted by the solver. The five message constructors (setMin/decr/setMed/incr/setMax)
    /// are supplied by each view from its own Msg type. Returns None when navigation must be
    /// hidden (a multi-component orderable whose components do not each have a single distinct
    /// orderable quantity).
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
        // Only show nav when every component has a single distinct orderable quantity.
        let showNav =
            ord.Orderable.Components
            |> Array.forall (fun cmp ->
                cmp.OrderableQuantity.Variable.Vals
                |> Option.map (fun vu -> vu.Value |> Array.length = 1)
                |> Option.defaultValue false
            )

        if not showNav then
            None
        else
            let canIncr =
                ord.Orderable.Components |> Array.length = 1
                || ord.Orderable.DoseCount.Variable.Vals
                   |> Option.map (fun vu -> vu.Value |> Array.map snd |> Array.forall (fun v -> v > 1m))
                   |> Option.defaultValue false

            let solved = ord |> isSolved
            // can jump to the min, median, or max
            let navigable = ord.Orderable.Dose.Quantity |> OrderVariable.isNavigable

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
            |> Some


    let ovarDisplay select (name: string) (format: decimal -> string) minWidth (ovar: OrderVariable) =
        let warning = ovar.Level |> getWarning
        let label = ovar |> ovarLabel name
        let vals = ovar |> ovarVals format
        select false label None ignore None false warning minWidth vals


    let autoComplete disabled isLoading lbl selected dispatch xs =
        let isEmpty = xs |> Array.isEmpty

        Components.Autocomplete.View
            {|
                updateSelected = if isEmpty then ignore else dispatch
                label = lbl
                selected = selected
                values = xs
                isLoading = isLoading
                disabled = disabled || isEmpty
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


    let progressOrEmpty (deferred: Deferred<'a>) =
        match deferred with
        | Resolved _
        | Recalculating _ -> null
        | _ -> circularProgress


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


    let modalStyle =
        {|
            position = "absolute"
            top = "50%"
            left = "50%"
            transform = "translate(-50%, -50%)"
            width = "90vw"
            maxWidth = 500
            maxHeight = "90vh"
            overflowY = "auto"
            overflowX = "hidden"
            bgcolor = "background.paper"
            boxShadow = 24
            borderRadius = "16px"
        |}


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


        let patientWeight (patient: Patient option) =
            patient
            |> Option.bind Models.Patient.getWeightInKg
            |> Option.map (fun w ->
                let s = decimal w |> Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision 1
                s + " kg"
            )
            |> Option.defaultValue "onbekend"


        [<JSX.Component>]
        let PatientHeader (props: {| weightKg: string |}) =
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
                        <TableCell sx={headerCellSx}>D.D.</TableCell>
                        <TableCell sx={valueCellSx}>{currentDate}</TableCell>
                        <TableCell sx={headerCellSx}>Patientnummer</TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                    </TableRow>
                    <TableRow>
                        <TableCell sx={headerCellSx}>Afdeling</TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                        <TableCell sx={headerCellSx}>Naam</TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                    </TableRow>
                    <TableRow>
                        <TableCell sx={headerCellSx}>Bed</TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                        <TableCell sx={headerCellSx}>Geboorte datum</TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                    </TableRow>
                    <TableRow>
                        <TableCell sx={headerCellSx}>Arts</TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                        <TableCell sx={headerCellSx}>Gewicht</TableCell>
                        <TableCell sx={valueCellSx}>{props.weightKg}</TableCell>
                    </TableRow>
                    <TableRow>
                        <TableCell sx={headerCellSx}>Zoemer</TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                        <TableCell sx={headerCellSx}></TableCell>
                        <TableCell sx={valueCellSx}></TableCell>
                    </TableRow>
                </TableBody>
            </Table>
            """


        [<JSX.Component>]
        let PatientSignature () =
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
                <Typography variant="body2">Paraaf arts:</Typography>
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
