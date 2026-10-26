namespace Components


module Localization =


    open Fable.Core
    open Feliz
    open Fable.Core.JsInterop


    [<JSX.Component>]
    let View
        (props:
            {|
                languages: Informedica.GenPRES.Shared.Localization.Locales[]
                switchLang: Informedica.GenPRES.Shared.Localization.Locales -> unit
            |})
        =

        let context: Global.Context = React.useContext Global.context
        let anchorElLang, setAnchorElLang = React.useState None

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
                    <Typography>{$"{l |> Informedica.GenPRES.Shared.Localization.toString}"}</Typography>
                </MenuItem>
                """
            )

        let menuOrigin =
            {|
                vertical = "top"
                horizontal = "right"
            |}

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
                {Mui.Icons.Language}
            </IconButton>
            <Menu
                sx={ {| marginTop = "40px" |} }
                anchorEl={anchorElLang}
                anchorOrigin={menuOrigin}
                keepMounted
                transformOrigin={menuOrigin}
                open={anchorElLang.IsSome}
                onClose={handleCloseLangMenu}
            >
                {menuItems}
            </Menu>
            <Typography variant="body1" component="div" >
                {$"{context.Localization |> Informedica.GenPRES.Shared.Localization.toString}"}
            </Typography>
        </Box>
        """
