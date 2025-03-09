[<AutoOpen>]
module ServerApiImpl

open Shared.Api


/// An implementation of the Shared IServerApi protocol.
let serverApi: IServerApi =
    {
        processMessage =
            fun msg ->
                async {
                    return msg |> Message.processMsg
                }

        testApi =
            fun () ->
                async {
                    return "Hello world!"
                }
    }