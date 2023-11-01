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


    /// <summary>
    /// Log an informative message
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="msg">The message</param>
    let logInfo logger msg =
        msg
        |> Logging.OrderEvent
        |> log LoggingType.Informative logger


    /// <summary>
    /// Log a warning message
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="msg">The message</param>
    let logWarning logger msg =
        msg
        |> Logging.OrderEvent
        |> log LoggingType.Warning logger



    /// <summary>
    /// Log an error message
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="msg">The message</param>
    let logError (logger : LoggingType.Logger) msg =
        msg
        |> Logging.OrderException
        |> log LoggingType.Error logger


