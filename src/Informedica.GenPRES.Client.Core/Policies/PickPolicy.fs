namespace Informedica.GenPRES.Client.Core.Policies

/// Decides whether a pick field is enabled, what it shows as chosen and whether it can be
/// cleared, from the options it is given, and which command a pick sends.
module PickPolicy =

    open Informedica.GenPRES.Shared.Api


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


    /// Where a pick lies in the options of a field.
    [<RequireQualifiedAccess>]
    type Position =
        /// The value at this position of the options.
        | At of int
        /// The field emptied.
        | Cleared
        /// A value the options do not hold: a bug, which sends nothing.
        | NotOffered


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


    /// Where the value picked lies in the options; an emptied field is cleared.
    let position (options: 'a[]) (picked: 'a option) =
        match picked with
        | None -> Position.Cleared
        | Some x ->
            match options |> Array.tryFindIndex ((=) x) with
            | Some n -> Position.At n
            | None -> Position.NotOffered


    /// The command of a pick, built from the position of the value in the options, or from none for a field
    /// emptied; none for a value the options do not hold.
    let command toCommand options picked =
        match position options picked with
        | Position.At n -> Some(toCommand (Some n))
        | Position.Cleared -> Some(toCommand None)
        | Position.NotOffered -> None


    /// The command of a pick in a filter field: the position of the value in the options, or a clear of the
    /// field emptied; none for a value the options do not hold.
    let filterCommand field options picked =
        let toCommand n =
            match n with
            | Some n -> OrderViewCommand.SetNthFilterProperty(field, n)
            | None -> OrderViewCommand.ClearFilterProperty field

        command toCommand options picked


    /// The command of a diluent pick: the position of the diluent in the options, or a clear of the diluent
    /// emptied; none for a diluent the options do not hold.
    let diluentCommand options picked =
        let toCommand n =
            match n with
            | Some n -> OrderViewCommand.SetNthDiluentProperty n
            | None -> OrderViewCommand.ClearDiluentProperty

        command toCommand options picked
