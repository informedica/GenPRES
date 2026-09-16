namespace ServerApi

open Shared.Types
open Shared.Api


/// The order-context member: the prescribing workbench over the order-context port.
module OrderContextCommand =

    /// The context's patient made at the ingress; a draft that is none is refused.
    let processCmd (env: AppEnv) (cmd: OrderContextCommand, ctx: OrderContext) =
        Patient.over ctx.Patient (fun () -> env.orderContext.evaluate cmd ctx)
