namespace Components


module TitleBar =


    open Fable.Core
    open Feliz
    open Fable.Core.JsInterop


    [<JSX.Component>]
    let View
        (props:
            {|
                title: string
                toggleSideMenu: unit -> unit
                languages: Shared.Localization.Locales[]
                hospitals: Deferred<string[]>
                switchLang: Shared.Localization.Locales -> unit
                switchHosp: string -> unit
                isAuthenticated: bool
                onLogin: string -> unit
                onLogout: unit -> unit
            |})
        =

        let context: Global.Context = React.useContext Global.context

        let anchorElHosp, setAnchorElHosp = React.useState None

        let handleOpenHospMenu = fun ev -> ev?currentTarget |> setAnchorElHosp
        let handleCloseHospMenu = fun _ -> setAnchorElHosp None

        let anchorElLang, setAnchorElLang = React.useState None

        let handleOpenLangMenu = fun ev -> ev?currentTarget |> setAnchorElLang
        let handleCloseLangMenu = fun _ -> setAnchorElLang None

        // Login dialog state
        let loginDialogOpen, setLoginDialogOpen = React.useState false
        let password, setPassword = React.useState ""
        let loginError, setLoginError = React.useState false
        let loginAttempted = React.useRef false

        // Close dialog on successful login
        React.useEffect (
            fun () ->
                if loginAttempted.current then
                    if props.isAuthenticated then
                        loginAttempted.current <- false
                        setLoginDialogOpen false
                        setPassword ""
                        setLoginError false
                    else
                        loginAttempted.current <- false
                        setLoginError true
            , [| box props.isAuthenticated |]
        )

        let onClickLangMenuItem l =
            fun () ->
                handleCloseLangMenu ()
                l |> props.switchLang

        let onClickHospMenuItem s =
            fun () ->
                handleCloseHospMenu ()
                s |> props.switchHosp

        let handleLoginClick =
            fun (e: Browser.Types.MouseEvent) ->
                let target = e.currentTarget :?> Browser.Types.HTMLElement
                target.blur ()

                if props.isAuthenticated then
                    props.onLogout ()
                else
                    setPassword ""
                    setLoginError false
                    setLoginDialogOpen true

        let handleLoginClose = fun _ -> setLoginDialogOpen false

        let handleLoginSubmit (e: Browser.Types.Event) =
            e.preventDefault ()
            loginAttempted.current <- true
            setLoginError false
            props.onLogin password

        let handlePasswordChange (e: Browser.Types.Event) =
            setPassword (e.target?value: string)
            setLoginError false

        let menuItems =
            let sxFlag = {| marginRight = 1 |}

            props.languages
            |> Array.mapi (fun i l ->
                let flag = l |> Shared.Localization.toFlag
                let name = l |> Shared.Localization.toString

                JSX.jsx
                    $"""
                <MenuItem key={i} value={$"{l}"} onClick={onClickLangMenuItem l} >
                    <Typography sx={sxFlag}>{flag}</Typography>
                    <Typography>{name}</Typography>
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

        let sxLangBox =
            {|
                display = "flex"
                alignItems = "center"
                marginLeft = 2
            |}

        let loginButtonSx = {| marginLeft = 2 |}

        let sxLangLabel =
            {|
                cursor = "pointer"
                color = "inherit"
                userSelect = "none"
            |}

        let loginButtonText = if props.isAuthenticated then "Logout" else "Login"

        let loginButtonIcon =
            if props.isAuthenticated then
                Mui.Icons.Logout
            else
                Mui.Icons.Login

        let flexGrowSx = {| flexGrow = 1 |}
        let menuIconSx = {| marginRight = 2 |}
        let menuSx = {| marginTop = "40px" |}

        let topRightOrigin =
            {|
                vertical = "top"
                horizontal = "right"
            |}

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
        import Dialog from '@mui/material/Dialog';
        import DialogTitle from '@mui/material/DialogTitle';
        import DialogContent from '@mui/material/DialogContent';
        import DialogActions from '@mui/material/DialogActions';
        import TextField from '@mui/material/TextField';

        <Box sx={flexGrowSx}>
            <AppBar position="static">
                <Toolbar>
                    <IconButton
                        size="large"
                        edge="start"
                        color="inherit"
                        aria-label="menu"
                        sx={menuIconSx}
                        onClick={props.toggleSideMenu}
                        >
                        <MenuIcon />

                    </IconButton>
                    <Typography variant="body1" component="div" sx={flexGrowSx}>
                        {props.title}
                    </Typography>

                    <Box sx={ {| paddingLeft = 1 |} }>
                        <IconButton color="inherit" onClick={handleOpenHospMenu}>
                            {Mui.Icons.LocalHospital}
                        </IconButton>
                        <Menu
                            sx={menuSx}
                            anchorEl={anchorElHosp}
                            anchorOrigin={topRightOrigin}
                            keepMounted
                            transformOrigin={topRightOrigin}
                            open={anchorElHosp.IsSome}
                            onClose={handleCloseHospMenu}
                        >
                            {hospitals}
                        </Menu>
                    </Box>
                    <Typography variant="body1" component="div" >
                        {$"{context.Hospital}"}
                    </Typography>

                    <Box sx={sxLangBox}>
                        <IconButton color="inherit" onClick={handleOpenLangMenu}>
                            {Mui.Icons.Language}
                        </IconButton>
                        <Typography variant="body1" component="div" sx={sxLangLabel} onClick={handleOpenLangMenu}>
                            {context.Localization |> Shared.Localization.toShortCode}
                        </Typography>
                        <Menu
                            sx={menuSx}
                            anchorEl={anchorElLang}
                            anchorOrigin={topRightOrigin}
                            keepMounted
                            transformOrigin={topRightOrigin}
                            open={anchorElLang.IsSome}
                            onClose={handleCloseLangMenu}
                        >
                            {menuItems}
                        </Menu>
                    </Box>
                    <Button color="inherit" onClick={handleLoginClick} startIcon={loginButtonIcon} sx={loginButtonSx}>{loginButtonText}</Button>
                </Toolbar>
            </AppBar>
            <Dialog open={loginDialogOpen} onClose={handleLoginClose}>
                <form onSubmit={handleLoginSubmit}>
                    <input
                        type="text"
                        name="username"
                        autoComplete="username"
                        value="genpres"
                        readOnly={true}
                        style={ {| display = "none" |} }
                    />
                    <DialogTitle>{"Login"}</DialogTitle>
                    <DialogContent>
                        <TextField
                            autoFocus={true}
                            margin="dense"
                            name="password"
                            autoComplete="current-password"
                            label="Password"
                            type="password"
                            fullWidth={true}
                            variant="outlined"
                            value={password}
                            error={loginError}
                            helperText={if loginError then "Invalid password" else ""}
                            onChange={handlePasswordChange}
                        />
                    </DialogContent>
                    <DialogActions>
                        <Button onClick={handleLoginClose} type="button">{"Cancel"}</Button>
                        <Button type="submit" variant="contained">{"Login"}</Button>
                    </DialogActions>
                </form>
            </Dialog>
        </Box>
        """
