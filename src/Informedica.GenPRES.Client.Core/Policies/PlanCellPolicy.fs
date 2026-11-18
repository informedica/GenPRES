namespace Informedica.GenPRES.Client.Core.Policies

/// Decides which cells of the order plan table the user can step, and which order variable a cell
/// steps. A cell opens a quantity field only when the plan context policy lets the variable change,
/// the context is not locked, and the value can be stepped.
module PlanCellPolicy =

    open Informedica.GenPRES.Shared.Types


    /// A column of the order plan table.
    [<RequireQualifiedAccess>]
    type Column =
        /// The medication name.
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


    /// Returns the field and order variable with a step command that a column shows, by the schedule of
    /// the order; None when the column has no such variable. For a continuous order the frequency column
    /// shows the dose rate, and the solution column shows the orderable quantity, which has no step command.
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


    /// Returns the field and order variable that a cell of a context steps in the given plan; None when the
    /// cell shows only text.
    let stepable plan (ctx: OrderContext) (column: Column) =
        Informedica.GenPRES.Shared.Models.OrderContext.contribution ctx
        |> Option.filter (fun _ -> not (PlanContextPolicy.locked plan ctx))
        |> Option.bind (fun sc ->
            fieldOf column sc.Order
            |> Option.filter (fun (field, ovar) ->
                PlanContextPolicy.editable field
                && QuantityModePolicy.decideFor field sc.Order ovar = QuantityModePolicy.Mode.Stepable
            )
        )
