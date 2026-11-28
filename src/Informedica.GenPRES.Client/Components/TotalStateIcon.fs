namespace Components


/// A small icon for the state of a total against its norm: within, too low, too high, or too low and too high at
/// once. A total without a norm, or a total that is not there, shows no icon.
module TotalStateIcon =

    open Fable.Core
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Client.Core.Models


    /// The label of the icon of each state, for a screen reader and on hover.
    type Labels =
        {|
            withinNorm: string
            belowNorm: string
            aboveNorm: string
            outsideNorm: string
        |}


    /// Returns the labels of the icons in the language of the user, with getTerm.
    let labels getTerm : Labels =
        {|
            withinNorm = Terms.``Totals Within Norm`` |> getTerm "Within the norm"
            belowNorm = Terms.``Totals Below Norm`` |> getTerm "Too low"
            aboveNorm = Terms.``Totals Above Norm`` |> getTerm "Too high"
            outsideNorm = Terms.``Totals Outside Norm`` |> getTerm "Too low and too high"
        |}


    let private iconSx (color: string) =
        {|
            fontSize = "1rem"
            color = color
        |}


    let private withinSx = iconSx Mui.Styles.validColor


    let private offSx = iconSx Mui.Styles.warningColor


    let private markSx =
        {|
            display = "inline-flex"
            verticalAlign = "middle"
        |}


    // the icon of a state with its label, or None without a state or for a total without a norm
    let private iconOf (labels: Labels) state =
        match state with
        | None
        | Some TotalState.NoNorm -> None
        | Some TotalState.WithinNorm ->
            (JSX.jsx
                $"""
            import CheckCircleOutlinedIcon from '@mui/icons-material/CheckCircleOutlined';
            <CheckCircleOutlinedIcon sx={withinSx} />
            """,
             labels.withinNorm)
            |> Some
        | Some TotalState.BelowNorm ->
            (JSX.jsx
                $"""
            import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward';
            <ArrowDownwardIcon sx={offSx} />
            """,
             labels.belowNorm)
            |> Some
        | Some TotalState.AboveNorm ->
            (JSX.jsx
                $"""
            import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward';
            <ArrowUpwardIcon sx={offSx} />
            """,
             labels.aboveNorm)
            |> Some
        | Some TotalState.OutsideNorm ->
            (JSX.jsx
                $"""
            import SwapVertIcon from '@mui/icons-material/SwapVert';
            <SwapVertIcon sx={offSx} />
            """,
             labels.outsideNorm)
            |> Some


    [<JSX.Component>]
    let View
        (props:
            {|
                state: TotalState option
                labels: Labels
            |})
        =
        // the mark takes the keyboard focus and names the state for a screen reader, so the tooltip opens on
        // focus as well as on hover
        match props.state |> iconOf props.labels with
        | None -> null
        | Some(icon, label) ->
            JSX.jsx
                $"""
            import Box from '@mui/material/Box';
            import Tooltip from '@mui/material/Tooltip';
            <Tooltip title={label} arrow>
                <Box sx={markSx} role="img" aria-label={label} tabIndex={0}>{icon}</Box>
            </Tooltip>
            """
