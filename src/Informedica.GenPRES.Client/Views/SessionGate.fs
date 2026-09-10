namespace Views


/// <summary>
/// The session gate (plan 409, uc-01 Refusals): a modal over the app while a launch is being
/// presented or resumed, and after a refusal or an unreachable server. Every refusal ends the
/// same way, no Session opens (Rule 7); the gate says why and what the User can do: retry
/// (ext 3a, and a missing browser identity answered before the identity hop, ext 3c),
/// continue without a launch (no Role, ext 5a), or relaunch from MainEHR (everything else).
/// While the launch waits on a PIN (UC-2) the gate is the enrolment form: the mailed
/// confirmation code and the chosen PIN, twice. The gate cannot be dismissed: the edge from
/// MainEHR is one-way, so the Client cannot relaunch by itself.
/// </summary>
module SessionGate =

    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz
    open Shared
    open SessionGatePolicy


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        let session = AppEnv.asEnv<AppEnv.ISession> props.appEnv
        let terms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms
        let context: Global.Context = React.useContext Global.context

        // the sheet's translation in the User's language, else the policy's English
        let tr term =
            Global.getLocalizedTerm terms context.Localization (english term) term

        let gate =
            gateFor tr session.Session
            |> Option.defaultValue
                {
                    Title = ""
                    Body = ""
                    Busy = false
                    Actions = []
                    Form = None
                }

        let onRetry = fun _ -> session.Retry()
        let onContinue = fun _ -> session.OpenAnonymously()

        // the form's fields live here until they are sent; a local check runs first, the
        // server's answer comes back through the session as the form's Error
        let code, setCode = React.useState ""
        let pin, setPin = React.useState ""
        let repeat, setRepeat = React.useState ""
        let localError, setLocalError = React.useState<string option> None

        let onCode (e: Browser.Types.Event) =
            setCode (e.target?value: string)
            setLocalError None

        let onPin (e: Browser.Types.Event) =
            setPin (e.target?value: string)
            setLocalError None

        let onRepeat (e: Browser.Types.Event) =
            setRepeat (e.target?value: string)
            setLocalError None

        let submit () =
            match formError tr code pin repeat with
            | Some error -> setLocalError (Some error)
            | None -> session.SupplyPin code pin

        let onSubmit = fun _ -> submit ()

        let onKeyDown (e: Browser.Types.KeyboardEvent) =
            if e.key = "Enter" then
                submit ()

        let form =
            match gate.Form with
            | None -> null
            | Some form ->
                let error = localError |> Option.orElse form.Error
                let hasError = error.IsSome
                let helper = error |> Option.defaultValue ""

                JSX.jsx
                    $"""
                <Box component="form" noValidate={true} autoComplete="off" sx={ {| marginTop = 2 |} }>
                    <TextField
                        id="session-enrolment-code"
                        name="code"
                        autoFocus={true}
                        margin="dense"
                        label={form.Code}
                        fullWidth={true}
                        variant="outlined"
                        value={code}
                        onChange={onCode}
                        onKeyDown={onKeyDown}
                        error={hasError}
                        slotProps={ {|
                                        htmlInput =
                                            {|
                                                inputMode = "numeric"
                                                autoComplete = "one-time-code"
                                                maxLength = 6
                                            |}
                                    |} }
                    />
                    <TextField
                        id="session-enrolment-pin"
                        name="pin"
                        margin="dense"
                        label={form.Pin}
                        type="password"
                        fullWidth={true}
                        variant="outlined"
                        value={pin}
                        onChange={onPin}
                        onKeyDown={onKeyDown}
                        error={hasError}
                        slotProps={ {|
                                        htmlInput =
                                            {|
                                                inputMode = "numeric"
                                                autoComplete = "new-password"
                                                maxLength = 6
                                            |}
                                    |} }
                    />
                    <TextField
                        id="session-enrolment-repeat"
                        name="repeat"
                        margin="dense"
                        label={form.Repeat}
                        type="password"
                        fullWidth={true}
                        variant="outlined"
                        value={repeat}
                        onChange={onRepeat}
                        onKeyDown={onKeyDown}
                        error={hasError}
                        helperText={helper}
                        slotProps={ {|
                                        htmlInput =
                                            {|
                                                inputMode = "numeric"
                                                autoComplete = "new-password"
                                                maxLength = 6
                                            |}
                                    |} }
                    />
                </Box>
                """

        let submitButton =
            match gate.Form with
            | None -> null
            | Some form ->
                JSX.jsx
                    $"""
                <Button key="submit" variant="contained" color="primary" onClick={onSubmit}>
                    {form.Submit}
                </Button>
                """

        let progress =
            if gate.Busy then
                JSX.jsx
                    $"""
                <Box sx={ {| marginTop = 2 |} }>
                    <CircularProgress size={24} />
                </Box>
                """
            else
                null

        let actions =
            gate.Actions
            |> List.map (fun action ->
                match action with
                | Action.Retry ->
                    JSX.jsx
                        $"""
                    <Button key="retry" variant="contained" color="primary" onClick={onRetry}>
                        {tr Terms.``Session Try Again``}
                    </Button>
                    """
                | Action.ContinueWithoutLaunch ->
                    JSX.jsx
                        $"""
                    <Button key="continue" variant="contained" color="primary" onClick={onContinue}>
                        {tr Terms.``Session Continue Without Launch``}
                    </Button>
                    """
            )
            |> List.toArray

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Card from '@mui/material/Card';
        import CardActions from '@mui/material/CardActions';
        import CardHeader from '@mui/material/CardHeader';
        import CardContent from '@mui/material/CardContent';
        import Button from '@mui/material/Button';
        import Typography from '@mui/material/Typography';
        import CircularProgress from '@mui/material/CircularProgress';
        import TextField from '@mui/material/TextField';

        <Card sx={ {| p = 4 |} } variant="outlined" role="alertdialog" aria-labelledby="session-gate-title">
            <CardHeader id="session-gate-title" title={gate.Title} />
            <CardContent>
                <Typography variant="body2">{gate.Body}</Typography>
                {form}
                {progress}
            </CardContent>
            <CardActions>
                {actions}
                {submitButton}
            </CardActions>
        </Card>
        """
