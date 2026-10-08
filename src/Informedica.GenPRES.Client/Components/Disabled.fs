namespace Components


/// Content disabled as a whole while a request is out: it takes no click, no key and no focus,
/// and a small spinner lies over it. A control that has the focus loses it. It fills the column
/// it is placed in.
module Disabled =


    open Fable.Core
    open Feliz


    /// Whether the content is disabled, and the content.
    type Props =
        {|
            isDisabled: bool
            children: JSX.Element
        |}


    // the backdrop is clipped by this box, so it never lies over what is around it
    let containerSx =
        {|
            position = "relative"
            display = "flex"
            flexDirection = "column"
            flexGrow = 1
            minHeight = 0
        |}


    let contentSx =
        {|
            display = "flex"
            flexDirection = "column"
            flexGrow = 1
            minHeight = 0
        |}


    let backdropSx = {| position = "absolute" |}


    [<JSX.Component>]
    let View (props: Props) =
        // the backdrop is beside the content, not in it: content that scrolls would carry it
        // out of view; no fade, so it catches no click once the request is answered
        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Backdrop from '@mui/material/Backdrop';
        import CircularProgress from '@mui/material/CircularProgress';

        <Box sx={containerSx}>
            <Box sx={contentSx} inert={props.isDisabled}>
                {props.children}
            </Box>
            <Backdrop sx={backdropSx} open={props.isDisabled} invisible={true} transitionDuration={0}>
                <CircularProgress size={24} />
            </Backdrop>
        </Box>
        """
