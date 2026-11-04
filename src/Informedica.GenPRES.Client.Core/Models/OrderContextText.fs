namespace Informedica.GenPRES.Client.Core.Models

/// What the pages call an order context.
module OrderContextText =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models


    /// What a page calls the context: the category's name for a nutrition order, the
    /// generic for a medication, nothing before a generic is chosen.
    let label (ctx: OrderContext) =
        match ctx.Category with
        | OrderCategory.Nutrition category -> NutritionCategory.label category
        | OrderCategory.Medication -> ctx.Filter.Generic |> Option.defaultValue ""


    /// What a page calls an offer: its generic with the dose text of its dose type, which tells it from its
    /// main order of the same generic; the generic alone for a dose type without a text.
    let offerName (ctx: OrderContext) =
        match ctx.Filter.Generic, ctx.Filter.DoseType |> Option.bind DoseTypeText.text with
        | Some generic, Some text -> $"%s{generic} (%s{text})"
        | Some generic, None -> generic
        | None, _ -> label ctx
