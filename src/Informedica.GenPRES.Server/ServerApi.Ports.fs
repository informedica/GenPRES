namespace ServerApi

open Shared.Types
open Shared.Api


type FormularyPort =
    {
        getFormulary: Formulary -> Async<Result<Formulary, string[]>>
        getParenteralia: Parenteralia -> Async<Result<Parenteralia, string[]>>
    }


type OrderContextPort = { evaluate: OrderContextCommand -> OrderContext -> Async<Result<OrderContext, string[]>> }


type OrderPlanPort =
    {
        updateOrderPlan: OrderPlan -> (OrderContextCommand * OrderContext) option -> Async<Result<OrderPlan, string[]>>
        filterOrderPlan: OrderPlan -> Async<Result<OrderPlan, string[]>>
    }


type NutritionPlanPort =
    {
        initNutritionPlan: Patient -> Async<Result<NutritionPlan, string[]>>
        addNutritionContext: NutritionPlan * NutritionCategory -> Async<Result<NutritionPlan, string[]>>
        removeNutritionContext: NutritionPlan * string -> Async<Result<NutritionPlan, string[]>>
        updateNutritionOrderContext: NutritionPlan * string * OrderContext -> Async<Result<NutritionPlan, string[]>>
        selectNutritionOrderScenario: NutritionPlan * string * OrderContext -> Async<Result<NutritionPlan, string[]>>
        navigateNutritionOrderContext:
            NutritionPlan * string * OrderContextCommand * OrderContext -> Async<Result<NutritionPlan, string[]>>
    }


type InteractionPort =
    {
        checkInteractions: string list -> Async<Result<DrugInteraction list, string[]>>
        getDrugNames: unit -> Async<Result<string list, string[]>>
    }


type LogAnalyzerPort =
    {
        listLogFiles: unit -> Async<Result<LogFileInfo[], string[]>>
        analyzeLogFile: string -> Async<Result<string, string[]>>
    }


/// Who the IdentityProvider says is at the browser (Concept 4). The Session's User is derived
/// from it (Rule 4), never from the Launch.
type BrowserIdentity =
    {
        Login: string
        DisplayName: string
    }


/// What the UserRegistry says about a login at this launch (Rules 5, 6, 24): the User with the
/// Role, the Patient active in MainEHR, and whether a PIN is set.
type UserStanding =
    {
        User: UserContext
        ActivePatientId: string option
        PinSet: bool
    }


/// Actor 8. `authorizeUrl` is where the browser is sent with the `state` (4.2); `redeem`
/// exchanges the callback's code for the identity over the server's own connection (4.4, C6).
type IdentityProviderPort =
    {
        authorizeUrl: string -> string
        redeem: string -> BrowserIdentity option
    }


type UserRegistryPort = { standing: BrowserIdentity -> UserStanding option }


/// The PatientDataPlatform, read once at the launch (Concept 2). `None` is not a refusal
/// (ext 6a): the Session opens without imported data.
type PatientDataPort = { read: string -> Patient option }


/// The session adapter's answer to a presentation. The session id is the server's to put in
/// the cookie; the composition root maps this to the client's `LaunchOutcome` without it.
/// `RedirectTo` carries the `state` the edge writes to the state cookie (uc-01 step 4.2) next
/// to the url that carries it to the IdentityProvider.
[<RequireQualifiedAccess>]
type LaunchResult =
    | Opened of sessionId: string * SessionOpened
    | RedirectTo of url: string * state: string
    | Refused of LaunchRefusal


/// What the callback (4.5) brings: the `state` from the url and from the cookie, and either a
/// code or the IdentityProvider's error.
type Callback =
    {
        State: string
        StateCookie: string option
        Code: string option
        Error: string option
    }


/// The answer to a callback: where the browser goes next, and the session id for the cookie
/// when a Session opened.
[<RequireQualifiedAccess>]
type CallbackResult =
    | Opened of sessionId: string * redirect: string
    | Refused of LaunchRefusal * redirect: string


type SessionPort =
    {
        // idempotent per public key within the Launch lifetime (Rule 2, uc-01 Retries)
        present: Launch * PublicKey -> Async<LaunchResult>
        // uc-01 step 4.5: the browser is back from the IdentityProvider
        callback: Callback -> Async<CallbackResult>
        // by session id from the cookie
        find: string -> Async<SessionOpened option>
        // Rule 10: explicit close
        close: string -> Async<unit>
    }


/// The session cookie of one request, as three functions. Built from the HttpContext in
/// Server.fs; the composition root only reads, writes and deletes through it.
type SessionCookie =
    {
        read: unit -> string option
        write: string -> unit
        delete: unit -> unit
    }


/// The state cookie of one request (4.2): written with the redirect, read at the callback.
type LaunchStateCookie =
    {
        read: unit -> string option
        write: string -> unit
    }


type AppEnv =
    {
        formulary: FormularyPort
        orderContext: OrderContextPort
        orderPlan: OrderPlanPort
        nutritionPlan: NutritionPlanPort
        interaction: InteractionPort
        logAnalyzer: LogAnalyzerPort
        requireLoaded: unit -> string[] option
        session: SessionPort
    }
