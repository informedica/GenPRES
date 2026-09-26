namespace ServerApi

open Informedica.GenOrder.Lib
// after the domain, so that the contract model's cases win unqualified
open Shared.Types
open Shared.Api


/// The plan member: the one plan, nutrition included, over the plan port.
module OrderPlanCommand =

    /// The contract model's plan parsed into the domain, or the reasons it is none in the
    /// server's words.
    let parsePlan (plan: OrderPlan) : Result<Informedica.GenOrder.Lib.Types.OrderPlan, string[]> =
        plan
        |> OrderPlanMapper.ofModel
        |> Informedica.GenOrder.Lib.OrderPlan.Dto.fromDto
        |> Result.mapError (List.map OrderContextMapper.words >> List.toArray)


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


    /// The plan's patient and that of every context in it at the Session's age.
    let agedPlan (age: Age option) (plan: OrderPlan) : OrderPlan =
        { plan with
            Patient = plan.Patient |> Patient.aged age
            OrderContexts = plan.OrderContexts |> Array.map (OrderContextMapper.aged age)
        }


    /// Every patient the command carries at the Session's age: the plan's and its contexts',
    /// and the context's where the command carries one.
    let aged (age: Age option) (cmd: OrderPlanCommand) : OrderPlanCommand =
        match cmd with
        | OrderPlanCommand.Recalculate plan -> OrderPlanCommand.Recalculate(agedPlan age plan)
        | OrderPlanCommand.Navigate(plan, contextId, ctxCmd, ctx) ->
            OrderPlanCommand.Navigate(agedPlan age plan, contextId, ctxCmd, OrderContextMapper.aged age ctx)
        | OrderPlanCommand.AddOrderContext(plan, ctx) ->
            OrderPlanCommand.AddOrderContext(agedPlan age plan, OrderContextMapper.aged age ctx)
        | OrderPlanCommand.NewOrderContext(plan, category) ->
            OrderPlanCommand.NewOrderContext(agedPlan age plan, category)
        | OrderPlanCommand.RemoveOrderContexts(plan, ids) ->
            OrderPlanCommand.RemoveOrderContexts(agedPlan age plan, ids)
        | OrderPlanCommand.Open(pat, contexts) ->
            OrderPlanCommand.Open(Patient.aged age pat, contexts |> Array.map (OrderContextMapper.aged age))


    /// The plan's patient and that of every context it carries, and the context's where a
    /// command carries one, made at the inbound boundary; the plan and the context parsed into
    /// the domain, the verb mapped, the port asked, the answer mapped out with the environment's
    /// demo flag. A draft that is none, or a plan the domain does not read, is refused.
    let processCmd (env: AppEnv) (cmd: OrderPlanCommand) =
        match cmd with
        | OrderPlanCommand.Recalculate plan ->
            Patient.overAll (Patient.ofPlan plan) (fun () -> answer env (parsePlan plan) env.orderPlan.recalculate)
        | OrderPlanCommand.Navigate(plan, contextId, ctxCmd, ctx) ->
            Patient.overAll
                (Patient.ofPlan plan @ [ ctx.Patient ])
                (fun () ->
                    answer
                        env
                        (both (parsePlan plan) (OrderContextService.parse ctx))
                        (fun (p, pc) ->
                            env.orderPlan.navigate p contextId (OrderContextMapper.Command.toDomain ctxCmd) pc
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
