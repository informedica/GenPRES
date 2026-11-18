namespace Views


/// <summary>
/// A modal over the order plan during a signing challenge. It lists the orders that are new, changed
/// or removed since the version last opened or signed, each with a tag, and asks for the PIN; the user
/// signs the whole plan as shown, or cancels to edit it. Before a challenge, when the patient data
/// changed or cannot be read, it shows the EHR data beside the plan data instead, to take, keep or
/// cancel. SigningPolicy decides what the dialog offers, and the signing machine what happens.
/// </summary>
module SignDialog =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Client.Core.StateMachines
    open Informedica.GenPRES.Client.Core.MachinePolicies

    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz
    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models
    open SigningMachine
    open SigningPolicy


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        let signing = AppEnv.asEnv<AppEnv.ISigning> props.appEnv
        let terms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms
        let context: Global.Context = React.useContext Global.context

        // Translates a term into the language of the User, with the English of the policy as fallback.
        let tr term =
            Global.getLocalizedTerm terms context.Localization (english term) term

        let phase = signing.Signing
        let isOpen = dialogOpen phase

        // The PIN stays here until it is sent. A local check runs first; the server answer comes back
        // through the signing machine as the refusal of the challenge.
        let pin, setPin = React.useState ""
        let localError, setLocalError = React.useState<string option> None
        // The server answer is shown until the User edits the field; the next answer is shown again.
        let edited, setEdited = React.useState false

        // The field belongs to one signature and is cleared whenever the dialog closes.
        React.useEffect (
            (fun () ->
                if not isOpen then
                    setPin ""
                    setLocalError None
                    setEdited false
            ),
            [| box isOpen |]
        )

        // A request is under way while the challenge is requested or the Submission is sent. Only the
        // challenge request can be cancelled, and its answer is then ignored.
        let busy =
            match phase with
            | SigningView.RequestingChallenge
            | SigningView.Submitting _ -> true
            | _ -> false

        let submitting =
            match phase with
            | SigningView.Submitting _ -> true
            | _ -> false

        // When a request ends, its answer is shown until the next edit.
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
        let onTake = fun _ -> signing.TakeEhrData()
        let onKeep = fun _ -> signing.KeepPlanData()

        let refusal =
            match phase with
            | SigningView.AskingPin(_, refusal) -> refusal
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
            | SigningView.DataChanged(plan, notice) -> Some(plan, notice)
            | _ -> None

        // The orders that differ from the version last opened or signed, each with its difference and
        // the argumentation of its context. A removed order is shown as it was in that version.
        let orders =
            match phase with
            | SigningView.DataChanged _
            | SigningView.AskingPin _
            | SigningView.Submitting _ ->
                signing.Differences
                |> Array.choose (fun (ctx, difference) ->
                    OrderContext.contribution ctx
                    |> Option.map (fun sc -> sc, difference, ctx.Argumentation)
                )
            | SigningView.Idle
            | SigningView.RequestingChallenge -> [||]

        // True when the plan is up for signing and no order differs from the version last opened or signed.
        let unchanged =
            match phase with
            | SigningView.DataChanged _
            | SigningView.AskingPin _
            | SigningView.Submitting _ -> orders |> Array.isEmpty
            | SigningView.Idle
            | SigningView.RequestingChallenge -> false

        let differenceTag (difference: PlanDifferencePolicy.Difference) =
            let label, color =
                match difference with
                | PlanDifferencePolicy.Difference.New -> tr Terms.``Signing New``, "success"
                | PlanDifferencePolicy.Difference.Changed -> tr Terms.``Signing Changed``, "warning"
                | PlanDifferencePolicy.Difference.Removed -> tr Terms.``Signing Removed``, "error"

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

        // Shows the argumentation read-only under the order, so the signer reads what the pharmacist reads.
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
            | Some(_, notice) -> noticeSentence tr notice
            | None -> tr Terms.``Signing Dialog Text``

        // The EHR data beside the plan data, only the rows that differ.
        let noticeTable =
            match notice with
            | Some(plan, notice) ->
                match noticeRows tr plan.PatientData notice with
                | [] -> null
                | rows ->
                    let lines =
                        rows
                        |> List.toArray
                        |> Array.map (fun row ->
                            let label = tr row.Label

                            JSX.jsx
                                $"""
                            <TableRow key={label}>
                                <TableCell>{label}</TableCell>
                                <TableCell>{row.Plan}</TableCell>
                                <TableCell>{row.Ehr}</TableCell>
                            </TableRow>
                            """
                        )

                    JSX.jsx
                        $"""
                    <Table size="small">
                        <TableHead>
                            <TableRow>
                                <TableCell></TableCell>
                                <TableCell>{tr Terms.``Order Plan``}</TableCell>
                                <TableCell>{tr Terms.``Signing Notice Ehr``}</TableCell>
                            </TableRow>
                        </TableHead>
                        <TableBody>
                            {lines}
                        </TableBody>
                    </Table>
                    """
            | None -> null

        // The rows are block elements, so the secondary text is a div instead of the paragraph that
        // ListItemText renders.
        let secondarySlot = {| secondary = {| ``component`` = "div" |} |}

        // The orders that differ: the prescription of each scenario, one line per row, under the name
        // and the tag.
        let orderList =
            orders
            |> Array.mapi (fun i (sc, difference, argumentation) ->
                let rows =
                    sc.Prescription
                    |> TextBlockDisplay.flatten
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

        // only a warning: the prescriber may finish the orders of this patient on purpose
        let ehrMovedOnWarning =
            match notice, signing.EhrMovedOn with
            | None, true ->
                let sentence = signing.SignedFor |> ehrMovedOnSentence tr

                JSX.jsx
                    $"""
                import Alert from '@mui/material/Alert';
                <Alert severity="warning">{sentence}</Alert>
                """
            | _ -> null

        // only a warning: signing such an order is accepting it
        let notRecalculatedWarning =
            let sentence =
                match notice, (AppEnv.asEnv<AppEnv.IOrderPlan> props.appEnv).OrderPlan with
                | None, OrderPlanMachine.OrderPlanView.Settled(plan, _)
                | None, OrderPlanMachine.OrderPlanView.Changing(plan, _) -> plan |> notRecalculatedSentence tr
                | _ -> None

            match sentence with
            | Some sentence ->
                JSX.jsx
                    $"""
                import Alert from '@mui/material/Alert';
                <Alert severity="warning">{sentence}</Alert>
                """
            | None -> null

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

        let progress = ViewHelpers.busyProgress busy

        let primary =
            match notice with
            // A notice without EHR patient data has nothing to take.
            | Some(_, { PatientData = None }) ->
                JSX.jsx
                    $"""
                <Button key="keep" variant="contained" color="primary" onClick={onKeep}>
                    {tr Terms.``Signing Proceed``}
                </Button>
                """
            | Some _ ->
                JSX.jsx
                    $"""
                <>
                    <Button onClick={onKeep}>{tr Terms.``Signing Keep Plan Data``}</Button>
                    <Button variant="contained" color="primary" onClick={onTake}>
                        {tr Terms.``Signing Take Ehr Data``}
                    </Button>
                </>
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
        import Dialog from '@mui/material/Dialog';
        import DialogActions from '@mui/material/DialogActions';
        import DialogContent from '@mui/material/DialogContent';
        import DialogContentText from '@mui/material/DialogContentText';
        import DialogTitle from '@mui/material/DialogTitle';
        import List from '@mui/material/List';
        import ListItem from '@mui/material/ListItem';
        import ListItemText from '@mui/material/ListItemText';
        import Table from '@mui/material/Table';
        import TableBody from '@mui/material/TableBody';
        import TableCell from '@mui/material/TableCell';
        import TableHead from '@mui/material/TableHead';
        import TableRow from '@mui/material/TableRow';
        import TextField from '@mui/material/TextField';
        import Typography from '@mui/material/Typography';

        <Dialog open={isOpen} onClose={onCancel} fullWidth={true} maxWidth="sm" aria-labelledby="sign-dialog-title">
            <DialogTitle id="sign-dialog-title">{tr Terms.``Signing Dialog Title``}</DialogTitle>
            <DialogContent>
                <DialogContentText>{body}</DialogContentText>
                {noticeTable}
                {noChanges}
                <List dense={true}>
                    {orderList}
                </List>
                {ehrMovedOnWarning}
                {notRecalculatedWarning}
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
