namespace Informedica.GenOrder.Lib


module OrderProcessor =

    open Informedica.Utils.Lib
    open Order

    module Quantity = OrderVariable.Quantity
    module Concentration = OrderVariable.Concentration
    module Time = OrderVariable.Time
    module Count = OrderVariable.Count
    module Rate = OrderVariable.Rate
    module Frequency = OrderVariable.Frequency
    module PerTime = OrderVariable.PerTime
    module QuantityAdjust = OrderVariable.QuantityAdjust
    module PerTimeAdjust = OrderVariable.PerTimeAdjust
    module RateAdjust = OrderVariable.RateAdjust
    module Dose = Orderable.Dose
    module Increment = Informedica.GenSolver.Lib.Variable.ValueRange.Increment
    module Variable = Informedica.GenSolver.Lib.Variable


    /// The order as it is: every property command runs through the order program, and a component
    /// in stock is not implemented.
    let processChangeProperty logger cmd ord =
        match cmd with
        | ComponentInStock _ ->
            $"{cmd} not implemented" |> Events.OrderScenario |> Logging.logWarning logger
            ord
        | _ -> ord


    type GenSolverExceptionMsg = Informedica.GenSolver.Lib.Types.Exceptions.Message


    let (|NoConstraintsApplied|NoValues|HasValues|DoseSolved|) ord =
        match ord with
        | _ when ord |> areAllConstraintsNotApplied -> NoConstraintsApplied
        | _ when ord |> hasValues -> HasValues
        | _ when ord |> doseIsSolved -> DoseSolved
        | _ -> NoValues


    let printState =
        function
        | NoConstraintsApplied -> "NoConstraintsApplied"
        | NoValues -> "NoValues"
        | HasValues -> "HasValues"
        | DoseSolved -> "DoseSolved"


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
            CanSetNormDose = ord |> canSetNormDose
            DoseIsSolved = ord |> doseIsSolved
            OrderIsSolved = ord |> isSolved
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
    /// depending on the command given, without the order program
    /// </summary>
    /// <param name="caps">The maximum number of values a pruned variable keeps, per schedule</param>
    /// <param name="logger">The logger</param>
    /// <param name="cmd">The command to process</param>
    /// <returns>A Result with the processed Order or a list of error messages</returns>
    let rec processCommandWith caps logger cmd =

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

        let applyCalculatedConstraintsStep = applyCalculatedConstraints >> Ok

        let increaseIncrementStep ord = ord |> increaseIncrements logger 10

        let calcValuesStep useMax skipRate ord = ord |> minIncrMaxToValues caps useMax skipRate logger None

        // the rate of a timed or once timed order gets its values once the dose quantity has one
        // value, and only while the rate is still a range
        let rateStage useMax (ord: Order) =
            if
                ord |> rateWaits
                && ord.Orderable.Dose.Quantity |> Quantity.toOrdVar |> OrderVariable.isSolved
                && ord.Orderable.Dose |> Dose.rateIsRange
            then
                ord |> calcValuesStep useMax false
            else
                Ok ord

        // a rate list made when the scenario is selected or after a pick gets its median, as the
        // norm dose gets the median dose quantity, so the order solves; a rate that already has a
        // list, as after a reset or a recalculation, keeps it to pick from
        let rateStageWithMedian useMax (ord: Order) =
            if ord.Orderable.Dose |> Dose.rateIsRange |> not then
                Ok ord
            else
                ord
                |> rateStage useMax
                |> Result.bind (fun ord ->
                    if ord.Orderable.Dose |> Dose.rateIsRange then
                        Ok ord
                    else
                        let dos = { ord.Orderable.Dose with Rate = ord.Orderable.Dose.Rate |> Rate.setMedianValue }
                        let medOrd = { ord with Orderable = { ord.Orderable with Dose = dos } }

                        // a list of one rate is its own median and needs no solve
                        if medOrd = ord then
                            Ok ord
                        else
                            medOrd |> solveOrder "Median Rate" true logger
                )

        let solveStep ord = solveOrder "Solve Order Step" true logger ord

        let pickNearestHigherElseLowerComponentQuantityStep ord =
            ord |> pickNearestHigherElseLowerComponentQuantity logger

        let finalSolveStep =
            {
                Name = "final-solve"
                Guard = (_.OrderIsSolved >> not)
                Run = solveStep
            }

        // the values ensured, solved, and the component quantity picked, as a solve ends; the rate
        // list is made once the dose quantity has one value, with its median
        let solveSteps ord =
            [
                {
                    Name = "solve-order: ensure-values-1"
                    Guard = (_.HasValues >> not)
                    Run = calcValuesStep (ord |> hasAtMostTwoComponents) (ord |> rateWaits)
                }
                finalSolveStep
                {
                    Name = "solve-order: pick-cmp-qty"
                    Guard = (fun _ -> true)
                    Run = pickNearestHigherElseLowerComponentQuantityStep
                }
                {
                    Name = "solve-order: calc-rate-values"
                    Guard = (fun _ -> true)
                    Run = rateStageWithMedian (ord |> hasAtMostTwoComponents)
                }
            ]

        match cmd with
        // the order program runs the calculations, through processPipelineWith
        | CalcMinMax _
        | CalcValues _
        | ReCalcValues _ -> invalidArg (nameof cmd) "the order program runs this command"
        | IncreaseIncrements ord ->
            [
                {
                    Name = "increase-increment: increase-increment"
                    Guard = (fun _ -> true)
                    Run = increaseIncrementStep
                }
            ]
            |> runPipeline ord

        | SolveOrder ord -> ord |> solveSteps |> runPipeline ord

        | ChangeProperty(ord, cmd) -> //ord |> processChangeProperty cmd |> Ok
            [
                {
                    Name = $"change-property: {cmd}"
                    Guard = (fun _ -> true)
                    Run = processChangeProperty logger cmd >> Ok
                }
                {
                    Name = "change-property: solve-minmax"
                    Guard = (fun _ -> true)
                    Run = calcMinMaxStep
                }
            ]
            |> runPipeline ord


    module OrderVariableReference =


        /// The variable a reference names in an order; None when the order has no such variable.
        let tryGet (ord: Order) ovarRef =
            let tryComponent c = ord.Orderable |> Orderable.tryComponent c

            let tryItem c i = tryComponent c |> Option.bind (Orderable.Component.tryItem i)

            match ovarRef with
            | OrderVariableReference.OrderAdjust -> ord.Adjust |> Quantity.toOrdVar |> Some
            | OrderVariableReference.ScheduleFrequency ->
                ord.Schedule |> Schedule.getFrequency |> Option.map Frequency.toOrdVar
            | OrderVariableReference.ScheduleTime ->
                match ord.Schedule with
                | Continuous tme
                | OnceTimed tme
                | Timed(_, tme) -> tme |> Time.toOrdVar |> Some
                | _ -> None
            | OrderVariableReference.OrderableQuantity -> ord.Orderable.OrderableQuantity |> Quantity.toOrdVar |> Some
            | OrderVariableReference.OrderableDoseCount -> ord.Orderable.DoseCount |> Count.toOrdVar |> Some
            | OrderVariableReference.OrderableOrderCount -> ord.Orderable.OrderCount |> Count.toOrdVar |> Some
            | OrderVariableReference.OrderDuration -> ord.Duration |> Time.toOrdVar |> Some
            | OrderVariableReference.OrderableOrderQuantity -> ord.Orderable.OrderQuantity |> Quantity.toOrdVar |> Some
            | OrderVariableReference.OrderableDose p -> ord.Orderable.Dose |> Dose.toOrdVar p |> Some
            | OrderVariableReference.ComponentOrderQuantity c ->
                tryComponent c |> Option.map (_.OrderQuantity >> Quantity.toOrdVar)
            | OrderVariableReference.ComponentOrderCount c ->
                tryComponent c |> Option.map (_.OrderCount >> Count.toOrdVar)
            | OrderVariableReference.ComponentQuantity c ->
                tryComponent c |> Option.map (_.ComponentQuantity >> Quantity.toOrdVar)
            | OrderVariableReference.ComponentOrderableQuantity c ->
                tryComponent c |> Option.map (_.OrderableQuantity >> Quantity.toOrdVar)
            | OrderVariableReference.ComponentOrderableCount c ->
                tryComponent c |> Option.map (_.OrderableCount >> Count.toOrdVar)
            | OrderVariableReference.ComponentOrderableConcentration c ->
                tryComponent c
                |> Option.map (_.OrderableConcentration >> Concentration.toOrdVar)
            | OrderVariableReference.ComponentDose(c, p) -> tryComponent c |> Option.map (_.Dose >> Dose.toOrdVar p)
            | OrderVariableReference.ItemComponentQuantity(c, i) ->
                tryItem c i |> Option.map (_.ComponentQuantity >> Quantity.toOrdVar)
            | OrderVariableReference.ItemComponentConcentration(c, i) ->
                tryItem c i |> Option.map (_.ComponentConcentration >> Concentration.toOrdVar)
            | OrderVariableReference.ItemOrderableQuantity(c, i) ->
                tryItem c i |> Option.map (_.OrderableQuantity >> Quantity.toOrdVar)
            | OrderVariableReference.ItemOrderableConcentration(c, i) ->
                tryItem c i |> Option.map (_.OrderableConcentration >> Concentration.toOrdVar)
            | OrderVariableReference.ItemDose(c, i, p) -> tryItem c i |> Option.map (_.Dose >> Dose.toOrdVar p)


        /// Whether a variable is basic for the schedule, one the other variables of the order are
        /// calculated from: the frequency when the schedule has one, the dose quantity unless the
        /// order runs continuously, the rate when the schedule has a time, the orderable quantity of
        /// each component, and the order count unless the order is given once.
        let isBasic sch ovarRef =
            match ovarRef with
            | OrderVariableReference.ScheduleFrequency -> sch |> Schedule.hasFrequency
            | OrderVariableReference.OrderableDose DoseProperty.Quantity -> sch |> Schedule.isContinuous |> not
            | OrderVariableReference.OrderableDose DoseProperty.Rate -> sch |> Schedule.hasTime
            | OrderVariableReference.ComponentOrderableQuantity _ -> true
            | OrderVariableReference.OrderableOrderCount -> not (sch |> Schedule.isOnce || sch |> Schedule.isOnceTimed)
            | _ -> false


        /// The invariants: the adjustment, the content of each component, the content and
        /// concentration of each of its items, and on Continuous the dose count.
        let invariants (ord: Order) =
            let name = WrappedString.Name.toString
            let cmps = ord.Orderable.Components

            [
                OrderVariableReference.OrderAdjust

                for c in cmps do
                    OrderVariableReference.ComponentQuantity(c.Name |> name)

                for c in cmps do
                    for i in c.Items do
                        OrderVariableReference.ItemComponentQuantity(c.Name |> name, i.Name |> name)
                        OrderVariableReference.ItemComponentConcentration(c.Name |> name, i.Name |> name)

                if ord.Schedule |> Schedule.isContinuous then
                    OrderVariableReference.OrderableDoseCount
            ]


    /// Complete orders: every variable holds one value and every equation holds. A search fixes the
    /// free variables one at a time and solves the order for min and max after each value, so a value
    /// that no complete order reaches is dropped before the search tries it.
    module CompleteOrder =

        open Informedica.GenUnits.Lib


        /// The most solves one search may take.
        let maxSolves = 1000


        /// The names of the basic variables and the invariants, without the order count: the order
        /// count has no maximum, so every multiple of a total over the order is reached.
        let freeNames (ord: Order) =
            [
                OrderVariableReference.ScheduleFrequency
                OrderVariableReference.OrderableDose DoseProperty.Quantity
                OrderVariableReference.OrderableDose DoseProperty.Rate
                yield! ord |> components |> List.map OrderVariableReference.ComponentOrderableQuantity
            ]
            |> List.filter (OrderVariableReference.isBasic ord.Schedule)
            |> fun basics -> basics @ (ord |> OrderVariableReference.invariants)
            |> List.choose (OrderVariableReference.tryGet ord)
            |> List.map OrderVariable.getName


        /// The variable of an order with the name.
        let byName ord name =
            ord |> toOrdVars |> List.find (OrderVariable.getName >> (=) name)


        /// The variable with the values of the value unit as a list.
        let withValues vu (ovar: OrderVariable) =
            { ovar with Variable = ovar.Variable |> Informedica.GenOrder.Lib.Variable.replaceValuesWithValueUnit vu }


        /// The variable set to each of its values, or None when the variable holds a range that cannot
        /// be listed.
        let singles (ovar: OrderVariable) =
            ovar.Variable.Values
            |> Variable.ValueRange.tryToValueSet 100000
            |> Option.map (fun vs ->
                let vu = vs |> Variable.ValueRange.ValueSet.toValueUnit
                let u = vu |> ValueUnit.getUnit

                [
                    for v in vu |> ValueUnit.getValue -> ovar |> withValues (v |> ValueUnit.singleWithUnit u)
                ]
            )


        /// Whether the messages of a failed solve prove that the order cannot hold the values it was
        /// solved with. A loop limit, a value set too large to list or an unexpected exception proves
        /// nothing.
        let rec proves msgs =
            msgs
            |> List.forall (
                function
                | Informedica.GenSolver.Lib.Types.Exceptions.SolverTooManyLoops _
                | Informedica.GenSolver.Lib.Types.Exceptions.ValueSetOverflow _
                | Informedica.GenSolver.Lib.Types.Exceptions.UnexpectedException _ -> false
                | Informedica.GenSolver.Lib.Types.Exceptions.SolverErrored(_, ms, _) -> ms |> proves
                | _ -> true
            )


        /// The outcome of a search.
        type Search =
            {
                /// The complete orders found.
                Complete: Order list
                /// The solves the search took.
                Solves: int
                /// False when the search stopped at maxSolves, or on a variable that holds a range
                /// that cannot be listed.
                Finished: bool
            }


        /// The order with the variable fixed to the value, solved for min and max.
        let setAndSolve logger (ovar: OrderVariable) ord =
            ord
            |> mapOrdVars (fun o -> if o.Variable.Name = ovar.Variable.Name then ovar else o)
            |> solveMinMax "Complete order" false logger


        /// Every complete order the order allows, or the first one when firstOnly holds.
        let search logger firstOnly ord =
            let names = ord |> freeNames

            let isDone acc =
                acc.Solves >= maxSolves || firstOnly && acc.Complete |> List.isEmpty |> not

            let rec go acc ord =
                match names |> List.map (byName ord) |> List.filter (OrderVariable.hasOneValue >> not) with
                | [] -> { acc with Complete = ord :: acc.Complete }
                | unsolved ->
                    match unsolved |> List.map singles |> List.sequenceOption with
                    | None -> { acc with Finished = false }
                    | Some lists ->
                        // the variable with the fewest values first, so the search branches as late as possible
                        lists
                        |> List.minBy List.length
                        |> List.fold
                            (fun acc v ->
                                if acc |> isDone then
                                    { acc with Finished = acc.Finished && acc.Solves < maxSolves }
                                else
                                    let acc = { acc with Solves = acc.Solves + 1 }

                                    match ord |> setAndSolve logger v with
                                    | Ok o -> go acc o
                                    | Error(_, msgs) when msgs |> proves -> acc
                                    // a failure that proves nothing leaves the branch undecided
                                    | Error _ -> { acc with Finished = false }
                            )
                            acc

            go
                {
                    Complete = []
                    Solves = 0
                    Finished = true
                }
                ord


        /// The first complete order that sets the variable to the value.
        let tryFindWith logger ovar ord =
            ord
            |> setAndSolve logger ovar
            |> Result.toOption
            |> Option.bind (search logger true >> _.Complete >> List.tryHead)


        /// Whether no complete order sets the variable to the value: the solve with the value fails
        /// in a way that proves it, or a search that tries every branch finds no complete order. A
        /// failure that proves nothing, or a search that stops at maxSolves or at a range it cannot
        /// list, decides nothing, so the value is not impossible.
        let isProvenImpossible logger ovar ord =
            match ord |> setAndSolve logger ovar with
            | Error(_, msgs) -> msgs |> proves
            | Ok o ->
                let s = o |> search logger true
                s.Complete |> List.isEmpty && s.Finished


        /// The variable with the values a search proves impossible dropped from its list, one search
        /// per value, run in parallel; a value a search cannot decide stays. The variable as it is when
        /// it holds no list of more than one value, and None when every value of the list is proven
        /// impossible.
        let dropProvenImpossible logger ord (ovar: OrderVariable) =
            match ovar |> singles with
            | Some(_ :: _ :: _ as ss) ->
                let keep s = ord |> isProvenImpossible logger s |> not

                match
                    ss
                    |> List.toArray
                    |> Array.Parallel.choose (fun s -> if keep s then Some s else None)
                with
                | [||] -> None
                | kept ->
                    let vu =
                        kept
                        |> Array.toList
                        |> List.choose OrderVariable.solvedValue
                        |> ValueUnit.collectValues

                    ovar |> withValues vu |> Some
            | _ -> Some ovar


    module Choice =


        /// The value a choice selects from the current range of a variable.
        let select ovarRef choice (ovar: OrderVariable) =
            let notSettable reason = Error(ActionFailure.NotSettable(ovarRef, reason))

            // the median of a range with a value count is its value at half the count, as in a list
            let median (ovar: OrderVariable) =
                match ovar |> OrderVariable.shape, ovar.Variable |> Variable.valueCount with
                | Shape.SelectableRange, Some n -> ovar |> OrderVariable.setNthValue (n / 2)
                | _ -> ovar |> OrderVariable.setMedianValue

            let fromList ovar =
                let take f = ovar |> f |> OrderVariable.solvedValue |> Ok

                match choice with
                | Choice.Min -> take OrderVariable.setMinValue
                | Choice.Max -> take OrderVariable.setMaxValue
                | Choice.Median -> take median
                | Choice.Nth n -> take (OrderVariable.setNthValue n)
                | Choice.Percentile p when ovarRef = OrderVariableReference.OrderableDose DoseProperty.Quantity ->
                    take (OrderVariable.setPercValue p)
                | Choice.Percentile _ -> notSettable "a percentile is only for the orderable dose quantity"
                | Choice.Step _ -> notSettable "a range is not steppable"
                |> Result.bind (
                    function
                    | Some vu -> Ok vu
                    | None -> Error(ActionFailure.ValueNotInRange ovarRef)
                )

            match choice, ovar |> OrderVariable.shape with
            // a field that holds one value keeps it, so the order is solved again unchanged
            | (Choice.Min | Choice.Max | Choice.Median | Choice.Nth 0), _ when ovar |> OrderVariable.hasOneValue ->
                ovar |> fromList
            | _, Shape.SelectableList -> ovar |> fromList
            // a percentile counts the increments from min to max, not the values of a pruned list
            | Choice.Percentile _, Shape.SelectableRange -> ovar |> fromList
            // a range with a value count is read from its min, increment and max, without writing out
            // its values, which can be more than a value set holds; as in a list, no nth value lies
            // outside the count
            | Choice.Nth n, Shape.SelectableRange when
                ovar.Variable |> Variable.valueCount |> Option.exists (fun c -> n < 0 || n >= c)
                ->
                Error(ActionFailure.ValueNotInRange ovarRef)
            | _, Shape.SelectableRange when ovar.Variable |> Variable.valueCount |> Option.isSome -> ovar |> fromList
            | _, Shape.SelectableRange -> ovar |> OrderVariable.minIncrMaxToValues [] None |> fromList
            | Choice.Step(n, incr), Shape.Steppable ->
                // a step is not bounded by the constraints; it never goes below one increment
                let useCalc = incr = StepIncrement.Calculated

                let step =
                    if n >= 0 then
                        OrderVariable.increase useCalc n
                    else
                        OrderVariable.decrease useCalc -n

                match ovar |> step |> OrderVariable.solvedValue with
                | Some vu -> Ok vu
                | None -> Error(ActionFailure.ValueNotInRange ovarRef)
            | _, Shape.Steppable -> notSettable "one value only steps"
            | _, Shape.NotSettable -> notSettable "the range offers nothing to choose"


    module SetBackTo =


        /// The order variable set back to the target; the orderable quantity of the order bounds
        /// DefinedUpToOrderable.
        let apply target (ord: Order) ovar =
            match target with
            | SetBackTo.Calculated -> ovar |> OrderVariable.applyCalculatedConstraints
            | SetBackTo.CalculatedElseDefined -> ovar |> OrderVariable.applyConstraints
            | SetBackTo.Defined -> ovar |> OrderVariable.applyDefinedConstraints
            | SetBackTo.DefinedUpToOrderable ->
                ovar
                |> OrderVariable.applyDefinedConstraints
                |> OrderVariable.setMaxToValueOf (ord.Orderable.OrderableQuantity |> Quantity.toOrdVar)
            | SetBackTo.MinIncr -> ovar |> OrderVariable.applyOnlyMinIncrConstraints
            | SetBackTo.AnyPositive -> ovar |> OrderVariable.free
            | SetBackTo.One -> ovar |> OrderVariable.setToOne
            | SetBackTo.AtLeastOne -> ovar |> OrderVariable.setToAtLeastOne


    module OrderCondition =


        /// Whether the condition holds on the order.
        let holds condition ord =
            match condition with
            | OrderCondition.DoseSolved -> ord |> doseIsSolved
            | OrderCondition.OrderSolved -> ord |> isSolved
            | OrderCondition.HasValues -> ord |> hasValues
            | OrderCondition.CanSetNormDose -> ord |> canSetNormDose


    module OrderEffect =

        open Informedica.GenUnits.Lib


        /// A failure of a solve or calculation as the failure of an effect: the order it failed on,
        /// with the messages of the solver.
        let ofSolve result =
            result
            |> Result.mapError (fun (ord, msgs) -> ord, ActionFailure.SolveFailed msgs)


        /// The order solved for min and max, or with values.
        let solve logger mode ord =
            match mode with
            | SolveMode.MinMax -> ord |> solveMinMax "Order effect" true logger
            | SolveMode.Values -> ord |> solveOrder "Order effect" true logger
            |> ofSolve


        /// The result of f on the variable a reference names, or NoSuchVariable when the order has no
        /// such variable.
        let withVar ovarRef ord f =
            match ovarRef |> OrderVariableReference.tryGet ord with
            | None -> Error(ord, ActionFailure.NoSuchVariable ovarRef)
            | Some ovar -> ovar |> f


        /// The basic variables made lists with the cap, including the values of the original order.
        let enumerate caps logger cap rate original ord =
            ord
            |> minIncrMaxToValues caps (cap = ValueCap.Larger) (rate = RateList.WithoutRate) logger original
            |> ofSolve


        /// The rate list of a timed order once its dose quantity holds one value; with SelectMedianRate
        /// a rate that became a list gets its median and the order is solved.
        let rateValues caps logger cap selection original (ord: Order) =
            let rateList ord =
                if
                    ord |> rateWaits
                    && ord.Orderable.Dose.Quantity |> Quantity.toOrdVar |> OrderVariable.isSolved
                    && ord.Orderable.Dose |> Dose.rateIsRange
                then
                    ord |> enumerate caps logger cap RateList.WithRate original
                else
                    Ok ord

            match selection with
            | RateSelection.OfferRates -> ord |> rateList
            | RateSelection.SelectMedianRate when ord.Orderable.Dose |> Dose.rateIsRange |> not -> Ok ord
            | RateSelection.SelectMedianRate ->
                ord
                |> rateList
                |> Result.bind (fun ord ->
                    // a rate the list was not made for, or a list of one rate, is kept as it is
                    if ord.Orderable.Dose |> Dose.rateIsList |> not then
                        Ok ord
                    else
                        { ord with Order.Orderable.Dose.Rate = ord.Orderable.Dose.Rate |> Rate.setMedianValue }
                        |> solveOrder "Median Rate" true logger
                        |> ofSolve
                )


        /// The names of the variables the kept variables do not determine, without the ones in stays.
        let undetermined kept stays ord =
            let names refs =
                refs
                |> List.choose (OrderVariableReference.tryGet ord)
                |> List.map OrderVariable.getName
                |> Set.ofList

            (ord |> Determination.undeterminedBy (names kept)) - names stays


        /// The names of the variables an effect sets back, with the target; None for an effect that sets
        /// nothing back, and NoSuchVariable for a reference the order does not have.
        let setsBack ord effect =
            match effect with
            | OrderEffect.SetAllOrderVariableRangesBack target ->
                Ok(Some(ord |> toOrdVars |> List.map OrderVariable.getName |> Set.ofList, target))
            | OrderEffect.SetOrderVariableRangeBack(ovarRef, target) ->
                withVar ovarRef ord (fun ovar -> Ok(Some(Set.singleton (ovar |> OrderVariable.getName), target)))
            | OrderEffect.SetBackUndetermined(kept, stays, target) -> Ok(Some(ord |> undetermined kept stays, target))
            | _ -> Ok None


        /// The names of the variables the effects set back.
        let setBackNames ord effects =
            effects
            |> List.choose (setsBack ord >> Result.toOption >> Option.flatten)
            |> List.map fst
            |> Set.unionMany


        /// Runs one effect; a failure keeps the order the effect was run on, as a failed solve returns the
        /// order it partly solved.
        let rec apply caps logger effect ord =
            ord |> applyEffect caps logger effect |> Result.mapError (fun (_, f) -> ord, f)


        /// Runs one effect; a failure returns the order the effect failed on.
        and applyEffect caps logger effect (ord: Order) =
            match effect with
            | OrderEffect.SetAllOrderVariableRangesBack _
            | OrderEffect.SetOrderVariableRangeBack _
            | OrderEffect.SetBackUndetermined _ ->
                match setsBack ord effect with
                | Error f -> Error f
                | Ok None -> Ok ord
                | Ok(Some(names, target)) ->
                    ord
                    |> mapOrdVars (fun ovar ->
                        if names |> Set.contains (ovar |> OrderVariable.getName) then
                            ovar |> SetBackTo.apply target ord
                        else
                            ovar
                    )
                    |> Ok
            | OrderEffect.ConstrainOrderVariableRange(ovarRef, vu) ->
                withVar
                    ovarRef
                    ord
                    (fun ovar ->
                        match ovar |> OrderVariable.tryConstrain vu with
                        | Some ovar -> Ok(ord |> fromOrdVars [ ovar ])
                        | None -> Error(ord, ActionFailure.ValueNotInRange ovarRef)
                    )
            | OrderEffect.SelectOrderVariableValue(ovarRef, choice) ->
                withVar
                    ovarRef
                    ord
                    (fun ovar ->
                        match ovar |> Choice.select ovarRef choice with
                        | Ok vu -> ord |> apply caps logger (OrderEffect.ConstrainOrderVariableRange(ovarRef, vu))
                        | Error f -> Error(ord, f)
                    )
            | OrderEffect.SolveOrder mode -> ord |> solve logger mode
            | OrderEffect.EnumerateOrderVariableValues(cap, rate, original) ->
                ord |> enumerate caps logger cap rate original
            | OrderEffect.EnumerateRateValues(cap, selection, original) ->
                ord |> rateValues caps logger cap selection original
            | OrderEffect.CoarsenIncrements maxCount -> ord |> increaseIncrements logger maxCount |> ofSolve
            | OrderEffect.RecordCalculatedConstraints -> ord |> setCalculatedConstraints |> Ok
            | OrderEffect.SetNormDose -> ord |> solveNormDose logger |> ofSolve
            | OrderEffect.SelectNearestComponentQuantity ->
                ord |> pickNearestHigherElseLowerComponentQuantity logger |> ofSolve
            // with one dose count every value a solve keeps is reached, so there is nothing to drop
            | OrderEffect.DropImpossibleValues _ when
                ord.Orderable.DoseCount |> Count.toOrdVar |> OrderVariable.hasOneValue
                ->
                Ok ord
            | OrderEffect.DropImpossibleValues ovarRef ->
                withVar
                    ovarRef
                    ord
                    (fun ovar ->
                        match ovar |> CompleteOrder.dropProvenImpossible logger ord with
                        | Some ovar -> ord |> fromOrdVars [ ovar ] |> solve logger SolveMode.Values
                        | None -> Error(ord, ActionFailure.ValueNotInRange ovarRef)
                    )
            | OrderEffect.Unless(conditions, eff) ->
                if conditions |> List.exists (fun c -> ord |> OrderCondition.holds c) then
                    Ok ord
                else
                    ord |> apply caps logger eff
            | OrderEffect.Try effects ->
                match ord |> run caps logger effects with
                | Ok o -> Ok o
                | Error(_, f) ->
                    // the order is kept, so the failure is logged instead of returned
                    $"Try kept the order, the effects failed: %A{f}"
                    |> Events.OrderScenario
                    |> Logging.logWarning logger

                    Ok ord


        /// Runs the effects in sequence; the first failure stops them.
        and run caps logger effects ord =
            (Ok ord, effects)
            ||> List.fold (fun acc eff -> acc |> Result.bind (apply caps logger eff))


        /// The effects for the order of one combination of concentrations: a list includes the
        /// values of the original order only when they belong to that combination, so a value of one
        /// concentration is not included in another.
        let forConcentration (ord: Order) effs =
            let invariants =
                ord
                |> OrderVariableReference.invariants
                |> List.choose (OrderVariableReference.tryGet ord)
                |> OrderVariable.byName

            // a held value belongs when the invariant of this combination allows it
            let belongs (held: OrderVariable) =
                match invariants |> Map.tryFind held.Variable.Name with
                | None -> true
                | Some ovar ->
                    match held |> OrderVariable.getValSetValueUnit, ovar.DefinedConstraints.Values with
                    | Some h, Some vs ->
                        vs
                        |> Variable.ValueRange.ValueSet.toValueUnit
                        |> ValueUnit.intersect h
                        |> ValueUnit.isEmpty
                        |> not
                    | _ -> true

            let originalBelongs original =
                original
                |> toOrdVars
                |> List.filter OrderVariable.hasOneValue
                |> List.forall belongs

            effs
            |> List.map (
                function
                | OrderEffect.EnumerateOrderVariableValues(cap, rate, Some original) when
                    original |> originalBelongs |> not
                    ->
                    OrderEffect.EnumerateOrderVariableValues(cap, rate, None)
                | OrderEffect.EnumerateRateValues(cap, selection, Some original) when original |> originalBelongs |> not ->
                    OrderEffect.EnumerateRateValues(cap, selection, None)
                | eff -> eff
            )


    module SetCommand =


        /// How the variable a reference names can be set: a list is selectable; a min, increment and
        /// max is a range that ofSet takes a value from only on a basic variable, since the range of a
        /// derived variable can hold values no complete order reaches; one value with a defined
        /// increment is steppable, on a basic variable only.
        let settable sch ovarRef ovar =
            match ovar |> OrderVariable.shape with
            | Shape.Steppable when ovarRef |> OrderVariableReference.isBasic sch |> not -> Shape.NotSettable
            | shape -> shape


        /// What a set does with its choice, given the shape of the variable. A step on the frequency,
        /// the dose quantity, the dose rate or a component orderable quantity, and a percentile of the
        /// dose quantity, run a recipe that first sets back the variables that depend on the set
        /// variable, so they can follow the new value. A min, max, median or nth sets only the value:
        /// from a list on any variable, from a min, increment and max only on a basic variable. On a
        /// variable that is steppable or not settable a min, max, median or nth still sets only the
        /// value: Choice.select then refuses it with the reason the shape gives no value. On Continuous
        /// a min, max or median of the dose quantity gives no command: there the dose quantity is not a
        /// basic variable, and the rate has its own min, max and median.
        let ofSet ovarRef sch shape choice =
            match ovarRef, choice with
            | _, Choice.Step _ when shape <> Shape.Steppable -> SetCommand.NoCommand
            | OrderVariableReference.OrderableDose DoseProperty.Quantity, (Choice.Min | Choice.Max | Choice.Median) when
                sch |> Schedule.isContinuous
                ->
                SetCommand.NoCommand
            | OrderVariableReference.ScheduleFrequency, Choice.Step _ ->
                SetCommand.WithRecipe Recipe.ScheduleFrequencyStep
            | OrderVariableReference.OrderableDose DoseProperty.Quantity, Choice.Step _ ->
                SetCommand.WithRecipe Recipe.OrderableDoseQuantityStep
            // on Continuous the dose quantity is not basic, so it has no percentile
            | OrderVariableReference.OrderableDose DoseProperty.Quantity, Choice.Percentile _ when
                ovarRef |> OrderVariableReference.isBasic sch
                ->
                SetCommand.WithRecipe Recipe.OrderableDoseQuantityPercentile
            | OrderVariableReference.OrderableDose DoseProperty.Rate, Choice.Step _ ->
                SetCommand.WithRecipe Recipe.OrderableDoseRateStep
            | OrderVariableReference.ComponentOrderableQuantity c, Choice.Step _ ->
                SetCommand.WithRecipe(Recipe.ComponentOrderableQuantityStep c)
            | _, (Choice.Min | Choice.Max | Choice.Median | Choice.Nth _) when
                shape = Shape.SelectableRange
                && ovarRef |> OrderVariableReference.isBasic sch |> not
                ->
                SetCommand.NoCommand
            | _, (Choice.Min | Choice.Max | Choice.Median | Choice.Nth _) -> SetCommand.ValueOnly
            | _ -> SetCommand.NoCommand


    /// The recipes as effects. A recipe reads the order only for its conditions and for the names of
    /// its components.
    module Recipe =


        /// The preparation a recipe keeps: the orderable quantity of each component.
        let keptPreparation ord =
            ord |> components |> List.map OrderVariableReference.ComponentOrderableQuantity


        /// Every variable the kept variables do not determine freed to any positive value, but the
        /// variables in stays, such as the target of a step, which the step gives its value.
        let freeUndetermined kept stays =
            OrderEffect.SetBackUndetermined(kept, stays, SetBackTo.AnyPositive)


        /// The variable set back to the target.
        let setFloor ovarRef target = OrderEffect.SetOrderVariableRangeBack(ovarRef, target)


        /// The rate a step keeps, on a schedule with an administration time.
        let keptRate (ord: Order) =
            [
                if ord.Schedule |> Schedule.hasTime then
                    OrderVariableReference.OrderableDose DoseProperty.Rate
            ]


        /// The step of the frequency: keeps the dose quantity, the preparation, the rate, the order
        /// count and the invariants, so the doses per time follow the frequency.
        let scheduleFrequencyStep ord =
            [
                freeUndetermined
                    [
                        OrderVariableReference.OrderableDose DoseProperty.Quantity
                        yield! ord |> keptPreparation
                        yield! ord |> keptRate
                        OrderVariableReference.OrderableOrderCount
                        yield! ord |> OrderVariableReference.invariants
                    ]
                    [ OrderVariableReference.ScheduleFrequency ]
            ]


        /// The step of the dose quantity: keeps the frequency, the rate, the order count, the
        /// invariants and, with more than one component, the preparation. The dose count goes to 1
        /// with one component, to at least 1 with more. The orderable quantity of a single component
        /// is freed with the rest, and so keeps its increment.
        let orderableDoseQuantityStep ord =
            let isSingle = ord |> components |> List.length = 1

            [
                freeUndetermined
                    [
                        OrderVariableReference.ScheduleFrequency
                        yield! ord |> keptRate
                        OrderVariableReference.OrderableOrderCount

                        if not isSingle then
                            yield! ord |> keptPreparation

                        yield! ord |> OrderVariableReference.invariants
                    ]
                    [ OrderVariableReference.OrderableDose DoseProperty.Quantity ]

                if isSingle then
                    setFloor OrderVariableReference.OrderableDoseCount SetBackTo.One
                else
                    setFloor OrderVariableReference.OrderableDoseCount SetBackTo.AtLeastOne
            ]


        /// The percentile of the dose quantity: keeps the frequency, the preparation, the order count
        /// and the invariants. The dose quantity goes to its defined constraints up to the orderable
        /// quantity, its adjust and the rate to their defined constraints, the dose count to at least
        /// 1, and on a schedule with a time the time to its calculated constraints.
        let orderableDoseQuantityPercentile (ord: Order) =
            let dose = OrderVariableReference.OrderableDose

            [
                freeUndetermined
                    [
                        OrderVariableReference.ScheduleFrequency
                        yield! ord |> keptPreparation
                        OrderVariableReference.OrderableOrderCount
                        yield! ord |> OrderVariableReference.invariants
                    ]
                    []

                if ord.Schedule |> Schedule.hasTime then
                    setFloor OrderVariableReference.ScheduleTime SetBackTo.Calculated

                setFloor (dose DoseProperty.Quantity) SetBackTo.DefinedUpToOrderable
                setFloor (dose DoseProperty.QuantityAdjust) SetBackTo.Defined
                setFloor (dose DoseProperty.Rate) SetBackTo.Defined
                setFloor OrderVariableReference.OrderableDoseCount SetBackTo.AtLeastOne
            ]


        /// The step of the dose rate: keeps the frequency, the dose quantity, the preparation, the
        /// order count and the invariants, so the time and the rates of the components and items
        /// follow the rate.
        let orderableDoseRateStep ord =
            [
                freeUndetermined
                    [
                        OrderVariableReference.ScheduleFrequency
                        OrderVariableReference.OrderableDose DoseProperty.Quantity
                        yield! ord |> keptPreparation
                        OrderVariableReference.OrderableOrderCount
                        yield! ord |> OrderVariableReference.invariants
                    ]
                    [ OrderVariableReference.OrderableDose DoseProperty.Rate ]
            ]


        /// The step of the orderable quantity of the component name: keeps the frequency, the
        /// orderable quantities of the other components, the rate, the order count, the invariants
        /// and, with one component, the dose quantity. The orderable quantity goes to its min and
        /// increment; with more than one component the dose quantity to its min and increment and
        /// the dose count to 1.
        let componentOrderableQuantityStep name ord =
            let isSingle = ord |> components |> List.length = 1

            [
                freeUndetermined
                    [
                        OrderVariableReference.ScheduleFrequency

                        for c in ord |> components do
                            if BCL.String.equalsCapInsens c name |> not then
                                OrderVariableReference.ComponentOrderableQuantity c

                        yield! ord |> keptRate
                        OrderVariableReference.OrderableOrderCount

                        if isSingle then
                            OrderVariableReference.OrderableDose DoseProperty.Quantity

                        yield! ord |> OrderVariableReference.invariants
                    ]
                    [ OrderVariableReference.ComponentOrderableQuantity name ]

                setFloor OrderVariableReference.OrderableQuantity SetBackTo.MinIncr

                if not isSingle then
                    setFloor (OrderVariableReference.OrderableDose DoseProperty.Quantity) SetBackTo.MinIncr
                    setFloor OrderVariableReference.OrderableDoseCount SetBackTo.One
            ]


        /// The variables of refs a recalculation keeps: only the ones that lie within their calculated
        /// constraints, so a variable a step moved outside its constraints is set back.
        let keptWithinConstraints ord refs =
            refs
            |> List.filter (
                OrderVariableReference.tryGet ord
                >> Option.exists (OrderVariable.isWithinConstraints true)
            )


        /// Every variable the kept variables do not determine set back to its calculated constraints.
        let setBackUndetermined ord kept =
            OrderEffect.SetBackUndetermined(kept |> keptWithinConstraints ord, [], SetBackTo.Calculated)


        /// The recalculation of the kind Dose: keeps the frequency, the order count and the
        /// invariants. The preparation is never kept: a kept preparation limits the dose to the
        /// preparation divided by the dose count, a single value when the dose count holds one value.
        let reCalcDose ord =
            [
                [
                    OrderVariableReference.ScheduleFrequency
                    OrderVariableReference.OrderableOrderCount
                    yield! ord |> OrderVariableReference.invariants
                ]
                |> setBackUndetermined ord
            ]


        /// The recalculation of the kind Per time: keeps what the kind Dose keeps, without the
        /// frequency, since a per time is the dose quantity times the frequency. With one component
        /// whose dose quantity the definition fixes, its calculated constraints holding one value, only
        /// the product can change the per time, so the product invariants of that component are not
        /// kept either.
        let reCalcPerTime (ord: Order) =
            let name = WrappedString.Name.toString

            let isQuantityFixed =
                ord.Orderable.Dose.Quantity
                |> Quantity.toOrdVar
                |> OrderVariable.hasOneCalculatedValue

            let product =
                match ord.Orderable.Components with
                | [ c ] when isQuantityFixed ->
                    [
                        OrderVariableReference.ComponentQuantity(c.Name |> name)

                        for i in c.Items do
                            OrderVariableReference.ItemComponentQuantity(c.Name |> name, i.Name |> name)
                            OrderVariableReference.ItemComponentConcentration(c.Name |> name, i.Name |> name)
                    ]
                | _ -> []

            [
                [
                    OrderVariableReference.OrderableOrderCount
                    yield! ord |> OrderVariableReference.invariants |> List.except product
                ]
                |> setBackUndetermined ord
            ]


        /// The recalculation of the kind Time: keeps the frequency, the order count and the invariants;
        /// the dose quantity unless the preparation is out of its constraints or the time is fixed; the
        /// preparation unless it is free: out of its constraints, or a fixed time, since a fixed time
        /// ties the dose to the rate and a dose count that holds one value ties the preparation to the
        /// dose.
        let reCalcTime ord =
            let isTimeFixed = ord.Schedule |> Schedule.isTimeFixed
            let isPrepFree = ord |> isPrepWithinConstraints |> not || isTimeFixed

            [
                [
                    OrderVariableReference.ScheduleFrequency
                    OrderVariableReference.OrderableOrderCount

                    // on Continuous the dose quantity is the preparation, which this recipe keeps or frees
                    if ord.Schedule |> Schedule.isContinuous |> not && not isPrepFree then
                        OrderVariableReference.OrderableDose DoseProperty.Quantity

                    if not isPrepFree then
                        yield! ord |> keptPreparation

                    yield! ord |> OrderVariableReference.invariants
                ]
                |> setBackUndetermined ord
            ]


        /// The effects of a recipe on the order.
        let effects recipe ord =
            match recipe with
            | Recipe.ScheduleFrequencyStep -> ord |> scheduleFrequencyStep
            | Recipe.OrderableDoseQuantityStep -> ord |> orderableDoseQuantityStep
            | Recipe.OrderableDoseQuantityPercentile -> ord |> orderableDoseQuantityPercentile
            | Recipe.OrderableDoseRateStep -> ord |> orderableDoseRateStep
            | Recipe.ComponentOrderableQuantityStep c -> ord |> componentOrderableQuantityStep c
            | Recipe.ReCalcDose -> ord |> reCalcDose
            | Recipe.ReCalcPerTime -> ord |> reCalcPerTime
            | Recipe.ReCalcTime -> ord |> reCalcTime


        /// The recipe a recalculation of the variable runs, per dose type: the kind Dose, Per time or
        /// Time that recalculates the variable; None for a variable no kind recalculates, which includes
        /// the dose of an item of a component the rules do not constrain.
        let forReCalcCommand ovarRef (ord: Order) =
            let isUnconstrained =
                match ovarRef with
                | OrderVariableReference.ItemDose(c, _, _) ->
                    ord.Orderable
                    |> Orderable.tryComponent c
                    |> Option.exists Orderable.Component.isDoseConstrained
                    |> not
                | _ -> false

            // the orderable dose quantity and rate have no command on the wire; the program
            // recalculates them for its own actions
            let kind =
                match ovarRef with
                | OrderVariableReference.OrderableDose DoseProperty.Quantity
                | OrderVariableReference.ItemDose(_, _, DoseProperty.Quantity)
                | OrderVariableReference.ItemDose(_, _, DoseProperty.QuantityAdjust) -> Some Recipe.ReCalcDose
                | OrderVariableReference.ItemDose(_, _, DoseProperty.PerTime)
                | OrderVariableReference.ItemDose(_, _, DoseProperty.PerTimeAdjust) -> Some Recipe.ReCalcPerTime
                | OrderVariableReference.ScheduleTime
                | OrderVariableReference.OrderableDose DoseProperty.Rate
                | OrderVariableReference.ItemDose(_, _, DoseProperty.Rate)
                | OrderVariableReference.ItemDose(_, _, DoseProperty.RateAdjust) -> Some Recipe.ReCalcTime
                | _ -> None

            match kind, ord.Schedule with
            | _ when isUnconstrained -> None
            | Some Recipe.ReCalcDose, (Once | Discontinuous _ | Timed _ | OnceTimed _)
            | Some Recipe.ReCalcPerTime, (Discontinuous _ | Timed _)
            | Some Recipe.ReCalcTime, (Continuous _ | Timed _ | OnceTimed _) -> kind
            | _ -> None


    module ActionFailure =

        /// The messages of the solver of a failure; none for a refusal.
        let toMessages =
            function
            | ActionFailure.SolveFailed msgs -> msgs
            | _ -> []


    module OrderAction =

        /// The effects of an action on an order, decided by the server.
        let effects action (ord: Order) =
            let cap =
                if ord |> hasAtMostTwoComponents then
                    ValueCap.Larger
                else
                    ValueCap.Default

            let rate =
                if ord |> rateWaits then
                    RateList.WithoutRate
                else
                    RateList.WithRate

            // the lists made where missing, solved with values, the component quantity picked and
            // the rate list made, as a solve with values ends
            let values selection original =
                [
                    OrderEffect.Unless(
                        [ OrderCondition.HasValues ],
                        OrderEffect.EnumerateOrderVariableValues(cap, rate, None)
                    )
                    OrderEffect.Unless([ OrderCondition.OrderSolved ], OrderEffect.SolveOrder SolveMode.Values)
                    OrderEffect.SelectNearestComponentQuantity
                    OrderEffect.EnumerateRateValues(cap, selection, original)
                ]

            match action with
            | OrderAction.Set(ovarRef, choice) ->
                // an nth is solved with values, as a selection from a list is; on an order without lists a
                // solve for min and max first carries the value through, because the lists are made with a cap
                // that thins them, and a list thinned before the narrowing can miss the values the set value
                // needs; a min, max or median on an order with lists is solved with values, because a solve
                // for min and max keeps every listed value within the new bounds, also the values the set
                // value no longer reaches; the other choices are solved for min and max
                let solve =
                    match choice with
                    | Choice.Nth _ when ord |> hasValues -> values RateSelection.SelectMedianRate None
                    | Choice.Nth _ ->
                        OrderEffect.SolveOrder SolveMode.MinMax
                        :: values RateSelection.SelectMedianRate None
                    | Choice.Min
                    | Choice.Max
                    | Choice.Median when ord |> hasValues -> [ OrderEffect.SolveOrder SolveMode.Values ]
                    | _ -> [ OrderEffect.SolveOrder SolveMode.MinMax ]

                let setValue recipe ovar =
                    // the value is selected from the range as it is before the recipe; a stepped
                    // value replaces the one held, any other value narrows the range
                    let replace =
                        match choice with
                        | Choice.Step _ -> [ OrderEffect.SetOrderVariableRangeBack(ovarRef, SetBackTo.AnyPositive) ]
                        | _ -> []

                    ovar
                    |> Choice.select ovarRef choice
                    |> Result.map (fun vu ->
                        recipe
                        @ replace
                        @ [ OrderEffect.ConstrainOrderVariableRange(ovarRef, vu) ]
                        @ solve
                    )

                match ovarRef |> OrderVariableReference.tryGet ord with
                | None -> Error(ActionFailure.NoSuchVariable ovarRef)
                | Some ovar ->
                    match
                        SetCommand.ofSet ovarRef ord.Schedule (ovar |> SetCommand.settable ord.Schedule ovarRef) choice
                    with
                    | SetCommand.NoCommand -> Error(ActionFailure.NoRecipe action)
                    // a percentile of a dose that is not part of a composition the user set changes nothing
                    | SetCommand.WithRecipe Recipe.OrderableDoseQuantityPercentile when ord |> isComposed |> not ->
                        Ok []
                    // a percentile is selected from the range its recipe gives
                    | SetCommand.WithRecipe Recipe.OrderableDoseQuantityPercentile ->
                        Ok
                            [
                                yield! ord |> Recipe.effects Recipe.OrderableDoseQuantityPercentile
                                OrderEffect.SelectOrderVariableValue(ovarRef, choice)
                                yield! solve
                            ]
                    | SetCommand.WithRecipe recipe -> ovar |> setValue (ord |> Recipe.effects recipe)
                    | SetCommand.ValueOnly -> ovar |> setValue []
            | OrderAction.ReCalc ovarRef ->
                match ovarRef |> OrderVariableReference.tryGet ord, Recipe.forReCalcCommand ovarRef ord with
                | None, _ -> Error(ActionFailure.NoSuchVariable ovarRef)
                | _, None -> Error(ActionFailure.NoRecipe action)
                | Some _, Some recipe ->
                    // after a dose the increments are coarsened before the lists are made; the lists include
                    // the values the order held when the action received it, so the order the user had
                    // stays on offer; the rate list is left to select from
                    Ok
                        [
                            yield! ord |> Recipe.effects recipe
                            OrderEffect.SolveOrder SolveMode.MinMax
                            if recipe = Recipe.ReCalcDose || recipe = Recipe.ReCalcPerTime then
                                OrderEffect.CoarsenIncrements 100
                            OrderEffect.EnumerateOrderVariableValues(ValueCap.Larger, rate, Some ord)
                            yield! values RateSelection.OfferRates (Some ord)
                            OrderEffect.SolveOrder SolveMode.Values
                            OrderEffect.DropImpossibleValues ovarRef
                        ]
            | OrderAction.CalcConstraints ->
                Ok
                    [
                        OrderEffect.SetAllOrderVariableRangesBack SetBackTo.CalculatedElseDefined
                        OrderEffect.SolveOrder SolveMode.MinMax
                        OrderEffect.CoarsenIncrements 10
                        OrderEffect.RecordCalculatedConstraints
                        // the norm dose needs dose values; a failure to make them keeps the order
                        if ord |> hasNormDose && ord |> hasAtMostTwoComponents then
                            OrderEffect.Unless(
                                [ OrderCondition.CanSetNormDose ],
                                OrderEffect.Try
                                    [ OrderEffect.EnumerateOrderVariableValues(ValueCap.Larger, rate, None) ]
                            )

                            OrderEffect.SetNormDose
                    ]
            | OrderAction.Reset ->
                // the creation wrote calculated constraints for every variable, inside its defined ones
                Ok
                    [
                        OrderEffect.SetAllOrderVariableRangesBack SetBackTo.Calculated
                        OrderEffect.EnumerateOrderVariableValues(cap, rate, None)
                        if ord |> rateWaits then
                            OrderEffect.EnumerateRateValues(cap, RateSelection.OfferRates, None)
                        OrderEffect.Unless([ OrderCondition.OrderSolved ], OrderEffect.SolveOrder SolveMode.Values)
                    ]
            | OrderAction.CalcValues ->
                // an order with more than two components keeps its ranges, and a solved order is left as it is
                let unsolved eff =
                    OrderEffect.Unless([ OrderCondition.DoseSolved; OrderCondition.OrderSolved ], eff)

                Ok
                    [
                        if ord |> hasAtMostTwoComponents then
                            unsolved (OrderEffect.EnumerateOrderVariableValues(ValueCap.Default, rate, None))

                            if ord |> rateWaits then
                                unsolved (
                                    OrderEffect.EnumerateRateValues(
                                        ValueCap.Default,
                                        RateSelection.SelectMedianRate,
                                        None
                                    )
                                )
                    ]


        /// The most combinations of concentrations an order is solved for one by one; past it the
        /// order is solved once, with every concentration.
        let maxConcentrations = 16


        /// The most values the union of the orders per concentration makes a list of; past it a
        /// range.
        let maxUnionValues = 200


        /// Runs an action; a failure keeps the order before the action. When a component has more
        /// than one concentration, the action runs once per combination of concentrations, in
        /// parallel, with the effects decided on the order as it is, and the order offers the union
        /// of those that succeed, so no value of one concentration is combined with a value of
        /// another. When that union cannot be listed, the action runs on the whole order.
        let run caps logger action ord =
            match ord |> effects action with
            | Error f -> Error(ord, f)
            | Ok effs ->
                let setBack = OrderEffect.setBackNames ord effs
                let setsBack (ovar: OrderVariable) = setBack |> Set.contains ovar.Variable.Name
                let runOne o =
                    o |> OrderEffect.run caps logger (effs |> OrderEffect.forConcentration o)

                let runWhole () =
                    ord
                    |> OrderEffect.run caps logger effs
                    |> Result.mapError (fun (_, f) -> ord, f)

                match ord |> splitPerConcentration maxConcentrations setsBack with
                | [ o ] -> o |> runOne |> Result.mapError (fun (_, f) -> ord, f)
                | ords ->
                    let results = ords |> List.toArray |> Array.Parallel.map runOne |> Array.toList

                    match results |> List.choose Result.toOption with
                    | [] -> results |> List.head |> Result.mapError (fun (_, f) -> ord, f)
                    // a union that becomes a range would offer values no concentration allows, so the
                    // order runs whole
                    | oks when oks |> isListedUnion maxUnionValues |> not -> runWhole ()
                    | oks ->
                        let records = effs |> List.contains OrderEffect.RecordCalculatedConstraints
                        Ok(oks |> unionPerConcentration maxUnionValues records ord)


    /// The property commands as actions of the order program, while the order context still sends
    /// them.
    module CommandBridge =

        /// The action of the program a property command is; None for the component in stock, which
        /// the pipeline of the command still processes.
        let ofChangeProperty cmd =
            let step n useCalc =
                Choice.Step(
                    n,
                    (if useCalc then
                         StepIncrement.Calculated
                     else
                         StepIncrement.Defined)
                )

            let set ovarRef choice = Some(OrderAction.Set(ovarRef, choice))
            let reCalc ovarRef = Some(OrderAction.ReCalc ovarRef)
            let frq = OrderVariableReference.ScheduleFrequency
            let tme = OrderVariableReference.ScheduleTime
            let orbQty = OrderVariableReference.OrderableQuantity
            let qty = OrderVariableReference.OrderableDose DoseProperty.Quantity
            let rte = OrderVariableReference.OrderableDose DoseProperty.Rate
            let cmpQty c = OrderVariableReference.ComponentOrderableQuantity c
            let cmpQtyAdj c =
                OrderVariableReference.ComponentDose(c, DoseProperty.QuantityAdjust)
            let itmDose c i p = OrderVariableReference.ItemDose(c, i, p)
            let itmCmpCnc c i = OrderVariableReference.ItemComponentConcentration(c, i)
            let itmOrbCnc c i = OrderVariableReference.ItemOrderableConcentration(c, i)
            let itmOrbQty c i = OrderVariableReference.ItemOrderableQuantity(c, i)

            match cmd with
            | IncreaseScheduleFrequency -> set frq (step 1 false)
            | DecreaseScheduleFrequency -> set frq (step -1 false)
            | SetMinScheduleFrequency -> set frq Choice.Min
            | SetMaxScheduleFrequency -> set frq Choice.Max
            | SetMedianScheduleFrequency -> set frq Choice.Median
            | IncreaseOrderableDoseQuantity(n, useCalc) -> set qty (step n useCalc)
            | DecreaseOrderableDoseQuantity(n, useCalc) -> set qty (step -n useCalc)
            | SetOrderableDoseQuantityPerc p -> set qty (Choice.Percentile p)
            | SetMinOrderableDoseQuantity -> set qty Choice.Min
            | SetMaxOrderableDoseQuantity -> set qty Choice.Max
            | SetMedianOrderableDoseQuantity -> set qty Choice.Median
            | IncreaseOrderableDoseRate(n, useCalc) -> set rte (step n useCalc)
            | DecreaseOrderableDoseRate(n, useCalc) -> set rte (step -n useCalc)
            | SetMinOrderableDoseRate -> set rte Choice.Min
            | SetMaxOrderableDoseRate -> set rte Choice.Max
            | SetMedianOrderableDoseRate -> set rte Choice.Median
            | IncreaseComponentOrderableQuantity(c, n, useCalc) -> set (cmpQty c) (step n useCalc)
            | DecreaseComponentOrderableQuantity(c, n, useCalc) -> set (cmpQty c) (step -n useCalc)
            | SetMinComponentOrderableQuantity c -> set (cmpQty c) Choice.Min
            | SetMaxComponentOrderableQuantity c -> set (cmpQty c) Choice.Max
            | SetMedianComponentOrderableQuantity c -> set (cmpQty c) Choice.Median
            | SetNthScheduleFrequency n -> set frq (Choice.Nth n)
            | SetNthScheduleTime n -> set tme (Choice.Nth n)
            | SetNthOrderableQuantity n -> set orbQty (Choice.Nth n)
            | SetNthOrderableDoseQuantity n -> set qty (Choice.Nth n)
            | SetNthOrderableDoseRate n -> set rte (Choice.Nth n)
            | SetNthComponentOrderableQuantity(c, n) -> set (cmpQty c) (Choice.Nth n)
            | SetNthComponentDoseQuantityAdjust(c, n) -> set (cmpQtyAdj c) (Choice.Nth n)
            | SetNthItemDoseQuantity(c, i, n) -> set (itmDose c i DoseProperty.Quantity) (Choice.Nth n)
            | SetNthItemDoseQuantityAdjust(c, i, n) -> set (itmDose c i DoseProperty.QuantityAdjust) (Choice.Nth n)
            | SetNthItemDosePerTime(c, i, n) -> set (itmDose c i DoseProperty.PerTime) (Choice.Nth n)
            | SetNthItemDosePerTimeAdjust(c, i, n) -> set (itmDose c i DoseProperty.PerTimeAdjust) (Choice.Nth n)
            | SetNthItemDoseRate(c, i, n) -> set (itmDose c i DoseProperty.Rate) (Choice.Nth n)
            | SetNthItemDoseRateAdjust(c, i, n) -> set (itmDose c i DoseProperty.RateAdjust) (Choice.Nth n)
            | SetNthItemComponentConcentration(c, i, n) -> set (itmCmpCnc c i) (Choice.Nth n)
            | SetNthItemOrderableConcentration(c, i, n) -> set (itmOrbCnc c i) (Choice.Nth n)
            | SetNthItemOrderableQuantity(c, i, n) -> set (itmOrbQty c i) (Choice.Nth n)
            | ReCalcScheduleTime -> reCalc tme
            | ReCalcOrderableQuantity -> reCalc orbQty
            | ReCalcComponentDoseQuantityAdjust c -> reCalc (cmpQtyAdj c)
            | ReCalcItemDoseQuantity(c, i) -> reCalc (itmDose c i DoseProperty.Quantity)
            | ReCalcItemDoseQuantityAdjust(c, i) -> reCalc (itmDose c i DoseProperty.QuantityAdjust)
            | ReCalcItemDosePerTime(c, i) -> reCalc (itmDose c i DoseProperty.PerTime)
            | ReCalcItemDosePerTimeAdjust(c, i) -> reCalc (itmDose c i DoseProperty.PerTimeAdjust)
            | ReCalcItemDoseRate(c, i) -> reCalc (itmDose c i DoseProperty.Rate)
            | ReCalcItemDoseRateAdjust(c, i) -> reCalc (itmDose c i DoseProperty.RateAdjust)
            | ReCalcItemOrderableConcentration(c, i) -> reCalc (itmOrbCnc c i)
            | ReCalcItemOrderableQuantity(c, i) -> reCalc (itmOrbQty c i)
            | ComponentInStock _ -> None


        /// The order and the action of the program a command is; None for a command the pipeline
        /// of the command still processes.
        let ofOrderCommand =
            function
            | CalcMinMax ord -> Some(ord, OrderAction.CalcConstraints)
            | CalcValues ord -> Some(ord, OrderAction.CalcValues)
            | ReCalcValues ord -> Some(ord, OrderAction.Reset)
            | ChangeProperty(ord, cmd) -> cmd |> ofChangeProperty |> Option.map (fun action -> ord, action)
            | IncreaseIncrements _
            | SolveOrder _ -> None


    /// Process an order by the command given: a command the order program has an action for runs as
    /// that action, any other command through its pipeline. A refusal of the program has no messages.
    let processPipelineWith caps logger cmd =
        match cmd |> CommandBridge.ofOrderCommand with
        | Some(ord, action) ->
            ord
            |> OrderAction.run caps logger action
            |> Result.mapError (fun (o, f) -> o, f |> ActionFailure.toMessages)
        | None -> cmd |> processCommandWith caps logger


    /// Process an order by the command given, pruned with the default caps.
    let processPipeline logger cmd = processPipelineWith ValueCaps.defaults logger cmd
