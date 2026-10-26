namespace Components


/// One choice among some options: what the field offers is the pick rule's answer, not the
/// page's. A field with nothing to choose is disabled and empty; a field with one option shows
/// it chosen and is disabled; a field with more is enabled when its caller says so and clears
/// when its caller allows and something is chosen. The rule is the same whether the user
/// scrolls the options or types to narrow them, so both shapes are drawn from here. The field
/// picks nothing itself: an answer of the server already picks every field left with one
/// option.
module PickField =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Policies


    open Fable.Core
    open Feliz
    open Informedica.GenPRES.Shared


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

        let trace event effects =
            StepTrail.event
                "Field"
                $"%s{event} %s{props.label}"
                effects
                (SimpleSelect.fieldState props.selected props.options.Length false)

        // a field narrowed to one option cannot be opened, since there is nothing else to
        // choose, but it can still be emptied, and emptying it is how the user widens the
        // filter and reaches the other options again. Without this a filter built out leaves
        // every field shut, with no way back.
        let hasClear =
            offer.CanClear
            || (offer.Disabled && props.clearable && props.enabled && offer.Selected.IsSome)

        // such a field is not greyed out but held: greying it takes the cross with it, since
        // what a disabled field holds is disabled too
        let isHeld = offer.Disabled && hasClear

        // a field that is held still takes a clearing, since the cross is the only way out of
        // it, and it takes the one value it holds, since a field offered as usable that refuses
        // what it offers is a field that lies. A field with nothing to choose takes neither.
        let updateSelected value =
            match value with
            | None when hasClear ->
                trace "clear" [ "onChange none" ]
                props.onChange None
            | Some _ when PickPolicy.acceptsChange pick || isHeld ->
                trace "pick" [ "onChange" ]
                props.onChange value
            | None -> trace "clear" [ "dropped" ]
            | Some _ -> trace "pick" [ "dropped" ]

        match props.shape with
        | Shape.Scroll ->
            SimpleSelect.View
                {|
                    label = props.label
                    selected = offer.Selected
                    values = props.options
                    updateSelected = updateSelected
                    isLoading = props.isLoading
                    disabled = offer.Disabled && not isHeld
                    readOnly = isHeld
                    hasClear = hasClear
                    canStep = false
                    severity = Severity.Normal
                    minWidth = None
                    description = None
                    reopen = None
                    restore = ignore
                    busy = false
                    placeholder = None
                |}
        | Shape.Type ->
            Autocomplete.View
                {|
                    label = props.label
                    selected = offer.Selected
                    values = props.options |> Array.map fst
                    updateSelected = updateSelected
                    isLoading = props.isLoading
                    disabled = offer.Disabled && not isHeld
                    canClear = hasClear
                |}
