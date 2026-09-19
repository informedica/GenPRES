namespace Informedica.GenOrder.Lib


module OrderProcessor =

    open Informedica.Utils.Lib
    open ConsoleWriter.NewLineNoTime
    open Order

    module Quantity = OrderVariable.Quantity
    module Concentration = OrderVariable.Concentration
    module Time = OrderVariable.Time
    module Rate = OrderVariable.Rate
    module Frequency = OrderVariable.Frequency
    module Dose = Orderable.Dose
    module Increment = Informedica.GenSolver.Lib.Variable.ValueRange.Increment


    // FrequencyCleared and ConcentrationCleared are defensive arms —
    // these variables are not Clearable per the UI spec (Section 13.1),
    // but the arms are kept as guards against unexpected UI behavior.
    // NotCleared is a defensive catch-all that logs a warning. The system
    // operates on the single-variable change invariant: only one variable
    // is changed at a time, followed by a solver run. Multiple simultaneous
    // clears are not an expected scenario.
    let (|FrequencyCleared|RateCleared|TimeCleared|ConcentrationCleared|DoseQuantityCleared|DosePerTimeCleared|NotCleared|)
        (ord: Order)
        =
        let frq = ord.Schedule |> Schedule.getFrequency
        let tme = ord.Schedule |> Schedule.getTime

        match
            frq |> Option.map Frequency.isCleared |> Option.defaultValue false,
            ord.Orderable |> Orderable.isDoseRateCleared
            || ord.Orderable |> Orderable.isItemDoseRateCleared,
            tme |> Option.map Time.isCleared |> Option.defaultValue false,
            ord.Orderable |> Orderable.isConcentrationCleared,
            ord.Orderable |> Orderable.isDoseQuantityCleared,
            ord.Orderable |> Orderable.isItemDosePerTimeCleared
        with
        | true, false, false, false, false, false -> FrequencyCleared
        | false, true, false, false, false, false -> RateCleared
        | false, false, true, false, false, false -> TimeCleared
        | false, false, false, true, false, false -> ConcentrationCleared
        | false, false, false, false, true, false -> DoseQuantityCleared
        | false, false, false, false, false, true -> DosePerTimeCleared
        | res ->
            $"{res} was not matched!" |> writeWarningMessage
            NotCleared

    // == Property Change Frequency

    let orderPropertyIncrOrDecrFrequency step ord =
        ord
        |> OrderPropertyChange.proc
            [
                OrderableDose Dose.setPerTimeToNonZeroPositive
                ComponentDose("", Dose.setPerTimeToNonZeroPositive)
                ItemDose("", "", Dose.setPerTimeToNonZeroPositive)
            ]
        |> OrderPropertyChange.proc [ ScheduleFrequency step ]


    // == Property Change Dose Rate

    let orderPropertyIncrOrDecrOrderableDoseRate step ord =
        ord
        // clear dose rates
        |> OrderPropertyChange.proc
            [
                ScheduleTime Time.setToNonZeroPositive

                OrderableDose Dose.setRateAdjustToNonZeroPositive
                ComponentDose("", Dose.setRateToNonZeroPositive)
                ItemDose("", "", Dose.setRateToNonZeroPositive)

            ]
        // increase or decrease
        |> OrderPropertyChange.proc [ OrderableDose step ]

    // == Property Change Dose Quantity

    let orderPropertyIncrOrDecrOrderableDoseQuantity step ord =

        ord
        // clear order quantities
        |> OrderPropertyChange.proc
            [
                if ord.Schedule |> Schedule.hasTime then
                    ScheduleTime Time.setToNonZeroPositive

                if ord.Orderable.Components |> List.length > 1 then
                    OrderableDoseCount OrderVariable.Count.setMinToOne
                else
                    OrderableDoseCount OrderVariable.Count.setToOne
                    OrderableQuantity Quantity.setToNonZeroPositive
                    ComponentOrderableCount("", OrderVariable.Count.setToNonZeroPositive)
                    ComponentOrderableQuantity("", Quantity.setToNonZeroPositive)
                    ItemOrderableQuantity("", "", Quantity.setToNonZeroPositive)

                OrderableDose Dose.setQuantityAdjustToNonZeroPositive
                ComponentDose("", Dose.setQuantityToNonZeroPositive)
                ItemDose("", "", Dose.setQuantityToNonZeroPositive)

                OrderableDose Dose.setPerTimeToNonZeroPositive
                ComponentDose("", Dose.setPerTimeToNonZeroPositive)
                ItemDose("", "", Dose.setPerTimeToNonZeroPositive)
            ]
        // decrease or increase
        |> OrderPropertyChange.proc [ OrderableDose step ]


    // == Property Change Component Quantity

    /// <summary>
    /// Increase or decrease a specific component's orderable quantity by an increment.
    /// The new value may fall outside min/max constraints—this is intentional to allow
    /// the user to explore boundary values. All dependent variables are cleared
    /// (set to non-zero positive) to accept the propagated out-of-bounds values.
    /// Key relationships maintained:
    /// - orb_qty = sum(cmp_orb_qty): orderable quantity is recalculated from components
    /// - cmp_orb_qty = orb_dos_cnt * cmp_dos_qty: dose count stays constant
    /// - itm_orb_cnc = itm_orb_qty / orb_qty: item concentrations update for all components
    /// For timed schedules, rate is kept constant while time adjusts.
    /// </summary>
    /// <param name="step">The function to apply (increase or decrease) to the component quantity</param>
    /// <param name="cmp">The name of the component to modify</param>
    /// <param name="ord">The order to process</param>
    let orderPropertyIncrOrDecrComponentOrderableQuantity step cmp ord =
        ord
        |> OrderPropertyChange.proc
            [
                // for timed schedules: keep rate constant, clear time to absorb quantity change
                if ord.Schedule |> Schedule.hasTime then
                    ScheduleTime Time.setToNonZeroPositive
                // orderable quantity is recalculated from component sum (eq 57: orb_qty = sum(cmp_orb_qty))
                // clear to accept any positive value since component may be out of bounds
                OrderableQuantity Quantity.applyOnlyMinIncrConstraints
                // component counts and concentrations change when component quantities change
                // clear to allow recalculation with potentially out-of-bounds values
                ComponentOrderableCount(cmp, OrderVariable.Count.setToNonZeroPositive)
                ComponentOrderableConcentration("", Concentration.setToNonZeroPositive)
                // component dose quantity
                // is recalculated via: cmp_orb_qty = orb_dos_cnt * cmp_dos_qty
                // if cmp_orb_qty is out of bounds, cmp_dos_qty can also be out of bounds
                ComponentDose(cmp, Dose.setQuantityToNonZeroPositive)
                ItemDose(cmp, "", Dose.setQuantityToNonZeroPositive)
                // in addition to work with non-continuous as well
                ComponentDose(cmp, Dose.setPerTimeToNonZeroPositive)
                ItemDose(cmp, "", Dose.setPerTimeToNonZeroPositive)
                // dose rate changes for all components as this is
                // relative to concentration with a fixed orderable dose rate
                ComponentDose("", Dose.setRateToNonZeroPositive)
                ItemDose("", "", Dose.setRateToNonZeroPositive)
                // item orderable quantities change only for the modified component
                // (item quantities in other components are unaffected by their component's quantity)
                ItemOrderableQuantity(cmp, "", Quantity.setToNonZeroPositive)
                // all item orderable concentrations change because total orb_qty changes
                // (eq 4: itm_orb_cnc = itm_orb_qty / orb_qty)
                ItemOrderableConcentration("", "", Concentration.setToNonZeroPositive)

                // recalc orb_dos_cnt when only one component, so only
                // that component can change
                if ord.Orderable.Components.Length = 1 then
                    OrderableDoseCount OrderVariable.Count.setToNonZeroPositive
                else
                    // the composition has changed so reset
                    OrderableDoseCount OrderVariable.Count.setToOne
                    // orderable doses are derived from component doses
                    // clear to accept propagated out-of-bounds values
                    OrderableDose Dose.applyQuantityMinIncrConstraints
                    OrderableDose Dose.setPerTimeToNonZeroPositive
            ]
        |> OrderPropertyChange.proc [ ComponentOrderableQuantity(cmp, step) ]


    let processChangeProperty cmd ord =
        let setFreq step = OrderPropertyChange.proc [ ScheduleFrequency step ]

        let setDose step = OrderPropertyChange.proc [ OrderableDose step ]

        let setCmpOrbQty cmp step =
            OrderPropertyChange.proc [ ComponentOrderableQuantity(cmp, step) ]

        match cmd with
        // Frequency
        | DecreaseScheduleFrequency -> ord |> orderPropertyIncrOrDecrFrequency Frequency.decrease
        | IncreaseScheduleFrequency -> ord |> orderPropertyIncrOrDecrFrequency Frequency.increase
        | SetMinScheduleFrequency -> ord |> setFreq Frequency.setMinValue
        | SetMedianScheduleFrequency -> ord |> setFreq Frequency.setMedianValue
        | SetMaxScheduleFrequency -> ord |> setFreq Frequency.setMaxValue
        // Dose Quantity
        | DecreaseOrderableDoseQuantity(n, useCalc) ->
            ord
            |> orderPropertyIncrOrDecrOrderableDoseQuantity (Dose.decreaseQuantity useCalc n)
        | IncreaseOrderableDoseQuantity(n, useCalc) ->
            ord
            |> orderPropertyIncrOrDecrOrderableDoseQuantity (Dose.increaseQuantity useCalc n)
        | SetMinOrderableDoseQuantity -> ord |> setDose (Dose.setMinDose ord.Schedule false)
        | SetMaxOrderableDoseQuantity -> ord |> setDose (Dose.setMaxDose ord.Schedule false)
        | SetMedianOrderableDoseQuantity -> ord |> setDose (Dose.setMedianDose ord.Schedule false)
        | SetOrderableDoseQuantityPerc n -> ord |> setDose (Dose.setPercValue n ord.Schedule false)
        // Dose Rate
        | DecreaseOrderableDoseRate(n, useCalc) ->
            ord |> orderPropertyIncrOrDecrOrderableDoseRate (Dose.decreaseRate useCalc n)
        | IncreaseOrderableDoseRate(n, useCalc) ->
            ord |> orderPropertyIncrOrDecrOrderableDoseRate (Dose.increaseRate useCalc n)
        | SetMinOrderableDoseRate -> ord |> setDose (Dose.setMinDose ord.Schedule true)
        | SetMaxOrderableDoseRate -> ord |> setDose (Dose.setMaxDose ord.Schedule true)
        | SetMedianOrderableDoseRate -> ord |> setDose (Dose.setMedianDose ord.Schedule true)
        // Component Quantity
        | DecreaseComponentOrderableQuantity(cmp, n, useCalc) ->
            ord
            |> orderPropertyIncrOrDecrComponentOrderableQuantity (Quantity.decrease useCalc n) cmp
        | IncreaseComponentOrderableQuantity(cmp, n, useCalc) ->
            ord
            |> orderPropertyIncrOrDecrComponentOrderableQuantity (Quantity.increase useCalc n) cmp
        | SetMinComponentOrderableQuantity cmp -> ord |> setCmpOrbQty cmp Quantity.setMinValue
        | SetMaxComponentOrderableQuantity cmp -> ord |> setCmpOrbQty cmp Quantity.setMaxValue
        | SetMedianComponentOrderableQuantity cmp -> ord |> setCmpOrbQty cmp Quantity.setMedianValue
        | ComponentInStock _ ->
            ConsoleWriter.writeWarningMessage $"{cmd} not implemented" true false
            ord


    let processClearedFrequency ord =
        ord
        |> OrderPropertyChange.proc
            [
                ScheduleFrequency Frequency.setToNonZeroPositive

                OrderableDose Dose.setPerTimeToNonZeroPositive
                ComponentDose("", Dose.setPerTimeToNonZeroPositive)
                ItemDose("", "", Dose.setPerTimeToNonZeroPositive)
            ]
        |> OrderPropertyChange.proc [ ScheduleFrequency Frequency.setStandardValues ]


    let isPrepWithinConstraints ord =
        [
            ord.Orderable.OrderableQuantity |> Quantity.toOrdVar
            yield! ord.Orderable.Components |> List.map (_.OrderableQuantity >> Quantity.toOrdVar)
        ]
        |> List.forall (OrderVariable.isWithinConstraints true)


    // processClearedDose and processClearedRate use applyConstraints to reset
    // out-of-bounds preparation variables to their Calculated constraints
    // (the effective solver domain from Section 4). Defined constraints are
    // preserved separately for user-facing violation feedback.
    // isPrepWithinConstraints checks orderable and component quantities to
    // determine whether preparation variables need resetting.
    let processClearedDose ord =
        ord
        |> OrderPropertyChange.proc
            [
                if ord.Schedule |> Schedule.isFrequencyWithinConstraints true |> not then
                    ScheduleFrequency Frequency.applyConstraints

                if ord.Schedule |> Schedule.hasTime then
                    ScheduleTime Time.applyConstraints

                if
                    ord.Orderable.Components |> List.length = 1
                    || ord |> isPrepWithinConstraints |> not
                then
                    OrderableQuantity Quantity.applyConstraints
                    ComponentOrderableQuantity("", Quantity.applyConstraints)
                    ComponentOrderableCount("", OrderVariable.Count.applyConstraints)
                    ComponentOrderableConcentration("", Concentration.applyConstraints)
                    ItemOrderableQuantity("", "", Quantity.applyConstraints)
                    ItemOrderableConcentration("", "", Concentration.applyConstraints)

                OrderableDoseCount OrderVariable.Count.applyConstraints
                OrderableDose Dose.applyQuantityConstraints
                OrderableDose Dose.applyPerTimeConstraints
                ComponentDose("", Dose.applyQuantityConstraints)
                ComponentDose("", Dose.applyPerTimeConstraints)
                ItemDose("", "", Dose.applyQuantityConstraints)
                ItemDose("", "", Dose.applyPerTimeConstraints)
            ]


    let processClearedRate ord =
        ord
        |> OrderPropertyChange.proc
            [
                if ord.Schedule |> Schedule.hasTime then
                    ScheduleTime Time.applyConstraints

                if ord |> isPrepWithinConstraints |> not then
                    OrderableQuantity Quantity.applyConstraints
                    ComponentOrderableQuantity("", Quantity.applyConstraints)
                    ComponentOrderableCount("", OrderVariable.Count.applyConstraints)
                    ComponentOrderableConcentration("", Concentration.applyConstraints)
                    ItemOrderableQuantity("", "", Quantity.applyConstraints)
                    ItemOrderableConcentration("", "", Concentration.applyConstraints)

                    OrderableDoseCount OrderVariable.Count.applyConstraints
                    OrderableDose Dose.applyQuantityConstraints
                    ComponentDose("", Dose.applyQuantityConstraints)
                    ItemDose("", "", Dose.applyQuantityConstraints)

                OrderableDose Dose.applyRateConstraints
                ComponentDose("", Dose.applyRateConstraints)
                ItemDose("", "", Dose.applyRateConstraints)
            ]


    /// Process an order that has been cleared
    /// by setting relevant variables to non-zero positive
    /// values and solving the order again
    let processClearedOrder logger ord =
        // small helpers to clarify post-processing after property changes
        let solveAndToValues minTime skipRate =
            solveMinMax "Process Cleared Order" true logger
            >> Result.map (minIncrMaxToValues true minTime skipRate logger)

        let defaultInc = 100

        let solveIncrIncrAndToValues minTime skipRate =
            solveMinMax "Process Cleared Order" true logger
            >> Result.bind (increaseIncrements logger defaultInc defaultInc)
            >> Result.map (minIncrMaxToValues true minTime skipRate logger)

        let hasTimeNotContinuous =
            ord.Schedule |> Schedule.hasTime && ord.Schedule |> Schedule.isContinuous |> not

        let logUnmatched (kind: string) =
            $"===> no match for {kind} cleared " |> writeWarningMessage

            // FLAG: expensive message build (toConsoleTableString renders a full
            // console table) — kept lazy so it is skipped when logging is off.
            Logging.logWarningLazy logger (fun () -> ord |> toConsoleTableString |> Events.OrderScenario)

        match (ord |> inf).Schedule with
        | Continuous _ ->
            match ord with
            | TimeCleared
            | RateCleared ->
                "Rate or Time cleared" |> Events.OrderScenario |> Logging.logInfo logger

                ord |> processClearedRate |> solveAndToValues true false
            | _ ->
                logUnmatched "continuous"
                ord |> solveOrder "Process Cleared Order" true logger
        | Once
        | Discontinuous _ ->
            match ord with
            | DosePerTimeCleared
            | DoseQuantityCleared ->
                "Dose per time or quantity cleared"
                |> Events.OrderScenario
                |> Logging.logInfo logger

                ord |> processClearedDose |> solveIncrIncrAndToValues true false
            | _ ->
                logUnmatched "discontinuous"
                ord |> solveOrder "Process Cleared Order" true logger
        | OnceTimed _
        | Timed _ ->
            match ord with
            | RateCleared
            | TimeCleared ->
                "Rate or Time cleared" |> Events.OrderScenario |> Logging.logInfo logger

                ord |> processClearedRate |> solveAndToValues false hasTimeNotContinuous
            | DosePerTimeCleared
            | DoseQuantityCleared ->
                "Dose per time or quantity cleared"
                |> Events.OrderScenario
                |> Logging.logInfo logger

                ord |> processClearedDose |> solveIncrIncrAndToValues false hasTimeNotContinuous
            | _ ->
                logUnmatched "timed"
                ord |> solveOrder "Process Cleared Order" true logger


    type GenSolverExceptionMsg = Informedica.GenSolver.Lib.Types.Exceptions.Message


    let (|NoConstraintsApplied|NoValues|HasValues|DoseSolvedNotCleared|DoseSolvedAndCleared|) ord =
        match ord with
        | _ when ord |> areAllConstraintsNotApplied -> NoConstraintsApplied
        | _ when ord |> hasValues -> HasValues
        | _ when ord |> doseIsSolved && ord |> isCleared |> not -> DoseSolvedNotCleared
        | _ when ord |> doseIsSolved && ord |> isCleared -> DoseSolvedAndCleared
        | _ -> NoValues


    let printState =
        function
        | NoConstraintsApplied -> "NoConstraintsApplied"
        | NoValues -> "NoValues"
        | HasValues -> "HasValues"
        | DoseSolvedNotCleared -> "DoseSolvedNotCleared"
        | DoseSolvedAndCleared -> "DoseSolvedAndCleared"


    // New: A lightweight classification and step-driven pipeline
    type PrescriptionKind =
        | PKOnce
        | PKOnceTimed
        | PKDiscontinuous
        | PKContinuous
        | PKTimed


    /// The state of an Order used for classification
    /// in the processing pipeline
    /// IsEmpty: whether the order is empty
    /// HasValues: whether the order has values
    /// DoseIsSolved: whether the dose is solved
    /// IsCleared: whether the order is cleared
    /// PrescriptionKind: the kind of prescription
    /// (Once, OnceTimed, Discontinuous, Continuous, Timed)
    /// This is used to determine which steps to run
    /// in the processing pipeline
    /// and in which order
    type OrderState =
        {
            IsConstraintsNotApplied: bool
            HasValues: bool
            CanSetNormDose: bool
            DoseIsSolved: bool
            OrderIsSolved: bool
            IsCleared: bool
            PrescriptionKind: PrescriptionKind
        }


    /// Classify an order into an OrderState
    let classify (ord: Order) : OrderState =
        let kind =
            match ord.Schedule with
            | Once -> PKOnce
            | OnceTimed _ -> PKOnceTimed
            | Discontinuous _ -> PKDiscontinuous
            | Continuous _ -> PKContinuous
            | Timed _ -> PKTimed

        {
            IsConstraintsNotApplied = ord |> areAllConstraintsNotApplied
            HasValues = ord |> hasValues
            CanSetNormDose =
                [
                    if ord.Schedule |> Schedule.hasTime then
                        ord.Orderable.Dose.Rate |> Rate.toOrdVar
                    if ord.Schedule.IsContinuous |> not then
                        ord.Orderable.Dose.Quantity |> Quantity.toOrdVar
                ]
                |> List.forall OrderVariable.hasValues
            DoseIsSolved = ord |> doseIsSolved
            OrderIsSolved = ord |> isSolved
            IsCleared = ord |> isCleared
            PrescriptionKind = kind
        }


    /// A processing step in the pipeline
    /// with a name, a guard function to check
    /// whether to run the step, and a run function
    /// that takes an Order and returns a Result
    /// with the processed Order or a list of error messages
    type Step =
        {
            Name: string
            Guard: OrderState -> bool
            Run: Order -> Result<Order, Order * GenSolverExceptionMsg list>
        }


    /// <summary>
    /// Process an order through a pipeline of steps
    /// depending on the command given
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="cmd">The command to process</param>
    /// <returns>A Result with the processed Order or a list of error messages</returns>
    let processPipeline logger cmd =

        let runStep (step: Step) (ord: Order) =
            if step.Guard(classify ord) then
                $"\n=== PIPELINE START {step.Name} ===\n"
                |> Events.OrderScenario
                |> Logging.logInfo logger

                step.Run ord
                |> function
                    | Ok ord ->
                        //ord |> stringTable |> Events.OrderScenario |> Logging.logInfo logger
                        Ok ord
                    | Error(ord, msgs) ->
                        $"Error in {step.Name}" |> Events.OrderScenario |> Logging.logInfo logger

                        Error(ord, msgs)

                |> fun res ->
                    $"\n=== PIPELINE END {step.Name} ===\n"
                    |> Events.OrderScenario
                    |> Logging.logInfo logger

                    res

            else
                Ok ord

        let runPipeline (ord: Order) (steps: Step list) =
            (Ok ord, steps)
            ||> List.fold (fun acc step -> acc |> Result.bind (runStep step))

        // Core step functions
        let calcMinMaxStep = calcMinMax logger

        let setCalculatedConstraintsStep =
            setCalculatedConstraints
            >> fun ord ->
                // FLAG: expensive message build (toConsoleTableString) — kept lazy
                // so it is skipped when the logger is off.
                Logging.logInfoLazy logger (fun () -> ord |> toConsoleTableString |> Events.OrderScenario)

                ord
            >> Ok

        let applyCalculatedConstraintsStep = applyCalculatedConstraints >> Ok

        let increaseIncrementStep ord = ord |> increaseIncrements logger 10 10

        let calcValuesStep useMax skipRate ord = ord |> minIncrMaxToValues useMax true skipRate logger |> Ok

        let reCalcValuesStep useMax skipRate ord = ord |> minIncrMaxToValues useMax false skipRate logger |> Ok

        let solveStep ord = solveOrder "Solve Order Step" true logger ord

        let calcNormDoseStep = solveNormDose logger

        let processClearedStep ord =
            match processClearedOrder logger ord with
            | Ok o -> Ok o
            | Error _ -> solveOrder "Error in Cleared Step Solve again" true logger ord

        let applyConstraintsStep ord =
            ord
            |> applyConstraints
            |> fun ord ->
                // FLAG: expensive message build (toConsoleTableString) — kept lazy
                // so it is skipped when the logger is off.
                Logging.logInfoLazy logger (fun () -> ord |> toConsoleTableString |> Events.OrderScenario)

                ord
            |> Ok

        let pickNearestHigherElseLowerComponentQuantityStep ord =
            ord |> pickNearestHigherElseLowerComponentQuantity logger

        match cmd with
        | CalcMinMax ord ->
            [
                {
                    Name = "calc-minmax: apply-constraints"
                    Guard = (fun _ -> true)
                    Run = applyConstraintsStep
                }
                {
                    Name = "calc-minmax: calc-minmax"
                    Guard = (fun _ -> true)
                    Run = calcMinMaxStep
                }
                {
                    Name = "calc-minmax: increase-increments"
                    Guard = (fun _ -> true)
                    Run = increaseIncrementStep
                }
                {
                    Name = "calc-minmax: set-calculated-constraints"
                    Guard = (fun _ -> true)
                    Run = setCalculatedConstraintsStep
                }
                // norm dose calc needs dose qty values; for timed orders rate is deferred (staged expansion)
                if ord |> hasNormDose && ord.Orderable.Components |> List.length <= 2 then
                    let hasTimeNotContinuous =
                        ord.Schedule |> Schedule.hasTime && ord.Schedule |> Schedule.isContinuous |> not

                    {
                        Name = "solve-order: ensure-dose-values-1"
                        Guard = _.CanSetNormDose >> not
                        Run = calcValuesStep (ord.Orderable.Components |> List.length <= 2) hasTimeNotContinuous
                    }

                    {
                        Name = "calc-minmax: set-normdose"
                        Guard = (fun _ -> true)
                        Run = calcNormDoseStep
                    }
            ]
            |> runPipeline ord

        | IncreaseIncrements ord ->
            [
                {
                    Name = "increase-increment: increase-increment"
                    Guard = (fun _ -> true)
                    Run = increaseIncrementStep
                }
            ]
            |> runPipeline ord

        | CalcValues ord ->
            let guard (os: OrderState) =
                ord.Orderable.Components |> List.length <= 2
                && os.DoseIsSolved |> not
                && os.OrderIsSolved |> not

            let hasTimeNotContinuous =
                ord.Schedule |> Schedule.hasTime && ord.Schedule |> Schedule.isContinuous |> not

            [
                {
                    Name = "calc-values: calc-qty-values"
                    Guard = guard
                    Run = calcValuesStep false hasTimeNotContinuous
                }
                if hasTimeNotContinuous then
                    {
                        Name = "calc-values: calc-rate-values"
                        Guard = guard
                        Run = calcValuesStep false false
                    }
            ]
            |> runPipeline ord

        | SolveOrder ord ->
            [
                {
                    Name = "solve-order: process-cleared"
                    Guard = (fun os -> os.DoseIsSolved && os.IsCleared)
                    Run = processClearedStep
                }
                {
                    Name = "solve-order: ensure-values-1"
                    Guard = (_.HasValues >> not)
                    Run = calcValuesStep (ord.Orderable.Components |> List.length <= 2) false
                }
                {
                    Name = "solve-order: final-solve"
                    Guard = (_.OrderIsSolved >> not)
                    Run = solveStep
                }
                {
                    Name = "solve-order: pick-cmp-qty"
                    Guard = (fun _ -> true)
                    Run = pickNearestHigherElseLowerComponentQuantityStep
                }
            ]
            |> runPipeline ord

        | ReCalcValues ord ->
            let useMax = ord.Orderable.Components |> List.length <= 2

            let hasTimeNotContinuous =
                ord.Schedule |> Schedule.hasTime && ord.Schedule |> Schedule.isContinuous |> not

            [
                {
                    Name = "recalc-values: apply-calculated-constraints"
                    Guard = (fun _ -> true)
                    Run = applyCalculatedConstraintsStep
                }
                {
                    Name = "recalc-values: calc-qty-values"
                    Guard = (fun _ -> true)
                    Run = reCalcValuesStep useMax hasTimeNotContinuous
                }
                if hasTimeNotContinuous then
                    {
                        Name = "recalc-values: calc-rate-values"
                        Guard = (fun _ -> true)
                        Run = reCalcValuesStep useMax false
                    }
                {
                    Name = "recalc-values: final-solve"
                    Guard = (_.OrderIsSolved >> not)
                    Run = solveStep
                }
            ]
            |> runPipeline ord

        | ChangeProperty(ord, cmd) -> //ord |> processChangeProperty cmd |> Ok
            [
                {
                    Name = $"change-property: {cmd}"
                    Guard = (fun _ -> true)
                    Run = processChangeProperty cmd >> Ok
                }
                {
                    Name = "change-property: solve-minmax"
                    Guard = (fun _ -> true)
                    Run = calcMinMaxStep
                }
            ]
            |> runPipeline ord
