namespace Components


/// A question before an act that cannot be undone: what is about to happen, and the two ways
/// out. The confirming action completes the task, so it is the prominent button on the right;
/// cancelling steps back, outlined on the left. Nothing happens until the user answers, and
/// closing the dialog any other way is a cancel.
module ConfirmDialog =


    open Fable.Core
    open Feliz


    /// The question and its two answers.
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
