namespace Components


/// One quantity of an order as the user sees and moves it: the value picked from what the
/// rules allow, its severity marked on it, the step buttons beside it, and the value the
/// steps predict shown while the server answers. The field renders one entry of a list its
/// caller orders; it knows neither where it stands nor what the other fields are.
///
/// The field is one row of five slots, whatever its mode: four button slots around the value.
/// A slot without a button in the current mode is hidden, not left out, so a change of mode
/// keeps the value and the buttons where they were on screen. A value whose large step is its
/// small step is the exception: it never has outer buttons, so it leaves their slots out.
module QuantityField =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Policies


    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz
    open Informedica.GenPRES.Shared


    /// The steps a value offers and what a click does; absent steps draw disabled. The
    /// prediction maps counted small and large clicks to the key and label the value would
    /// have, so the field can show it before the server confirms. A value whose large step is its
    /// small step has no large step: a stepable field then shows only the inner buttons.
    type Steps =
        {|
            step: (int * int -> string * string) option
            large: string option
            hasLarge: bool
            first: (int -> unit) option
            decrease: (int -> unit) option
            median: (unit -> unit) option
            increase: (int -> unit) option
            last: (int -> unit) option
            useDebounce: bool
        |}


    /// The texts the field shows in the user's language: what an empty value asks, what a
    /// click on a range does, and the hover texts of the four button slots, from left to right,
    /// where the field picks from a range and where it steps one value.
    type Texts =
        {|
            pickValue: string
            pickMedian: string
            navigableTitles: string * string * string * string
            stepableTitles: string * string * string * string
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


    /// Where the answer to the clicks a field sent is: none awaited, the clicks sent, or their
    /// request out. The field knows the request is out by its own disabled, which its caller sets
    /// while a request of the page is out, and only then; a field the caller disables for another
    /// reason meanwhile keeps its predicted value until the value changes.
    [<RequireQualifiedAccess>]
    type Answer =
        | Idle
        | Sent
        | Out


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
            texts: Texts
            hasClear: bool
            // a field whose list cannot be opened: a value only shown, or one the solver determined
            readOnly: bool
            // a field the user narrowed: its arrow clears its choice and the list shows the answer
            reopen: (unit -> unit) option
            // the list of a reopen closed without a pick
            restore: unit -> unit
            // a request of the page under way: the arrow does not reopen meanwhile
            busy: bool
            disabled: bool
            isLoading: bool
            severity: Severity
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
    // the least width of a field with only the inner buttons
    let fieldWidthInnerSlots = 2 * slotWidth + markWidth + valueMinWidth
    let fieldWidthWithoutSlots = markWidth + valueMinWidth


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


    // The row of a field with only the inner buttons: the outer slots are left out.
    let innerRowSx =
        {| rowSx with gridTemplateColumns = $"%i{slotWidth}px minmax(0, 1fr) %i{slotWidth}px %i{markWidth}px" |}


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
    let innerLabelSx = {| paddingLeft = $"%i{slotWidth + 8}px" |}


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


    // A step button: disabled when the step is not offered; with debounce a counting button,
    // which repeats while held, shows the field's count on a badge and predicts each click, and
    // the field sends the clicks; otherwise a plain button that sends one step per click.
    let stepButton disabled useDebounce count (step: (int -> unit) option) (onStep: unit -> unit) icon =
        match step with
        | None -> plainButton true ignore icon
        | Some _ when useDebounce ->
            ClickCountingButton.View
                {|
                    disabled = disabled
                    count = count
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
        // underlying value changes, or when the answer to the clicks sent has landed.
        let smallDelta, setSmallDelta = React.useState 0
        let largeDelta, setLargeDelta = React.useState 0
        let smallRef = React.useRef 0
        let largeRef = React.useRef 0

        // Use the raw value string as the dependency (a JS primitive compared by value)
        // so the reset only fires when the underlying value actually changes — boxing an
        // option would create a new reference every render and reset on every render.
        let valueKey = props.values |> Array.tryHead |> Option.map fst |> Option.defaultValue ""

        let reset () =
            smallRef.current <- 0
            largeRef.current <- 0
            setSmallDelta 0
            setLargeDelta 0

        // useLayoutEffect (not useEffect) so the deltas are reset BEFORE the browser
        // paints the frame on which the server's new value arrives — otherwise that frame
        // would briefly show newServerValue + staleDelta × increment.
        React.useLayoutEffect (reset, [| box valueKey |])

        // the answer to the clicks sent has landed once the field, disabled while their request is
        // out, is enabled again; only then the prediction goes, also when the answer repeats the
        // value. Right after the send the request may not be out yet, so the field first waits for
        // the disable
        let awaiting = React.useRef Answer.Idle

        React.useLayoutEffect (
            (fun () ->
                match awaiting.current, props.disabled with
                | Answer.Sent, true -> awaiting.current <- Answer.Out
                | Answer.Out, false ->
                    awaiting.current <- Answer.Idle
                    reset ()
                | _ -> ()
            ),
            [| box props.disabled |]
        )

        // while another field counts its clicks this one is disabled; the field counting stays
        // enabled, and tells the App when it starts and when it has sent its clicks
        let counting = React.useContext Global.counting
        let selfCounting, setSelfCounting = React.useState false
        let report = React.useRef counting.Report
        report.current <- counting.Report
        let disabled = props.disabled || (counting.Counting && not selfCounting)

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

        // the clicks of all four arrows go out as one command for their net count, 700 ms after
        // the last click, so that no click is lost to the request of another arrow. Meanwhile
        // every other field and the page wait, so nothing else is out when the clicks go
        let timerRef = React.useRef (None: int option)

        // a field unmounted while it counts drops its clicks, and the count ends
        React.useEffect (
            (fun () ->
                fun () ->
                    match timerRef.current with
                    | Some timer ->
                        JS.clearTimeout timer
                        report.current false
                    | None -> ()
            ),
            [||]
        )

        let send () =
            timerRef.current <- None

            let step =
                match StepPolicy.net smallRef.current largeRef.current, steps with
                | Some(StepPolicy.Pair.Inner, n), Some s when n > 0 -> s.increase |> Option.map (fun f -> f, n)
                | Some(StepPolicy.Pair.Inner, n), Some s -> s.decrease |> Option.map (fun f -> f, -n)
                | Some(StepPolicy.Pair.Outer, n), Some s when n > 0 -> s.last |> Option.map (fun f -> f, n)
                | Some(StepPolicy.Pair.Outer, n), Some s -> s.first |> Option.map (fun f -> f, -n)
                | None, _
                | _, None -> None

            match step with
            | Some(f, n) ->
                awaiting.current <- Answer.Sent
                f n
            | None -> reset ()

            setSelfCounting false
            report.current false

        let counted bump =
            fun () ->
                bump ()

                match timerRef.current with
                | Some timer -> JS.clearTimeout timer
                | None ->
                    setSelfCounting true
                    report.current true

                timerRef.current <- Some(JS.setTimeout send 700)

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

        // A navigable field that shows its range picks the median on a click or on Enter or
        // Space; the dropdown, which would hold the range alone, does not open. A click on the
        // cross still clears the value.
        let median =
            match props.mode, props.values with
            | Navigable steps, [| ("range", _) |] when not disabled -> steps.median
            | _ -> None

        let select =
            SimpleSelect.View
                {|
                    label = props.label
                    selected = displaySelected
                    values = displayValues
                    updateSelected = props.onChange
                    isLoading = props.isLoading
                    disabled = disabled
                    readOnly = props.readOnly
                    hasClear = props.hasClear
                    canStep = canStep
                    severity = props.severity
                    // the minimum is the field's; the select takes the column the slots leave
                    minWidth = None
                    description = median |> Option.map (fun _ -> props.texts.pickMedian)
                    reopen = props.reopen
                    restore = props.restore
                    busy = props.busy
                    placeholder = Some props.texts.pickValue
                |}

        // the step buttons rest while the field is disabled, as while a request runs
        let hasButtons, (title1, title2, title4, title5), (icon1, icon2, icon4, icon5) =
            match props.mode with
            | Navigable _ ->
                true,
                props.texts.navigableTitles,
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
                props.texts.stepableTitles,
                (large "−" Mui.Icons.KeyboardDoubleArrowLeftIcon,
                 Mui.Icons.RemoveIcon,
                 Mui.Icons.Add,
                 large "+" Mui.Icons.KeyboardDoubleArrowRightIcon)

        // a pair rests while the other pair holds clicks, so the field's clicks are one pair's
        let button pair count (pick: Steps -> (int -> unit) option) bump icon =
            let useDebounce = steps |> Option.exists _.useDebounce
            let rests = disabled || StepPolicy.rests pair smallDelta largeDelta
            stepButton rests useDebounce count (steps |> Option.bind pick) (counted bump) icon

        // a stepable value without a large step leaves out the outer slots, which would step the
        // same as the inner ones; the inner buttons then close the group
        let hasOuter =
            match props.mode with
            | Stepable steps -> steps.hasLarge
            | Navigable _
            | Selectable
            | Fixed -> true

        let slot1 =
            if hasOuter then
                button StepPolicy.Pair.Outer (max -largeDelta 0) _.first (fun () -> bumpLarge -1) icon1
                |> slot hasButtons true "4px 0 0 4px" title1
            else
                null

        let slot2 =
            button StepPolicy.Pair.Inner (max -smallDelta 0) _.decrease (fun () -> bumpSmall -1) icon2
            |> slot hasButtons (not hasOuter) (if hasOuter then "0" else "4px 0 0 4px") title2
        let slot4 =
            button StepPolicy.Pair.Inner (max smallDelta 0) _.increase (fun () -> bumpSmall 1) icon4
            |> slot hasButtons false (if hasOuter then "0" else "0 4px 4px 0") title4

        let slot5 =
            if hasOuter then
                button StepPolicy.Pair.Outer (max largeDelta 0) _.last (fun () -> bumpLarge 1) icon5
                |> slot hasButtons false "0 4px 4px 0" title5
            else
                null

        let mark =
            SeverityMark.View
                {|
                    severity = props.severity
                    reason = props.reason
                |}

        // the field is never narrower than its fixed columns and the least value width, whatever
        // the caller asks
        let minWidth =
            props.minWidth
            |> Option.defaultValue 150
            |> max (if hasOuter then fieldWidth else fieldWidthInnerSlots)

        // the caption is not the select's own label, so a click on it moves the focus to the
        // select, as a click on a label does
        // read by a container that drops the hidden slots of a field without buttons
        let buttons = if hasButtons then "some" else "none"

        let onButton (e: Browser.Types.Event) = e.target?closest (".MuiIconButton-root") |> isNull |> not

        let pickMedian (e: Browser.Types.Event) =
            match median with
            | Some pick when not (onButton e) ->
                e.preventDefault ()
                e.stopPropagation ()
                pick ()
            | _ -> ()

        // Enter and Space pick the median; the arrow keys, which would open the menu, do nothing
        let pickMedianByKey (e: Browser.Types.KeyboardEvent) =
            match e.key with
            | "Enter"
            | " " -> pickMedian e
            | "ArrowUp"
            | "ArrowDown" when median.IsSome ->
                e.preventDefault ()
                e.stopPropagation ()
            | _ -> ()

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
                <Tooltip title={props.texts.pickMedian}>
                    <Box sx={medianSx} onMouseDownCapture={pickMedian} onKeyDownCapture={pickMedianByKey}>
                        {select}
                    </Box>
                </Tooltip>
                """

        let focusSelect =
            fun _ ->
                match Browser.Dom.document.getElementById props.label with
                | null -> ()
                | el -> el.focus ()

        let sx = fieldSx minWidth props.isLead
        let fieldRowSx = if hasOuter then rowSx else innerRowSx
        let fieldLabelSx = if hasOuter then labelSx else innerLabelSx
        let cellSx = valueSx hasButtons

        // the severity mark stands outside the group, right of the last button
        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Typography from '@mui/material/Typography';

        <Box sx={sx} data-buttons={buttons}>
            <Typography variant="caption" color="text.secondary" sx={fieldLabelSx} data-part="label" onClick={focusSelect}>{props.label}</Typography>
            <Box sx={fieldRowSx} data-part="row">
                {slot1}
                {slot2}
                <Box sx={cellSx}>
                    {value}
                </Box>
                {slot4}
                {slot5}
                <Box sx={markSx}>{mark}</Box>
            </Box>
        </Box>
        """
