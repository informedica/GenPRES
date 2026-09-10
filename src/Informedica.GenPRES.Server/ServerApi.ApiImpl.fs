namespace ServerApi


[<AutoOpen>]
module ApiImpl =

    /// Creates the IServerApi implementation for one request using the composition root.
    let createServerApi
        (settings: Shared.Api.ServerSettings)
        (env: AppEnv)
        (cookie: SessionCookie)
        : Shared.Api.IServerApi
        =
        CompositionRoot.compose settings env cookie
