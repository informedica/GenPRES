namespace Informedica.GenForm.Lib

module FormLogging =

    open Informedica.Logging.Lib
    open Informedica.GenForm.Lib.Types


    /// A logger that does nothing
    let noOp = Logging.noOp


    let formatMessage (msg: IMessage) : string =
        match msg with
        | :? Message as msg ->
            match msg with
            | Info s -> s
            | Warning s -> $"Warning: {s}"
            | ErrorMsg(s, None) -> $"Error: {s}"
            | ErrorMsg(s, Some ex) -> $"Error: {s}\nException: {ex.Message}"
        | _ -> "Unknown message type"
