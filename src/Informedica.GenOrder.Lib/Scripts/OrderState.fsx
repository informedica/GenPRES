#load "load.fsx"

open Informedica.GenSolver.Lib
open Informedica.GenOrder.Lib


/// Represents the value state of an OrderVariable
type VariableValueState =
    /// Has not both an Lower and Upper Bound
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

/// Phase of order processing
type OrderPart =
    | Prescription
    | Preparation
    | Administration

/// Prescription-specific variable relevance
type PrescriptionVariableRelevance = {
    PrescriptionType: string
    OrderPart: OrderPart
    RelevantVariables: VariableRole list
    CriticalVariables: VariableRole list  // Must be solved for this phase
}

/// Overall state of an Order
type OrderState =
    /// Order is initiated but not processed yet
    | Empty
    /// Constraints have been applied but there are no selectable values
    | Constrained
    /// Order has multiple options available for selection
    | HasChoices
    /// All prescription variables are solved, but preparation may need work
    | PrescriptionSolved
    /// All preparation variables are solved, but administration may need work
    | PreparationSolved
    /// All relevant variables are solved and order is ready
    | FullySolved
    /// A variable has been cleared and order needs recalculation
    | RequiresRecalculation

/// State transition operations
type StateTransition =
    | ApplyConstraints
    | CalculateMinMax
    | MaterializeValues
    | SolveEquations
    | ProcessCleared
    | IncreaseIncrement of maxCount: int

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

