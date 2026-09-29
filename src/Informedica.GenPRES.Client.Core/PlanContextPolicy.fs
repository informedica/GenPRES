/// Decides what an order context in the order plan can change and when: only the frequency, the
/// orderable dose quantity and the orderable dose rate, and only while the patient data it was
/// calculated with match the plan's.
module PlanContextPolicy

open Shared.Types


/// Whether a field of an order context in the plan can change: the frequency, the orderable
/// dose quantity and the orderable dose rate can; every other field cannot.
let editable (field: QuantityModePolicy.Field) =
    match field with
    | QuantityModePolicy.Field.Frequency
    | QuantityModePolicy.Field.DoseQuantity
    | QuantityModePolicy.Field.DoseRate -> true
    | QuantityModePolicy.Field.ComponentQuantity
    | QuantityModePolicy.Field.Other -> false


/// The patient data the orders rest on: the age is blanked, since it changes by itself as
/// time passes, and so are the P3 and P97 estimates. The estimated weight and height are
/// blanked only when a measured value exists; without one, the estimate is what the doses
/// were calculated with, so a corrected age that changes it still counts.
let entered (p: Patient) =
    { p with
        Age = None
        Weight =
            { p.Weight with
                EstimatedP3 = None
                Estimated =
                    if p.Weight.Measured.IsSome then
                        None
                    else
                        p.Weight.Estimated
                EstimatedP97 = None
            }
        Height =
            { p.Height with
                EstimatedP3 = None
                Estimated =
                    if p.Height.Measured.IsSome then
                        None
                    else
                        p.Height.Estimated
                EstimatedP97 = None
            }
    }


/// Whether the patient data an order context was calculated with match the plan's patient
/// data: the entered data and the estimates the doses rest on, the age aside.
let matches (plan: OrderPlan) (ctx: OrderContext) = entered ctx.Patient = entered plan.Patient


/// Whether an order context in the plan is locked: its patient data do not match the plan's.
let locked (plan: OrderPlan) (ctx: OrderContext) = matches plan ctx |> not


/// The values of an order variable as numbers with their unit group, leaving out how they
/// are rendered: the unit text, the language and the JSON.
let values (ovar: OrderVariable) =
    ovar.Variable.Vals |> Option.map (fun vu -> vu.Value |> Array.map snd, vu.Group)


/// The values of the editable order variables of the order a context contributes; nothing
/// when it contributes no order.
let editableValues (ctx: OrderContext) =
    Shared.Models.OrderContext.contribution ctx
    |> Option.map (fun sc ->
        [
            sc.Order.Schedule.Frequency |> values
            sc.Order.Orderable.Dose.Quantity |> values
            sc.Order.Orderable.Dose.Rate |> values
        ]
    )


/// Whether an order context in the plan changed against its opened or signed version: one of
/// the editable variables or the argumentation differs.
let changed (opened: OrderContext) (ctx: OrderContext) =
    editableValues opened <> editableValues ctx
    || opened.Argumentation <> ctx.Argumentation
