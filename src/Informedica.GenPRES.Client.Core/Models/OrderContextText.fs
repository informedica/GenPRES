namespace Informedica.GenPRES.Client.Core.Models

/// What the pages call an order context.
module OrderContextText =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models


    /// What a page calls the context: the category's name for a nutrition order, the
    /// generic for a drug, nothing before a generic is chosen.
    let label (ctx: OrderContext) =
        match ctx.Category with
        | OrderCategory.Nutrition category -> NutritionCategory.label category
        | OrderCategory.Drug -> ctx.Filter.Generic |> Option.defaultValue ""
