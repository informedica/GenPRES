namespace Informedica.GenPRES.Client.Core.Policies

/// Decides what an order context in the order plan can change: only the frequency, the orderable
/// dose quantity and the orderable dose rate, with or without a change of the patient data.
module PlanContextPolicy =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Shared.Types


    /// Whether the intake slider of an order in the plan can set its dose as a share of the full intake.
    [<RequireQualifiedAccess>]
    type DoseShare =
        /// The order is a TPN, composed, its dose count holds one value, and it is not locked.
        | Settable
        /// The slider shows the share and takes no change; so for every order in the plan that is not a TPN.
        | Fixed


    /// What the order dialog lets the user change.
    [<RequireQualifiedAccess>]
    type Editing =
        /// The prescribing workbench: every field, the argumentation and Reset.
        | Workbench
        /// An order context in the plan: the fields the plan context rule allows, the argumentation and,
        /// when it is settable, the dose share; no Reset, which re-solves from the rules and can change the
        /// rest.
        | PlanContext of DoseShare
        /// An order the current patient data could not be calculated on: nothing; the dialog sends
        /// no command.
        | Locked


    /// Why the order a context contributes is locked, or that it is not.
    [<RequireQualifiedAccess>]
    type Lock =
        /// The order and every order of its group can be calculated on the current patient data.
        | Unlocked
        /// The order itself could not be calculated on the current patient data, so it stands as it was.
        | NotCalculated
        /// The order can be calculated, but another order of its group could not: its main order, an order
        /// linked to it, or an order linked to the same main order. The group stands as it was.
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


    /// Why the order a context contributes is locked over the plan given, or that it is not.
    let lock plan (ctx: OrderContext) =
        if notCalculated ctx then
            Lock.NotCalculated
        elif group plan ctx |> Array.exists notCalculated then
            Lock.Group
        else
            Lock.Unlocked


    /// The text of a lock: why an order is locked, by itself or by its group; none for an order not locked.
    let lockTerm lock =
        match lock with
        | Lock.Unlocked -> None
        | Lock.NotCalculated -> Some Terms.``Plan Context Locked``
        | Lock.Group -> Some Terms.``Plan Context Group Locked``


    /// Whether the order a context contributes is locked over the plan given: the order or another order
    /// of its group could not be calculated on the current patient data.
    let locked plan ctx = lock plan ctx <> Lock.Unlocked


    /// Whether the intake slider can set the dose of the order of a context: only for a TPN, and not when the
    /// order is locked, since the plan would not take the change and the slider would show a value the order
    /// does not hold.
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


    /// The editing of the order dialog for the context of the plan with this id: locked when it or another
    /// order of its group could not be calculated on the current patient data, the plan context rule
    /// otherwise, with the dose share settable when the intake slider can set the dose.
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
