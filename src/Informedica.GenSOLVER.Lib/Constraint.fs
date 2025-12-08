namespace Informedica.GenSolver.Lib



module Constraint =

    open Types

    module ValueRange = Variable.ValueRange
    module Property = ValueRange.Property
    module ValueSet = ValueRange.ValueSet
    module Name = Variable.Name


    /// <summary>
    /// Check whether constraint c1 has the same name as constraint c2.
    /// </summary>
    let eqsName (c1 : Constraint) (c2 : Constraint) = c1.Name = c2.Name


    /// <summary>
    /// Print the constraint as a string
    /// </summary>
    let toString { Name = n; Property = p } = $"{n |> Name.toString}: {p}"


    /// <summary>
    /// Give a constraint a score based on the property.
    /// Low scores should be solved first.
    /// </summary>
    /// <param name="c">The constraint</param>
    let scoreConstraint c =
            match c.Property with
            | ValsProp vs ->
                let n = vs |> ValueSet.count
                if n = 1 then    -3, c
                else              n, c
            | MinProp _   -> -5, c
            | IncrProp _      -> -4, c
            | _               -> -2, c


    /// <summary>
    /// Order constraints based on their score.
    /// </summary>
    /// <param name="log">The logger to log operations</param>
    /// <param name="cs">The list of constraints</param>
    let orderConstraints log cs =
        cs
        // calc min and max from valsprop constraints
        |> List.fold (fun acc c ->
            match c.Property with
            | ValsProp vs ->
                if vs |> ValueSet.count <= 1 then [c] |> List.append acc
                else
                    let min = vs |> ValueSet.getMin |> Option.map MinProp
                    let max = vs |> ValueSet.getMax |> Option.map MaxProp
                    [
                        c
                        if min.IsSome then { c with Property = min.Value }
                        if max.IsSome then { c with Property = max.Value }
                    ]
                    |> List.append acc
            | _ -> [c] |> List.append acc
        ) []
        |> List.fold (fun acc c ->
            if acc |> List.exists ((=) c) then acc
            else
                acc @ [c]
        ) []
        |> fun cs -> cs |> List.map scoreConstraint
        |> List.sortBy fst
        |> fun cs ->
            cs
            |> Events.ConstraintSortOrder
            |> Logger.logInfo log

            cs
            |> List.map snd


    /// <summary>
    /// Apply a constraint to the matching variables
    /// in the list of equations.
    /// </summary>
    /// <param name="log">The logger</param>
    /// <param name="c">The constraint</param>
    /// <param name="eqs">The list of Equations</param>
    /// <returns>The variable the constraint is applied to</returns>
    let apply log (c : Constraint) eqs =

        eqs
        |> List.collect (Equation.findName c.Name)
        |> function
        | [] ->
            (c, eqs)
            |> Exceptions.ConstraintVariableNotFound
            |> Exceptions.raiseExc (Some log) []

        | var::_ ->
            var
            |> Variable.setValueRange (
                c.Property
                |> Property.toValueRange
            )
        |> fun var ->
            c
            |> Events.ConstraintApplied
            |> Logger.logInfo log

            var


    /// <summary>
    /// Apply a constraint to the matching variables and solve the
    /// list of equations.
    /// </summary>
    /// <param name="onlyMinIncrMax">Whether only min incr max should be calculated</param>
    /// <param name="log">The logger</param>
    /// <param name="sortQue">The algorithm to sort the equations</param>
    /// <param name="c">The constraint</param>
    /// <param name="eqs">The list of Equations</param>
    /// <param name="useParallel">Optional param to indicate parallel processing</param>
    /// <typeparam name="'a"></typeparam>
    /// <returns></returns>
    let solve useParallel onlyMinIncrMax log sortQue (c : Constraint) eqs =
        let var = apply log c eqs

        eqs
        |> Solver.solveVariable useParallel onlyMinIncrMax log sortQue var
        |> fun eqs ->
            c
            |> Events.ConstrainedSolved
            |> Logger.logInfo log

            eqs
