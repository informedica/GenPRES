namespace Components


/// A field to pick one option, drawn as PickPolicy decides. A field without options is disabled
/// and empty; a field with one option shows it selected and is disabled; a field with more options
/// is enabled when the caller enables it. Both shapes follow the same rule. The field never picks
/// an option itself, because the server already picks the option of a field with a single option.
module PickField =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Policies


    open Fable.Core
    open Feliz
    open Informedica.GenPRES.Shared


    /// How the field is drawn. Both shapes follow the same rule; typing suits a long list better.
    [<RequireQualifiedAccess>]
    type Shape =
        /// A list the user opens and scrolls.
        | Scroll
        /// A box the user types in, narrowing the options as they type.
        | Type


    /// The label, the options as key and label pairs, the selected key, the change handler, whether
    /// the selection may be cleared, whether the options are loading, whether the caller enables the
    /// field, and the shape.
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

        // A field with a single option cannot be opened but can still be cleared. Clearing it widens
        // the filter, so the user gets the other options back.
        let hasClear =
            offer.CanClear
            || (offer.Disabled && props.clearable && props.enabled && offer.Selected.IsSome)

        // Such a field is read-only rather than disabled, because a disabled field also disables
        // its clear button.
        let isHeld = offer.Disabled && hasClear

        // A read-only field accepts a clear, its only way out, and a pick of the value it shows.
        // A field without options accepts neither.
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
                    reCalc = None
                    restore = ignore
                    busy = false
                    placeholder = None
                    placeholderValue = None
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
                    placeholder = None
                |}
