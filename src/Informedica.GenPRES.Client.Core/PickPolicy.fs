/// Decides whether a pick field is enabled, what it shows as chosen and whether it can be
/// cleared, from the options it is given.
module PickPolicy


/// What the field is given: its options as keys, the key chosen, whether clearing is
/// allowed, and whether the caller has it enabled.
type Pick =
    {
        /// The keys the user may choose from.
        Options: string[]
        /// The key chosen so far, if any.
        Chosen: string option
        /// Whether the caller allows the choice to be cleared.
        Clearable: bool
        /// Whether the caller has the field enabled at all.
        Enabled: bool
    }


/// What the field shows: whether it can be used, the key shown as chosen, and whether the
/// cross that clears is offered.
type Offer =
    {
        /// The field cannot be used.
        Disabled: bool
        /// The key shown as chosen.
        Selected: string option
        /// The cross that clears is offered.
        CanClear: bool
    }


/// The rule.
let offer (pick: Pick) =
    match pick.Options with
    | [||] ->
        {
            Disabled = true
            Selected = None
            CanClear = false
        }
    | [| only |] ->
        {
            Disabled = true
            Selected = Some only
            CanClear = false
        }
    | options ->
        // a chosen key the options no longer hold is not shown as chosen
        let selected = pick.Chosen |> Option.filter (fun k -> options |> Array.contains k)

        {
            Disabled = not pick.Enabled
            Selected = selected
            CanClear = pick.Clearable && pick.Enabled && selected.IsSome
        }


/// Whether a change the user makes is taken: only from a field that is offered enabled.
let acceptsChange (pick: Pick) = (offer pick).Disabled |> not
