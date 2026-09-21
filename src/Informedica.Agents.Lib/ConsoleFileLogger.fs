namespace Informedica.Agents.Lib


open Informedica.Logging.Lib


[<RequireQualifiedAccess>]
module ConsoleFileLogger =

    /// Create a logger that prints to the console using a message formatter
    let createConsole (formatter: IMessage -> string) : Logger =
        Logging.create (fun msg ->
            msg.Message
            |> formatter
            |> fun s ->
                if not (System.String.IsNullOrEmpty s) then
                    printfn $"%s{s}"
        )


    /// Create a logger that writes to a file using a message formatter
    let createFile path (formatter: IMessage -> string) : Logger =
        Logging.create (fun msg ->
            msg.Message
            |> formatter
            |> fun s ->
                if not (System.String.IsNullOrEmpty s) then
                    let text = [ $"{msg.TimeStamp}: {msg.Level}"; s ]
                    System.IO.File.AppendAllLines(path, text)
        )
