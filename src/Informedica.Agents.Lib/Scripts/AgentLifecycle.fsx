// =================================================================
// AgentLifecycle.fsx
// Prototype: Named agents, graceful shutdown, and modular registry
//
// Motivation (issue #264 — halcwb request 2026-04-01):
//   1. Show CancellationTokenSource-based graceful shutdown
//      for the five server agents in AgentAdapters.makeAppEnv
//   2. Sketch moving agents closer to their domain libraries
//      for a more modular architecture
//
// Current state (as of 2026-04):
//   All five server agents are created once at startup via
//   AgentAdapters.makeAppEnv and live until process exit.
//   Agent<'T> already carries a CancellationTokenSource and
//   implements IDisposable; what's missing is:
//     - named identification for logging / diagnostics
//     - a Composition Root lifecycle (stop all on shutdown)
//     - individual agent factories inside each domain library
//
// Historical note (2026-09-05, issue #420):
//   The five server agents and ServerApi.AgentAdapters.fs were removed;
//   ServerApi.CompositionRoot.fs now composes the ports from the direct
//   ServerApi.Adapters. The lifecycle sketch below is kept as a reference
//   for the remaining logging agents (Informedica.Agents.Lib, issue #416).
// =================================================================

#I __SOURCE_DIRECTORY__
#load "load.fsx"

open System
open Informedica.Agents.Lib


// -----------------------------------------------------------------
// 1. Named, disposable wrapper around Agent.createReply
//    (Agent<'T> already owns its CancellationTokenSource and
//     implements IDisposable; this layer adds name + disposal guard)
// -----------------------------------------------------------------

