namespace Components


/// The mark on a value that stands outside what the rules allow: an icon in the severity's
/// colour, and on hover the reason, the bound the value crosses. Nothing for a normal value.
/// The reason shows on hover and nowhere else: no popup, nothing to dismiss.
module SeverityMark =


    open Fable.Core
    open Feliz
    open Shared


    /// The severity, and the reason as the caller words it; none when the caller cannot say.
    type Props =
        {|
            severity: Types.Severity
            reason: string option
        |}


    /// The icon of a severity, in whatever colour its parent draws; nothing for normal.
    let icon (severity: Types.Severity) =
        match severity with
        | Types.Severity.Normal -> null
        | Types.Severity.Caution ->
            JSX.jsx
                $"""
            import InfoOutlined from '@mui/icons-material/InfoOutlined';
            <InfoOutlined fontSize="small" />
            """
        | Types.Severity.Warning ->
            JSX.jsx
                $"""
            import WarningAmber from '@mui/icons-material/WarningAmber';
            <WarningAmber fontSize="small" />
            """
        | Types.Severity.Alert ->
            JSX.jsx
                $"""
            import ErrorOutlined from '@mui/icons-material/ErrorOutlined';
            <ErrorOutlined fontSize="small" />
            """


    [<JSX.Component>]
    let View (props: Props) =
        match props.severity |> Models.Severity.isRaised, Mui.Styles.severityColor props.severity with
        | false, _
        | _, None -> null
        | true, Some color ->
            let markSx =
                {|
                    display = "inline-flex"
                    alignItems = "center"
                    color = color
                    // on the value's baseline beside a select, as the step buttons are
                    paddingBottom = "4px"
                |}

            // the reason reads at the body text size: the tooltip's own is too small under
            // this theme's base size
            let tooltipSlotProps =
                {|
                    tooltip =
                        {|
                            sx =
                                {|
                                    typography = "body2"
                                    padding = "6px 10px"
                                |}
                        |}
                |}

            // the mark can be reached with the keyboard as well as the pointer, and names its
            // reason for a reader, so the tooltip opens on focus as it does on hover
            match props.reason with
            | None ->
                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                <Box sx={markSx} role="img">{icon props.severity}</Box>
                """
            | Some reason ->
                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                import Tooltip from '@mui/material/Tooltip';
                <Tooltip title={reason} arrow slotProps={tooltipSlotProps}>
                    <Box sx={markSx} role="img" aria-label={reason} tabIndex={0}>{icon props.severity}</Box>
                </Tooltip>
                """
