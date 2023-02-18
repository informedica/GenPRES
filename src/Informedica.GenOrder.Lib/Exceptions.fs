namespace Informedica.GenOrder.Lib


module Exceptions =


        /// Equation exception
        exception OrderException of Exceptions.Message

        /// Raise an `EquationException` with `Message` `m`.
        let raiseExc log m o =
            match log with
            | Some log ->
                printfn $"logging error"
                (m, o)
                |> Exceptions.OrderCouldNotBeSolved
                |> Logging.logError log

            | None -> ()

            (m, o)
            |> Exceptions.OrderCouldNotBeSolved
            |> OrderException
            |> raise


        let toString (exn : exn) =
            match exn with
            | :? OrderException as m ->
                match m.Data0 with
                | Exceptions.OrderCouldNotBeSolved(m, o) ->
                    $"{o} could not be resolved because: {m}"
            | _ -> $"cannot turn {exn} to string"