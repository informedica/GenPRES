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
        match Shared.Api.OrderContextCommand.toChange cmd ctx with
        | Error err -> async { return Error [| err |] }
        | Ok(change, ctx) ->
            Patient.over
                ctx.Patient
                (fun () ->
                    match ctx |> OrderContextService.parse, change with
                    | Error errs, _ -> async { return Error errs }
                    | Ok pc, None ->
                        async { return Ok(Evaluated pc) |> Result.map (OrderContextService.toResponse env.demo) }
                    | Ok pc, Some cmd ->
                        async {
                            let! answer = env.orderContext.evaluate (OrderContextMapper.Command.toDomain cmd) pc

                            return answer |> Result.map (OrderContextService.toResponse env.demo)
                        }
                )
