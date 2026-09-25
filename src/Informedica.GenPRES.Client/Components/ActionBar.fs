namespace Components


/// The buttons a page or a dialog ends in, placed by what they do rather than by hand: the
/// action that completes the task is the prominent one and goes on the right; an action that
/// discards or steps back is secondary and goes on the left; an action that destroys is set
/// apart from both. Every button is bounded, never full-width, so it is not where you click
/// by default.
module ActionBar =


    open Fable.Core
    open Feliz


    /// What an action does to the task, which decides how and where its button is drawn.
    [<RequireQualifiedAccess>]
    type Kind =
        /// Completes the task: OK, submit, sign, add to the plan. Contained, on the right.
        | Primary
        /// Discards or steps back without completing: reset, cancel, close. Outlined, on the
        /// left.
        | Secondary
        /// Removes something that exists: delete, remove from the plan. Text in the error
        /// colour, on the left and apart from the secondary actions.
        | Destructive


    /// One action: what it says, what it does, and whether it can be done now.
    type Action =
        {|
            label: string
            kind: Kind
            onClick: unit -> unit
            disabled: bool
            icon: JSX.Element option
        |}


    let private variantOf kind =
        match kind with
        | Kind.Primary -> "contained"
        | Kind.Secondary -> "outlined"
        | Kind.Destructive -> "text"


    let private colorOf kind =
        match kind with
        | Kind.Primary
        | Kind.Secondary -> "primary"
        | Kind.Destructive -> "error"


    /// One action as a button, drawn by its kind. Bounded: it is as wide as its label.
    [<JSX.Component>]
    let ActionButton (props: Action) =
        let onClick = fun _ -> props.onClick ()
        let startIcon = props.icon |> Option.defaultValue null

        JSX.jsx
            $"""
        import Button from '@mui/material/Button';

        <Button
            variant={variantOf props.kind}
            color={colorOf props.kind}
            disabled={props.disabled}
            onClick={onClick}
            startIcon={startIcon}
        >
            {props.label}
        </Button>
        """


    // the full width, so the bar has room to put the primary action on the right wherever it
    // sits: a lone child of a flex container such as CardActions does not stretch by itself
    let private barSx =
        {|
            display = "flex"
            width = "100%"
            justifyContent = "space-between"
            alignItems = "center"
            gap = 1
            marginTop = 2
        |}


    let private groupSx =
        {|
            display = "flex"
            gap = 1
        |}

    // the destructive actions stand apart from the secondary ones
    let private destructiveGroupSx =
        {|
            display = "flex"
            gap = 1
            marginRight = 2
        |}


    /// The actions as a bar: destructive first and secondary next on the left, primary on the
    /// right, whatever order they are given in.
    [<JSX.Component>]
    let View (props: {| actions: Action[] |}) =
        let ofKind kind =
            props.actions |> Array.filter (fun a -> a.kind = kind) |> Array.map ActionButton

        let asFragment (xs: JSX.Element[]) = xs |> unbox<seq<ReactElement>> |> React.Fragment

        let destructive =
            match ofKind Kind.Destructive with
            | [||] -> null
            | xs ->
                let inner = asFragment xs

                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                <Box sx={destructiveGroupSx}>{inner}</Box>
                """

        let secondary = ofKind Kind.Secondary |> asFragment
        let primary = ofKind Kind.Primary |> asFragment

        JSX.jsx
            $"""
        import Box from '@mui/material/Box';

        <Box sx={barSx}>
            <Box sx={groupSx}>
                {destructive}
                {secondary}
            </Box>
            <Box sx={groupSx}>{primary}</Box>
        </Box>
        """
