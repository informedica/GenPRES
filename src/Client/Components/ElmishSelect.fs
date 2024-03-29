namespace Components

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types



open Elmish
open Fable.Core.JsInterop


module ElmishSelect =


    module private Elmish =


        type State = string option


        type Msg = | Select of string | Clear


        let init s : State * Cmd<Msg> = s, Cmd.none


        let update updateSelected (msg : Msg) _ : State * Cmd<Msg> =
            match msg with
            | Clear    -> None, Cmd.none
            | Select s -> Some s, Cmd.none
            |> fun (s, c) ->
                s |> updateSelected
                s, Cmd.none


    open Elmish


    [<JSX.Component>]
    let View (props :
            {|
                label : string
                selected : string option
                values : (int *string) []
                updateSelected : string option -> unit
            |}
        ) =
        let depArr = [||] //[| box props.dispatch |]
        let state, dispatch = React.useElmish(init props.selected, update props.updateSelected, depArr)

        let handleChange =
            fun ev ->
                let value = ev?target?value

                value
                |> string
                |> Select
                |> dispatch

        let clear = fun _ -> Clear |> dispatch

        let items =
            props.values
            |> Array.map (fun (k, v) ->
                JSX.jsx
                    $"""
                <MenuItem key={k} value={k}>{v}</MenuItem>
                """
            )

        let isClear = state |> Option.defaultValue "" |> String.IsNullOrWhiteSpace

        let clearButton =
            JSX.jsx
                $"""
            import ClearIcon from '@mui/icons-material/Clear';
            import IconButton from "@mui/material/IconButton";

            <IconButton sx={ {| visibility = if isClear then "hidden" else "visible" |} } onClick={clear}>
                <ClearIcon/>
            </IconButton>
            """

        JSX.jsx
            $"""
        import InputLabel from '@mui/material/InputLabel';
        import MenuItem from '@mui/material/MenuItem';
        import FormControl from '@mui/material/FormControl';
        import Select from '@mui/material/Select';

        <div>
        <FormControl variant="standard" sx={ {| m = 1; minWidth = 120 |} }>
            <InputLabel id="demo-simple-select-standard-label">{props.label}</InputLabel>
            <Select
            labelId="demo-simple-select-standard-label"
            id="demo-simple-select-standard"
            value={state |> Option.defaultValue ""}
            onChange={handleChange}
            label={props.label}
            sx={ {| ``& .MuiSelect-icon`` = {| visibility = if isClear then "visible" else "hidden" |} |} }
            endAdornment={clearButton}
            >
                {items}
            </Select>
        </FormControl>
        </div>
        """

