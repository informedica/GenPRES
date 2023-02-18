namespace Informedica.GenOrder.Lib



/// Helper functions to
/// facilitate the use of the
/// `Informedica.GenSolver.Lib`
module Solver =

    open Informedica.Utils.Lib
    open Informedica.GenUnits.Lib
    open Informedica.GenSolver.Lib.Types

    module Variable = Informedica.GenSolver.Lib.Variable
    module Name = Variable.Name
    module ValueRange = Variable.ValueRange
    module Property = ValueRange.Property
    module Equation = Informedica.GenSolver.Lib.Equation
    module Solver = Informedica.GenSolver.Lib.Solver
    module Api = Informedica.GenSolver.Lib.Api



    let mapToSolverEqs eqs =
        eqs
        |> List.map (fun eq ->
            match eq with
            | OrderProductEquation (y, xs) -> (y.Variable, xs |> List.map (fun v -> v.Variable)) |> ProductEquation
            | OrderSumEquation     (y, xs) -> (y.Variable, xs |> List.map (fun v -> v.Variable)) |> SumEquation
        )
        |> List.map Equation.nonZeroOrNegative


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


    let solveMinMax = Api.solveAll true


    let solve = Api.solveAll false


