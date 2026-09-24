namespace Components


module SimpleSelect =


    open System
    open Shared
    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz


    [<JSX.Component>]
    let View
        (props:
            {|
                label: string
                selected: string option
                values: (string * string)[]
                updateSelected: string option -> unit
                stepper:
                    {|
                        step: (int * int -> string * string) option
                        first: (int -> unit) option
                        decrease: (int -> unit) option
                        median: (unit -> unit) option
                        increase: (int -> unit) option
                        last: (int -> unit) option
                        useDebounce: bool
                        revision: int
                    |} option
                isLoading: bool
                disabled: bool
                hasClear: bool
                severity: Types.Severity
                minWidth: int option
            |})
        =

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        // Net click deltas accumulated from the nav buttons. Drive an optimistic displayed
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

        // A monotonic counter the parent bumps on every server response. It also resets
        // the optimistic deltas when the server returns the SAME value as before — e.g. a
        // no-op step (already at the maximum) leaves the current value unchanged, so
        // valueKey never changes and would otherwise leave a stale optimistic value shown.
        let revision = props.stepper |> Option.map (fun n -> n.revision) |> Option.defaultValue 0

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

        let stepFn = props.stepper |> Option.bind (fun n -> n.step)

        // Only accumulate a click that actually moves the predicted value. When the step
        // has saturated at a bound (the feasibility ceiling or the increment floor) the
        // value stops changing; continuing to grow the delta would store invisible
        // "overflow" that a reversal must first unwind before the value moves again.
        let changesValue small large =
            match stepFn with
            | Some f -> f (small, large) <> f (smallRef.current, largeRef.current)
            | None -> true

        let bumpSmall sign =
            fun () ->
                let next = smallRef.current + sign

                if changesValue next largeRef.current then
                    smallRef.current <- next
                    setSmallDelta next

        let bumpLarge sign =
            fun () ->
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

        let selectSlotProps =
            if isMobile then
                {| menu = {| slotProps = {| paper = {| style = {| maxHeight = 400 |} |} |} |} |}
                |> box
            else
                {| |} |> box

        let handleChange =
            fun ev ->
                let value = ev?target?value

                value
                |> string
                |> function
                    | s when s |> String.isNullOrWhiteSpace -> None
                    | s -> s |> Some
                |> props.updateSelected

        let clear = fun _ -> None |> props.updateSelected

        let menuItemSx =
            {|
                maxWidth = 400
                paddingY = if isMobile then 0.25 else 0.75
            |}

        let items =
            displayValues
            |> Array.mapi (fun i (k, v) ->
                JSX.jsx
                    $"""
                <MenuItem key={i} value={k} sx={menuItemSx} dense={isMobile} >
                    {v}
                </MenuItem>
                """
            )

        let isClear = displaySelected |> Option.defaultValue "" |> String.isNullOrWhiteSpace

        let clearButton =
            match props.isLoading, isClear with
            | true, _ -> Mui.Icons.Downloading
            | false, true -> null
            | false, false ->
                JSX.jsx
                    $"""
                import ClearIcon from '@mui/icons-material/Clear';
                import IconButton from "@mui/material/IconButton";

                <IconButton onClick={clear}>
                    {Mui.Icons.Clear}
                </IconButton>
                """

        // the step buttons rest only when the select is disabled: a step sent while the value
        // is loading waits for the answer and steps from it
        let stepper =
            match props.stepper with
            | None -> null
            | Some nav ->
                Stepper.View
                    {|
                        first = nav.first
                        decrease = nav.decrease
                        median = nav.median
                        increase = nav.increase
                        last = nav.last
                        useDebounce = nav.useDebounce
                        disabled = props.disabled
                        onSmallStep = fun sign -> bumpSmall sign ()
                        onLargeStep = fun sign -> bumpLarge sign ()
                    |}

        // the cross clears the value whether or not the value has a stepper beside it
        let endAdornment =
            if not isClear && props.hasClear then
                Some clearButton
            else
                None

        let hasNavigation =
            props.stepper
            |> Option.map (fun nav ->
                nav.first.IsSome
                || nav.decrease.IsSome
                || nav.median.IsSome
                || nav.increase.IsSome
                || nav.last.IsSome
            )
            |> Option.defaultValue false

        let hasInteraction = hasNavigation || props.values.Length > 1

        let sx =
            match props.severity |> Models.Severity.isRaised, hasInteraction with
            | true, _ ->
                {| ``& .MuiSelect-icon`` = {| visibility = if endAdornment.IsNone then "visible" else "hidden" |} |}
                |> box
                |> Mui.Styles.markSx props.severity
            | false, false ->
                {|
                    ``& .MuiSelect-icon`` = {| visibility = if endAdornment.IsNone then "visible" else "hidden" |}
                    backgroundColor = "action.hover"
                    borderRadius = "4px"
                    padding = "2px 8px"
                |}
                |> box
            | false, true ->
                {| ``& .MuiSelect-icon`` = {| visibility = if endAdornment.IsNone then "visible" else "hidden" |} |}
                |> box

        let formControlSx =
            {|
                minWidth = props.minWidth |> Option.defaultValue 150
                maxWidth = "100%"
            |}

        // the select and its stepper side by side, the stepper on the value's baseline; the
        // stepper wraps under the select where the cell is too narrow for both
        let rowSx =
            {|
                display = "flex"
                flexWrap = "wrap"
                alignItems = "flex-end"
                gap = 0.5
            |}

        JSX.jsx
            $"""
        import InputLabel from '@mui/material/InputLabel';
        import MenuItem from '@mui/material/MenuItem';
        import FormControl from '@mui/material/FormControl';
        import Select from '@mui/material/Select';
        import Box from '@mui/material/Box';

        <Box sx={rowSx}>
        <FormControl variant="standard" sx={formControlSx}>
            <InputLabel id={props.label + "-label"}>{props.label}</InputLabel>
            <Select
            labelId={props.label + "-label"}
            id={props.label}
            name={props.label}
            value={displaySelected |> Option.defaultValue ""}
            onChange={handleChange}
            label={props.label}
            disabled={props.disabled}
            endAdornment={endAdornment}
            sx={sx}
            slotProps={selectSlotProps}
            >
                {items}
            </Select>
        </FormControl>
        {stepper}
        </Box>
        """
