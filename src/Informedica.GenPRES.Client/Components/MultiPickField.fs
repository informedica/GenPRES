namespace Components


/// A select field for several options, each option a row with a checkbox. The list stays open
/// while the user ticks options and closes on a click outside; the clear button clears every
/// selected option. A field without options is disabled and empty.
module MultiPickField =


    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz


    /// The label, the options as key and label pairs, the selected keys, the change handler,
    /// whether the options are loading, and whether the caller enables the field.
    type Props =
        {|
            label: string
            options: (string * string)[]
            selected: string[]
            onChange: string[] -> unit
            isLoading: bool
            enabled: bool
        |}


    let private formControlSx =
        {|
            minWidth = 150
            maxWidth = 400
        |}


    let private itemSx = {| maxWidth = 400 |}


    [<JSX.Component>]
    let View (props: Props) =
        let hasOptions = props.options |> Array.isEmpty |> not
        let disabled = not props.enabled || not hasOptions
        let selected = if hasOptions then props.selected else [||]
        let isClear = selected |> Array.isEmpty

        // a multiple select returns the array of the selected keys
        let handleChange =
            fun ev ->
                let keys: string[] = unbox ev?target?value
                props.onChange keys

        let clear = fun _ -> props.onChange [||]

        // the closed field shows the labels of the selected keys, in the order of the options
        let renderValue =
            fun (keys: string[]) ->
                props.options
                |> Array.filter (fun (k, _) -> keys |> Array.contains k)
                |> Array.map snd
                |> String.concat ", "

        let items =
            props.options
            |> Array.map (fun (k, v) ->
                let ticked = selected |> Array.contains k

                JSX.jsx
                    $"""
                import Checkbox from '@mui/material/Checkbox';
                import ListItemText from '@mui/material/ListItemText';

                <MenuItem key={k} value={k} sx={itemSx}>
                    <Checkbox checked={ticked} />
                    <ListItemText primary={v} />
                </MenuItem>
                """
            )

        let clearButton =
            match props.isLoading, isClear with
            | true, _ -> Mui.Icons.Downloading
            | false, true -> null
            | false, false ->
                JSX.jsx
                    $"""
                import IconButton from "@mui/material/IconButton";

                <IconButton onClick={clear}>
                    {Mui.Icons.Clear}
                </IconButton>
                """

        let selectSx = Mui.Styles.selectIconVisibilitySx (isClear && not props.isLoading)

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
            value={selected}
            onChange={handleChange}
            renderValue={renderValue}
            label={props.label}
            disabled={disabled}
            multiple={true}
            endAdornment={clearButton}
            sx={selectSx}
            >
                {items}
            </Select>
        </FormControl>
        """
