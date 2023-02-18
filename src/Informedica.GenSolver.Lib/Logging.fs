namespace Informedica.GenSolver.Lib


module Logging =
    
    open System
    open Types.Logging


    let private create l e = 
        {
            TimeStamp = DateTime.Now
            Level = l
            Message = e
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