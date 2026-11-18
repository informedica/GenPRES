namespace Components


/// Disables the content as a whole while a request runs: the content takes no click, key or focus,
/// and a small spinner lies over it. It fills the column it is placed in.
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
        // The backdrop is a sibling of the content, so scrolling content cannot carry it out of
        // view. It has no transition, so it stops catching clicks as soon as the request ends.
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
