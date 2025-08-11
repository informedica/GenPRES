open System

Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1s76xvQJXhfTpV15FuvTZfB-6pkkNTpSB30p51aAca8I")

#load "load.fsx"

open Shared.Api


type OrderAgent =
    {
        Start : unit -> unit
        Restart : unit -> unit
        ProcessCommand : Command -> Async<Result<Response, string[]>>
    }


/// The message type for the OrderAgent using the updated ServerApi approach
type OrderAgentMessage =
    | Start of AsyncReplyChannel<unit>
    | Stop of AsyncReplyChannel<unit>
    | ProcessCommand of Command * AsyncReplyChannel<Result<Response, string[]>>
    | ReloadResources of AsyncReplyChannel<unit>


let private createAgent (serverApi: IServerApi) = MailboxProcessor.Start(fun inbox ->
    let rec messageLoop() = async {
        let! msg = inbox.Receive()
        match msg with
        | Start reply ->
            // Initialize resources if needed
            reply.Reply()
            return! messageLoop()

        | Stop reply ->
            reply.Reply()
            return ()

        | ProcessCommand (cmd, reply) ->
            try
                let! result = serverApi.processCommand cmd
                reply.Reply(result)
            with
            | ex -> 
                reply.Reply(Error [| ex.Message |])
            return! messageLoop()

        | ReloadResources reply ->
            // Trigger resource reload if implemented
            reply.Reply()
            return! messageLoop()
    }

    messageLoop()
)

let mutable private agentOpt : MailboxProcessor<OrderAgentMessage> option = None


let private getOrCreateAgent (serverApi: IServerApi) =
    match agentOpt with
    | Some agent -> agent
    | None ->
        let newAgent = createAgent serverApi
        agentOpt <- Some newAgent
        newAgent


/// Implementation of the OrderAgent using the updated ServerApi
let createOrderAgent (serverApi: IServerApi) : OrderAgent =
    {
        Start =
            fun () ->
                let agent = getOrCreateAgent serverApi
                agent.PostAndAsyncReply Start
                |> Async.RunSynchronously

        Restart =
            fun () ->
                match agentOpt with
                | Some agent ->
                    agent.PostAndAsyncReply Stop |> ignore
                    agentOpt <- None
                | None -> ()

        ProcessCommand =
            fun cmd ->
                async {
                    let agent = getOrCreateAgent serverApi
                    return! agent.PostAndAsyncReply(fun reply -> ProcessCommand (cmd, reply))
                }
    }



// Create the ServerApi instance properly
let provider = 
    System.Environment.GetEnvironmentVariable "GENPRES_URL_ID"
    |> Option.ofObj
    |> Option.defaultValue "16ftzbk2CNtPEq3KAOeP7LEexyg3B-E5w52RPOyQVVks"
    |> Informedica.GenForm.Lib.Api.getCachedProviderWithDataUrlId

let serverApi = ServerApi.ApiImpl.createServerApi provider



// Usage example
let orderAgent = createOrderAgent serverApi




open Shared
open Shared.Types



// Initialize the agent
orderAgent.Start()


// Test with a simple formulary command
let testFormulary = Models.Formulary.empty


// Test the new command processing
async {
    let! result = orderAgent.ProcessCommand (FormularyCmd testFormulary)
    match result with
    | Ok (FormularyResp formulary) -> 
        printfn "Successfully processed formulary command"
        printfn $"Found %d{formulary.Generics |> Array.length} generics"
    | Ok _ -> 
        printfn "Unexpected response type"
    | Error errs -> 
        printfn "Error: %A" errs
} |> Async.RunSynchronously


// Test with OrderContext command
let testPatient : Patient = {
    Models.Patient.empty with
        Department = Some "ICK"
        Age = Some Models.Patient.Age.ageZero
        Gender = Male
}


let testOrderContext = {
    Patient = testPatient
    Filter = Models.OrderContext.filter
    Scenarios = [||]
    Intake = Models.Totals.empty
    DemoVersion = false
}


async {
    let! result = orderAgent.ProcessCommand (
        testOrderContext 
        |> Api.UpdateOrderContext 
        |> OrderContextCmd
    )
    match result with
    | Ok (OrderContextResp (OrderContextUpdated ctx)) -> 
        printfn "Successfully processed order context command"
        printfn "Updated context with %d scenarios" ctx.Scenarios.Length
    | Ok _ -> 
        printfn "Unexpected response type"
    | Error errs -> 
        printfn "Error processing order context: %A" errs
} |> Async.RunSynchronously


// Test TreatmentPlan command
let testTreatmentPlan = {
    Patient = testPatient
    Scenarios = [||]
    Selected = None
    Filtered = [||]
    Totals = Models.Totals.empty
}


async {
    let! result = orderAgent.ProcessCommand (
        testTreatmentPlan
        |> UpdateTreatmentPlan
        |> TreatmentPlanCmd
    )
    match result with
    | Ok (TreatmentPlanResp (TreatmentPlanUpdated tp)) -> 
        printfn "Successfully processed treatment plan command"
        printfn "Treatment plan has %d scenarios" tp.Scenarios.Length
    | Ok _ -> 
        printfn "Unexpected response type"
    | Error errs -> 
        printfn "Error processing treatment plan: %A" errs
} |> Async.RunSynchronously


// Test Parenteralia command
let testParenteralia = Models.Parenteralia.empty


async {
    let! result = orderAgent.ProcessCommand (ParenteraliaCmd testParenteralia)
    match result with
    | Ok (ParentaraliaResp par) -> 
        printfn "Successfully processed parenteralia command"
        printfn $"Found %d{par.Generics.Length} generics"
    | Ok _ -> 
        printfn "Unexpected response type"
    | Error errs -> 
        printfn "Error processing parenteralia: %A" errs
} |> Async.RunSynchronously

printfn "OrderAgent testing completed!"

