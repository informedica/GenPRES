namespace ServerApi


[<AutoOpen>]
module ApiImpl =

    /// Creates the IServerApi implementation for one request using the composition root.
    let createServerApi
        (settings: Informedica.GenForm.Lib.Types.Departments -> Informedica.GenPRES.Shared.Api.ServerSettings)
        (env: AppEnv)
        (cookie: SessionCookie)
        (stateCookie: LaunchStateCookie)
        (enrolment: EnrolmentCookie)
        : Informedica.GenPRES.Shared.Api.IServerApi
        =
        CompositionRoot.compose settings env cookie stateCookie enrolment
