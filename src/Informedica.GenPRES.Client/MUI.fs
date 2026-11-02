module Mui

open Informedica.GenPRES.Client.Core.Models
open Informedica.GenPRES.Client.Core.Helpers
open Fable.Core

module Icons =

    [<JSX.Component>]
    let Assignment =
        JSX.jsx
            $"""
        import Assignment from '@mui/icons-material/Assignment';
        <Assignment />
    """

    [<JSX.Component>]
    let Message =
        JSX.jsx
            $"""
        import Message from '@mui/icons-material/Message';
        <Message />
    """

    //closeIcon
    [<JSX.Component>]
    let Close =
        JSX.jsx
            $"""
        import Close from '@mui/icons-material/Close';
        <Close />
    """

    //addIcon
    [<JSX.Component>]
    let Add =
        JSX.jsx
            $"""
        import Add from '@mui/icons-material/Add';
        <Add />
    """
    //deleteIcon
    [<JSX.Component>]
    let Delete =
        JSX.jsx
            $"""
        import Delete from '@mui/icons-material/Delete';
        <Delete />
    """

    // personicon
    [<JSX.Component>]
    let Person =
        JSX.jsx
            $"""
        import Person from '@mui/icons-material/Person';
        <Person />
    """

    //Settings
    [<JSX.Component>]
    let Settings =
        JSX.jsx
            $"""
        import Settings from '@mui/icons-material/Settings';
        <Settings />
    """

    //Restore
    [<JSX.Component>]
    let Restore =
        JSX.jsx
            $"""
        import Restore from '@mui/icons-material/Restore';
        <Restore />
    """

    // editIcon
    [<JSX.Component>]
    let Edit =
        JSX.jsx
            $"""
        import Edit from '@mui/icons-material/Edit';
        <Edit />
    """

    //info icon
    [<JSX.Component>]
    let MedicationLiquid =
        JSX.jsx
            $"""
        import MedicationLiquid from '@mui/icons-material/MedicationLiquid';
        <MedicationLiquid />
    """

    //info icon
    [<JSX.Component>]
    let Vaccines =
        JSX.jsx
            $"""
        import Vaccines from '@mui/icons-material/Vaccines';
        <Vaccines />
    """

    //info icon
    [<JSX.Component>]
    let Notes =
        JSX.jsx
            $"""
        import Notes from '@mui/icons-material/Notes';
        <Notes />
    """

    //info icon
    [<JSX.Component>]
    let Downloading =
        JSX.jsx
            $"""
        import Downloading from '@mui/icons-material/Downloading';
        <Downloading />
    """

    //info icon
    [<JSX.Component>]
    let Clear =
        JSX.jsx
            $"""
        import Clear from '@mui/icons-material/Clear';
        <Clear />
    """

    [<JSX.Component>]
    let FireExtinguisher =
        JSX.jsx
            $"""
        import FireExtinguisher from '@mui/icons-material/FireExtinguisher';
        <FireExtinguisher />
    """

    [<JSX.Component>]
    let LocalPharmacy =
        JSX.jsx
            $"""
        import LocalPharmacy from '@mui/icons-material/LocalPharmacy';
        <LocalPharmacy />
    """

    [<JSX.Component>]
    let Bloodtype =
        JSX.jsx
            $"""
        import Bloodtype from '@mui/icons-material/Bloodtype';
        <Bloodtype />
    """


    [<JSX.Component>]
    let Language =
        JSX.jsx
            $"""
        import Language from '@mui/icons-material/Language';
        <Language />
    """

    [<JSX.Component>]
    let LocalHospital =
        JSX.jsx
            $"""
        import LocalHospital from '@mui/icons-material/LocalHospital';
        <LocalHospital />
    """

    [<JSX.Component>]
    let Login =
        JSX.jsx
            $"""
        import LoginIcon from '@mui/icons-material/Login';
        <LoginIcon />
    """

    [<JSX.Component>]
    let Logout =
        JSX.jsx
            $"""
        import LogoutIcon from '@mui/icons-material/Logout';
        <LogoutIcon />
    """

    let CalculateIcon =
        JSX.jsx
            """
        import CalculateIcon from '@mui/icons-material/Calculate';
        <CalculateIcon/>
    """

    let SummarizeIcon =
        JSX.jsx
            $"""
    import SummarizeIcon from '@mui/icons-material/Summarize';
    <SummarizeIcon/>
    """

    let LocalDiningIcon =
        JSX.jsx
            $"""
        import LocalDiningIcon from '@mui/icons-material/LocalDining';
        <LocalDiningIcon/>
    """

    let LockIcon =
        JSX.jsx
            $"""
     import LockIcon from '@mui/icons-material/Lock';
     <LockIcon fontSize="small"/>
    """

    let RefreshIcon =
        JSX.jsx
            $"""
     import RefreshIcon from '@mui/icons-material/Refresh';
     <RefreshIcon/>
    """

    let FirstPageIcon =
        JSX.jsx
            $"""
     import FirstPageIcon from '@mui/icons-material/FirstPage';
     <FirstPageIcon/>
    """

    let LastPageIcon =
        JSX.jsx
            $"""
     import LastPageIcon from '@mui/icons-material/LastPage';
     <LastPageIcon/>
    """

    let SkipNextIcon =
        JSX.jsx
            $"""
     import SkipNextIcon from '@mui/icons-material/SkipNext';
     <SkipNextIcon/>
    """

    let SkipPreviousIcon =
        JSX.jsx
            $"""
     import SkipPreviousIcon from '@mui/icons-material/SkipPrevious';
     <SkipPreviousIcon/>
    """

    let KeyboardDoubleArrowLeftIcon =
        JSX.jsx
            $"""
    import KeyboardDoubleArrowLeftIcon from '@mui/icons-material/KeyboardDoubleArrowLeft';
    <KeyboardDoubleArrowLeftIcon/>
    """

    let RemoveIcon =
        JSX.jsx
            $"""
    import RemoveIcon from '@mui/icons-material/Remove';
    <RemoveIcon/>
    """

    let KeyboardDoubleArrowRightIcon =
        JSX.jsx
            $"""
    import KeyboardDoubleArrowRightIcon from '@mui/icons-material/KeyboardDoubleArrowRight';
    <KeyboardDoubleArrowRightIcon/>
    """

    let ExpandMoreIcon =
        JSX.jsx
            $"""
    import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
    <ExpandMoreIcon/>
    """

    let ExpandLessIcon =
        JSX.jsx
            $"""
    import ExpandLessIcon from '@mui/icons-material/ExpandLess';
    <ExpandLessIcon/>
    """

    [<JSX.Component>]
    let WarningAmber =
        JSX.jsx
            $"""
        import WarningAmber from '@mui/icons-material/WarningAmber';
        <WarningAmber />
    """


