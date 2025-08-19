namespace Informedica.GenForm.Lib

module GenFormLogging =

    open Informedica.Logging.Lib
    open Informedica.GenForm.Lib.Types


    /// A logger that does nothing
    let ignore = Logging.ignore


    let formatMessage (msg: IMessage) : string =
        match msg with
        | :? Message as msg ->
            match msg with
            | Info s -> s
            | Warning s -> $"Warning: {s}"
            | ErrorMsg (s, None) -> $"Error: {s}"
            | ErrorMsg (s, Some ex) -> $"Error: {s}\nException: {ex.Message}"
        | _ -> "Unknown message type"


    /// A logger that prints to the console
    let printLogger : Logger = 
        let formatter = MessageFormatter.create [
            typeof<Message>, formatMessage
        ]
        Logging.createConsole formatter


    let agentLogger =
        MessageFormatter.create [
            typeof<Message>, formatMessage
        ]
        |> AgentLogging.createWithFormatter        