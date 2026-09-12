namespace ServerApi


module Command =

    open Shared.Api


    /// Every command left here needs the formulary loaded. Applied by Compute.bound, not here.
    let gate (_: Command) = Gate.RequiresLoaded


    /// The dispatcher: the order context to its port. Gating, logging and the Session notice
    /// are Compute.bound's.
    let processCmd (env: AppEnv) cmd =
        match cmd with
        | OrderContextCmd(ctxCmd, ctx) ->
            async {
                let! result = env.orderContext.evaluate ctxCmd ctx
                return result |> Result.map (OrderContextResult >> OrderContextResp)
            }
