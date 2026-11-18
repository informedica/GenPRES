namespace Components


/// The heading of a form section: a line across with the section name on it in small type, and
/// an optional action beside the name, such as a remove button. The caller gives the translated name.
module SectionHeading =


    open Fable.Core
    open Feliz


    /// The section name, and an optional action beside it.
    type Props =
        {|
            label: string
            action: JSX.Element option
        |}


    let private rowSx = {| alignItems = "center" |}


    [<JSX.Component>]
    let View (props: Props) =
        let action = props.action |> Option.defaultValue null

        JSX.jsx
            $"""
        import Divider from '@mui/material/Divider';
        import Stack from '@mui/material/Stack';
        import Typography from '@mui/material/Typography';

        <Divider>
            <Stack direction="row" spacing={1} sx={rowSx}>
                <Typography variant="caption">{props.label}</Typography>
                {action}
            </Stack>
        </Divider>
        """
