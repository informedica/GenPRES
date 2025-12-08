#load "load.fsx"

namespace Informedica.GenOrder.Lib.OrderState

open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib
open Informedica.GenOrder.Lib
open Informedica.GenOrder.Lib.Types


/// Represents the value state of an OrderVariable
type VariableValueState =
    /// Has not both a Lower and Upper Bound
    | UnBounded
    /// Has min/max bounds but no specific increment
    | Bounded
    /// Has min/max bounds with increment (can generate discrete values)
    | CanHaveValues
    /// Has discrete set of multiple values to choose from
    | HasValues
    /// Single value determined (solved)
    | IsSolved

/// Represents the constraint state of an OrderVariable
type VariableConstraintState =
    /// Has meaningful constraints (not just empty/non-zero-positive)
    | HasConstraints
    /// No effective constraints
    | NoConstraints

/// Combined state of an OrderVariable
type OrderVariableState = {
    ValueState: VariableValueState
    ConstraintState: VariableConstraintState
}

/// Categories of OrderVariables by their role in the order
type VariableRole =
    // Prescription variables
    | PrescriptionFrequency
    | PrescriptionTime
    | OrderDuration
    | OrderAdjust

    // Dose variables
    | OrderableDoseQuantity
    | OrderableDosePerTime
    | OrderableDoseRate
    | OrderableDoseTotal
    | OrderableDoseQuantityAdjust
    | OrderableDosePerTimeAdjust
    | OrderableDoseRateAdjust
    | OrderableDoseTotalAdjust

    // Orderable variables
    | OrderableQuantity
    | OrderQuantity
    | OrderCount
    | DoseCount

    // Component variables
    | ComponentQuantity
    | ComponentOrderableQuantity
    | ComponentOrderableCount
    | ComponentOrderQuantity
    | ComponentOrderCount
    | ComponentOrderableConcentration
    | ComponentDoseQuantity
    | ComponentDosePerTime
    | ComponentDoseRate
    | ComponentDoseTotal
    | ComponentDoseQuantityAdjust
    | ComponentDosePerTimeAdjust
    | ComponentDoseRateAdjust
    | ComponentDoseTotalAdjust

    // Item variables
    | ItemComponentQuantity
    | ItemOrderableQuantity
    | ItemComponentConcentration
    | ItemOrderableConcentration
    | ItemDoseQuantity
    | ItemDosePerTime
    | ItemDoseRate
    | ItemDoseTotal
    | ItemDoseQuantityAdjust
    | ItemDosePerTimeAdjust
    | ItemDoseRateAdjust
    | ItemDoseTotalAdjust

/// Order parts (integral components, not sequential phases)
type OrderPart =
    | Prescription  // Dosing information
    | Preparation   // Component/concentration calculations
    | Administration // Final quantities and rates

/// Corrected Order state progression
type OrderState =
    /// Order has no constraints applied
    | NotConstraint
    /// Constraints have been applied to OrderVariables
    | ConstraintsApplied
    /// Min/Max values have been calculated and order is ready to show
    | MinMaxCalculated
    /// Discrete values have been calculated, user can make selections
    | ValuesCalculated
    /// All OrderVariables are solved (single values)
    | Solved
    /// One or more OrderVariables have been cleared, requiring recalculation
    | Cleared

/// User selection action - can only pick ONE option at a time
type UserSelection = {
    VariableRole: VariableRole
    SelectedValue: ValueUnit
}

/// State transition operations that follow the corrected flow
type StateTransition =
    | ApplyConstraints          // NotConstraint → ConstraintsApplied
    | CalculateMinMax          // ConstraintsApplied → MinMaxCalculated
    | MaterializeValues        // MinMaxCalculated → ValuesCalculated
    | MakeSelection of UserSelection  // ValuesCalculated → (ValuesCalculated | Solved)
    | ClearVariable of VariableRole   // Any state → Cleared
    | RecalculateFromCleared   // Cleared → ValuesCalculated

/// Represents a selectable option for the user
type SelectableOption = {
    VariableRole: VariableRole
    AvailableValues: ValueUnit list
    CurrentState: OrderVariableState
}

/// Result of order processing
type OrderProcessingResult = {
    Order: Order
    CurrentState: OrderState
    SelectableOptions: SelectableOption list
    NextTransition: StateTransition option
    CanProgress: bool
    IsReadyForUser: bool  // True when state is MinMaxCalculated
}

/// Classification functions
module OrderVariableClassification =

    /// Determine the value state of an OrderVariable
    let classifyValueState (ovar: OrderVariable) : VariableValueState =
        if OrderVariable.isEmpty ovar then UnBounded
        elif OrderVariable.isSolved ovar then IsSolved
        elif OrderVariable.hasValues ovar then HasValues
        elif ovar.Variable |> Variable.isMinIncrMax then CanHaveValues
        elif ovar.Variable |> Variable.isMinMax then Bounded
        else UnBounded

    /// Determine the constraint state of an OrderVariable
    let classifyConstraintState (ovar: OrderVariable) : VariableConstraintState =
        if OrderVariable.hasConstraints ovar then HasConstraints
        else NoConstraints

    /// Get combined state of an OrderVariable
    let classifyOrderVariable (ovar: OrderVariable) : OrderVariableState = {
        ValueState = classifyValueState ovar
        ConstraintState = classifyConstraintState ovar
    }

