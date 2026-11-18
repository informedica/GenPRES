namespace ServerApi

open System
open Informedica.GenPRES.Shared.Types

// GenOrder and GenForm name the domain types of the stored clinical records. Unqualified type names
// are the contract types.
module GenOrder = Informedica.GenOrder.Lib.Types
module GenForm = Informedica.GenForm.Lib.Types


module PublicKey =

    open System.Text
    open System.Text.Json
    open System.Security.Cryptography


    let private base64Url (bytes: byte[]) =
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')


    let private sha256 (s: string) = s |> Encoding.UTF8.GetBytes |> SHA256.HashData


    /// The required members of a JWK per key type, in the lexicographic order RFC 7638
    /// section 3 prescribes. Empty for a key type the thumbprint does not cover.
    let private requiredMembers kty =
        match kty with
        | "EC" -> [ "crv"; "kty"; "x"; "y" ]
        | "RSA" -> [ "e"; "kty"; "n" ]
        | "OKP" -> [ "crv"; "kty"; "x" ]
        | _ -> []


    /// The string members RFC 7638 hashes, or None when the text is not such a JWK.
    let private jwkMembers (text: string) =
        try
            use doc = JsonDocument.Parse text
            let root = doc.RootElement

            if root.ValueKind <> JsonValueKind.Object then
                None
            else
                let get (name: string) =
                    match root.TryGetProperty name with
                    | true, v when v.ValueKind = JsonValueKind.String -> Some(name, v.GetString())
                    | _ -> None

                match get "kty" with
                | None -> None
                | Some(_, kty) ->
                    let members = requiredMembers kty |> List.map get

                    if members.IsEmpty || members |> List.exists Option.isNone then
                        None
                    else
                        members |> List.choose id |> Some
        with :? JsonException ->
            None


    /// RFC 7638 thumbprint of a public JWK: SHA-256 over the required members serialised
    /// without whitespace in lexicographic order, base64url without padding. Text that is
    /// not a JWK of a covered key type gets the hash of the text itself, so the stub can
    /// correlate any key the client sends; the client computes the same value only for a
    /// real JWK.
    let thumbprint (PublicKey text) =
        match jwkMembers text with
        | Some members ->
            members
            |> List.map (fun (name, value) -> $"\"{name}\":{JsonSerializer.Serialize value}")
            |> String.concat ","
            |> fun body -> "{" + body + "}"
            |> sha256
            |> base64Url
        | None -> text |> sha256 |> base64Url


    /// A random, unguessable id for a session cookie: 256 bits from the CSPRNG, base64url.
    let randomId () = RandomNumberGenerator.GetBytes 32 |> base64Url


module LaunchSeal =

    /// The key the Launch is sealed under. 32 bytes from a CSPRNG (newKey); shared with the
    /// LaunchScript in the real integration, made per host start for the stub.
    type Key = | Key of byte[]


    /// The claims of a Launch whose seal is verified.
    type Claims =
        {
            PatientId: string
            Nonce: string
            Expiry: DateTime
        }


    /// The sealed payload on the wire. Field names are the contract; keep them short.
    type Payload =
        {
            pid: string
            nonce: string
            // unix seconds, UTC
            exp: int64
        }


    let keyLength = 32


    let newKey (random: int -> byte[]) = Key(random keyLength)


    let toBase64Url (bytes: byte[]) =
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')


    let fromBase64Url (s: string) =
        try
            let padded = s.Replace('-', '+').Replace('_', '/')

            let padded =
                match padded.Length % 4 with
                | 2 -> padded + "=="
                | 3 -> padded + "="
                | 0 -> padded
                | _ -> raise (FormatException "bad length")

            Some(Convert.FromBase64String padded)
        with _ ->
            None


    let mac (Key key) (data: byte[]) = System.Security.Cryptography.HMACSHA256.HashData(key, data)


    let private json = System.Text.Json.JsonSerializerOptions()


    /// Seals the claims: base64url(json) + "." + base64url(HMAC-SHA256(key, json)).
    let mint (key: Key) (claims: Claims) : Launch =
        let payload =
            {
                pid = claims.PatientId
                nonce = claims.Nonce
                exp = DateTimeOffset(claims.Expiry, TimeSpan.Zero).ToUnixTimeSeconds()
            }

        let bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload, json)
        Launch $"{toBase64Url bytes}.{toBase64Url (mac key bytes)}"


    /// Verifies the seal (constant-time), then the lifetime. Anything that is not a
    /// Launch sealed under the key is LaunchInvalid; a Launch past its expiry is
    /// LaunchExpired.
    let verify (now: DateTime) (key: Key) (Launch text) : Result<Claims, LaunchRefusal> =
        let parts = if isNull text then [||] else text.Split('.')

        match parts with
        | [| payload; signature |] ->
            match fromBase64Url payload, fromBase64Url signature with
            | Some bytes, Some given when
                System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(mac key bytes, given)
                ->
                try
                    let p = System.Text.Json.JsonSerializer.Deserialize<Payload>(bytes, json)

                    if isNull p.pid || isNull p.nonce || p.pid = "" || p.nonce = "" then
                        Error LaunchRefusal.LaunchInvalid
                    else
                        let expiry = DateTimeOffset.FromUnixTimeSeconds(p.exp).UtcDateTime

                        if now > expiry then
                            Error LaunchRefusal.LaunchExpired
                        else
                            Ok
                                {
                                    PatientId = p.pid
                                    Nonce = p.nonce
                                    Expiry = expiry
                                }
                with _ ->
                    Error LaunchRefusal.LaunchInvalid
            | _ -> Error LaunchRefusal.LaunchInvalid
        | _ -> Error LaunchRefusal.LaunchInvalid


/// A stored PIN: a PBKDF2-SHA256 hash with its own salt, never the PIN itself, so two users with the
/// same PIN store different hashes.
type PinHash =
    {
        Salt: byte[]
        Hash: byte[]
    }


module PinHash =

    /// Enough rounds to make guessing a four-to-six-digit PIN offline slow, few enough for a
    /// signing check to feel immediate. One place to tune.
    let iterations = 100_000

    let saltLength = 16

    let hashLength = 32


    let private derive (salt: byte[]) (pin: string) =
        System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            pin,
            salt,
            iterations,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            hashLength
        )


    /// Derives the hash of a PIN under a fresh salt from newSalt (the CSPRNG in the host).
    let make (newSalt: int -> byte[]) (pin: string) : PinHash =
        let salt = newSalt saltLength

        {
            Salt = salt
            Hash = derive salt pin
        }


    /// Whether the PIN derives to the stored hash, compared in constant time.
    let verify (pin: string) (hash: PinHash) =
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(derive hash.Salt pin, hash.Hash)


/// The signing credential stored per user id, not per login. The wrong-PIN count spans sessions and
/// is reset to zero when the PIN is set.
type Credential =
    {
        PinHash: PinHash option
        WrongCount: int
        /// The time until which signing is locked. None when no lock is set.
        LockedUntil: DateTime option
    }


