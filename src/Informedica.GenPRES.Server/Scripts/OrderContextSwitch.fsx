// Step 4.1 of docs/implementation-plans/725-contract-model-dto-domain-flow.md (issue #762):
// the switch-over of the order context path. The port is typed on the domain: it takes the
// domain's command verb and a plan context and answers a plan context with its intake. The
// service behind it is the domain's pipeline, `PlanContext.evaluate`, with the errors in the
// server's words. The command handler keeps its patient gate, parses the contract model in
// two steps (`ofModel`, then `fromDto`, refusing on an error), and maps the answer out with the
// demo flag the environment holds. Until step 4.2 puts the order plan port on domain values,
// the order plan service evaluates through the same pipeline via a contract-typed function,
// so that one evaluation path remains and the old mapping is unused.
//
// Dosing: the switch-over. The contract model is unchanged; the last part of this script runs
// the old path and the new one on the demo data and compares their answers.
//
// Prototype per the script-only policy in AGENTS.md. What migration touches:
//
//   1. Ports.fs: `OrderContextPort.evaluate` typed as below; `AppEnv` gains `demo: bool`.
//   2. Services.fs: `OrderContextService.evaluate` (domain) and `evaluateModel` (the contract
//      model in and out, for the order plan service until 4.2); the old body, `setDemoVersion`
//      and `updateIntake` go. `OrderPlanService` takes an `evaluate` function where it took the
//      port.
//   3. Adapters.fs: the port over the new service; the order plan port given the function;
//      `makeAppEnvWith` takes `demo` and `makeAppEnv` reads it from the environment once.
//   4. Server.fs: `demo = not settings.IsProd` into `makeAppEnvWith`.
//   5. OrderContextCommand.fs: `processCmd` as below; `DtoError.words` in Mappers.OrderContext.
//   6. Tests: `portContext`/`portPlan` become the domain value; `makeEnv` gains `demo`; the
//      routing tests as below in a new OrderContextSwitchTests.fs.
//
// Run from this directory: dotnet fsi OrderContextSwitch.fsx

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open System
open Informedica.Utils.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
// after the domain, so that the contract model's cases win unqualified
open Shared.Types
open ServerApi


// ---------------------------------------------------------------------------
// 1. Ports.fs: the port on the domain
// ---------------------------------------------------------------------------

/// The prescribing workbench's port: the domain's command verb over a plan context, the
/// answer a plan context with its intake. The verb is the wire's, mapped by the command
/// handler; the context is parsed there too, so the port never sees the contract model.
type OrderContextPort =
    {
        evaluate:
            (Informedica.GenOrder.Lib.Types.OrderContext -> OrderContext.Command)
                -> PlanContext
                -> Async<Result<PlanContext, string[]>>
    }


// ---------------------------------------------------------------------------
// 2. Mappers.OrderContext.fs: the words for a Dto that is no context
// ---------------------------------------------------------------------------

module DtoErrorWords =

    /// The server's words for a reason the contract model is no plan context. Only the
    /// patient's can come from the client's panel; the rest name a Dto from elsewhere.
    let words =
        function
        | DtoError.Patient e -> Patient.words e
        | DtoError.OrderNotCreated m -> $"De order kon niet worden gemaakt: %s{m}"
        | DtoError.UnknownDoseType s -> $"Onbekend doseertype: %s{s}"
        | DtoError.UnknownTextKind s -> $"Onbekende tekstsoort: %s{s}"
        | DtoError.UnknownCategory s -> $"Onbekende categorie: %s{s}"
        | DtoError.Missing f -> $"Ontbreekt: %s{f}"


// ---------------------------------------------------------------------------
// 3. Services.fs: the service as the domain's pipeline
// ---------------------------------------------------------------------------

module OrderContextService =

    open Informedica.Utils.Lib.ConsoleWriter.NewLineTime


    /// The messages of a failed evaluation as one refusal.
    let private refusal messages =
        messages
        |> List.map (fun m -> OrderLogging.formatOrderMessage (m :> Informedica.Logging.Lib.IMessage))
        |> String.concat "\n"
        |> Array.singleton


    /// The plan context evaluated against the rules: the domain's pipeline, the intake over
    /// the provider's totals data. An exception on the way is the refusal.
    let evaluate
        logger
        (provider: Resources.IResourceProvider)
        (cmd: Informedica.GenOrder.Lib.Types.OrderContext -> OrderContext.Command)
        (pc: PlanContext)
        : Result<PlanContext, string[]>
        =
        try
            pc
            |> PlanContext.evaluate logger provider (provider.GetTotals()) cmd
            |> Result.mapError refusal
        with e ->
            writeErrorMessage $"errored:\n{e}"
            Error [| e.Message |]


    /// The contract model's context parsed, the plan context the mapper makes of it, or the
    /// reasons it is none in the server's words.
    let parse (ctx: OrderContext) : Result<PlanContext, string[]> =
        ctx
        |> OrderContextMapper.ofModel
        |> PlanContext.Dto.fromDto
        |> Result.mapError (List.map DtoErrorWords.words >> List.toArray)


    /// The contract model in and out over the pipeline: what the order plan service calls
    /// until it is on domain values itself.
    let evaluateModel
        (demo: bool)
        logger
        provider
        (cmd: Shared.Api.OrderContextCommand)
        (ctx: OrderContext)
        : Result<OrderContext, string[]>
        =
        ctx
        |> parse
        |> Result.bind (evaluate logger provider (OrderContextMapper.Command.toDomain cmd))
        |> Result.map (PlanContext.Dto.toDto >> OrderContextMapper.toModel demo)


