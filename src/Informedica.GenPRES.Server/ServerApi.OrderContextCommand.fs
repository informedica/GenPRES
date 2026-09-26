namespace ServerApi

open Informedica.GenOrder.Lib
open Shared.Types
open Shared.Api


/// The order-context member: the prescribing workbench over the order-context port.
module OrderContextCommand =

    /// The context's patient at the Session's age.
    let aged (age: Age option) (cmd: OrderContextCommand, ctx: OrderContext) = cmd, OrderContextMapper.aged age ctx


    /// The patient the request edits: the context's.
    let patientOf (_: OrderContextCommand, ctx: OrderContext) = Some ctx.Patient


    /// The context's patient made at the inbound boundary, the context parsed into the
    /// domain, the verb mapped, the port asked, the answer mapped out with the environment's
    /// demo flag. A draft that is none, or a context the domain does not read, is refused.
    let processCmd (env: AppEnv) (cmd: OrderContextCommand, ctx: OrderContext) =
        Patient.over
            ctx.Patient
            (fun () ->
                match ctx |> OrderContextService.parse with
                | Error errs -> async { return Error errs }
                | Ok pc ->
                    async {
                        let! answer = env.orderContext.evaluate (OrderContextMapper.Command.toDomain cmd) pc

                        return
                            answer
                            |> Result.map (PlanContext.Dto.toDto >> OrderContextMapper.toModel env.demo)
                    }
            )
