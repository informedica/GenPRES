namespace Components


module MultipleSelect =


    open System
    open Shared
    open Fable.Core
    open Fable.Core.JsInterop


    [<JSX.Component>]
    let View
        (props:
            {|
                label: string
                selected: string[]
                values: (string * string)[]
                updateSelected: string[] -> unit
                isLoading: bool
                disabled: bool
            |})
        =

        let handleChange =
            fun ev ->
                let value = ev?target?value

                value
                |> string
                |> function
                    | s when s |> String.isNullOrWhiteSpace -> [||]
                    | s -> props.values |> Array.map snd |> Array.filter s.Contains

                |> props.updateSelected

        let clear = fun _ -> [||] |> props.updateSelected

        let items =
            props.values
            |> Array.map (fun (k, v) ->
                JSX.jsx
                    $"""
                <MenuItem key={k} value={k} sx = { {| maxWidth = 400 |} }>
                    {v}
                </MenuItem>
                """
            )

        let isClear = props.selected |> Array.isEmpty

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

        let selectSx = Mui.Styles.selectIconVisibilitySx (isClear && not props.isLoading)

        let formControlSx =
            {|
                minWidth = 150
                maxWidth = 400
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
            value={props.selected}
            onChange={handleChange}
            label={props.label}
            disabled={props.disabled}
            multiple={true}
            endAdornment={clearButton}
            sx={selectSx}
            >
                {items}
            </Select>
        </FormControl>
        """
