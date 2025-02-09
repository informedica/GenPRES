namespace Components

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types

open Shared.Utils

module MultipleSelect =

    open Elmish
    open Fable.Core.JsInterop

    [<JSX.Component>]
    let View (props :
            {|
                label : string
                selected : string []
                values : (string * string) []
                updateSelected : string [] -> unit
                isLoading : bool
            |}
        ) =

        let handleChange =
            fun ev ->
                let value = ev?target?value

                value
                |> string
                |> function
                | s when s |> String.IsNullOrWhiteSpace -> [||]
                | s ->
                    s.Split ","
                |> props.updateSelected

        let clear = fun _ -> [||] |> props.updateSelected

        let items =
            props.values
            |> Array.mapi (fun i (k, v) ->
                JSX.jsx
                    $"""
                <MenuItem key={i} value={k} sx = { {| maxWidth = 400 |} }>
                    {v}
                </MenuItem>
                """
            )

        let isClear = props.selected |> Array.isEmpty

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

        <FormControl variant="standard" sx={ {| minWidth = 150; maxWidth = 400 |} }>
            <InputLabel id={props.label}>{props.label}</InputLabel>
            <Select
            labelId={props.label}
            id={props.label}
            value={props.selected}
            onChange={handleChange}
            label={props.label}
            multiple={true}
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
        """

