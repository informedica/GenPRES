namespace Components


/// A field to search a list by text: what is typed narrows the list as it is typed, and the
/// cross empties it. It holds no list and knows no matching; it tells the caller the text and
/// the caller decides what matches.
module SearchField =


    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz


    /// The field: its label, the text in it, what typing does, and whether it can be used.
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
