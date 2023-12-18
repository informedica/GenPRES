namespace Components

open System
open Fable.Core
open Fable.React
open Feliz
open Browser.Types



open Elmish
open Fable.Core.JsInterop


module TitleBar =


    [<JSX.Component>]
    let View (props: {|
            title: string
            toggleSideMenu : unit -> unit
            languages : Shared.Localization.Locales []
            hospitals : Deferred<string []>
            switchLang : Shared.Localization.Locales -> unit
            switchHosp : string -> unit
        |}) =

        let context = React.useContext(Global.context)

        let anchorElHosp, setAnchorElHosp = React.useState(None)

        let handleOpenHospMenu = fun ev -> ev?currentTarget |> setAnchorElHosp
        let handleCloseHospMenu = fun _ -> setAnchorElHosp None

        let anchorElLang, setAnchorElLang = React.useState(None)

        let handleOpenLangMenu = fun ev -> ev?currentTarget |> setAnchorElLang
        let handleCloseLangMenu = fun _ -> setAnchorElLang None

        let onClickLangMenuItem l =
            fun () ->
                handleCloseLangMenu ()
                l |> props.switchLang

        let onClickHospMenuItem s =
            fun () ->
                handleCloseHospMenu ()
                printfn $"setting hosptital to: {s}"
                s |> props.switchHosp

        let menuItems =
            props.languages
            |> Array.mapi (fun i l ->
                JSX.jsx
                    $"""
                <MenuItem key={i} value={$"{l}"} onClick={onClickLangMenuItem l} >
                    <Typography>{$"{l |> Shared.Localization.toString}"}</Typography>
                </MenuItem>
                """
            )

        let hospitals =
            props.hospitals
            |> Deferred.defaultValue [||]
            |> Array.mapi (fun i hosp ->
                JSX.jsx
                    $"""
                <MenuItem key={i} value={$"{hosp}"} onClick={onClickHospMenuItem hosp} >
                    <Typography>{$"{hosp}"}</Typography>
                </MenuItem>
                """
            )

        JSX.jsx
            $"""
        import AppBar from '@mui/material/AppBar';
        import Box from '@mui/material/Box';
        import Toolbar from '@mui/material/Toolbar';
        import Typography from '@mui/material/Typography';
        import Button from '@mui/material/Button';
        import IconButton from '@mui/material/IconButton';
        import MenuIcon from '@mui/icons-material/Menu';
        import Menu from '@mui/material/Menu';
        import MenuItem from '@mui/material/MenuItem';

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

                    <Box sx={ {| paddingLeft = 1 |} }>
                        <IconButton color="inherit" onClick={handleOpenHospMenu}>
                            { Mui.Icons.LocalHospital }
                        </IconButton>
                        <Menu
                            sx={ {| mt="45px" |} }
                            anchorEl={anchorElHosp}
                            anchorOrigin={ {| vertical="top"; horizontal="right" |} }
                            keepMounted
                            transformOrigin={ {| vertical="top"; horizontal="right" |} }
                            open={anchorElHosp.IsSome}
                            onClose={handleCloseHospMenu}
                        >
                            {hospitals}
                        </Menu>
                    </Box>
                    <Typography variant="body1" component="div" >
                        {$"{context.Hospital}"}
                    </Typography>

                    <Box >
                        <IconButton color="inherit" onClick={handleOpenLangMenu}>
                            { Mui.Icons.Language }
                        </IconButton>
                        <Menu
                            sx={ {| mt="45px" |} }
                            anchorEl={anchorElLang}
                            anchorOrigin={ {| vertical="top"; horizontal="right" |} }
                            keepMounted
                            transformOrigin={ {| vertical="top"; horizontal="right" |} }
                            open={anchorElLang.IsSome}
                            onClose={handleCloseLangMenu}
                        >
                            {menuItems}
                        </Menu>
                    </Box>
                    <Typography variant="body1" component="div" >
                        {$"{context.Localization |> Shared.Localization.toString}"}
                    </Typography>
                    <Button color="inherit">Login</Button>
                </Toolbar>
            </AppBar>
        </Box>
        """
