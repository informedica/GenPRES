
#r "nuget: FSharpPlus"
#r "nuget: Newtonsoft.Json"
#r "nuget: NJsonSchema"

#r "../../Informedica.Utils.Lib/bin/Debug/net8.0/Informedica.Utils.Lib.dll"

#load "../Types.fs"
#load "../Utils.fs"
#load "../Texts.fs"
#load "../Prompts.fs"
#load "../Message.fs"
#load "../OpenAI.fs"
#load "../Fireworks.fs"
#load "../Ollama.fs"


open Newtonsoft.Json

open FSharpPlus
open FSharpPlus.Data
open Informedica.Utils.Lib.BCL

open Informedica.OpenAI.Lib


//let systemMsg = [ Texts.systemDoseQuantityExpert |> Message.system ]


let inline extract (model: string) zero msg =
    monad {
        let! msgs = State.get

        let msgs, res =
            msg
            |> Ollama.validate2
                model
                msgs
            |> Async.RunSynchronously
            |> function
                | Ok (result, msgs) -> msgs, result
                | Error (_, msgs)   -> msgs, zero

        do! State.put msgs
        return res
    }


let extractSubstanceUnit model text =
    let zero = {| substanceUnit = "" |}
    let validator s =
        try
            let un = JsonConvert.DeserializeObject<{| substanceUnit: string |}>(s)
            match un.substanceUnit |> String.split "/" with
            | [_] -> Ok s
            | _ -> s |> Error
        with
        | e ->
            e.ToString()
            |> Error

    $"""
Use Schema {"{| substanceUnit: string |}" |> Utils.anonymousTypeStringToJson}
Extract the substance unit in the text between '''

'''%s{text}'''
Respond in JSON
"""
    |> Message.userWithValidator validator
    |> extract model zero


let extractAdjustUnit model text =
    let zero = {| adjustUnit = "" |}
    let validator s =
        let validUnit s =
            ["kg"; "m2"; "mˆ2"]
            |> List.exists (String.equalsCapInsens s)
        try
            let un = JsonConvert.DeserializeObject<{| adjustUnit: string |}>(s)
            match un.adjustUnit |> String.split "/" with
            | [u] when u |> validUnit ->
                Ok s
            | _ -> $"{s} is not a valid adjust unit" |> Error
        with
        | e ->
            e.ToString()
            |> Error

    $"""
Use Schema {"{| adjustUnit: string |}" |> Utils.anonymousTypeStringToJson}
Extract the adjust unit in the text between '''

'''{text}'''
Respond in JSON
"""
    |> Message.userWithValidator validator
    |> extract model zero


let extractTimeUnit model text =
    let zero = {| timeUnit = "" |}
    let validator s =
        try
            let un = JsonConvert.DeserializeObject<{| timeUnit: string |}>(s)
            match un.timeUnit |> String.split "/" with
            | [_] -> Ok s
            | _ -> s |> Error
        with
        | e ->
            e.ToString()
            |> Error

    $"""
Use Schema {"{| timeUnit: string |}" |> Utils.anonymousTypeStringToJson}
Extract the time unit in the text between '''

'''{text}'''
Respond in JSON
"""
    |> Message.userWithValidator validator
    |> extract model zero


let createDoseUnits model text =
    monad {
        let! substanceUnit = extractSubstanceUnit model text
        let! adjustUnit = extractAdjustUnit model text
        let! timeUnit = extractTimeUnit model text

        return
            {|
                substanceUnit = substanceUnit.substanceUnit
                adjustUnit = adjustUnit.adjustUnit
                timeUnit = timeUnit.timeUnit
            |}
    }

let un, msgs =
    State.run
        (createDoseUnits Ollama.Models.llama2 Texts.testTexts[0])
        systemMsg

msgs
|> List.iter Message.print



