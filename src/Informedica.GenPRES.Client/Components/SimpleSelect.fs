namespace Components


module SimpleSelect =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Policies


    open System
    open Informedica.GenPRES.Shared
    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz


    /// Describes a field for the step trail: whether a value is picked, how many values are listed,
    /// and whether it recalculates. The value itself is never included.
    let fieldState (selected: string option) (listed: int) (recalculating: bool) =
        let held = if selected.IsSome then "picked" else "none"
        let list = if listed = 0 then "range" else string listed
        let reCalc = if recalculating then " recalculating" else ""
        $"%s{held} of %s{list}%s{reCalc}"


    [<JSX.Component>]
    let View
        (props:
            {|
                label: string
                selected: string option
                values: (string * string)[]
                updateSelected: string option -> unit
                isLoading: bool
                disabled: bool
                // the field cannot be opened, but it is not greyed out and still reacts, so its
                // clear button works
                readOnly: bool
                hasClear: bool
                // whether step buttons stand beside the select; a single value that can be stepped
                // is not drawn as a fixed value
                canStep: bool
                severity: Severity
                minWidth: int option
                // what a click or a key on the select does besides choosing, read to a screen
                // reader on the focused element
                description: string option
                // for a field with one value: opening the list recalculates the field first, and the
                // list shows the result; None opens the list as it is
                reCalc: (unit -> unit) option
                // called when the list of a recalculation closes without a pick, so the page restores
                // what it showed
                restore: unit -> unit
                // a page request runs, also for a step shown before its answer; opening the list then
                // starts no recalculation, since it would wait behind that request with nothing to restore
                busy: bool
                // the greyed text shown while there are values to pick and none is selected
                placeholder: string option
                // the value the placeholder stands for, if it is one of the values; the list opens
                // on it, shown selected, while the box shows the placeholder
                placeholderValue: string option
            |})
        =

        let isMobile = Mui.Hooks.useIsMobile ()

        let selectSlotProps =
            if isMobile then
                {| menu = {| slotProps = {| paper = {| style = {| maxHeight = 400 |} |} |} |} |}
                |> box
            else
                {| |} |> box

        // The list opens from the select and from a recalculation. A recalculation opens it at once
        // with the prior value, until the answer brings the other values.
        let isOpen, setOpen = React.useState false
        // whether a recalculation runs, whether its loading has been seen, and the value selected
        // before it
        let recalculating = React.useRef false
        let sawLoading = React.useRef false
        let prior = React.useRef props.selected
        // whether the list of a recalculation waits for the answer; a loading overlay covers it then
        let waiting, setWaiting = React.useState false

        // a single range is not a list: a click on it picks the median
        let listed =
            match props.values with
            | [| ("range", _) |] -> 0
            | values -> values.Length

        let handleChange =
            fun ev ->
                let value = ev?target?value

                // a pick ends a recalculation, so there is nothing to restore
                recalculating.current <- false
                setWaiting false
                setOpen false

                value
                |> string
                |> function
                    | s when s |> String.isNullOrWhiteSpace -> None
                    | s -> s |> Some
                |> props.updateSelected

        let clear = fun _ -> None |> props.updateSelected

        let trace event effects =
            StepTrail.event
                "Field"
                $"%s{event} %s{props.label}"
                effects
                (fieldState props.selected listed recalculating.current)

        let handleOpen =
            fun _ ->
                match props.reCalc with
                | Some _ when props.isLoading || props.busy ->
                    trace
                        "open"
                        [
                            (if props.isLoading then
                                 "blocked loading"
                             else
                                 "blocked busy")
                        ]
                | Some reCalc ->
                    recalculating.current <- true
                    sawLoading.current <- false
                    prior.current <- props.selected
                    reCalc ()
                    setWaiting true
                    setOpen true
                | None -> setOpen true

        // closing without a pick after a recalculation lets the page restore what it showed
        let handleClose =
            fun _ ->
                setOpen false
                setWaiting false

                if recalculating.current then
                    trace "close" [ "restore" ]
                    recalculating.current <- false
                    props.restore ()

        // when the answer to a recalculation has no values to list, the list closes and the field
        // shows the range
        React.useEffect (
            (fun () ->
                if recalculating.current then
                    if props.isLoading then
                        sawLoading.current <- true
                    elif sawLoading.current then
                        setWaiting false

                        match FieldOpenPolicy.afterReCalc listed with
                        | FieldOpenPolicy.AfterReCalc.NoList ->
                            trace "recalculated" [ "NoList" ]
                            recalculating.current <- false
                            setOpen false
                        | FieldOpenPolicy.AfterReCalc.ShowList -> trace "recalculated" [ "ShowList" ]
            ),
            [| box props.isLoading; box listed |]
        )

        let menuItemSx =
            {|
                maxWidth = 400
                paddingY = if isMobile then 0.25 else 0.75
            |}

        // The select marks only its selected value as selected, so the value the placeholder
        // stands for gets the selected class and the focus here, which also scrolls it into view.
        let placeholderValue = props.placeholderValue |> Option.filter (fun _ -> props.selected.IsNone)

        let items =
            props.values
            |> Array.mapi (fun i (k, v) ->
                let isPlaceholderValue = placeholderValue = Some k
                let className = if isPlaceholderValue then "Mui-selected" else null

                JSX.jsx
                    $"""
                <MenuItem key={i} value={k} sx={menuItemSx} dense={isMobile} className={className} autoFocus={isPlaceholderValue} >
                    {v}
                </MenuItem>
                """
            )

        // laid over the list, not added to it, so nothing in the list moves while the answer comes
        let waitingSx =
            {|
                position = "absolute"
                top = 0
                right = 0
                bottom = 0
                left = 0
                display = "flex"
                alignItems = "center"
                justifyContent = "center"
                backgroundColor = "rgba(255, 255, 255, 0.6)"
                lineHeight = "normal"
                zIndex = 1
            |}

        let waitingOverlay =
            JSX.jsx
                $"""
            import ListSubheader from '@mui/material/ListSubheader';
            import CircularProgress from '@mui/material/CircularProgress';

            <ListSubheader key="waiting" sx={waitingSx}>
                <CircularProgress size={20} />
            </ListSubheader>
            """

        let items =
            if waiting then
                Array.append items [| waitingOverlay |]
            else
                items

        let isClear = props.selected |> Option.defaultValue "" |> String.isNullOrWhiteSpace

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

        // the clear button clears the value, also when step buttons stand beside it
        let endAdornment =
            if not isClear && props.hasClear then
                Some clearButton
            else
                None

        let displayProps =
            match props.description with
            | Some text -> {| ``aria-description`` = text |} |> box
            | None -> {| |} |> box

        let hasInteraction = props.canStep || props.values.Length > 1 || props.reCalc.IsSome

        // the dropdown arrow shows only when the field can be opened and has no clear button
        let iconVisibility =
            if endAdornment.IsNone && not props.readOnly then
                "visible"
            else
                "hidden"

        let sx =
            match props.severity |> Severity.isRaised, hasInteraction with
            | true, _ ->
                {| ``& .MuiSelect-icon`` = {| visibility = iconVisibility |} |}
                |> box
                |> Mui.Styles.markSx props.severity
            | false, false ->
                {|
                    ``& .MuiSelect-icon`` = {| visibility = iconVisibility |}
                    backgroundColor = "action.hover"
                    borderRadius = "4px"
                    padding = "2px 8px"
                |}
                |> box
            | false, true -> {| ``& .MuiSelect-icon`` = {| visibility = iconVisibility |} |} |> box

        // during a recalculation the value selected before it stays selected while it is among
        // the values
        let value =
            match props.selected with
            | Some s -> s
            | None when recalculating.current ->
                prior.current
                |> Option.filter (fun p -> props.values |> Array.exists (fst >> (=) p))
                |> Option.defaultValue ""
            | None -> ""

        // the placeholder shows only when the box shows no value and there are values, so the
        // two never overlap
        let placeholder =
            match props.placeholder with
            | Some text when value = "" && props.values.Length > 0 -> Some text
            | _ -> None

        let placeholderSx = {| color = "text.disabled" |}

        let renderValue =
            match placeholder with
            | Some text ->
                fun (_: string) ->
                    JSX.jsx
                        $"""
                    import Box from '@mui/material/Box';
                    <Box component="span" sx={placeholderSx}>{text}</Box>
                    """
                |> box
            | None -> null

        // A label inside the box would cover the placeholder, so the label shrinks above the box
        // while the placeholder shows. Otherwise shrink is left undefined and MUI decides.
        let labelShrink =
            match placeholder with
            | Some _ -> box true
            | None -> JS.undefined

        // The tooltip shows the label of the selected value, so a label cut off with an ellipsis
        // can still be read. It opens only when the label does not fit and the list is closed.
        let tipOpen, setTipOpen = React.useState false

        let selectedLabel =
            props.values
            |> Array.tryFind (fst >> (=) value)
            |> Option.map snd
            |> Option.defaultValue ""

        // MUI may call this after its enter delay, when the event no longer has a current target,
        // so the field is found from the element under the pointer.
        let handleTipOpen (e: Browser.Types.Event) =
            let field = e.target?closest (".MuiFormControl-root")

            let display =
                if isNull field then
                    null
                else
                    field?querySelector (".MuiSelect-select")

            let overflows =
                not (isNull display)
                && (display?scrollWidth: float) > (display?clientWidth: float)

            setTipOpen overflows

        let handleTipClose = fun _ -> setTipOpen false

        let showTip = tipOpen && not isOpen && selectedLabel <> ""

        // in a row beside a stepper the select takes the width the stepper leaves
        let formControlSx =
            {|
                minWidth = props.minWidth |> Option.defaultValue 150
                maxWidth = "100%"
                flexGrow = 1
            |}

        JSX.jsx
            $"""
        import InputLabel from '@mui/material/InputLabel';
        import MenuItem from '@mui/material/MenuItem';
        import FormControl from '@mui/material/FormControl';
        import Select from '@mui/material/Select';
        import Tooltip from '@mui/material/Tooltip';

        <Tooltip title={selectedLabel} open={showTip} onOpen={handleTipOpen} onClose={handleTipClose}>
        <FormControl variant="standard" sx={formControlSx}>
            <InputLabel id={props.label + "-label"} shrink={labelShrink}>{props.label}</InputLabel>
            <Select
            labelId={props.label + "-label"}
            id={props.label}
            name={props.label}
            value={value}
            displayEmpty={placeholder.IsSome}
            renderValue={renderValue}
            open={isOpen}
            onOpen={handleOpen}
            onClose={handleClose}
            onChange={handleChange}
            label={props.label}
            disabled={props.disabled}
            readOnly={props.readOnly}
            endAdornment={endAdornment}
            sx={sx}
            slotProps={selectSlotProps}
            SelectDisplayProps={displayProps}
            >
                {items}
            </Select>
        </FormControl>
        </Tooltip>
        """
