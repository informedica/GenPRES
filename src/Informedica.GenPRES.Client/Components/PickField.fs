namespace Components


/// One choice among some options: what the field offers is the pick rule's answer, not the
/// page's. A field with nothing to choose is disabled and empty; a field with one option shows
/// it chosen and is disabled; a field with more is enabled when its caller says so and clears
/// when its caller allows and something is chosen. The rule is the same whether the user
/// scrolls the options or types to narrow them, so both shapes are drawn from here and the one
/// option is told to the page once, whichever shape the field has.
module PickField =


    open Fable.Core
    open Feliz
    open Shared


    /// How the field is drawn. The rule does not change with it: a long list is easier to type
    /// into than to scroll, and that is all the difference amounts to.
    [<RequireQualifiedAccess>]
    type Shape =
        /// A list the user opens and scrolls.
        | Scroll
        /// A box the user types in, narrowing the options as they type.
        | Type


    /// The field: its label, the options as key and label, the key chosen, what choosing does,
    /// whether the choice may be cleared, whether the caller has the field enabled, and how it
    /// is drawn.
    type Props =
        {|
            label: string
            options: (string * string)[]
            selected: string option
            onChange: string option -> unit
            clearable: bool
            isLoading: bool
            enabled: bool
            shape: Shape
        |}


    // a shut field and the cross that empties it, side by side
    let private besideSx =
        {|
            display = "flex"
            alignItems = "flex-end"
            flexGrow = 1
            minWidth = 0
        |}


    [<JSX.Component>]
    let View (props: Props) =
        let pick: PickPolicy.Pick =
            {
                Options = props.options |> Array.map fst
                Chosen = props.selected
                Clearable = props.clearable
                Enabled = props.enabled
            }

        let offer = PickPolicy.offer pick

        // The one option a field shows chosen is chosen for the page too, once, when the field
        // arrives at it: the choice the user could only have made is made for them, and the
        // next field can follow. The dependencies are the option and the choice as strings and
        // whether the field is enabled, so the effect runs when any of them changes, also when
        // a field that was disabled with its one option already there becomes enabled, and not
        // on every render.
        let only =
            match props.options with
            | [| (key, _) |] -> Some key
            | _ -> None

        let chosen = props.selected |> Option.defaultValue ""

        React.useEffect (
            (fun () ->
                match only with
                | Some key when props.selected <> Some key && props.enabled -> props.onChange (Some key)
                | _ -> ()
            ),
            [| box (only |> Option.defaultValue ""); box chosen; box props.enabled |]
        )

        // a field narrowed to one option cannot be opened, since there is nothing else to
        // choose, but it can still be emptied, and emptying it is how the user widens the
        // filter and reaches the other options again. Without this a filter built out leaves
        // every field shut, with no way back.
        let hasClear =
            offer.CanClear
            || (offer.Disabled && props.clearable && props.enabled && offer.Selected.IsSome)

        // a field that cannot be opened still takes a clearing, since the cross is the only way
        // out of it; a value is taken only from a field that can be used
        let updateSelected value =
            match value with
            | None when hasClear -> props.onChange None
            | Some _ when PickPolicy.acceptsChange pick -> props.onChange value
            | _ -> ()

        match props.shape with
        | Shape.Scroll ->
            SimpleSelect.View
                {|
                    label = props.label
                    selected = offer.Selected
                    values = props.options
                    updateSelected = updateSelected
                    isLoading = props.isLoading
                    disabled = offer.Disabled
                    hasClear = hasClear
                    canStep = false
                    severity = Types.Severity.Normal
                    minWidth = None
                |}
        | Shape.Type ->
            let box =
                Autocomplete.View
                    {|
                        label = props.label
                        selected = offer.Selected
                        values = props.options |> Array.map fst
                        updateSelected = updateSelected
                        isLoading = props.isLoading
                        disabled = offer.Disabled
                        // a box that is shut disables the cross it holds, so in that case the
                        // cross stands beside it instead
                        canClear = hasClear && not offer.Disabled
                    |}

            if not (offer.Disabled && hasClear) then
                box
            else
                let clear = fun _ -> props.onChange None

                JSX.jsx
                    $"""
                import Box from '@mui/material/Box';
                import IconButton from '@mui/material/IconButton';

                <Box sx={besideSx}>
                    {box}
                    <IconButton onClick={clear} aria-label="clear">
                        {Mui.Icons.Clear}
                    </IconButton>
                </Box>
                """
