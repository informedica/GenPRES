namespace Components


/// A button that adds something to a list, such as a feeding or a supplement: small, outlined,
/// with a plus before its name.
module AddButton =


    open Fable.Core
    open Feliz


    /// The button: its name, what a click does, and whether it rests.
    type Props =
        {|
            label: string
            onClick: unit -> unit
            disabled: bool
        |}


    let private buttonSx =
        {|
            marginTop = 1
            marginBottom = 1
        |}


    [<JSX.Component>]
    let View (props: Props) =
        JSX.jsx
            $"""
        import Button from '@mui/material/Button';
        <Button
            variant="outlined"
            size="small"
            startIcon={Mui.Icons.Add}
            disabled={props.disabled}
            onClick={fun _ -> props.onClick ()}
            sx={buttonSx}
        >
            {props.label}
        </Button>
        """
