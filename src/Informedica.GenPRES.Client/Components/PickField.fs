namespace Components


/// One choice among some options: what the field offers is the pick rule's answer, not the
/// page's. A field with nothing to choose is disabled and empty; a field with one option shows
/// it chosen and is disabled; a field with more is enabled when its caller says so and clears
/// when its caller allows and something is chosen.
module PickField =


    open Fable.Core
    open Feliz
    open Shared


    /// The field: its label, the options as key and label, the key chosen, what choosing does,
    /// whether the choice may be cleared, and whether the caller has the field enabled.
    type Props =
        {|
            label: string
            options: (string * string)[]
            selected: string option
            onChange: string option -> unit
            clearable: bool
            isLoading: bool
            enabled: bool
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
        // next field can follow. The dependencies are the option and the choice as strings, so
        // the effect runs when either changes and not on every render.
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
            [| box (only |> Option.defaultValue ""); box chosen |]
        )

        SimpleSelect.View
            {|
                label = props.label
                selected = offer.Selected
                values = props.options
                updateSelected =
                    if PickPolicy.acceptsChange pick then
                        props.onChange
                    else
                        ignore
                isLoading = props.isLoading
                disabled = offer.Disabled
                hasClear = offer.CanClear
                canStep = false
                severity = Types.Severity.Normal
                minWidth = None
            |}
