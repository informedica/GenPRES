namespace Informedica.GenPRES.Client.Core.Models

/// The name a page shows for an order context.
module OrderContextText =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models


    /// Returns the name of the nutrition category for a nutrition order, the generic for a medication
    /// order, or an empty string when no generic is chosen.
    let label (ctx: OrderContext) =
        match ctx.Category with
        | OrderCategory.Nutrition category -> NutritionCategory.label category
        | OrderCategory.Medication -> ctx.Filter.Generic |> Option.defaultValue ""


    /// Returns the generic with the text of the dose type, which tells the order apart from the main order of
    /// the same generic. Returns the generic alone when the dose type has no text, and the label when no
    /// generic is chosen.
    let offerName (ctx: OrderContext) =
        match ctx.Filter.Generic, ctx.Filter.DoseType |> Option.bind DoseTypeText.text with
        | Some generic, Some text -> $"%s{generic} (%s{text})"
        | Some generic, None -> generic
        | None, _ -> label ctx
