namespace Shared


module Api =


    open Types


    type Message =
        | PrescriptionContextMsg of PrescriptionContext
        | TreatmentPlanMsg of TreatmentPlan
        | FormularyMsg of Formulary
        | ParenteraliaMsg of Parenteralia


    /// Defines how routes are generated on server and mapped from client
    let routerPaths typeName method = sprintf "/api/%s/%s" typeName method


    /// A type that specifies the communication protocol between client and server
    /// to learn more, read the docs at https://zaid-ajaj.github.io/Fable.Remoting/src/basics.html
    type IServerApi =
        {
            processMessage: Message -> Async<Result<Message, string[]>>
            testApi: unit -> Async<string>
        }