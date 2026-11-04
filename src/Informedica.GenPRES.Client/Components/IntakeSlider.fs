namespace Components

/// The share of the full intake an order gives, as a slider in steps of ten percent: it sets the dose
/// quantity of the order. A dose set otherwise shows its exact share and no handle.
module IntakeSlider =


    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz
    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Client.Core.Policies


    /// The slider: the id of its caption, unique on the page, the share the dose gives now, whether it
    /// takes a change, what a release sets, and the order it is for, whose change drops a position dragged.
    type Props =
        {|
            labelId: string
            share: decimal option
            disabled: bool
            onCommit: int -> unit
            order: obj
        |}


    // the steps of the intake slider, in percent of the full intake; only a few carry a label, so
    // the slider stays readable on a narrow screen
    let private intakeMarks =
        [|
            for v in 10..10..100 ->
                if v = 10 || v = 50 || v = 100 then
                    createObj [ "value" ==> v; "label" ==> $"%i{v}%%" ]
                else
                    createObj [ "value" ==> v ]
        |]


    // on the right the room of the severity mark of a field, so the slider is as wide as the
    // fields of its column; no room above it, so the total row is no higher than the others and
    // the value that pops up while it moves lies over the caption
    let private sliderBoxSx = {| paddingRight = $"%i{QuantityField.markWidth}px" |}


    // a dose that is no step of the slider has no handle on it
    let private noHandleSx = {| ``& .MuiSlider-thumb`` = {| display = "none" |} |}


    [<JSX.Component>]
    let View (props: Props) =
        // the position while the user drags; otherwise the slider reads it from the order, and a
        // new order drops it
        let dragged, setDragged = React.useState (None: int option)

        React.useEffect ((fun () -> setDragged None), [| props.order |])

        let position = dragged |> Option.orElse (props.share |> Option.bind IntakePolicy.sliderStep)
        let value = position |> Option.defaultValue 100
        let sliderSx = if position.IsSome then null else box noHandleSx
        let track: obj = if position.IsSome then box "normal" else box false

        // a dose set otherwise shows its exact share instead of a handle
        let shareText =
            match position, props.share with
            | None, Some share ->
                let pct = share |> Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision 3
                $": %s{pct}%% van totaal"
            | _ -> ""

        let onIntakeChange = System.Func<obj, int, unit>(fun _ v -> setDragged (Some v))
        let onIntakeCommitted = System.Func<obj, int, unit>(fun _ v -> props.onCommit v)
        let intakeLabel = fun (v: int) -> $"%i{v}%% van totaal"

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Slider from '@mui/material/Slider';
        import Typography from '@mui/material/Typography';
        <Box sx={sliderBoxSx}>
            <Typography id={props.labelId} variant="caption" color="text.secondary">
                toedien hoeveelheid als percentage van totaal{shareText}
            </Typography>
            <Slider
                aria-labelledby={props.labelId}
                value={value}
                onChange={onIntakeChange}
                onChangeCommitted={onIntakeCommitted}
                step={10}
                min={10}
                max={100}
                marks={intakeMarks}
                track={track}
                sx={sliderSx}
                valueLabelDisplay="auto"
                valueLabelFormat={intakeLabel}
                disabled={props.disabled}
            />
        </Box>
        """
