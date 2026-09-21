namespace ServerApi

open Informedica.GenOrder.Lib
// after the domain, so that the contract model's cases win unqualified
open Shared.Types


/// The order plan mapper: the contract model's order plan to the domain's Dto and back, its
/// contexts through the order context mapper.
module OrderPlanMapper =

    module Category = OrderContextMapper.Category


    /// A nutrition category from the wire, for a context the plan is asked to make.
    let nutritionCategory (c: NutritionCategory) =
        match c |> OrderCategory.Nutrition |> Category.toDomain with
        | Informedica.GenOrder.Lib.Types.OrderCategory.Nutrition c -> c
        | Informedica.GenOrder.Lib.Types.OrderCategory.Drug ->
            invalidOp "a nutrition category maps to a nutrition category"


    /// Total: the contract model's order plan as the domain's Dto, nothing dropped.
    let ofModel (plan: OrderPlan) : OrderPlan.Dto.Dto =
        {
            Patient = plan.Patient |> Patient.ofModel
            Filtered = plan.Filtered
            Contexts = plan.OrderContexts |> Array.map OrderContextMapper.ofModel
            Totals = plan.Totals |> OrderContextMapper.totals
        }


    /// The Dto as the contract model's order plan, from the Dto alone; the demo flag on every
    /// context is the server's.
    let toModel (demo: bool) (dto: OrderPlan.Dto.Dto) : OrderPlan =
        {
            Patient = dto.Patient |> Patient.toModel
            Filtered = dto.Filtered
            OrderContexts = dto.Contexts |> Array.map (OrderContextMapper.toModel demo)
            Totals = dto.Totals |> OrderContextMapper.totalsBack
        }


    /// The server's words for a change the plan refuses; the category's label from the rule
    /// sets the server owns.
    let words (ruleSets: NutritionRuleSet[]) =
        function
        | OrderPlanError.NoSuchContext id -> $"The plan holds no context %s{id}"
        | OrderPlanError.NotNarrowed n -> $"The workbench holds %i{n} candidates, not one order"
        | OrderPlanError.OrderHeld _ -> "The plan already holds this order"
        | OrderPlanError.SupplementNeedsFeeding -> "A supplement needs a feeding in the plan"
        | OrderPlanError.CategoryHeld category ->
            let label =
                ruleSets
                |> NutritionRuleSet.tryFind category
                |> Option.map _.Label
                |> Option.defaultValue $"%A{category}"

            $"The plan already holds a %s{label} context"
