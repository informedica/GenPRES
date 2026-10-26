namespace Informedica.GenPRES.Client.Core.Policies

/// Decides whether a pick field is enabled, what it shows as chosen and whether it can be
/// cleared, from the options it is given.
module PickPolicy =


    /// What the caller gives the field.
    type Pick =
        {
            /// The keys the user may choose from.
            Options: string[]
            /// The key chosen so far, if any.
            Chosen: string option
            /// Whether the caller allows the choice to be cleared.
            Clearable: bool
            /// Whether the caller has the field enabled.
            Enabled: bool
        }


    /// What the field shows.
    type Offer =
        {
            /// Whether the field cannot be used.
            Disabled: bool
            /// The key shown as chosen.
            Selected: string option
            /// Whether the field offers the cross that clears it.
            CanClear: bool
        }


    /// What the field shows: no option leaves it disabled and empty; one option is shown chosen and
    /// disabled; more options follow the caller's enabled flag, and can be cleared when the caller
    /// allows it and something is chosen.
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


    /// Whether the field takes a change from the user: only when it is enabled.
    let acceptsChange (pick: Pick) = (offer pick).Disabled |> not
