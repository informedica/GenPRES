namespace Informedica.GenSolver.Lib



type IMessage = interface end


type TimeStamp = DateTime


type Level =
    | Informative
    | Warning
    | Debug
    | Error


type SolverMessage =
    | ExceptionMessage of Exceptions.Message
    | SolverMessage of Events.Event
    interface IMessage


type Message =
    {
        TimeStamp: TimeStamp
        Level: Level
        Message: IMessage
    }


type Logger = { Log: Message -> unit }


module logger =

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