// ---------------------------------------------------------------------------
// 4. OrderContextCommand.fs: the handler
// ---------------------------------------------------------------------------

module OrderContextCommand =

    /// The context's patient made at the inbound boundary, the context parsed into the
    /// domain, the verb mapped, the port asked, the answer mapped out with the environment's
    /// demo flag. A draft that is none, or a context the domain does not read, is refused.
    let processCmd
        (demo: bool)
        (port: OrderContextPort)
        (cmd: Shared.Api.OrderContextCommand, ctx: OrderContext)
        : Async<Result<OrderContext, string[]>>
        =
        Patient.over ctx.Patient (fun () ->
            match ctx |> OrderContextService.parse with
            | Error errs -> async { return Error errs }
            | Ok pc ->
                async {
                    let! answer = port.evaluate (OrderContextMapper.Command.toDomain cmd) pc

                    return
                        answer
                        |> Result.map (PlanContext.Dto.toDto >> OrderContextMapper.toModel demo)
                }
        )


// ---------------------------------------------------------------------------
// 5. Tests, for tests/Informedica.GenPRES.Server.Tests/OrderContextSwitchTests.fs
// ---------------------------------------------------------------------------

module OrderContextSwitchTests =

    open Expecto
    open Expecto.Flip
    open Shared.Types


    let patient = StubPatientData.patient

    let ctx: OrderContext =
        { Shared.Models.OrderContext.empty with
            Id = "c-1"
            Category = OrderCategory.Nutrition NutritionCategory.TPN
            DemoVersion = true
            Filter = { Shared.Models.OrderContext.filter with Generic = Some "glucose" }
            Patient = patient
            Intake = { Shared.Models.Totals.empty with Volume = [| Normal "10 ml" |] }
        }

    let echo: OrderContextPort =
        { evaluate = fun _ pc -> async { return Ok pc } }


    let tests =
        testList
            "the order context switch-over"
            [
                testAsync "the answer is the port's plan context as the contract model, the demo flag the environment's" {
                    let! answer = OrderContextCommand.processCmd false echo (Shared.Api.OrderContextCommand.UpdateOrderContext, ctx)

                    match answer with
                    | Ok a ->
                        a |> Expect.equal "the context as sent, but for the demo flag" { ctx with DemoVersion = false }
                    | Error e -> failtest $"refused: %A{e}"

                    let! demo = OrderContextCommand.processCmd true echo (Shared.Api.OrderContextCommand.UpdateOrderContext, ctx)

                    demo
                    |> Result.map _.DemoVersion
                    |> Expect.equal "demo as the environment says" (Ok true)
                }

                testAsync "the port receives the verb over the parsed context, id and category included" {
                    let seen = ref None

                    let port: OrderContextPort =
                        { evaluate =
                            fun cmd pc ->
                                seen.Value <- Some(cmd pc.Context, pc.Id, pc.Category)
                                async { return Ok pc } }

                    let! _ = OrderContextCommand.processCmd false port (Shared.Api.OrderContextCommand.SelectOrderScenario, ctx)

                    match seen.Value with
                    | Some(OrderContext.SelectOrderScenario domainCtx, id, category) ->
                        domainCtx.Filter.Generic |> Expect.equal "the context parsed" (Some "glucose")
                        id |> Expect.equal "the plan's id" "c-1"

                        category
                        |> Expect.equal
                            "the plan's category"
                            (Informedica.GenOrder.Lib.Types.OrderCategory.Nutrition
                                Informedica.GenOrder.Lib.Types.NutritionCategory.TPN)
                    | other -> failtest $"the port saw %A{other}"
                }

                testAsync "a draft that is no patient is refused before the port, with the server's words" {
                    let asked = ref false

                    let port: OrderContextPort =
                        { evaluate =
                            fun _ pc ->
                                asked.Value <- true
                                async { return Ok pc } }

                    let! answer =
                        OrderContextCommand.processCmd
                            false
                            port
                            (Shared.Api.OrderContextCommand.UpdateOrderContext,
                             { ctx with Patient = Shared.Models.Patient.empty })

                    answer |> Expect.equal "no patient" (Error [| Patient.noPatient |])
                    asked.Value |> Expect.isFalse "the port never asked"
                }

                testAsync "the port's refusal is the answer" {
                    let port: OrderContextPort =
                        { evaluate = fun _ _ -> async { return Error [| "ctx error" |] } }

                    let! answer = OrderContextCommand.processCmd false port (Shared.Api.OrderContextCommand.UpdateOrderContext, ctx)
                    answer |> Expect.equal "propagated" (Error [| "ctx error" |])
                }

                test "the words for a Dto that is no context" {
                    DtoError.Patient PatientError.NoAgeOrMeasuredWeightAndHeight
                    |> DtoErrorWords.words
                    |> Expect.equal "the patient's" Patient.noPatient

                    DtoError.OrderNotCreated "x" |> DtoErrorWords.words |> Expect.stringContains "the order's" "x"
                }
            ]


