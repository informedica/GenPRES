namespace ServerApi

open Informedica.GenOrder.Lib
// after the domain, so that the contract model's cases win unqualified
open Shared.Types
open Shared.Api


/// The plan member: the one plan, nutrition included, over the plan port.
module OrderPlanCommand =

    /// The contract model's plan parsed into the domain, or the reasons it is none in the
    /// server's words; an argumentation over the cap on any context is refused first, so a
    /// plan submitted for signing is held to it too.
    let parsePlan (plan: OrderPlan) : Result<Informedica.GenOrder.Lib.Types.OrderPlan, string[]> =
        plan.OrderContexts
        |> OrderContextService.Argumentation.checkAll
        |> Result.bind (fun () ->
            plan
            |> OrderPlanMapper.ofModel
            |> Informedica.GenOrder.Lib.OrderPlan.Dto.fromDto
            |> Result.mapError (List.map OrderContextMapper.words >> List.toArray)
        )


    /// The contexts parsed, all of them or the first refusal.
    let parseContexts (contexts: OrderContext[]) : Result<PlanContext[], string[]> =
        contexts
        |> Array.fold
            (fun acc ctx ->
                match acc, ctx |> OrderContextService.parse with
                | Ok pcs, Ok pc -> Ok(pc :: pcs)
                | Error e, _
                | _, Error e -> Error e
            )
            (Ok [])
        |> Result.map (List.rev >> List.toArray)


    let private both a b =
        match a, b with
        | Ok a, Ok b -> Ok(a, b)
        | Error e, _
        | _, Error e -> Error e


    /// The port asked over what parsed, else the refusal; the answer mapped out with the
    /// environment's demo flag.
    let private answer
        (env: AppEnv)
        (parsed: Result<'a, string[]>)
        (run: 'a -> Async<Result<Informedica.GenOrder.Lib.Types.OrderPlan, string[]>>)
        =
        match parsed with
        | Error e -> async { return Error e }
        | Ok v ->
            async {
                let! result = run v

                return
                    result
                    |> Result.map (Informedica.GenOrder.Lib.OrderPlan.Dto.toDto >> OrderPlanMapper.toModel env.demo)
            }


    /// The plan's patient and that of every context it carries, and the context's where a
    /// command carries one, made at the inbound boundary; the plan and the context parsed into
    /// the domain, the verb mapped, the port asked, the answer mapped out with the environment's
    /// demo flag. A draft that is none, or a plan the domain does not read, is refused.
    let rec processCmd (env: AppEnv) (cmd: OrderPlanCommand) =
        match cmd with
        | OrderPlanCommand.Recalculate plan ->
            Patient.overAll (Patient.ofPlan plan) (fun () -> answer env (parsePlan plan) env.orderPlan.recalculate)
        | OrderPlanCommand.Navigate(plan, contextId, ctxCmd, ctx) ->
            match ctxCmd with
            // the argumentation is written by the server on the plan's context, without the domain
            | OrderViewCommand.SetArgumentationProperty text when
                plan.OrderContexts |> Array.exists (fun c -> c.Id = contextId)
                ->
                let ctx = ctx |> Shared.Models.OrderContext.Argumentation.write text

                let written (c: OrderContext) =
                    if c.Id = contextId then
                        { c with Argumentation = ctx.Argumentation }
                    else
                        c
                processCmd
                    env
                    (OrderPlanCommand.Recalculate { plan with OrderContexts = Array.map written plan.OrderContexts })
            | OrderViewCommand.SetArgumentationProperty _ ->
                async { return Error [| OrderPlanMapper.words [||] (OrderPlanError.NoSuchContext contextId) |] }
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
                    (Patient.ofPlan plan @ [ ctx.Patient ])
                    (fun () ->
                        answer
                            env
                            (both (parsePlan plan) (OrderContextService.parse ctx))
                            (fun (p, pc) ->
                                env.orderPlan.navigate
                                    p
                                    contextId
                                    (OrderContextMapper.Command.toDomain ctx.Category ctxCmd)
                                    pc
                            )
                    )
        | OrderPlanCommand.AddOrderContext(plan, ctx) ->
            Patient.overAll
                (Patient.ofPlan plan @ [ ctx.Patient ])
                (fun () ->
                    answer
                        env
                        (both (parsePlan plan) (OrderContextService.parse ctx))
                        (fun (p, pc) -> env.orderPlan.addOrderContext p pc)
                )
        | OrderPlanCommand.NewOrderContext(plan, category) ->
            Patient.overAll
                (Patient.ofPlan plan)
                (fun () ->
                    answer
                        env
                        (parsePlan plan)
                        (fun p -> env.orderPlan.newOrderContext p (OrderPlanMapper.nutritionCategory category))
                )
        | OrderPlanCommand.RemoveOrderContexts(plan, ids) ->
            Patient.overAll
                (Patient.ofPlan plan)
                (fun () -> answer env (parsePlan plan) (fun p -> env.orderPlan.removeOrderContexts p ids))
        | OrderPlanCommand.Open(pat, contexts) ->
            Patient.overAll
                (pat :: (contexts |> Array.map _.Patient |> Array.toList))
                (fun () ->
                    answer
                        env
                        (both (Patient.parse pat) (parseContexts contexts))
                        (fun (p, pcs) -> env.orderPlan.openWith p pcs)
                )
