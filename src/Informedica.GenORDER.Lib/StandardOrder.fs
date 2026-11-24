namespace Informedica.GenOrder.Lib


/// The standard orders of a rule set: each rule evaluated to its scenarios, with the texts a list shows for each
/// scenario.
module StandardOrder =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenForm.Lib

    module Parallel = Informedica.GenSolver.Lib.Utils.Parallel


    /// Returns the options of the first open choice of the filter, in the order generic, route, dose type, form,
    /// each as the context with that option chosen. A choice is open when nothing is chosen and the list has more
    /// than one option. Empty when no choice is open.
    let branches (ctx: OrderContext) =
        let f = ctx.Filter
        let withFilter filter = { ctx with Filter = filter }

        match f with
        | {
              Generic = None
              Generics = gs
          } when gs.Length > 1 -> gs |> Array.map (fun g -> withFilter { f with Generic = Some g })
        | {
              Route = None
              Routes = rs
          } when rs.Length > 1 -> rs |> Array.map (fun r -> withFilter { f with Route = Some r })
        | {
              DoseType = None
              DoseTypes = ds
          } when ds.Length > 1 -> ds |> Array.map (fun d -> withFilter { f with DoseType = Some d })
        | {
              Form = None
              Forms = xs
          } when xs.Length > 1 -> xs |> Array.map (fun x -> withFilter { f with Form = Some x })
        | _ -> [||]


    /// Evaluates the plan context as a medication order: with the dose rules of the context and without a rule set.
    /// Returns the messages of a failed evaluation, or the message of an exception, as one text. An exception is
    /// also logged as an error, with its stack trace.
    let evaluate start logger provider (pc: PlanContext) =
        try
            pc
            |> PlanContext.evaluateSettled
                start
                logger
                (provider |> OrderContext.withDoseRulesOf pc.Context)
                None
                OrderContext.UpdateOrderContext
            |> Result.mapError (List.map (fun m -> OrderLogging.formatOrderMessage m) >> String.concat "\n")
        with e ->
            $"errored:\n%s{string e}"
            |> Events.OrderScenario
            |> OrderLogging.logOrderEventError logger
            Error e.Message


    /// Evaluates the context and returns the evaluated context with each of its scenarios, with a warning for each
    /// evaluation that gives no scenarios. A context without scenarios is evaluated again for each option of the
    /// first open choice, at most four choices deep.
    let rec scenarios evaluate depth (ctx: OrderContext) =
        let names (c: OrderContext) =
            c.Filter.Indication |> Option.defaultValue "", c.Filter.Generic |> Option.defaultValue ""

        match ctx |> PlanContext.create "" OrderCategory.Medication |> evaluate with
        | Ok(Evaluated pc) when pc.Context.Scenarios |> Array.isEmpty |> not ->
            pc.Context.Scenarios |> Array.map (fun sc -> pc.Context, sc), [||]
        | Ok(Evaluated pc) ->
            let ind, gen = names pc.Context

            match branches pc.Context with
            | [||] -> [||], [| StandardOrderWarning.NoScenarios(ind, gen) |]
            | _ when depth >= 4 -> [||], [| StandardOrderWarning.TooManyChoices(ind, gen) |]
            | bs ->
                let found = bs |> Array.map (scenarios evaluate (depth + 1))
                found |> Array.collect fst, found |> Array.collect snd
        | Ok(Refused(pc, refusal)) ->
            let ind, gen = names pc.Context
            [||], [| StandardOrderWarning.Refused(ind, gen, refusal) |]
        | Error messages ->
            let ind, gen = names ctx
            [||], [| StandardOrderWarning.Failed(ind, gen, messages) |]


    /// Returns the indication without the prefix, such as "Noodlijst ", or the indication when it does not start
    /// with the prefix.
    let categoryOf (prefix: string) (indication: string) =
        if indication.StartsWith prefix then
            indication.Substring prefix.Length
        else
            indication


    /// Returns the quantity, followed by the quantity per kg in brackets when the order is per kg and the quantity
    /// per kg has a text.
    let withAdjust useAdjust qty adj = if useAdjust && adj <> "" then $"%s{qty} (%s{adj})" else qty


    /// Returns the context narrowed to the scenario: the form of the scenario is chosen and the scenario is the only
    /// scenario.
    let narrowed (ctx: OrderContext) (sc: OrderScenario) =
        { ctx with
            Filter = { ctx.Filter with Form = Some sc.Form }
            Scenarios = [| sc |]
        }


    /// Returns the standard order of a scenario of the context. A scenario with a substance shows the substance; a
    /// scenario dosed by volume shows the orderable.
    let ofScenario (ctx: OrderContext, sc: OrderScenario) =
        let orb = sc.Order.Orderable

        // The values have the precision of the prescription texts of the scenario.
        let orbQty =
            orb.Dose.Quantity
            |> OrderVariable.toValueUnitText OrderVariable.Quantity.toOrdVar -1
        let orbQtyAdj =
            orb.Dose.QuantityAdjust
            |> OrderVariable.toValueUnitText OrderVariable.QuantityAdjust.toOrdVar 2

        // The item of the scenario: the substance that is dosed, not an additional substance of the product.
        let item =
            orb.Components
            |> List.collect _.Items
            |> List.tryFind (fun i -> Some(i.Name |> Name.toString) = sc.Item)

        let substanceDose, preparation =
            match item with
            | Some i ->
                let qty =
                    i.Dose.Quantity
                    |> OrderVariable.toValueUnitText OrderVariable.Quantity.toOrdVar 3
                let qtyAdj =
                    i.Dose.QuantityAdjust
                    |> OrderVariable.toValueUnitText OrderVariable.QuantityAdjust.toOrdVar 3
                let cnc =
                    i.OrderableConcentration
                    |> OrderVariable.toValueUnitText OrderVariable.Concentration.toOrdVar 3

                withAdjust sc.UseAdjust qty qtyAdj,
                if orbQty = "" || cnc = "" then
                    ""
                else
                    $"%s{orbQty} van %s{cnc}"
            | None -> withAdjust sc.UseAdjust orbQty orbQtyAdj, $"%s{orbQty} %s{sc.Name}"

        {
            Context = narrowed ctx sc
            Category = categoryOf "Noodlijst " sc.Indication
            Generic = ctx.Filter.Generic |> Option.defaultValue sc.Name
            SubstanceDose = substanceDose
            Preparation = preparation
        }


    /// Returns the order context of a rule for the patient: the indication and the generic of the rule.
    let contextOf patientData (rule: Types.Data.RuleSetData) =
        {
            Filter =
                { OrderContext.emptyFilter with
                    Indication = Some rule.Indication
                    Generic = if rule.Generic = "" then None else Some rule.Generic
                }
            PatientData = patientData
            Scenarios = [||]
            Argumentation = None
        }


    /// Returns the standard orders of the rule sets of the kind EmergencyList for the patient, a standard order for
    /// each scenario, with a warning for each evaluation that gives no scenarios. The rules are evaluated in chunks
    /// of Parallel.totalWorders: the chunks run in parallel, the rules in a chunk one after the other.
    let emergencyList start logger provider patientData (sets: RuleSet[]) =
        let found =
            sets
            |> Array.filter (fun s -> s.Kind = RuleSetKind.EmergencyList)
            |> Array.collect _.Rows
            |> Array.chunkBySize Parallel.totalWorders
            |> Array.Parallel.map (Array.map (contextOf patientData >> scenarios (evaluate start logger provider) 0))
            |> Array.concat

        found |> Array.collect fst |> Array.map ofScenario, found |> Array.collect snd