/// Variable relevance table by prescription type and phase
module VariableRelevanceTable =

    let prescriptionRelevance : PrescriptionVariableRelevance list = [
        // Once prescription
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

        {
            PrescriptionType = "Once"
            OrderPart = Preparation
            RelevantVariables = [
                OrderableQuantity; ComponentOrderableQuantity; ComponentOrderableCount
                ItemOrderableQuantity; ItemComponentConcentration; ItemOrderableConcentration
            ]
            CriticalVariables = [OrderableQuantity]
        }

        {
            PrescriptionType = "Once"
            OrderPart = Administration
            RelevantVariables = [
                OrderableQuantity; ComponentOrderableQuantity
                ItemOrderableQuantity
            ]
            CriticalVariables = [OrderableQuantity]
        }

        // OnceTimed prescription
        {
            PrescriptionType = "OnceTimed"
            OrderPart = Prescription
            RelevantVariables = [
                PrescriptionTime; OrderableDoseQuantity; OrderableDoseQuantityAdjust
                OrderableDoseRate; OrderableDoseRateAdjust
                ComponentDoseQuantity; ComponentDoseQuantityAdjust
                ItemDoseQuantity; ItemDoseQuantityAdjust
            ]
            CriticalVariables = [PrescriptionTime; OrderableDoseQuantity]
        }

        {
            PrescriptionType = "OnceTimed"
            OrderPart = Preparation
            RelevantVariables = [
                OrderableQuantity; ComponentOrderableQuantity; ComponentOrderableCount
                ItemOrderableQuantity; ItemComponentConcentration; ItemOrderableConcentration
            ]
            CriticalVariables = [OrderableQuantity]
        }

        {
            PrescriptionType = "OnceTimed"
            OrderPart = Administration
            RelevantVariables = [
                OrderableQuantity; OrderableDoseRate
                ComponentOrderableQuantity; ItemOrderableQuantity
            ]
            CriticalVariables = [OrderableQuantity; OrderableDoseRate]
        }

        // Discontinuous prescription
        {
            PrescriptionType = "Discontinuous"
            OrderPart = Prescription
            RelevantVariables = [
                PrescriptionFrequency; OrderableDoseQuantity; OrderableDosePerTime
                OrderableDoseQuantityAdjust; OrderableDosePerTimeAdjust
                ComponentDoseQuantity; ComponentDosePerTime
                ComponentDoseQuantityAdjust; ComponentDosePerTimeAdjust
                ItemDoseQuantity; ItemDosePerTime
                ItemDoseQuantityAdjust; ItemDosePerTimeAdjust
            ]
            CriticalVariables = [PrescriptionFrequency; OrderableDosePerTime]
        }

        {
            PrescriptionType = "Discontinuous"
            OrderPart = Preparation
            RelevantVariables = [
                OrderableQuantity; ComponentOrderableQuantity; ComponentOrderableCount
                ItemOrderableQuantity; ItemComponentConcentration; ItemOrderableConcentration
            ]
            CriticalVariables = [OrderableQuantity]
        }

        {
            PrescriptionType = "Discontinuous"
            OrderPart = Administration
            RelevantVariables = [
                PrescriptionFrequency; OrderableQuantity
                ComponentOrderableQuantity; ItemOrderableQuantity
            ]
            CriticalVariables = [PrescriptionFrequency; OrderableQuantity]
        }

        // Continuous prescription
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

        {
            PrescriptionType = "Continuous"
            OrderPart = Preparation
            RelevantVariables = [
                OrderableQuantity; ComponentOrderableQuantity; ComponentOrderableCount
                ItemOrderableQuantity; ItemComponentConcentration; ItemOrderableConcentration
            ]
            CriticalVariables = [OrderableQuantity]
        }

        {
            PrescriptionType = "Continuous"
            OrderPart = Administration
            RelevantVariables = [
                OrderableQuantity; OrderableDoseRate
                ComponentOrderableQuantity; ItemOrderableQuantity
            ]
            CriticalVariables = [OrderableQuantity; OrderableDoseRate]
        }

        // Timed prescription
        {
            PrescriptionType = "Timed"
            OrderPart = Prescription
            RelevantVariables = [
                PrescriptionFrequency; PrescriptionTime
                OrderableDoseQuantity; OrderableDosePerTime; OrderableDoseRate
                OrderableDoseQuantityAdjust; OrderableDosePerTimeAdjust; OrderableDoseRateAdjust
                ComponentDoseQuantity; ComponentDosePerTime; ComponentDoseRate
                ComponentDoseQuantityAdjust; ComponentDosePerTimeAdjust; ComponentDoseRateAdjust
                ItemDoseQuantity; ItemDosePerTime; ItemDoseRate
                ItemDoseQuantityAdjust; ItemDosePerTimeAdjust; ItemDoseRateAdjust
            ]
            CriticalVariables = [PrescriptionFrequency; PrescriptionTime; OrderableDosePerTime]
        }

        {
            PrescriptionType = "Timed"
            OrderPart = Preparation
            RelevantVariables = [
                OrderableQuantity; ComponentOrderableQuantity; ComponentOrderableCount
                ItemOrderableQuantity; ItemComponentConcentration; ItemOrderableConcentration
            ]
            CriticalVariables = [OrderableQuantity]
        }

        {
            PrescriptionType = "Timed"
            OrderPart = Administration
            RelevantVariables = [
                PrescriptionFrequency; PrescriptionTime; OrderableQuantity; OrderableDoseRate
                ComponentOrderableQuantity; ItemOrderableQuantity
            ]
            CriticalVariables = [PrescriptionFrequency; PrescriptionTime; OrderableQuantity; OrderableDoseRate]
        }
    ]

    /// Get relevant variables for a prescription type and the order part
    let getRelevantVariables (prescriptionType: string) (part: OrderPart) : VariableRole list =
        prescriptionRelevance
        |> List.filter (fun r -> r.PrescriptionType = prescriptionType && r.OrderPart = part)
        |> List.collect (fun r -> r.RelevantVariables)
        |> List.distinct

    /// Get critical variables for a prescription type and the order part
    let getCriticalVariables (prescriptionType: string) (phase: OrderPart) : VariableRole list =
        prescriptionRelevance
        |> List.filter (fun r -> r.PrescriptionType = prescriptionType && r.OrderPart = phase)
        |> List.collect (fun r -> r.CriticalVariables)
        |> List.distinct

/// Order state classification and transitions
module OrderStateClassification =

    /// Classify the overall state of an Order
    let classifyOrderState (order: Order) : OrderState =
        if Order.isEmpty order then Empty
        elif Order.hasValues order then HasChoices
        elif Order.doseIsSolved order then
            if Order.isCleared order then RequiresRecalculation
            else PrescriptionSolved  // Could be further refined to check preparation/administration
        elif Order.hasConstraints order then Constrained
        else Empty

    /// Determine what transitions are possible from current state
    let possibleTransitions (orderState: OrderState) : StateTransition list =
        match orderState with
        | Empty -> [ApplyConstraints; CalculateMinMax]
        | Constrained -> [CalculateMinMax; MaterializeValues]
        | HasChoices -> [SolveEquations]
        | PrescriptionSolved -> [MaterializeValues; SolveEquations]
        | PreparationSolved -> [SolveEquations]
        | RequiresRecalculation -> [ProcessCleared; ApplyConstraints]
        | FullySolved -> []

    /// Determine the next recommended action
    let recommendAction (order: Order) : StateTransition option =
        let state = classifyOrderState order
        possibleTransitions state |> List.tryHead

