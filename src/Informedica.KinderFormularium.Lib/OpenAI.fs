namespace Informedica.KinderFormularium.Lib


module OpenAI =

    open System
    open System.Net.Http
    open System.Text
    open Newtonsoft.Json
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib


    type Message = {
        role: string
        content: string
    }

    type Choice = {
        index: int
        message: Message
        logprobs: Nullable<float> option
        finish_reason: string
    }

    type Usage = {
        prompt_tokens: int
        completion_tokens: int
        total_tokens: int
    }

    type ChatCompletion = {
        id: string
        object: string
        created: int64
        model: string
        choices: Choice list
        usage: Usage
        system_fingerprint: string option
    }


    let tryCreateAbsMaxDose s =
        if s |> String.IsNullOrEmpty then None
        else
            try
                match s |> String.split " " with
                | [v;u] when u |> String.split "/" |> List.length = 2 ->
                    let v = v |> String.replace "." "" |> String.replace "," "."
                    match v |> Double.tryParse |> Option.bind BigRational.fromFloat,
                          $"{u}[Time]" |> Units.fromString with
                    | Some v, Some u -> v |> ValueUnit.singleWithUnit u |> Some
                    | _ -> None
                | _ -> None
            with
            | _ -> None


    let tryCreateMaxDose s =
        if s |> String.IsNullOrEmpty then None
        else
            try
                match s |> String.split " " with
                | [v;u] ->
                    let v = v |> String.replace "." "" |> String.replace "," "."
                    let u =
                        match u |> String.split "/" with
                        | [u] -> Some u
                        | [u1;u2] when u2 |> String.contains "dose" -> Some u1
                        | _ -> None
                    match v |> Double.tryParse |> Option.bind BigRational.fromFloat,
                          u |> Option.bind Units.fromString with
                    | Some v, Some u -> v |> ValueUnit.singleWithUnit u |> Some
                    | _ -> None
                | _ -> None
            with
            | _ -> None


    // Define the API key and endpoint
    //
    let apiKey = Environment.GetEnvironmentVariable("OPEN_AI_KEY") // Replace with your actual API key

    let endpoint = "https://api.openai.com/v1/chat/completions"


    let callAI msg =
        // Create an HTTP client
        let client = new HttpClient()

        // Set up the request headers
        client.DefaultRequestHeaders.Authorization <-
            System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey)
        client.DefaultRequestHeaders.Add("User-Agent", "F# OpenAI Client")

        // Define the request body
        let requestBody =
            {|
                model = "gpt-4"
                temperature = 0.
                messages =
                    [
                        {|
                            role = "system"
                            content = "je bent een farmacotherapeutisch expert"
                        |}
                        {|
                            role = "user"
                            content = msg
                        |}
                    ]
            |} |> JsonConvert.SerializeObject

        let content = new StringContent(requestBody, Encoding.UTF8, "application/json")

        // Asynchronous API call
        async {
            let! response = client.PostAsync(endpoint, content) |> Async.AwaitTask
            let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            return responseBody
        }


    let getAbsMaxDose doseText =
        async {
            let! response = callAI $"wat is de maximale absolute dosering, geef alleen de dosering als antwoord: {doseText}"
            return
                try
                    response
                        |> Informedica.ZIndex.Lib.Json.deSerialize<ChatCompletion>
                        |> fun compl ->
                            compl.choices
                            |> List.tryHead
                            |> Option.map _.message
                            |> Option.bind (fun m -> m.content |> tryCreateAbsMaxDose)
                with
                | e ->
                    printfn $"an error occurred:\n{response}"
                    None
        }


    let getMaxDose doseText =
        async {
            let! response = callAI $"wat is de maximale dosering die per keer gegeven kan worden, geef alleen de dosering als antwoord: {doseText}"
            return
                try
                    response
                    |> Informedica.ZIndex.Lib.Json.deSerialize<ChatCompletion>
                    |> fun compl ->
                        compl.choices
                        |> List.tryHead
                        |> Option.map _.message
                        |> Option.bind (fun m -> m.content |> tryCreateMaxDose)
                with
                | e ->
                    printfn $"an error occurred:\n{response}"
                    None
        }


    let mapMaxDoses (mapped : {| adjustUnit: string; doseType: string; doseUnit: string; freqUnit: string; freqs: string; gender: string; generic: string; indication: string; maxAge: string; maxBSA: string; maxGestAge: string; maxPMAge: string; maxPerTime: string; maxPerTimeAdj: string; maxQty: string; maxQtyAdj: string; maxWeight: string; minAge: string; minBSA: string; minGestAge: string; minPMAge: string; minPerTime: string; minPerTimeAdj: string; minQty: string; minQtyAdj: string; minWeight: string; normPerTimeAdj: string; normQtyAdj: string; route: string; scheduleText: string; shape: string; substance: string |}) =
        async {
            if mapped.scheduleText |> String.contains "max" |> not ||
               mapped.maxQty |> String.notEmpty ||
               mapped.maxPerTime |> String.notEmpty then return mapped
            else
                let doseText =
                    mapped.scheduleText
                    |> String.split "\n"
                    |> List.tryHead
                    |> Option.defaultValue ""

                let! maxAbsDose = getAbsMaxDose doseText
                let! maxDose = getMaxDose doseText

                let du = mapped.doseUnit |> Units.fromString
                let tu = mapped.freqUnit |> Units.fromString

                return
                    {| mapped with
                        maxQty =
                            maxDose
                            |> Option.bind (fun d ->
                                match du with
                                | Some u ->
                                    d
                                    |> ValueUnit.convertTo u
                                    |> ValueUnit.getValue
                                    |> Array.tryHead
                                    |> function
                                        | None -> None
                                        | Some br -> br |> BigRational.toDouble |> Some
                                    |> Option.map string
                                | None -> None
                            )
                            |> Option.defaultValue ""
                        maxPerTime =
                            maxAbsDose
                            |> Option.bind (fun d ->
                                match du, tu with
                                | Some du, Some tu ->
                                    d
                                    |> ValueUnit.convertTo (du |> Units.per tu)
                                    |> ValueUnit.getValue
                                    |> Array.tryHead
                                    |> function
                                        | None -> None
                                        | Some br -> br |> BigRational.toDouble |> Some
                                    |> Option.map string
                                | _ -> None
                            )
                            |> Option.defaultValue ""
                    |}
        }