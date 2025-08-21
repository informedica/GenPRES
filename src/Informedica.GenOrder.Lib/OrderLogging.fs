namespace Informedica.GenOrder.Lib


module OrderLogging =

    open System

    open Informedica.GenSolver.Lib
    open Informedica.GenUnits.Lib
    open Informedica.GenOrder.Lib
    open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime
    open Informedica.Logging.Lib

    open Types.Logging

    module Units = ValueUnit.Units
    module Quantity = OrderVariable.Quantity
    module Name = Variable.Name


    let printOrderEqs (o : Order) eqs =
        let toEqString op vs =
            vs
            |> List.sortBy (fun vs -> vs |> List.head)
            |> List.map (fun vs ->
                match vs with
                | h::tail ->
                    let s =
                        tail
                        |> List.map (OrderVariable.toString false)
                        |> String.concat op
                    $"{h |> OrderVariable.toString false} = {s}"
                | _ -> ""
            )
            |> String.concat "\n"

        let (Id s) = o.Id
        let s = $"{s}."

        let mapping =
            match o.Prescription with
            | Continuous _ -> Order.Mapping.continuous
            | Once -> Order.Mapping.once
            | OnceTimed _ -> Order.Mapping.onceTimed
            | Discontinuous _ -> Order.Mapping.discontinuous
            | Timed _ -> Order.Mapping.timed
            |> Order.Mapping.getEquations
            |> Order.Mapping.getEqsMapping o

        try
            eqs
            |> Solver.mapToOrderEqs (o |> Order.mapToOrderEquations mapping)
            |> List.map (fun e ->
                match e with
                | OrderProductEquation (ovar, ovars)
                | OrderSumEquation (ovar, ovars) -> ovar::ovars
            )
            |> fun xs ->
        $"""
        {(xs |> toEqString " * ").Replace(s, "")}
        {(xs |> toEqString " + ").Replace(s, "")}
        """
        with
        | e ->
            writeErrorMessage $"error printing: {e.ToString()}"
            ""


    let printOrderEvent = function
        | Events.SolverReplaceUnit (n, u) ->
            $"replaced {n |> Name.toString} unit with {u |> ValueUnit.unitToString}"

        | Events.OrderSolveStarted o -> 
            $"=== Order ({o.Orderable.Name |> Name.toString}) Solver Started ==="

        | Events.OrderSolveFinished o -> 
            $"=== Order ({o.Orderable.Name |> Name.toString}) Solver Finished ==="

        | Events.OrderScenario s -> s
        | Events.OrderScenarioWithNameValue (o, n, v) ->
            let (Types.Id oid) = o.Id
            $"Scenario {oid}: {n |> Name.toString} = {v}"

        | _ -> ""


    let printOrderException = function
        | Exceptions.OrderCouldNotBeSolved(s, o) ->
            $"Order could not be solved: {s} for order {o.Orderable.Name |> Name.toString}"


    /// Format order messages using the IMessage interface
    let formatOrderMessage (msg: IMessage) : string =
        match msg with
        | :? OrderMessage as orderMsg ->
            match orderMsg with
            | OrderException ex -> ex |> printOrderException
            | OrderEventMessage evt -> evt |> printOrderEvent
        | :? Logging.SolverMessage as solverMsg ->
            // Delegate to solver logging for solver messages
            SolverLogging.formatSolverMessage solverMsg
        | _ -> $"Unknown message type: {msg.GetType().Name}"


    /// Create an order-specific logger using the general logging framework
    let createLogger (baseLogger: Logger option) =
        let formatter = MessageFormatter.create [
            typeof<OrderMessage>, formatOrderMessage
            typeof<Logging.SolverMessage>, SolverLogging.formatSolverMessage
        ]
        
        match baseLogger with
        | Some logger -> logger
        | None -> Logging.createConsole formatter


    /// Create a file-based order logger
    let createFileLogger (path: string) =
        MessageFormatter.create [
            typeof<OrderMessage>, formatOrderMessage
            typeof<Logging.SolverMessage>, SolverLogging.formatSolverMessage
        ]
        |> Logging.createFile path


    /// Create an agent-based order logger
    let createAgentLogger () =
        MessageFormatter.create [
            typeof<OrderMessage>, formatOrderMessage
            typeof<Logging.SolverMessage>, SolverLogging.formatSolverMessage
        ]
        |> AgentLogging.createWithFormatter


    /// Convenience functions for logging order events
    let logOrderEvent (logger: Logger) (event: Events.Event) =
        event 
        |> OrderEventMessage 
        |> Logging.logInfo logger


    let logOrderWarning (logger: Logger) (event: Events.Event) =
        event 
        |> OrderEventMessage 
        |> Logging.logWarning logger


    let logOrderException (logger: Logger) (ex: Exceptions.Message) =
        ex 
        |> OrderException 
        |> Logging.logError logger


    /// Enhanced print function that can handle messages with context
    let printOrderMsgWithContext (msgs : ResizeArray<float * Event> option) (msg : Event) =
        match msg.Message with
        (*
        | :? Logging.SolverMessage as m -> 
            SolverLogging.formatSolverMessage m
        *)
        | :? OrderMessage as m -> 
            match m with
            | OrderException (Exceptions.OrderCouldNotBeSolved(s, o)) ->
                writeErrorMessage $"""
printing error for order {o.Orderable.Name |> Name.toString}
messages: {msgs.Value.Count}
"""
                let eqs =
                    match msgs with
                    | Some msgs ->
                        msgs
                        |> Array.ofSeq
                        |> Array.choose (fun (_, m) ->
                            match m.Message with
                            | :? Logging.SolverMessage as solverMsg ->
                                match solverMsg with
                                | Logging.SolverMessage.ExceptionMessage m ->
                                    match m with
                                    | Informedica.GenSolver.Lib.Types.Exceptions.SolverErrored (_, _, eqs) ->
                                        Some eqs
                                    | _ -> None
                                | _ -> None
                            | _ -> None
                        )
                        |> fun xs ->
                            writeInfoMessage $"found {xs |> Array.length}"; xs
                        |> Array.tryHead
                    | None -> None
                
                match eqs with
                | Some eqs ->
                    let s = $"Terminated with {s}:\n{printOrderEqs o eqs}"
                    writeInfoMessage $"%s{s}"
                    s
                | None ->
                    let s = $"Terminated with {s}"
                    writeInfoMessage $"%s{s}"
                    s
            | OrderEventMessage evt -> evt |> printOrderEvent
        | _ ->
            writeErrorMessage $"printMsg cannot handle {msg}"
            ""


    /// Backward compatibility - create a logger that matches the old interface
    let create (f: string -> unit) =
        let formatter = MessageFormatter.create [
            typeof<OrderMessage>, formatOrderMessage
            typeof<Logging.SolverMessage>, SolverLogging.formatSolverMessage
        ]
        
        {
            Log = fun event ->
                event.Message
                |> formatter
                |> fun s -> if not (String.IsNullOrEmpty s) then f s
        }


    /// A logger that does nothing
    let ignore = Logging.ignore


    /// A logger that prints to the console
    let printLogger : Logger = 
        let formatter = MessageFormatter.create [
            typeof<OrderMessage>, formatOrderMessage
            typeof<Logging.SolverMessage>, SolverLogging.formatSolverMessage
            typeof<Informedica.GenForm.Lib.Types.Message>, Informedica.GenForm.Lib.FormLogging.formatMessage
        ]
        Logging.createConsole formatter


    let agentLogger =
        let formatter =
            MessageFormatter.create [
                typeof<OrderMessage>, formatOrderMessage
                typeof<Logging.SolverMessage>, SolverLogging.formatSolverMessage
                typeof<Informedica.GenForm.Lib.Types.Message>, Informedica.GenForm.Lib.FormLogging.formatMessage
            ]
        AgentLogging.AgentLoggerDefaults.config
        |> AgentLogging.AgentLoggerDefaults.withFormatter formatter
        |> AgentLogging.AgentLoggerDefaults.withLevel Level.Informative
        |> AgentLogging.AgentLoggerDefaults.withFlushInterval (TimeSpan.FromSeconds 5.)
        |> AgentLogging.AgentLoggerDefaults.withMinFlushInterval (TimeSpan.FromSeconds 1.)
        |> AgentLogging.AgentLoggerDefaults.withMaxFlushInterval (TimeSpan.FromSeconds 60.)
        |> AgentLogging.createAgentLogger


    /// <summary>
    /// Prints the scenarios for a given list of orders
    /// </summary>
    /// <param name="verbose">Also print the Order</param>
    /// <param name="ns">The items to print</param>
    /// <param name="orders">The list of Orders</param>
    let printScenarios verbose ns (orders : Order list) =
        let w =
            match orders with
            | h::_ ->
                h.Adjust
                |> Quantity.toValueUnitStringList
                |> Option.defaultValue ""
            | _ -> ""

        writeInfoMessage $"\n\n=== SCENARIOS for Weight: %s{w} ==="
        orders
        |> List.iteri (fun i o ->
            o
            |> Order.Print.printOrderToString true ns
            |> fun (p, a, d) ->
                writeInfoMessage $"%i{i + 1}\tprescription:\t%s{p}"
                writeInfoMessage $"  \tdispensing:\t%s{a}"
                writeInfoMessage $"  \tpreparation:\t%s{d}"

            if verbose then
                o
                |> Order.toString
                |> List.iteri (fun i s -> writeInfoMessage $"%i{i + 1}\t%s{s}")

                writeInfoMessage "\n"
        )