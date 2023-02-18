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


    /// Initialize the solver returning a set of equations
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


    let setVariableValues onlyMinIncrMax n p eqs =
        eqs
        |> List.collect (Equation.findName n)
        |> function
        | [] -> None
        | var::_ ->
            p
            |> Property.toValueRange
            |> Variable.setValueRange onlyMinIncrMax var
            |> Some


    let solveAll = Solver.solveAll


    /// Solve an `Equations` list with
    ///
    /// * f: function used to process string message
    /// * n: the name of the variable to be updated
    /// * p: the property of the variable to be updated
    /// * vs: the values to update the property of the variable
    /// * eqs: the list of equations to solve
    let solve onlyMinIncrMax sortQue log n p eqs =
        eqs
        |> setVariableValues onlyMinIncrMax n p
        |> function
        | None -> eqs |> Ok
        | Some var ->
            eqs
            |> Solver.solveVariable onlyMinIncrMax log sortQue var


    /// Make a list of `EQD`
    /// to contain only positive
    /// values as solutions
    let nonZeroNegative eqs =
        eqs
        |> List.map Equation.nonZeroOrNegative


    let applyConstraints onlyMinIncrMax log eqs cs =
        let apply = Constraint.apply onlyMinIncrMax log

        cs
        |> List.fold (fun acc c ->
            acc
            |> apply c
            |> fun var ->
                acc
                |> List.map (Equation.replace var)
        ) eqs


    let solveConstraints onlyMinIncrMax log cs eqs =
        cs
        |> Constraint.orderConstraints log
        |> applyConstraints false log eqs
        |> Solver.solveAll onlyMinIncrMax log