type Color =
    {|
        ``50``: string
        ``100``: string
        ``200``: string
        ``300``: string
        ``400``: string
        ``500``: string
        ``600``: string
        ``700``: string
        ``800``: string
        ``900``: string
        A100: string
        A200: string
        A400: string
        A700: string
    |}


type PaletteColor =
    {|
        main: string
        light: string
        dark: string
        contrastText: string
    |}


[<Erase>]
type Theme =
    abstract member palette:
        {|
            common:
                {|
                    black: string
                    white: string
                |}
            mode: string
            contrastThreshold: float
            tonalOffset: float
            primary: PaletteColor
            secondary: PaletteColor
            error: PaletteColor
            warning: PaletteColor
            info: PaletteColor
            success: PaletteColor
            grey: Color
            text:
                {|
                    primary: string
                    secondary: string
                    disabled: string
                |}
            divider: string

            background:
                {|
                    paper: string
                    ``default``: string
                |}

            getContrastText: string -> string
        |}

    abstract member spacing: int -> string

    abstract member zIndex:
        {|
            mobileStepper: int
            speedDial: int
            appBar: int
            drawer: int
            modal: int
            snackbar: int
            tooltip: int
            fab: int
        |}

    abstract member typography:
        {|
            fontFamily: string
            fontSize: int
            fontWeightLight: int
            fontWeightRegular: int
            fontWeightMedium: int
            fontWeightBold: int
            h1:
                {|
                    fontFamily: string
                    fontWeight: int
                    fontSize: string
                    lineHeight: string
                    letterSpacing: string
                |}
            h2:
                {|
                    fontFamily: string
                    fontWeight: int
                    fontSize: string
                    lineHeight: string
                    letterSpacing: string
                |}
            h3:
                {|
                    fontFamily: string
                    fontWeight: int
                    fontSize: string
                    lineHeight: string
                    letterSpacing: string
                |}
            h4:
                {|
                    fontFamily: string
                    fontWeight: int
                    fontSize: string
                    lineHeight: string
                    letterSpacing: string
                |}
            h5:
                {|
                    fontFamily: string
                    fontWeight: int
                    fontSize: string
                    lineHeight: string
                    letterSpacing: string
                |}
            h6:
                {|
                    fontFamily: string
                    fontWeight: int
                    fontSize: string
                    lineHeight: string
                    letterSpacing: string
                |}
            subtitle1:
                {|
                    fontFamily: string
                    fontWeight: int
                    fontSize: string
                    lineHeight: string
                    letterSpacing: string
                |}
            subtitle2:
                {|
                    fontFamily: string
                    fontWeight: int
                    fontSize: string
                    lineHeight: string
                    letterSpacing: string
                |}
            body1:
                {|
                    fontFamily: string
                    fontWeight: int
                    fontSize: string
                    lineHeight: string
                    letterSpacing: string
                |}
            body2:
                {|
                    fontFamily: string
                    fontWeight: int
                    fontSize: string
                    lineHeight: string
                    letterSpacing: string
                |}
            button:
                {|
                    fontFamily: string
                    fontWeight: int
                    fontSize: string
                    lineHeight: string
                    letterSpacing: string
                |}
            caption:
                {|
                    fontFamily: string
                    fontWeight: int
                    fontSize: string
                    lineHeight: string
                    letterSpacing: string
                |}
            overline:
                {|
                    fontFamily: string
                    fontWeight: int
                    fontSize: string
                    lineHeight: string
                    letterSpacing: string
                |}
        |}

    abstract member transitions:
        {|
            easing:
                {|
                    easeInOut: string
                    easeOut: string
                    easeIn: string
                    sharp: string
                |}

            duration:
                {|
                    shortest: string
                    shorter: string
                    short: string
                    standard: string
                    complex: string
                    enteringScreen: string
                    leavingScreen: string
                |}

            create: string array -> string -> string -> string -> string
        |}


