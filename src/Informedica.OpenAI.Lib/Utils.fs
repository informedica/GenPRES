namespace Informedica.OpenAI.Lib


module Utils =

    open System.Net.Http
    open System.Net.Http.Headers
    open Informedica.Utils.Lib.BCL
    open Newtonsoft.Json


    let anonymousTypeStringToJson s =
        s
        |> String.replace "|" ""
        |> String.replace ";" ""


    // Create an HTTP client
    let client = new HttpClient()


    let createResponse<'Response> responseBody =
        let create s r = { Original = s; Response = r }
        try
            responseBody
            |> JsonConvert.DeserializeObject<'Response>
            |> create responseBody
            |> Ok
        with
        | e ->
            $"""
Error in parsing the response:
{responseBody}

Error:
{e.ToString}
"""
            |> Error


    let post<'Response> (endPoint : string) apiKey payload =
        // Set up the request headers
        match apiKey with
        | None -> ()
        | Some apiKey ->
            client.DefaultRequestHeaders.Authorization <-
                AuthenticationHeaderValue("Bearer", apiKey)
        client.DefaultRequestHeaders.Add("User-Agent", "F# OpenAI Client")

        let content = new StringContent(payload, MediaTypeWithQualityHeaderValue("application/json"))

        // Asynchronous API call
        async {
            let! response = client.PostAsync(endPoint, content) |> Async.AwaitTask
            let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

            let modelResponse =
                responseBody
                |> createResponse<'Response>
            return modelResponse
        }


    let get<'Response> (endPoint : string) apiKey =
        match apiKey with
        | None -> ()
        | Some apiKey ->
            client.DefaultRequestHeaders.Authorization <-
                AuthenticationHeaderValue("Bearer", apiKey)

        // Asynchronous API call
        async {
            let! response = client.GetAsync(endPoint) |> Async.AwaitTask
            let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

            let modelResponse = responseBody |> createResponse<'Response>
            return modelResponse
        }
