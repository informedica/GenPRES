namespace ServerApi

open Shared.Types
open Shared.Api

// The ports on domain values name the domain through these; unqualified, the names below
// are the contract model's, which the other ports still take.
module GenOrder = Informedica.GenOrder.Lib.Types
module GenOrderContext = Informedica.GenOrder.Lib.OrderContext
module GenForm = Informedica.GenForm.Lib.Types


type FormularyPort =
    {
        getFormulary: Formulary -> Async<Result<Formulary, string[]>>
        getParenteralia: Parenteralia -> Async<Result<Parenteralia, string[]>>
    }


/// The prescribing workbench's port: the domain's command verb over a plan context, the
/// answer a plan context with its intake. The verb is the wire's, mapped by the command
/// handler; the context is parsed there too, so the port never sees the contract model.
type OrderContextPort =
    {
        evaluate:
            (GenOrder.OrderContext -> GenOrderContext.Command)
                -> GenOrder.PlanContext
                -> Async<Result<GenOrder.PlanContext, string[]>>
    }


/// The one plan: every member answers the plan with its totals recomputed over its orders.
/// The verb of a navigation is the wire's, mapped by the command handler; the plan and the
/// contexts are parsed there too, so the port never sees the contract model.
type OrderPlanPort =
    {
        recalculate: GenOrder.OrderPlan -> Async<Result<GenOrder.OrderPlan, string[]>>
        // the command into the context named
        navigate:
            GenOrder.OrderPlan
                -> string
                -> (GenOrder.OrderContext -> GenOrderContext.Command)
                -> GenOrder.PlanContext
                -> Async<Result<GenOrder.OrderPlan, string[]>>
        // a workbench evaluated elsewhere into the plan as it is
        addOrderContext: GenOrder.OrderPlan -> GenOrder.PlanContext -> Async<Result<GenOrder.OrderPlan, string[]>>
        // a fresh workbench for a nutrition category, its filter discovered
        newOrderContext: GenOrder.OrderPlan -> GenOrder.NutritionCategory -> Async<Result<GenOrder.OrderPlan, string[]>>
        // the contexts named, every kind; a feeding takes its supplements with it
        removeOrderContexts: GenOrder.OrderPlan -> string[] -> Async<Result<GenOrder.OrderPlan, string[]>>
        // a signed version's patient and contexts as they were, nothing evaluated
        openWith: GenForm.Patient -> GenOrder.PlanContext[] -> Async<Result<GenOrder.OrderPlan, string[]>>
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


/// The IdentityProvider. authorizeUrl is where the browser is sent with the state;
/// redeem exchanges the callback's code for the identity over the server's own connection.
type IdentityProviderPort =
    {
        authorizeUrl: string -> string
        redeem: string -> BrowserIdentity option
    }


type UserRegistryPort = { standing: BrowserIdentity -> UserStanding option }


/// The EHR, read once at the launch and again at a challenge: what it returns for a patient
/// id, as the domain's patient with the identity on it, or none, and the patient the rules see
/// from it at a date. The adapter parses what the EHR gives, and a reading that is no patient
/// is no reading; none is not a refusal: the Session opens without imported data.
type PatientDataPort =
    {
        read: string -> GenForm.EhrPatientData option
        /// the GenFORM patient the EHR data is at a date; the composition root adds the estimate
        patient: System.DateTime -> GenForm.EhrPatientData -> GenForm.Patient
    }


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


/// A row the release cannot read: its identity from the plain columns beside the JSON,
/// which are authoritative, and why.
type UnreadableVersion =
    {
        Id: string
        No: int
        PatientId: string
        Base: string option
        SignedBy: GenOrder.Signer
        SignedAt: System.DateTime
        Reason: string
    }


/// A version as the record holds it once loaded: parsed, or kept by its identity when the
/// row cannot be read (a structure version newer than the release knows, an upgrade that
/// fails, a Dto the domain refuses), so that nothing vanishes and nothing is overtaken.
[<RequireQualifiedAccess>]
type StoredVersion =
    | Readable of GenOrder.OrderPlanVersion
    | Unreadable of UnreadableVersion


module StoredVersion =

    /// Who signed, as the client knows a user: only a Prescriber signs.
    let private signer (s: GenOrder.Signer) : UserContext =
        {
            UserId = s.UserId
            DisplayName = s.DisplayName
            Role = UserRole.Prescriber
        }


    let id =
        function
        | StoredVersion.Readable v -> v.Id
        | StoredVersion.Unreadable u -> u.Id


    let no =
        function
        | StoredVersion.Readable v -> v.No
        | StoredVersion.Unreadable u -> u.No


    /// The id of a version that can be read; none for one that cannot.
    let readableId =
        function
        | StoredVersion.Readable v -> Some v.Id
        | StoredVersion.Unreadable _ -> None


    /// What identifies the version to the client: whose, and when.
    let head (version: StoredVersion) : OrderPlanHead =
        match version with
        | StoredVersion.Readable v ->
            {
                Id = v.Id
                No = v.No
                By = signer v.SignedBy
                SignedAt = v.SignedAt
            }
        | StoredVersion.Unreadable u ->
            {
                Id = u.Id
                No = u.No
                By = signer u.SignedBy
                SignedAt = u.SignedAt
            }


/// A value the user measured in the Session and when; none says the user cleared it.
type Measured<'v> =
    {
        Value: 'v option
        At: System.DateTime
    }


/// The user's measurements a Session holds, each the latest row: nothing for one the user
/// never touched, so that the EHR's value stands; a value; or cleared.
type Measurements =
    {
        Weight: Measured<int<gram>> option
        Height: Measured<int<cm>> option
        GestAge: Measured<GestAge> option
    }


/// One measurement as the store writes it, in its own row: the kind and the value, none
/// clearing it.
[<RequireQualifiedAccess>]
type Measurement =
    | Weight of int<gram> option
    | Height of int<cm> option
    | GestAge of GestAge option


/// What the store holds of an open Session: who, for which patient and on what data, the
/// token, the key thumbprint, and the head of the record it opened with, readable or not.
/// The command handlers map it to what the client keeps.
type OpenedSession =
    {
        /// None = anonymous session: opened without a launch, no User, no Role
        User: UserContext option
        /// None = launch without an active patient
        PatientId: string option
        /// the EHR data as read at the launch; none for no-data and for a launch without EHR
        EhrData: GenForm.EhrPatientData option
        /// the data shown for the patient: the EHR data projected at the open, else the head's,
        /// else none
        Patient: GenForm.Patient option
        /// what the user measured in the Session, held beside the EHR data as read
        Measured: Measurements
        OpenedToken: OpenedToken option
        /// RFC 7638 thumbprint of the public key this Session will sign requests with
        KeyThumbprint: string option
        /// the head of the record it opened with; None from nothing
        Head: StoredVersion option
    }


/// The signature as the session service takes it: the plan parsed at the boundary, the
/// OpenedToken the Session holds, the challenge it was issued, the PIN, and the client's own
/// key so that the commit takes effect once. Never logged.
type Signature =
    {
        Plan: GenOrder.OrderPlan
        Opened: OpenedToken
        Challenge: string
        Pin: string
        IdemKey: string
    }


/// The answer of the session service to a signing command, on domain values; the command
/// handler maps it to the wire's SigningResponse.
[<RequireQualifiedAccess>]
type SigningOutcome =
    /// the challenge over exactly this plan; comes back with the PIN
    | ChallengeIssued of challenge: string
    /// no challenge yet: the token, and the data as it stands, none when it could not be read
    | DataNotice of token: string * data: GenForm.Patient option
    /// the version committed, and a fresh OpenedToken over it
    | Submitted of GenOrder.OrderPlanVersion * OpenedToken
    | Refused of SigningRefusal


/// The session adapter's answer to a presentation. The session id is the server's to put in
/// the cookie; the composition root maps this to the client's LaunchOutcome without it.
/// RedirectTo carries the state the edge writes to the state cookie next to the url
/// that carries it to the IdentityProvider.
[<RequireQualifiedAccess>]
type LaunchResult =
    | Opened of sessionId: string * OpenedSession
    | RedirectTo of url: string * state: string
    | Refused of LaunchRefusal
    /// the launch suspended at the PIN question; the browser holds the attempt in a cookie
    | Enrolling of attemptId: string


/// What the callback from the IdentityProvider brings: the state from the url and from the
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
    /// a reload of a callback whose Session a newer launch has since replaced: the browser
    /// goes to the app on whatever cookie it holds, which is the newer Session's
    | Superseded of redirect: string
    /// the launch suspended at the PIN question: the attempt for the enrolment cookie, and
    /// how long the code it is bound to lives
    | Enrolling of attemptId: string * redirect: string * until: System.DateTime


/// The answer to a supplied PIN: the Session that opened, or why not.
[<RequireQualifiedAccess>]
type SupplyPinResult =
    | Opened of sessionId: string * OpenedSession
    | Refused of PinRefusal


/// What the store says about a session id from the cookie: the Session, nothing, or that the
/// server ended it, said as long as the browser still sends the cookie; the client
/// acknowledges with CloseSession, which deletes the cookie and drops the ending.
[<RequireQualifiedAccess>]
type SessionLookup =
    | Found of OpenedSession
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
        // a signing challenge over the plan as shown, parsed, for the Session the cookie names
        challenge: string -> GenOrder.OrderPlan * OpenedToken * string option -> Async<SigningOutcome>
        // the signature, its plan parsed, for the Session the cookie names
        submit: string -> Signature -> Async<SigningOutcome>
        // every computing request: the Session the cookie names is marked seen and told
        // whether the record moved on or the Session ended, and the age it holds for an
        // identified patient; what the patient the request edits measures is recorded
        seen: string -> OpenedToken option -> Patient option -> Async<RecordNotice option * Age option>
        // the age the Session the cookie names holds for an identified patient; the Session is
        // not touched, a signing request does that itself
        age: string -> Async<Age option>
        // the version named becomes what the Session the cookie names opened with
        openVersion: string -> string -> Async<OpenedSession option>
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
        orderPlan: OrderPlanPort
        interaction: InteractionPort
        admin: AdminPort
        requireLoaded: unit -> string[] option
        session: SessionPort
        // whether the server runs on the demo data; read once at start-up, told on every
        // context the client gets
        demo: bool
        // the departments the loaded rules name and the default; none until the resources
        // are loaded, since the settings are asked for before that
        departments: unit -> Informedica.GenForm.Lib.Types.Departments option
        logger: Informedica.Logging.Lib.Logger
    }
