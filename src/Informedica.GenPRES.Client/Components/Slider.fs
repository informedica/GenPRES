namespace Components


module Slider =


    open System
    open Shared
    open Fable.Core

    [<JSX.Component>]
    let View
        (props:
            {|
                label: string
                value: float
                min: float
                max: float
                step: float
                updateValue: float -> unit
                isLoading: bool
            |})
        =

        let handleChange = fun (ev, newValue) -> newValue |> float |> props.updateValue

        let stopPropagation = fun (ev: Browser.Types.Event) -> ev.stopPropagation ()

        let marks =
            [|
                {|
                    value = props.min
                    label = string props.min
                |}
                {|
                    value = props.max
                    label = string props.max
                |}
            |]

        let labelDisplay =
            if props.label |> String.isNullOrWhiteSpace then
                null
            else
                JSX.jsx
                    $"""
                import Typography from '@mui/material/Typography';
                
                <Typography id={props.label} gutterBottom>
                    {props.label}: {props.value}
                </Typography>
                """

        let boxSx =
            {|
                width = 250
                paddingX = 2
            |}

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Slider from '@mui/material/Slider';

        <Box
            sx={boxSx}
            onClick={stopPropagation}
            onMouseDown={stopPropagation}
        >
            {labelDisplay}
            <Slider
                aria-labelledby={props.label}
                name={props.label}
                value={props.value}
                min={props.min}
                max={props.max}
                step={props.step}
                onChange={handleChange}
                valueLabelDisplay="auto"
                marks={marks}
                disabled={props.isLoading}
            />
        </Box>
        """
