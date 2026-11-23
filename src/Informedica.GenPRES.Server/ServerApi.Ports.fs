namespace ServerApi

open System
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Api

// Aliases for the domain modules that some ports take; the unqualified names below come from the contract model.
module GenOrder = Informedica.GenOrder.Lib.Types
module GenOrderContext = Informedica.GenOrder.Lib.OrderContext


/// The GenCORE patient: an optional identity, absent for an anonymous patient, and the patient data.
type CorePatient = Informedica.GenCore.Lib.Patients.Patient


/// The GenCORE patient data: what is known about a patient apart from the identity.
type CorePatientData = Informedica.GenCore.Lib.Patients.PatientData


/// Who signed an order plan version.
type Signer =
    {
        /// The user id at the identity provider.
        UserId: string
        /// The name shown for the signer.
        DisplayName: string
    }


/// An order plan version: the order plan as the prescriber signed it. It stores the whole plan, with the
/// filter, the totals and the totals of each order context, so opening the version again shows what the
/// signer saw without calculating it again.
type OrderPlanVersion =
    {
        /// The id of the version.
        Id: string
        /// The version number, one above the previous version of the patient.
        No: int
        /// The patient the plan belongs to.
        PatientId: string
        /// The id of the version this one was built on; None for the first.
        Base: string option
        /// The prescriber who signed the version.
        SignedBy: Signer
        /// The time the prescriber signed the version.
        SignedAt: DateTime
        /// The order plan as signed.
        Plan: GenOrder.OrderPlan
        /// Whether the EHR could be read at the signing challenge. False when the version was signed
        /// without an EHR reading.
        Verified: bool
        /// The patient read from the EHR at the signing challenge. None when the EHR could not be read.
        /// A value the prescriber typed shows as a difference from the patient of the plan.
        EhrPatient: CorePatient option
    }

/// The logger of the logging library.
type Logger = Informedica.Logging.Lib.Logger

/// The identity of a patient, of GenCORE.
type CorePatientIdentity = Informedica.GenCore.Lib.Patients.PatientIdentity

/// The resources GenCORE completes the patient data with.
type CorePatientDataResources = Informedica.GenCore.Lib.Patients.PatientDataResources


type FormularyPort =
    {
        getFormulary: Formulary -> Async<Result<Formulary, string[]>>
        getParenteralia: Parenteralia -> Async<Result<Parenteralia, string[]>>
    }


/// Evaluates order contexts on domain values. The command handler parses the context and maps the command from the
/// contract, so the port never sees the contract model.
type OrderContextPort =
    {
        /// Evaluates a plan context with a command and returns the outcome: evaluated, or refused with the reason.
        evaluate:
            (GenOrder.OrderContext -> GenOrderContext.Command)
                -> GenOrder.PlanContext
                -> Async<Result<GenOrder.Outcome<GenOrder.PlanContext>, string[]>>
        /// Returns the order contexts the link rules offer to link to the context when it is narrowed to one order;
        /// an empty array otherwise.
        linkedOrders: GenOrder.PlanContext -> Async<Result<GenOrder.PlanContext[], string[]>>
    }


/// Changes an order plan. Every function except totals returns the plan with its totals recalculated over its
/// orders; totals returns only the totals. The command handler parses the plan and the contexts and maps the
/// command from the contract, so the port never sees the contract model.
type OrderPlanPort =
    {
        recalculate: GenOrder.OrderPlan -> Async<Result<GenOrder.OrderPlan, string[]>>
        // Recalculates every order for the patient data of the plan.
        followPatientData: GenOrder.OrderPlan -> Async<Result<GenOrder.OrderPlan, string[]>>
        // Evaluates the command on the context with the given id and puts the result in the plan.
        evaluateContext:
            GenOrder.OrderPlan
                -> string
                -> (GenOrder.OrderContext -> GenOrderContext.Command)
                -> GenOrder.PlanContext
                -> Async<Result<GenOrder.OrderPlan, string[]>>
        // Adds an evaluated order context with its linked orders to the plan; when one of them is refused, none of
        // them is added.
        addOrderContexts:
            GenOrder.OrderPlan
                -> GenOrder.PlanContext
                -> GenOrder.PlanContext[]
                -> Async<Result<GenOrder.OrderPlan, string[]>>
        // Removes the contexts with the given ids, of any category, each with the contexts linked to it.
        removeOrderContexts: GenOrder.OrderPlan -> string[] -> Async<Result<GenOrder.OrderPlan, string[]>>
        // Creates a plan from the patient data and contexts of a signed version without evaluating them.
        openWith: CorePatientData -> GenOrder.PlanContext[] -> Async<Result<GenOrder.OrderPlan, string[]>>
        // Calculates the totals over the orders of the contexts for the patient data, without changing a plan.
        totals: CorePatientData -> GenOrder.PlanContext[] -> Async<Result<GenOrder.Totals, string[]>>
    }


