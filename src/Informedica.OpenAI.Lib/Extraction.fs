namespace Informedica.OpenAI.Lib


module Extraction =

    open Newtonsoft.Json

    open FSharpPlus
    open Informedica.Utils.Lib.BCL
    open FSharpPlus.Data


    let inline extract getJson model zero (msg : Message) =
        monad {
            // get the current input
            let! state = State.get
            // get the structured extraction along with
            // the updated input
            let state, res = state |> getJson model zero (msg : Message)
            // refresh the state with the updated list of messages
            do! State.put state
            // return the structured extraction
            return res
        }


    let inline unitValidator<'Unit> text zero get validUnits s =
        let isValidUnit s =
            if validUnits |> List.isEmpty then true
            else
                validUnits
                |> List.exists (String.equalsCapInsens s)
        try
            let un = JsonConvert.DeserializeObject<'Unit>(s)
            // check the 'zero' case
            if zero |> JsonConvert.SerializeObject = (un |> JsonConvert.SerializeObject) then Ok s
            else
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


    let extractSubstanceUnit jsonSubstUnit model text =
        let zero = {| substanceUnit = "" |}
        let validator =
            []
            |> unitValidator
                text (zero |> box)
                (fun (u: {| substanceUnit: string |}) -> u.substanceUnit)

        Prompts.User.substanceUnitText
        |> Message.userWithValidator validator
        |> extract jsonSubstUnit model zero


    let extractAdjustUnit jsonAdjustUnit model text =
        let zero = {| adjustUnit = "" |}
        let validator =
            ["kg"; "m2"; "mˆ2"]
            |> unitValidator
                text (zero |> box)
                (fun (u: {| adjustUnit: string |}) -> u.adjustUnit)


        Prompts.User.adjustUnitText zero
        |> Message.userWithValidator validator
        |> extract jsonAdjustUnit model zero


    let extractTimeUnit jsonTimeUnit model text =
        let zero = {| timeUnit = "" |}
        let validator =
            [
                "dag"
                "week"
                "maand"
            ]
            |> unitValidator
                   text (zero |> box)
                   (fun (u: {| timeUnit: string |}) -> u.timeUnit)

        Prompts.User.timeUnitText zero
        |> Message.userWithValidator validator
        |> extract jsonTimeUnit model zero


    let createDoseUnits
        jsonSubstUnit
        jsonAdjustUnit
        jsonTimeUnit
        model text =

        monad {
            let! (substanceUnit : {| substanceUnit: string |})  = extractSubstanceUnit jsonSubstUnit model text
            let! (adjustUnit : {| adjustUnit : string |}) = extractAdjustUnit jsonAdjustUnit model text
            let! (timeUnit: {| timeUnit: string  |}) = extractTimeUnit jsonTimeUnit model text

            return
                {|
                    substanceUnit = substanceUnit.substanceUnit
                    adjustUnit = adjustUnit.adjustUnit
                    timeUnit = timeUnit.timeUnit
                |}
        }


    let extractFrequency jsonFreq model timeUnit =
        let zero = {| frequencies = List.empty<int>; timeUnit = timeUnit |}
        // just return zero if there is no time unit
        let jsonFreq =
            if timeUnit |> String.isNullOrWhiteSpace |> not then jsonFreq
            else
                fun _ _ _ state -> state, zero
        let validator =
            fun s ->
                try
                    let _ = JsonConvert.DeserializeObject<{| frequencies : int list; timeUnit : string |}>(s)
                    s |> Ok
                with
                | e -> e.ToString() |> Error

        Prompts.User.frequencyText timeUnit zero
        |> Message.userWithValidator validator
        |> extract jsonFreq model zero