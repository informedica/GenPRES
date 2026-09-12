namespace ServerApi

open Shared.Types
open Shared.Api


type FormularyPort =
    {
        getFormulary: Formulary -> Async<Result<Formulary, string[]>>
        getParenteralia: Parenteralia -> Async<Result<Parenteralia, string[]>>
    }


type OrderContextPort = { evaluate: OrderContextCommand -> OrderContext -> Async<Result<OrderContext, string[]>> }


/// The one plan: every member answers the plan with its totals recomputed over its orders.
type PlanPort =
    {
        recalculate: OrderPlan -> Async<Result<OrderPlan, string[]>>
        // the command into the nutrition context named, or into the selected scenario when None
        navigate:
            OrderPlan -> string option -> OrderContextCommand -> OrderContext -> Async<Result<OrderPlan, string[]>>
        addContext: OrderPlan -> NutritionCategory -> Async<Result<OrderPlan, string[]>>
        removeContext: OrderPlan -> string -> Async<Result<OrderPlan, string[]>>
    }


type InteractionPort =
    {
        checkInteractions: string list -> Async<Result<DrugInteraction list, string[]>>
        getDrugNames: unit -> Async<Result<string list, string[]>>
    }


/// What the admin commands need from the edge. The secret and the clock are values the DMZ
/// reads and passes in, so the command module never touches the environment.
type AdminPort =
    {
        // GENPRES_PASSWORD; None when unset, empty or whitespace, so every check fails closed
        secret: unit -> string option
        now: unit -> System.DateTimeOffset
        listLogFiles: unit -> Async<Result<LogFileInfo[], string[]>>
        analyzeLogFile: string -> Async<Result<string, string[]>>
        // the resource provider reloaded: the formulary and, later, the knowledge sheets
        reloadResources: unit -> Async<Result<unit, string[]>>
    }


/// Who the IdentityProvider says is at the browser. The Session's User is derived from it,
/// never from the Launch.
type BrowserIdentity =
    {
        Login: string
        DisplayName: string
    }


/// What the UserRegistry says about a login at this launch: the User with the Role, the
/// Patient active in MainEHR, and the mail address a confirmation code goes to. Whether a
/// PIN is set is the credential store's answer, not the registry's.
type UserStanding =
    {
        User: UserContext
        ActivePatientId: string option
        MailAddress: string
    }


/// The IdentityProvider. `authorizeUrl` is where the browser is sent with the `state`;
/// `redeem` exchanges the callback's code for the identity over the server's own connection.
type IdentityProviderPort =
    {
        authorizeUrl: string -> string
        redeem: string -> BrowserIdentity option
    }


type UserRegistryPort = { standing: BrowserIdentity -> UserStanding option }


/// The PatientDataPlatform, read once at the launch. `None` is not a refusal: the Session
/// opens without imported data.
type PatientDataPort = { read: string -> Patient option }


/// One mail from the Server to a User: a confirmation code, a notice that the PIN was set,
/// a notice at the wrong-PIN limit.
type Mail =
    {
        To: string
        Subject: string
        Body: string
    }


/// The MailService. Sending is fire and forget: the Server will record what it sent in the
/// audit (Rule 46, not built yet), not the outcome of delivery.
type MailPort = { send: Mail -> unit }


/// The session adapter's answer to a presentation. The session id is the server's to put in
/// the cookie; the composition root maps this to the client's `LaunchOutcome` without it.
/// `RedirectTo` carries the `state` the edge writes to the state cookie next to the url
/// that carries it to the IdentityProvider.
[<RequireQualifiedAccess>]
type LaunchResult =
    | Opened of sessionId: string * SessionOpened
    | RedirectTo of url: string * state: string
    | Refused of LaunchRefusal
    // the launch suspended at the PIN question; the browser holds the attempt in a cookie
    | Enrolling of attemptId: string


/// What the callback from the IdentityProvider brings: the `state` from the url and from the
/// cookie, and either a code or the IdentityProvider's error.
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
    // a reload of a callback whose Session a newer launch has since replaced: the browser
    // goes to the app on whatever cookie it holds, which is the newer Session's
    | Superseded of redirect: string
    // the launch suspended at the PIN question: the attempt for the enrolment cookie, and
    // how long the code it is bound to lives
    | Enrolling of attemptId: string * redirect: string * until: System.DateTime


/// The answer to a supplied PIN: the Session that opened, or why not.
[<RequireQualifiedAccess>]
type SupplyPinResult =
    | Opened of sessionId: string * SessionOpened
    | Refused of PinRefusal


/// What the store says about a session id from the cookie: the Session, nothing, or that the
/// server ended it, said as long as the browser still sends the cookie; the client
/// acknowledges with CloseSession, which deletes the cookie and drops the ending.
[<RequireQualifiedAccess>]
type SessionLookup =
    | Found of SessionOpened
    | NotFound
    | Ended of SessionEnding


type SessionPort =
    {
        // idempotent per public key within the Launch lifetime: a repeat from the same
        // browser is answered as the first presentation was
        present: Launch * PublicKey -> Async<LaunchResult>
        // the browser is back from the IdentityProvider
        callback: Callback -> Async<CallbackResult>
        // by session id from the cookie; an ending is told until it is acknowledged
        find: string -> Async<SessionLookup>
        // explicit close by the User
        close: string -> Async<unit>
        // what a browser holding an enrolment attempt is told
        findEnrolment: string -> Async<EnrolmentPending option>
        // the confirmation code and the chosen PIN, for the attempt in the cookie
        supplyPin: string -> string -> string -> Async<SupplyPinResult>
        // an attempt the browser gave up on (CloseSession while enrolling)
        dropEnrolment: string -> Async<unit>
        // a signing challenge over the plan as shown, for the Session the cookie names
        challenge: string -> OrderPlan * OpenedToken * string option -> Async<SigningResponse>
        // the signature, for the Session the cookie names
        submit: string -> Submission -> Async<SigningResponse>
        // every computing request: the Session the cookie names is marked seen and told
        // whether the record moved on or the Session ended
        seen: string -> OpenedToken option -> Async<RecordNotice option>
        // the version named becomes what the Session the cookie names opened with
        openVersion: string -> string -> Async<SessionOpened option>
    }


/// The session cookie of one request, as three functions. Built from the HttpContext in
/// Server.fs; the composition root only reads, writes and deletes through it.
type SessionCookie =
    {
        read: unit -> string option
        write: string -> unit
        delete: unit -> unit
    }


/// The state cookie of one request: written with the redirect to the IdentityProvider, read
/// at the callback.
type LaunchStateCookie =
    {
        // the cookie of one hop, named by its state, so that two tabs can launch at once
        read: string -> string option
        write: string -> unit
    }


/// The enrolment cookie of one request: written at the callback with the code's expiry,
/// read at GetSession and SupplyPin, deleted when the attempt is spent or gone.
type EnrolmentCookie =
    {
        read: unit -> string option
        write: string -> System.DateTime -> unit
        delete: unit -> unit
    }


type AppEnv =
    {
        formulary: FormularyPort
        orderContext: OrderContextPort
        plan: PlanPort
        interaction: InteractionPort
        admin: AdminPort
        requireLoaded: unit -> string[] option
        session: SessionPort
    }
