namespace Components

open System
open Fable.Core
open Fable.React
open Feliz
open Feliz.Router
open Browser.Types


open Elmish
open Fable.Core.JsInterop


module Localization =


//    [<ReactComponent>]
    let View (props : {|
            languages : Shared.Localization.Locales []
            switchLang : Shared.Localization.Locales -> unit
    |}) =

        let lang = React.useContext(Global.languageContext)
        let anchorElLang, setAnchorElLang = React.useState(None)

        let handleOpenLangMenu = fun ev -> ev?currentTarget |> setAnchorElLang
        let handleCloseLangMenu = fun _ -> setAnchorElLang None

        let onClickMenuItem l =
            fun () ->
                handleCloseLangMenu ()
                l |> props.switchLang

        let menuItems =
            props.languages
            |> Array.mapi (fun i l ->
                JSX.jsx
                    $"""
                <MenuItem key={i} value={$"{l}"} onClick={onClickMenuItem l} >
                    <Typography>{$"{l |> Shared.Localization.toString}"}</Typography>
                </MenuItem>
                """
            )

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Typography from '@mui/material/Typography';
        import Button from '@mui/material/Button';
        import IconButton from '@mui/material/IconButton';
        import MenuIcon from '@mui/icons-material/Menu';
        import Menu from '@mui/material/Menu';
        import MenuItem from '@mui/material/MenuItem';

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
            <Typography variant="body1" component="div" >
                {$"{lang |> Shared.Localization.toString}"}
            </Typography>
        </Box>
        """
