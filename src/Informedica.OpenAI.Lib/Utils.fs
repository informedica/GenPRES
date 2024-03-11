namespace Informedica.OpenAI.Lib


module Utils =

    open System.Net.Http
    open System.Text
    open Newtonsoft.Json


    // Create an HTTP client
    let client = new HttpClient()


    let post<'Response> (endPoint : string) apiKey content =

        // Set up the request headers
        match apiKey with
        | None -> ()
        | Some apiKey ->
            client.DefaultRequestHeaders.Authorization <-
                System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey)
        client.DefaultRequestHeaders.Add("User-Agent", "F# OpenAI Client")

        let content = content |> JsonConvert.SerializeObject
        let content = new StringContent(content, Encoding.UTF8, "application/json")

        // Asynchronous API call
        async {
            let! response = client.PostAsync(endPoint, content) |> Async.AwaitTask
            let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask

            let modelResponse =
                try
                    responseBody
                    |> JsonConvert.DeserializeObject<'Response>
                    |> Ok
                with
                | e ->
                    e.ToString() |> Error

            return modelResponse
        }


    let get<'Response> (endPoint : string) =

        // Asynchronous API call
        async {
            let! response = client.GetAsync(endPoint) |> Async.AwaitTask
            let! responseBody = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            let models =
                try
                    responseBody
                    |> JsonConvert.DeserializeObject<'Response>
                with
                | e -> e.ToString() |> failwith

            return models
        }
