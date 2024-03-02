namespace Components

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types


// Note: cannot set label on textfield in render function using the props.label
// therefore hardcoded versions of autocomplete. Need to fix this.
// Note: runs when building in development mode, but fails in production mode!
module Autocomplete =

    open Elmish
    open Fable.Core.JsInterop


    [<JSX.Component>]
    let Indications (props :
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
                |> fun x -> Logging.log "value is none: " x.IsNone; x
                |> props.updateSelected

        let renderInput pars =
            JSX.jsx """
                <TextField {...pars} label="Indicaties" />
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
//            disabled={props.isLoading |> not}
            value={props.selected |> Option.defaultValue ""}
            onChange={handleChange}
            options={props.values}
            renderInput={renderInput}
        >
        </Autocomplete>
        """



    [<JSX.Component>]
    let Medication (props :
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
                |> fun x -> Logging.log "value is none: " x.IsNone; x
                |> props.updateSelected

        let renderInput pars =
            JSX.jsx """
                <TextField {...pars} label="Medicatie" />
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
//            disabled={props.isLoading |> not}
            value={props.selected |> Option.defaultValue ""}
            onChange={handleChange}
            options={props.values}
            renderInput={renderInput}
        >
        </Autocomplete>
        """



    [<JSX.Component>]
    let Routes (props :
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
                |> fun x -> Logging.log "value is none: " x.IsNone; x
                |> props.updateSelected

        let renderInput pars =
            JSX.jsx """
                <TextField {...pars} label="Routes" />
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
//            disabled={props.isLoading |> not}
            value={props.selected |> Option.defaultValue ""}
            onChange={handleChange}
            options={props.values}
            renderInput={renderInput}
        >
        </Autocomplete>
        """



    [<JSX.Component>]
    let DoseTypes (props :
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
                |> fun x -> Logging.log "value is none: " x.IsNone; x
                |> props.updateSelected

        let renderInput pars =
            JSX.jsx """
                <TextField {...pars} label="Doseer types" />
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
//            disabled={props.isLoading |> not}
            value={props.selected |> Option.defaultValue ""}
            onChange={handleChange}
            options={props.values}
            renderInput={renderInput}
        >
        </Autocomplete>
        """
