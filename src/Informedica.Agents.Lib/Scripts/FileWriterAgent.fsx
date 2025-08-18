
#load "load.fsx"

open Informedica.Utils.Lib
open Informedica.Agents.Lib


let useWriter () =
    use writer = FileWriterAgent.create ()

    let directory = 
        __SOURCE_DIRECTORY__
        |> Path.combineWith "test.txt"

    // Example usage: append a message to the agent
    writer 
    |> FileWriterAgent.append directory [| "test" |]
    |> FileWriterAgent.flush 
    |> FileWriterAgent.stop


useWriter ()