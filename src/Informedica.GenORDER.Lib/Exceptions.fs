namespace Informedica.GenOrder.Lib


module Exceptions =


    /// Raised when an order cannot be solved or created.
    exception OrderException of Exceptions.Message


    /// Logs the message and the order when a logger is given, then raises an OrderException that the
    /// order could not be solved.
    let raiseExc log m o =
        match log with
        | Some log -> (m, o) |> Exceptions.OrderCouldNotBeSolved |> Logging.logError log

        | None -> ()

        (m, o) |> Exceptions.OrderCouldNotBeSolved |> OrderException |> raise


    let toString (exn: exn) =
        match exn with
        | :? OrderException as m ->
            match m.Data0 with
            | Exceptions.OrderCouldNotBeSolved(m, o) -> $"{o} could not be resolved because: {m}"
            | Exceptions.OrderCouldNotBeCreated exn -> $"Order could not be created:\n{exn}"
        | _ -> exn.ToString()
