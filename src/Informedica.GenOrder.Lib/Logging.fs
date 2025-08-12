namespace Informedica.GenOrder.Lib


module Logging =

    open Informedica.Logging.Lib
    open Types.Logging


    /// Log a solver event with a specific level
    let logMessage level (logger: Logger) (evt: Types.Events.Event) =
        evt
        |> OrderEventMessage
        |> Logging.logWith level logger


    /// Log an informative solver event
    let logInfo logger evt = logMessage Informative logger evt


    /// Log a warning solver event
    let logWarning logger evt = logMessage Warning logger evt


    /// Log a solver exception as an error
    let logError (logger: Logger) (msg: Exceptions.Message) =
        msg
        |> OrderException
        |> Logging.logError logger


    /// Ignore logger for backward compatibility
    let ignore = Logging.ignore


