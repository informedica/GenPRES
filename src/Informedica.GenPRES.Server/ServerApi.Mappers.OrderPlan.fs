namespace ServerApi

open Informedica.GenOrder.Lib
// Opened last, so an unqualified case name refers to the contract case.
open Informedica.GenPRES.Shared.Types


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
        | OrderPlanError.UnknownCategory name -> $"RuleSets: %s{name} is not a nutrition category"
        | OrderPlanError.NoRuleSet category -> $"RuleSets: no rule set for %s{category |> NutritionRuleSet.sheetName}"
        | OrderPlanError.BrokenLink e -> e |> OrderContextMapper.linkWords
        | OrderPlanError.OrderRefused(generic, e) -> $"%s{generic}: %s{words e}"
