namespace Components


/// A dialog that asks to confirm an action that cannot be undone. The confirm button is the
/// primary action on the right, the cancel button is on the left, and closing the dialog in any
/// other way cancels.
module ConfirmDialog =


    open Fable.Core
    open Feliz


    /// Shows the title and the text with a confirm and a cancel button.
    [<JSX.Component>]
    let View
        (props:
            {|
                isOpen: bool
                title: string
                text: string
                confirmLabel: string
                cancelLabel: string
                onConfirm: unit -> unit
                onCancel: unit -> unit
            |})
        =
        let onClose = fun _ -> props.onCancel ()

        let actions =
            ActionBar.View
                {|
                    actions =
                        [|
                            {|
                                label = props.cancelLabel
                                kind = ActionBar.Kind.Secondary
                                onClick = props.onCancel
                                disabled = false
                                icon = None
                            |}
                            {|
                                label = props.confirmLabel
                                kind = ActionBar.Kind.Primary
                                onClick = props.onConfirm
                                disabled = false
                                icon = None
                            |}
                        |]
                |}

        JSX.jsx
            $"""
        import Dialog from '@mui/material/Dialog';
        import DialogTitle from '@mui/material/DialogTitle';
        import DialogContent from '@mui/material/DialogContent';
        import DialogContentText from '@mui/material/DialogContentText';
        import DialogActions from '@mui/material/DialogActions';

        <Dialog open={props.isOpen} onClose={onClose}>
            <DialogTitle>{props.title}</DialogTitle>
            <DialogContent>
                <DialogContentText>{props.text}</DialogContentText>
            </DialogContent>
            <DialogActions>
                {actions}
            </DialogActions>
        </Dialog>
        """
