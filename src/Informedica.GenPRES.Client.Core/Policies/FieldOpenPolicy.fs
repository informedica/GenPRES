namespace Informedica.GenPRES.Client.Core.Policies

/// Decides whether a field of an order is selectable. An order variable never offers the cross that
/// empties it: a field with one value is recalculated instead, when the intent policy allows it,
/// since clearing it only has the solver fill it again.
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


    /// Whether an order variable is selectable. Several values open as a list. One value is
    /// recalculated when the intent policy allows it, and is not selectable otherwise. A range has
    /// no list: a click on it picks the median.
    let orderVariable values allowed usable =
        match usable, values with
        | false, _ -> Selectable.No
        | true, n when n > 1 -> Selectable.List
        | true, 1 when allowed -> Selectable.ReCalc
        | true, _ -> Selectable.No


    /// What the field shows once the answer to a recalculation has landed.
    [<RequireQualifiedAccess>]
    type AfterReCalc =
        /// The list opens with the values answered, one or more.
        | ShowList
        /// No values were answered, so no list opens: the field shows its range, whose click picks the
        /// median when it can step, or a range it cannot move.
        | NoList


    /// What the field shows after a recalculation: a list only when there are values to list, so a
    /// recalculation never leads to an empty dropdown.
    let afterReCalc values =
        match values with
        | 0 -> AfterReCalc.NoList
        | _ -> AfterReCalc.ShowList
