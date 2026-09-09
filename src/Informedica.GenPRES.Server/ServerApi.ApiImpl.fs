namespace ServerApi


[<AutoOpen>]
module ApiImpl =

    /// Creates the IServerApi implementation for one request using the composition root.
    let createServerApi (env: AppEnv) (cookie: SessionCookie) : Shared.Api.IServerApi =
        CompositionRoot.compose env cookie
