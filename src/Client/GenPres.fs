module GenPres

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types


module private Api =

    open Fable.Remoting.Client

    let serverApi =
        Remoting.createApi ()
        |> Remoting.withRouteBuilder
            Shared.Api.routerPaths
        |> Remoting.buildProxy<Shared.Api.IServerApi>


// For React Fast Refresh to work, the file must have **one single export**
// This is shy it is important to set the inner modules as private

module private Elmish =

    open Elmish


    type State =
        {
            Test: Deferred<string>
        }

    type Msg =
        | Test of AsyncOperationStatus<string>



    let init () = { Test = HasNotStartedYet }, Cmd.ofMsg (Test Started)


    let update (msg: Msg) (state: State) =
        match msg with
        | Test Started ->
            let load =
                async {
                    let! s = Api.serverApi.test ()
                    return Finished s |> Test
                }
            { state with Test = InProgress }, Cmd.fromAsync load

        | Test (Finished s) ->
            { state with Test = Resolved s}, Cmd.none


module private Components =
    open Elmish


    [<JSX.Component>]
    let AppBar (props: {| title: string |}) =
        JSX.jsx
            $"""
        import AppBar from '@mui/material/AppBar';
        import Box from '@mui/material/Box';
        import Toolbar from '@mui/material/Toolbar';
        import Typography from '@mui/material/Typography';
        import Button from '@mui/material/Button';
        import IconButton from '@mui/material/IconButton';
        import MenuIcon from '@mui/icons-material/Menu';

        <Box sx={ {| flexGrow = 1 |} }>
            <AppBar position="static">
                <Toolbar>
                    <IconButton
                        size="large"
                        edge="start"
                        color="inherit"
                        aria-label="menu"
                        sx={ {| mr = 2 |} }
                        >
                        <MenuIcon />
                    </IconButton>
                    <Typography variant="h6" component="div" sx={ {| flexGrow = 1 |} }>
                        {props.title}
                    </Typography>
                    <Button color="inherit">Login</Button>
                </Toolbar>
            </AppBar>
        </Box>
        """



open Elmish
open Components

[<JSX.Component>]
let App () =
    let model, dispatch = React.useElmish (init, update, [||])

    JSX.jsx
        $"""
    <div className="container mx-4 mt-4">
        {AppBar ({| title = "GenPRES 2023" |})}
        <p>
            {match model.Test with | Resolved s -> s | _ -> "Still waiting"}
        </p>
    </div>
    """