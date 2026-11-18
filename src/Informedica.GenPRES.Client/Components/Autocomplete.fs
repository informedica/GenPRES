namespace Components


module Autocomplete =


    open System
    open Informedica.GenPRES.Shared
    open Fable.Core
    open Fable.Core.JsInterop


    [<JSX.Component>]
    let View
        (props:
            {|
                label: string
                selected: string option
                values: string[]
                updateSelected: string option -> unit
                isLoading: bool
                disabled: bool
                // whether the clear button is shown
                canClear: bool
                // the greyed text shown while no value is selected
                placeholder: string option
            |})
        =

        let handleChange =
            fun ev ->
                ev?target?innerText
                |> string
                |> function
                    | s when s |> String.isNullOrWhiteSpace || s = "undefined" -> None
                    | s -> s |> Some
                |> props.updateSelected

        let renderInput pars =
            // Copy the MUI-provided params before mutating to avoid React reuse issues,
            // then add the label, avoiding Fable 5 interpolation issue: https://github.com/fable-compiler/Fable/issues/3999
            let parsCopy = emitJsExpr pars "Object.assign({}, $0)"
            parsCopy?label <- props.label
            parsCopy?name <- props.label

            // a text field shows its placeholder only when the label is shrunk above the box
            match props.placeholder with
            | Some text when props.selected.IsNone ->
                parsCopy?placeholder <- text
                parsCopy?InputLabelProps <- emitJsExpr pars?InputLabelProps "Object.assign({}, $0, { shrink: true })"
            | _ -> ()

            JSX.jsx
                """
                <TextField {...parsCopy} />
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
            loading={props.isLoading}
            disabled={props.disabled}
            disableClearable={not props.canClear}
            value={props.selected |> Option.defaultValue ""}
            onChange={handleChange}
            options={props.values}
            renderInput={renderInput}
        >
        </Autocomplete>
        """
