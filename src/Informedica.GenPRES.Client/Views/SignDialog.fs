namespace Views


/// <summary>
/// The signing dialog: modal over the order plan while a challenge stands. It lists the
/// orders new, changed or removed since the version last opened or signed, each tagged, and
/// asks the PIN; the whole plan is signed as shown, or the user cancels and edits. Before a challenge, when the patient data changed or cannot be read, it is the data
/// notice instead: continue over the data
/// as it stands, or cancel. Every text is a Terms case; what the dialog offers comes from
/// SigningPolicy, and what happens from the signing machine.
/// </summary>
module SignDialog =

    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz
    open Shared
    open Shared.Types
    open Shared.Models
    open SigningMachine
    open SigningPolicy


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        let signing = AppEnv.asEnv<AppEnv.ISigning> props.appEnv
        let terms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms
        let context: Global.Context = React.useContext Global.context

        // the sheet's translation in the User's language, else the policy's English
        let tr term =
            Global.getLocalizedTerm terms context.Localization (english term) term

        let phase = signing.Signing
        let isOpen = dialogOpen phase

        // the PIN lives here until it is sent; a local check runs first, the server's answer
        // comes back through the machine as the challenge's refusal
        let pin, setPin = React.useState ""
        let localError, setLocalError = React.useState<string option> None
        // the server's answer is shown until the User edits the field; the next answer shows again
        let edited, setEdited = React.useState false

        // the field belongs to one signature: cleared whenever the dialog closes
        React.useEffect (
            (fun () ->
                if not isOpen then
                    setPin ""
                    setLocalError None
                    setEdited false
            ),
            [| box isOpen |]
        )

        // a request under way: the challenge asked or the Submission sent; only the challenge
        // asked can be cancelled, its answer then landing nowhere
        let busy =
            match phase with
            | SigningView.Requesting
            | SigningView.Submitting _ -> true
            | _ -> false

        let submitting =
            match phase with
            | SigningView.Submitting _ -> true
            | _ -> false

        // an answer arrived (the request in flight is over): show it, until the next edit
        React.useEffect ((fun () -> setEdited false), [| box busy |])

        let onPin (e: Browser.Types.Event) =
            setPin (e.target?value: string)
            setLocalError None
            setEdited true

        let confirm () =
            match pinError tr pin with
            | Some error -> setLocalError (Some error)
            | None -> signing.Confirm pin

        let onConfirm = fun _ -> confirm ()

        // the PIN field sits in a form, so Enter submits it; the page does not reload
        let onSubmit (e: Browser.Types.Event) =
            e.preventDefault ()
            confirm ()

        let onCancel = fun _ -> signing.Cancel()
        let onAccept = fun _ -> signing.Accept()

        let refusal =
            match phase with
            | SigningView.Challenged(_, refusal) -> refusal
            | _ -> None

        let error =
            localError
            |> Option.orElse (
                if edited then
                    None
                else
                    refusal |> Option.map (refusalSentence tr)
            )

        let notice =
            match phase with
            | SigningView.Noticed(_, notice) -> Some notice
            | _ -> None

        // the orders that differ from the version last opened or signed, as they stood at the
        // sign, each with how it differs and the argumentation its context holds; a removed one
        // as the version held it
        let orders =
            match phase with
            | SigningView.Noticed _
            | SigningView.Challenged _
            | SigningView.Submitting _ ->
                signing.Differences
                |> Array.choose (fun (ctx, difference) ->
                    OrderContext.contribution ctx
                    |> Option.map (fun sc -> sc, difference, ctx.Argumentation)
                )
            | SigningView.Idle
            | SigningView.Requesting -> [||]

        // the plan is up for signing and none of its orders differs from the version
        let unchanged =
            match phase with
            | SigningView.Noticed _
            | SigningView.Challenged _
            | SigningView.Submitting _ -> orders |> Array.isEmpty
            | SigningView.Idle
            | SigningView.Requesting -> false

        let differenceTag (difference: HeldContextPolicy.Difference) =
            let label, color =
                match difference with
                | HeldContextPolicy.Difference.New -> tr Terms.``Signing New``, "success"
                | HeldContextPolicy.Difference.Changed -> tr Terms.``Signing Changed``, "warning"
                | HeldContextPolicy.Difference.Removed -> tr Terms.``Signing Removed``, "error"

            JSX.jsx
                $"""
            <Chip label={label} color={color} size="small" variant="outlined" />
            """

        let primarySx =
            {|
                display = "flex"
                alignItems = "center"
                gap = 1
            |}

        let noChangesSx = {| marginTop = 1 |}

        let argumentationLabel =
            Global.getLocalizedTerm terms context.Localization "Argumentation" Terms.``Order Argumentation``

        let argumentationSx =
            {|
                marginTop = 0.5
                fontStyle = "italic"
            |}

        // the text read-only under the order, so that the signer reads what the pharmacist will
        let argued (text: string option) =
            match text with
            | None -> null
            | Some text ->
                JSX.jsx
                    $"""
                <Box sx={argumentationSx}>{argumentationLabel}: {text}</Box>
                """

        let body =
            match notice with
            | Some notice -> noticeSentence tr signing.Held notice
            | None -> tr Terms.``Signing Dialog Text``

        // the rows are block elements, so the secondary text is a div, not the paragraph
        // ListItemText makes of it
        let secondarySlot = {| secondary = {| ``component`` = "div" |} |}

        // the orders that differ: each scenario's prescription, one line per row, under the
        // name and its tag
        let orderList =
            orders
            |> Array.mapi (fun i (sc, difference, argumentation) ->
                let rows =
                    sc.Prescription
                    |> TextBlock.flatten
                    |> Array.mapi (fun j row ->
                        let cells =
                            row
                            |> Array.mapi (fun k cell ->
                                JSX.jsx
                                    $"""
                                import React from 'react';

                                <React.Fragment key={k}>{cell |> Mui.TypoGraphy.fromTextBlock}</React.Fragment>
                                """
                            )

                        JSX.jsx
                            $"""
                        <Box key={j} sx={ {|
                                              display = "flex"
                                              flexWrap = "wrap"
                                              gap = 1
                                          |} }>
                            {cells}
                        </Box>
                        """
                    )

                let secondary =
                    JSX.jsx
                        $"""
                    <div>{rows}{argued argumentation}</div>
                    """

                let primary =
                    JSX.jsx
                        $"""
                    <Box sx={primarySx}>{sc.Order.Orderable.Name}{differenceTag difference}</Box>
                    """

                JSX.jsx
                    $"""
                <ListItem key={i} divider={true}>
                    <ListItemText primary={primary} secondary={secondary} slotProps={secondarySlot} />
                </ListItem>
                """
            )

        let noChanges =
            if unchanged then
                JSX.jsx
                    $"""
                <Typography variant="body2" sx={noChangesSx}>{tr Terms.``Signing No Changes``}</Typography>
                """
            else
                null

        let pinField =
            match notice with
            | Some _ -> null
            | None ->
                let hasError = error.IsSome
                let helper = error |> Option.defaultValue ""

                JSX.jsx
                    $"""
                <Box component="form" noValidate={true} onSubmit={onSubmit}>
                    <TextField
                        id="sign-dialog-pin"
                        name="pin"
                        autoFocus={true}
                        margin="dense"
                        label={tr Terms.``Signing Pin``}
                        type="password"
                        fullWidth={true}
                        variant="outlined"
                        value={pin}
                        onChange={onPin}
                        disabled={busy}
                        error={hasError}
                        helperText={helper}
                        slotProps={ {|
                                        htmlInput =
                                            {|
                                                inputMode = "numeric"
                                                autoComplete = "current-password"
                                                maxLength = 6
                                            |}
                                    |} }
                    />
                </Box>
                """

        let progress =
            if busy then
                JSX.jsx
                    $"""
                <Box sx={ {| marginTop = 2 |} }>
                    <CircularProgress size={24} />
                </Box>
                """
            else
                null

        let primary =
            match notice with
            | Some _ ->
                JSX.jsx
                    $"""
                <Button key="accept" variant="contained" color="primary" onClick={onAccept}>
                    {tr Terms.``Signing Proceed``}
                </Button>
                """
            | None ->
                JSX.jsx
                    $"""
                <Button key="sign" variant="contained" color="primary" onClick={onConfirm} disabled={busy}>
                    {tr Terms.``Signing Sign``}
                </Button>
                """

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Button from '@mui/material/Button';
        import Chip from '@mui/material/Chip';
        import CircularProgress from '@mui/material/CircularProgress';
        import Dialog from '@mui/material/Dialog';
        import DialogActions from '@mui/material/DialogActions';
        import DialogContent from '@mui/material/DialogContent';
        import DialogContentText from '@mui/material/DialogContentText';
        import DialogTitle from '@mui/material/DialogTitle';
        import List from '@mui/material/List';
        import ListItem from '@mui/material/ListItem';
        import ListItemText from '@mui/material/ListItemText';
        import TextField from '@mui/material/TextField';
        import Typography from '@mui/material/Typography';

        <Dialog open={isOpen} onClose={onCancel} fullWidth={true} maxWidth="sm" aria-labelledby="sign-dialog-title">
            <DialogTitle id="sign-dialog-title">{tr Terms.``Signing Dialog Title``}</DialogTitle>
            <DialogContent>
                <DialogContentText>{body}</DialogContentText>
                {noChanges}
                <List dense={true}>
                    {orderList}
                </List>
                {pinField}
                {progress}
            </DialogContent>
            <DialogActions>
                <Button key="cancel" onClick={onCancel} disabled={submitting}>
                    {tr Terms.``Signing Cancel``}
                </Button>
                {primary}
            </DialogActions>
        </Dialog>
        """
