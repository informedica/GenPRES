
open System

Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.SetEnvironmentVariable("GENPRES_URL_ID", "16ftzbk2CNtPEq3KAOeP7LEexyg3B-E5w52RPOyQVVks")


#load "load.fsx"



open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


type OrderAgent =
    {
        Start : unit -> unit
        Restart : unit -> unit
        Create : Patient -> ScenarioResult
        Filter : ScenarioResult -> ScenarioResult
        CalcOrder : Order.Dto.Dto -> Result<Order.Dto.Dto,string>
        SolveOrder : Order.Dto.Dto -> Result<Order.Dto.Dto, Order * Informedica.GenSolver.Lib.Types.Exceptions.Message list>
        DoseRules : Filter -> DoseRule array
        SolutionRules : string option -> string option -> string option -> SolutionRule array
    }


/// The message typ for the OrderAgent.
/// The order agent will be implement using the MailboxProcessor.
type OrderAgentMessage =
    | Start of AsyncReplyChannel<unit>
    | Stop of AsyncReplyChannel<unit>
    | Create of Patient * AsyncReplyChannel<ScenarioResult>
    | Filter of ScenarioResult * AsyncReplyChannel<ScenarioResult>
    | Calc of Order.Dto.Dto * AsyncReplyChannel<Result<Order.Dto.Dto, string>>
    | Solve of Order.Dto.Dto * AsyncReplyChannel<Result<Order.Dto.Dto, Order * Informedica.GenSolver.Lib.Types.Exceptions.Message list>>
    | GetDoseRules of Filter * AsyncReplyChannel<DoseRule[]>
    | GetSolutionRules of (string option * string option * string option) * AsyncReplyChannel<SolutionRule[]>


let private createAgent () = MailboxProcessor.Start(fun inbox ->

    let rec messageLoop() = async {
        let! msg = inbox.Receive()
        match msg with
        | Start reply ->
            // Implement Start logic
            reply.Reply() // Send back the result
            return! messageLoop()

        | Stop reply ->
            // Implement Stop logic
            reply.Reply()
            return ()

        | Create (patient, reply) ->
            // Implement Create logic
            let result = patient |> Api.scenarioResult
            reply.Reply(result)
            return! messageLoop()

        | Filter (scenario, reply) ->
            // Implement Filter logic
            let result = scenario |> Api.filter
            reply.Reply(result)
            return! messageLoop()

        | Calc (orderDto, reply) ->
            // Implement Calc logic
            let result = orderDto |> Api.calc
            reply.Reply(result)
            return! messageLoop()

        | Solve (orderDto, reply) ->
            // Implement Solve logic
            let result = orderDto |> Api.solve
            reply.Reply(result)
            return! messageLoop()

        | GetDoseRules (filter, reply) ->
            // Implement GetDoseRules logic
            let result = filter |> Api.getDoseRules
            reply.Reply(result)
            return! messageLoop()

        | GetSolutionRules (opts, reply) ->
            // Implement GetSolutionRules logic
            let result =
                let generic, shape, route = opts
                Api.getSolutionRules generic shape route
            reply.Reply(result)
            return! messageLoop()
    }

    messageLoop()
)

let mutable private agentOpt : MailboxProcessor<OrderAgentMessage> option = None


/// implementation of the OrderAgent
/// using the MailboxProcessor 'agent'
let orderAgent : OrderAgent =
    {
        Start =
            fun () ->
                match agentOpt with
                | Some agent ->
                    agent
                | None ->
                    agentOpt <- createAgent () |> Some
                    agentOpt.Value
                |> fun agent ->
                    agent.PostAndAsyncReply(Start)
                    |> Async.RunSynchronously
        Restart =
            fun () ->
                match agentOpt with
                | Some agent ->
                    agent.PostAndAsyncReply(Stop)
                    |> Async.RunSynchronously
                    agentOpt <- None
                | None -> ()
        Create =
            fun patient ->
                match agentOpt with
                | Some agent ->
                    agent
                | None ->
                    agentOpt <- createAgent () |> Some
                    agentOpt.Value
                |> fun agent ->
                    agent.PostAndAsyncReply(fun reply -> Create (patient, reply))
                    |> Async.RunSynchronously
        Filter =
            fun scenario ->
                match agentOpt with
                | Some agent ->
                    agent
                | None ->
                    agentOpt <- createAgent () |> Some
                    agentOpt.Value
                |> fun agent ->
                    agent.PostAndAsyncReply(fun reply -> Filter (scenario, reply))
                    |> Async.RunSynchronously
        CalcOrder =
            fun orderDto ->
                match agentOpt with
                | Some agent ->
                    agent
                | None ->
                    agentOpt <- createAgent () |> Some
                    agentOpt.Value
                |> fun agent ->
                    agent.PostAndAsyncReply(fun reply -> Calc (orderDto, reply))
                    |> Async.RunSynchronously
        SolveOrder =
            fun orderDto ->
                match agentOpt with
                | Some agent ->
                    agent
                | None ->
                    agentOpt <- createAgent () |> Some
                    agentOpt.Value
                |> fun agent ->
                    agent.PostAndAsyncReply(fun reply -> Solve (orderDto, reply))
                    |> Async.RunSynchronously
        DoseRules =
            fun filter ->
                match agentOpt with
                | Some agent ->
                    agent
                | None ->
                    agentOpt <- createAgent () |> Some
                    agentOpt.Value
                |> fun agent ->
                    agent.PostAndAsyncReply(fun reply -> GetDoseRules (filter, reply))
                    |> Async.RunSynchronously
        SolutionRules =
            fun generic shape route ->
                match agentOpt with
                | Some agent ->
                    agent
                | None ->
                    agentOpt <- createAgent () |> Some
                    agentOpt.Value
                |> fun agent ->
                    agent.PostAndAsyncReply(fun reply -> GetSolutionRules ((generic, shape, route), reply))
                    |> Async.RunSynchronously
    }



orderAgent.Start ()
orderAgent.Restart ()
orderAgent.DoseRules Filter.filter

