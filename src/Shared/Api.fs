namespace Shared


module Api =


    open Types


    type ScenarioResultMessage =
        | GetScenarioResult of ScenarioResult
        | CalcValues of ScenarioResult
        | SolveOrder of ScenarioResult

    type TreatmentPlanMessage =
        | CalcTreatmentPlan of TreatmentPlan

    type FormularyMessage =
        | GetFormulary of Formulary
        | GetParenteralia of Parenteralia

    type Message =
        | ScenarioResultMsg of ScenarioResultMessage
        | TreatmentPlanMsg of TreatmentPlanMessage
        | FormularyMsg of FormularyMessage


    /// Defines how routes are generated on server and mapped from client
    let routerPaths typeName method = sprintf "/api/%s/%s" typeName method


    /// A type that specifies the communication protocol between client and server
    /// to learn more, read the docs at https://zaid-ajaj.github.io/Fable.Remoting/src/basics.html
    type IServerApi =
        {
            processMessage: Message -> Async<Result<Message, string[]>>
            testApi: unit -> Async<string>
        }