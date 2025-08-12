namespace Informedica.GenSolver.Lib


module Exceptions =

    /// Solver exception
    exception SolverException of Exceptions.Message list



    /// Raise an `EquationException` with `Message` `m` and adds it to
    /// the list of `errs`.
    let raiseExc log errs m =

        match log with
        | Some log ->
            m |> Logger.logError log
        | None -> ()

        m::errs |> SolverException |> raise


    let toString (exn : exn) =
        match exn with
        | :? SolverException as m ->
            m.Data0
            |> List.map (fun m -> m.ToString()) |> String.concat "\n"
        | _ ->
            exn.ToString()