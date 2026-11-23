namespace ServerApi

open Informedica.GenOrder.Lib
// Opened last, so an unqualified case name refers to the contract case.
open Informedica.GenPRES.Shared.Types


/// Maps a nutrition category to its name in the "RuleSets" sheet and back.
module NutritionCategoryMapper =

    // Opened here, so that the case names refer to the GenORDER category and not the contract category.
    open Informedica.GenOrder.Lib.Types

    /// The name of a nutrition category in the "RuleSets" sheet.
    let toName =
        function
        | NutritionCategory.EnteralFeeding -> "EnteralFeeding"
        | NutritionCategory.EnteralSupplement -> "EnteralSupplement"
        | NutritionCategory.TPN -> "TPN"
        | NutritionCategory.Lipid -> "Lipid"
        | NutritionCategory.ElectrolyteGlucose -> "ElectrolyteGlucose"


    /// Returns the nutrition category of a name in the "RuleSets" sheet, or None for another name.
    let ofName name =
        NutritionRuleSet.categories |> Array.tryFind (toName >> (=) name)


/// Maps an order plan of the contract to the order plan Dto of GenORDER and back.
module OrderPlanMapper =

    /// Maps an order plan to the Dto.
    let ofModel (plan: OrderPlan) : OrderPlan.Dto.Dto =
        {
            PatientData = plan.PatientData |> Patient.ofModel
            Filtered = plan.Filtered
            Contexts = plan.OrderContexts |> Array.map OrderContextMapper.ofModel
            Totals = plan.Totals |> OrderContextMapper.totals
        }


    /// Maps an order plan Dto to an order plan with the demo flag of the server.
    let toModel (demo: bool) (dto: OrderPlan.Dto.Dto) : OrderPlan =
        {
            PatientData = dto.PatientData |> Patient.toModel
            Filtered = dto.Filtered
            OrderContexts = dto.Contexts |> Array.map (OrderContextMapper.toModel demo)
            Totals = dto.Totals |> OrderContextMapper.totalsBack
        }


    /// Returns the message for an order plan error.
    let rec words =
        function
        | OrderPlanError.NoSuchContext id -> $"The plan holds no context %s{id}"
        | OrderPlanError.NotNarrowed n -> $"Choose one order first: %i{n} candidates remain"
        | OrderPlanError.OrderHeld _ -> "The plan already holds this order"
        | OrderPlanError.NoRuleSet category ->
            $"RuleSets: no rule set for %s{category |> NutritionCategoryMapper.toName}"
        | OrderPlanError.BrokenLink e -> e |> OrderContextMapper.linkWords
        | OrderPlanError.OrderRefused(generic, e) -> $"%s{generic}: %s{words e}"
