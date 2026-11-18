namespace Views


/// <summary>
/// A modal over the app while a launch is presented or resumed, after a refusal, and when the server
/// cannot be reached. After a refusal no Session opens; the gate says why and what the User can do:
/// retry (the server cannot be reached, or the browser identity is missing before the identity hop),
/// continue without a launch (no Role), or relaunch from MainEHR (all other cases). While the launch
/// waits on a PIN, the gate shows the enrolment form: the confirmation code from the mail and the
/// chosen PIN twice. The gate cannot be closed, because the Client cannot relaunch by itself.
/// </summary>
module SessionGate =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.StateMachines
    open Informedica.GenPRES.Client.Core.MachinePolicies

    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz
    open Informedica.GenPRES.Shared
    open SessionGatePolicy


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        let session = AppEnv.asEnv<AppEnv.ISession> props.appEnv
        let terms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms
        let context: Global.Context = React.useContext Global.context

        let startup = (AppEnv.asEnv<AppEnv.IStartup> props.appEnv).Startup

        // Translates a term into the language of the User, with the English of the policy as fallback.
        let tr term =
            Global.getLocalizedTerm terms context.Localization (StartupPolicy.english term) term

        let gate =
            StartupPolicy.gate tr startup session.Session
            |> Option.defaultValue
                {
                    Title = ""
                    Body = ""
                    Busy = false
                    Actions = []
                    Form = None
                }

        let onRetry = fun _ -> session.RetryLaunch()
        let onContinue = fun _ -> session.OpenAnonymously()

        // The form fields stay here until they are sent. A local check runs first; the server answer
        // comes back through the session as the Error of the form.
        let code, setCode = React.useState ""
        let pin, setPin = React.useState ""
        let repeat, setRepeat = React.useState ""
        let localError, setLocalError = React.useState<string option> None
        // The server answer is shown until the User edits a field; the next answer is shown again.
        let edited, setEdited = React.useState false

        let enrolling =
            match session.Session with
            | SessionMachine.SessionView.Enrolling _
            | SessionMachine.SessionView.SupplyingPin _ -> true
            | _ -> false

        // The fields belong to one enrolment and are cleared when the launch leaves it, so a later
        // enrolment in this page never shows or sends what was typed before.
        React.useEffect (
            (fun () ->
                if not enrolling then
                    setCode ""
                    setPin ""
                    setRepeat ""
                    setLocalError None
                    setEdited false
            ),
            [| box enrolling |]
        )

        // When a request ends, its answer is shown until the next edit.
        React.useEffect ((fun () -> setEdited false), [| box gate.Busy |])

        let onCode (e: Browser.Types.Event) =
            setCode (e.target?value: string)
            setLocalError None
            setEdited true

        let onPin (e: Browser.Types.Event) =
            setPin (e.target?value: string)
            setLocalError None
            setEdited true

        let onRepeat (e: Browser.Types.Event) =
            setRepeat (e.target?value: string)
            setLocalError None
            setEdited true

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
                let error = localError |> Option.orElse (if edited then None else form.Error)
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

        let progress = ViewHelpers.busyProgress gate.Busy

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
