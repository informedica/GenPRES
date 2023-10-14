namespace Informedica.GenSolver.Lib



module Constraint =

    open Types

    module ValueRange = Variable.ValueRange
    module Property = ValueRange.Property
    module ValueSet = ValueRange.ValueSet
    module Name = Variable.Name


    let eqsName (c1 : Constraint) (c2 : Constraint) = c1.Name = c2.Name


    let toString { Name = n; Property = p } = $"{n |> Name.toString}: {p}"


    let scoreConstraint c =
            match c.Property with
            | ValsProp vs ->
                let n = vs |> ValueSet.count
                if n = 1 then    -3, c
                else              n, c
            | MinProp _   -> -5, c
            | IncrProp _      -> -4, c
            | _               -> -2, c


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
            |> Logging.logInfo log

            cs
            |> List.map snd


    let apply onlyMinIncrMax log (c : Constraint) eqs =

        eqs
        |> List.collect (Equation.findName c.Name)
        |> function
        | [] ->
            (c, eqs)
            |> Exceptions.ConstraintVariableNotFound
            |> Exceptions.raiseExc (Some log) []

        | vr::_ ->
            c.Property
            |> Property.toValueRange
            |> Variable.setValueRange onlyMinIncrMax vr
        |> fun var ->
            c
            |> Events.ConstraintApplied
            |> Logging.logInfo log

            var


    let solve onlyMinIncrMax log sortQue (c : Constraint) eqs =
        let var = apply onlyMinIncrMax log c eqs

        eqs
        |> Solver.solveVariable onlyMinIncrMax log sortQue var
        |> fun eqs ->
            c
            |> Events.ConstrainedSolved
            |> Logging.logInfo log

            eqs