/// Variable relevance by prescription type and order part
module VariableRelevanceTable =

    type PrescriptionVariableRelevance = {
        PrescriptionType: string
        OrderPart: OrderPart
        RelevantVariables: VariableRole list
        CriticalVariables: VariableRole list
    }

    let prescriptionRelevance : PrescriptionVariableRelevance list = [
        // Once prescription - Prescription part
        {
            PrescriptionType = "Once"
            OrderPart = Prescription
            RelevantVariables = [
                OrderableDoseQuantity; OrderableDoseQuantityAdjust
                ComponentDoseQuantity; ComponentDoseQuantityAdjust
                ItemDoseQuantity; ItemDoseQuantityAdjust
            ]
            CriticalVariables = [OrderableDoseQuantity]
        }

        // Once prescription - Preparation part
        {
            PrescriptionType = "Once"
            OrderPart = Preparation
            RelevantVariables = [
                OrderableQuantity; ComponentOrderableQuantity; ComponentOrderableCount
                ItemOrderableQuantity; ItemComponentConcentration; ItemOrderableConcentration
            ]
            CriticalVariables = [OrderableQuantity]
        }

        // Once prescription - Administration part
        {
            PrescriptionType = "Once"
            OrderPart = Administration
            RelevantVariables = [
                OrderableQuantity; ComponentOrderableQuantity
                ItemOrderableQuantity
            ]
            CriticalVariables = [OrderableQuantity]
        }

        // Continuous prescription - Prescription part
        {
            PrescriptionType = "Continuous"
            OrderPart = Prescription
            RelevantVariables = [
                PrescriptionTime; OrderableDoseRate; OrderableDoseRateAdjust
                ComponentDoseRate; ComponentDoseRateAdjust
                ItemDoseRate; ItemDoseRateAdjust
            ]
            CriticalVariables = [OrderableDoseRate]
        }

        // Add other prescription types as needed...
    ]

    /// Get relevant variables for a prescription type and order part
    let getRelevantVariables (prescriptionType: string) (part: OrderPart) : VariableRole list =
        prescriptionRelevance
        |> List.filter (fun r -> r.PrescriptionType = prescriptionType && r.OrderPart = part)
        |> List.collect (fun r -> r.RelevantVariables)
        |> List.distinct

/// Order state classification following the corrected flow
module OrderStateClassification =

    /// Classify the overall state of an Order based on the corrected flow
    let classifyOrderState (order: Order) : OrderState =
        if Order.isCleared order then Cleared
        elif Order.isSolved order then Solved
        elif Order.hasValues order then ValuesCalculated
        elif Order.hasConstraints order then
            // Need to distinguish between ConstraintsApplied and MinMaxCalculated
            // This would require checking if min/max calculations have been done
            MinMaxCalculated  // Simplified - in practice would check calculation state
        else NotConstraint

    /// Determine valid transitions from current state
    let possibleTransitions (orderState: OrderState) : StateTransition list =
        match orderState with
        | NotConstraint -> [ApplyConstraints]
        | ConstraintsApplied -> [CalculateMinMax]
        | MinMaxCalculated -> [MaterializeValues]
        | ValuesCalculated -> [] // User must make selection or clear variable
        | Solved -> [] // User can only clear variables
        | Cleared -> [RecalculateFromCleared]

    /// Determine the next automatic transition (no user input required)
    let nextAutomaticTransition (order: Order) : StateTransition option =
        let state = classifyOrderState order
        match state with
        | NotConstraint -> Some ApplyConstraints
        | ConstraintsApplied -> Some CalculateMinMax
        | MinMaxCalculated -> Some MaterializeValues
        | _ -> None

