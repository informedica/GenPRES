namespace Components

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types



module Autocomplete =

    open Elmish
    open Fable.Core.JsInterop


    [<JSX.Component>]
    let View (props :
            {|
                label : string
                selected : string option
                values : string []
                updateSelected : string option -> unit
                isLoading : bool
//                minWidht : int
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
                |> fun x -> Logging.log "value is none: " x.IsNone; x
                |> props.updateSelected


        let renderInput lbl pars =
            JSX.jsx """
                <TextField {...pars} label={lbl} />
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
            value={props.selected |> Option.defaultValue ""}
            onChange={handleChange}
            options={props.values}
            renderInput={renderInput props.label}
        >
        </Autocomplete>
        """

