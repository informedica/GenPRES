namespace Components


/// The heading of a section of a form: a line across, with the section's name on it in small
/// type, and beside the name an action when the section has one, such as removing it. The name
/// is the caller's, resolved where its terms are.
module SectionHeading =


    open Fable.Core
    open Feliz


    /// The heading: the section's name, and an action beside it when there is one.
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
