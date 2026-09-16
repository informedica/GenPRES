namespace Informedica.GenOrder.Lib


module OrderLogging =

    open System

    open Informedica.GenSolver.Lib
    open Informedica.GenUnits.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenOrder.Lib
    open Informedica.Logging.Lib

    open Types.Logging

    module Units = ValueUnit.Units
    module Quantity = OrderVariable.Quantity
    module Name = Variable.Name
    module Mapping = EquationMapping


    /// Convenience functions for logging order events
    let logOrderEvent (logger: Logger) (event: Events.Event) = event |> OrderEventMessage |> Logging.logInfo logger


    let logOrderWarning (logger: Logger) (event: Events.Event) = event |> OrderEventMessage |> Logging.logWarning logger


    let logOrderEventError (logger: Logger) (event: Events.Event) =
        event |> OrderEventMessage |> Logging.logError logger


    let logOrderException (logger: Logger) (ex: Exceptions.Message) = ex |> OrderException |> Logging.logError logger


    let printOrderEqs logger (o: Order) eqs =
        let toEqString op vs =
            vs
            |> List.sortBy (fun vs -> vs |> List.head)
            |> List.map (fun vs ->
                match vs with
                | h :: tail ->
                    let s = tail |> List.map (OrderVariable.toString false) |> String.concat op
                    $"{h |> OrderVariable.toString false} = {s}"
                | _ -> ""
            )
            |> String.concat "\n"

        let (Id s) = o.Id
        let s = $"{s}."

        let mapping =
            match o.Schedule with
            | Continuous _ -> Mapping.Literals.continuous
            | Once -> Mapping.Literals.once
            | OnceTimed _ -> Mapping.Literals.onceTimed
            | Discontinuous _ -> Mapping.Literals.discontinuous
            | Timed _ -> Mapping.Literals.timed
            |> Mapping.getEquations
            |> Mapping.getEqsMapping o

        try
            eqs
            |> Solver.mapToOrderEqs (o |> Order.mapToOrderEquations mapping)
            |> List.map (fun e ->
                match e with
                | OrderProductEquation(ovar, ovars)
                | OrderSumEquation(ovar, ovars) -> ovar :: ovars
            )
            |> fun xs ->
                $"""
        {(xs |> toEqString " * ").Replace(s, "")}
        {(xs |> toEqString " + ").Replace(s, "")}
        """
        with e ->
            $"error printing: {e}" |> Events.OrderScenario |> logOrderEventError logger
            ""


    let printOrderEvent =
        function
        | Events.MinIncrMaxToValues ovar ->
            $"OrderVariable to values {ovar.Variable |> Variable.count}: {ovar |> OrderVariable.toString true}"

        | Events.SolverReplaceUnit(n, u) -> $"replaced {n |> Name.toString} unit with {u |> ValueUnit.unitToString}"

        | Events.OrderSolveStarted o -> $"=== Order ({o.Orderable.Name |> Name.toString}) Solver Started ==="

        | Events.OrderSolveFinished o -> $"=== Order ({o.Orderable.Name |> Name.toString}) Solver Finished ==="

        | Events.OrderSolveConstraintsStarted(o, cs) ->
            $"=== Order ({o.Orderable.Name |> Name.toString}) Solver Constraints Started ({cs.Length} constraints) ==="

        | Events.OrderSolveConstraintsFinished(o, cs) ->
            $"=== Order ({o.Orderable.Name |> Name.toString}) Solver Constraints Finished ({cs.Length} constraints) ==="

        | Events.OrderScenario s -> s

        | Events.OrderScenarioWithNameValue(o, n, v) ->
            let (Id oid) = o.Id
            $"Scenario {oid}: {n |> Name.toString} = {v}"

        | Events.MedicationCreated m -> $"Medication created:\n\n{m}\n"

        | Events.ComponentItemsHarmonized s -> s

        | Events.OrderIncreaseQuantityIncrement _ -> $"increased quantity increment"

        | Events.OrderIncreaseRateIncrement _ -> $"increased rate increment"


    let printOrderException =
        function
        | Exceptions.OrderCouldNotBeSolved(s, o) ->
            $"Order could not be solved: {s} for order {o.Orderable.Name |> Name.toString}"
        | Exceptions.OrderCouldNotBeCreated exn -> $"Order could not be created:\n%A{exn}"


    /// Format order messages using the IMessage interface
    let formatOrderMessage (msg: IMessage) : string =
        match msg with
        | :? OrderMessage as orderMsg ->
            match orderMsg with
            | OrderException ex -> ex |> printOrderException
            | OrderEventMessage evt -> evt |> printOrderEvent
        | :? Informedica.GenForm.Lib.Types.Message as formMsg ->
            Informedica.GenForm.Lib.FormLogging.formatMessage formMsg
        | _ -> $"Unknown message type: {msg.GetType().Name}"


    /// Enhanced print function that can handle messages with context
    let printOrderMsgWithContext logger (msgs: ResizeArray<float * Event> option) (msg: Event) =
        match msg.Message with
        | :? OrderMessage as m ->
            match m with
            | OrderException(Exceptions.OrderCouldNotBeCreated exn) -> $"Order couldn not be created:\n{exn}"
            | OrderException(Exceptions.OrderCouldNotBeSolved(s, o)) ->
                $"""
printing error for order {o.Orderable.Name |> Name.toString}
messages: {msgs.Value.Count}
"""
                |> Events.OrderScenario
                |> logOrderEventError logger

                let eqs =
                    match msgs with
                    | Some msgs ->
                        msgs
                        |> Array.ofSeq
                        |> Array.choose (fun (_, m) ->
                            match m.Message with
                            | :? SolverMessage as solverMsg ->
                                match solverMsg with
                                | SolverMessage.ExceptionMessage m ->
                                    match m with
                                    | Informedica.GenSolver.Lib.Types.Exceptions.SolverErrored(_, _, eqs) -> Some eqs
                                    | _ -> None
                                | _ -> None
                            | _ -> None
                        )
                        |> fun xs ->
                            $"found {xs |> Array.length}" |> Events.OrderScenario |> logOrderEvent logger
                            xs
                        |> Array.tryHead
                    | None -> None

                match eqs with
                | Some eqs ->
                    let s = $"Terminated with {s}:\n{printOrderEqs logger o eqs}"
                    $"%s{s}" |> Events.OrderScenario |> logOrderEvent logger
                    s
                | None ->
                    let s = $"Terminated with {s}"
                    $"%s{s}" |> Events.OrderScenario |> logOrderEvent logger
                    s
            | OrderEventMessage evt -> evt |> printOrderEvent
        | _ ->
            $"printMsg cannot handle {msg}"
            |> Events.OrderScenario
            |> logOrderEventError logger

            ""


    /// Backward compatibility - create a logger that matches the old interface
    let create (f: string -> unit) =
        let formatter =
            MessageFormatter.create
                [
                    typeof<OrderMessage>, formatOrderMessage
                    typeof<SolverMessage>, SolverLogging.formatSolverMessage
                ]

        {
            Log =
                fun event ->
                    event.Message
                    |> formatter
                    |> fun s ->
                        if s |> String.notNullOrEmpty then
                            f s
            Enabled = fun _ -> true
        }


    /// A logger that does nothing
    let noOp = Logging.noOp


    /// <summary>
    /// Prints the scenarios for a given list of orders
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="verbose">Also print the Order</param>
    /// <param name="ns">The items to print</param>
    /// <param name="orders">The list of Orders</param>
    let printScenarios logger verbose ns (orders: Order list) =
        let w =
            match orders with
            | h :: _ -> h.Adjust |> Quantity.toValueUnitStringList |> Option.defaultValue ""
            | _ -> ""

        $"\n\n=== SCENARIOS for Weight: %s{w} ==="
        |> Events.OrderScenario
        |> logOrderEvent logger

        orders
        |> List.iteri (fun i o ->
            o
            |> Order.Print.printOrderToString true ns
            |> fun (p, a, d) ->
                $"%i{i + 1}\tprescription:\t%s{p}"
                |> Events.OrderScenario
                |> logOrderEvent logger

                $"  \tdispensing:\t%s{a}" |> Events.OrderScenario |> logOrderEvent logger
                $"  \tpreparation:\t%s{d}" |> Events.OrderScenario |> logOrderEvent logger

            if verbose then
                o
                |> Order.toString
                |> List.iteri (fun i s -> $"%i{i + 1}\t%s{s}" |> Events.OrderScenario |> logOrderEvent logger)

                "\n" |> Events.OrderScenario |> logOrderEvent logger
        )
