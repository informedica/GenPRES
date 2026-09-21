namespace Components


module StateSelect =


    open System
    open Shared
    open Fable.Core
    open Feliz
    open Fable.Core.JsInterop


    [<JSX.Component>]
    let View
        (props:
            {|
                label: string
                selected: string option
                values: (int * string)[]
                updateSelected: string option -> unit
            |})
        =
        let handleChange =
            fun ev ->
                ev?target?value
                |> string
                |> function
                    | s when s |> String.isNullOrWhiteSpace -> None
                    | s -> s |> Some
                |> props.updateSelected

        let clear = fun _ -> props.updateSelected None

        let items =
            props.values
            |> Array.map (fun (k, v) ->
                JSX.jsx
                    $"""
                <MenuItem key={k} value={v}>{v}</MenuItem>
                """
            )

        let isClear = props.selected |> Option.defaultValue "" |> String.isNullOrWhiteSpace

        let clearButton =
            JSX.jsx
                $"""
            import ClearIcon from '@mui/icons-material/Clear';
            import IconButton from "@mui/material/IconButton";

            <IconButton sx={Mui.Styles.clearButtonVisibilitySx isClear} onClick={clear}>
                <ClearIcon/>
            </IconButton>
            """

        let formControlSx =
            {|
                margin = 1
                minWidth = 120
            |}

        JSX.jsx
            $"""
        import InputLabel from '@mui/material/InputLabel';
        import MenuItem from '@mui/material/MenuItem';
        import FormControl from '@mui/material/FormControl';
        import Select from '@mui/material/Select';

        <div>
        <FormControl variant="standard" sx={formControlSx}>
            <InputLabel id="demo-simple-select-standard-label">{props.label}</InputLabel>
            <Select
            labelId="demo-simple-select-standard-label"
            id="demo-simple-select-standard"
            value={props.selected |> Option.defaultValue ""}
            onChange={handleChange}
            label={props.label}
            sx={Mui.Styles.selectIconVisibilitySx isClear}
            endAdornment={clearButton}
            >
                {items}
            </Select>
        </FormControl>
        </div>
        """
