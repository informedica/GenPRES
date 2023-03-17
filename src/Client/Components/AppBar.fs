namespace Components

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types



open Elmish
open Fable.Core.JsInterop


module AppBar =


    [<JSX.Component>]
    let View (props: {| title: string; toggleSideMenu : unit -> unit |}) =
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
                        onClick={props.toggleSideMenu}
                        >
                        <MenuIcon />

                    </IconButton>
                    <Typography variant="body1" component="div" sx={ {| flexGrow = 1 |} }>
                        {props.title}
                    </Typography>
                    <Button color="inherit">Login</Button>
                </Toolbar>
            </AppBar>
        </Box>
        """