OrderContextSwitchTests.tests |> Expecto.Tests.runTestsWithCLIArgs [] [||] |> ignore


// ---------------------------------------------------------------------------
// 6. Against the demo data: the old path and the new one on the same context. Run here; the
//    acceptance of the switch-over is the client suite and the walkthrough.
// ---------------------------------------------------------------------------

Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable("GENPRES_DEBUG", "0")
Environment.SetEnvironmentVariable("GENPRES_PROD", "0")

let provider: Resources.IResourceProvider =
    Api.getCachedProviderWithDataUrlId OrderLogging.noOp (Environment.GetEnvironmentVariable "GENPRES_URL_ID")

let logger = OrderLogging.noOp

let child =
    { StubPatientData.patient with
        // the old path defaults an empty department to ICK; given, both paths filter alike
        Department = Some "ICK"
    }

let start: OrderContext =
    { Shared.Models.OrderContext.empty with
        Filter =
            { Shared.Models.OrderContext.filter with
                Generic = Some "paracetamol"
                Route = Some "rectaal"
            }
        Patient = child
    }

// the old path is the library's current service, reached by its full name since the script's
// module shadows it; the new path is the script's evaluateModel over the same provider
let oldPath cmd ctx =
    ServerApi.OrderContextService.evaluate logger provider cmd ctx
    |> Result.defaultWith (fun e -> failwith $"old: %A{e}")

let newPath cmd ctx =
    OrderContextService.evaluateModel true logger provider cmd ctx
    |> Result.defaultWith (fun e -> failwith $"new: %A{e}")

let compare label (a: OrderContext) (b: OrderContext) =
    let same what x y = if x = y then printfn "  %s: same" what else printfn "  %s: DIFFER\n    old %A\n    new %A" what x y
    printfn "%s" label
    same "filter" a.Filter b.Filter
    same "patient" a.Patient b.Patient
    same "scenario count" a.Scenarios.Length b.Scenarios.Length
    same "scenario names" (a.Scenarios |> Array.map _.Name) (b.Scenarios |> Array.map _.Name)
    same "prescriptions" (a.Scenarios |> Array.map _.Prescription) (b.Scenarios |> Array.map _.Prescription)
    same "preparations" (a.Scenarios |> Array.map _.Preparation) (b.Scenarios |> Array.map _.Preparation)
    same "administrations" (a.Scenarios |> Array.map _.Administration) (b.Scenarios |> Array.map _.Administration)
    // an order's id is minted and its start read from the clock at every evaluation; compare
    // the orders with both blanked
    let blankId (o: Order) =
        (sprintf "%A" o).Replace(o.Id, "ID").Split('\n')
        |> Array.map (fun l -> if l.Trim().StartsWith "Start =" then "Start = CLOCK" else l)
        |> String.concat "\n"
    let oa = a.Scenarios |> Array.map (_.Order >> blankId)
    let ob = b.Scenarios |> Array.map (_.Order >> blankId)

    if oa = ob then
        printfn "  orders: same but for the minted id and the clock"
    else
        printfn "  orders: DIFFER beyond the id"

        Array.zip oa ob
        |> Array.iteri (fun i (x, y) ->
            let xl = x.Split('\n')
            let yl = y.Split('\n')

            Array.zip xl yl
            |> Array.filter (fun (l1, l2) -> l1 <> l2)
            |> Array.truncate 12
            |> Array.iter (fun (l1, l2) -> printfn "    [%i] old %s\n        new %s" i (l1.Trim()) (l2.Trim()))
        )
    same "intake" a.Intake b.Intake
    same "demo" a.DemoVersion b.DemoVersion

let first = oldPath Shared.Api.OrderContextCommand.UpdateOrderContext start
let firstNew = newPath Shared.Api.OrderContextCommand.UpdateOrderContext start
compare "first pass (generic and route)" first firstNew

let indication = first.Filter.Indications |> Array.tryHead
let second = oldPath Shared.Api.OrderContextCommand.UpdateOrderContext { first with Filter = { first.Filter with Indication = indication } }
let secondNew = newPath Shared.Api.OrderContextCommand.UpdateOrderContext { firstNew with Filter = { firstNew.Filter with Indication = indication } }
compare $"second pass (indication %A{indication})" second secondNew

let third = oldPath (Shared.Api.OrderContextCommand.IncreaseOrderableDoseQuantityProperty(1, false)) second
let thirdNew = newPath (Shared.Api.OrderContextCommand.IncreaseOrderableDoseQuantityProperty(1, false)) secondNew
compare "third pass (dose stepped up once)" third thirdNew
