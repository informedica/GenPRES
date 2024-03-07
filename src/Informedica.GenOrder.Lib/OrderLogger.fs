namespace Informedica.GenOrder.Lib

open Informedica.Utils.Lib



module OrderLogger =

    open System
    open System.Diagnostics

    open Informedica.GenSolver.Lib

    open Informedica.GenUnits.Lib
    open Informedica.GenOrder.Lib


    module Units = ValueUnit.Units
    module Quantity = OrderVariable.Quantity

    module SolverLogging = SolverLogging

    type Logger = Informedica.GenSolver.Lib.Types.Logging.Logger
    type SolverMessage = Informedica.GenSolver.Lib.Types.Logging.SolverMessage
    type OrderMessage = Logging.OrderMessage


    type Agent<'Msg> = MailboxProcessor<'Msg>
    type IMessage = Informedica.GenSolver.Lib.Types.Logging.IMessage
    type Level = Informedica.GenSolver.Lib.Types.Logging.Level

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
        let s = s + "."

        let mapping =
            match o.Prescription with
            | Continuous -> Order.Mapping.continuous
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
            ConsoleWriter.writeErrorMessage $"error printing: {e.ToString()}" true false
            ""


    // To print all messages related to an order
    let printOrderMsg (msgs : ResizeArray<float * Informedica.GenSolver.Lib.Types.Logging.Message> option) msg =
        match msg with
        | Logging.OrderEvent m ->
            match m with
            | Events.SolverReplaceUnit (n, u) ->
                $"replaced {n |> Name.toString} unit with {u |> ValueUnit.unitToString}"

            | Events.OrderSolveStarted o -> $"=== Order ({o.Orderable.Name |> Name.toString}) Solver Started ==="

            | Events.OrderSolveFinished o -> $"=== Order ({o.Orderable.Name |> Name.toString}) Solver Finished ==="

            | Events.OrderScenario _ -> ""

            | _ -> ""

        | Logging.OrderException (Exceptions.OrderCouldNotBeSolved(s, o)) ->
            ConsoleWriter.writeErrorMessage $"""
printing error for order {o.Orderable.Name}
messages: {msgs.Value.Count}
"""
                true false

            let eqs =
                match msgs with
                | Some msgs ->
                    msgs
                    |> Array.ofSeq
                    |> Array.choose (fun (_, m) ->
                        match m.Message with
                        | :? SolverMessage as solverMsg ->
                            match solverMsg with
                            | Informedica.GenSolver.Lib.Types.Logging.ExceptionMessage m ->
                                match m with
                                | Informedica.GenSolver.Lib.Types.Exceptions.SolverErrored (_, _, eqs) ->
                                    Some eqs
                                | _ -> None
                            | _ -> None
                        | _ -> None
                    )
                    |> fun xs ->
                        printfn $"found {xs |> Array.length}"; xs
                    |> Array.tryHead
                | None -> None
            match eqs with
            | Some eqs ->
                let s = $"Terminated with {s}:\n{printOrderEqs o eqs}"
                printfn $"%s{s}"
                s
            | None ->
                let s = $"Terminated with {s}"
                printfn $"%s{s}"
                s



    // Catches a message and will dispatch this to the appropriate
    // print function
    let printMsg (msgs : ResizeArray<float * Informedica.GenSolver.Lib.Types.Logging.Message> option) (msg : Informedica.GenSolver.Lib.Types.Logging.Message) =
        match msg.Message with
        | :? SolverMessage as m -> m |> SolverLogging.printMsg
        | :? OrderMessage  as m -> m |> printOrderMsg msgs
        | _ ->
            ConsoleWriter.writeErrorMessage $"printMsg cannot handle {msg}" true false
            ""

    // A message to send to the order logger agent
    type Message =
        | Start of path: string option * Level
        | Received of Informedica.GenSolver.Lib.Types.Logging.Message
        | Report
        | Write of string
        | Stop

    // The type for an order logger agent that will
    // catch a message and will process this in an asynchronous way.
    type OrderLogger =
        {
            Start : string option -> Level -> unit
            Logger: Logger
            Report: unit -> unit
            Write : string -> unit
            Stop : unit -> unit
        }


    /// A logger that does nothing
    let noLogger : Logger = { Log = ignore }


    /// A logger that prints to the console
    let printLogger : Logger = { Log = (printMsg None >> (printfn "%s")) }


    /// The Order logger agent
    let logger =

        let write path i t ms m =
            match path with
            | None -> ()
            | Some p ->
                m
                |> printMsg ms
                |> function
                | s when s |> String.IsNullOrEmpty -> ()
                | s ->
                    let text = [ $"{i}. {t}: {m.Level}"; s ]
                    System.IO.File.AppendAllLines(p, text)

        let loggerAgent : Agent<Message> =
            Agent.Start <| fun inbox ->
                let msgs = ResizeArray<float * Informedica.GenSolver.Lib.Types.Logging.Message>()

                let rec loop (timer : Stopwatch) path level msgs =
                    async {
                        let! msg = inbox.Receive ()

                        match msg with
                        | Stop ->
                            return ()
                        | Start (path, level) ->
                            if path.IsSome then
                                $"Start logging {level}: {DateTime.Now}\n\n"
                                |> File.writeTextToFile path.Value

                            let timer = Stopwatch.StartNew()
                            return!
                                ResizeArray<float * Informedica.GenSolver.Lib.Types.Logging.Message>()
                                |> loop timer path level

                        | Received m ->
                            match level with
                            | Level.Informative ->
                                let t = timer.Elapsed.TotalSeconds
                                let i = msgs.Count
                                write path i t (Some msgs) m

                                msgs.Add(timer.Elapsed.TotalSeconds, m)
                            | _ when m.Level = level || m.Level = Level.Error ->
                                let t = timer.Elapsed.TotalSeconds
                                let i = msgs.Count
                                write path i t (Some msgs) m

                                msgs.Add(timer.Elapsed.TotalSeconds, m)
                            | _ -> ()
                            return! loop timer path level msgs

                        | Report ->
                            printfn "=== Start Report ===\n"
                            msgs
                            |> Seq.length
                            |> printfn "Total messages received: %i\n"

                            msgs
                            |> Seq.iteri (fun i (t, m) ->
                                m
                                |> printMsg (Some msgs)
                                |> function
                                | s when s |> String.IsNullOrEmpty -> ()
                                | s -> printfn $"\n%i{i}. %f{t}: %A{m.Level}\n%s{s}"
                            )
                            printfn "\n"

                            return! loop timer path level msgs

                        | Write path ->
                            msgs
                            |> Seq.iteri (fun i (t, m) -> write (Some path) i t (Some msgs) m)

                            return! loop timer (Some path) level msgs
                    }

                let timer = Stopwatch.StartNew()
                loop timer None Level.Informative msgs

        {
            Start =
                fun path level ->
                    ConsoleWriter.writeInfoMessage
                        $"start logging at level {level}" true false
                    if path.IsSome then
                        ConsoleWriter.writeInfoMessage
                            $"file logging to {path}" true false

                    (path, level)
                    |> Start
                    |> loggerAgent.Post
            Logger = {
                Log =
                    fun msg ->
                        msg
                        |> Received
                        |> loggerAgent.Post
            }
            Report =
                fun _ ->
                    Report
                    |> loggerAgent.Post
            Write =
                fun path ->
                    Write path
                    |> loggerAgent.Post
            Stop = fun () -> Stop |> loggerAgent.Post
        }



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

        printfn $"\n\n=== SCENARIOS for Weight: %s{w} ==="
        orders
        |> List.iteri (fun i o ->
            o
            |> Order.Print.printOrderToString true ns
            |> fun (p, a, d) ->
                printfn $"%i{i + 1}\tprescription:\t%s{p}"
                printfn $"  \tdispensing:\t%s{a}"
                printfn $"  \tpreparation:\t%s{d}"

            if verbose then
                o
                |> Order.toString
                |> List.iteri (fun i s -> printfn $"%i{i + 1}\t%s{s}")

                printfn "\n"
        )

