

#r "nuget: Newtonsoft.Json"
#r "nuget: NJsonSchema"

#r "../../Informedica.Utils.Lib/bin/Debug/net8.0/Informedica.Utils.Lib.dll"

#load "../Types.fs"
#load "../Utils.fs"
#load "../Texts.fs"
#load "../Prompts.fs"
#load "../Message.fs"
#load "../OpenAI.fs"


open System
open Informedica.OpenAI.Lib


OpenAI.list()
|> List.iter (printfn "%s")

let model = "gpt-4"


OpenAI.Chat.defaultChatInput
    model
    {
        Role = "user"
        Content = "Why is the sky blue?"
        Validator = Ok
    }
    []
|> OpenAI.chat
|> Async.RunSynchronously
|> function
    | Ok resp ->
        printfn $"{resp}"
    | Error err -> printfn $"{err}"



OpenAI.Chat.defaultChatInput
    "gpt-3.5-turbo"
    {
        Role = "user"
        Content = "Why is the sky blue? Return one JSON"
        Validator = Ok
    }
    []
|> OpenAI.chatJson<{| answer: string |}>
|> Async.RunSynchronously
