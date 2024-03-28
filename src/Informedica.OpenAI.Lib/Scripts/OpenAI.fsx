
#r "nuget: FSharpPlus"
#r "nuget: Newtonsoft.Json"
#r "nuget: NJsonSchema"

#r "../../Informedica.Utils.Lib/bin/Debug/net8.0/Informedica.Utils.Lib.dll"

#load "../Types.fs"
#load "../Utils.fs"
#load "../Texts.fs"
#load "../Prompts.fs"
#load "../Message.fs"
#load "../Extraction.fs"
#load "../OpenAI.fs"


open System
open FSharpPlus
open FSharpPlus.Data
open Newtonsoft.Json
open Informedica.Utils.Lib.BCL
open Informedica.OpenAI.Lib
open OpenAI.WithState



let systemMsg model text =
        let msg =
            text
            |> Texts.systemDoseQuantityExpert2 |> Message.system
        OpenAI.Chat.defaultChatInput model msg []


let unitValidator<'Unit> text get validUnits s =
    let isValidUnit s =
        if validUnits |> List.isEmpty then true
        else
            validUnits
            |> List.exists (String.equalsCapInsens s)
    try
        let un = JsonConvert.DeserializeObject<'Unit>(s)
        match un |> get |> String.split "/" with
        | [u] when u |> isValidUnit ->
            if text |> String.containsCapsInsens u then Ok s
            else
                $"{u} is not mentioned in the text"
                |> Error
        | _ ->
            if validUnits |> List.isEmpty then $"{s} is not a valid unit, the unit should not contain '/'"
            else
                $"""
{s} is not a valid unit, the unit should not contain '/' and the unit should be one of the following:
{validUnits |> String.concat ", "}
"""
            |> Error
    with
    | e ->
        e.ToString()
        |> Error


let extractSubstanceUnit model text =
    let zero = {| substanceUnit = "" |}
    let validator = unitValidator text (fun (u: {| substanceUnit: string |}) -> u.substanceUnit)  []

    """
Use the provided schema to extract the unit of measurement (substance unit) from the medication dosage information contained in the text.
Your answer should return a JSON string representing the extracted unit.

Use schema: { substanceUnit: string }

Examples of usage and expected output:
 - For "mg/kg/dag", return: "{ "substanceUnit": "mg" }"
 - For "g/m2/dag", return: "{ "substanceUnit": "g" }"
 - For "IE/m2", return: "{ "substanceUnit": "IE" }"

Respond in JSON
"""
    |> Message.userWithValidator validator
    |> extract model zero


let extractAdjustUnit model text =
    let zero = {| adjustUnit = "" |}
    let validator =
        ["kg"; "m2"; "mˆ2"]
        |> unitValidator text (fun (u: {| adjustUnit: string |}) -> u.adjustUnit)

    """
Use the provided schema to extract the unit by which a medication dose is adjusted, such as patient weight or body surface area, from the medication dosage information contained in the text.
Your answer should return a JSON string representing the extracted adjustment unit.

Use schema : { adjustUnit: string }

Examples of usage and expected output:
- For "mg/kg/dag", return: "{ "adjustUnit": "kg" }"
- For "mg/kg", return: "{ "adjustUnit": "kg" }"
- For "mg/m2/dag", return: "{ "adjustUnit": "m2" }"
- For "mg/m2", return: "{ "adjustUnit": "m2" }"

Respond in JSON
"""
    |> Message.userWithValidator validator
    |> extract model zero


let extractTimeUnit model text =
    let zero = {| timeUnit = "" |}
    let validator =
        [
            "dag"
            "week"
            "maand"
        ]
        |> unitValidator text (fun (u: {| timeUnit: string |}) -> u.timeUnit)

    """
Use the provided schema to extract the time unit from the medication dosage information contained in the text.
Your answer should return a JSON string representing the extracted time unit.

Use schema : { timeUnit: string }

Examples of usage and expected output:
- For "mg/kg/dag", return: "{ "timeUnit": "dag" }"
- For "mg/kg", return: "{ "timeUnit": "" }"
- For "mg/m2/week", return: "{ "timeUnit": "week" }"
- For "mg/2 dagen", return: "{ "timeUnit": "2 dagen" }"

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


let un, input =
    let text = Texts.testTexts[3]
    let model = OpenAI.Models.``gpt-4-turbo-preview``
    State.run
        (createDoseUnits model text)
        (systemMsg model text)

printfn $"## The final extracted structure:\n{un}\n\n"

printfn "## The full conversation"
input
|> OpenAI.Chat.print


let test model =
    [
        for (text, exp) in Texts.testUnitTexts do
            let un, _ =
                State.run
                    (createDoseUnits model text)
                    (systemMsg model text)
            if un = exp then 1 else 0
    ]
    |> List.sum


[
    OpenAI.Models.``gpt-3.5-turbo``
    OpenAI.Models.``gpt-4-turbo-preview``
]
|> List.map (fun model ->
    printf $"- Testing: {model}: "
    let s = model |> test
    printfn $"score: {s}"
    model, s
)
|> List.maxBy snd
|> fun (m, s) -> printfn $"\n\n## And the winner is: {m} with a high score: {s} from {Texts.testUnitTexts |> List.length}"

