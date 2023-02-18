namespace Informedica.GenOrder.Lib


module Logging =

    open System

    module SolverLogging = Informedica.GenSolver.Lib.Logging
    module LoggingType = Informedica.GenSolver.Lib.Types.Logging


    let private log level (logger : LoggingType.Logger) msg =
        msg
        |> fun m ->
            {
                LoggingType.TimeStamp = DateTime.Now
                LoggingType.Level = level
                LoggingType.Message = m
            }
            |> logger.Log


    let logInfo logger msg =
        msg
        |> Logging.OrderEvent
        |> log LoggingType.Informative logger


    let logWarning logger msg =
        msg
        |> Logging.OrderEvent
        |> log LoggingType.Warning logger

    let logError (logger : LoggingType.Logger) msg =
        msg
        |> Logging.OrderException
        |> log LoggingType.Error logger


