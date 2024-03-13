namespace Informedica.OpenAI.Lib


module Utils =

    open System.Net.Http
    open System.Net.Http.Headers
    open Informedica.Utils.Lib
    open Newtonsoft.Json


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
        ConsoleWriter.writeInfoMessage $"""
EndPoint: {endPoint}
Payload:
{payload}
"""
            true false

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

            let modelResponse = responseBody |> createResponse<'Response>
            return modelResponse
        }


    let get<'Response> (endPoint : string) =
        ConsoleWriter.writeInfoMessage $"""
EndPoint: {endPoint}
"""
            true false

        // Asynchronous API call
        async {
            let! response = client.GetAsync(endPoint) |> Async.AwaitTask
            let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

            let modelResponse = responseBody |> createResponse<'Response>
            return modelResponse
        }
