namespace Views


/// The snackbar at the bottom of the page: the alert in words, coloured by its severity. A success
/// or a note hides itself after three seconds; an error or a warning stays until it is closed.
module AlertSnackbar =


    open Fable.Core
    open Shared


    /// The snackbar of the alert; closed when there is none. The close button and the auto-hide
    /// call onClose; a click outside the snackbar does not.
    [<JSX.Component>]
    let View
        (props:
            {|
                alert: Alert.Alert option
                getTerm: string -> Terms -> string
                onClose: unit -> unit
            |})
        =
        let isOpen = props.alert |> Option.isSome

        let severity = props.alert |> Option.map AlertText.severity |> Option.defaultValue "error"

        let text =
            props.alert
            |> Option.map (AlertText.text props.getTerm)
            |> Option.defaultValue ""

        let autoHide =
            match severity with
            | "success"
            | "info" -> 3000 |> box
            | _ -> null

        // MUI calls this with the event and the reason at once; a curried F# function would take
        // the event only and never run, so it is a Func of two arguments
        let closeUnlessClickAway =
            System.Func<obj, string, unit>(fun _ reason ->
                if reason <> "clickaway" then
                    props.onClose ()
            )

        let close _ = props.onClose ()

        JSX.jsx
            $"""
        import Snackbar from '@mui/material/Snackbar';
        import MuiAlert from '@mui/material/Alert';

        <div>
            <Snackbar
                open={isOpen}
                autoHideDuration={autoHide}
                onClose={closeUnlessClickAway}
            >
                <MuiAlert severity={severity} onClose={close} sx={ {| width = "100%" |} }>
                    {text}
                </MuiAlert>
            </Snackbar>
        </div>
        """
