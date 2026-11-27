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
        /// A value that is not in the options: a bug, which sends nothing.
        | NotOffered


    /// What the field shows.
    type Offer =
        {
            /// Whether the field cannot be used.
            Disabled: bool
            /// The key shown as chosen.
            Selected: string option
            /// Whether the field shows a clear button.
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
            // A chosen key that is no longer in the options is not shown as chosen.
            let selected = pick.Chosen |> Option.filter (fun k -> options |> Array.contains k)

            {
                Disabled = not pick.Enabled
                Selected = selected
                CanClear = pick.Clearable && pick.Enabled && selected.IsSome
            }


    /// Whether the field takes a change from the user: only when it is enabled.
    let acceptsChange (pick: Pick) = (offer pick).Disabled |> not


    /// Returns the position of the picked value in the options; Cleared when nothing is picked.
    let position (options: 'a[]) (picked: 'a option) =
        match picked with
        | None -> Position.Cleared
        | Some x ->
            match options |> Array.tryFindIndex ((=) x) with
            | Some n -> Position.At n
            | None -> Position.NotOffered


    /// Builds the command of a pick from the position of the value in the options, or from None for an
    /// emptied field. Returns None for a value that is not in the options.
    let command toCommand options picked =
        match position options picked with
        | Position.At n -> Some(toCommand (Some n))
        | Position.Cleared -> Some(toCommand None)
        | Position.NotOffered -> None


    /// Returns the command of a filter field pick: set the value at its position, or clear the field when
    /// emptied. Returns None for a value that is not in the options.
    let filterCommand field options picked =
        let toCommand n =
            match n with
            | Some n -> OrderViewCommand.SetNthFilterProperty(field, n)
            | None -> OrderViewCommand.ClearFilterProperty field

        command toCommand options picked


    /// Returns the command of a diluent pick: set the diluent at its position, or clear the diluent when
    /// emptied. Returns None for a diluent that is not in the options.
    let diluentCommand options picked =
        let toCommand n =
            match n with
            | Some n -> OrderViewCommand.SetNthDiluentProperty n
            | None -> OrderViewCommand.ClearDiluentProperty

        command toCommand options picked
