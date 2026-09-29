/// Decides, per field, whether it offers the cross that empties it and what its dropdown arrow
/// does. The cross is only for a field whose empty state stays and means something; a field the
/// user narrowed is reopened by its arrow instead, since clearing it only has the solver fill it
/// again.
module FieldOpenPolicy


/// Whether the user constrained the value of an order variable.
[<RequireQualifiedAccess>]
type Constrained =
    /// The user picked or stepped the value.
    | Yes
    /// The solver derived the value, or the server chose it by default.
    | No
    /// The order does not say.
    | Unknown


/// What a click on the dropdown arrow does.
[<RequireQualifiedAccess>]
type Arrow =
    /// Opens the list of the values the field holds.
    | Open
    /// Clears the field's own choice and opens what the server returns.
    | ReopenCleared
    /// The field has no arrow: nothing to choose, or a value the solver determined.
    | NoArrow


/// What a field offers: the cross and the arrow.
type Offer =
    {
        /// Whether the field offers the cross that empties it.
        Cross: bool
        /// What its arrow does.
        Arrow: Arrow
    }


/// A field as the decision needs it.
[<RequireQualifiedAccess>]
type Field =
    /// A quantity of an order: the number of values it holds (0 for a range or nothing), how
    /// the user may move it, whether the user constrained it, and whether it can be used.
    | OrderVariable of values: int * mode: QuantityModePolicy.Mode * constrained: Constrained * usable: bool
    /// A filter field, as the pick rule is given it.
    | Filter of PickPolicy.Pick
    /// A field whose empty state stays: whether it can be used, whether it may be emptied,
    /// and whether it holds a value.
    | Entry of usable: bool * emptiable: bool * hasValue: bool


/// Nothing offered.
let none =
    {
        Cross = false
        Arrow = Arrow.NoArrow
    }


/// What an order variable offers. Never the cross: an order variable is never left empty.
/// Several values open as a list. One value the user constrained, or may have, is reopened; one
/// the solver determined has no arrow. A range has no list: a click on it picks the median.
let orderVariable values (mode: QuantityModePolicy.Mode) (constrained: Constrained) usable =
    let arrow =
        match usable, values, mode, constrained with
        | false, _, _, _ -> Arrow.NoArrow
        | true, n, _, _ when n > 1 -> Arrow.Open
        | true, 1, _, Constrained.No -> Arrow.NoArrow
        | true, 1, _, Constrained.Yes
        | true, 1, _, Constrained.Unknown -> Arrow.ReopenCleared
        | true, _, _, _ -> Arrow.NoArrow

    { none with Arrow = arrow }


/// What a filter field offers. Each list holds the options the field could take given every
/// other choice, so a chosen field opens as any list does, and the cross drops the choice. One
/// option is the only one there is: it is chosen, with neither cross nor arrow.
let filter (pick: PickPolicy.Pick) =
    let offer = PickPolicy.offer pick

    {
        Cross = offer.CanClear
        Arrow = if offer.Disabled then Arrow.NoArrow else Arrow.Open
    }


/// What a field whose empty state stays offers: the cross while it holds a value it may lose,
/// the arrow while it can be used.
let entry usable emptiable hasValue =
    {
        Cross = usable && emptiable && hasValue
        Arrow = if usable then Arrow.Open else Arrow.NoArrow
    }


/// What the field offers.
let decide (field: Field) =
    match field with
    | Field.OrderVariable(values, mode, constrained, usable) -> orderVariable values mode constrained usable
    | Field.Filter pick -> filter pick
    | Field.Entry(usable, emptiable, hasValue) -> entry usable emptiable hasValue


/// What a click on the arrow starts.
[<RequireQualifiedAccess>]
type Click =
    /// Nothing: no arrow, or a request is under way.
    | Nothing
    /// The list opens at once.
    | OpenList
    /// The field's choice is cleared; the list waits for the answer.
    | ClearAndWait


/// What a click on the arrow starts; nothing while a request is under way, since the answer
/// would land on a field already changing.
let click isLoading (offer: Offer) =
    match isLoading, offer.Arrow with
    | true, _
    | _, Arrow.NoArrow -> Click.Nothing
    | false, Arrow.Open -> Click.OpenList
    | false, Arrow.ReopenCleared -> Click.ClearAndWait


/// What the field shows once the answer to a reopen has landed.
[<RequireQualifiedAccess>]
type Reopened =
    /// The list opens with the values answered, one or more.
    | ShowList
    /// No values were answered, so no list opens: the field shows its range, whose click picks the
    /// median when it can step, or a range it cannot move.
    | NoList


/// What the field shows after a reopen: a list only when there are values to list, so the arrow
/// never leads to an empty dropdown.
let reopened values =
    match values with
    | 0 -> Reopened.NoList
    | _ -> Reopened.ShowList


/// What a list closed without a pick does after a reopen: the order shown before the click
/// is put back, so a look never undoes a choice.
[<RequireQualifiedAccess>]
type Closed =
    /// The pick stands.
    | Keep
    /// The order before the click is put back.
    | Restore


/// What closing the list does: a pick stands; a close without one restores, but only after a
/// reopen, since a plain list changed nothing.
let closed (click: Click) picked =
    match click, picked with
    | _, true -> Closed.Keep
    | Click.ClearAndWait, false -> Closed.Restore
    | Click.OpenList, false
    | Click.Nothing, false -> Closed.Keep
