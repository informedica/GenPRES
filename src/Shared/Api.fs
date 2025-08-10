namespace Shared


module Api =


    open Types


    type Command =
        | OrderContextCmd of OrderContextCommand
        | TreatmentPlanCmd of TreatmentPlanCommand
        | FormularyCmd of Formulary
        | ParenteraliaCmd of Parenteralia

    and OrderContextCommand =
        | UpdateOrderContext of OrderContext
        | SelectOrderScenario of OrderContext
        | UpdateOrderScenario of OrderContext
        | ResetOrderScenario of OrderContext
        | ReloadResources of OrderContext

    and TreatmentPlanCommand =
        | UpdateTreatmentPlan of TreatmentPlan
        | FilterTreatmentPlan of TreatmentPlan

    type Response =
        | OrderContextResp of OrderContextResponse
        | TreatmentPlanResp of TreatmentPlanResponse
        | FormularyResp of Formulary
        | ParentaraliaResp of Parenteralia

    and OrderContextResponse =
        | OrderContextSelected of OrderContext
        | OrderContextUpdated of OrderContext
        | OrderContextRefreshed of OrderContext
        | ResourcesReloaded of OrderContext

    and TreatmentPlanResponse =
        | TreatmentPlanFiltered of TreatmentPlan
        | TreatmentPlanUpdated of TreatmentPlan


    module Command = 

        let toString = function
            | OrderContextCmd cmd -> 
                match cmd with
                | UpdateOrderContext _ -> "UpdateOrderContext"
                | SelectOrderScenario _ -> "SelectOrderScenario"
                | UpdateOrderScenario _ -> "UpdateOrderScenario"
                | ResetOrderScenario _ -> "ResetOrderScenario"
                | ReloadResources _ -> "ReloadResources"
            | TreatmentPlanCmd cmd -> 
                match cmd with
                | UpdateTreatmentPlan _ -> "UpdateTreatmentPlan"
                | FilterTreatmentPlan _ -> "FilterTreatmentPlan"
            | FormularyCmd _ -> "FormularyCmd"
            | ParenteraliaCmd _ -> "ParenteraliaCmd"


    /// Defines how routes are generated on server and mapped from client
    let routerPaths typeName method = sprintf "/api/%s/%s" typeName method


    /// A type that specifies the communication protocol between client and server
    /// to learn more, read the docs at https://zaid-ajaj.github.io/Fable.Remoting/src/basics.html
    type IServerApi =
        {
            processCommand: Command -> Async<Result<Response, string[]>>
            testApi: unit -> Async<string>
        }