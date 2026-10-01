namespace Components


/// Content with a spinner laid over it while it reloads. The spinner takes no room, so the
/// content keeps its place and its height; content without height yet, such as an empty list,
/// gets room for the spinner while it loads.
module LoadingOverlay =


    open Fable.Core
    open Feliz


    /// The overlay: whether it is loading, and the content it lies over.
    type Props =
        {|
            isLoading: bool
            children: JSX.Element
        |}


    let private overlaySx =
        {|
            position = "absolute"
            inset = 0
            display = "flex"
            alignItems = "center"
            justifyContent = "center"
            pointerEvents = "none"
        |}


    [<JSX.Component>]
    let View (props: Props) =
        // the room of a spinner with padding around it, so it never lies over what comes before
        let containerSx =
            {|
                position = "relative"
                minHeight = if props.isLoading then 56 else 0
            |}

        let spinner =
            if props.isLoading then
                JSX.jsx
                    $"""
                import CircularProgress from '@mui/material/CircularProgress';
                <CircularProgress size={24} />
                """
            else
                null

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        <Box sx={containerSx}>
            {props.children}
            <Box sx={overlaySx}>{spinner}</Box>
        </Box>
        """