module Credential =

    let empty =
        {
            PinHash = None
            WrongCount = 0
            LockedUntil = None
        }


    /// Whether a PIN is set.
    let pinSet (credential: Credential) = credential.PinHash.IsSome


    /// Creates a credential with the PIN, a wrong-PIN count of zero and no lock.
    let withPin (newSalt: int -> byte[]) (pin: string) : Credential =
        {
            PinHash = Some(PinHash.make newSalt pin)
            WrongCount = 0
            LockedUntil = None
        }


    /// The number of wrong PINs that ends the session and locks signing.
    let wrongPinLimit = 3

    /// The first lock: one minute.
    let lockBase = TimeSpan.FromMinutes 1.0


    /// The longest lock. The delay does not decay with time, so it is capped instead; an unbounded
    /// doubling would overflow.
    let lockMax = TimeSpan.FromHours 24.0


    /// The delay after count wrong entries. The entry that reaches the limit locks for
    /// lockBase; each one after it doubles that, up to lockMax.
    let lockFor (count: int) =
        // 2^11 minutes is already more than a day; the bound keeps pown in range.
        let doublings = min 11 (max 0 (count - wrongPinLimit))
        min lockMax (lockBase * float (pown 2 doublings))


    /// Whether signing is locked at this moment.
    let isLocked (now: DateTime) (credential: Credential) =
        match credential.LockedUntil with
        | Some until -> now < until
        | None -> false


    /// Verifies the PIN and returns whether it is accepted, with the updated credential. The PIN is
    /// verified here and nowhere else. A right PIN while unlocked resets the count and the lock. A right
    /// PIN while locked is refused and changes nothing. A wrong PIN adds one to the count and, from the
    /// limit on, locks for lockFor from now, so a wrong PIN while locked extends and doubles the delay.
    let verify (now: DateTime) (pin: string) (credential: Credential) : bool * Credential =
        let locked = isLocked now credential

        let right =
            match credential.PinHash with
            | Some hash -> PinHash.verify pin hash
            | None -> false

        if right && not locked then
            true,
            { credential with
                WrongCount = 0
                LockedUntil = None
            }
        elif right then
            false, credential
        else
            let count = credential.WrongCount + 1

            let until =
                if count >= wrongPinLimit then
                    Some(now + lockFor count)
                else
                    None

            false,
            { credential with
                WrongCount = count
                LockedUntil = until
            }


    /// The wrong entries left before the limit.
    let attemptsLeft (credential: Credential) = max 0 (wrongPinLimit - credential.WrongCount)


module Pin =

    /// Four to six digits: an assumption; no format has been specified for the PIN.
    let isValid (pin: string) =
        not (isNull pin)
        && pin.Length >= 4
        && pin.Length <= 6
        && pin |> Seq.forall Char.IsAsciiDigit


module MailHint =

    /// Masks a mail address as n***@domain, so the user recognises the mailbox without showing the
    /// address. Returns *** for an address without a name before the @.
    let ofAddress (address: string) =
        match address.IndexOf '@' with
        | i when i > 0 -> $"{address[0]}***{address.Substring i}"
        | _ -> "***"


/// The mails the server sends a user: the confirmation code, the notice that a PIN was set and the
/// notice at the wrong-PIN limit. English only.
module Mails =

    let confirmationCode (displayName: string) (code: string) (minutes: int) : string * string =
        "GenPRES: your confirmation code",
        $"Hello {displayName},\n\nYour confirmation code is {code}. It is valid for {minutes} minutes. Enter it in GenPRES together with the PIN of your choice.\n\nIf you did not open GenPRES just now, somebody tried to enrol in your name; nothing was set."


    let pinSet (displayName: string) : string * string =
        "GenPRES: your PIN was set",
        $"Hello {displayName},\n\nA PIN was set for your GenPRES account just now. If that was not you, tell your administrator."


    /// The mail sent when wrong PINs ended the session and locked signing.
    let pinLimit (displayName: string) : string * string =
        "GenPRES: signing is locked",
        $"Hello {displayName},\n\nThe PIN was entered wrong three times at a signature just now. Your session was ended and signing is locked for a while. If that was not you, tell your administrator."


/// The calendar date a patient's age is taken on: the date in the hospital's time zone, so that a
/// patient is a day older at local midnight and not an hour or two later, when the UTC date turns.
module LocalDate =

    /// The id of the time zone an age is taken in when the site sets none: the Netherlands.
    let defaultZoneId = "Europe/Amsterdam"


    /// Finds the time zone of an id. Returns an error that names the id when the system does not know it.
    let zoneOf (id: string) : Result<TimeZoneInfo, string> =
        try
            Ok(TimeZoneInfo.FindSystemTimeZoneById id)
        with
        | :? TimeZoneNotFoundException
        | :? InvalidTimeZoneException -> Error $"'%s{id}' is not a time zone this system knows"


    /// The time zone of the default id. Raises when the system does not know it, which a server
    /// start refuses before this is reached.
    let defaultZone () = zoneOf defaultZoneId |> Result.defaultWith invalidOp


    /// The calendar date in a time zone of a moment in UTC. The moment is taken as UTC whatever
    /// its Kind, since a value read back from the session store has no Kind.
    let ofUtc (zone: TimeZoneInfo) (utc: DateTime) =
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone).Date


