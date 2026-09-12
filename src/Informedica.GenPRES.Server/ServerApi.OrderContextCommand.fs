namespace ServerApi

open Shared.Types
open Shared.Api


/// The order-context member: the prescribing workbench over the order-context port.
module OrderContextCommand =

    let processCmd (env: AppEnv) (cmd: OrderContextCommand, ctx: OrderContext) = env.orderContext.evaluate cmd ctx
