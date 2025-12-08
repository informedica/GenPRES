namespace Components


// Note: cannot set label on textfield in render function using the props.label
// therefore hardcoded versions of autocomplete. Need to fix this.
// Note: runs when building in development mode, but fails in production mode!
module Autocomplete =


    open System
    open Fable.Core
    open Fable.Core.JsInterop


    [<JSX.Component>]
    let View (props :
            {|
                label : string
                selected : string option
                values : string []
                updateSelected : string option -> unit
                isLoading : bool
            |}
        ) =

        let handleChange =
            fun ev ->
                ev?target?innerText
                |> string
                |> function
                | s when s |> String.IsNullOrWhiteSpace ||
                         s = "undefined" -> None
                | s -> s |> Some
                |> props.updateSelected

        let renderInput pars =
        // this is a hack, avoiding an interpolated string but using 
        // a normal string to work with fable 5 version: https://github.com/fable-compiler/Fable/issues/3999
        // the original code uses
        //    JSX.jsx $"""
        //        <TextField {{...pars}} label={props.label} />
        //    """
            JSX.jsx """
                <TextField {...pars} label={$props.label} />
            """
            
        JSX.jsx
            $"""
        import InputLabel from '@mui/material/InputLabel';
        import TextField from '@mui/material/TextField';
        import Autocomplete from '@mui/material/Autocomplete';
        import FormControl from '@mui/material/FormControl';

        <Autocomplete
            sx={ {| minWidth = 300 |} }
            id={props.label}
            blurOnSelect
            value={props.selected |> Option.defaultValue ""}
            onChange={handleChange}
            options={props.values}
            renderInput={renderInput}
        >
        </Autocomplete>
        """
