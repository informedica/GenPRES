namespace Informedica.GenOrder.Lib


/// The standard orders of a rule set: each rule evaluated to its scenarios, with the texts and the advice a list
/// shows for each scenario.
module StandardOrder =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenForm.Lib

    module Parallel = Informedica.GenSolver.Lib.Utils.Parallel
    module DosePrint = Order.Orderable.Dose.Print


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
        let indicationAndGeneric (c: OrderContext) =
            c.Filter.Indication |> Option.defaultValue "", c.Filter.Generic |> Option.defaultValue ""

        match ctx |> PlanContext.create "" OrderCategory.Medication |> evaluate with
        | Ok(Evaluated pc) when pc.Context.Scenarios |> Array.isEmpty |> not ->
            pc.Context.Scenarios |> Array.map (fun sc -> pc.Context, sc), [||]
        | Ok(Evaluated pc) ->
            let ind, gen = indicationAndGeneric pc.Context

            match branches pc.Context with
            | [||] -> [||], [| StandardOrderWarning.NoScenarios(ind, gen) |]
            | _ when depth >= 4 -> [||], [| StandardOrderWarning.TooManyChoices(ind, gen) |]
            | bs ->
                let found = bs |> Array.map (scenarios evaluate (depth + 1))
                found |> Array.collect fst, found |> Array.collect snd
        | Ok(Refused(pc, refusal)) ->
            let ind, gen = indicationAndGeneric pc.Context
            [||], [| StandardOrderWarning.Refused(ind, gen, refusal) |]
        | Error messages ->
            let ind, gen = indicationAndGeneric ctx
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


    /// Returns the item of the scenario: the substance that is dosed, not an additional substance of the product.
    /// Returns None for a scenario dosed by volume.
    let itemOf (sc: OrderScenario) =
        sc.Order.Orderable.Components
        |> List.collect _.Items
        |> List.tryFind (fun i -> Some(i.Name |> Name.toString) = sc.Item)


    /// Returns the limits followed, for a once timed order, by the time and the rate of the order, with the
    /// precision of the prescription texts.
    let withTimeAndRate limits (ord: Order) =
        match ord.Schedule with
        | Schedule.OnceTimed time ->
            let time = time |> OrderVariable.toValueUnitText OrderVariable.Time.toOrdVar -1
            let rate =
                ord.Orderable.Dose.Rate
                |> OrderVariable.toValueUnitText OrderVariable.Rate.toOrdVar -1

            $"%s{limits} in %s{time}, %s{rate}" |> String.trim
        | _ -> limits


    /// Returns the advice of the scenario: the dose limits of the order as the prescription text prints them, per
    /// kg for an order per kg, followed for a once timed order by the time and the rate. The limits are those of the
    /// substance of the scenario, or of the orderable for a scenario dosed by volume.
    let advice (sc: OrderScenario) =
        let dose =
            match itemOf sc with
            | Some item -> item.Dose
            | None -> sc.Order.Orderable.Dose

        let limits =
            if sc.UseAdjust then
                dose |> DosePrint.doseQuantityAdjustConstraints 3
            else
                dose |> DosePrint.doseQuantityConstraints 3

        withTimeAndRate limits sc.Order


    /// Returns the bolus texts of a scenario. A scenario with a substance shows the substance; a scenario dosed by
    /// volume shows the orderable.
    let bolusTexts (sc: OrderScenario) =
        let orb = sc.Order.Orderable

        // The values have the precision of the prescription texts of the scenario.
        let orbQty =
            orb.Dose.Quantity
            |> OrderVariable.toValueUnitText OrderVariable.Quantity.toOrdVar -1
        let orbQtyAdj =
            orb.Dose.QuantityAdjust
            |> OrderVariable.toValueUnitText OrderVariable.QuantityAdjust.toOrdVar 2

        let substanceDose, preparation =
            match itemOf sc with
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

        StandardOrderTexts.Bolus
            {
                SubstanceDose = substanceDose
                Preparation = preparation
                Advice = advice sc
            }


    /// Returns the infusion texts of a scenario. A scenario with a substance shows the substance; a scenario dosed by
    /// volume shows the orderable, and the rate limits of its component, else of the orderable.
    let infusionTexts (sc: OrderScenario) =
        let orb = sc.Order.Orderable

        // The values have the precision of the prescription texts of the scenario.
        let rate = orb.Dose.Rate |> OrderVariable.toValueUnitText OrderVariable.Rate.toOrdVar -1

        // The rate, followed by the rate per kg of the dose for an order per kg, else by the rate of the dose.
        let withRate (dose: Dose) =
            let doseRate =
                if sc.UseAdjust then
                    dose.RateAdjust
                    |> OrderVariable.toValueUnitText OrderVariable.RateAdjust.toOrdVar 3
                else
                    dose.Rate |> OrderVariable.toValueUnitText OrderVariable.Rate.toOrdVar 3

            if doseRate = "" then rate else $"%s{rate} = %s{doseRate}"

        // The rate limits of the dose, per kg for an order per kg.
        let limits (dose: Dose) =
            if sc.UseAdjust then
                dose |> DosePrint.doseRateAdjustConstraints 3
            else
                dose |> DosePrint.doseRateConstraints 3

        match itemOf sc with
        | Some i ->
            let qty =
                i.OrderableQuantity
                |> OrderVariable.toValueUnitText OrderVariable.Quantity.toOrdVar 3
            let orbQty =
                orb.OrderableQuantity
                |> OrderVariable.toValueUnitText OrderVariable.Quantity.toOrdVar -1
            let diluent = sc.Diluent |> Option.defaultValue ""

            StandardOrderTexts.Infusion
                {
                    Quantity = qty
                    Solution = $"%s{orbQty} %s{diluent}" |> String.trim
                    Dose = withRate i.Dose
                    Advice = limits i.Dose
                }
        | None ->
            let advice =
                orb.Components
                |> List.tryHead
                |> Option.map (_.Dose >> limits)
                |> Option.filter (String.isNullOrWhiteSpace >> not)
                |> Option.defaultWith (fun () -> limits orb.Dose)

            StandardOrderTexts.Infusion
                {
                    Quantity = ""
                    Solution = sc.Name
                    // Without a substance, only the rate per kg of the orderable adds to the rate.
                    Dose = if sc.UseAdjust then withRate orb.Dose else rate
                    Advice = advice
                }


    /// Returns the standard order of a scenario of the context: the category is the indication without the prefix,
    /// and toTexts makes the texts.
    let ofScenario prefix toTexts (ctx: OrderContext, sc: OrderScenario) =
        {
            Context = narrowed ctx sc
            Category = categoryOf prefix sc.Indication
            Generic = ctx.Filter.Generic |> Option.defaultValue sc.Name
            Texts = toTexts sc
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


    /// Returns the standard orders of the rule sets of the kind for the patient, a standard order for each scenario,
    /// with a warning for each evaluation that gives no scenarios. The rules are evaluated in chunks of
    /// Parallel.totalWorders: the chunks run in parallel, the rules in a chunk one after the other.
    let standardOrders kind prefix toTexts start logger provider patientData (sets: RuleSet[]) =
        let found =
            sets
            |> Array.filter (fun s -> s.Kind = kind)
            |> Array.collect _.Rows
            |> Array.chunkBySize Parallel.totalWorders
            |> Array.Parallel.map (Array.map (contextOf patientData >> scenarios (evaluate start logger provider) 0))
            |> Array.concat

        found |> Array.collect fst |> Array.map (ofScenario prefix toTexts), found |> Array.collect snd


    /// Returns the standard orders of the emergency list for the patient, with the bolus texts.
    let emergencyList start logger provider patientData sets =
        standardOrders RuleSetKind.EmergencyList "Noodlijst " bolusTexts start logger provider patientData sets


    /// Returns the standard orders of the continuous medication list for the patient, with the infusion texts.
    let standardInfusions start logger provider patientData sets =
        standardOrders
            RuleSetKind.StandardInfusions
            "Standaard infusie "
            infusionTexts
            start
            logger
            provider
            patientData
            sets
