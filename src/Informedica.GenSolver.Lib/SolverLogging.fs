namespace Informedica.GenSolver.Lib


module SolverLogging =

    open System

    open Types
    open Types.Logging
    open Types.Events
    open Informedica.Logging.Lib

    module Name = Variable.Name
    module ValueRange = Variable.ValueRange


    let private eqsToStr eqs =
        let eqs =
            eqs
            |> List.sortBy (fun e ->
                e
                |> Equation.toVars
                |> List.tryHead
                |> function
                | Some v -> Some v.Name
                | None -> None
            )
        $"""{eqs |> List.map Equation.toStringShort |> String.concat "\n"}"""


    let private varsToStr vars =
        $"""{vars |> List.map Variable.toStringShort |> String.concat ", "}"""


    let rec printException = function
    | Exceptions.ValueRangeEmptyValueSet s ->
        $"ValueRange cannot have an empty value set: {s}"

    | Exceptions.EquationEmptyVariableList ->
        "An equation should at least contain one variable"

    | Exceptions.SolverInvalidEquations eqs ->
        $"The following equations are invalid {eqs |> eqsToStr} "

    | Exceptions.ValueRangeMinLargerThanMax (min, max) ->
        $"{min} is larger than {max}"

    | Exceptions.ValueRangeMinOverFlow min ->
        $"Min overflow: {min}"

    | Exceptions.ValueRangeMaxOverFlow max ->
        $"Max overflow: {max}"

    | Exceptions.ValueRangeNotAValidOperator ->
        "The value range operator was invalid or unknown"

    | Exceptions.EquationDuplicateVariables vars ->
        $"""The list of variables for the equation contains duplicates
{vars |> List.map (Variable.getName >> Name.toString) |> String.concat ", "}
"""

    | Exceptions.NameLongerThan1000 s ->
        $"This name contains more than 1000 chars: {s}"

    | Exceptions.NameNullOrWhiteSpaceException ->
        "A name cannot be a blank string"

    | Exceptions.VariableCannotSetValueRange (var, vlr) ->
        $"This variable:\n{var |> Variable.toString true}\ncannot be set with this range:{vlr |> ValueRange.toString true}\n"

    | Exceptions.SolverTooManyLoops (n, eqs) ->
        $"""Looped (total {n}) more than {Constants.MAX_LOOP_COUNT} times the equation list count ({eqs |> List.length})
{eqs |> eqsToStr}
"""

    | Exceptions.SolverErrored (n, msgs, eqs) ->
        $"=== Solver Errored Solving ({n} loops) ===\n{eqs |> eqsToStr}"
        |> fun s ->
            msgs
            |> List.map (fun msg ->
                match msg with
                | Exceptions.SolverErrored _ -> s
                | _ ->
                        $"Error: {msg |> printException}"
            )
            |> String.concat "\n"
            |> fun es -> $"{s}\n{es}"

    | Exceptions.ValueRangeEmptyIncrement -> "Increment can not be an empty set"

    | Exceptions.ValueSetOverflow c ->
        $"Trying to calculate with {c} values, which is higher than the max calc count {Constants.MAX_CALC_COUNT}"

    | Exceptions.ConstraintVariableNotFound (c, eqs) ->
        $"""=== Constraint Variable not found ===
        {c
        |> sprintf "Constraint %A cannot be set"
        |> fun s ->
           eqs
           |> List.map (Equation.toString true)
           |> String.concat "\n"
           |> sprintf "%s\In equations:\%s" s
        }
        """
    | _ -> "not a recognized msg"


    let printSolverEvent = function
        | EquationStartedSolving eq ->
            $"=== Start solving Equation ===\n{eq |> Equation.toStringShort}"

        | EquationStartCalculation (op1, op2, y, xs) ->
            $"start calculating: {Equation.calculationToString false op1 op2 y xs}"

        | EquationFinishedCalculation (xs, changed) ->
            if not changed then "NO CHANGES"
            else
                $"Changed: {xs |> varsToStr}"

        | EquationFinishedSolving (eq, b) ->
            $"""=== Equation Finished Solving ===
{eq |> Equation.toStringShort}
{b |> Equation.SolveResult.toString}
"""

        | EquationCouldNotBeSolved eq ->
            $"=== Cannot solve Equation ===\n{eq |> Equation.toString false}"

        | SolverStartSolving eqs ->
            $"=== Solver Start Solving ===\n{eqs |> eqsToStr}"

        | SolverLoopedQue (n, eqs) ->
            $"solver looped que {n} times with {eqs |> List.length} equations"

        | SolverFinishedSolving eqs ->
            $"=== Solver Finished Solving ===\n{eqs |> eqsToStr}"

        | ConstraintSortOrder cs ->
            let s =
                cs
                |> List.map (fun (i, c) ->
                    c
                    |> Constraint.toString
                    |> sprintf "%i: %s" i
                )
                |> String.concat "\n"
            $"=== Constraint sort order ===\n{s}"

        | ConstraintApplied c -> $"Constraint {c |> Constraint.toString} applied"

        | ConstrainedSolved c -> $"Constraint {c |> Constraint.toString} solved"


    /// Format solver messages using the IMessage interface
    let formatSolverMessage (msg: IMessage) : string =
        match msg with
        | :? SolverMessage as solverMsg ->
            match solverMsg with
            | ExceptionMessage ex -> ex |> printException
            | SolverEventMessage evt -> evt |> printSolverEvent
        | _ -> $"Unknown message type: {msg.GetType().Name}"


    /// Create a solver-specific logger using the general logging framework
    let createLogger (baseLogger: Logger option) =
        let formatter = MessageFormatter.create [
            typeof<SolverMessage>, formatSolverMessage
        ]
        
        match baseLogger with
        | Some logger -> logger
        | None -> Logging.createConsole formatter


    /// Create a file-based solver logger
    let createFileLogger (path: string) =
        let formatter = MessageFormatter.create [
            typeof<SolverMessage>, formatSolverMessage
        ]
        Logging.createFile path formatter


    /// Create an agent-based solver logger
    let createAgentLogger () =
        let formatter = MessageFormatter.create [
            typeof<SolverMessage>, formatSolverMessage
        ]
        AgentLogging.createWithFormatter formatter


    /// Convenience functions for logging solver events
    let logSolverEvent (logger: Logger) (event: Events.Event) =
        event 
        |> SolverEventMessage 
        |> Logging.logInfo logger


    let logSolverWarning (logger: Logger) (event: Events.Event) =
        event 
        |> SolverEventMessage 
        |> Logging.logWarning logger


    let logSolverException (logger: Logger) (ex: Exceptions.Message) =
        ex 
        |> ExceptionMessage 
        |> Logging.logError logger


    /// Backward compatibility - create a logger that matches the old interface
    let create (f: string -> unit) =
        let formatter = MessageFormatter.create [
            typeof<SolverMessage>, formatSolverMessage
        ]
        
        {
            Log = fun event ->
                event.Message
                |> formatter
                |> fun s -> if not (String.IsNullOrEmpty s) then f s
        }

    /// Ignore logger for backward compatibility
    let noOp = Logging.noOp