namespace Informedica.GenSolver.Lib


module Logger =

    open Informedica.Logging.Lib
    open Types.Logging


    /// Log a solver event with a specific level
    let logMessage level (logger: Logger) (evt: Types.Events.Event) =
        evt
        |> SolverEventMessage
        |> Logging.logWith level logger


    /// Log an informative solver event
    let logInfo logger evt = logMessage Level.Informative logger evt


    /// Log a warning solver event
    let logWarning logger evt = logMessage Level.Warning logger evt


    /// Log a solver exception as an error
    let logError (logger: Logger) (msg: Exceptions.Message) =
        msg
        |> ExceptionMessage
        |> Logging.logError logger


    /// Ignore logger for backward compatibility
    let noOp = Logging.noOp