type InteractionPort =
    {
        checkInteractions: string list -> Async<Result<DrugInteraction list, string[]>>
        getDrugNames: unit -> Async<Result<string list, string[]>>
    }


/// The settings and functions the admin commands need. The server reads the secret and the clock and passes them
/// in, so the command module never reads the environment.
type AdminPort =
    {
        // The value of GENPRES_PASSWORD; None when unset, empty or whitespace, so every password check fails.
        secret: unit -> string option
        // True on a demo or development server. The admin tokens are signed under this mode, so a token of a demo
        // or development server never opens a production server with the same password.
        demo: bool
        now: unit -> DateTimeOffset
        listLogFiles: unit -> Async<Result<LogFileInfo[], string[]>>
        analyzeLogFile: string -> Async<Result<string, string[]>>
        // Reloads the resources of the provider.
        reloadResources: unit -> Async<Result<unit, string[]>>
    }


/// The user at the browser as the identity provider reports it. The session user comes from this identity, never
/// from the launch.
type BrowserIdentity =
    {
        Login: string
        DisplayName: string
    }


/// What the user registry returns for a login at a launch or a challenge: the user with the role, the patient
/// active in the EHR and the mail address for confirmation codes. Whether a PIN is set comes from the credential
/// store.
type UserStanding =
    {
        User: UserContext
        ActivePatientId: string option
        MailAddress: string
    }


/// The identity provider. authorizeUrl returns the url the browser is sent to with the state; redeem exchanges the
/// callback code for the identity over the server's own connection.
type IdentityProviderPort =
    {
        authorizeUrl: string -> string
        redeem: string -> BrowserIdentity option
    }


type UserRegistryPort = { standing: BrowserIdentity -> UserStanding option }


/// Reads a patient from the EHR. The EHR is read at the launch and again at a challenge.
type EhrPatientPort =
    {
        /// The patient with a patient id, with its ages calculated at the read. None when the EHR
        /// has no patient for the id. The Session then opens without EHR data.
        read: string -> CorePatient option
        /// The patient data of an EHR patient. The composition root adds the estimates.
        patientData: CorePatient -> CorePatientData
    }


/// A mail from the server to a user: a confirmation code, a notice that the PIN was set, or a notice that the
/// wrong-PIN limit was reached.
type Mail =
    {
        To: string
        Subject: string
        Body: string
    }


/// The mail service. Sending is fire and forget; delivery is not checked.
type MailPort = { send: Mail -> unit }


/// A stored version this release cannot read: its identity from the plain columns beside the JSON, which are
/// authoritative, and the reason it cannot be read.
type UnreadableVersion =
    {
        Id: string
        No: int
        PatientId: string
        Base: string option
        SignedBy: Signer
        SignedAt: DateTime
        /// The patient name and birth date from the columns; None for a version signed in a session without an
        /// identity, and for a row written before these columns existed.
        Identity: CorePatientIdentity option
        Reason: string
    }


/// A stored version after loading: readable, or unreadable with its identity from the columns (a structure version
/// newer than this release, a failed upgrade, a Dto the domain refuses), so no version disappears or is silently
/// superseded. Both carry the patient identity from the columns, never from the plan JSON; None for a version
/// signed in a session without an identity.
[<RequireQualifiedAccess>]
type StoredVersion =
    | Readable of OrderPlanVersion * identity: CorePatientIdentity option
    | Unreadable of UnreadableVersion


