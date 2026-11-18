namespace Informedica.GenOrder.Lib


/// Functions that map order equations to GenSOLVER equations and solve them.
module Solver =

    open Informedica.GenSolver.Lib.Types


    module Variable = Informedica.GenSolver.Lib.Variable
    module Name = Variable.Name
    module ValueRange = Variable.ValueRange
    module Property = ValueRange.Property
    module Equation = Informedica.GenSolver.Lib.Equation
    module Solver = Informedica.GenSolver.Lib.Solver
    module Api = Informedica.GenSolver.Lib.Api


    /// Maps order equations to solver equations. Every variable in the result accepts only values above zero.
    let mapToSolverEqs eqs =
        eqs
        |> List.map (fun eq ->
            match eq with
            | OrderProductEquation(y, xs) -> (y.Variable, xs |> List.map _.Variable) |> ProductEquation
            | OrderSumEquation(y, xs) -> (y.Variable, xs |> List.map _.Variable) |> SumEquation
        )
        |> List.map Equation.nonZeroOrNegative


    /// Returns the order equations ordEqs with each variable replaced by the solver variable with the same name
    /// from the solver equations eqs. Throws when a variable name is missing from eqs.
    let mapToOrderEqs ordEqs eqs =
        let vars = eqs |> List.collect Equation.toVars

        let repl v =
            { v with Variable = vars |> List.find (Variable.getName >> (=) v.Variable.Name) }

        ordEqs
        |> List.map (fun eq ->
            match eq with
            | OrderProductEquation(y, xs) -> (y |> repl, xs |> List.map repl) |> OrderProductEquation
            | OrderSumEquation(y, xs) -> (y |> repl, xs |> List.map repl) |> OrderSumEquation
        )


    /// Solves the equations on the minimum, increment and maximum of each variable only.
    let solveMinMax = Api.solveAll true


    /// Solves the equations on the value sets as well as on the minimum, increment and maximum.
    let solve = Api.solveAll false
