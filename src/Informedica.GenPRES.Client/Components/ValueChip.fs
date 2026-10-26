namespace Components


/// A value in a pill: a derived or reference quantity shown as one thing, such as a dose per
/// day, a rate, or the room left in a fluid budget, coloured by its severity so a value outside
/// the rules is seen before it is read. Nothing but the value and its colour; what it is, and
/// where it stands, is the caller's.
module ValueChip =

    open Informedica.GenPRES.Client.Core.Models


    open Fable.Core
    open Feliz
    open Informedica.GenPRES.Shared


    /// The chip: the value as text, its severity, and a name before it when it needs one.
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

        // a raised severity is told by its icon as well as by its colour, for whoever cannot
        // tell the colours apart
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
