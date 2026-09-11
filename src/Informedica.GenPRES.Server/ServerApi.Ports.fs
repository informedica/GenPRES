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


/// What the UserRegistry says about a login at this launch (Rules 5, 6, 27): the User with the
/// Role, the Patient active in MainEHR, and the mail address a confirmation code goes to.
/// Whether a PIN is set is the Database's answer (Rule 24), not the registry's.
type UserStanding =
    {
        User: UserContext
        ActivePatientId: string option
        MailAddress: string
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


/// One mail from the Server to a User (Rule 27): a confirmation code, a notice that the PIN
/// was set, a notice at the wrong-PIN limit.
type Mail =
    {
        To: string
        Subject: string
        Body: string
    }


/// Actor M, the MailService, over edge C10. Sending is fire and forget: the Server records
/// what it sent in the audit (Rule 46, later), not the outcome of delivery.
type MailPort = { send: Mail -> unit }


/// The session adapter's answer to a presentation. The session id is the server's to put in
/// the cookie; the composition root maps this to the client's `LaunchOutcome` without it.
/// `RedirectTo` carries the `state` the edge writes to the state cookie (uc-01 step 4.2) next
/// to the url that carries it to the IdentityProvider.
[<RequireQualifiedAccess>]
type LaunchResult =
    | Opened of sessionId: string * SessionOpened
    | RedirectTo of url: string * state: string
    | Refused of LaunchRefusal
    // the launch suspended into enrolment (UC-2); the browser holds the attempt in a cookie
    | Enrolling of attemptId: string


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
    // a reload of a callback whose Session a newer launch has since replaced (Rule 8): the
    // browser goes to the app on whatever cookie it holds, which is the newer Session's
    | Superseded of redirect: string
    // UC-2: the attempt for the enrolment cookie, and how long the code it is bound to lives
    | Enrolling of attemptId: string * redirect: string * until: System.DateTime


/// The answer to a supplied PIN: the Session that opened, or why not.
[<RequireQualifiedAccess>]
type SupplyPinResult =
    | Opened of sessionId: string * SessionOpened
    | Refused of PinRefusal


/// What the store says about a session id from the cookie: the Session, nothing, or that the
/// server ended it (Rule 11), said as long as the browser still sends the cookie; the client
/// acknowledges with CloseSession, which deletes the cookie and drops the ending.
[<RequireQualifiedAccess>]
type SessionLookup =
    | Found of SessionOpened
    | NotFound
    | Ended of SessionEnding


type SessionPort =
    {
        // idempotent per public key within the Launch lifetime (Rule 2, uc-01 Retries)
        present: Launch * PublicKey -> Async<LaunchResult>
        // uc-01 step 4.5: the browser is back from the IdentityProvider
        callback: Callback -> Async<CallbackResult>
        // by session id from the cookie
        // by session id from the cookie; an ending is told once (Rule 11)
        find: string -> Async<SessionLookup>
        // Rule 10: explicit close
        close: string -> Async<unit>
        // UC-2: what a browser holding an attempt is told
        findEnrolment: string -> Async<EnrolmentPending option>
        // UC-2: the code and the chosen PIN, for the attempt in the cookie
        supplyPin: string -> string -> string -> Async<SupplyPinResult>
        // an attempt the browser gave up on (CloseSession while enrolling)
        dropEnrolment: string -> Async<unit>
        // UC-3 step 2: a challenge over the plan as shown, for the Session the cookie names
        challenge: string -> OrderPlan * OpenedToken * string option -> Async<SigningResponse>
        // UC-3 step 3: the signature, for the Session the cookie names
        submit: string -> Submission -> Async<SigningResponse>
        // uc-03 step 1, every computing request: the Session the cookie names is marked seen
        // (Rule 9) and told whether the record moved on (Rule 21) or the Session ended (Rule 11)
        seen: string -> OpenedToken option -> Async<RecordNotice option>
        // UC-4 step 4: the version named becomes what the Session the cookie names opened with
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


/// The state cookie of one request (4.2): written with the redirect, read at the callback.
type LaunchStateCookie =
    {
        // the cookie of one hop, named by its state, so that two tabs can launch at once
        read: string -> string option
        write: string -> unit
    }


/// The enrolment cookie of one request (UC-2): written at the callback with the code's expiry,
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
        orderPlan: OrderPlanPort
        nutritionPlan: NutritionPlanPort
        interaction: InteractionPort
        logAnalyzer: LogAnalyzerPort
        requireLoaded: unit -> string[] option
        session: SessionPort
    }
