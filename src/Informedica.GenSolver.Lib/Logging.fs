namespace Informedica.GenSolver.Lib



module Logger =

    open System
    open Types.Logging


    let private create level msg =
        {
            TimeStamp = DateTime.Now
            Level = level
            Message = msg
        }


    let logMessage level (logger : Logger) evt =
        evt
        |> SolverMessage
        |> create level
        |> logger.Log


    let logInfo logger msg = logMessage Informative logger msg


    let logWarning logger msg = logMessage Warning logger msg


    let logError (logger : Logger) msg =
        msg
        |> ExceptionMessage
        |> create Error
        |> logger.Log


    let ignore = { Log = ignore }