module StoredVersion =

    /// Maps a signer to a user with the prescriber role, since only a prescriber signs.
    let private signer (s: Signer) : UserContext =
        {
            UserId = s.UserId
            DisplayName = s.DisplayName
            Role = UserRole.Prescriber
        }


    let id =
        function
        | StoredVersion.Readable(v, _) -> v.Id
        | StoredVersion.Unreadable u -> u.Id


    let no =
        function
        | StoredVersion.Readable(v, _) -> v.No
        | StoredVersion.Unreadable u -> u.No


    /// The id of a version that can be read; none for one that cannot.
    let readableId =
        function
        | StoredVersion.Readable(v, _) -> Some v.Id
        | StoredVersion.Unreadable _ -> None


    /// Returns the patient identity of a version, readable or not.
    let identity =
        function
        | StoredVersion.Readable(_, identity) -> identity
        | StoredVersion.Unreadable u -> u.Identity


    /// Returns the head of a version for the client: its id, number, signer and signing time.
    let head (version: StoredVersion) : OrderPlanHead =
        match version with
        | StoredVersion.Readable(v, _) ->
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


/// The act that wrote the data a session opened with. The newest row counts, so a resumed session knows its data
/// without comparing times.
[<RequireQualifiedAccess>]
type SessionAct =
    /// The session opened from a launch.
    | Opened
    /// A version of the record opened in the session.
    | VersionOpened
    /// The EHR data read again.
    | Refreshed
    /// A plan signed.
    | Signed


/// What the store holds of an open session: the user, the patient id, the EHR data, the token, the key thumbprint,
/// the record head it opened with and the act that wrote it. The patient data shown is computed from these. The
/// command handlers map it to the session the client keeps.
type OpenedSession =
    {
        /// None for an anonymous session: opened without a launch, with no user and no role.
        User: UserContext option
        /// None for a launch without an active patient.
        PatientId: string option
        /// The patient as the EHR gave it at the launch; None for no-data and for a launch without EHR data.
        EhrPatient: CorePatient option
        OpenedToken: OpenedToken option
        /// The RFC 7638 thumbprint of the public key the session signs requests with.
        KeyThumbprint: string option
        /// The head of the record the session opened with; None when there was no signed version.
        Head: StoredVersion option
        /// The latest act, which says whether the patient data shown is the signed data.
        Act: SessionAct
    }


module OpenedSession =

    /// Returns the patient identity of the session: the identity in its EHR data, or the identity of the head when
    /// there is no EHR data. EHR data without an identity gives no identity, whatever the head says.
    let identity (opened: OpenedSession) =
        match opened.EhrPatient with
        | Some ehr -> ehr.Identity
        | None -> opened.Head |> Option.bind StoredVersion.identity


/// A signature for the session service: the plan parsed at the boundary, the opened token of the session, the
/// issued challenge, the PIN, and the idempotency key of the client so the commit takes effect once. Never logged.
type Signature =
    {
        Plan: GenOrder.OrderPlan
        Opened: OpenedToken
        Challenge: string
        Pin: string
        IdemKey: string
    }


/// The answer of the session service to a signing command, on domain values. The command handler maps it to the
/// contract SigningResponse.
[<RequireQualifiedAccess>]
type SigningOutcome =
    /// A challenge for exactly this plan, to be sent back with the PIN.
    | ChallengeIssued of challenge: string * ehrMovedOn: bool
    /// No challenge yet: the token and the current patient data. None when the EHR could not be
    /// read.
    | DataNotice of token: string * patientData: CorePatientData option
    /// The version is stored. It has the identity of the patient, a new OpenedToken, and the
    /// patient data the Session shows after the signing.
    | Submitted of
        version: OrderPlanVersion *
        identity: CorePatientIdentity option *
        OpenedToken *
        patientData: CorePatientData
    | Refused of SigningRefusal


/// The answer of the session port to a presented launch. The server puts the session id in the cookie and maps the
/// rest to the client LaunchOutcome. RedirectTo carries the identity provider url and the state the server writes
/// to the state cookie.
[<RequireQualifiedAccess>]
type LaunchResult =
    | Opened of sessionId: string * OpenedSession
    | RedirectTo of url: string * state: string
    | Refused of LaunchRefusal
    /// The launch waits for PIN enrolment; the browser keeps the attempt id in a cookie.
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
    /// A reload of a callback whose session a newer launch replaced: the browser goes to the app with the cookie it
    /// has, which names the newer session.
    | Superseded of redirect: string
    /// The launch waits for PIN enrolment: the attempt id for the enrolment cookie, the redirect, and the expiry of
    /// the confirmation code.
    | Enrolling of attemptId: string * redirect: string * until: DateTime


