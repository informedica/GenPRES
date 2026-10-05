namespace ServerApi

open Informedica.GenOrder.Lib
open Shared.Types
open Shared.Api


/// The order-context member: the prescribing workbench over the order-context port.
module OrderContextCommand =

    /// The context's patient mapped.
    let patients (f: Patient -> Patient) (cmd: OrderContextCommand, ctx: OrderContext) =
        cmd, OrderContextMapper.patients f ctx


    /// The patient the request edits: the context's.
    let patientOf (_: OrderContextCommand, ctx: OrderContext) = Some ctx.Patient


    /// The context's patient made at the inbound boundary, the context parsed into the
    /// domain, the verb mapped, the port asked, the outcome mapped out as the response with
    /// the environment's demo flag. A draft that is none, or a context the domain does not
    /// read, is a failure.
    let processCmd (env: AppEnv) (cmd: OrderContextCommand, ctx: OrderContext) =
        // the argumentation is written by the server and answered without the domain
        let ctx, domainCmd =
            match cmd with
            | OrderContextCommand.SetArgumentationProperty text ->
                ctx |> Shared.Models.OrderContext.Argumentation.write text, None
            | cmd -> ctx, Some(OrderContextMapper.Command.toDomain ctx.Category cmd)

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
