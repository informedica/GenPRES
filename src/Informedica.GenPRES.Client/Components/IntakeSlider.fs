namespace Components

/// A slider in steps of ten percent that sets the dose quantity of an order as a share of the full
/// intake. The handle shows the exact share of the dose, and the caption shows it as a number.
module IntakeSlider =


    open Fable.Core
    open Fable.Core.JsInterop
    open Feliz
    open Informedica.GenPRES.Shared


    /// The caption and the sentence that tells the percentage of the total.
    type Texts =
        {|
            caption: string
            shareOfTotal: string -> string
        |}


    /// The caption id, unique on the page, the current share of the dose, whether the slider is
    /// disabled, the handler called with the percentage on release, the order, and the texts.
    /// A new order clears a dragged position.
    type Props =
        {|
            labelId: string
            share: decimal option
            disabled: bool
            onCommit: int -> unit
            order: obj
            texts: Texts
        |}


    // A mark at every ten percent. Only 10, 50 and 100 percent carry a label, so the slider stays
    // readable on a narrow screen.
    let private intakeMarks =
        [|
            for v in 10..10..100 ->
                if v = 10 || v = 50 || v = 100 then
                    createObj [ "value" ==> v; "label" ==> $"%i{v}%%" ]
                else
                    createObj [ "value" ==> v ]
        |]


    // Right padding as wide as the severity mark of a field, so the slider is as wide as the fields
    // in its column. No top padding, so the total row is no higher than the others; the value label
    // shown while dragging lies over the caption.
    let private sliderBoxSx = {| paddingRight = $"%i{QuantityField.markWidth}px" |}


    // hides the handle when the order has no share, because the dose or the total is not one value
    let private noHandleSx = {| ``& .MuiSlider-thumb`` = {| display = "none" |} |}


    [<JSX.Component>]
    let View (props: Props) =
        // The position while the user drags. Without it the slider shows the share of the order.
        // A new order clears it.
        let dragged, setDragged = React.useState (None: int option)

        React.useEffect ((fun () -> setDragged None), [| props.order |])

        // The slider is disabled while the request sent on release runs. When it is enabled again the
        // dragged position is cleared, so after a failed request the slider shows the unchanged order.
        React.useEffect (
            (fun () ->
                if not props.disabled then
                    setDragged None
            ),
            [| box props.disabled |]
        )

        // The slider rounds only what the user drags to a step of ten. A value it is given is drawn
        // where it lies, so the handle follows a dose that is not a slider step.
        let position = dragged |> Option.map float |> Option.orElse (props.share |> Option.map float)

        let value = position |> Option.defaultValue 100.
        let sliderSx = if position.IsSome then null else box noHandleSx
        let track: obj = if position.IsSome then box "normal" else box false

        let percentText (pct: float) =
            pct |> decimal |> Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision 3

        // the caption shows the exact share, as the handle cannot show a change of less than a percent
        let shareText =
            match props.share with
            | Some share -> $": %s{share |> float |> percentText |> props.texts.shareOfTotal}"
            | None -> ""

        let onIntakeChange = System.Func<obj, int, unit>(fun _ v -> setDragged (Some v))
        let onIntakeCommitted = System.Func<obj, int, unit>(fun _ v -> props.onCommit v)
        let intakeLabel = fun (v: float) -> v |> percentText |> props.texts.shareOfTotal

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';
        import Slider from '@mui/material/Slider';
        import Typography from '@mui/material/Typography';
        <Box sx={sliderBoxSx}>
            <Typography id={props.labelId} variant="caption" color="text.secondary">
                {props.texts.caption}{shareText}
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
