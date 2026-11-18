namespace Components


/// Lays a spinner over the content while it loads. The spinner takes no room, so the content
/// keeps its place. Content without height, such as an empty list, gets room for the spinner.
module LoadingOverlay =


    open Fable.Core
    open Feliz


    /// Whether the content is loading, and the content.
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
        // room for a spinner with padding, so the spinner never lies over the content above
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
