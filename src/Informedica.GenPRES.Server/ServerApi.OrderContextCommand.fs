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
    /// argumentation is written by the server and answered without the domain.
    let processCmd (env: AppEnv) (cmd: OrderContextCommand, ctx: OrderContext) =
        let ctx, domainCmd =
            match cmd with
            | OrderContextCommand.SetArgumentationProperty text ->
                ctx |> Shared.Models.OrderContext.Argumentation.write text, None
            | cmd -> ctx, Some(OrderContextMapper.Command.toDomain ctx.Category cmd)

        evaluated env ctx domainCmd


    /// The patients the request carries mapped: the context's, and a patient change's own.
    let patientsActive (f: Patient -> Patient) (cmd: ActiveOrderContextCommand, ctx: OrderContext) =
        let cmd =
            match cmd with
            | ActiveOrderContextCommand.ChangePatient pat -> ActiveOrderContextCommand.ChangePatient(f pat)
            | ActiveOrderContextCommand.Command _ -> cmd

        cmd, OrderContextMapper.patients f ctx


    /// The patient the request edits: a patient change's own, else the context's.
    let patientOfActive (cmd: ActiveOrderContextCommand, ctx: OrderContext) =
        match cmd with
        | ActiveOrderContextCommand.ChangePatient pat -> Some pat
        | ActiveOrderContextCommand.Command _ -> Some ctx.Patient


    /// A command over the order context being worked on, or that context evaluated again for
    /// the patient a patient change carries: the patient written into the context, so that it is
    /// made and parsed as the context's own, and the domain takes the parsed context's patient.
    let processActive (env: AppEnv) (cmd: ActiveOrderContextCommand, ctx: OrderContext) =
        match cmd with
        | ActiveOrderContextCommand.Command cmd -> processCmd env (cmd, ctx)
        | ActiveOrderContextCommand.ChangePatient pat ->
            let change (ctx: Informedica.GenOrder.Lib.Types.OrderContext) =
                Informedica.GenOrder.Lib.OrderContext.ChangePatient(ctx, ctx.Patient)

            Some change |> evaluated env { ctx with Patient = pat }
