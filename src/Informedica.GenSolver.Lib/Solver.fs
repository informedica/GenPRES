namespace Informedica.GenSolver.Lib


/// Implementations of solvers for product equations
/// sum equations and a set of product and/or sum
/// equations
module Solver =

    module EQD = Equation.Dto
    module Name = Variable.Name

    open Types

    let sortByName eqs =
        eqs
        |> List.sortBy (fun e ->
            e
            |> Equation.toVars
            |> List.head
            |> Variable.getName)


    /// Format a set of equations to print.
    /// Using **f** to allow additional processing
    /// of the string.
    let printEqs exact pf eqs =

        "equations result:\n" |> pf
        eqs
        |> sortByName
        |> List.map (Equation.toString exact)
        |> List.iteri (fun i s -> $"%i{i}.\t%s{s}"  |> pf)
        "-----" |> pf

        eqs


    /// Checks whether a list of `Equation` **eqs**
    /// contains an `Equation` **eq**
    let contains eq eqs = eqs |> List.exists ((=) eq)


    /// Replace a list of `Variable` **vs**
    /// in a list of `Equation` **es**, return
    /// a list of replaced `Equation` and a list
    /// of unchanged `Equation`
    let replace vars es =
        let rpl, rst =
            es
            |> List.partition (fun e ->
                vars
                |> List.exists (fun v -> e |> Equation.contains v)
            )

        vars
        |> List.fold (fun acc v ->
            acc
            |> List.map (Equation.replace v)
        ) rpl
        , rst


    let memSolve f =
        let cache = ref Map.empty
        fun e ->
            match cache.Value.TryFind(e) with
            | Some r -> r
            | None ->
                let r = f e
                cache.Value <- cache.Value.Add(e, r)
                r

    let sortQue que =
        if que |> List.length = 0 then que
        else
            que
            |> List.sortBy Equation.count //Equation.countProduct


    /// Create the equation solver using a
    /// product equation and a sum equation solver
    /// and function to determine whether an
    /// equation is solved
    let solve onlyMinIncrMax log sortQue var eqs =
        let solveE n eqs eq =
            try
                Equation.solve onlyMinIncrMax log eq
            with
            | Exceptions.SolverException errs ->
                (n, errs, eqs)
                |> Exceptions.SolverErrored
                |> Exceptions.raiseExc None errs
            | e ->
                let msg = $"didn't catch {e}"
                printfn $"{msg}"
                msg |> failwith

        let rec loop n que acc =
            match acc with
            | Error _ -> acc
            | Ok acc  ->
                let n = n + 1
                if n > ((que @ acc |> List.length) * Constants.MAX_LOOP_COUNT) then
                    printfn $"too many loops: {n}"
                    (n, que @ acc)
                    |> Exceptions.SolverTooManyLoops
                    |> Exceptions.raiseExc None []

                let que = que |> sortQue

                //(n, que)
                //|> Events.SolverLoopedQue
                //|> Logging.logInfo log

                match que with
                | [] ->
                    match acc |> List.filter (Equation.check >> not) with
                    | []      -> acc |> Ok
                    | invalid ->
                        printfn "invalid equations"
                        invalid
                        |> Exceptions.SolverInvalidEquations
                        |> Exceptions.raiseExc None []

                | eq::tail ->
                    // need to calculate result first to enable tail call optimization
                    let q, r =
                        // If the equation is already solved, or not solvable
                        // just put it to  the accumulated equations and go on with the rest
                        if eq |> Equation.isSolvable |> not then
                            tail,
                            [ eq ]
                            |> List.append acc
                            |> Ok
                        // Else go solve the equation
                        else
                            match eq |> solveE n (acc @ que) with
                            // Equation is changed, so every other equation can
                            // be changed as well (if changed vars are in the other
                            // equations) so start new
                            | eq, Changed cs ->
                                let vars = cs |> List.map fst
                                // find all eqs with vars in acc and put these back on que
                                acc
                                |> replace vars
                                |> function
                                | rpl, rst ->
                                    // replace vars in tail
                                    let que =
                                        tail
                                        |> replace vars
                                        |> function
                                        | es1, es2 ->
                                            es1
                                            |> List.append es2
                                            |> List.append rpl

                                    que,
                                    rst
                                    |> List.append [ eq ]
                                    |> Ok

                            // Equation did not in fact change, so put it to
                            // the accumulated equations and go on with the rest
                            | eq, Unchanged ->
                                tail,
                                [eq]
                                |> List.append acc
                                |> Ok

                            | eq, Errored m ->
                                [],
                                [eq]
                                |> List.append acc
                                |> List.append que
                                |> fun eqs ->
                                    Error (eqs, m)
                    loop n q r

        match var with
        | None     -> eqs, []
        | Some var -> eqs |> replace [var]
        |> function
        | rpl, rst ->
            rpl
            |> Events.SolverStartSolving
            |> Logging.logInfo log

            try
                match rpl with
                | [] -> eqs |> Ok
                | _  -> loop 0 rpl (Ok rst)
            with
            | Exceptions.SolverException errs  ->
                 Error (rpl @ rst, errs)
            | e ->
                let msg = $"something unexpected happened, didn't catch {e}"
                printfn $"{msg}"
                msg |> failwith

            |> function
            | Ok eqs ->
                eqs
                |> Events.SolverFinishedSolving
                |> Logging.logInfo log

                eqs |> Ok
            | Error (eqs, m) ->
                eqs
                |> Events.SolverFinishedSolving
                |> Logging.logInfo log

                Error (eqs, m)


    let solveVariable onlyMinIncrMax log sortQue vr eqs =
        solve onlyMinIncrMax log sortQue (Some vr) eqs


    let solveAll onlyMinIncrMax log eqs =
        solve onlyMinIncrMax log sortQue None eqs
