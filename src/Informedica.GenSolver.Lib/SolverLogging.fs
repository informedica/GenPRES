namespace Informedica.GenSolver.Lib


module SolverLogging =

    open Informedica.Utils.Lib.BCL

    open Types
    open Types.Logging
    open Types.Events

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
        $"""{eqs |> List.map (Equation.toString false) |> String.concat "\n"}"""


    let private varsToStr b vars =
        $"""{vars |> List.map (Variable.toString b) |> String.concat ", "}"""


    let rec printException = function
    | Exceptions.ValueRangeEmptyValueSet ->
        "ValueRange cannot have an empty value set"

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

    | Exceptions.ValueRangeTooManyValues c ->
        $"Trying to calculate with {c} values, which is higher than the max calc count {Constants.MAX_CALC_COUNT}"

    | Exceptions.ConstraintVariableNotFound (c, eqs) ->
        $"""=== Constraint Variable not found ===
        {c
        |> sprintf "Constraint %A cannot be set"
        |> (fun s ->
            eqs
            |> List.map (Equation.toString true)
            |> String.concat "\n"
            |> sprintf "%s\In equations:\%s" s
        )
        }
        """
    | _ -> "not a recognized msg"

    let printMsg = function
    | ExceptionMessage m ->
        m
        |> printException
    | SolverMessage m ->
        let toString eq =
            let op = if eq |> Equation.isProduct then " * " else " + "
            let varName = Variable.getName >> Variable.Name.toString

            match eq |> Equation.toVars with
            | [] -> ""
            | [ _ ] -> ""
            | y::xs ->
                $"""{y |> varName } = {xs |> List.map varName |> String.concat op}"""


        match m with
        | EquationStartedSolving eq ->
            $"=== Start solving Equation ===\n{eq |> toString}"

        | EquationStartCalculation (op1, op2, y, x, xs) ->
            $"start calculating: {Equation.calculationToString true op1 op2 y x xs}"

        | EquationFinishedCalculation (xs, changed) ->
            if not changed then "No Changes"
            else
                $"Changed: {xs |> varsToStr true}"

        | EquationFinishedSolving (eq, b) ->
            $"""=== Equation Finished Solving ===
{eq |> Equation.toString false}
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


    let logger f =
        {
            Log =
                fun { TimeStamp = _; Level = _; Message = msg } ->
                    match msg with
                    | :? Logging.SolverMessage as m ->
                        m |> printMsg |> f
                    | _ -> $"cannot print msg: {msg}" |> f

        }
