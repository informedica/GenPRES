namespace Components


/// The one way a view is shown over the page: a modal, centred, as wide as the page allows
/// up to a bound, scrolling inside itself when its content is taller than the page. What is
/// shown inside is the caller's; the shell only frames it, so every dialog frames alike.
module DialogShell =


    open Fable.Core
    open Feliz


    let private shellSx (maxWidth: int) =
        {|
            position = "absolute"
            top = "50%"
            left = "50%"
            transform = "translate(-50%, -50%)"
            width = "90vw"
            maxWidth = maxWidth
            maxHeight = "90vh"
            overflowY = "auto"
            overflowX = "hidden"
            bgcolor = "background.paper"
            boxShadow = 24
            borderRadius = "16px"
        |}


    /// The frame: open or not, what closing it does, how wide it may grow, and its content.
    [<JSX.Component>]
    let View
        (props:
            {|
                isOpen: bool
                onClose: unit -> unit
                maxWidth: int
                children: JSX.Element
            |})
        =
        let onClose = fun _ -> props.onClose ()
        let sx = shellSx props.maxWidth

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Modal from '@mui/material/Modal';

        <Modal open={props.isOpen} onClose={onClose}>
            <Box sx={sx}>
                {props.children}
            </Box>
        </Modal>
        """
