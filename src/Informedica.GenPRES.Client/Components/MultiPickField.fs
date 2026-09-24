namespace Components


/// Some choices among some options, and visibly so: every option is a row with a checkbox,
/// the chosen ones ticked, so nobody has to know the list is a multi-select to use it as one.
/// The list stays open while the user ticks, and closes when they click away; the cross clears
/// every choice at once. A field with nothing to choose is disabled and empty.
module MultiPickField =


    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz


    /// The field: its label, the options as key and label, the keys chosen, what choosing does,
    /// and whether the caller has the field enabled.
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

        // a multiple select answers with the array of the keys ticked
        let handleChange =
            fun ev ->
                let keys: string[] = unbox ev?target?value
                props.onChange keys

        let clear = fun _ -> props.onChange [||]

        // what the closed field shows: the labels of the keys ticked, in the options' order
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
