namespace Informedica.GenSolver.Lib


module Exceptions =

    /// Equation exception
    exception SolverException of Exceptions.Message list


    /// Raise an `EquationException` with `Message` `m`.
    let raiseExc log errs m =
        printfn $"raising error"

        match log with
        | Some log ->
            m |> Logging.logError log
        | None -> ()

        m::errs |> SolverException |> raise