/// Helper functions for state processing
module StateHelpers =

    /// Map OrderVariable to its role based on variable name patterns
    let identifyVariableRole (ovar: OrderVariable) : VariableRole option =
        let name = ovar |> OrderVariable.getName |> Name.toString
        match name with
        | n when n.Contains("pres.freq") -> Some PrescriptionFrequency
        | n when n.Contains("pres.time") -> Some PrescriptionTime
        | n when n.Contains("ord.adj") -> Some OrderAdjust
        | n when n.Contains("ord.time") -> Some OrderDuration
        | n when n.Contains("orb.dos.qty") && n.Contains("adj") -> Some OrderableDoseQuantityAdjust
        | n when n.Contains("orb.dos.qty") -> Some OrderableDoseQuantity
        | n when n.Contains("orb.dos.ptm") && n.Contains("adj") -> Some OrderableDosePerTimeAdjust
        | n when n.Contains("orb.dos.ptm") -> Some OrderableDosePerTime
        | n when n.Contains("orb.dos.rte") && n.Contains("adj") -> Some OrderableDoseRateAdjust
        | n when n.Contains("orb.dos.rte") -> Some OrderableDoseRate
        | n when n.Contains("orb.qty") -> Some OrderableQuantity
        | n when n.Contains("cmp.orb.qty") -> Some ComponentOrderableQuantity
        | n when n.Contains("cmp.orb.cnt") -> Some ComponentOrderableCount
        | n when n.Contains("cmp.orb.cnc") -> Some ComponentOrderableConcentration
        | n when n.Contains("itm.orb.qty") -> Some ItemOrderableQuantity
        | n when n.Contains("itm.orb.cnc") -> Some ItemOrderableConcentration
        | n when n.Contains("itm.cmp.cnc") -> Some ItemComponentConcentration
        | n when n.Contains("itm.dos.qty") && n.Contains("adj") -> Some ItemDoseQuantityAdjust
        | n when n.Contains("itm.dos.qty") -> Some ItemDoseQuantity
        | n when n.Contains("itm.dos.rte") && n.Contains("adj") -> Some ItemDoseRateAdjust
        | n when n.Contains("itm.dos.rte") -> Some ItemDoseRate
        | _ -> None

    /// Extract selectable options from an order in ValuesCalculated state
    let extractSelectableOptions (order: Order) : SelectableOption list =
        order
        |> Order.toOrdVars
        |> List.choose (fun ovar ->
            identifyVariableRole ovar
            |> Option.bind (fun role ->
                let state = OrderVariableClassification.classifyOrderVariable ovar
                if state.ValueState = HasValues then
                    // Extract available values from the OrderVariable
                    ovar
                    |> OrderVariable.getValSetValueUnit
                    |> Option.map (fun vu ->
                        {
                            VariableRole = role
                            AvailableValues = [vu] // Simplified - would extract all values
                            CurrentState = state
                        })
                else None
            ))

    /// Process a user selection by applying it to the order
    let processUserSelection (order: Order) (selection: UserSelection) : Order =
        // This would apply the selected value to the specific OrderVariable
        // and then trigger a complete recalculation of all OrderVariables
        // Implementation would depend on the specific OrderVariable type
        order // Placeholder - actual implementation needed

    /// Check if order is ready for user interaction
    let isReadyForUser (order: Order) : bool =
        let state = OrderStateClassification.classifyOrderState order
        state = MinMaxCalculated

    /// Process order through automatic state transitions until user input needed
    let processToUserReady (order: Order) : OrderProcessingResult =
        let rec processStep currentOrder =
            let currentState = OrderStateClassification.classifyOrderState currentOrder
            match OrderStateClassification.nextAutomaticTransition currentOrder with
            | Some ApplyConstraints ->
                let newOrder = currentOrder |> Order.applyConstraints
                processStep newOrder
            | Some CalculateMinMax ->
                // This would call the appropriate min/max calculation
                // For now, assume it results in MinMaxCalculated state
                let newOrder = currentOrder // Placeholder
                processStep newOrder
            | Some MaterializeValues ->
                // This would materialize discrete values
                let newOrder = currentOrder // Placeholder
                processStep newOrder
            | None ->
                // No more automatic transitions possible
                {
                    Order = currentOrder
                    CurrentState = currentState
                    SelectableOptions =
                        if currentState = ValuesCalculated then
                            extractSelectableOptions currentOrder
                        else []
                    NextTransition = None
                    CanProgress = false
                    IsReadyForUser = isReadyForUser currentOrder
                }

        processStep order

/// Main processing interface following the corrected model
module OrderProcessor =

    /// Process an order following the state machine model
    let processOrder (order: Order) : OrderProcessingResult =
        StateHelpers.processToUserReady order

    /// Apply a user selection and continue processing
    let applyUserSelection (order: Order) (selection: UserSelection) : OrderProcessingResult =
        let updatedOrder = StateHelpers.processUserSelection order selection
        StateHelpers.processToUserReady updatedOrder

    /// Clear a variable and return to appropriate state
    let clearVariable (order: Order) (variableRole: VariableRole) : OrderProcessingResult =
        // This would clear the specified variable and recalculate
        let clearedOrder = order // Placeholder implementation
        StateHelpers.processToUserReady clearedOrder

/// Usage example showing the corrected flow
module UsageExample =

    let demonstrateOrderFlow (initialOrder: Order) =
        // Step 1: Process order to user-ready state
        let result1 = OrderProcessor.processOrder initialOrder

        match result1.IsReadyForUser with
        | true ->
            printfn "Order ready for user at state: %A" result1.CurrentState
            printfn "Available options: %d" result1.SelectableOptions.Length

            // Step 2: User makes a selection (only ONE at a time)
            match result1.SelectableOptions with
            | option :: _ ->
                let userSelection = {
                    VariableRole = option.VariableRole
                    SelectedValue = option.AvailableValues |> List.head
                }

                let result2 = OrderProcessor.applyUserSelection result1.Order userSelection
                printfn "After selection, new state: %A" result2.CurrentState
                printfn "Remaining options: %d" result2.SelectableOptions.Length

            | [] ->
                printfn "No selectable options available"
        | false ->
            printfn "Order not ready for user interaction"