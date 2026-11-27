namespace Informedica.GenPRES.Client.Core.Policies

/// Decides whether an order field is selectable, and which filter fields of a linked offer can change.
/// An order variable has no clear button, because the solver would fill it again. A field with a single
/// value is recalculated instead when the intent policy allows it.
module FieldOpenPolicy =

    open Informedica.GenPRES.Shared.Models


    /// Whether a field is selectable, and what selecting it does.
    [<RequireQualifiedAccess>]
    type Selectable =
        /// The server recalculates the field and its answer opens as a list.
        | ReCalc
        /// The list of the field values opens.
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
        /// No values were returned, so no list opens. The field shows its range; a click on it picks the
        /// median when the range has a step.
        | NoList


    /// What the field shows after a recalculation: a list only when there are values to list, so a
    /// recalculation never leads to an empty dropdown.
    let afterReCalc values =
        match values with
        | 0 -> AfterReCalc.NoList
        | _ -> AfterReCalc.ShowList


    /// Whether a filter field of a linked offer can be picked or cleared: only the form can. The link rule
    /// sets the generic, indication, route and dose type of the offer; changing one of them could widen the
    /// offer to the dose of its main order.
    let offerFilter (field: OrderContext.FilterField) =
        match field with
        | OrderContext.FilterField.Form -> true
        | OrderContext.FilterField.Indication
        | OrderContext.FilterField.Generic
        | OrderContext.FilterField.Route
        | OrderContext.FilterField.DoseType -> false
