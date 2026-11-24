namespace Components


/// The row of action buttons at the end of a page or a dialog, placed by kind: primary actions on
/// the right, destructive and secondary actions on the left. Every button is as wide as its label.
module ActionBar =


    open Informedica.GenPRES.Client.Core.Models

    open Fable.Core
    open Feliz


    /// The kind of an action, which sets the style and the place of its button.
    [<RequireQualifiedAccess>]
    type Kind =
        /// Completes the task: OK, submit, sign, add to the plan. Contained, on the right.
        | Primary
        /// Does not complete the task: reset, cancel, close, edit. Outlined, on the left.
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


    /// The primary action that prescribes, labelled with the Prescribe term.
    let prescribe getTerm onClick disabled : Action =
        {|
            label = Terms.``Prescribe`` |> getTerm "Prescribe"
            kind = Kind.Primary
            onClick = onClick
            disabled = disabled
            icon = Some Mui.Icons.Add
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


    /// Renders an action as a button styled by its kind. A mouse press does not move the focus,
    /// so a field being edited does not blur and start a request before the click arrives.
    [<JSX.Component>]
    let ActionButton (props: Action) =
        let onClick = fun _ -> props.onClick ()
        let keepFocus = fun (e: Browser.Types.MouseEvent) -> e.preventDefault ()
        let startIcon = props.icon |> Option.defaultValue null

        JSX.jsx
            $"""
        import Button from '@mui/material/Button';

        <Button
            variant={variantOf props.kind}
            color={colorOf props.kind}
            disabled={props.disabled}
            onMouseDown={keepFocus}
            onClick={onClick}
            startIcon={startIcon}
        >
            {props.label}
        </Button>
        """


    // Full width, so the primary actions can sit on the right; a lone child of a flex container
    // such as CardActions does not stretch by itself.
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