[<Erase>]
module Colors =
    type Blue =
        static member ``50`` = "#e3f2fd"
        static member ``100`` = "#bbdefb"
        static member ``200`` = "#90caf9"
        static member ``300`` = "#64b5f6"
        static member ``400`` = "#42a5f5"
        static member ``500`` = "#2196f3"
        static member ``600`` = "#1e88e5"
        static member ``700`` = "#1976d2"
        static member ``800`` = "#1565c0"
        static member ``900`` = "#0d47a1"
        static member A100 = "#82b1ff"
        static member A200 = "#448aff"
        static member A400 = "#2979ff"
        static member A700 = "#2962ff"

    type BlueGrey =
        static member ``50`` = "#eceff1"
        static member ``100`` = "#cfd8dc"
        static member ``200`` = "#b0bec5"
        static member ``300`` = "#90a4ae"
        static member ``400`` = "#78909c"
        static member ``500`` = "#607d8b"
        static member ``600`` = "#546e7a"
        static member ``700`` = "#455a64"
        static member ``800`` = "#37474f"
        static member ``900`` = "#263238"

    type Grey =
        static member ``50`` = "#fafafa"
        static member ``100`` = "#f5f5f5"
        static member ``200`` = "#eeeeee"
        static member ``300`` = "#e0e0e0"
        static member ``400`` = "#bdbdbd"
        static member ``500`` = "#9e9e9e"
        static member ``600`` = "#757575"
        static member ``700`` = "#616161"
        static member ``800`` = "#424242"
        static member ``900`` = "#212121"

    type Indigo =
        static member ``50`` = "#e8eaf6"
        static member ``100`` = "#c5cae9"
        static member ``200`` = "#9fa8da"
        static member ``300`` = "#7986cb"
        static member ``400`` = "#5c6bc0"
        static member ``500`` = "#3f51b5"
        static member ``600`` = "#3949ab"
        static member ``700`` = "#303f9f"
        static member ``800`` = "#283593"
        static member ``900`` = "#1a237e"
        static member A100 = "#8c9eff"
        static member A200 = "#536dfe"
        static member A400 = "#3d5afe"

