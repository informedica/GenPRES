namespace Components


/// Shows a derived or reference value in a chip, such as a dose per day, a rate or the volume left
/// in a fluid budget. The chip is coloured by its severity, so a value outside the rules stands out.
module ValueChip =

    open Informedica.GenPRES.Client.Core.Models


    open Fable.Core
    open Feliz
    open Informedica.GenPRES.Shared


    /// The value as text, its severity, and an optional label before it.
    type Props =
        {|
            value: string
            severity: Severity
            label: string option
        |}


    let private colorOf (severity: Severity) =
        match severity with
        | Severity.Normal -> "default"
        | Severity.Caution -> "info"
        | Severity.Warning -> "warning"
        | Severity.Alert -> "error"


    let private rowSx =
        {|
            display = "inline-flex"
            alignItems = "center"
            gap = 0.5
            marginRight = 1
        |}


    let private labelSx = {| color = Mui.Styles.mutedTextColor |}


    [<JSX.Component>]
    let View (props: Props) =
        let color = colorOf props.severity

        // a raised severity also shows an icon, for users who cannot tell the colours apart
        let icon = SeverityMark.icon props.severity

        let label =
            match props.label with
            | None -> null
            | Some label ->
                JSX.jsx
                    $"""
                import Typography from '@mui/material/Typography';
                <Typography variant="body2" sx={labelSx}>{label}</Typography>
                """

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Chip from '@mui/material/Chip';

        <Box sx={rowSx}>
            {label}
            <Chip label={props.value} color={color} variant="outlined" size="small" icon={icon} />
        </Box>
        """
