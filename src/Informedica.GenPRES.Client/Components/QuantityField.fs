namespace Components


/// One quantity of an order as the user sees and moves it: the value picked from what the
/// rules allow, its severity marked on it, the step buttons beside it, and the value the
/// steps predict shown while the server answers. The field renders one entry of a list its
/// caller orders; it knows neither where it stands nor what the other fields are.
///
/// The field is one row of five slots, whatever its mode: four button slots around the value.
/// A slot without a button in the current mode is hidden, not left out, so a change of mode
/// keeps the value and the buttons where they were on screen.
module QuantityField =


    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz
    open Shared


    /// The steps a value offers and what a click does; absent steps draw disabled. The
    /// prediction maps counted small and large clicks to the key and label the value would
    /// have, so the field can show it before the server confirms; the revision is bumped by the
    /// caller on every answer, so a prediction is dropped even when the answer repeats the value.
    type Steps =
        {|
            step: (int * int -> string * string) option
            large: string option
            first: (int -> unit) option
            decrease: (int -> unit) option
            median: (unit -> unit) option
            increase: (int -> unit) option
            last: (int -> unit) option
            useDebounce: bool
            revision: int
        |}


    /// How the user may move the value; decided by the caller from the order variable.
    type Mode =
        /// Several values allowed: the value is chosen from the dropdown, without step buttons.
        | Selectable
        /// A range allowed: the steps narrow it, first and last jump to the min and the max.
        | Navigable of Steps
        /// One value: the steps move it, first and last make a large step.
        | Stepable of Steps
        /// One value or a range the user cannot move from this field.
        | Fixed


    /// The field: its label, the values allowed and the one chosen, what choosing does, how
    /// the value may be moved, whether it can be cleared, its severity, and whether it is the
    /// field the user is pointed at first.
    type Props =
        {|
            label: string
            values: (string * string)[]
            selected: string option
            onChange: string option -> unit
            mode: Mode
            placeholder: string
            hasClear: bool
            disabled: bool
            isLoading: bool
            severity: Types.Severity
            // why the value is marked, as the caller words it; shown on hover
            reason: string option
            minWidth: int option
            isLead: bool
        |}


    // a button slot: a square as tall as the value cell
    let slotWidth = 40
    let slotHeight = 40
    // the column right of the group that holds the severity mark, kept whether or not the
    // value is marked, so the groups of all fields line up
    let markWidth = 28
    // the least width the value cell keeps, so a value stays readable in a narrow row
    let valueMinWidth = 120
    // the least width of a field that keeps its button slots, and of one that drops them
    let fieldWidth = 4 * slotWidth + markWidth + valueMinWidth
    let fieldWidthWithoutSlots = markWidth + valueMinWidth

    // the hover texts of the four button slots, per mode
    let navigableTitles = "naar minimum", "lager", "hoger", "naar maximum"
    let stepableTitles = "grote stap omlaag", "stap omlaag", "stap omhoog", "grote stap omhoog"


    // the label above the group; the field the user is pointed at first carries the accent on
    // its left
    let fieldSx (minWidth: int) (isLead: bool) =
        {|
            display = "flex"
            flexDirection = "column"
            width = "100%"
            minWidth = minWidth
            borderLeft = if isLead then "3px solid" else "none"
            borderColor = if isLead then Mui.Styles.accentColor else "transparent"
            paddingLeft = if isLead then 1 else 0
        |}


    // Six fixed columns: the four button slots, the value between them and the severity mark
    // after them. The value column takes what the others leave and may shrink, so the row spans
    // exactly the width it is given.
    let rowSx =
        {|
            display = "grid"
            gridTemplateColumns =
                $"%i{slotWidth}px %i{slotWidth}px minmax(0, 1fr) %i{slotWidth}px %i{slotWidth}px %i{markWidth}px"
            alignItems = "stretch"
            columnGap = 0
        |}


    // One cell of the segmented group. Each cell has its own border and overlaps the one on its
    // left by a pixel, so neighbours share a single line and a hidden slot leaves the value cell
    // with its border all round.
    let cellSx (first: bool) (radius: string) =
        {|
            height = slotHeight
            display = "flex"
            alignItems = "center"
            border = "1px solid"
            borderColor = "divider"
            borderRadius = radius
            marginLeft = if first then "0" else "-1px"
        |}


    // a slot keeps its size when its button is hidden: visibility, not display, so the grid
    // never reflows and a hidden button leaves the tab order
    let slotSx (visible: bool) (first: bool) (radius: string) =
        {| cellSx first radius with
            justifyContent = "center"
            backgroundColor = "grey.100"
            visibility = if visible then "visible" else "hidden"
            // the span the tooltip anchors on, and the button in it, fill the whole cell
            ``& > span`` =
                {|
                    display = "flex"
                    width = "100%"
                    height = "100%"
                |}
            ``& .MuiIconButton-root`` =
                {|
                    width = "100%"
                    height = "100%"
                    // hover and focus follow the rounded outer corners
                    borderRadius = "inherit"
                |}
        |}


    // The value cell: the select without its own label and underline, since the label stands
    // above the group and the cell draws the border; the border shows the focus instead.
    let valueSx (hasButtons: bool) =
        {| cellSx false (if hasButtons then "0" else "4px") with
            minWidth = 0
            overflow = "hidden"
            // the placeholder stands over the empty select
            position = "relative"
            backgroundColor = "background.paper"
            ``&:focus-within`` = {| borderColor = "primary.main" |}
            ``& .MuiInputLabel-root`` = {| display = "none" |}
            // the select fills the cell, so a single value's grey meets the cell border
            ``& .MuiFormControl-root`` = {| height = "100%" |}
            // && doubles the cell's class, so these win over the select's own rules of the same
            // weight (the margin MUI gives an input after a label, the select's padding) whatever
            // order the styles were inserted in
            ``&& .MuiInput-root`` =
                {|
                    marginTop = 0
                    height = "100%"
                    padding = "0 8px"
                    borderRadius = 0
                |}
            // the value text stands in the middle of the cell whatever the select's own styling
            // with the cross shown the dropdown arrow is hidden, so its room goes to the value,
            // and the cross is a compact button: both fit a narrow cell without being cut off
            ``&& .MuiInputBase-adornedEnd .MuiSelect-select`` = {| paddingRight = 0 |}
            ``&& .MuiInputBase-root > .MuiIconButton-root`` =
                {|
                    padding = "4px"
                    flexShrink = 0
                |}
            ``&& .MuiSelect-select`` =
                {|
                    display = "flex"
                    alignItems = "center"
                    height = "100%"
                    paddingTop = 0
                    paddingBottom = 0
                |}
            ``& .MuiInput-root::before`` = {| display = "none" |}
            ``& .MuiInput-root::after`` = {| display = "none" |}
        |}


    // the label starts where the value text starts: after the two left slots and the value
    // cell's text padding
    let labelSx = {| paddingLeft = $"%i{2 * slotWidth + 8}px" |}


    // For a container where a field without buttons never gets any, so dropping its hidden
    // slots moves nothing: such a field gives their width to its value, and its label moves
    // along with the value.
    let dropUnusedSlotsSx =
        {|
            ``& [data-buttons="none"]`` = {| minWidth = $"%i{fieldWidthWithoutSlots}px" |}
            ``& [data-buttons="none"] [data-part="slot"]`` = {| display = "none" |}
            ``& [data-buttons="none"] [data-part="row"]`` =
                {| gridTemplateColumns = $"minmax(0, 1fr) %i{markWidth}px" |}
            ``& [data-buttons="none"] [data-part="label"]`` = {| paddingLeft = "8px" |}
        |}


    let markSx =
        {|
            display = "flex"
            alignItems = "center"
            justifyContent = "center"
        |}


    // a flex box, so the select's own flexGrow fills the value column instead of keeping the
    // natural width of its label
    let growSx =
        {|
            display = "flex"
            flexGrow = 1
            alignSelf = "stretch"
            minWidth = 0
        |}


    // over the empty select, where its value would stand, and out of the way of a click on it
    let placeholderSx =
        {|
            position = "absolute"
            left = "8px"
            color = "text.disabled"
            pointerEvents = "none"
            whiteSpace = "nowrap"
        |}


    // the range that picks its median on a click shows the hand, as a button does
    let medianSx =
        {| growSx with
            cursor = "pointer"
            ``& .MuiSelect-select`` = {| cursor = "pointer" |}
        |}


    // the large step as text on a button, in the size of the value
    let stepText (text: string) =
        JSX.jsx
            $"""
        import Typography from '@mui/material/Typography';
        <Typography variant="body2">{text}</Typography>
        """


    let plainButton (disabled: bool) (onClick: unit -> unit) (icon: JSX.Element) =
        let click = fun _ -> onClick ()

        JSX.jsx
            $"""
        import IconButton from "@mui/material/IconButton";
        <IconButton size="small" sx={Mui.Styles.stepButtonSx} disabled={disabled} onClick={click}>{icon}</IconButton>
        """


    // A step button as the Stepper drew it: disabled when the step is not offered; with
    // debounce a counting button, which repeats while held, shows the count on a badge and
    // predicts each click; otherwise a plain button that sends one step per click.
    let stepButton disabled useDebounce (step: (int -> unit) option) (onStep: unit -> unit) icon =
        match step with
        | None -> plainButton true ignore icon
        | Some onClick when useDebounce ->
            ClickCountingButton.View
                {|
                    disabled = disabled
                    onClick = onClick
                    onStep = onStep
                    icon = icon
                |}
        | Some onClick -> plainButton disabled (fun () -> onClick 1) icon


    // One button slot. The button sits in a span so the tooltip still anchors when the button
    // is disabled, since a disabled element fires no pointer events of its own.
    let slot (visible: bool) (first: bool) (radius: string) (title: string) (button: JSX.Element) =
        let sx = slotSx visible first radius

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Tooltip from '@mui/material/Tooltip';

        <Box sx={sx} data-part="slot">
            <Tooltip title={title}>
                <span>{button}</span>
            </Tooltip>
        </Box>
        """


    [<JSX.Component>]
    let View (props: Props) =
        // Steps holds functions, so Mode has no equality; the mode is read by matching only.
        let steps =
            match props.mode with
            | Navigable steps
            | Stepable steps -> Some steps
            | Selectable
            | Fixed -> None

        // Net click deltas accumulated from the step buttons. Drive an optimistic displayed
        // value that follows the live click count (the badge) before the server confirms.
        // Small = single-step decrease/increase (the defined increment); Large = jump
        // decrease/increase (the server's larger calculated increment). Reset when the
        // underlying value or the server revision changes.
        let smallDelta, setSmallDelta = React.useState 0
        let largeDelta, setLargeDelta = React.useState 0
        let smallRef = React.useRef 0
        let largeRef = React.useRef 0

        // Use the raw value string as the dependency (a JS primitive compared by value)
        // so the reset only fires when the underlying value actually changes — boxing an
        // option would create a new reference every render and reset on every render.
        let valueKey = props.values |> Array.tryHead |> Option.map fst |> Option.defaultValue ""

        let revision = steps |> Option.map (fun s -> s.revision) |> Option.defaultValue 0

        // useLayoutEffect (not useEffect) so the deltas are reset BEFORE the browser
        // paints the frame on which the server's new value arrives — otherwise that frame
        // would briefly show newServerValue + staleDelta × increment.
        React.useLayoutEffect (
            (fun () ->
                smallRef.current <- 0
                largeRef.current <- 0
                setSmallDelta 0
                setLargeDelta 0
            ),
            [| box valueKey; box revision |]
        )

        let stepFn = steps |> Option.bind (fun s -> s.step)

        // Only accumulate a click that actually moves the predicted value. When the step
        // has saturated at a bound (the feasibility ceiling or the increment floor) the
        // value stops changing; continuing to grow the delta would store invisible
        // "overflow" that a reversal must first unwind before the value moves again.
        let changesValue small large =
            match stepFn with
            | Some f -> f (small, large) <> f (smallRef.current, largeRef.current)
            | None -> true

        let bumpSmall sign =
            let next = smallRef.current + sign

            if changesValue next largeRef.current then
                smallRef.current <- next
                setSmallDelta next

        let bumpLarge sign =
            let next = largeRef.current + sign

            if changesValue smallRef.current next then
                largeRef.current <- next
                setLargeDelta next

        // Override only the displayed LABEL with the optimistically stepped value, keeping
        // the original (server-provided) key. The key is a BigRational string the server
        // recognises, so an in-flight dropdown change still dispatches a valid key; only
        // the shown text reflects the optimistic step.
        let displayValues, displaySelected =
            match stepFn, props.values |> Array.tryHead with
            | Some step, Some(origKey, _) when smallDelta <> 0 || largeDelta <> 0 ->
                let _, label = step (smallDelta, largeDelta)
                [| (origKey, label) |], Some origKey
            | _ -> props.values, props.selected

        let canStep =
            steps
            |> Option.map (fun s -> s.first.IsSome || s.decrease.IsSome || s.increase.IsSome || s.last.IsSome)
            |> Option.defaultValue false

        let select =
            SimpleSelect.View
                {|
                    label = props.label
                    selected = displaySelected
                    values = displayValues
                    updateSelected = props.onChange
                    isLoading = props.isLoading
                    disabled = props.disabled
                    readOnly = false
                    hasClear = props.hasClear
                    canStep = canStep
                    severity = props.severity
                    // the minimum is the field's; the select takes the column the slots leave
                    minWidth = None
                |}

        // the step buttons rest only when the field is disabled: a step sent while the value
        // is loading waits for the answer and steps from it
        let hasButtons, (title1, title2, title4, title5), (icon1, icon2, icon4, icon5) =
            match props.mode with
            | Navigable _ ->
                true,
                navigableTitles,
                (Mui.Icons.FirstPageIcon, Mui.Icons.SkipPreviousIcon, Mui.Icons.SkipNextIcon, Mui.Icons.LastPageIcon)
            | Stepable _
            | Selectable
            | Fixed ->
                // the large step shows as text when the steps name it, as double arrows otherwise
                let large sign icon =
                    steps
                    |> Option.bind _.large
                    |> Option.map (fun text -> stepText $"%s{sign}%s{text}")
                    |> Option.defaultValue icon

                steps.IsSome,
                stepableTitles,
                (large "−" Mui.Icons.KeyboardDoubleArrowLeftIcon,
                 Mui.Icons.RemoveIcon,
                 Mui.Icons.Add,
                 large "+" Mui.Icons.KeyboardDoubleArrowRightIcon)

        let button (pick: Steps -> (int -> unit) option) onStep icon =
            let useDebounce = steps |> Option.exists _.useDebounce
            stepButton props.disabled useDebounce (steps |> Option.bind pick) onStep icon

        let slot1 =
            button _.first (fun () -> bumpLarge -1) icon1
            |> slot hasButtons true "4px 0 0 4px" title1

        let slot2 =
            button _.decrease (fun () -> bumpSmall -1) icon2
            |> slot hasButtons false "0" title2
        let slot4 =
            button _.increase (fun () -> bumpSmall 1) icon4
            |> slot hasButtons false "0" title4

        let slot5 =
            button _.last (fun () -> bumpLarge 1) icon5
            |> slot hasButtons false "0 4px 4px 0" title5

        let mark =
            SeverityMark.View
                {|
                    severity = props.severity
                    reason = props.reason
                |}

        // the field is never narrower than its fixed columns and the least value width, whatever
        // the caller asks
        let minWidth = props.minWidth |> Option.defaultValue 150 |> max fieldWidth

        // the caption is not the select's own label, so a click on it moves the focus to the
        // select, as a click on a label does
        // read by a container that drops the hidden slots of a field without buttons
        let buttons = if hasButtons then "some" else "none"

        // A navigable field that shows its range picks the median on a click or on Enter or
        // Space; the dropdown, which would hold the range alone, does not open. A click on the
        // cross still clears the value.
        let median =
            match props.mode, props.values with
            | Navigable steps, [| ("range", _) |] when not props.disabled -> steps.median
            | _ -> None

        let onButton (e: Browser.Types.Event) = e.target?closest (".MuiIconButton-root") |> isNull |> not

        let pickMedian (e: Browser.Types.Event) =
            match median with
            | Some pick when not (onButton e) ->
                e.preventDefault ()
                e.stopPropagation ()
                pick ()
            | _ -> ()

        let pickMedianByKey (e: Browser.Types.KeyboardEvent) =
            if e.key = "Enter" || e.key = " " then
                pickMedian e

        let value =
            match median with
            | None ->
                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                <Box sx={growSx}>{select}</Box>
                """
            | Some _ ->
                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                import Tooltip from '@mui/material/Tooltip';
                <Tooltip title="naar mediaan">
                    <Box sx={medianSx} onMouseDownCapture={pickMedian} onKeyDownCapture={pickMedianByKey}>
                        {select}
                    </Box>
                </Tooltip>
                """

        // with values to pick from and none picked the cell is not left blank: it asks for one
        let placeholder =
            match displaySelected, displayValues with
            | None, values when values.Length > 0 ->
                JSX.jsx
                    $"""
                import Typography from '@mui/material/Typography';
                <Typography variant="body1" sx={placeholderSx}>{props.placeholder}</Typography>
                """
            | _ -> null

        let focusSelect =
            fun _ ->
                match Browser.Dom.document.getElementById props.label with
                | null -> ()
                | el -> el.focus ()

        let sx = fieldSx minWidth props.isLead
        let cellSx = valueSx hasButtons

        // the severity mark stands outside the group, right of the last button
        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Typography from '@mui/material/Typography';

        <Box sx={sx} data-buttons={buttons}>
            <Typography variant="caption" color="text.secondary" sx={labelSx} data-part="label" onClick={focusSelect}>{props.label}</Typography>
            <Box sx={rowSx} data-part="row">
                {slot1}
                {slot2}
                <Box sx={cellSx}>
                    {value}
                    {placeholder}
                </Box>
                {slot4}
                {slot5}
                <Box sx={markSx}>{mark}</Box>
            </Box>
        </Box>
        """
