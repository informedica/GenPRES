namespace Informedica.GenForm.Lib


module Logging =

    open Informedica.Logging.Lib


    /// Logs a message at the given level.
    let logMessage level (logger: Logger) (msg: Message) = msg |> Logging.logWith level logger


    /// Logs a message at the informative level.
    let logInfo logger msg = logMessage Level.Informative logger msg


    /// Logs a message as a warning.
    let logWarning logger msg = logMessage Level.Warning logger msg


    /// Logs a message as an error.
    let logError (logger: Logger) (msg: Message) = msg |> Logging.logError logger


    /// A logger that does nothing.
    let noOp = Logging.noOp
