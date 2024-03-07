namespace Informedica.GenSolver.Lib


/// Implementations of solvers for product equations
/// sum equations and a set of product and/or sum
/// equations
module Solver =

    open System.Runtime.CompilerServices
    open Informedica.Utils.Lib

    module EQD = Equation.Dto
    module Name = Variable.Name

    open Types

    /// <summary>
    /// Sort a list of equations by the name of the
    /// first variable in the equation.
    /// </summary>
    /// <param name="eqs">The list of Equations</param>
    let sortByName eqs =
        eqs
        |> List.sortBy (fun e ->
            e
            |> Equation.toVars
            |> List.head
            |> Variable.getName)


    /// <summary>
    /// Format a set of equations to print.
    /// Using **f** to allow additional processing
    /// of the string.
    /// </summary>
    /// <param name="exact">Whether to print the exact value</param>
    /// <param name="pf">The print function</param>
    /// <param name="eqs">The equations to print</param>
    let printEqs exact pf eqs =

        "equations result:\n" |> pf
        eqs
        |> sortByName
        |> List.map (Equation.toString exact)
        |> List.iteri (fun i s -> $"%i{i}.\t%s{s}"  |> pf)
        "-----" |> pf

        eqs


    /// <summary>
    /// Checks whether a list of `Equation` **eqs**
    /// contains an `Equation` **eq**
    /// </summary>
    /// <param name="eq">The `Equation` to check for</param>
    /// <param name="eqs">The list of `Equation` to check in</param>
    let contains eq eqs = eqs |> List.exists ((=) eq)


    /// <summary>
    /// Replace a list of `Variable` **vs**
    /// in a list of `Equation` **es**, return
    /// a list of replaced `Equation` and a list
    /// of unchanged `Equation`
    /// </summary>
    /// <param name="vars">The list of `Variable` to replace</param>
    /// <param name="eqs">The list of `Equation` to replace in</param>
    let replace vars eqs =
        let rpl, rst =
            eqs
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


    /// <summary>
    /// Sort a list of equations by the number of
    /// total values in the equation.
    /// </summary>
    /// <param name="onlyMinMax">Whether only min incr max is calculated</param>
    /// <param name="eqs">The list of Equations</param>
    let sortQue onlyMinMax eqs =
        if eqs |> List.length = 0 then eqs
        else
            eqs
            |> List.sortBy (Equation.count onlyMinMax) //Equation.countProduct


    let check eqs =
        if eqs |> List.forall (fun eq ->
            eq |> Equation.isSolvable |> not ||
            eq |> Equation.isSolved
        ) then
            eqs
            |> List.forall Equation.check
        else true


    /// <summary>
    /// Solve a set of equations.
    /// </summary>
    /// <param name="onlyMinIncrMax">Whether to only use min, incr and max</param>
    /// <param name="log">The log function</param>
    /// <param name="sortQue">The sort function for the que</param>
    /// <param name="var">An option variable to solve for</param>
    /// <param name="eqs">The equations to solve</param>
    let solve onlyMinIncrMax log sortQue var eqs =

        let solveE n eqs eq =
            try
                Equation.solve onlyMinIncrMax log eq
            with
            | Exceptions.SolverException errs ->
                (n, errs, eqs)
                |> Exceptions.SolverErrored
                |> Exceptions.raiseExc (Some log) errs
            | e ->
                let msg = $"didn't catch {e}"
                ConsoleWriter.writeErrorMessage msg true false

                msg |> failwith

        let rec loop n que acc =
            match acc with
            | Error _ -> acc
            | Ok acc  ->
                let n = n + 1
                if n > ((que @ acc |> List.length) * Constants.MAX_LOOP_COUNT) then
                    ConsoleWriter.writeErrorMessage $"too many loops: {n}" true false
                    (n, que @ acc)
                    |> Exceptions.SolverTooManyLoops
                    |> Exceptions.raiseExc (Some log) []

                let que = que |> sortQue onlyMinIncrMax

                //(n, que)
                //|> Events.SolverLoopedQue
                //|> Logging.logInfo log

                match que with
                | [] ->
                    match acc |> List.filter (Equation.check >> not) with
                    | []      -> acc |> Ok
                    | invalid ->
                        ConsoleWriter.writeErrorMessage "invalid equations" true false
                        invalid
                        |> Exceptions.SolverInvalidEquations
                        |> Exceptions.raiseExc (Some log) []

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
                                    // replace vars in the que tail
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
                                [eq] // TODO: check if this is right
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
                ConsoleWriter.writeErrorMessage msg true false
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


    /// <summary>
    /// Solve a set of equations for a specific variable.
    /// </summary>
    /// <param name="onlyMinIncrMax">Whether to only use min, incr and max</param>
    /// <param name="log">The log function</param>
    /// <param name="sortQue">The sort function for the que</param>
    /// <param name="vr">The variable to solve for</param>
    /// <param name="eqs">The equations to solve</param>
    let solveVariable onlyMinIncrMax log sortQue vr eqs =
        let n1 = eqs |> List.length
        let solve =
            solve onlyMinIncrMax log sortQue (Some vr)

        match solve eqs with
        | Error (eqs, errs) -> Error (eqs, errs)
        | Ok eqs ->
            //TODO: need to clean up the number check
            let n2 = eqs |> List.length
            if n2 <> n1 then failwith $"not the same number of eqs, was: {n1}, now {n2}"
            else Ok eqs


    /// <summary>
    /// Solve a set of equations for all variables.
    /// </summary>
    /// <param name="onlyMinIncrMax">Whether to only use min, incr and max</param>
    /// <param name="log">The log function</param>
    /// <param name="eqs">The equations to solve</param>
    let solveAll onlyMinIncrMax log eqs =
        let n1 = eqs |> List.length
        let solve =
            solve onlyMinIncrMax log sortQue None

        match solve eqs with
        | Error (eqs, errs) -> Error (eqs, errs)
        | Ok eqs ->
            //TODO: need to clean up the number check
            let n2 = eqs |> List.length
            if n2 <> n1 then failwith $"not the same number of eqs, was: {n1}, now {n2}"
            else Ok eqs
