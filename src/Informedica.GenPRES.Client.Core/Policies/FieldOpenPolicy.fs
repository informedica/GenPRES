namespace Informedica.GenPRES.Client.Core.Policies

/// Decides, per field, whether it offers the cross that empties it and whether it is selectable.
/// The cross is only for a field whose empty state stays and means something; a field with one
/// value is recalculated instead, when the intent policy allows it, since clearing it only has the
/// solver fill it again.
module FieldOpenPolicy =


    /// Whether a field is selectable, and what selecting it does.
    [<RequireQualifiedAccess>]
    type Selectable =
        /// The server recalculates the field and its answer opens as a list.
        | ReCalc
        /// The list of the values the field holds opens.
        | List
        /// Not selectable: nothing to choose, or a value no recalculation is allowed on.
        | No


    /// What a field offers: the cross, and whether it is selectable.
    type Offer =
        {
            /// Whether the field offers the cross that empties it.
            Cross: bool
            /// Whether the field is selectable, and what selecting it does.
            Selectable: Selectable
        }


    /// A field as the decision needs it.
    [<RequireQualifiedAccess>]
    type Field =
        /// A quantity of an order: the number of values it holds (0 for a range or nothing), whether
        /// the intent policy allows its recalculation, and whether it can be used.
        | OrderVariable of values: int * allowed: bool * usable: bool
        /// A filter field, as the pick rule is given it.
        | Filter of PickPolicy.Pick
        /// A field whose empty state stays: whether it can be used, whether it may be emptied,
        /// and whether it holds a value.
        | Entry of usable: bool * emptiable: bool * hasValue: bool


    /// Nothing offered.
    let none =
        {
            Cross = false
            Selectable = Selectable.No
        }


    /// What an order variable offers. Never the cross: an order variable is never left empty.
    /// Several values open as a list. One value is recalculated when the intent policy allows it,
    /// and is not selectable otherwise. A range has no list: a click on it picks the median.
    let orderVariable values allowed usable =
        let selectable =
            match usable, values with
            | false, _ -> Selectable.No
            | true, n when n > 1 -> Selectable.List
            | true, 1 when allowed -> Selectable.ReCalc
            | true, _ -> Selectable.No

        { none with Selectable = selectable }


    /// What a filter field offers. Each list holds the options the field could take given every
    /// other choice, so a chosen field opens as any list does, and the cross drops the choice. One
    /// option is the only one there is: it is chosen, with neither the cross nor a list.
    let filter (pick: PickPolicy.Pick) =
        let offer = PickPolicy.offer pick

        {
            Cross = offer.CanClear
            Selectable = if offer.Disabled then Selectable.No else Selectable.List
        }


    /// What a field whose empty state stays offers: the cross while it holds a value it may lose,
    /// the list while it can be used.
    let entry usable emptiable hasValue =
        {
            Cross = usable && emptiable && hasValue
            Selectable = if usable then Selectable.List else Selectable.No
        }


    /// What the field offers.
    let decide (field: Field) =
        match field with
        | Field.OrderVariable(values, allowed, usable) -> orderVariable values allowed usable
        | Field.Filter pick -> filter pick
        | Field.Entry(usable, emptiable, hasValue) -> entry usable emptiable hasValue


    /// What a click on a selectable field starts.
    [<RequireQualifiedAccess>]
    type Click =
        /// Nothing: not selectable, or a request is under way.
        | Nothing
        /// The list opens at once.
        | OpenList
        /// The field is recalculated; the list waits for the answer.
        | ReCalcAndWait


    /// What a click on the field starts; nothing while a request is under way, since the answer
    /// would land on a field already changing.
    let click isLoading (offer: Offer) =
        match isLoading, offer.Selectable with
        | true, _
        | _, Selectable.No -> Click.Nothing
        | false, Selectable.List -> Click.OpenList
        | false, Selectable.ReCalc -> Click.ReCalcAndWait


    /// What the field shows once the answer to a reopen has landed.
    [<RequireQualifiedAccess>]
    type Reopened =
        /// The list opens with the values answered, one or more.
        | ShowList
        /// No values were answered, so no list opens: the field shows its range, whose click picks the
        /// median when it can step, or a range it cannot move.
        | NoList


    /// What the field shows after a reopen: a list only when there are values to list, so a
    /// recalculation never leads to an empty dropdown.
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
        | Click.ReCalcAndWait, false -> Closed.Restore
        | Click.OpenList, false
        | Click.Nothing, false -> Closed.Keep
