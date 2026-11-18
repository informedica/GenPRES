namespace Informedica.GenPRES.Client.Core.Policies

/// Decides what an order context in the order plan can change: only the frequency, the orderable
/// dose quantity and the orderable dose rate, with or without a change of the patient data.
module PlanContextPolicy =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Shared.Types


    /// Whether the intake slider of an order in the plan can set its dose as a share of the full intake.
    [<RequireQualifiedAccess>]
    type DoseShare =
        /// The order is a composed TPN, its dose count has a single value, and it is not locked.
        | Settable
        /// The slider shows the share but cannot change it, as for every order in the plan that is not a TPN.
        | Fixed


    /// What the order dialog lets the user change.
    [<RequireQualifiedAccess>]
    type Editing =
        /// The prescribing workbench: every field, the argumentation and Reset.
        | Workbench
        /// An order context in the plan: the editable fields, the argumentation and, when settable, the dose
        /// share. No Reset, because Reset solves again from the rules and can change the other fields.
        | PlanContext of DoseShare
        /// An order that could not be calculated on the current patient data, or whose group could not:
        /// nothing can change and the dialog sends no command.
        | Locked


    /// Whether the order of a context is locked, and why.
    [<RequireQualifiedAccess>]
    type Lock =
        /// The order and every order of its group can be calculated on the current patient data.
        | Unlocked
        /// The order itself could not be calculated on the current patient data, so it stays unchanged.
        | NotCalculated
        /// The order can be calculated, but another order of its group could not: its main order, an order
        /// linked to it, or an order linked to the same main order. The group stays unchanged.
        | Group


    /// Whether the order a context contributes could not be calculated on the current patient data.
    let notCalculated (ctx: OrderContext) =
        Informedica.GenPRES.Shared.Models.OrderContext.contribution ctx
        |> Option.exists _.NotRecalculated


    /// The contexts of the plan linked to the main order with this id.
    let linkedTo (plan: OrderPlan) main =
        plan.OrderContexts |> Array.filter (fun c -> c.LinkedTo = Some main)


    /// The contexts of the plan in the group of a context: its main order and every order linked to
    /// that main order. A context that is not linked is the main order of its group.
    let group (plan: OrderPlan) (ctx: OrderContext) =
        let main = ctx.LinkedTo |> Option.defaultValue ctx.Id

        Array.append (plan.OrderContexts |> Array.filter (fun c -> c.Id = main)) (linkedTo plan main)


    /// Returns whether the order of a context is locked in the given plan, and why.
    let lock plan (ctx: OrderContext) =
        if notCalculated ctx then
            Lock.NotCalculated
        elif group plan ctx |> Array.exists notCalculated then
            Lock.Group
        else
            Lock.Unlocked


    /// Returns the term that says why an order is locked, by itself or by its group; None when not locked.
    let lockTerm lock =
        match lock with
        | Lock.Unlocked -> None
        | Lock.NotCalculated -> Some Terms.``Plan Context Locked``
        | Lock.Group -> Some Terms.``Plan Context Group Locked``


    /// Whether the order of a context is locked in the given plan: the order or another order of its
    /// group could not be calculated on the current patient data.
    let locked plan ctx = lock plan ctx <> Lock.Unlocked


    /// Whether the intake slider can set the dose of the order of a context: only for a TPN whose dose the
    /// slider can set, and not when the order is locked, because the plan would refuse the change and the
    /// slider would show a value the order does not have.
    let canSetDoseShare plan (ctx: OrderContext) =
        ctx.Category = OrderCategory.Nutrition NutritionCategory.TPN
        && not (locked plan ctx)
        && Informedica.GenPRES.Shared.Models.OrderContext.contribution ctx
           |> Option.exists (_.Order >> IntakePolicy.canSetDoseQuantityPerc)


    /// Whether a field of an order context in the plan can change: the frequency, the orderable
    /// dose quantity and the orderable dose rate can; every other field cannot.
    let editable (field: QuantityModePolicy.Field) =
        match field with
        | QuantityModePolicy.Field.Frequency
        | QuantityModePolicy.Field.DoseQuantity
        | QuantityModePolicy.Field.DoseRate -> true
        | QuantityModePolicy.Field.ComponentQuantity
        | QuantityModePolicy.Field.Other -> false


    /// Returns the values of an order variable as numbers with their unit, without formatting. The unit is
    /// the serialized unit JSON, which is the same in every language and tells 5 mg from 5 g where the unit
    /// group does not. The unit text is used when there is no JSON.
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


    /// Returns the values of the editable order variables of the order of a context; None when the context
    /// has no order.
    let editableValues (ctx: OrderContext) =
        Informedica.GenPRES.Shared.Models.OrderContext.contribution ctx
        |> Option.map (fun sc ->
            [
                sc.Order.Schedule.Frequency |> values
                sc.Order.Orderable.Dose.Quantity |> values
                sc.Order.Orderable.Dose.Rate |> values
            ]
        )


    /// Returns the doses per kg or per m2 of the orderable, its components and their items, for the order of
    /// a context. New patient data changes these values while the given amounts stay the same. None when
    /// the context has no order.
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


    /// Returns what informs the prescriber about the order of a context: whether the rules no longer apply,
    /// whether the order could not be calculated, and the notification level of each order variable. None
    /// when the context has no order.
    let notifications (ctx: OrderContext) =
        Informedica.GenPRES.Shared.Models.OrderContext.contribution ctx
        |> Option.map (fun sc ->
            sc.RulesNoLongerApply, sc.NotRecalculated, sc.Order |> ArgumentationPolicy.variables |> List.map _.Level
        )


    /// Returns the order id of a context; None when the context has no order. An order created again from
    /// the rules, for another medication or rebuilt, gets a new id; a step keeps the id.
    let orderId (ctx: OrderContext) =
        Informedica.GenPRES.Shared.Models.OrderContext.contribution ctx
        |> Option.map _.Order.Id


    /// Whether an order context in the plan differs from its opened or signed version: it has another
    /// order, or an editable variable, a dose per kg or per m2, a notification or the argumentation differs.
    let changed (opened: OrderContext) (ctx: OrderContext) =
        orderId opened <> orderId ctx
        || editableValues opened <> editableValues ctx
        || adjustedValues opened <> adjustedValues ctx
        || notifications opened <> notifications ctx
        || opened.Argumentation <> ctx.Argumentation


    /// Returns the kind of an order dialog field from the key its changes are tracked under; an unknown key
    /// is Other.
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
        | Editing.PlanContext _ -> key |> fieldKind |> editable
        | Editing.Locked -> false


    /// Whether the order dialog offers Reset: on the workbench alone.
    let resets (editing: Editing) =
        match editing with
        | Editing.Workbench -> true
        | Editing.PlanContext _
        | Editing.Locked -> false


    /// Whether the order dialog lets the argumentation change and commits it: everywhere but a
    /// locked context.
    let argues (editing: Editing) =
        match editing with
        | Editing.Workbench
        | Editing.PlanContext _ -> true
        | Editing.Locked -> false


    /// Returns the dialog editing for the plan context with this id. Locked when its order or another order
    /// of its group could not be calculated on the current patient data; otherwise PlanContext, with a
    /// settable dose share when the intake slider can set the dose.
    let editingOf (plan: OrderPlan) (contextId: string) =
        match plan.OrderContexts |> Array.tryFind (fun c -> c.Id = contextId) with
        | Some ctx when locked plan ctx -> Editing.Locked
        | Some ctx when canSetDoseShare plan ctx -> Editing.PlanContext DoseShare.Settable
        | Some _
        | None -> Editing.PlanContext DoseShare.Fixed


    /// Whether the dose share can be set under the editing given: only for an order in the plan whose
    /// intake slider can set its dose.
    let setsDoseShare (editing: Editing) =
        match editing with
        | Editing.PlanContext DoseShare.Settable -> true
        | Editing.PlanContext DoseShare.Fixed
        | Editing.Workbench
        | Editing.Locked -> false
