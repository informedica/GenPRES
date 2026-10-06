namespace ServerApi

open Informedica.GenOrder.Lib
open Shared.Types
open Shared.Api


/// The order-context member: the prescribing workbench over the order-context port.
module OrderContextCommand =

    /// The context's patient made at the inbound boundary, the context parsed into the domain, the
    /// port asked with the domain command, or the context answered as it is without one; the
    /// outcome mapped out as the response with the environment's demo flag. A draft that is none,
    /// or a context the domain does not read, is a failure.
    let evaluated (env: AppEnv) (ctx: OrderContext) domainCmd =
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

                        return answer |> Result.map (OrderContextService.toResponse env.demo)
                    }
            )


    /// The verb mapped to the domain's command and the context evaluated with it; the
    /// argumentation is written by the server and answered without the domain, and a reset
    /// clears it, since it puts the order back within the rules.
    let processCmd (env: AppEnv) (cmd: OrderContextCommand, ctx: OrderContext) =
        let ctx, domainCmd =
            match cmd with
            | OrderContextCommand.SetArgumentationProperty text ->
                ctx |> Shared.Models.OrderContext.Argumentation.write text, None
            | OrderContextCommand.ResetOrderScenario ->
                { ctx with Argumentation = None }, Some(OrderContextMapper.Command.toDomain ctx.Category cmd)
            | cmd -> ctx, Some(OrderContextMapper.Command.toDomain ctx.Category cmd)

        evaluated env ctx domainCmd


    /// A command over the order context being worked on, or that context evaluated again for
    /// the patient a patient change carries: the patient written into the context, so that it is
    /// made and parsed as the context's own, and the domain takes the parsed context's patient.
    let processActive (env: AppEnv) (cmd: ActiveOrderContextCommand, ctx: OrderContext) =
        match cmd with
        | ActiveOrderContextCommand.Command cmd -> processCmd env (cmd, ctx)
        | ActiveOrderContextCommand.PatientChanged pat ->
            let change (ctx: Informedica.GenOrder.Lib.Types.OrderContext) =
                Informedica.GenOrder.Lib.OrderContext.PatientChanged(ctx, ctx.Patient)

            Some change |> evaluated env { ctx with Patient = pat }