/// The tokens a shared component spells its colours in. Each is a path into the theme's
/// palette, resolved by sx, so a colour is stated once, in the theme, and a component names
/// what it means rather than what it is. The hexes still in Mui.Colors are the ones the
/// palette does not cover yet.
module Styles =

    open Fable.Core.JsInterop
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models

    /// The four severities the client shows, as the palette names them.
    let validColor = "success.main"
    let cautionColor = "info.main"
    let warningColor = "warning.main"
    let alertColor = "error.main"

    /// The tint behind a caution, a warning or an alert, as an alert box has it.
    let cautionBg = "severityBg.caution"
    let warningBg = "severityBg.warning"
    let alertBg = "severityBg.alert"

    /// Text that explains a value.
    let mutedTextColor = "text.secondary"

    /// The accent of a selected row and a marked border.
    let accentColor = "primary.main"

    /// The header band over a table or a section; a tint the palette has no name for yet.
    let headerBgColor = Colors.Blue.``50``

    /// The yellow tint a changed value lights up with before it fades.
    let changedBgColor = "#fff9c4"


    let selectIconVisibilitySx isClear =
        {| ``& .MuiSelect-icon`` = {| visibility = if isClear then "visible" else "hidden" |} |}


    /// A step button beside an input: as tall as the input's line, so that a row of them on the
    /// input's baseline centres on its text.
    let stepButtonSx = {| padding = "2px" |}


    /// The colour of a severity; none for normal, which gets no mark.
    let severityColor (severity: Severity) =
        match severity with
        | Severity.Normal -> None
        | Severity.Caution -> Some cautionColor
        | Severity.Warning -> Some warningColor
        | Severity.Alert -> Some alertColor


    /// The tint behind a severity; none for normal.
    let severityBg (severity: Severity) =
        match severity with
        | Severity.Normal -> None
        | Severity.Caution -> Some cautionBg
        | Severity.Warning -> Some warningBg
        | Severity.Alert -> Some alertBg


    /// One sx over two: the second's properties over the first's.
    let mergeSx (sx: obj) (over: obj) : obj = emitJsExpr (sx, over) "Object.assign({}, $0, $1)"


    /// The colour of a severity resolved on the theme, for a CSS property sx does not resolve a
    /// palette path in, such as a text decoration's colour; none for normal.
    let severityPaletteColor (theme: Theme) (severity: Severity) =
        match severity with
        | Severity.Normal -> None
        | Severity.Caution -> Some theme.palette.info.main
        | Severity.Warning -> Some theme.palette.warning.main
        | Severity.Alert -> Some theme.palette.error.main


    /// The mark a raised severity puts under a value, a double underline in its colour, added to
    /// an sx; a normal severity adds nothing. The colour is a theme callback, since sx resolves
    /// a palette path in color and backgroundColor but not in textDecorationColor.
    let markSx (severity: Severity) (sx: obj) =
        if severity |> Severity.isRaised then
            let underlineColor (theme: Theme) =
                severity |> severityPaletteColor theme |> Option.defaultValue "currentColor"

            {|
                textDecoration = "underline double"
                textDecorationColor = underlineColor
                textUnderlineOffset = "3px"
            |}
            |> box
            |> mergeSx sx
        else
            sx


