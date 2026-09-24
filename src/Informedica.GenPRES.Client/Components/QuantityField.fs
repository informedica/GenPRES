namespace Components


/// One quantity of an order as the user sees and moves it: the value picked from what the
/// rules allow, its severity marked on it, the step buttons beside it, and the value the
/// steps predict shown while the server answers. The field renders one entry of a list its
/// caller orders; it knows neither where it stands nor what the other fields are.
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
            median: (unit -> unit) option
            increase: (int -> unit) option
            last: (int -> unit) option
            useDebounce: bool
            revision: int
        |}


    /// The field: its label, the values allowed and the one chosen, what choosing does, its
    /// steps if any, whether it can be cleared, its severity, and whether it is the field the
    /// user is pointed at first.
    type Props =
        {|
            label: string
            values: (string * string)[]
            selected: string option
            onChange: string option -> unit
            steps: Steps option
            hasClear: bool
            disabled: bool
            isLoading: bool
            severity: Types.Severity
            minWidth: int option
            isLead: bool
        |}


    // the select and its stepper side by side, the stepper on the value's baseline; the
    // stepper wraps under the select where the cell is too narrow for both
    let private rowSx =
        {|
            display = "flex"
            flexWrap = "wrap"
            alignItems = "flex-end"
            gap = 0.5
        |}


    // the field the user is pointed at first carries the accent on its left
    let private leadSx =
        {|
            display = "flex"
            flexWrap = "wrap"
            alignItems = "flex-end"
            gap = 0.5
            borderLeft = $"3px solid"
            borderColor = Mui.Styles.accentColor
            paddingLeft = 1
        |}


    [<JSX.Component>]
    let View (props: Props) =
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

        let revision = props.steps |> Option.map (fun s -> s.revision) |> Option.defaultValue 0

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

        let stepFn = props.steps |> Option.bind (fun s -> s.step)

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
            props.steps
            |> Option.map (fun s ->
                s.first.IsSome
                || s.decrease.IsSome
                || s.median.IsSome
                || s.increase.IsSome
                || s.last.IsSome
            )
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
                    hasClear = props.hasClear
                    canStep = canStep
                    severity = props.severity
                    minWidth = props.minWidth
                |}

        // the step buttons rest only when the field is disabled: a step sent while the value
        // is loading waits for the answer and steps from it
        let stepper =
            match props.steps with
            | None -> null
            | Some steps ->
                Stepper.View
                    {|
                        first = steps.first
                        decrease = steps.decrease
                        median = steps.median
                        increase = steps.increase
                        last = steps.last
                        useDebounce = steps.useDebounce
                        disabled = props.disabled
                        onSmallStep = bumpSmall
                        onLargeStep = bumpLarge
                    |}

        let sx = if props.isLead then box leadSx else box rowSx

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';

        <Box sx={sx}>
            {select}
            {stepper}
        </Box>
        """
