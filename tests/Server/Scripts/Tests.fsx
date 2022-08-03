
#load "load.fsx"


module AppTests =

    open Expecto
    open Informedica.GenOrder.Lib

    open System
    open System.Net
    open System.Net.Http
    open System.IO
    open Microsoft.AspNetCore.Builder
    open Microsoft.AspNetCore.Hosting
    open Microsoft.AspNetCore.TestHost
    open Microsoft.Extensions.DependencyInjection

    // ---------------------------------
    // Test server/client setup
    // ---------------------------------

    let createHost() =
        WebHostBuilder()
            .UseContentRoot(Directory.GetCurrentDirectory())
            .Configure(Action<IApplicationBuilder> Main.configureApp)
            .ConfigureServices(Action<IServiceCollection> Main.configureServices)

    // ---------------------------------
    // Helper functions
    // ---------------------------------

    let runTask task =
        task
        |> Async.AwaitTask
        |> Async.RunSynchronously

    let get (client : HttpClient) (path : string) =
        path
        |> client.GetAsync
        |> runTask

    let createRequest (method : HttpMethod) (path : string) =
        let url = "http://127.0.0.1" + path
        new HttpRequestMessage(method, url)

    let addCookiesFromResponse (response : HttpResponseMessage)
                               (request  : HttpRequestMessage) =
        request.Headers.Add("Cookie", response.Headers.GetValues("Set-Cookie"))
        request

    let makeRequest (client : HttpClient) (request : HttpRequestMessage) =
        use server = new TestServer(createHost())
        use client = server.CreateClient()
        request
        |> client.SendAsync
        |> runTask

    let ensureSuccess (response : HttpResponseMessage) =
        if not response.IsSuccessStatusCode
        then response.Content.ReadAsStringAsync() |> runTask |> failwithf "%A"
        else response

    let isStatus (code : HttpStatusCode) (response : HttpResponseMessage) =
        Expect.equal code response.StatusCode ""
        response

    let isOfType (contentType : string) (response : HttpResponseMessage) =
        Expect.equal contentType (response.Content.Headers.ContentType.MediaType) ""
        response

    let readText (response : HttpResponseMessage) =
        response.Content.ReadAsStringAsync()
        |> runTask

    let readJson<'T> (response : HttpResponseMessage) =
        response.Content.ReadAsStringAsync()
        |> runTask
        |> Newtonsoft.Json.JsonConvert.DeserializeObject<'T>


    let equals txt exp act =
        Expect.equal act exp txt 

    // ---------------------------------
    // Tests
    // ---------------------------------

    [<Tests>]
    let tests =
        
        testList "Server" [
            test "can retrieve a test json" {
                use server = new TestServer(createHost())
                use client = server.CreateClient()

                get client "/test"
                |> ensureSuccess
                |> readJson
                |> equals "" Dto.testDto                
            }
        ]