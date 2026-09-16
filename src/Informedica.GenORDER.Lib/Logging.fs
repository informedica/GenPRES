namespace Informedica.GenOrder.Lib


module Logging =

    open Informedica.Logging.Lib
    open Types.Logging


    /// Log a solver event with a specific level
    let logMessage level (logger: Logger) (evt: Events.Event) = evt |> OrderEventMessage |> Logging.logWith level logger


    /// Log an informative solver event
    let logInfo logger evt = logMessage Level.Informative logger evt


    /// Log a warning solver event
    let logWarning logger evt = logMessage Level.Warning logger evt


    /// Log a debug solver event
    let logDebug logger evt = logMessage Level.Debug logger evt


    /// Log a solver exception as an error
    let logError (logger: Logger) (msg: Exceptions.Message) = msg |> OrderException |> Logging.logError logger


    /// Ignore logger for backward compatibility
    let noOp = Logging.noOp


    /// Log an order event built lazily at the given level: the thunk (and any
    /// expensive work inside it, e.g. a console table) runs only if the logger
    /// would actually consume a message at that level.
    let logMessageLazy level (logger: Logger) (mk: unit -> Events.Event) =
        Logging.logLazy level logger (fun () -> mk () |> OrderEventMessage :> IMessage)


    /// Log an order event built lazily: the thunk (and any expensive work inside
    /// it) runs only if the logger would consume an informative message.
    let logInfoLazy (logger: Logger) (mk: unit -> Events.Event) = logMessageLazy Level.Informative logger mk


    /// Log an order event built lazily: the thunk runs only if the logger would
    /// consume a warning message.
    let logWarningLazy (logger: Logger) (mk: unit -> Events.Event) = logMessageLazy Level.Warning logger mk
