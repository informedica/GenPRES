namespace Components


module SimpleSelect =


    open System
    open Shared
    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz


    /// A field's state for the trail: whether it holds a value and what it lists; never the value.
    let fieldState (selected: string option) (listed: int) (reopening: bool) =
        let held = if selected.IsSome then "picked" else "none"
        let list = if listed = 0 then "range" else string listed
        let reopen = if reopening then " reopening" else ""
        $"%s{held} of %s{list}%s{reopen}"


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
                // the field cannot be opened, but it is not greyed and what it holds still
                // answers, so the cross inside it can be pressed
                readOnly: bool
                hasClear: bool
                // whether the value can be stepped beside the select: a single value that can
                // be stepped is not a fixed one, and is not drawn as one
                canStep: bool
                severity: Types.Severity
                minWidth: int option
                // what a click or a key on the select does beyond choosing, told to a screen
                // reader on the element that has the focus
                description: string option
                // a field the user narrowed: opening it clears its choice first and the list shows
                // what the server answers; None opens the list as it is
                reopen: (unit -> unit) option
                // the list of a reopen closed without a pick: the page puts back what it showed
                restore: unit -> unit
                // a request of the page under way, also a step shown before its answer: a reopen
                // then does nothing, since the clear would wait behind it with nothing to put back
                busy: bool
            |})
        =

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

        let selectSlotProps =
            if isMobile then
                {| menu = {| slotProps = {| paper = {| style = {| maxHeight = 400 |} |} |} |} |}
                |> box
            else
                {| |} |> box

        // the list is opened by the select and by a reopen; a reopen opens it at once, showing
        // the value held until the answer brings the others
        let isOpen, setOpen = React.useState false
        // a reopen under way, whether its answer has been asked for, and the value held before it
        let reopening = React.useRef false
        let sawLoading = React.useRef false
        let prior = React.useRef props.selected
        // the list of a reopen waits for its answer: drawn under a loading overlay meanwhile
        let waiting, setWaiting = React.useState false

        // a lone range is no list: its click picks the median
        let listed =
            match props.values with
            | [| ("range", _) |] -> 0
            | values -> values.Length

        let handleChange =
            fun ev ->
                let value = ev?target?value

                // a pick ends a reopen: nothing to put back
                reopening.current <- false
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
                (fieldState props.selected listed reopening.current)

        let handleOpen =
            fun _ ->
                match props.reopen with
                | Some _ when props.isLoading || props.busy ->
                    trace
                        "open"
                        [
                            (if props.isLoading then
                                 "blocked loading"
                             else
                                 "blocked busy")
                        ]
                | Some reopen ->
                    reopening.current <- true
                    sawLoading.current <- false
                    prior.current <- props.selected
                    reopen ()
                    setWaiting true
                    setOpen true
                | None -> setOpen true

        // closed without a pick after a reopen: the page puts back what it showed
        let handleClose =
            fun _ ->
                setOpen false
                setWaiting false

                if reopening.current then
                    trace "close" [ "restore" ]
                    reopening.current <- false
                    props.restore ()

        // the answer to a reopen with no values to list closes the list; the field shows the range
        React.useEffect (
            (fun () ->
                if reopening.current then
                    if props.isLoading then
                        sawLoading.current <- true
                    elif sawLoading.current then
                        setWaiting false

                        match FieldOpenPolicy.reopened listed with
                        | FieldOpenPolicy.Reopened.NoList ->
                            trace "reopened" [ "NoList" ]
                            reopening.current <- false
                            setOpen false
                        | FieldOpenPolicy.Reopened.ShowList -> trace "reopened" [ "ShowList" ]
            ),
            [| box props.isLoading; box listed |]
        )

        let menuItemSx =
            {|
                maxWidth = 400
                paddingY = if isMobile then 0.25 else 0.75
            |}

        let items =
            props.values
            |> Array.mapi (fun i (k, v) ->
                JSX.jsx
                    $"""
                <MenuItem key={i} value={k} sx={menuItemSx} dense={isMobile} >
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

        // the cross clears the value whether or not the value has a stepper beside it
        let endAdornment =
            if not isClear && props.hasClear then
                Some clearButton
            else
                None

        let displayProps =
            match props.description with
            | Some text -> {| ``aria-description`` = text |} |> box
            | None -> {| |} |> box

        let hasInteraction = props.canStep || props.values.Length > 1 || props.reopen.IsSome

        // the arrow shows where it opens something: not beside the cross, not on a field that
        // cannot be opened
        let iconVisibility =
            if endAdornment.IsNone && not props.readOnly then
                "visible"
            else
                "hidden"

        let sx =
            match props.severity |> Models.Severity.isRaised, hasInteraction with
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

        // while a reopen waits or lists, the value held before it stays chosen when it is offered
        let value =
            match props.selected with
            | Some s -> s
            | None when reopening.current ->
                prior.current
                |> Option.filter (fun p -> props.values |> Array.exists (fst >> (=) p))
                |> Option.defaultValue ""
            | None -> ""

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

        <FormControl variant="standard" sx={formControlSx}>
            <InputLabel id={props.label + "-label"}>{props.label}</InputLabel>
            <Select
            labelId={props.label + "-label"}
            id={props.label}
            name={props.label}
            value={value}
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
        """
