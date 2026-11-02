namespace Informedica.GenPRES.Client.Core.Policies

/// Decides what an order context in the order plan can change: only the frequency, the orderable
/// dose quantity and the orderable dose rate, with or without a change of the patient data.
module PlanContextPolicy =

    open Informedica.GenPRES.Shared.Types


    /// What the order dialog lets the user change.
    [<RequireQualifiedAccess>]
    type Editing =
        /// The prescribing workbench: every field, the argumentation and Reset.
        | Workbench
        /// An order context in the plan: the fields the plan context rule allows and the
        /// argumentation; no Reset, which re-solves from the rules and can change the rest.
        | PlanContext
        /// An order the current patient data could not be calculated on: nothing; the dialog sends
        /// no command.
        | Locked


    /// Whether the order a context contributes could not be calculated on the current patient data,
    /// so that it stands as it was: the order is locked.
    let locked (ctx: OrderContext) =
        Informedica.GenPRES.Shared.Models.OrderContext.contribution ctx
        |> Option.exists _.NotRecalculated


    /// Whether a field of an order context in the plan can change: the frequency, the orderable
    /// dose quantity and the orderable dose rate can; every other field cannot.
    let editable (field: QuantityModePolicy.Field) =
        match field with
        | QuantityModePolicy.Field.Frequency
        | QuantityModePolicy.Field.DoseQuantity
        | QuantityModePolicy.Field.DoseRate -> true
        | QuantityModePolicy.Field.ComponentQuantity
        | QuantityModePolicy.Field.Other -> false


    /// The values of an order variable as numbers with their unit, leaving out how they are
    /// rendered. The unit is its JSON, the serialized unit, which is the same in every language
    /// and tells 5 mg from 5 g where the unit group does not; the unit text stands in when there
    /// is no JSON.
    let values (ovar: OrderVariable) =
        ovar.Variable.Vals
        |> Option.map (fun vu ->
            let unit =
                if System.String.IsNullOrEmpty vu.Json then
                    vu.Unit
                else
                    vu.Json
            vu.Value |> Array.map snd, unit
        )


    /// The values of the editable order variables of the order a context contributes; nothing
    /// when it contributes no order.
    let editableValues (ctx: OrderContext) =
        Informedica.GenPRES.Shared.Models.OrderContext.contribution ctx
        |> Option.map (fun sc ->
            [
                sc.Order.Schedule.Frequency |> values
                sc.Order.Orderable.Dose.Quantity |> values
                sc.Order.Orderable.Dose.Rate |> values
            ]
        )


    /// What new patient data moves in the order a context contributes, since the given amounts stay as
    /// they are: the doses per kg or per m2 of the orderable, its components and their items. Nothing
    /// when it contributes no order.
    let adjustedValues (ctx: OrderContext) =
        let adjusted (dose: Dose) =
            [ dose.QuantityAdjust; dose.PerTimeAdjust; dose.RateAdjust; dose.TotalAdjust ]
            |> List.map values

        Informedica.GenPRES.Shared.Models.OrderContext.contribution ctx
        |> Option.map (fun sc ->
            let orderable = sc.Order.Orderable

            [
                orderable.Dose
                for cmp in orderable.Components do
                    cmp.Dose

                    for item in cmp.Items do
                        item.Dose
            ]
            |> List.collect adjusted
        )


    /// What tells the prescriber about the order a context contributes: the notification on each
    /// order variable, whether the rules no longer apply and whether the order could not be
    /// calculated. Nothing when it contributes no order.
    let notifications (ctx: OrderContext) =
        Informedica.GenPRES.Shared.Models.OrderContext.contribution ctx
        |> Option.map (fun sc ->
            sc.RulesNoLongerApply, sc.NotRecalculated, sc.Order |> ArgumentationPolicy.variables |> List.map _.Level
        )


    /// The id of the order a context contributes; nothing when it contributes no order. An order
    /// created anew from the rules, for another medication or rebuilt, gets a new id; a step keeps
    /// it.
    let orderId (ctx: OrderContext) =
        Informedica.GenPRES.Shared.Models.OrderContext.contribution ctx
        |> Option.map _.Order.Id


    /// Whether an order context in the plan changed against its opened or signed version: it holds
    /// another order, or one of the editable variables, a dose per kg or per m2, a notification or the
    /// argumentation differs.
    let changed (opened: OrderContext) (ctx: OrderContext) =
        orderId opened <> orderId ctx
        || editableValues opened <> editableValues ctx
        || adjustedValues opened <> adjustedValues ctx
        || notifications opened <> notifications ctx
        || opened.Argumentation <> ctx.Argumentation


    /// The kind of a field of the order dialog, by the key its changes are tracked under; the keys
    /// the plan context rule names map to their kind, every other key is Other.
    let fieldKind (key: string) =
        match key with
        | "frequency" -> QuantityModePolicy.Field.Frequency
        | "ordDoseQty" -> QuantityModePolicy.Field.DoseQuantity
        | "ordDoseRate" -> QuantityModePolicy.Field.DoseRate
        | "compOrdQty" -> QuantityModePolicy.Field.ComponentQuantity
        | _ -> QuantityModePolicy.Field.Other


    /// Whether a field of the order dialog, by its key, can be edited under the editing given.
    let canEdit (editing: Editing) (key: string) =
        match editing with
        | Editing.Workbench -> true
        | Editing.PlanContext -> key |> fieldKind |> editable
        | Editing.Locked -> false


    /// Whether the order dialog offers Reset: on the workbench alone.
    let resets (editing: Editing) =
        match editing with
        | Editing.Workbench -> true
        | Editing.PlanContext
        | Editing.Locked -> false


    /// Whether the order dialog lets the argumentation change and commits it: everywhere but a
    /// locked context.
    let argues (editing: Editing) =
        match editing with
        | Editing.Workbench
        | Editing.PlanContext -> true
        | Editing.Locked -> false


    /// The editing of the order dialog for the context of the plan with this id: locked when it could
    /// not be calculated on the current patient data, the plan context rule otherwise.
    let editingOf (plan: OrderPlan) (contextId: string) =
        match plan.OrderContexts |> Array.tryFind (fun c -> c.Id = contextId) with
        | Some ctx when locked ctx -> Editing.Locked
        | Some _
        | None -> Editing.PlanContext
