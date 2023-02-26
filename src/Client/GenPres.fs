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
            DrawerIsOpen : bool
            Test: Deferred<string>
        }

    type Msg =
        | ToggleDrawer
        | Test of AsyncOperationStatus<string>



    let init () =
        {
            DrawerIsOpen = true
            Test = HasNotStartedYet
        },
        Cmd.ofMsg (Test Started)


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
        | ToggleDrawer ->
            { state with DrawerIsOpen = state.DrawerIsOpen |> not }, Cmd.none


module private Components =
    open Elmish


    [<JSX.Component>]
    let AppBar (props: {| title: string; dispatch : Msg -> unit |}) =
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
                        onClick={fun _ -> ToggleDrawer |> props.dispatch}
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


    [<JSX.Component>]
    let List (prop : {| items : string[] |}) =
        let items =
            prop.items
            |> Array.map (fun text ->
                JSX.jsx
                    $"""
                <ListItem key={text} disablePadding>
                    <ListItemButton>
                    <ListItemText primary={text} />
                    </ListItemButton>
                </ListItem>
                """
            )

        JSX.jsx
            $"""
        import List from '@mui/material/List';
        import Divider from '@mui/material/Divider';
        import ListItem from '@mui/material/ListItem';
        import ListItemButton from '@mui/material/ListItemButton';
        import ListItemIcon from '@mui/material/ListItemIcon';
        import ListItemText from '@mui/material/ListItemText';

        <List>
            {items}
        </List>
        """


    [<JSX.Component>]
    let Drawer (props : {| anchor : string; isOpen : bool; toggle : unit -> unit |}) =
        let drawerWidth = 240

        let menu =
            {|
                items = [|
                    "Noodlijst"
                    "Continue Medicatie"
                    "Voorschrijven"
                    "Voeding"
                    "Formularium"
                |]
            |}
            |> List

        JSX.jsx
            $"""
        import Drawer from '@mui/material/Drawer';
        import Typography from '@mui/material/Typography';

        <div>
            <Drawer
                anchor={props.anchor}
                width={drawerWidth}
                open={props.isOpen}
                onClose={props.toggle}
            >
            {menu}
            </Drawer>
        </div>
        """


open Elmish
open Components

[<Literal>]
let private themeDef = """createTheme({
})""" 

[<Import("createTheme", from="@mui/material/styles")>] 
[<Emit(themeDef)>]
let private theme : obj = jsNative


[<JSX.Component>]
let App () =
    let model, dispatch = React.useElmish (init, update, [||])

    JSX.jsx
        $"""
    import {{ ThemeProvider}} from '@mui/material/styles';
    import CssBaseline from '@mui/material/CssBaseline';
    import React from "react";
    import Box from '@mui/material/Box';
    import Container from '@mui/material/Container';

    <React.StrictMode>
        <ThemeProvider theme={theme}>
            <Box sx={ {| display="flex"; height="100vh" |} }>
                <CssBaseline />
                {AppBar ({| title = "GenPRES 2023"; dispatch = dispatch |})}
                <p>
                    {match model.Test with | Resolved s -> s | _ -> "Still waiting"}
                </p>
                {Drawer ({| anchor = "left"; isOpen = model.DrawerIsOpen; toggle = fun _ -> ToggleDrawer |> dispatch |})}
            </Box>
        </ThemeProvider>
    </React.StrictMode>
    """