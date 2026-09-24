namespace Components


/// What a page tells the user in a box: something went well, something to know, something to
/// mind, something wrong, or that there is nothing here. One look for each, so a page says it
/// the same way as every other page, with a title when the message needs one, an action when
/// there is one to take, and a way to dismiss it when it may be dismissed.
module Notice =


    open Fable.Core
    open Feliz


    /// What kind of thing the notice says.
    [<RequireQualifiedAccess>]
    type Kind =
        /// Something went well.
        | Success
        /// Something to know.
        | Info
        /// Something to mind.
        | Warning
        /// Something wrong.
        | Error
        /// Nothing here: no result, no data, nothing to show. Its own case, since nothing found
        /// is neither a success nor a failure.
        | Empty


    /// The notice: its kind, a title when the message needs one, the message, an action when
    /// there is one to take, and what dismissing it does when it may be dismissed.
    type Props =
        {|
            kind: Kind
            title: string option
            message: string
            action:
                {|
                    label: string
                    onClick: unit -> unit
                |} option
            onClose: (unit -> unit) option
        |}


    let private severityOf kind =
        match kind with
        | Kind.Success -> "success"
        | Kind.Info
        | Kind.Empty -> "info"
        | Kind.Warning -> "warning"
        | Kind.Error -> "error"


    // the empty state is outlined and without an icon: quiet, not a result
    let private variantOf kind =
        match kind with
        | Kind.Empty -> "outlined"
        | _ -> "standard"


    let private noticeSx = {| width = "100%" |}


    [<JSX.Component>]
    let View (props: Props) =
        let severity = severityOf props.kind
        let variant = variantOf props.kind
        let hasIcon = props.kind <> Kind.Empty

        let title =
            match props.title with
            | None -> null
            | Some title ->
                JSX.jsx
                    $"""
                import AlertTitle from '@mui/material/AlertTitle';
                <AlertTitle>{title}</AlertTitle>
                """

        let action =
            match props.action with
            | None -> null
            | Some act ->
                let onClick = fun _ -> act.onClick ()

                JSX.jsx
                    $"""
                import Button from '@mui/material/Button';
                <Button color="inherit" size="small" onClick={onClick}>{act.label}</Button>
                """

        let onClose =
            match props.onClose with
            | None -> null
            | Some close -> box (fun _ -> close ())

        JSX.jsx
            $"""
        import Alert from '@mui/material/Alert';

        <Alert severity={severity} variant={variant} icon={if hasIcon then null else box false} action={action} onClose={onClose} sx={noticeSx}>
            {title}
            {props.message}
        </Alert>
        """
