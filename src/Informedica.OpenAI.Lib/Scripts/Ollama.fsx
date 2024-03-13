

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
open Informedica.OpenAI.Lib
open Ollama.Operators


let tools =
    {|
      city =  {|
        ``type`` =  "string"
        description = "The city to get the weather for"
      |}
      state = {|
        ``type`` =  "string"
        description = "The state to get the weather for"
      |}
    |}
    |> Ollama.Tool.create
        "get_weather"
        "Get the weather."
        ["city"; "state"]
    |> List.singleton

"What is the weather in Seattle?"
|> Message.user
|> Ollama.extract
    tools
    "joefamous/firefunction-v1:q3_k"
    []
|> Async.RunSynchronously
|> function
    | Ok resp ->
        resp.Response.message.content
        |> printfn "%s"
    | _ -> ()


let extractDoseQuantities model text =
        Texts.systemDoseQuantityExpert
        |> init model
        >>? $"""
The text between the ''' describes dose quantities for a
substance:

'''{text}'''

For which substance?
Give the answer as Substance : ?
"""
        >>? """
What is the unit used for the substance, the substance unit?
Give the answer as SubstanceUnit : ?
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


Ollama.options.temperature <- 0.
Ollama.options.penalize_newline <- true
Ollama.options.top_k <- 10
Ollama.options.top_p <- 0.95


Ollama.Models.``openchat:7b``
|> testModel


module BNFC =

    let paracetamolPO =
        [
            """
paracetamol
Neonate 28 weeks to 32 weeks corrected gestational age
20 mg/ kg for 1 dose, then 10–15 mg/kg every 8–12 hours as required, maximum daily dose to be given in divided doses; maximum 30 mg/kg per day.
"""
            """
Child 1–2 months
30–60 mg every 8 hours as required, maximum daily dose to be given in divided doses; maximum 60 mg/kg per day.
"""
            """
paracetamol
Child 3–5 months
60 mg every 4–6 hours; maximum 4 doses per day.
"""
            """
paracetamol
Child 6–23 months
120 mg every 4–6 hours; maximum 4 doses per day.
"""
        ]



"You are a helpful assistant"
|> init Ollama.Models.``openchat:7b``
>>? "Why is the sky blue?"
|> Conversation.print


Ollama.options.temperature <- 0
Ollama.options.seed <- 101
Ollama.options.penalize_newline <- true
Ollama.options.top_k <- 10
Ollama.options.top_p <- 0.95


BNFC.paracetamolPO[0]
|> extractDoseQuantities Ollama.Models.``openchat:7b``
|> Conversation.print


Ollama.options.temperature <- 0.5
Ollama.options.seed <- 101
Ollama.options.penalize_newline <- false
Ollama.options.top_k <- 50
Ollama.options.top_p <- 0.5


"""
You are an empathic medical professional and translate medical topics to parents
that have a child admitted to a pediatric critical care unit.
"""
|> init Ollama.Models.``openchat:7b``
>>? """
Explain to the parents that there child as to be put on a ventilator and has to
be intubated.
"""
//>>? "translate the previous message to Dutch"
|> Conversation.print


"""
Je bent een empathische zorgverlener die ouders uitleg moet geven over hun kind
dat op de kinder IC ligt.

Je geeft alle uitleg en antwoorden in het Nederlands.
"""
|> init Ollama.Models.``openchat:7b``
>>? """
Leg aan ouders uit dat hun kind aan de beademing moet worden gelegd en daarvoor
geintubeerd moet worden.
"""
|> Conversation.print

let x =
    """
Je bent een empathische zorgverlener die ouders uitleg moet geven over hun kind
dat op de kinder IC ligt.

Je geeft alle uitleg en antwoorden in het Nederlands.
"""
    |> init Ollama.Models.``openchat:7b``


"""
Je bent een empathische zorgverlener die ouders uitleg moet geven over hun kind
dat op de kinder IC ligt.

Je geeft alle uitleg en antwoorden in het Nederlands.
"""
|> Message.system
|> Ollama.chat Ollama.Models.llama2 []
|> Async.RunSynchronously


Ollama.listModels ()
|> Async.RunSynchronously


"""
Use schema: { number: int; unit: string }
What is the minimal corrected gestational age mentioned in the text between '''

'''A neonate 28 weeks to 32 weeks corrected gestational age.'''

Reply just in one JSON."""
|> Message.user
|> Ollama.json<{| number: int; unit: string |}>
    Ollama.Models.llama2
    []
|> Async.RunSynchronously
|> function
    | Ok x -> printfn $"{x}"
    | Error e -> printfn $"oops: {e}"



"""
Use schema: { number: int; unit: string }
Foo bar. Only give an anwer when possible.
Reply just in one JSON."""
|> Message.user
|> Ollama.json<{| number: int; unit: string |}>
    Ollama.Models.llama2
    []
|> Async.RunSynchronously
|> function
    | Ok x -> printfn $"{x}"
    | Error e -> printfn $"oops: {e}"


"""
Wy is the sky blue?
"""
|> Message.user
|> Ollama.openAIchat
    Ollama.Models.llama2
    []
|> Async.RunSynchronously



"""
You are an empathic medical professional and translate medical topics to parents
that have a child admitted to a pediatric critical care unit.
"""
|> init Ollama.Models.``openchat:7b``
>>? """
Explain to the parents that there child as to be put on a ventilator and has to
be intubated.
"""
//>>? "translate the previous message to Dutch"
|> Conversation.print
