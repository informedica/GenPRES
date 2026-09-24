namespace Components


/// The step buttons beside a value: to its lowest, one step down, to its median, one step
/// up, to its highest. A button whose step the caller does not offer is drawn disabled, so the
/// row keeps its shape. The stepper only sends the clicks; what the value becomes, and what is
/// shown while the server answers, is the caller's.
module Stepper =


    open Fable.Core
    open Feliz


    /// The steps offered, each absent when the value cannot move that way, and what a click
    /// does. With useDebounce the one-step and outer buttons count rapid clicks and send them
    /// as one, telling the caller each click as a small (one step) or large (outer) delta.
    type Props =
        {|
            first: (int -> unit) option
            decrease: (int -> unit) option
            median: (unit -> unit) option
            increase: (int -> unit) option
            last: (int -> unit) option
            useDebounce: bool
            disabled: bool
            onSmallStep: int -> unit
            onLargeStep: int -> unit
        |}


    let private plain (disabled: bool) (onClick: unit -> unit) (icon: JSX.Element) =
        let click = fun _ -> onClick ()

        JSX.jsx
            $"""
        import IconButton from "@mui/material/IconButton";
        <IconButton size="small" sx={Mui.Styles.stepButtonSx} disabled={disabled} onClick={click}>{icon}</IconButton>
        """


    /// A counted button when clicks are debounced, a plain one otherwise; disabled when the
    /// step is not offered or the whole stepper rests.
    let private stepButton (props: Props) (step: (int -> unit) option) (onStep: unit -> unit) icon =
        match step with
        | None -> plain true ignore icon
        | Some onClick when props.useDebounce ->
            ClickCountingButton.View
                {|
                    disabled = props.disabled
                    onClick = onClick
                    onStep = onStep
                    icon = icon
                |}
        | Some onClick -> plain props.disabled (fun () -> onClick 1) icon


    let private groupSx =
        {|
            display = "flex"
            flexDirection = "column"
            alignItems = "center"
        |}


    /// The five buttons as one group.
    [<JSX.Component>]
    let View (props: Props) =
        let first = stepButton props props.first (fun () -> props.onLargeStep -1) Mui.Icons.FirstPageIcon
        let decrease =
            stepButton props props.decrease (fun () -> props.onSmallStep -1) Mui.Icons.SkipPreviousIcon
        let increase = stepButton props props.increase (fun () -> props.onSmallStep 1) Mui.Icons.SkipNextIcon
        let last = stepButton props props.last (fun () -> props.onLargeStep 1) Mui.Icons.LastPageIcon

        let median =
            match props.median with
            | None -> plain true ignore Mui.Icons.PauseIcon
            | Some onClick -> plain props.disabled onClick Mui.Icons.PauseIcon

        JSX.jsx
            $"""
        import ButtonGroup from '@mui/material/ButtonGroup';
        import Box from '@mui/material/Box';

        <Box sx={groupSx}>
            <ButtonGroup variant="text" aria-label="step buttons">
                {first}
                {decrease}
                {median}
                {increase}
                {last}
            </ButtonGroup>
        </Box>
        """
