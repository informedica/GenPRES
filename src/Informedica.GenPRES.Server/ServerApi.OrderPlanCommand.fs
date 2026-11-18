namespace ServerApi

open FsToolkit.ErrorHandling
open Informedica.GenOrder.Lib
// Opened last, so an unqualified case name refers to the contract case.
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Api


/// Handles a command on an order plan.
module OrderPlanCommand =

    /// Reads an order plan on the date today. Returns the messages for an argumentation that is
    /// too long or an invalid plan.
    let parsePlan today (plan: OrderPlan) : Result<Informedica.GenOrder.Lib.Types.OrderPlan, string[]> =
        plan.OrderContexts
        |> OrderContextService.Argumentation.checkAll
        |> Result.bind (fun () ->
            plan
            |> OrderPlanMapper.ofModel today
            |> Informedica.GenOrder.Lib.OrderPlan.Dto.fromDto today
            |> Result.mapError (List.map OrderContextMapper.words >> List.toArray)
        )


    /// Reads the order contexts. Returns the messages of the first invalid one.
    let parseContexts today (contexts: OrderContext[]) : Result<PlanContext[], string[]> =
        contexts |> Array.traverseResultM (OrderContextService.parse today)


    let private both a b =
        match a, b with
        | Ok a, Ok b -> Ok(a, b)
        | Error e, _
        | _, Error e -> Error e


    /// Runs the port function on a parsed value and maps the result. Returns the parse error.
    let private answerWith map parsed run =
        match parsed with
        | Error e -> async { return Error e }
        | Ok v ->
            async {
                let! result = run v
                return result |> Result.map map
            }


    /// Runs the port function on a parsed value and maps the returned plan to the contract.
    let private answer (env: AppEnv) parsed run =
        answerWith (Informedica.GenOrder.Lib.OrderPlan.Dto.toDto >> OrderPlanMapper.toModel env.demo) parsed run


    /// Runs the port function on the plan when every patient of the plan passes the check.
    let overPlan (env: AppEnv) run (plan: OrderPlan) =
        Patient.overAll (Patient.ofPlan plan) (fun () -> answer env (parsePlan (env.today ()) plan) run)


    /// Recalculates the totals of the plan.
    let recalculate (env: AppEnv) (plan: OrderPlan) = plan |> overPlan env env.orderPlan.recalculate


    /// Runs a command on an order plan. Returns the messages for invalid patient data or an
    /// invalid plan.
    let processCmd (env: AppEnv) (cmd: OrderPlanCommand) =
        let today = env.today ()

        match cmd with
        // the server writes the patient and the rows; the client sends the plan as answered
        | OrderPlanCommand.UpdatePatientData(pat, plan) ->
            { plan with PatientData = pat } |> overPlan env env.orderPlan.followPatientData
        | OrderPlanCommand.FilterRows(ids, plan) -> recalculate env { plan with Filtered = ids }
        | OrderPlanCommand.EvaluateContext(plan, contextId, ctxCmd, ctx) ->
            match ctxCmd with
            // the argumentation is written by the server on the plan's context, without the domain
            | OrderViewCommand.SetArgumentationProperty text when
                plan.OrderContexts |> Array.exists (fun c -> c.Id = contextId)
                ->
                let ctx = ctx |> Informedica.GenPRES.Shared.Models.OrderContext.Argumentation.write text

                let written (c: OrderContext) =
                    if c.Id = contextId then
                        { c with Argumentation = ctx.Argumentation }
                    else
                        c
                recalculate env { plan with OrderContexts = Array.map written plan.OrderContexts }
            | OrderViewCommand.SetArgumentationProperty _ ->
                async { return Error [| OrderPlanMapper.words (OrderPlanError.NoSuchContext contextId) |] }
            | ctxCmd ->
                // a reset clears the argumentation, on the context and on the plan's copy of it, since
                // it puts the order back within the rules
                let plan, ctx =
                    match ctxCmd with
                    | OrderViewCommand.ResetOrderScenario ->
                        let clear (c: OrderContext) =
                            if c.Id = contextId then
                                { c with Argumentation = None }
                            else
                                c

                        { plan with OrderContexts = Array.map clear plan.OrderContexts },
                        { ctx with Argumentation = None }
                    | _ -> plan, ctx

                Patient.overAll
                    (Patient.ofPlan plan @ [ ctx.PatientData ])
                    (fun () ->
                        answer
                            env
                            (both (parsePlan today plan) (OrderContextService.parse today ctx))
                            (fun (p, pc) ->
                                env.orderPlan.evaluateContext
                                    p
                                    contextId
                                    (OrderContextMapper.Command.toDomain ctx.Category ctxCmd)
                                    pc
                            )
                    )
        | OrderPlanCommand.AddOrderContext(plan, ctx, linked) ->
            Patient.overAll
                (Patient.ofPlan plan @ ((ctx :: Array.toList linked) |> List.map _.PatientData))
                (fun () ->
                    answer
                        env
                        (both
                            (parsePlan today plan)
                            (both (OrderContextService.parse today ctx) (parseContexts today linked)))
                        (fun (p, (pc, pcs)) -> env.orderPlan.addOrderContexts p pc pcs)
                )
        | OrderPlanCommand.RemoveOrderContexts(plan, ids) ->
            Patient.overAll
                (Patient.ofPlan plan)
                (fun () -> answer env (parsePlan today plan) (fun p -> env.orderPlan.removeOrderContexts p ids))
        | OrderPlanCommand.Open(pat, contexts) ->
            Patient.overAll
                (pat :: (contexts |> Array.map _.PatientData |> Array.toList))
                (fun () ->
                    answer
                        env
                        (both (Patient.parse today pat) (parseContexts today contexts))
                        (fun (p, pcs) -> env.orderPlan.openWith p pcs)
                )


    /// The totals over the orders of the query, from the plan port. The patient of the query and of every
    /// context are checked first, as for a plan command.
    let processTotals (env: AppEnv) (query: TotalsQuery) =
        let today = env.today ()

        Patient.overAll
            (query.PatientData
             :: (query.OrderContexts |> Array.map _.PatientData |> Array.toList))
            (fun () ->
                answerWith
                    (Totals.Dto.toDto >> OrderContextMapper.totalsBack)
                    (both (Patient.parse today query.PatientData) (parseContexts today query.OrderContexts))
                    (fun (pat, pcs) -> env.orderPlan.totals pat pcs)
            )