/// State-aware command for processing orders
type StateAwareCommand =
    | InitializeOrder of Order
    | ProgressToNextPhase of Order * OrderPart
    | HandleClearedVariables of Order * VariableRole list
    | MaterializeChoices of Order * VariableRole list
    | FinalizeOrder of Order

/// Result of state-aware processing
type StateProcessingResult = {
    Order: Order
    NewState: OrderState
    AvailableChoices: (VariableRole * OrderVariableState) list
    NextRecommendedAction: StateTransition option
    PhaseCompletionStatus: (OrderPart * bool) list
}

/// Enhanced OrderState with phase tracking
type DetailedOrderState = {
    OverallState: OrderState
    CurrentPhase: OrderPart
    CompletedPhases: OrderPart list
    PrescriptionType: string
    ClearedVariables: VariableRole list
    VariableStates: Map<VariableRole, OrderVariableState>
}

/// Helper functions for working with the state model
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

    /// Get detailed state analysis of an order
    let rec analyzeDetailedState (order: Order) : DetailedOrderState =
        let prescriptionType =
            match order.Prescription with
            | Once -> "Once"
            | OnceTimed _ -> "OnceTimed"
            | Continuous _ -> "Continuous"
            | Discontinuous _ -> "Discontinuous"
            | Timed _ -> "Timed"

        let ovars = order |> Order.toOrdVars
        let variableStates =
            ovars
            |> List.choose (fun ovar ->
                identifyVariableRole ovar
                |> Option.map (fun role ->
                    role, OrderVariableClassification.classifyOrderVariable ovar))
            |> Map.ofList

        let clearedVars =
            variableStates
            |> Map.toList
            |> List.filter (fun (_, state) ->
                state.ValueState = UnBounded)
            |> List.map fst

        let completedPhases =
            [Prescription; Preparation; Administration]
            |> List.filter (isPhaseComplete order)

        let currentPhase =
            if completedPhases |> List.contains Administration then Administration
            elif completedPhases |> List.contains Preparation then Administration
            elif completedPhases |> List.contains Prescription then Preparation
            else Prescription

        {
            OverallState = OrderStateClassification.classifyOrderState order
            CurrentPhase = currentPhase
            CompletedPhases = completedPhases
            PrescriptionType = prescriptionType
            ClearedVariables = clearedVars
            VariableStates = variableStates
        }

    /// Check if an order phase is complete
    and isPhaseComplete (order: Order) (phase: OrderPart) : bool =
        let prescriptionType =
            match order.Prescription with
            | Once -> "Once"
            | OnceTimed _ -> "OnceTimed"
            | Continuous _ -> "Continuous"
            | Discontinuous _ -> "Discontinuous"
            | Timed _ -> "Timed"

        let criticalVars = VariableRelevanceTable.getCriticalVariables prescriptionType phase

        // Simplified logic - in full implementation would check each critical variable
        match phase with
        | Prescription -> Order.doseIsSolved order
        | Preparation -> failwith "not implemented yet" // not (order.Orderable |> Orderable.isConcentrationCleared)
        | Administration -> Order.isSolved order

    /// Get variables that need attention for the current phase
    let getVariablesNeedingAttention (order: Order) (phase: OrderPart) : VariableRole list =
        let state = analyzeDetailedState order

        VariableRelevanceTable.getRelevantVariables state.PrescriptionType phase
        |> List.filter (fun role ->
            state.VariableStates
            |> Map.tryFind role
            |> Option.map (fun s -> s.ValueState <> IsSolved)
            |> Option.defaultValue true)

    /// Process state-aware command
    let processStateCommand (command: StateAwareCommand) : Result<StateProcessingResult, string> =
        match command with
        | InitializeOrder order ->
            let state = analyzeDetailedState order
            let choices =
                state.VariableStates
                |> Map.toList
                |> List.filter (fun (_, s) -> s.ValueState = HasValues)

            Ok {
                Order = order
                NewState = state.OverallState
                AvailableChoices = choices
                NextRecommendedAction = OrderStateClassification.recommendAction order
                PhaseCompletionStatus =
                    [Prescription; Preparation; Administration]
                    |> List.map (fun p -> p, isPhaseComplete order p)
            }
        | _ -> Error "Command processing not fully implemented"