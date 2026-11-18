namespace Components


/// The mark on a value outside the bounds of the rules: an icon in the colour of the severity, with
/// the reason, such as the bound the value crosses, in a tooltip on hover or focus. Shows nothing
/// for a normal value.
module SeverityMark =

    open Informedica.GenPRES.Client.Core.Models


    open Fable.Core
    open Feliz
    open Informedica.GenPRES.Shared


    /// The severity, and the reason for the mark; None when there is no reason to show.
    type Props =
        {|
            severity: Severity
            reason: string option
        |}


    /// The icon of a severity, in whatever colour its parent draws; nothing for normal.
    let icon (severity: Severity) =
        match severity with
        | Severity.Normal -> null
        | Severity.Caution ->
            JSX.jsx
                $"""
            import InfoOutlined from '@mui/icons-material/InfoOutlined';
            <InfoOutlined fontSize="small" />
            """
        | Severity.Warning ->
            JSX.jsx
                $"""
            import WarningAmber from '@mui/icons-material/WarningAmber';
            <WarningAmber fontSize="small" />
            """
        | Severity.Alert ->
            JSX.jsx
                $"""
            import ErrorOutlined from '@mui/icons-material/ErrorOutlined';
            <ErrorOutlined fontSize="small" />
            """


    [<JSX.Component>]
    let View (props: Props) =
        match props.severity |> Severity.isRaised, Mui.Styles.severityColor props.severity with
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

            // the reason uses the body text size, because the tooltip text size is too small with
            // the base size of this theme
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

            // With a reason the mark takes the keyboard focus and names the reason for a screen
            // reader, so the tooltip opens on focus as well as on hover.
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