/// The app's two MUI themes: the same palette and spacing, with a smaller font and smaller
/// components on a narrow screen.
module Themes =

    open Fable.Core.JsInterop


    let createTheme (options: obj) : obj = import "createTheme" "@mui/material/styles"


    let responsiveFontSizes (theme: obj, options: obj) : obj = import "responsiveFontSizes" "@mui/material/styles"


    /// A theme with this base font size and this default size for tables, fields and buttons.
    let themeOf fontSize size =
        let sized = {| defaultProps = {| size = size |} |}

        let theme =
            createTheme
                {|
                    typography = {| fontSize = fontSize |}
                    palette =
                        {|
                            mode = "light"
                            // the four severities the client shows: Valid, Caution, Warning, Alert. MUI's own
                            // light-mode shades, all three, so nothing derived replaces a built-in one
                            success =
                                {|
                                    main = "#2e7d32"
                                    light = "#4caf50"
                                    dark = "#1b5e20"
                                |}
                            info =
                                {|
                                    main = "#0288d1"
                                    light = "#03a9f4"
                                    dark = "#01579b"
                                |}
                            warning =
                                {|
                                    main = "#ed6c02"
                                    light = "#ff9800"
                                    dark = "#e65100"
                                |}
                            error =
                                {|
                                    main = "#d32f2f"
                                    light = "#ef5350"
                                    dark = "#c62828"
                                |}
                            // the tint behind a caution, a warning or an alert, as an alert box has it
                            severityBg =
                                {|
                                    caution = "#e5f6fd"
                                    warning = "#fff4e5"
                                    alert = "#fdeded"
                                |}
                            // the accent the tables and headers use
                            primary =
                                {|
                                    main = "#1976d2"
                                    light = "#42a5f5"
                                    dark = "#1565c0"
                                |}
                        |}
                    spacing = 6
                    components =
                        {|
                            MuiTable = sized
                            MuiTextField = sized
                            MuiButton = sized
                            MuiIconButton = sized
                            MuiToolbar = {| defaultProps = {| variant = "dense" |} |}
                            MuiAutocomplete = sized
                        |}
                |}

        responsiveFontSizes (theme, {| factor = 2 |})


    /// The theme of a wide screen.
    let desktop = themeOf 12 "medium"


    /// The theme of a narrow screen, up to 1200 pixels.
    let mobile = themeOf 11 "small"


module TypoGraphy =

    open Informedica.GenPRES.Shared.Types

    open Feliz

    let fromTextBlock (textBlock: TextBlock) =
        let print tb =
            let severity = tb |> Severity.ofTextBlock
            let items = tb |> Severity.items

            // bold text takes the severity's colour, valid text the palette's success colour
            let color = severity |> Styles.severityColor |> Option.defaultValue Styles.validColor

            let normalSx =
                {|
                    display = "inline"
                    color = Colors.Grey.``700``
                |}
                |> box
                |> Styles.markSx severity

            let boldSx =
                {|
                    display = "inline"
                    color = color
                |}
                |> box
                |> Styles.markSx severity

            let italicSx =
                {|
                    display = "inline"
                    color = Colors.Grey.``600``
                |}
                |> box
                |> Styles.markSx severity

            items
            |> Array.map (fun item ->
                match item with
                | Normal s ->
                    JSX.jsx
                        $"""
                    <Typography sx={normalSx}>{s}</Typography>
                    """
                | Bold s ->
                    JSX.jsx
                        $"""
                    <Typography
                    sx={boldSx}
                    >
                    <strong> {s} </strong>
                    </Typography>
                    """
                | Italic s ->
                    JSX.jsx
                        $"""
                    <Typography
                    sx={italicSx}
                    >
                    <em>{s}</em>
                    </Typography>
                    """
            )

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Typography from '@mui/material/Typography';

        <Box sx={ {| display = "inline" |} }>
            {textBlock |> print |> unbox<seq<ReactElement>> |> React.Fragment}
        </Box>
        """


open System.ComponentModel
open Fable.Core.JsInterop


[<EditorBrowsable(EditorBrowsableState.Never)>]
module HookImports =

    let useMediaQuery (query: string) : bool = importDefault "@mui/material/useMediaQuery"

    let useMediaQuery_theme (getQuery: Theme -> string) : bool = importDefault "@mui/material/useMediaQuery"


[<Erase>]
type Hooks =

    static member inline useMediaQuery(query: string) : bool = HookImports.useMediaQuery query

    static member inline useMediaQuery(getQuery: Theme -> string) : bool = HookImports.useMediaQuery_theme getQuery

    /// Whether the window is at most 1200 pixels wide, where the pages switch to their mobile layout.
    static member inline useIsMobile() = Hooks.useMediaQuery "(max-width:1200px)"

    /// Whether the window is at most 900 pixels wide, where a few views stack what they show side by
    /// side.
    static member inline useIsNarrow() = Hooks.useMediaQuery "(max-width:900px)"
