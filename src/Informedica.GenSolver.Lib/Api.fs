namespace Informedica.GenSolver.Lib


/// Public funtions to use the library
module Api =

    open System

    open Informedica.Utils.Lib.BCL

    module VRD = Variable.Dto
    module EQD = Equation.Dto

    module ValueRange = Variable.ValueRange
    module Property = ValueRange.Property
    module Name = Variable.Name


    /// <summary>
    /// Create a list of Equations from a list of strings
    /// </summary>
    /// <param name="eqs">List of strings</param>
    /// <returns>List of Equations</returns>
    let init eqs =
        let notEmpty = String.IsNullOrWhiteSpace >> not
        let prodEqs, sumEqs = eqs |> List.partition (String.contains "*")
        let createProdEqs = List.map (EQD.createProd >> EQD.fromDto)
        let createSumEqs  = List.map (EQD.createSum  >> EQD.fromDto)

        let parse eqs op =
            eqs
            |> List.map (String.splitAt '=')
            |> List.map (Array.collect (String.splitAt op))
            |> List.map (Array.map String.trim)
            |> List.map (Array.filter notEmpty)
            |> List.map (Array.map VRD.withName)

        (parse prodEqs '*' |> createProdEqs) @ (parse sumEqs '+' |> createSumEqs)


    /// <summary>
    /// Apply a variable property to a list of equations
    /// </summary>
    /// <param name="n">Name of the variable</param>
    /// <param name="p">Property of the variable</param>
    /// <param name="eqs">List of equations</param>
    /// <returns>The variable option where the property is applied to</returns>
    /// <remarks>
    /// The combination of n and p is equal to a constraint
    /// </remarks>
    let setVariableValues n p eqs =
        eqs
        |> List.collect (Equation.findName n)
        |> function
        | [] -> None
        | var::_ ->
            p
            |> Property.toValueRange
            |> Variable.setValueRange var
            |> Some


    /// Solve an `Equations` list
    let solveAll = Solver.solveAll


    /// <summary>
    /// Solve an `Equations` list with
    /// </summary>
    /// <param name="onlyMinIncrMax">True if only min, incr and max values are to be used</param>
    /// <param name="sortQue">The algorithm to sort the equations</param>
    /// <param name="log">The logger to log operations</param>
    /// <param name="n">Name of the variable to be updated</param>
    /// <param name="p">Property of the variable to be updated</param>
    /// <param name="eqs">List of equations to solve</param>
    /// <returns>A result type of the solved equations</returns>
    let solve onlyMinIncrMax sortQue log n p eqs =
        eqs
        |> setVariableValues n p
        |> function
        | None -> eqs |> Ok
        | Some var ->
            eqs
            |> Solver.solveVariable onlyMinIncrMax log sortQue var


    /// <summary>
    /// Set all variables in a list of equations to a non zero or negative value
    /// </summary>
    /// <param name="eqs">The list of Equations</param>
    let nonZeroNegative eqs =
        eqs
        |> List.map Equation.nonZeroOrNegative


    /// <summary>
    /// Apply a list of constraints to a list of equations
    /// </summary>
    /// <param name="log">The logger to log operations</param>
    /// <param name="eqs">A list of Equations</param>
    /// <param name="cs">A list of constraints</param>
    /// <returns>A list of Equations</returns>
    let applyConstraints log eqs cs =
        let apply = Constraint.apply log

        cs
        |> List.fold (fun acc c ->
            acc
            |> apply c
            |> fun var ->
                acc
                |> List.map (Equation.replace var)
        ) eqs


    /// <summary>
    /// Solve a list of equations using a list of constraints
    /// </summary>
    /// <param name="onlyMinIncrMax">True if only min, incr and max values are to be used</param>
    /// <param name="log">The logger to log operations</param>
    /// <param name="eqs">A list of Equations</param>
    /// <param name="cs">A list of constraints</param>
    /// <returns>A list of Equations</returns>
    let solveConstraints onlyMinIncrMax log cs eqs =
        cs
        |> Constraint.orderConstraints log
        |> applyConstraints log eqs
        |> Solver.solveAll onlyMinIncrMax log
