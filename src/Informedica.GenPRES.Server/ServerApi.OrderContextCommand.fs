namespace ServerApi

open Informedica.GenOrder.Lib
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Api


/// Handles a command on an order context.
module OrderContextCommand =

    /// Checks the patient data, reads the order context and runs the domain command on it.
    /// Without a command, returns the order context as read. Returns the messages for invalid
    /// patient data or an invalid order context.
    let evaluated (env: AppEnv) (viewCmd: OrderViewCommand) (ctx: OrderContext) domainCmd =
        Patient.over
            ctx.PatientData
            (fun () ->
                match ctx |> OrderContextService.parse (env.today ()), domainCmd with
                | Error errs, _ -> async { return Error errs }
                | Ok pc, None ->
                    async { return Ok(Evaluated pc) |> Result.map (OrderContextService.toResponse env.demo [||]) }
                | Ok pc, Some cmd ->
                    async {
                        let! answer = env.orderContext.evaluate cmd pc

                        match answer with
                        | Ok(Evaluated e) when viewCmd |> OrderViewCommand.offersLinked ->
                            let! offers = env.orderContext.linkedOrders e

                            // An error in the linked orders gives no linked orders, not an error.
                            return
                                Evaluated e
                                |> OrderContextService.toResponse env.demo (offers |> Result.defaultValue [||])
                                |> Ok
                        | _ -> return answer |> Result.map (OrderContextService.toResponse env.demo [||])
                    }
            )


    /// Maps the command to GenORDER and runs it. The server writes the argumentation itself.
    /// A reset clears the argumentation, because the order is back within the rules.
    let processViewCmd (env: AppEnv) (cmd: OrderViewCommand, ctx: OrderContext) =
        let ctx, domainCmd =
            match cmd with
            | OrderViewCommand.SetArgumentationProperty text ->
                ctx |> Informedica.GenPRES.Shared.Models.OrderContext.Argumentation.write text, None
            | OrderViewCommand.ResetOrderScenario ->
                { ctx with Argumentation = None }, Some(OrderContextMapper.Command.toDomain ctx.Category cmd)
            | cmd -> ctx, Some(OrderContextMapper.Command.toDomain ctx.Category cmd)

        evaluated env cmd ctx domainCmd


    /// Runs a command on an order context.
    let processCmd (env: AppEnv) (OrderContextCommand.Command(cmd, ctx)) = processViewCmd env (cmd, ctx)
