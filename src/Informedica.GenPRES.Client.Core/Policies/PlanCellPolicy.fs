namespace Informedica.GenPRES.Client.Core.Policies

/// Decides which cells of the order plan table the user can step, and which order variable a
/// cell steps: a cell opens a quantity field only for a variable the plan context rule lets
/// change, of a context that is not locked, whose value can be stepped now.
module PlanCellPolicy =

    open Informedica.GenPRES.Shared.Types


    /// A column of the order plan table.
    [<RequireQualifiedAccess>]
    type Column =
        /// The medication's name.
        | Medication
        /// The route.
        | Route
        /// The frequency, or the dose rate of a continuous order.
        | Frequency
        /// The item dose quantity, or the item orderable quantity of a continuous order.
        | Quantity
        /// The orderable dose quantity, or the orderable quantity of a continuous order.
        | Solution
        /// The adjusted dose.
        | Dose


    /// The field and the order variable a column shows that has a step command, by the order's
    /// schedule; nothing for a column whose variable has none. The frequency column shows the dose
    /// rate of a continuous order, and the solution column its orderable quantity, which has no
    /// step command.
    let fieldOf (column: Column) (ord: Order) : (QuantityModePolicy.Field * OrderVariable) option =
        let schedule = ord.Schedule

        match column with
        | Column.Frequency when schedule.IsDiscontinuous || schedule.IsTimed ->
            Some(QuantityModePolicy.Field.Frequency, schedule.Frequency)
        | Column.Frequency when schedule.IsContinuous ->
            Some(QuantityModePolicy.Field.DoseRate, ord.Orderable.Dose.Rate)
        | Column.Solution when
            schedule.IsDiscontinuous
            || schedule.IsTimed
            || schedule.IsOnce
            || schedule.IsOnceTimed
            ->
            Some(QuantityModePolicy.Field.DoseQuantity, ord.Orderable.Dose.Quantity)
        | Column.Frequency
        | Column.Solution
        | Column.Medication
        | Column.Route
        | Column.Quantity
        | Column.Dose -> None


    /// The field and the order variable a cell of a context in the plan steps; nothing when the
    /// cell keeps its text.
    let stepable (plan: OrderPlan) (ctx: OrderContext) (column: Column) =
        if PlanContextPolicy.locked plan ctx then
            None
        else
            Informedica.GenPRES.Shared.Models.OrderContext.contribution ctx
            |> Option.bind (fun sc ->
                fieldOf column sc.Order
                |> Option.filter (fun (field, ovar) ->
                    PlanContextPolicy.editable field
                    && QuantityModePolicy.decideFor field sc.Order ovar = QuantityModePolicy.Mode.Stepable
                )
            )
