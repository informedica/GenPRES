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


    let extractSubstanceUnit jsonSubstUnit model text =
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
        |> extract jsonSubstUnit model zero


    let extractAdjustUnit jsonAdjustUnit model text =
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
        |> extract jsonAdjustUnit model zero


    let extractTimeUnit jsonTimeUnit model text =
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


