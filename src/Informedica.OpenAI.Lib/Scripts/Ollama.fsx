

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


open System
open Informedica.Utils.Lib.BCL
open Informedica.OpenAI.Lib
open Ollama.Operators


Ollama.options.temperature <- 0.
Ollama.options.penalize_newline <- true
Ollama.options.top_k <- 10
Ollama.options.top_p <- 0.95


let extractDoseQuantities model text =
        Texts.systemDoseQuantityExpert
        |> init model
        >>? $"""
Use Schema: {{ name: string }}
The text between the ''' describes dose quantities for a
substance:

'''{text}'''

For which substance?
Reply in JSON.
"""
        >>? """
Use Schema: {{ substanceUnit: string }}
What is the unit used for the substance, the substance unit?

Reply in JSON.
"""
        >>? """
What is the unit to adjust the dose for?
Give the answer as AdjustUnit : ?
"""
        >>? """
What is the time unit for the dose frequency?
Give the answer as TimeUnit : ?
"""
        >>? """
What is the maximum dose per time in SubstanceUnit/TimeUnit?
Give the answer as MaximumDosePerTime: ?
"""
        >>? """
What is the dose, adjusted for weight in SubstanceUnit/AdjustUnit/TimeUnit?
Give the answer as AdjustedDosePerTime: ?
"""
        >>? """
What is the number of doses per TimeUnit?
Give the answer as Frequency: ?
"""
        >>? """
Schema
Summarize the previous answers as:

- Substance: ?
- SubstanceUnit: ?
- AdjustUnit: ?
- TimeUnit: ?
- MaximumDosePerTime: ?
- AdjustedDosePerTime: ?
- Frequency: ?

"""


let testModel model =

    printfn $"\n\n# Running: {model}\n\n"
    for text in Texts.testTexts do
        extractDoseQuantities model text
        |> Conversation.print


let testAll () =

    [
        Ollama.Models.gemma
        Ollama.Models.``gemma:7b-instruct``
        Ollama.Models.llama2
        Ollama.Models.``llama2:13b-chat``
        Ollama.Models.mistral
        Ollama.Models.``mistral:7b-instruct``
        Ollama.Models.``openchat:7b``
    ]
    |> List.iter testModel



open Newtonsoft.Json


type ProcessMessage<'ReturnType> =
    ProcessMessage of (Message list -> 'ReturnType * Message list)



module ProcessMessage =


    let run (ProcessMessage f) msgs = msgs |> f


    let map f procMsg =
        fun msgs ->
            let res1, msgs = run procMsg msgs
            f res1, msgs
        |> ProcessMessage


    let map2 f procMsg1 procMsg2 =
        fun msgs ->
            let res1, msgs = run procMsg1 msgs
            let res2, msgs = run procMsg2 msgs
            f res1 res2, msgs
        |> ProcessMessage


    let bind f procMsg =
        fun msgs ->
            let res1, msgs = run procMsg msgs
            run (f res1) msgs
        |> ProcessMessage


    let apply f procMsg =
        fun msgs ->
            let f, msgs = run f msgs
            let x, msgs = run procMsg msgs
            let y = f x
            y, msgs
        |> ProcessMessage


    let returnPM a =
        fun msgs ->
            a, msgs
        |> ProcessMessage


    module Operators =


        let (<*>) = apply

        let (<!>) = map

        let (>!) procMsg msgs = run procMsg msgs


let msgs = [ Texts.systemDoseQuantityExpert |> Message.system ]


let createProcessSubstanceUnitMsg model msg : ProcessMessage<{| substanceUnit: string |}> =
    fun msgs ->
        msg
        |> Ollama.validate2<{| substanceUnit : string |}>
            model
            msgs
        |> Async.RunSynchronously
        |> function
            | Ok (result, msgs) -> result, msgs
            | Error _ -> {| substanceUnit = "" |}, msgs

    |> ProcessMessage


let substanceUnitMsg =
    let validator =
        fun s ->
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
What is the substance unit in the text between '''

'''{Texts.testTexts[0]}'''
Respond in JSON
"""
    |>
    Message.userWithValidator validator



let procMsgSubstUnit model =
    createProcessSubstanceUnitMsg
        model
        substanceUnitMsg


let createProcessAdjustUnitMsg model msg : ProcessMessage<{| adjustUnit: string |}> =
    fun msgs ->
        msg
        |> Ollama.validate2<{| adjustUnit: string |}>
            model
            msgs
        |> Async.RunSynchronously
        |> function
            | Ok (result, msgs) -> result, msgs
            | Error _ -> {| adjustUnit = "" |}, msgs

    |> ProcessMessage


let adjustUnitMsg =
    let validator =
        fun s ->
            try
                let un = JsonConvert.DeserializeObject<{| adjustUnit: string |}>(s)
                match un.adjustUnit |> String.split "/" with
                | [_] -> Ok s
                | _ -> s |> Error
            with
            | e ->
                e.ToString()
                |> Error
    $"""
Use Schema {"{| adjustUnit: string |}" |> Utils.anonymousTypeStringToJson}
What is the adjust unit in the text between '''

'''{Texts.testTexts[0]}'''
Respond in JSON
"""
    |>
    Message.userWithValidator validator


let procMsgAdjustUnit model =
    createProcessAdjustUnitMsg
        model
        adjustUnitMsg


let createProcessTimeUnitMsg model msg : ProcessMessage<{| timeUnit: string |}> =
    fun msgs ->
        msg
        |> Ollama.validate2<{| timeUnit : string |}>
            model
            msgs
        |> Async.RunSynchronously
        |> function
            | Ok (result, msgs) -> result, msgs
            | Error _ -> {| timeUnit = "" |}, msgs

    |> ProcessMessage


let timeUnitMsg =
    let validator =
        fun s ->
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
What is the time unit in the text between '''

'''{Texts.testTexts[0]}'''
Respond in JSON
"""
    |>
    Message.userWithValidator validator



let procMsgTimeUnit model =
    createProcessTimeUnitMsg
        model
        timeUnitMsg


let createUnit
    (substUnit: {| substanceUnit : string |})
    (adjustUnit: {| adjustUnit : string |})
    (timeUnit: {| timeUnit : string |})  =
        {|
            substUnit = substUnit.substanceUnit
            adjustUnit = adjustUnit.adjustUnit
            timeUnit = timeUnit.timeUnit
        |}



open ProcessMessage.Operators


createUnit
<!> procMsgSubstUnit Ollama.Models.llama2
<*> procMsgAdjustUnit Ollama.Models.llama2
<*> procMsgTimeUnit Ollama.Models.llama2
>! [ Texts.systemDoseQuantityExpert |> Message.system ]