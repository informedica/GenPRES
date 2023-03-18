namespace Components

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types



module SimpleSelect =

    open Elmish
    open Fable.Core.JsInterop


    [<JSX.Component>]
    let View (props :
            {|
                label : string
                selected : string option
                values : (string * string) []
                updateSelected : string option -> unit
                isLoading : bool
            |}
        ) =

        let handleChange =
            fun ev ->
                let value = ev?target?value

                value
                |> string
                |> function
                | s when s |> String.IsNullOrWhiteSpace -> None
                | s -> s |> Some
                |> props.updateSelected

        let clear = fun _ -> None |> props.updateSelected

        let items =
            props.values
            |> Array.mapi (fun i (k, v) ->
                JSX.jsx
                    $"""
                <MenuItem key={i} value={k} sx = { {| maxWidth = 300 |} }>
                    {v}
                </MenuItem>
                """
            )

        let isClear = props.selected |> Option.defaultValue "" |> String.IsNullOrWhiteSpace

        let clearButton =
            match props.isLoading, isClear with
            | true, _      -> Mui.Icons.Downloading
            | false, true  -> JSX.jsx "<></>"
            | false, false ->
                JSX.jsx
                    $"""
                import ClearIcon from '@mui/icons-material/Clear';
                import IconButton from "@mui/material/IconButton";

                <IconButton onClick={clear}>
                    {Mui.Icons.Clear}
                </IconButton>
                """

        JSX.jsx
            $"""
        import InputLabel from '@mui/material/InputLabel';
        import MenuItem from '@mui/material/MenuItem';
        import FormControl from '@mui/material/FormControl';
        import Select from '@mui/material/Select';

        <div id={props.label} key={props.label}>
        <FormControl variant="standard" sx={ {| m = 1; minWidth = 120; maxWidth = 200 |} }>
            <InputLabel id={props.label}>{props.label}</InputLabel>
            <Select
            labelId={props.label}
            id={props.label}
            value={props.selected |> Option.defaultValue ""}
            onChange={handleChange}
            label={props.label}
            endAdornment={clearButton}
            sx=
                {
                    {| ``& .MuiSelect-icon`` =
                        {|
                            visibility = if isClear && not props.isLoading then "visible" else "hidden"
                        |}
                    |}
                }
            >
                {items}
            </Select>
        </FormControl>
        </div>
        """