/// The answer to a supplied PIN: the Session that opened, or why not.
[<RequireQualifiedAccess>]
type SupplyPinResult =
    | Opened of sessionId: string * OpenedSession
    | Refused of PinRefusal


/// The lookup of a session id from the cookie: the session, not found, or ended by the server. Ended is returned
/// as long as the browser sends the cookie; the client acknowledges it with CloseSession, which deletes the cookie
/// and the ending.
[<RequireQualifiedAccess>]
type SessionLookup =
    | Found of OpenedSession
    | NotFound
    | Ended of SessionEnding


/// The result of a command that changes a session: done, or InactiveTab when the token is not the token of the
/// session, with nothing changed.
[<RequireQualifiedAccess>]
type OnOwnToken<'a> =
    | Done of 'a
    | InactiveTab


type SessionPort =
    {
        // Idempotent per public key within the launch lifetime: a repeat from the same browser gets the first answer.
        present: Launch * PublicKey -> Async<LaunchResult>
        // Handles the browser coming back from the identity provider.
        callback: Callback -> Async<CallbackResult>
        // Finds a session by the id from the cookie; an ending is returned until the client acknowledges it.
        find: string -> Async<SessionLookup>
        // Returns the patient data an open session shows on the current date; reads no store.
        patientData: OpenedSession -> CorePatientData option
        // Ends the session on a new launch from this browser, whatever its token.
        close: string -> Async<unit>
        // Closes the session at the user's request when the tab holds the session token.
        closeTab: string -> OpenedToken option -> Async<OnOwnToken<unit>>
        // Returns the pending enrolment for the attempt id from the cookie.
        findEnrolment: string -> Async<EnrolmentPending option>
        // Takes the attempt id from the cookie, the confirmation code and the chosen PIN.
        supplyPin: string -> string -> string -> Async<SupplyPinResult>
        // an attempt the browser gave up on (CloseSession while enrolling)
        dropEnrolment: string -> Async<unit>
        // Issues a signing challenge for the plan as shown, for the session the cookie names.
        challenge: string -> GenOrder.OrderPlan * OpenedToken * string option -> Async<SigningOutcome>
        // Submits the signature for the session the cookie names.
        submit: string -> Signature -> Async<SigningOutcome>
        // Called on every calculation request: marks the session the cookie names as seen and tells whether the
        // record changed or the session ended.
        seen: string -> OpenedToken option -> Async<RecordNotice option>
        // Opens the version with the given id in the session the cookie names.
        openVersion: string -> string -> OpenedToken option -> Async<OnOwnToken<OpenedSession option>>
        // Reads the EHR again for the session the cookie names and opens the head on the new data.
        refresh: string -> OpenedToken option -> Async<OnOwnToken<OpenedSession option>>
    }


/// The session cookie of one request as three functions. Server.fs builds it from the HttpContext.
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
        // One cookie per launch, named by its state, so two tabs can launch at the same time.
        read: string -> string option
        write: string -> unit
    }


/// The enrolment cookie of one request: written at the callback with the code expiry, read at GetSession and
/// SupplyPin, and deleted when the attempt is used or expired.
type EnrolmentCookie =
    {
        read: unit -> string option
        write: string -> DateTime -> unit
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
        // Whether the server runs on the demo data; read once at startup and sent with every context to the client.
        demo: bool
        // The departments the loaded rules name; none while the rules are not loaded.
        departments: unit -> string[]
        // The locations the loaded rules name; none while the rules are not loaded.
        locations: unit -> string[]
        // The location a patient without a Location gets; None for no default.
        defaultLocation: string option
        // The resources GenCORE completes the patient data with; None until the resources are loaded. Calling it may
        // load them.
        patientDataResources: unit -> CorePatientDataResources option
        // The current date in the hospital time zone, used as the measurement date of a weight or height from the
        // client.
        today: unit -> DateTime
        logger: Logger
    }
