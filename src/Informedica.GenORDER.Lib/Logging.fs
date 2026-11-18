namespace Informedica.GenOrder.Lib


module Logging =

    open Informedica.Logging.Lib
    open Types.Logging


    /// Logs an order event at the given level.
    let logMessage level (logger: Logger) (evt: Events.Event) = evt |> OrderEventMessage |> Logging.logWith level logger


    /// Logs an order event at the informative level.
    let logInfo logger evt = logMessage Level.Informative logger evt


    /// Logs an order event at the warning level.
    let logWarning logger evt = logMessage Level.Warning logger evt


    /// Logs an order event at the debug level.
    let logDebug logger evt = logMessage Level.Debug logger evt


    /// Logs an order exception as an error.
    let logError (logger: Logger) (msg: Exceptions.Message) = msg |> OrderException |> Logging.logError logger


    /// A logger that does nothing.
    let noOp = Logging.noOp


    /// Logs the order event that mk returns at the given level. Calls mk only when the logger is enabled for that
    /// level, so expensive work such as a console table is skipped otherwise.
    let logMessageLazy level (logger: Logger) (mk: unit -> Events.Event) =
        Logging.logLazy level logger (fun () -> mk () |> OrderEventMessage :> IMessage)


    /// Logs the order event that mk returns at the informative level. Calls mk only when that level is enabled.
    let logInfoLazy (logger: Logger) (mk: unit -> Events.Event) = logMessageLazy Level.Informative logger mk


    /// Logs the order event that mk returns at the warning level. Calls mk only when that level is enabled.
    let logWarningLazy (logger: Logger) (mk: unit -> Events.Event) = logMessageLazy Level.Warning logger mk


    /// Logs the order event that mk returns at the debug level. Calls mk only when that level is enabled.
    let logDebugLazy (logger: Logger) (mk: unit -> Events.Event) = logMessageLazy Level.Debug logger mk