/// A named, lifecycle-managed wrapper around Agent.createReply.
/// Adds:
///   - human-readable <c>Name</c> for logs / diagnostics
///   - <c>IsDisposed</c> flag for safe double-stop
///   - <c>Stop()</c> that cancels the agent and is idempotent
///   - <c>Request</c> / <c>RequestAsync</c> helpers
///
/// Usage mirrors the existing AgentAdapters pattern but makes
/// the lifecycle explicit instead of relying on process exit.
type ManagedAgent<'Req, 'Reply>(name: string, processor: 'Req -> 'Reply) =

    let mutable disposed = false
    let inner = Agent.createReply<'Req, 'Reply> processor

    member _.Name = name

    member _.IsDisposed = disposed

    /// Send a request and return the reply synchronously.
    member _.Request(msg: 'Req) : 'Reply = inner |> Agent.postAndReply msg

    /// Send a request and return the reply asynchronously.
    member _.RequestAsync(msg: 'Req) : Async<'Reply> = inner |> Agent.postAndAsyncReply msg

    /// Cancel the agent and dispose its resources.  Idempotent.
    member _.Stop() =
        if not disposed then
            disposed <- true
            Agent.dispose inner

    interface IDisposable with
        member this.Dispose() = this.Stop()


// -----------------------------------------------------------------
// 2. Agent registry — coordinated lifecycle for a set of agents
//    Mirrors what the Composition Root should do on server shutdown
// -----------------------------------------------------------------

/// Holds a group of <c>ManagedAgent</c> instances and disposes
/// them all on <c>StopAll()</c> in reverse-registration order
/// (last registered = first stopped, matching LIFO teardown).
///
/// Intended usage in CompositionRoot:
///   let registry = new AgentRegistry()
///   // wire AppHost / IHostApplicationLifetime.OnStopping
///   //   to call registry.StopAll()
///   let formularyAgent = registry.Register("Formulary", FormularyAgent.processor provider)
///   let orderCtxAgent  = registry.Register("OrderCtx",  OrderContextAgent.processor logger provider)
///   ...
type AgentRegistry() =

    let agents = System.Collections.Generic.List<{| Name: string; Disposable: IDisposable |}>()
    let mutable stopped = false

    /// Create and register a named agent backed by <c>processor</c>.
    member _.Register<'Req, 'Reply>(name: string, processor: 'Req -> 'Reply) : ManagedAgent<'Req, 'Reply> =
        if stopped then
            invalidOp $"Cannot register agent '%s{name}' after AgentRegistry has been stopped."
        let a = new ManagedAgent<'Req, 'Reply>(name, processor)
        agents.Add({| Name = name; Disposable = a |})
        a

    /// Number of agents registered in this registry.
    member _.AgentCount = agents.Count

    /// Whether StopAll has been called.
    member _.IsStopped = stopped

    /// Cancel and dispose all agents in reverse-registration order.
    member _.StopAll() =
        if not stopped then
            stopped <- true

            agents
            |> Seq.toArray
            |> Array.rev
            |> Array.iter (fun d ->
                try
                    d.Disposable.Dispose()
                with ex ->
                    printfn $"[AgentRegistry] Error stopping agent '%s{d.Name}': %s{ex.Message}")

    interface IDisposable with
        member this.Dispose() = this.StopAll()


// -----------------------------------------------------------------
// 3. Modular agent sketch
//
//    Today all agent factories live in ServerApi.AgentAdapters.
//    Moving each bounded-context agent to its own library makes
//    the Composition Root a thin wiring layer and lets each library
//    own its command DU, processor, and factory — improving
//    cohesion and making isolated integration tests easier.
//
//    Proposed pattern (pseudocode):
//
//    // In Informedica.GenForm.Lib:
//    module FormularyAgent =
//        type Command = GetFormulary of ... | GetParenteralia of ...
//        type Response = Formulary of ... | Parenteralia of ...
//        let processor (provider: IResourceProvider) (cmd: Command) : Response = ...
//        let create (provider: IResourceProvider) =
//            new ManagedAgent<FormularyAgent.Command, FormularyAgent.Response>("Formulary", processor provider)
//
//    // In Informedica.GenOrder.Lib:
//    module OrderContextAgent =
//        ...
//        let create logger provider =
//            new ManagedAgent<OrderContextAgent.Command, OrderContextAgent.Response>("OrderContext", processor logger provider)
//
//    // In ServerApi.CompositionRoot (thin wiring only):
//    let compose provider =
//        use registry = new AgentRegistry()
//        let formularyAgent  = registry.Register("Formulary",   FormularyAgent.processor provider)
//        let orderCtxAgent   = registry.Register("OrderCtx",    OrderContextAgent.processor logger provider)
//        let orderPlanAgent  = registry.Register("OrderPlan",   OrderPlanAgent.processor orderCtxPort)
//        let nutritionAgent  = registry.Register("Nutrition",   NutritionAgent.processor logger provider orderCtxPort)
//        let interactionAgent = registry.Register("Interaction", InteractionAgent.processor ())
//        // wire registry.StopAll() to AppHost OnStopping
//        buildAppEnv registry formularyAgent orderCtxAgent orderPlanAgent nutritionAgent interactionAgent
//
//    Benefits over current approach:
//    - Domain libraries are self-contained (no leaking into AgentAdapters)
//    - Integration tests can spin up a single agent in isolation
//    - Saturn / Kestrel OnStopping calls registry.StopAll() → clean drain
//    - Names appear in logs, making cross-agent debugging easier
// -----------------------------------------------------------------


// -----------------------------------------------------------------
// 4. Tests — validate naming, request-reply, and lifecycle
// -----------------------------------------------------------------

open Expecto
open Expecto.Flip

type EchoReq = string
type EchoReply = string

let echoProcessor (req: EchoReq) : EchoReply = $"echo: %s{req}"

let lifecycleTests =
    testList "AgentLifecycle" [

        test "ManagedAgent — replies before stop" {
            use a = new ManagedAgent<EchoReq, EchoReply>("echo", echoProcessor)
            let reply = a.Request("hello")
            reply |> Expect.equal "should echo message" "echo: hello"
        }

        test "ManagedAgent — name is preserved" {
            use a = new ManagedAgent<EchoReq, EchoReply>("my-agent", echoProcessor)
            a.Name |> Expect.equal "name should match constructor arg" "my-agent"
        }

        test "ManagedAgent — IsDisposed false before Stop" {
            use a = new ManagedAgent<EchoReq, EchoReply>("pre-stop", echoProcessor)
            a.IsDisposed |> Expect.isFalse "not disposed before calling Stop"
        }

        test "ManagedAgent — IsDisposed true after Stop" {
            let a = new ManagedAgent<EchoReq, EchoReply>("post-stop", echoProcessor)
            a.Stop()
            a.IsDisposed |> Expect.isTrue "should be disposed after Stop"
        }

        test "ManagedAgent — Stop is idempotent" {
            let a = new ManagedAgent<EchoReq, EchoReply>("idem", echoProcessor)
            a.Stop()

            // second Stop must not throw
            a.Stop() // second Stop must not throw
        }

        test "AgentRegistry — AgentCount reflects registrations" {
            use registry = new AgentRegistry()
            let _a1 = registry.Register<EchoReq, EchoReply>("a1", echoProcessor)
            let _a2 = registry.Register<EchoReq, EchoReply>("a2", echoProcessor)
            registry.AgentCount |> Expect.equal "two agents registered" 2
        }

        test "AgentRegistry — registered agents reply correctly" {
            use registry = new AgentRegistry()
            let a1 = registry.Register<EchoReq, EchoReply>("r1", echoProcessor)
            let a2 = registry.Register<EchoReq, EchoReply>("r2", echoProcessor)
            a1.Request("ping") |> Expect.equal "r1 reply" "echo: ping"
            a2.Request("pong") |> Expect.equal "r2 reply" "echo: pong"
        }

        test "AgentRegistry — StopAll marks registry and all agents as stopped" {
            let registry = new AgentRegistry()
            let a1 = registry.Register<EchoReq, EchoReply>("s1", echoProcessor)
            let a2 = registry.Register<EchoReq, EchoReply>("s2", echoProcessor)
            registry.StopAll()
            registry.IsStopped |> Expect.isTrue "registry IsStopped"
            a1.IsDisposed |> Expect.isTrue "a1 IsDisposed"
            a2.IsDisposed |> Expect.isTrue "a2 IsDisposed"
        }

        test "AgentRegistry — StopAll is idempotent" {
            let registry = new AgentRegistry()
            let _a = registry.Register<EchoReq, EchoReply>("idem-reg", echoProcessor)
            registry.StopAll()
            registry.StopAll() // second StopAll must not throw
        }

        test "AgentRegistry — use binding disposes automatically" {
            let mutable agentRef = Unchecked.defaultof<ManagedAgent<EchoReq, EchoReply>>

            do
                use registry = new AgentRegistry()
                let a = registry.Register<EchoReq, EchoReply>("scoped", echoProcessor)
                agentRef <- a
                a.IsDisposed |> Expect.isFalse "agent alive inside use scope"

            agentRef.IsDisposed |> Expect.isTrue "agent disposed after use scope exit"
        }

        test "AgentRegistry — registered agent preserves name through registry" {
            use registry = new AgentRegistry()
            let a = registry.Register<EchoReq, EchoReply>("named-agent", echoProcessor)
            a.Name |> Expect.equal "name should be preserved" "named-agent"
            a.Request("test") |> Expect.equal "should still reply" "echo: test"
        }

    ]

runTestsWithCLIArgs [] [||] lifecycleTests
