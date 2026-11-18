namespace Components


/// A message box with one style per kind, so every page shows a success, an information, a warning,
/// an error or an empty result the same way. It has an optional title, action and close button.
module Notice =


    open Fable.Core
    open Feliz


    /// The kind of message, which sets the style of the notice.
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
        /// Nothing to show, such as no result or no data. This is neither a success nor a failure.
        | Empty


    /// The kind, an optional title, the message, an optional action button that can be disabled,
    /// and an optional close handler. Without a close handler the notice has no close button.
    type Props =
        {|
            kind: Kind
            title: string option
            message: string
            action:
                {|
                    label: string
                    onClick: unit -> unit
                    disabled: bool
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


    // the empty state is outlined, and the view shows it without an icon, so it reads as quiet
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
                <Button color="inherit" size="small" onClick={onClick} disabled={act.disabled}>{act.label}</Button>
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
