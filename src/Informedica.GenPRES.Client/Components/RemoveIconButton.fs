namespace Components


/// A bin that removes the section it sits in, small enough for a heading or the summary of a
/// disclosure. A click, Enter or Space removes; each stays with the bin, so a summary it sits in
/// does not open or close, and it removes nothing while the caller is busy. The bin is not a
/// button element, since the summary of a disclosure is a button and one button cannot hold
/// another; it takes the role of a button and the keyboard focus instead.
module RemoveIconButton =


    open Fable.Core
    open Feliz


    /// The bin: the name a screen reader gives it, what a click does, and whether a change is under way.
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
