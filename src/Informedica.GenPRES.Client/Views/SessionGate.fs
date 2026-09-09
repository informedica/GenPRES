namespace Views


/// <summary>
/// The session gate (plan 409, uc-01 Refusals): a modal over the app while a launch is being
/// presented or resumed, and after a refusal or an unreachable server. Every refusal ends the
/// same way, no Session opens (Rule 7); the gate says why and what the User can do: retry
/// (ext 3a, and a missing browser identity answered before the identity hop, ext 3c),
/// continue without a launch (no Role, ext 5a), or relaunch from MainEHR (everything else).
/// The gate cannot be dismissed: the edge from MainEHR is one-way, so the Client cannot
/// relaunch by itself.
/// </summary>
module SessionGate =

    open Fable.Core
    open Feliz
    open Shared.Types
    open SessionMachine


    /// What the gate offers besides the text.
    [<RequireQualifiedAccess>]
    type Action =
        | Retry
        | ContinueWithoutLaunch


    /// One gate: a title, a body, whether work is in progress, and the actions.
    type Gate =
        {
            Title: string
            Body: string
            Busy: bool
            Actions: Action list
        }


    // English for now. These become Shared.Localization.Terms cases plus sheet rows once the
    // terms are agreed; the keys below are the intended term names.
    let relaunch = "Open GenPRES again from MainEHR."

    let relaunchAfter = "open GenPRES again from MainEHR."

    let refusalBody refusal =
        match refusal with
        | LaunchRefusal.LaunchExpired -> $"The launch has expired. {relaunch}"
        | LaunchRefusal.LaunchSpent -> $"This launch has already been used. {relaunch}"
        | LaunchRefusal.LaunchInvalid -> $"The launch is not valid. {relaunch}"
        | LaunchRefusal.NoBrowserIdentity -> "Your browser could not be identified."
        | LaunchRefusal.NoRole ->
            "You have no role in GenPRES. You can continue without a launch: no patient is carried over."
        | LaunchRefusal.WrongActivePatient ->
            $"The patient active in MainEHR is not the patient of this launch. Activate the right patient and {relaunchAfter}"
        | LaunchRefusal.EnrolmentRequired ->
            $"A PIN has to be set before prescribing. Enrolment is not available yet. {relaunch}"


    /// The gate for a session phase, or None when the app is usable (anonymous, open, closing).
    let gateFor (session: Session) : Gate option =
        match session with
        | Session.Launching(_, _, attempt) ->
            Some
                {
                    Title = "Opening your session"
                    Body = $"Presenting the launch, attempt %i{attempt} of %i{Session.maxAttempts}."
                    Busy = true
                    Actions = []
                }
        | Session.Resuming ->
            Some
                {
                    Title = "Resuming your session"
                    Body = "Checking for an open session."
                    Busy = true
                    Actions = []
                }
        | Session.Unreachable(_, _, attempts) ->
            Some
                {
                    Title = "GenPRES could not be reached"
                    Body = $"The server did not answer after %i{attempts} attempts. Try again, or {relaunchAfter}"
                    Busy = false
                    Actions = [ Action.Retry ]
                }
        | Session.Refused(refusal, retry) ->
            Some
                {
                    Title = "GenPRES could not open your session"
                    Body =
                        match refusal, retry with
                        | LaunchRefusal.NoBrowserIdentity, Some _ -> $"{refusalBody refusal} Try again."
                        | LaunchRefusal.NoBrowserIdentity, None -> $"{refusalBody refusal} {relaunch}"
                        | _ -> refusalBody refusal
                    Busy = false
                    Actions =
                        [
                            match refusal, retry with
                            | LaunchRefusal.NoRole, _ -> Action.ContinueWithoutLaunch
                            | _, Some _ -> Action.Retry
                            | _ -> ()
                        ]
                }
        | Session.Anonymous
        | Session.Open _
        | Session.Closing _ -> None


    [<JSX.Component>]
    let View (props: {| appEnv: obj |}) =
        let session = AppEnv.asEnv<AppEnv.ISession> props.appEnv

        let gate =
            gateFor session.Session
            |> Option.defaultValue
                {
                    Title = ""
                    Body = ""
                    Busy = false
                    Actions = []
                }

        let onRetry = fun _ -> session.Retry()
        let onContinue = fun _ -> session.OpenAnonymously()

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
                        {"Try again"}
                    </Button>
                    """
                | Action.ContinueWithoutLaunch ->
                    JSX.jsx
                        $"""
                    <Button key="continue" variant="contained" color="primary" onClick={onContinue}>
                        {"Continue without launch"}
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

        <Card sx={ {| p = 4 |} } variant="outlined" role="alertdialog" aria-labelledby="session-gate-title">
            <CardHeader id="session-gate-title" title={gate.Title} />
            <CardContent>
                <Typography variant="body2">{gate.Body}</Typography>
                {progress}
            </CardContent>
            <CardActions>
                {actions}
            </CardActions>
        </Card>
        """
