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
                isLoading: bool
                disabled: bool
                hasClear: bool
                // whether the value can be stepped beside the select: a single value that can
                // be stepped is not a fixed one, and is not drawn as one
                canStep: bool
                severity: Types.Severity
                minWidth: int option
            |})
        =

        let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

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
            props.values
            |> Array.mapi (fun i (k, v) ->
                JSX.jsx
                    $"""
                <MenuItem key={i} value={k} sx={menuItemSx} dense={isMobile} >
                    {v}
                </MenuItem>
                """
            )

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

        // a field that cannot be opened can still be emptied, and emptying it is how the user
        // widens the filter again. An input that is disabled disables what it holds, so in that
        // one case the cross stands beside the field rather than inside it.
        let clearBeside = props.disabled && props.hasClear && not isClear

        // the cross clears the value whether or not the value has a stepper beside it
        let endAdornment =
            if not isClear && props.hasClear && not clearBeside then
                Some clearButton
            else
                None

        let hasInteraction = props.canStep || props.values.Length > 1

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

        // in a row beside a stepper the select takes the width the stepper leaves
        let formControlSx =
            {|
                minWidth = props.minWidth |> Option.defaultValue 150
                maxWidth = "100%"
                flexGrow = 1
            |}

        let besideSx =
            {|
                display = "flex"
                alignItems = "flex-end"
                flexGrow = 1
                minWidth = 0
            |}

        let field =
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
            value={props.selected |> Option.defaultValue ""}
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
        """

        if not clearBeside then
            field
        else
            JSX.jsx
                $"""
            import Box from '@mui/material/Box';

            <Box sx={besideSx}>
                {field}
                {clearButton}
            </Box>
            """
