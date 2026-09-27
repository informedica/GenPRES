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
    open Feliz
    open Shared


    /// The steps a value offers and what a click does; absent steps draw disabled. The
    /// prediction maps counted small and large clicks to the key and label the value would
    /// have, so the field can show it before the server confirms; the revision is bumped by the
    /// caller on every answer, so a prediction is dropped even when the answer repeats the value.
    type Steps =
        {|
            step: (int * int -> string * string) option
            first: (int -> unit) option
            decrease: (int -> unit) option
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
            hasClear: bool
            disabled: bool
            isLoading: bool
            severity: Types.Severity
            // why the value is marked, as the caller words it; shown on hover
            reason: string option
            minWidth: int option
            isLead: bool
        |}


    // a button slot: as wide as a small icon button, as tall as the select's input line
    let slotWidth = 32
    let slotHeight = 40

    // the hover texts of the four button slots, per mode
    let navigableTitles = "naar minimum", "lager", "hoger", "naar maximum"
    let stepableTitles = "grote stap omlaag", "stap omlaag", "stap omhoog", "grote stap omhoog"


    // Five fixed columns: the four button slots and the value between them. The value column
    // takes what the slots leave and may shrink, so the row spans exactly the width it is given.
    let rowSx (minWidth: int) (isLead: bool) =
        {|
            display = "grid"
            gridTemplateColumns = $"%i{slotWidth}px %i{slotWidth}px minmax(0, 1fr) %i{slotWidth}px %i{slotWidth}px"
            alignItems = "end"
            columnGap = 0.5
            width = "100%"
            minWidth = minWidth
            // the field the user is pointed at first carries the accent on its left
            borderLeft = if isLead then "3px solid" else "none"
            borderColor = if isLead then Mui.Styles.accentColor else "transparent"
            paddingLeft = if isLead then 1 else 0
        |}


    // a slot keeps its size when its button is hidden: visibility, not display, so the grid
    // never reflows and a hidden button leaves the tab order
    let slotSx (visible: bool) =
        {|
            width = slotWidth
            height = slotHeight
            display = "flex"
            alignItems = "center"
            justifyContent = "center"
            visibility = if visible then "visible" else "hidden"
        |}


    let valueSx =
        {|
            display = "flex"
            alignItems = "flex-end"
            minWidth = 0
        |}


    let growSx =
        {|
            flexGrow = 1
            minWidth = 0
        |}


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
    let slot (visible: bool) (title: string) (button: JSX.Element) =
        let sx = slotSx visible

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Tooltip from '@mui/material/Tooltip';

        <Box sx={sx}>
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
                steps.IsSome,
                stepableTitles,
                (Mui.Icons.KeyboardDoubleArrowLeftIcon,
                 Mui.Icons.RemoveIcon,
                 Mui.Icons.Add,
                 Mui.Icons.KeyboardDoubleArrowRightIcon)

        let button (pick: Steps -> (int -> unit) option) onStep icon =
            let useDebounce = steps |> Option.exists _.useDebounce
            stepButton props.disabled useDebounce (steps |> Option.bind pick) onStep icon

        let slot1 = button _.first (fun () -> bumpLarge -1) icon1 |> slot hasButtons title1
        let slot2 = button _.decrease (fun () -> bumpSmall -1) icon2 |> slot hasButtons title2
        let slot4 = button _.increase (fun () -> bumpSmall 1) icon4 |> slot hasButtons title4
        let slot5 = button _.last (fun () -> bumpLarge 1) icon5 |> slot hasButtons title5

        let mark =
            SeverityMark.View
                {|
                    severity = props.severity
                    reason = props.reason
                |}

        let minWidth = props.minWidth |> Option.defaultValue 150

        let sx = rowSx minWidth props.isLead

        // the severity mark sits in the value column, on the right of the select, so it takes
        // no slot of its own
        JSX.jsx
            $"""
        import Box from '@mui/material/Box';

        <Box sx={sx}>
            {slot1}
            {slot2}
            <Box sx={valueSx}>
                <Box sx={growSx}>{select}</Box>
                {mark}
            </Box>
            {slot4}
            {slot5}
        </Box>
        """
