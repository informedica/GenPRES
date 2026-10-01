namespace Components


/// A bin that removes the section it sits in, small enough for a heading or the summary of a
/// disclosure. The click stays with the bin, so a summary it sits in does not open or close,
/// and it removes nothing while the caller is busy.
module RemoveIconButton =


    open Fable.Core
    open Feliz


    /// The bin: what a click does, and whether a change is under way.
    type Props =
        {|
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
        let handleClick (e: Browser.Types.Event) =
            e.stopPropagation ()

            if not props.busy then
                props.onRemove ()

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import DeleteIcon from '@mui/icons-material/Delete';
        <Box component="span" onClick={handleClick} sx={binSx}>
            <DeleteIcon fontSize="small" />
        </Box>
        """
