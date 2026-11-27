namespace Components


/// The modal frame for every dialog: centred, 90 percent of the page wide up to a maximum width,
/// and scrolling inside when the content is taller than 90 percent of the page height.
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


    /// Shows the content in the frame while it is open; closing the frame calls onClose.
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
