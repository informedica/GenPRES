namespace Shared


module Api =

    open Types

    type Message =
        | GetScenarioResult of ScenarioResult
        | PrintScenarioResult of ScenarioResult
        | CalcMinIncrMax of ScenarioResult
        | SolveOrder of ScenarioResult
        | GetIntake of TreatmentPlan
        | GetFormulary of Formulary
        | GetParenteralia of Parenteralia
        | Test


    /// Defines how routes are generated on server and mapped from client
    let routerPaths typeName method = sprintf "/api/%s/%s" typeName method


    /// A type that specifies the communication protocol between client and server
    /// to learn more, read the docs at https://zaid-ajaj.github.io/Fable.Remoting/src/basics.html
    type IServerApi =
        {
            getScenarioResult: ScenarioResult -> Async<Result<ScenarioResult, string>>
            printScenarioResult: ScenarioResult -> Async<Result<ScenarioResult, string>>
            calcMinIncrMax: Order -> Async<Result<Order, string>>
            solveOrder: Order -> Async<Result<Order, string>>
            getIntake: float option -> Order[] -> Async<Result<Intake, string>>
            getFormulary: Formulary -> Async<Result<Formulary, string>>
            getParenteralia: Parenteralia -> Async<Result<Parenteralia, string>>
            test: unit -> Async<string>
        }