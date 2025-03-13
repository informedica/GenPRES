namespace Informedica.GenOrder.Lib



/// Helper functions to
/// facilitate the use of the
/// `Informedica.GenSolver.Lib`
module Solver =

    open Informedica.GenSolver.Lib.Types


    module Variable = Informedica.GenSolver.Lib.Variable
    module Name = Variable.Name
    module ValueRange = Variable.ValueRange
    module Property = ValueRange.Property
    module Equation = Informedica.GenSolver.Lib.Equation
    module Solver = Informedica.GenSolver.Lib.Solver
    module Api = Informedica.GenSolver.Lib.Api


    /// <summary>
    /// Map a list of OrderEquations to a list of Equations
    /// </summary>
    /// <param name="eqs">The list of OrderEquations</param>
    let mapToSolverEqs eqs =
        eqs
        |> List.map (fun eq ->
            match eq with
            | OrderProductEquation (y, xs) -> (y.Variable, xs |> List.map (fun v -> v.Variable)) |> ProductEquation
            | OrderSumEquation     (y, xs) -> (y.Variable, xs |> List.map (fun v -> v.Variable)) |> SumEquation
        )
        |> List.map Equation.nonZeroOrNegative


    /// <summary>
    /// Map a list of Equations to a list of OrderEquations
    /// using a list of original OrderEquations
    /// </summary>
    /// <param name="eqs">The original list of OrderEquations</param>
    /// <param name="ordEqs">The list of Equations</param>
    /// <returns>
    /// The new list of OrderEquations
    /// </returns>
    let mapToOrderEqs ordEqs eqs =
        let vars =
            eqs
            |> List.collect Equation.toVars
        let repl v =
            { v with
                Variable =
                    vars
                    |> List.find (Variable.getName >> ((=) v.Variable.Name))
            }
        ordEqs
        |> List.map (fun eq ->
            match eq with
            | OrderProductEquation (y, xs) ->
                (y |> repl, xs |> List.map repl)
                |> OrderProductEquation
            | OrderSumEquation (y, xs) ->
                (y |> repl, xs |> List.map repl)
                |> OrderSumEquation
        )


    /// Shorthand for Api.solveAll true
    let solveMinMax = Api.solveAll true


    /// Shorthand for Api.solveAll false
    let solve = Api.solveAll false