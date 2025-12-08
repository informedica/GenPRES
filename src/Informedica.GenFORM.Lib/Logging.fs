namespace Informedica.GenForm.Lib


module Logging =

    open Informedica.Logging.Lib


    /// Log a solver event with a specific level
    let logMessage level (logger: Logger) (msg: Message) =
        msg
        |> Logging.logWith level logger


    /// Log an informative solver event
    let logInfo logger msg = logMessage Level.Informative logger msg


    /// Log a warning solver event
    let logWarning logger msg = logMessage Level.Warning logger msg


    /// Log a solver exception as an error
    let logError (logger: Logger) (msg: Message) =
        msg
        |> Logging.logError logger


    /// Ignore logger for backward compatibility
    let noOp = Logging.noOp

