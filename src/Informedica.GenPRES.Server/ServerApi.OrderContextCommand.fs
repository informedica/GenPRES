namespace ServerApi

open Informedica.GenOrder.Lib
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Api


/// The order-context member: the prescribing workbench over the order-context port.
module OrderContextCommand =

    /// Whether the command changes the choices or the scenario of the context, so that the answer
    /// carries the linked orders offered again; a value step keeps the scenario, so the answer carries
    /// no offers.
    let offersLinked =
        function
        | OrderViewCommand.SetNthFilterProperty _
        | OrderViewCommand.ClearFilterProperty _
        | OrderViewCommand.ClearAllFilterProperty
        | OrderViewCommand.SelectNthOrderScenario _
        | OrderViewCommand.SeedFilter _ -> true
        | OrderViewCommand.ResetOrderScenario
        | OrderViewCommand.DecreaseScheduleFrequencyProperty
        | OrderViewCommand.IncreaseScheduleFrequencyProperty
        | OrderViewCommand.SetMinScheduleFrequencyProperty
        | OrderViewCommand.SetMaxScheduleFrequencyProperty
        | OrderViewCommand.SetMedianScheduleFrequencyProperty
        | OrderViewCommand.DecreaseOrderableDoseQuantityProperty _
        | OrderViewCommand.IncreaseOrderableDoseQuantityProperty _
        | OrderViewCommand.SetMinOrderableDoseQuantityProperty
        | OrderViewCommand.SetMaxOrderableDoseQuantityProperty
        | OrderViewCommand.SetMedianOrderableDoseQuantityProperty
        | OrderViewCommand.SetOrderableDoseQuantityPercProperty _
        | OrderViewCommand.DecreaseOrderableDoseRateProperty _
        | OrderViewCommand.IncreaseOrderableDoseRateProperty _
        | OrderViewCommand.SetMinOrderableDoseRateProperty
        | OrderViewCommand.SetMaxOrderableDoseRateProperty
        | OrderViewCommand.SetMedianOrderableDoseRateProperty
        | OrderViewCommand.DecreaseComponentOrderableQuantityProperty _
        | OrderViewCommand.IncreaseComponentOrderableQuantityProperty _
        | OrderViewCommand.SetMinComponentOrderableQuantityProperty _
        | OrderViewCommand.SetMaxComponentOrderableQuantityProperty _
        | OrderViewCommand.SetMedianComponentOrderableQuantityProperty _
        | OrderViewCommand.SetNthDiluentProperty _
        | OrderViewCommand.ClearDiluentProperty
        | OrderViewCommand.SetNthComponentsProperty _
        | OrderViewCommand.SetNthScheduleProperty _
        | OrderViewCommand.ReCalcScheduleProperty _
        | OrderViewCommand.SetNthOrderableProperty _
        | OrderViewCommand.ReCalcOrderableProperty _
        | OrderViewCommand.SetNthComponentProperty _
        | OrderViewCommand.ReCalcComponentProperty _
        | OrderViewCommand.SetNthItemProperty _
        | OrderViewCommand.ReCalcItemProperty _
        | OrderViewCommand.SetArgumentationProperty _ -> false


    /// The context's patient made at the inbound boundary, the context parsed into the domain, the
    /// port asked with the domain command, or the context answered as it is without one; the
    /// outcome mapped out as the response with the environment's demo flag. A draft that is none,
    /// or a context the domain does not read, is a failure.
    let evaluated (env: AppEnv) (viewCmd: OrderViewCommand) (ctx: OrderContext) domainCmd =
        Patient.over
            ctx.Patient
            (fun () ->
                match ctx |> OrderContextService.parse, domainCmd with
                | Error errs, _ -> async { return Error errs }
                | Ok pc, None ->
                    async { return Ok(Evaluated pc) |> Result.map (OrderContextService.toResponse env.demo) }
                | Ok pc, Some cmd ->
                    async {
                        let! answer = env.orderContext.evaluate cmd pc

                        match answer with
                        | Ok(Evaluated e) when viewCmd |> offersLinked ->
                            let! offers = env.orderContext.linkedOrders e

                            // the offers never fail the evaluation: without them the answer offers none
                            return
                                Evaluated e
                                |> OrderContextService.toResponse env.demo
                                |> OrderContextService.withLinkedOrders env.demo (offers |> Result.defaultValue [||])
                                |> Ok
                        | _ -> return answer |> Result.map (OrderContextService.toResponse env.demo)
                    }
            )


    /// The verb mapped to the domain's command and the context evaluated with it; the
    /// argumentation is written by the server and answered without the domain, and a reset
    /// clears it, since it puts the order back within the rules.
    let processViewCmd (env: AppEnv) (cmd: OrderViewCommand, ctx: OrderContext) =
        let ctx, domainCmd =
            match cmd with
            | OrderViewCommand.SetArgumentationProperty text ->
                ctx |> Informedica.GenPRES.Shared.Models.OrderContext.Argumentation.write text, None
            | OrderViewCommand.ResetOrderScenario ->
                { ctx with Argumentation = None }, Some(OrderContextMapper.Command.toDomain ctx.Category cmd)
            | cmd -> ctx, Some(OrderContextMapper.Command.toDomain ctx.Category cmd)

        evaluated env cmd ctx domainCmd


    /// A command over the order context being worked on.
    let processCmd (env: AppEnv) (OrderContextCommand.Command(cmd, ctx)) = processViewCmd env (cmd, ctx)
