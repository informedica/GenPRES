namespace Views


/// <summary>
/// The signing dialog (uc-03 steps 2 and 3, plan 622): modal over the order plan while a
/// challenge stands. It shows the orders exactly as they will be signed (Rule 43) and asks the
/// PIN; sign as shown, or cancel and edit (ext 3b). Before a challenge, when the patient data
/// changed or cannot be read, it is the data notice instead (Rule 44): continue over the data
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

        let busy =
            match phase with
            | Signing.Submitting _ -> true
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

        let onKeyDown (e: Browser.Types.KeyboardEvent) =
            if e.key = "Enter" then
                confirm ()

        let onCancel = fun _ -> signing.Cancel()
        let onAccept = fun _ -> signing.Accept()

        let refusal =
            match phase with
            | Signing.Challenged(_, _, refusal) -> refusal
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
            | Signing.Noticed(_, notice) -> Some notice
            | _ -> None

        let orders =
            match phase with
            | Signing.Noticed(plan, _)
            | Signing.Challenged(_, plan, _)
            | Signing.Submitting(_, plan, _)
            | Signing.Unsent(_, plan, _) -> plan.Scenarios
            | _ -> [||]

        let body =
            match notice with
            | Some notice -> noticeSentence tr notice
            | None -> tr Terms.``Signing Dialog Text``

        // the orders as they will be signed: each scenario's prescription, one line per row
        let orderList =
            orders
            |> Array.mapi (fun i sc ->
                let rows =
                    sc.Prescription
                    |> TextBlock.flatten
                    |> Array.mapi (fun j row ->
                        let cells = row |> Array.map Mui.TypoGraphy.fromTextBlock

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

                JSX.jsx
                    $"""
                <ListItem key={i} divider={true}>
                    <ListItemText primary={sc.Order.Orderable.Name} secondary={rows} />
                </ListItem>
                """
            )

        let pinField =
            match notice with
            | Some _ -> null
            | None ->
                let hasError = error.IsSome
                let helper = error |> Option.defaultValue ""

                JSX.jsx
                    $"""
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
                    onKeyDown={onKeyDown}
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

        <Dialog open={isOpen} onClose={onCancel} fullWidth={true} maxWidth="sm" aria-labelledby="sign-dialog-title">
            <DialogTitle id="sign-dialog-title">{tr Terms.``Signing Dialog Title``}</DialogTitle>
            <DialogContent>
                <DialogContentText>{body}</DialogContentText>
                <List dense={true}>
                    {orderList}
                </List>
                {pinField}
                {progress}
            </DialogContent>
            <DialogActions>
                <Button key="cancel" onClick={onCancel} disabled={busy}>
                    {tr Terms.``Signing Cancel``}
                </Button>
                {primary}
            </DialogActions>
        </Dialog>
        """
