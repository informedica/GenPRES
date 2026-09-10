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
