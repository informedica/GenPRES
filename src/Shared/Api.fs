namespace Shared


module Api =

    open Types

    /// Defines how routes are generated on server and mapped from client
    let routerPaths typeName method = sprintf "/api/%s/%s" typeName method


    /// A type that specifies the communication protocol between client and server
    /// to learn more, read the docs at https://zaid-ajaj.github.io/Fable.Remoting/src/basics.html
    type IServerApi =
        {
            getScenarioResult: ScenarioResult -> Async<Result<ScenarioResult, string>>
            test : unit -> Async<string>
        }