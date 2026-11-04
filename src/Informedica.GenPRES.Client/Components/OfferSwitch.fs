namespace Components


/// An order the link rules offer with a main order: its name with the switch that turns it on, and while it
/// is on the controls the page gives it, under its name.
module OfferSwitch =


    open Fable.Core
    open Feliz
    open Informedica.GenPRES.Client.Core.StateMachines.OrderContextMachine


    /// The offer: its name, its switch, whether the switch is disabled, what a turn does, and its controls.
    type Props =
        {|
            name: string
            switch: OfferSwitch
            disabled: bool
            onTurn: OfferSwitch -> unit
            controls: JSX.Element
        |}


    // the room of the switch before an offer's controls, so they stand under its name
    let private offerControlsSx = {| paddingLeft = 6 |}


    [<JSX.Component>]
    let View (props: Props) =
        let isOn = props.switch = OfferSwitch.On
        let handleSwitch _ = props.switch |> OfferSwitch.flip |> props.onTurn

        let controls =
            if isOn then
                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                <Box sx={offerControlsSx}>{props.controls}</Box>
                """
            else
                null

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import FormControlLabel from '@mui/material/FormControlLabel';
        import Switch from '@mui/material/Switch';

        <Box>
            <FormControlLabel
                control={{<Switch checked={isOn} disabled={props.disabled} onChange={handleSwitch} />}}
                label={props.name}
            />
            {controls}
        </Box>
        """
