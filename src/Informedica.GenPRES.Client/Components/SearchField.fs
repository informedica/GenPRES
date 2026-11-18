namespace Components


/// A text field to search a list. It passes every change of the text to the caller, which decides
/// what matches; the clear button empties the text.
module SearchField =


    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz


    /// The label, the text, the change handler, and whether the field is disabled.
    type Props =
        {|
            label: string
            value: string
            onChange: string -> unit
            disabled: bool
        |}


    let private fieldSx = {| minWidth = 200 |}


    [<JSX.Component>]
    let View (props: Props) =
        let onChange = fun (ev: Browser.Types.Event) -> ev.target?value |> string |> props.onChange

        let clear = fun _ -> props.onChange ""

        let endAdornment =
            if props.value |> String.length = 0 then
                null
            else
                JSX.jsx
                    $"""
                import IconButton from '@mui/material/IconButton';
                import InputAdornment from '@mui/material/InputAdornment';

                <InputAdornment position="end">
                    <IconButton size="small" onClick={clear} aria-label="clear">
                        {Mui.Icons.Clear}
                    </IconButton>
                </InputAdornment>
                """

        let startAdornment =
            JSX.jsx
                $"""
            import InputAdornment from '@mui/material/InputAdornment';
            import SearchIcon from '@mui/icons-material/Search';

            <InputAdornment position="start">
                <SearchIcon fontSize="small" />
            </InputAdornment>
            """

        let slotProps =
            {|
                input =
                    {|
                        startAdornment = startAdornment
                        endAdornment = endAdornment
                    |}
            |}

        JSX.jsx
            $"""
        import TextField from '@mui/material/TextField';

        <TextField
            variant="standard"
            type="search"
            label={props.label}
            value={props.value}
            onChange={onChange}
            disabled={props.disabled}
            slotProps={slotProps}
            sx={fieldSx}
        />
        """