/// The session lifecycle, from a presented Launch to a signed order plan version: the launch record
/// keyed by its nonce, the redirect to the identity provider, the callback with its checks (identity,
/// role, active patient, PIN), the opening that closes the other sessions of the user, the enrolment of
/// a PIN, the wrong-PIN lock, the signing challenge, the commit of a version and the notice of a newer
/// version. The functions are pure over a State record; the clock, the ids, the codes and the ports are
/// parameters. StubDatabase.makeSessionPort runs them over an in-memory store.
module Session =

    module CorePatient = Informedica.GenCore.Lib.Patients.Patient
    module CorePatientData = Informedica.GenCore.Lib.Patients.PatientData
    module CoreSource = Informedica.GenCore.Lib.Patients.Source


    /// Returns the word for a refusal in the URL #/session?refused=word, which the client reads with
    /// parseRefusal.
    let refusalWord refusal =
        match refusal with
        | LaunchRefusal.LaunchExpired -> "expired"
        | LaunchRefusal.LaunchSpent -> "spent"
        | LaunchRefusal.LaunchInvalid -> "invalid"
        | LaunchRefusal.NoBrowserIdentity -> "no-identity"
        | LaunchRefusal.NoRole -> "no-role"
        | LaunchRefusal.WrongActivePatient -> "wrong-patient"
        | LaunchRefusal.EnrolmentRequired -> "enrolment"


    let openedUrl = "/#/session"

    let refusedUrl refusal = $"/#/session?refused={refusalWord refusal}"


    /// A stored session: the opened session, the login it belongs to (a user has at most one open
    /// session), the id of the version it opened with, the opening time and the time of the last
    /// request. A submission is checked against the version id, which is None when there was no
    /// version or the newest version cannot be read. The idle lifetime counts from the last request.
    type SessionRecord =
        {
            Opened: OpenedSession
            Login: string option
            OpenedWith: string option
            OpenedAt: DateTime
            Seen: DateTime
        }


    /// A stored confirmation code: a MAC of the code, never the digits, with the mail address it was
    /// sent to, its expiry and the number of wrong tries. A user has at most one.
    type PendingCode =
        {
            UserId: string
            MailAddress: string
            CodeMac: byte[]
            Expiry: DateTime
            Tries: int
        }


    /// A launch waiting for the user to set a PIN: what opening the session needs once the PIN is set,
    /// and the public key of the browser that started it, so the session opens on that key. It expires
    /// with its confirmation code.
    type Enrolment =
        {
            Attempt: string
            UserId: string
            Login: string
            DisplayName: string
            PatientId: string
            PublicKey: PublicKey
        }


    /// A stored data notice, which tells a session that the EHR data changed. A session has at most
    /// one, and it expires after two minutes.
    type Notice =
        {
            Nonce: string
            /// The patient read from the EHR when the notice was given. None when the EHR could not
            /// be read.
            EhrPatient: CorePatient option
            /// The patient data the client is shown.
            PatientData: CorePatientData option
            Expiry: DateTime
        }


    /// A stored signing challenge: the digest of the order plan shown, in the canonical form of the
    /// store, and the EHR reading at the time of the challenge. A session has at most one, and it
    /// expires after two minutes. The plan itself is not stored; the submission carries the plan and
    /// is compared by digest.
    type Challenge =
        {
            Nonce: string
            Digest: string
            /// The patient read from the EHR at the challenge. None when the EHR could not be read; a
            /// version signed on that challenge is then unverified.
            EhrPatient: CorePatient option
            Expiry: DateTime
        }


    /// Compares EHR readings for a challenge. The age and the post-menstrual age are left out, so a
    /// reading on another day than the opening is not a change by that alone.
    module Reads =

        /// Whether two EHR reads hold the same data, leaving out the age and the post-menstrual age
        /// each read calculated; the birthdate and the gestational age they come from are compared.
        let sameDataApartFromAges (read: CorePatient option) (other: CorePatient option) =
            let withoutAges (ehr: CorePatient) =
                { ehr with
                    Data =
                        { ehr.Data with
                            Age = None
                            PostMenstrualAge = None
                        }
                }

            (read |> Option.map withoutAges) = (other |> Option.map withoutAges)


        /// Whether a new EHR reading is a change: no reading, or other data than the reading the session
        /// opened on. For a session that opened without a reading, every reading is a change.
        let changed (opened: CorePatient option) (current: CorePatient option) =
            current.IsNone || not (sameDataApartFromAges current opened)


        /// Returns the notice of the session that the token names, when the notice was given for the
        /// same EHR data as the current reading. None otherwise, so a changed reading needs a new notice.
        let accepted (notices: Map<string, Notice>) (sid: string) (token: string option) (current: CorePatient option) =
            token
            |> Option.bind (fun token ->
                notices
                |> Map.tryFind sid
                |> Option.filter (fun n -> n.Nonce = token && sameDataApartFromAges n.EhrPatient current)
            )


        /// Returns the EHR reading of a session after a commit: the reading of the challenge, so the next
        /// challenge compares against the signed data. Falls back on the opening reading when the
        /// challenge has none.
        let afterCommit (challenge: Challenge) (opened: CorePatient option) =
            challenge.EhrPatient |> Option.orElse opened


    /// The result of a store write: written; a conflict because another server signed first, with the
    /// version that was stored; or a failure with its reason.
    [<RequireQualifiedAccess>]
    type StoreOutcome =
        | Written
        | Conflict of StoredVersion
        | Failed of reason: string


    type LaunchRecord =
        {
            Nonce: string
            State: string
            PatientId: string
            PublicKey: PublicKey
            Expiry: DateTime
            Outcome: LaunchResult option
        }


    /// How the store records the end of a session. A superseded session gets no record, because the
    /// newer session of the same login shows it.
    [<RequireQualifiedAccess>]
    type StoredEnding =
        | Closed
        | Ended of SessionEnding


    /// A write to the store as a value, one case per stored fact. A session function returns the writes
    /// of its request with the new state and its answer.
    type Persist =
        /// Writes a signed order plan version with the identity of its patient.
        | WriteVersion of OrderPlanVersion * identity: Informedica.GenCore.Lib.Patients.PatientIdentity option
        /// Writes a new launch record.
        | RecordLaunch of LaunchRecord
        /// Writes the outcome of the callback of a launch.
        | RecordLaunchOutcome of nonce: string * LaunchResult * at: DateTime
        /// Writes a newly opened session.
        | OpenSession of sessionId: string * SessionRecord
        /// Writes the version and the act a session opened with, after an act changed them.
        | RecordOpenedWith of sessionId: string * SessionRecord * at: DateTime
        /// Writes the time of the last request of a session.
        | RecordSeen of sessionId: string * at: DateTime
        /// Ends a session by a close, idle time or the wrong-PIN limit.
        | EndSession of sessionId: string * StoredEnding * at: DateTime
        /// Records that the user acknowledged the end of a session.
        | AcknowledgeEnding of sessionId: string * at: DateTime
        /// Writes the credential after the event that changed it: the PIN set, a wrong PIN, the lock
        /// reached or a right PIN.
        | WriteCredential of userId: string * event: string * Credential * at: DateTime
        /// Writes the confirmation code mailed when a launch waits for a PIN.
        | WriteCode of PendingCode * at: DateTime
        /// Counts a wrong try of the code with this MAC. The MAC names the code the request read,
        /// because another server may have mailed a newer code that a try of the older code must not void.
        | CountCodeTry of userId: string * codeMac: byte[] * at: DateTime
        /// Spends the code with this MAC, for the same reason as CountCodeTry.
        | SpendCode of userId: string * codeMac: byte[] * at: DateTime
        /// Writes a launch that waits for a PIN.
        | WriteEnrolment of Enrolment * at: DateTime
        /// Drops the enrolment a browser gave up.
        | DropEnrolmentWrite of attempt: string * at: DateTime
        /// Drops every enrolment of a user, when the PIN is set or the code is void.
        | DropEnrolmentsOf of userId: string * at: DateTime
        /// Writes the data notice of a session.
        | WriteNotice of sessionId: string * Notice * at: DateTime
        /// Writes the signing challenge of a session.
        | WriteChallenge of sessionId: string * Challenge * at: DateTime
        /// Spends the challenge with the nonce the request read, so it can no longer be signed.
        | SpendChallenge of sessionId: string * nonce: string * at: DateTime
        /// Stores the answer to a submission, so a resent submission gets the same answer.
        | RememberAnswer of sessionId: string * idemKey: string * SigningOutcome * at: DateTime


    type State =
        {
            Launches: Map<string, LaunchRecord>
            Sessions: Map<string, SessionRecord>
            Endings: Map<string, SessionEnding * DateTime>
            Credentials: Map<string, Credential>
            /// the live confirmation code per person
            Codes: Map<string, PendingCode>
            /// the launches waiting for a PIN, by attempt
            Enrolments: Map<string, Enrolment>
            /// every version of each patient's order plan, newest first, readable or not
            Records: Map<string, StoredVersion list>
            /// the live data notice per Session
            Notices: Map<string, Notice>
            /// the live challenge per Session
            Challenges: Map<string, Challenge>
            /// what a Submission was answered, by Session and by the client's key, so that a
            /// retry gets the same answer
            Answered: Map<string * string, SigningOutcome * DateTime>
        }


    let emptyState =
        {
            Launches = Map.empty
            Sessions = Map.empty
            Endings = Map.empty
            Credentials = Map.empty
            Codes = Map.empty
            Enrolments = Map.empty
            Records = Map.empty
            Notices = Map.empty
            Challenges = Map.empty
            Answered = Map.empty
        }


    let initialState (credentials: Map<string, Credential>) = { emptyState with Credentials = credentials }


    /// How long a confirmation code is valid: long enough for a mail round trip. A launch waiting for a
    /// PIN expires with its code.
    let codeLifetime = TimeSpan.FromMinutes 15.0

    /// Wrong codes before the code is void.
    let maxTries = 3

    /// How long a challenge is valid: two minutes, enough to read the plan and enter a PIN, so what is
    /// signed was checked against the EHR shortly before.
    let challengeLifetime = TimeSpan.FromMinutes 2.0

    /// How long a Session lives without a request unless the site sets its own: an hour. A
    /// Session that has seen no request for that long is probably left behind, and a relaunch
    /// costs the User one click.
    let defaultIdleLifetime = TimeSpan.FromHours 1.0


    let credentialOf (userId: string) (state: State) =
        state.Credentials |> Map.tryFind userId |> Option.defaultValue Credential.empty


    /// The newest version of the record of a patient, readable or not. A session opens from it and a
    /// signature is checked against it.
    let headOf (patientId: string) (state: State) =
        state.Records |> Map.tryFind patientId |> Option.bind List.tryHead


    let private dropExpired (now: DateTime) (state: State) =
        let codes = state.Codes |> Map.filter (fun _ c -> now <= c.Expiry)

        { state with
            Launches = state.Launches |> Map.filter (fun _ r -> now <= r.Expiry)
            Codes = codes
            // An enrolment expires with its confirmation code.
            Enrolments = state.Enrolments |> Map.filter (fun _ e -> codes |> Map.containsKey e.UserId)
            Notices = state.Notices |> Map.filter (fun _ n -> now <= n.Expiry)
            Challenges = state.Challenges |> Map.filter (fun _ c -> now <= c.Expiry)
            Answered = state.Answered |> Map.filter (fun _ (_, at) -> now <= at + challengeLifetime)
        }


    let private answerOf (authorizeUrl: string -> string) (record: LaunchRecord) =
        match record.Outcome with
        | Some outcome -> outcome
        | None -> LaunchResult.RedirectTo(authorizeUrl record.State, record.State)


    let present
        (now: DateTime)
        (newId: unit -> string)
        (verify: Launch -> Result<LaunchSeal.Claims, LaunchRefusal>)
        (authorizeUrl: string -> string)
        (state: State)
        (launch, key)
        : State * LaunchResult * Persist list
        =
        let state = dropExpired now state

        match verify launch with
        | Error refusal -> state, LaunchResult.Refused refusal, []
        | Ok claims ->
            match state.Launches |> Map.tryFind claims.Nonce with
            | Some record when record.PublicKey = key -> state, answerOf authorizeUrl record, []
            | Some _ -> state, LaunchResult.Refused LaunchRefusal.LaunchSpent, []
            | None ->
                let record =
                    {
                        Nonce = claims.Nonce
                        State = newId ()
                        PatientId = claims.PatientId
                        PublicKey = key
                        Expiry = claims.Expiry
                        Outcome = None
                    }

                { state with Launches = state.Launches |> Map.add claims.Nonce record },
                answerOf authorizeUrl record,
                [ RecordLaunch record ]


    /// Returns the patient data with the age and the post-menstrual age that the birthdate gives at the
    /// local date. Without an identity there is no birthdate, and the patient data is returned unchanged.
    let atBirthDate date (whom: CorePatientIdentity option) (patientData: CorePatientData) =
        match whom with
        | None -> patientData
        | Some identity -> (patientData |> CorePatient.create (Some identity) |> CorePatient.atDate date).Data


    /// The patient data of a signed version, aged to the local date. When the age changed, the
    /// estimated weight and height are removed, because an estimate is only valid for the age it
    /// was made for. A measured value stays.
    let signedPatientDataAt date whom (patientData: CorePatientData) =
        let aged = patientData |> atBirthDate date whom

        let measured calc = calc |> Option.filter CoreSource.isMeasured

        // Compare the ages in days. Two age values with different parts can be the same age.
        if aged |> CorePatientData.ageInDays = (patientData |> CorePatientData.ageInDays) then
            aged
        else
            { aged with
                Weight.Calculation = aged.Weight.Calculation |> measured
                Height.Calculation = aged.Height.Calculation |> measured
            }
            |> CorePatientData.calculateBodySurfaceArea


    /// The patient data a Session shows on the local date. After a signing, the data of the signed
    /// version; after any other act, the data from the EHR. Each falls back on the other. None
    /// when neither exists; the user then enters the data.
    let sessionPatientData date (ehrPatients: EhrPatientPort) (opened: OpenedSession) =
        let ofEhrPatient = opened.EhrPatient |> Option.map ehrPatients.patientData

        let ofHead =
            match opened.Head with
            | Some(StoredVersion.Readable(v, whom)) -> Some(v.Plan.PatientData |> signedPatientDataAt date whom)
            | _ -> None

        match opened.Act with
        | SessionAct.Signed -> ofHead |> Option.orElse ofEhrPatient
        | SessionAct.Opened
        | SessionAct.VersionOpened
        | SessionAct.Refreshed -> ofEhrPatient |> Option.orElse ofHead


    /// Opens a session for a launch record at the callback, or for an enrolment once the PIN is set.
    /// The session opens on the newest version of the record and the EHR reading. The other sessions
    /// of the login are ended as superseded, so a user has at most one open session. When this release
    /// cannot read the newest version, the session opens without a version, so the next request
    /// reports a newer version and a signature is refused.
    let private openWith
        (now: DateTime)
        (newId: unit -> string)
        (ehrPatients: EhrPatientPort)
        (patientId: string)
        (key: PublicKey)
        (user: UserContext)
        (state: State)
        =
        let id = newId ()
        let head = headOf patientId state
        let ehr = ehrPatients.read patientId

        let opened: OpenedSession =
            {
                User = Some user
                PatientId = Some patientId
                EhrPatient = ehr
                OpenedToken = Some(OpenedToken $"opened-{id}")
                KeyThumbprint = Some(PublicKey.thumbprint key)
                Head = head
                Act = SessionAct.Opened
            }

        let login = Some user.UserId

        let session =
            {
                Opened = opened
                Login = login
                OpenedWith = head |> Option.bind StoredVersion.readableId
                OpenedAt = now
                Seen = now
            }

        let superseded =
            state.Sessions
            |> Map.filter (fun sid s -> sid <> id && s.Login = login)
            |> Map.toList
            |> List.map fst

        { state with
            Sessions =
                superseded
                |> List.fold (fun m sid -> Map.remove sid m) state.Sessions
                |> Map.add id session
            Endings =
                superseded
                |> List.fold (fun m sid -> Map.add sid (SessionEnding.SupersededByLaunch, now) m) state.Endings
        },
        (id, opened),
        // The superseded sessions need no write: the newer session of the same login shows them.
        [ OpenSession(id, session); RecordOpenedWith(id, session, now) ]


    let private recordOutcome now (record: LaunchRecord) outcome (state: State) =
        { state with Launches = state.Launches |> Map.add record.Nonce { record with Outcome = Some outcome } },
        RecordLaunchOutcome(record.Nonce, outcome, now)


    /// Whether the registry has this patient as the active patient in the EHR.
    let isActive (patientId: string) (standing: UserStanding) = standing.ActivePatientId = Some patientId


    let private openSession now newId ehrPatients (record: LaunchRecord) (standing: UserStanding) (state: State) =
        let state, (id, session), writes =
            openWith now newId ehrPatients record.PatientId record.PublicKey standing.User state

        let state, outcome = recordOutcome now record (LaunchResult.Opened(id, session)) state

        state, CallbackResult.Opened(id, openedUrl), writes @ [ outcome ]


    let private refuse now (record: LaunchRecord) refusal (state: State) =
        let state, outcome = recordOutcome now record (LaunchResult.Refused refusal) state
        state, CallbackResult.Refused(refusal, refusedUrl refusal), [ outcome ]


    /// Makes the launch wait for the user to set a PIN. A valid confirmation code of the user is reused
    /// without a mail; otherwise a new code is mailed to the address the registry gave for this request.
    /// The enrolment gets a new attempt id and the public key of the browser of the launch.
    let private suspend
        (now: DateTime)
        (newId: unit -> string)
        (newCode: unit -> string)
        (codeMac: string -> byte[])
        (send: Mail -> unit)
        (record: LaunchRecord)
        (identity: BrowserIdentity)
        (standing: UserStanding)
        (state: State)
        =
        let userId = standing.User.UserId

        let state, codeWrites =
            match state.Codes |> Map.tryFind userId with
            // The user has a valid code: no second mail and no second record.
            | Some _ -> state, []
            | None ->
                let code = newCode ()

                let subject, body = Mails.confirmationCode identity.DisplayName code (int codeLifetime.TotalMinutes)

                send
                    {
                        To = standing.MailAddress
                        Subject = subject
                        Body = body
                    }

                let pending =
                    {
                        UserId = userId
                        MailAddress = standing.MailAddress
                        CodeMac = codeMac code
                        Expiry = now + codeLifetime
                        Tries = 0
                    }

                { state with Codes = state.Codes |> Map.add userId pending }, [ WriteCode(pending, now) ]

        let attempt = newId ()

        let enrolment =
            {
                Attempt = attempt
                UserId = userId
                Login = identity.Login
                DisplayName = identity.DisplayName
                PatientId = record.PatientId
                PublicKey = record.PublicKey
            }

        let until = state.Codes[userId].Expiry

        let state, outcome =
            { state with Enrolments = state.Enrolments |> Map.add attempt enrolment }
            |> recordOutcome now record (LaunchResult.Enrolling attempt)

        state,
        CallbackResult.Enrolling(attempt, openedUrl, until),
        codeWrites @ [ WriteEnrolment(enrolment, now); outcome ]


    /// The result of the first half of a callback: an answer with its writes, or the identity and the
    /// user standing to open the session with.
    [<RequireQualifiedAccess>]
    type Redeemed =
        | Answered of State * CallbackResult * Persist list
        | Identified of LaunchRecord * BrowserIdentity * UserStanding


    /// <summary>
    /// The first half of the callback. Checks the state against the cookie, answers a reloaded callback
    /// from the outcome of the launch, and otherwise redeems the code for the identity and asks the
    /// registry for the role and the active patient. Reads only the launch record and the session its
    /// outcome names, so a store can load the records of the login afterwards, for <c>openAfterRedeem</c>.
    /// </summary>
    let redeem
        (now: DateTime)
        (redeemCode: string -> BrowserIdentity option)
        (standing: BrowserIdentity -> UserStanding option)
        (state: State)
        (cb: Callback)
        : Redeemed
        =
        let state = dropExpired now state

        let byState =
            state.Launches
            |> Map.toSeq
            |> Seq.map snd
            |> Seq.tryFind (fun r -> r.State = cb.State)

        let answer result = Redeemed.Answered(state, result, [])

        let invalid =
            CallbackResult.Refused(LaunchRefusal.LaunchInvalid, refusedUrl LaunchRefusal.LaunchInvalid)

        match cb.StateCookie, byState with
        | Some cookie, Some record when cookie = cb.State && cb.State <> "" ->
            match record.Outcome with
            | Some(LaunchResult.Opened(id, _)) when state.Sessions |> Map.containsKey id ->
                answer (CallbackResult.Opened(id, openedUrl))
            | Some(LaunchResult.Opened _) -> answer (CallbackResult.Superseded openedUrl)
            | Some(LaunchResult.Refused refusal) -> answer (CallbackResult.Refused(refusal, refusedUrl refusal))
            | Some(LaunchResult.Enrolling attempt) ->
                match state.Enrolments |> Map.tryFind attempt with
                | Some e -> answer (CallbackResult.Enrolling(attempt, openedUrl, state.Codes[e.UserId].Expiry))
                | None ->
                    answer (
                        CallbackResult.Refused(
                            LaunchRefusal.EnrolmentRequired,
                            refusedUrl LaunchRefusal.EnrolmentRequired
                        )
                    )
            | Some(LaunchResult.RedirectTo _)
            | None ->
                let identity =
                    match cb.Error, cb.Code with
                    | None, Some code -> redeemCode code
                    | _ -> None

                match identity with
                | None -> Redeemed.Answered(refuse now record LaunchRefusal.NoBrowserIdentity state)
                | Some identity ->
                    match standing identity with
                    | None -> Redeemed.Answered(refuse now record LaunchRefusal.NoRole state)
                    | Some standing -> Redeemed.Identified(record, identity, standing)
        | _, None -> answer invalid
        | _ -> answer invalid


    /// <summary>
    /// The second half of the callback. Checks the active patient, makes the launch wait for a PIN when
    /// the credential of a prescriber has no PIN, and otherwise opens the session.
    /// </summary>
    let openAfterRedeem
        (now: DateTime)
        (newId: unit -> string)
        (newCode: unit -> string)
        (codeMac: string -> byte[])
        (ehrPatients: EhrPatientPort)
        (send: Mail -> unit)
        (record: LaunchRecord, identity: BrowserIdentity, standing: UserStanding)
        (state: State)
        : State * CallbackResult * Persist list
        =
        let state = dropExpired now state

        if standing |> isActive record.PatientId |> not then
            refuse now record LaunchRefusal.WrongActivePatient state
        elif
            standing.User.Role = UserRole.Prescriber
            && not (credentialOf standing.User.UserId state |> Credential.pinSet)
        then
            suspend now newId newCode codeMac send record identity standing state
        else
            openSession now newId ehrPatients record standing state


    /// Handles the callback from the identity provider: runs redeem and then openAfterRedeem on one
    /// state. A prescriber without a PIN is not refused; the launch waits until the PIN is set. A
    /// reloaded callback gets the same answer while the enrolment exists; after that, a relaunch is
    /// asked for.
    let callback
        (now: DateTime)
        (newId: unit -> string)
        (newCode: unit -> string)
        (codeMac: string -> byte[])
        (redeemCode: string -> BrowserIdentity option)
        (standing: BrowserIdentity -> UserStanding option)
        (ehrPatients: EhrPatientPort)
        (send: Mail -> unit)
        (state: State)
        (cb: Callback)
        : State * CallbackResult * Persist list
        =
        match redeem now redeemCode standing state cb with
        | Redeemed.Answered(state, result, writes) -> state, result, writes
        | Redeemed.Identified(record, identity, standing) ->
            openAfterRedeem now newId newCode codeMac ehrPatients send (record, identity, standing) state


    /// Returns the enrolment of an attempt for GetSession: the name of the user and the masked mail
    /// address the code was sent to. None when the enrolment has expired or is gone.
    let findEnrolment (now: DateTime) (attempt: string) (state: State) : State * EnrolmentPending option =
        let state = dropExpired now state

        match state.Enrolments |> Map.tryFind attempt with
        | Some e ->
            state,
            Some
                {
                    DisplayName = e.DisplayName
                    MailHint = MailHint.ofAddress state.Codes[e.UserId].MailAddress
                }
        | None -> state, None


    /// Removes the confirmation code of a user and every enrolment bound to it, when the PIN is set, the
    /// code is void or the last browser gave up.
    let private dropCode (now: DateTime) (userId: string) (state: State) =
        // Spends the code this request read; there is nothing to spend when the code is already gone.
        let spent =
            state.Codes
            |> Map.tryFind userId
            |> Option.map (fun code -> [ SpendCode(userId, code.CodeMac, now) ])
            |> Option.defaultValue []

        { state with
            Codes = state.Codes |> Map.remove userId
            Enrolments = state.Enrolments |> Map.filter (fun _ e -> e.UserId <> userId)
        },
        spent @ [ DropEnrolmentsOf(userId, now) ]


    /// Drops an enrolment the browser gave up, by CloseSession while enrolling. The code stays for the
    /// other enrolments of the user; with the last enrolment the code goes too, so the next launch mails
    /// a new code.
    let dropEnrolment (now: DateTime) (attempt: string) (state: State) : State * Persist list =
        match state.Enrolments |> Map.tryFind attempt with
        | None -> state, []
        | Some e ->
            let state = { state with Enrolments = state.Enrolments |> Map.remove attempt }
            let dropped = DropEnrolmentWrite(attempt, now)

            if state.Enrolments |> Map.exists (fun _ o -> o.UserId = e.UserId) then
                // Another browser is still enrolling with this code, so the code stays.
                state, [ dropped ]
            else
                let state, writes = dropCode now e.UserId state
                state, dropped :: writes


    /// Sets the PIN with the confirmation code, checked in order. The enrolment and its code must exist.
    /// The PIN must have a valid format; a wrong format costs no try. A wrong code counts a try, and the
    /// third wrong code voids the code for every enrolment. A right code sets the PIN with a wrong-PIN
    /// count of zero, drops the code and its enrolments, and mails the user at the address the registry
    /// gives now, or else the address of the code. The session then opens on the public key of this
    /// enrolment, with the role the registry gives now, but only when the patient of the launch is
    /// still the active patient.
    let supplyPin
        (now: DateTime)
        (newId: unit -> string)
        (newSalt: int -> byte[])
        (codeMac: string -> byte[])
        (standing: BrowserIdentity -> UserStanding option)
        (ehrPatients: EhrPatientPort)
        (send: Mail -> unit)
        (attempt: string)
        (code: string)
        (pin: string)
        (state: State)
        : State * SupplyPinResult * Persist list
        =
        let state = dropExpired now state

        match state.Enrolments |> Map.tryFind attempt with
        | None -> state, SupplyPinResult.Refused PinRefusal.AttemptExpired, []
        | Some e ->
            let pending = state.Codes[e.UserId]

            if not (Pin.isValid pin) then
                state, SupplyPinResult.Refused PinRefusal.PinFormat, []
            elif
                not (
                    System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(codeMac code, pending.CodeMac)
                )
            then
                let tries = pending.Tries + 1

                if tries >= maxTries then
                    // The third wrong code voids the code for every enrolment bound to it.
                    let state, writes = dropCode now e.UserId state

                    state,
                    SupplyPinResult.Refused PinRefusal.CodeVoid,
                    CountCodeTry(e.UserId, pending.CodeMac, now) :: writes
                else
                    { state with Codes = state.Codes |> Map.add e.UserId { pending with Tries = tries } },
                    SupplyPinResult.Refused(PinRefusal.WrongCode(maxTries - tries)),
                    [ CountCodeTry(e.UserId, pending.CodeMac, now) ]
            else
                let identity =
                    {
                        Login = e.Login
                        DisplayName = e.DisplayName
                    }

                // Asks the registry again for the mail address, the role and the active patient,
                // because they may have changed during the wait. When the registry does not answer,
                // the code has already proved the mailbox, so the PIN is set and the launch continues
                // with the enrolment data.
                let fresh = standing identity

                let address = fresh |> Option.map _.MailAddress |> Option.defaultValue pending.MailAddress

                let user =
                    fresh
                    |> Option.map _.User
                    |> Option.defaultValue
                        {
                            UserId = e.UserId
                            DisplayName = e.DisplayName
                            Role = UserRole.Prescriber
                        }

                let subject, body = Mails.pinSet e.DisplayName

                send
                    {
                        To = address
                        Subject = subject
                        Body = body
                    }

                let credential = Credential.withPin newSalt pin

                let state, dropped =
                    { state with Credentials = state.Credentials |> Map.add e.UserId credential }
                    |> dropCode now e.UserId

                let settled = WriteCredential(e.UserId, "pin-set", credential, now) :: dropped

                match fresh with
                | Some s when s |> isActive e.PatientId |> not ->
                    // The PIN is set and mailed, but no session opens for a patient that is no
                    // longer active, and a relaunch is asked for. The PIN writes are stored either way.
                    state, SupplyPinResult.Refused PinRefusal.WrongActivePatient, settled
                | _ ->
                    let state, (id, session), writes = openWith now newId ehrPatients e.PatientId e.PublicKey user state

                    state, SupplyPinResult.Opened(id, session), settled @ writes


    /// Whether a session has had no request for the idle lifetime or longer.
    let isIdle (idle: TimeSpan) (now: DateTime) (record: SessionRecord) = now - record.Seen >= idle


    /// Ends an idle session before the request that names it runs. The session leaves the open
    /// sessions and its ending is recorded, so the request reports the ending and a relaunch reads the
    /// patient data again. An open session seen within the lifetime, an ended session and an unknown id
    /// are left as they are. There is no timer: a session that gets no further request costs nothing.
    let endIdle (idle: TimeSpan) (now: DateTime) (sid: string) (state: State) : State * Persist list =
        match state.Sessions |> Map.tryFind sid with
        | Some record when isIdle idle now record ->
            { state with
                Sessions = state.Sessions |> Map.remove sid
                Endings = state.Endings |> Map.add sid (SessionEnding.Idle, now)
            },
            [ EndSession(sid, StoredEnding.Ended SessionEnding.Idle, now) ]
        | _ -> state, []


    /// Records a request of a session, which restarts its idle time. Every function that takes the
    /// session id calls it, except close, which ends the session. Does nothing for an unknown session.
    let touch (now: DateTime) (sid: string) (state: State) : State * Persist list =
        match state.Sessions |> Map.tryFind sid with
        | Some r ->
            { state with Sessions = state.Sessions |> Map.add sid { r with Seen = now } }, [ RecordSeen(sid, now) ]
        | None -> state, []


    let find (now: DateTime) (id: string) (state: State) : State * SessionLookup * Persist list =
        match state.Sessions |> Map.tryFind id with
        | Some record ->
            let state, seen = touch now id state
            state, SessionLookup.Found record.Opened, seen
        | None ->
            match state.Endings |> Map.tryFind id with
            | Some(ending, _) -> state, SessionLookup.Ended ending, []
            | None -> state, SessionLookup.NotFound, []


    /// Closes a session or acknowledges its ending. An open session ends as closed and is acknowledged
    /// at once; an ended session is acknowledged. Neither is reported again.
    let close (now: DateTime) (id: string) (state: State) : State * Persist list =
        let writes =
            if state.Sessions |> Map.containsKey id then
                [ EndSession(id, StoredEnding.Closed, now); AcknowledgeEnding(id, now) ]
            elif state.Endings |> Map.containsKey id then
                [ AcknowledgeEnding(id, now) ]
            else
                []

        { state with
            Sessions = state.Sessions |> Map.remove id
            Endings = state.Endings |> Map.remove id
        },
        writes


    /// Returns the newest version of the record when it is not the version the session opened with. A
    /// submission is refused while such a newer version exists.
    let blockedBy (record: SessionRecord) (patientId: string) (state: State) =
        match headOf patientId state with
        | Some head when Some(StoredVersion.id head) <> record.OpenedWith -> Some(StoredVersion.head head)
        | _ -> None


    /// Returns the newest version when this release cannot read it. A signature is then refused
    /// whatever base the client names, because a signature is only accepted against the newest version.
    let unreadableHead (patientId: string) (state: State) =
        match headOf patientId state with
        | Some(StoredVersion.Unreadable _ as head) -> Some(StoredVersion.head head)
        | _ -> None


    /// Checks a computing request against its session. For an unknown session, returns its recorded
    /// ending, if any. For an open session, records the request and, when the request has the token of
    /// the session, reports a newer version than the session opened with. The notice blocks nothing;
    /// the refusal at submission is the only guard. An anonymous session, a session without a patient
    /// or a request without a token gets no notice. A request with another token gets InactiveTab.
    let seen
        (now: DateTime)
        (sid: string)
        (opened: OpenedToken option)
        (state: State)
        : State * RecordNotice option * Persist list
        =
        match state.Sessions |> Map.tryFind sid with
        | None -> state, state.Endings |> Map.tryFind sid |> Option.map (fst >> RecordNotice.Ended), []
        | Some record ->
            let state, writes = touch now sid state

            let told =
                if opened.IsSome && opened <> record.Opened.OpenedToken then
                    Some(RecordNotice.Ended SessionEnding.InactiveTab)
                else
                    match record.Opened.User, record.Opened.PatientId with
                    | Some _, Some patientId when opened.IsSome ->
                        blockedBy record patientId state |> Option.map RecordNotice.NewerVersion
                    | _ -> None

            state, told, writes


    /// Runs a command that changes a session only with the token of that session; a missing token
    /// counts as another token. For an unknown session the command runs, so a close still acknowledges
    /// an ending.
    let whenOwnToken sid opened run (state: State) =
        match state.Sessions |> Map.tryFind sid with
        | Some record when opened <> record.Opened.OpenedToken -> state, OnOwnToken.InactiveTab, []
        | _ ->
            let state, answer, writes = run state
            state, OnOwnToken.Done answer, writes


    /// Opens the version with id in the session. Returns None for an unknown session, an anonymous
    /// session or a session without a patient. An id the record of the patient does not hold, or a
    /// version this release cannot read, opens nothing and returns the session unchanged. The version
    /// that is already open keeps its token. Another version gets a new OpenedToken, and the challenge
    /// and the notice of the session are dropped, so a challenge for the previous version cannot be
    /// answered. Any readable version can be opened; a version that is not the newest blocks submission.
    let openVersion
        (now: DateTime)
        (newId: unit -> string)
        (sid: string)
        (id: string)
        (state: State)
        : State * OpenedSession option * Persist list
        =
        match state.Sessions |> Map.tryFind sid with
        | None -> state, None, []
        | Some record ->
            let state, seen = touch now sid state

            match record.Opened.User, record.Opened.PatientId with
            | None, _
            | _, None -> state, None, seen
            | Some _, Some patientId ->
                let version =
                    state.Records
                    |> Map.tryFind patientId
                    |> Option.bind (
                        List.tryPick (fun v ->
                            match v with
                            | StoredVersion.Readable(v, _) as stored when v.Id = id -> Some stored
                            | _ -> None
                        )
                    )

                match version with
                | None -> state, Some record.Opened, seen
                | Some version when record.OpenedWith = Some id ->
                    let opened =
                        { record.Opened with
                            Head = Some version
                            Act = SessionAct.VersionOpened
                        }

                    let session = { state.Sessions[sid] with Opened = opened }

                    { state with Sessions = state.Sessions |> Map.add sid session },
                    Some opened,
                    seen @ [ RecordOpenedWith(sid, session, now) ]
                | Some version ->
                    let opened =
                        { record.Opened with
                            OpenedToken = Some(OpenedToken $"opened-{newId ()}")
                            Head = Some version
                            Act = SessionAct.VersionOpened
                        }

                    let session =
                        { state.Sessions[sid] with
                            Opened = opened
                            OpenedWith = Some id
                        }

                    let spent =
                        state.Challenges
                        |> Map.tryFind sid
                        |> Option.map (fun c -> [ SpendChallenge(sid, c.Nonce, now) ])
                        |> Option.defaultValue []

                    { state with
                        Sessions = state.Sessions |> Map.add sid session
                        Challenges = state.Challenges |> Map.remove sid
                        Notices = state.Notices |> Map.remove sid
                    },
                    Some opened,
                    seen @ spent @ [ RecordOpenedWith(sid, session, now) ]


    /// Whether an order appears more than once in a plan.
    let duplicateOrders (plan: GenOrder.OrderPlan) =
        plan
        |> Informedica.GenOrder.Lib.OrderPlan.orders
        |> Array.countBy _.Order.Id
        |> Array.exists (fun (_, n) -> n > 1)


    /// Refreshes a session: reads the EHR again, gives a new OpenedToken, spends the challenge and drops
    /// the notice. The order plan version the session opened with stays; opening the last signed version
    /// is a separate action. Returns None for an unknown session, an anonymous session or a session
    /// without a patient. Without an EHR reading the session stays as it was, including its token and
    /// its act, and only the request time is written, so the act of the newest stored session stays true.
    let refresh
        (now: DateTime)
        (newId: unit -> string)
        (ehrPatients: EhrPatientPort)
        (sid: string)
        (state: State)
        : State * OpenedSession option * Persist list
        =
        match state.Sessions |> Map.tryFind sid with
        | None -> state, None, []
        | Some record ->
            let state, seen = touch now sid state

            match record.Opened.User, record.Opened.PatientId with
            | None, _
            | _, None -> state, None, seen
            | Some _, Some patientId ->
                match ehrPatients.read patientId with
                | None -> state, Some record.Opened, seen
                | Some read ->
                    let opened =
                        { record.Opened with
                            OpenedToken = Some(OpenedToken $"opened-{newId ()}")
                            EhrPatient = Some read
                            Act = SessionAct.Refreshed
                        }

                    let session = { state.Sessions[sid] with Opened = opened }

                    let spent =
                        state.Challenges
                        |> Map.tryFind sid
                        |> Option.map (fun c -> [ SpendChallenge(sid, c.Nonce, now) ])
                        |> Option.defaultValue []

                    { state with
                        Sessions = state.Sessions |> Map.add sid session
                        Challenges = state.Challenges |> Map.remove sid
                        Notices = state.Notices |> Map.remove sid
                    },
                    Some opened,
                    seen @ spent @ [ RecordOpenedWith(sid, session, now) ]


    /// Who the registry is asked about for a Session: its login, or the user id without one.
    let identityOf (record: SessionRecord) (user: UserContext) =
        {
            Login = record.Login |> Option.defaultValue user.UserId
            DisplayName = user.DisplayName
        }


    /// Whether the patient of the session is no longer the active patient in the EHR: another patient or
    /// no patient is active, the same check as at a launch. A registry that does not answer gives no
    /// warning.
    let ehrMovedOn (patientId: string) (standing: UserStanding option) =
        standing |> Option.exists (isActive patientId >> not)


    /// Issues a signing challenge, checked in order: a session with a user and a patient; the role
    /// prescriber; the OpenedToken of the session; then a new EHR reading. When the reading differs from
    /// the opening reading and no notice for this reading was accepted, returns a data notice instead,
    /// which replaces an earlier notice and drops an earlier challenge. A plan with an order twice is
    /// refused. The newest version must be readable and be the version the session opened with. Then
    /// issues a challenge over the digest of this plan, which replaces the earlier challenge and drops
    /// the notice. The patient data of the plan is what the user saw, entered or read; the patient is
    /// the patient of the session, never of the request. The PIN is not checked, so a refusal here
    /// costs no attempt.
    let challenge
        (now: DateTime)
        (newId: unit -> string)
        (digest: GenOrder.OrderPlan -> string)
        (ehrPatients: EhrPatientPort)
        (standing: BrowserIdentity -> UserStanding option)
        (sid: string)
        (plan: GenOrder.OrderPlan, opened: OpenedToken, notice: string option)
        (state: State)
        : State * SigningOutcome * Persist list
        =
        let state, seen = dropExpired now state |> touch now sid

        let refuse refusal = state, SigningOutcome.Refused refusal, seen

        match state.Sessions |> Map.tryFind sid with
        | None -> refuse SigningRefusal.NoSession
        | Some record ->
            match record.Opened.User, record.Opened.PatientId with
            | None, _ -> refuse SigningRefusal.NotPrescriber
            | Some _, None -> refuse SigningRefusal.NoPatient
            | Some user, Some patientId ->
                if user.Role <> UserRole.Prescriber then
                    refuse SigningRefusal.NotPrescriber
                elif record.Opened.OpenedToken <> Some opened then
                    refuse SigningRefusal.StaleToken
                else
                    // Reads the EHR again and compares the readings, so only a change in the EHR data gives a notice.
                    let currentEhrPatient = ehrPatients.read patientId
                    let accepted = Reads.accepted state.Notices sid notice currentEhrPatient

                    // No reading, or a reading other than at the opening, gives a data notice before the challenge.
                    if Reads.changed record.Opened.EhrPatient currentEhrPatient && accepted.IsNone then
                        let nonce = newId ()

                        // None when the EHR could not be read.
                        let current = currentEhrPatient |> Option.map ehrPatients.patientData

                        let notice =
                            {
                                Nonce = nonce
                                EhrPatient = currentEhrPatient
                                PatientData = current
                                Expiry = now + challengeLifetime
                            }

                        // a challenge over the data before the change must not be signed
                        let spent =
                            state.Challenges
                            |> Map.tryFind sid
                            |> Option.map (fun c -> [ SpendChallenge(sid, c.Nonce, now) ])
                            |> Option.defaultValue []

                        { state with
                            Notices = state.Notices |> Map.add sid notice
                            Challenges = state.Challenges |> Map.remove sid
                        },
                        SigningOutcome.DataNotice(nonce, current),
                        seen @ spent @ [ WriteNotice(sid, notice, now) ]
                    // no challenge over a plan that names an order twice
                    elif duplicateOrders plan then
                        refuse SigningRefusal.ChallengeMismatch
                    else
                        match unreadableHead patientId state, blockedBy record patientId state with
                        | Some head, _
                        | None, Some head -> refuse (SigningRefusal.Blocked head)
                        | None, None ->
                            let nonce = newId ()
                            let moved = identityOf record user |> standing |> ehrMovedOn patientId

                            let challenge =
                                {
                                    Nonce = nonce
                                    Digest = digest plan
                                    EhrPatient = currentEhrPatient
                                    Expiry = now + challengeLifetime
                                }

                            { state with
                                Notices = state.Notices |> Map.remove sid
                                Challenges = state.Challenges |> Map.add sid challenge
                            },
                            SigningOutcome.ChallengeIssued(nonce, moved),
                            seen @ [ WriteChallenge(sid, challenge, now) ]


    /// The local date the open challenge of a Session was made on. None when the Session has no
    /// challenge. A submission reads its plan on this date, so that the weight and the height get
    /// the same date as at the challenge.
    let challengeDate (zone: TimeZoneInfo) (sid: string) (state: State) =
        state.Challenges
        |> Map.tryFind sid
        |> Option.map (fun challenge -> challenge.Expiry - challengeLifetime |> LocalDate.ofUtc zone)


    /// Commits a signature, checked in order: a session with a user and a patient; an answer already
    /// given for this idempotency key of the session; the role prescriber from the registry, refused when
    /// the registry does not answer; the OpenedToken of the session; a readable newest version that is
    /// the version the session opened with; the challenge of the session, matching this plan by digest;
    /// and last the PIN, so a submission that fails anyway costs no attempt. On a right PIN the returned
    /// state holds the new version with the identity of the session, the challenge is spent and the
    /// OpenedToken is new, and the answer carries the signed patient at the age on the local date of
    /// signing. The returned writes are for the adapter to run, also on a refusal. Every answer, refusals
    /// included, is stored under the key. The third wrong PIN ends the session, locks signing and mails
    /// the user; a wrong PIN while locked extends the lock; a right PIN while locked is refused and
    /// counts nothing.
    let commit
        (zone: TimeZoneInfo)
        (now: DateTime)
        (newId: unit -> string)
        (digest: GenOrder.OrderPlan -> string)
        (standing: BrowserIdentity -> UserStanding option)
        (send: Mail -> unit)
        (sid: string)
        (signature: Signature)
        (state: State)
        : State * SigningOutcome * Persist list
        =
        let state, seen = dropExpired now state |> touch now sid

        let refuse refusal = state, SigningOutcome.Refused refusal, seen

        match state.Sessions |> Map.tryFind sid with
        | None -> refuse SigningRefusal.NoSession
        | Some record ->
            match record.Opened.User, record.Opened.PatientId with
            | None, _ -> refuse SigningRefusal.NotPrescriber
            | Some _, None -> refuse SigningRefusal.NoPatient
            | Some user, Some patientId ->
                match state.Answered |> Map.tryFind (sid, signature.IdemKey) with
                | Some(answer, _) -> state, answer, seen
                | None ->
                    // From here on every answer is stored under the session and the key, so a resent
                    // submission gets the same answer.
                    let remember (state: State) answer writes =
                        { state with Answered = state.Answered |> Map.add (sid, signature.IdemKey) (answer, now) },
                        answer,
                        seen @ writes @ [ RememberAnswer(sid, signature.IdemKey, answer, now) ]

                    let refuse refusal = remember state (SigningOutcome.Refused refusal) []

                    match identityOf record user |> standing with
                    | Some fresh when fresh.User.Role = UserRole.Prescriber ->
                        if record.Opened.OpenedToken <> Some signature.Opened then
                            refuse SigningRefusal.StaleToken
                        else
                            match unreadableHead patientId state, blockedBy record patientId state with
                            | Some head, _
                            | None, Some head -> refuse (SigningRefusal.Blocked head)
                            | None, None ->
                                match state.Challenges |> Map.tryFind sid with
                                | None -> refuse SigningRefusal.ChallengeExpired
                                | Some challenge when
                                    challenge.Nonce <> signature.Challenge
                                    || challenge.Digest <> digest signature.Plan
                                    || duplicateOrders signature.Plan
                                    ->
                                    refuse SigningRefusal.ChallengeMismatch
                                | Some challenge ->
                                    let credential = credentialOf user.UserId state
                                    let wasLocked = Credential.isLocked now credential
                                    let right, credential = Credential.verify now signature.Pin credential

                                    let state =
                                        { state with Credentials = state.Credentials |> Map.add user.UserId credential }

                                    // The credential is written on every entry: a right PIN clears the
                                    // count, a wrong PIN raises it and the limit locks signing.
                                    let credentialWrite =
                                        WriteCredential(
                                            user.UserId,
                                            (if right then "right"
                                             elif credential.LockedUntil.IsSome then "locked"
                                             else "wrong"),
                                            credential,
                                            now
                                        )

                                    if right then
                                        let id = newId ()

                                        let version: OrderPlanVersion =
                                            {
                                                Id = id
                                                No =
                                                    1
                                                    + (headOf patientId state
                                                       |> Option.map StoredVersion.no
                                                       |> Option.defaultValue 0)
                                                PatientId = patientId
                                                Base = record.OpenedWith
                                                SignedBy =
                                                    {
                                                        UserId = user.UserId
                                                        DisplayName = user.DisplayName
                                                    }
                                                SignedAt = now
                                                Plan = signature.Plan
                                                Verified = challenge.EhrPatient.IsSome
                                                EhrPatient = challenge.EhrPatient
                                            }

                                        let token = OpenedToken $"opened-{newId ()}"

                                        // The session uses the EHR reading just signed on, and the version
                                        // names the identity of that reading. The answer returns the signed
                                        // patient at the age on the date of signing; the version keeps the
                                        // age it was signed at.
                                        let ehr = Reads.afterCommit challenge record.Opened.EhrPatient
                                        let whom = OpenedSession.identity { record.Opened with EhrPatient = ehr }

                                        let patient =
                                            version.Plan.PatientData |> atBirthDate (LocalDate.ofUtc zone now) whom

                                        let opened =
                                            { record with
                                                Opened =
                                                    { record.Opened with
                                                        OpenedToken = Some token
                                                        Head = Some(StoredVersion.Readable(version, whom))
                                                        EhrPatient = ehr
                                                        Act = SessionAct.Signed
                                                    }
                                                OpenedWith = Some id
                                            }

                                        remember
                                            { state with
                                                Records =
                                                    state.Records
                                                    |> Map.change
                                                        patientId
                                                        (fun versions ->
                                                            Some(
                                                                StoredVersion.Readable(version, whom)
                                                                :: (versions |> Option.defaultValue [])
                                                            )
                                                        )
                                                Challenges = state.Challenges |> Map.remove sid
                                                Sessions = state.Sessions |> Map.add sid opened
                                            }
                                            (SigningOutcome.Submitted(version, whom, token, patient))
                                            [
                                                credentialWrite
                                                WriteVersion(version, whom)
                                                RecordOpenedWith(sid, opened, now)
                                                // Spends the challenge this signature answered.
                                                SpendChallenge(sid, challenge.Nonce, now)
                                            ]
                                    elif wasLocked then
                                        // The lock belongs to the credential, so the session stays open.
                                        remember
                                            state
                                            (SigningOutcome.Refused(SigningRefusal.Locked credential.LockedUntil.Value))
                                            [ credentialWrite ]
                                    elif credential |> Credential.attemptsLeft = 0 then
                                        // The wrong-PIN limit is reached, so the session ends.
                                        let subject, body = Mails.pinLimit user.DisplayName

                                        // The mail is best effort: the ending and the lock are stored even
                                        // when sending fails.
                                        try
                                            send
                                                {
                                                    To = fresh.MailAddress
                                                    Subject = subject
                                                    Body = body
                                                }
                                        with _ ->
                                            ()

                                        remember
                                            { state with
                                                Sessions = state.Sessions |> Map.remove sid
                                                Endings =
                                                    state.Endings |> Map.add sid (SessionEnding.WrongPinLimit, now)
                                                Challenges = state.Challenges |> Map.remove sid
                                            }
                                            (SigningOutcome.Refused SigningRefusal.PinLimit)
                                            [
                                                credentialWrite
                                                EndSession(sid, StoredEnding.Ended SessionEnding.WrongPinLimit, now)
                                            ]
                                    else
                                        remember
                                            state
                                            (SigningOutcome.Refused(
                                                SigningRefusal.PinWrong(credential |> Credential.attemptsLeft)
                                            ))
                                            [ credentialWrite ]
                    | _ -> refuse SigningRefusal.NotPrescriber


    /// Six digits from a random source (the CSPRNG in the host).
    let newCode (randomBelow: int -> int) () = (randomBelow 1_000_000).ToString "D6"


    /// The mac of a code under the host key: what the store keeps instead of the digits.
    let codeMac (key: LaunchSeal.Key) (code: string) = LaunchSeal.mac key (Text.Encoding.UTF8.GetBytes code)
