namespace Components


/// A small bin icon that removes the section it sits in, such as from a heading or the summary of
/// a disclosure. A click, Enter or Space calls onRemove, unless busy, and does not propagate, so a
/// summary around it does not open or close. The bin is a span with the button role, because the
/// summary of a disclosure is a button and a button cannot contain another button.
module RemoveIconButton =


    open Fable.Core
    open Feliz


    /// The name for a screen reader, the remove handler, and whether a change is under way.
    type Props =
        {|
            label: string
            onRemove: unit -> unit
            busy: bool
        |}


    let private binSx =
        {|
            marginLeft = "auto"
            display = "inline-flex"
            alignItems = "center"
            cursor = "pointer"
            padding = "4px"
            borderRadius = "50%"
            ``&:hover`` = {| backgroundColor = "rgba(0, 0, 0, 0.04)" |}
        |}


    [<JSX.Component>]
    let View (props: Props) =
        let remove () =
            if not props.busy then
                props.onRemove ()

        let handleClick (e: Browser.Types.Event) =
            e.stopPropagation ()
            remove ()

        let handleKeyDown (e: Browser.Types.KeyboardEvent) =
            match e.key with
            | "Enter"
            | " " ->
                e.preventDefault ()
                e.stopPropagation ()
                remove ()
            | _ -> ()

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import DeleteIcon from '@mui/icons-material/Delete';
        <Box
            component="span"
            role="button"
            tabIndex={0}
            aria-label={props.label}
            aria-disabled={props.busy}
            onClick={handleClick}
            onKeyDown={handleKeyDown}
            sx={binSx}
        >
            <DeleteIcon fontSize="small" />
        </Box>
        """
