namespace ServerApi

open System
open Shared.Types

// The store's clinical records are the domain's; unqualified, the names below are the
// contract model's, which the identity half of the session keeps.
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


    /// What a Launch says once the seal is verified.
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


/// A PIN as the Database keeps it: never the PIN, a PBKDF2-SHA256 derivation under a salt of
/// its own, so that two Users with the same PIN have nothing in common on disk.
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


/// The credential the store holds per person (keyed by UserId, not by login). No PIN yet is a
/// credential without one; the wrong-PIN count is per credential, counted across Sessions and
/// reset to zero when the PIN is set.
type Credential =
    {
        PinHash: PinHash option
        WrongCount: int
        /// signing is locked until this moment; a delay, not a state
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


    /// A credential with this PIN, a count of zero and no lock: setting the PIN resets both.
    let withPin (newSalt: int -> byte[]) (pin: string) : Credential =
        {
            PinHash = Some(PinHash.make newSalt pin)
            WrongCount = 0
            LockedUntil = None
        }


    /// Wrong PINs before the Session ends and signing locks.
    let wrongPinLimit = 3

    /// The first lock: one minute.
    let lockBase = TimeSpan.FromMinutes 1.0


    /// The longest lock. Rule 28's delay decays with time; the decay is not built, so the
    /// stub caps the delay instead: an unbounded doubling overflows the arithmetic long before
    /// it overflows anyone's patience.
    let lockMax = TimeSpan.FromHours 24.0


    /// The delay after count wrong entries. The entry that reaches the limit locks for
    /// lockBase; each one after it doubles that, up to lockMax.
    let lockFor (count: int) =
        // 2^11 minutes is already past a day; the bound keeps `pown` in range
        let doublings = min 11 (max 0 (count - wrongPinLimit))
        min lockMax (lockBase * float (pown 2 doublings))


    /// Whether signing is locked at this moment.
    let isLocked (now: DateTime) (credential: Credential) =
        match credential.LockedUntil with
        | Some until -> now < until
        | None -> false


    /// Whether the PIN is accepted, and the credential as it stands after the entry. The PIN
    /// is verified here and nowhere else. A right PIN while unlocked zeroes the count and
    /// clears the lock; a right PIN
    /// while locked is refused and counts nothing; a wrong PIN adds one and, at the limit or
    /// beyond it, locks for lockFor from now, so a wrong entry while locked pushes the
    /// delay out and doubles it.
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

    /// n***@stub.example: enough for the User to know which mailbox, not enough for a
    /// shoulder to read the address.
    let ofAddress (address: string) =
        match address.IndexOf '@' with
        | i when i > 0 -> $"{address[0]}***{address.Substring i}"
        | _ -> "***"


/// The mails the server sends a User: the confirmation code, the notice that a PIN was set,
/// the notice at the wrong-PIN limit. English only; the mail language is a later concern.
module Mails =

    let confirmationCode (displayName: string) (code: string) (minutes: int) : string * string =
        "GenPRES: your confirmation code",
        $"Hello {displayName},\n\nYour confirmation code is {code}. It is valid for {minutes} minutes. Enter it in GenPRES together with the PIN of your choice.\n\nIf you did not open GenPRES just now, somebody tried to enrol in your name; nothing was set."


    let pinSet (displayName: string) : string * string =
        "GenPRES: your PIN was set",
        $"Hello {displayName},\n\nA PIN was set for your GenPRES account just now. If that was not you, tell your administrator."


    /// The third wrong PIN ended a Session and locked signing.
    let pinLimit (displayName: string) : string * string =
        "GenPRES: signing is locked",
        $"Hello {displayName},\n\nThe PIN was entered wrong three times at a signature just now. Your session was ended and signing is locked for a while. If that was not you, tell your administrator."


/// The session lifecycle, from the presented Launch to the signed version: the LaunchRecord
/// keyed by the nonce, the redirect to the IdentityProvider, the callback with its ladder of
/// checks (identity, Role, active Patient, PIN), a reloaded callback answered as the first
/// time, the open as one act that closes the User's other Sessions, the launch suspended at
/// the PIN question and the enrolment that lifts it, the credential with its wrong-PIN lock,
/// the signing challenge and the commit of a version, and what a Session is told when the
/// record moved on. Pure over a State record: the clock, the ids, the codes and the ports
/// are parameters. StubDatabase.makeSessionPort runs it over an in-memory store.
module Session =

    /// The refusal words of #/session?refused=<word>; the client's parseRefusal reads them.
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


    /// A Session as the store holds it: what it is open on, the login it belongs to (a User
    /// has at most one open Session), the id of the version it opened with (None from
    /// nothing, or from a head that cannot be read), which a Submission is checked against,
    /// when it opened, the date its patient data is projected at, and when it was last seen
    /// (nothing acts on it yet; the idle and absolute lifetimes of Rule 10 are not built).
    type SessionRecord =
        {
            Opened: OpenedSession
            Login: string option
            OpenedWith: string option
            OpenedAt: DateTime
            Seen: DateTime
        }


    /// A confirmation code as the store keeps it, as a mac and never as the digits: one per
    /// credential, with the address it went to, its expiry and the wrong tries so far.
    type PendingCode =
        {
            UserId: string
            MailAddress: string
            CodeMac: byte[]
            Expiry: DateTime
            Tries: int
        }


    /// One launch suspended at the PIN question: what the open needs once the PIN is set, and the public key
    /// of the browser that made it, so the Session opens on the key the supplying browser holds.
    /// No lifetime of its own: it lives as long as the code it is bound to.
    type Enrolment =
        {
            Attempt: string
            UserId: string
            Login: string
            DisplayName: string
            PatientId: string
            PublicKey: PublicKey
        }


    /// A data notice as the store holds it: one per Session, the EHR data it was told over as
    /// read and its projection (none: unreadable), for two minutes.
    type Notice =
        {
            Nonce: string
            /// the EHR data as read when the notice was told; none when it could not be read
            Ehr: GenForm.EhrPatientData option
            /// its projection, what the client is shown
            Data: GenForm.Patient option
            Expiry: DateTime
        }


    /// A signing challenge as the store holds it: one per Session, the digest of exactly the
    /// plan shown over the store's canonical form, the EHR data as read when it was issued and
    /// its projection at the date of the open, for two minutes. The plan itself is never held:
    /// the submission carries it and is compared by digest.
    type Challenge =
        {
            Nonce: string
            Digest: string
            /// the EHR data as read at the challenge; none when it could not be read
            Ehr: GenForm.EhrPatientData option
            /// its projection at the date of the open, the patient the version is signed on
            Reading: GenForm.Patient option
            Expiry: DateTime
        }


    /// What a challenge compares: the EHR data as read, never its projection, whose age moves
    /// with the clock.
    module Reads =

        /// Whether a fresh read tells a change: none, or other than the EHR data the Session
        /// opened on. A Session that opened on none is told on every read that answers.
        let changed (opened: GenForm.EhrPatientData option) (current: GenForm.EhrPatientData option) =
            current.IsNone || current <> opened


        /// The notice the token names, when the Session has one and it was told over exactly
        /// the read at hand; a read that moved since is a fresh notice.
        let accepted
            (notices: Map<string, Notice>)
            (sid: string)
            (token: string option)
            (current: GenForm.EhrPatientData option)
            =
            token
            |> Option.bind (fun token ->
                notices
                |> Map.tryFind sid
                |> Option.filter (fun n -> n.Nonce = token && n.Ehr = current)
            )


        /// The EHR data a Session holds after a commit: the read the challenge was issued
        /// over, so that the next challenge compares against the data just signed on; what it
        /// opened on when the challenge had none.
        let afterCommit (challenge: Challenge) (opened: GenForm.EhrPatientData option) =
            challenge.Ehr |> Option.orElse opened


    /// What the adapter's write came to: landed; refused by the store because another server
    /// signed first, with the version that won; or failed.
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


    /// How a store records the end of a Session; a supersession is no row, since a newer
    /// Session of the same login tells it.
    [<RequireQualifiedAccess>]
    type StoredEnding =
        | Closed
        | Ended of SessionEnding


    /// A write the machine asks for, as a value: one case per fact the store records. A member
    /// returns the writes of its request next to the new state and its answer.
    type Persist =
        /// the order plan version of a commit; the Launch and what its callback came to
        | WriteVersion of GenOrder.OrderPlanVersion
        | RecordLaunch of LaunchRecord
        | RecordLaunchOutcome of nonce: string * LaunchResult * at: DateTime
        /// the Session opened, what it opened with (at the open, at a version opened and at a
        /// commit), and a request from it
        | OpenSession of sessionId: string * SessionRecord
        | RecordOpenedWith of sessionId: string * SessionRecord * at: DateTime
        | RecordSeen of sessionId: string * at: DateTime
        /// the end of a Session that is an act, and the acknowledgement of an ending
        | EndSession of sessionId: string * StoredEnding * at: DateTime
        | AcknowledgeEnding of sessionId: string * at: DateTime
        /// the credential as it stands after the event that changed it: the PIN set, a wrong
        /// entry counted, the lock reached, a right entry clearing the count
        | WriteCredential of userId: string * event: string * Credential * at: DateTime
        /// the confirmation code mailed when a launch suspends, and what becomes of it
        | WriteCode of PendingCode * at: DateTime
        /// the code these name is the one the request read, named by its mac: another server
        /// may have mailed a newer one meanwhile, and a try of the older must not void it
        | CountCodeTry of userId: string * codeMac: byte[] * at: DateTime
        | SpendCode of userId: string * codeMac: byte[] * at: DateTime
        /// the launch suspended at the PIN question, the attempt given up, and every attempt of
        /// a person dropped at once when their PIN is set or their code is void
        | WriteEnrolment of Enrolment * at: DateTime
        | DropEnrolmentWrite of attempt: string * at: DateTime
        | DropEnrolmentsOf of userId: string * at: DateTime
        /// what a Session holds in flight: the notice it was told, the challenge it answers,
        /// the challenge a commit or an opened version used up, named by the nonce the request
        /// read, and what a Submission was answered so that the same one is answered once
        | WriteNotice of sessionId: string * Notice * at: DateTime
        | WriteChallenge of sessionId: string * Challenge * at: DateTime
        | SpendChallenge of sessionId: string * nonce: string * at: DateTime
        | RememberAnswer of sessionId: string * idemKey: string * SigningOutcome * at: DateTime


    type State =
        {
            Launches: Map<string, LaunchRecord>
            Sessions: Map<string, SessionRecord>
            Endings: Map<string, SessionEnding * DateTime>
            Credentials: Map<string, Credential>
            /// the live confirmation code per person
            Codes: Map<string, PendingCode>
            /// the launches suspended at the PIN question, by attempt
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


    /// How long a confirmation code lives: a mail round trip, not a Session's idle gap. Bounds
    /// the half-finished launch too, which lives as long as its code.
    let codeLifetime = TimeSpan.FromMinutes 15.0

    /// Wrong codes before the code is void.
    let maxTries = 3

    /// How long a challenge lives: the Launch's two minutes, the time to read the modal and
    /// enter a PIN, so what is signed was checked against the platform moments ago.
    let challengeLifetime = TimeSpan.FromMinutes 2.0


    let credentialOf (userId: string) (state: State) =
        state.Credentials |> Map.tryFind userId |> Option.defaultValue Credential.empty


    /// The newest version of a patient's record, readable or not: what a Session starts from
    /// and what a sign is checked against.
    let headOf (patientId: string) (state: State) =
        state.Records |> Map.tryFind patientId |> Option.bind List.tryHead


    let private dropExpired (now: DateTime) (state: State) =
        let codes = state.Codes |> Map.filter (fun _ c -> now <= c.Expiry)

        { state with
            Launches = state.Launches |> Map.filter (fun _ r -> now <= r.Expiry)
            Codes = codes
            // an attempt lives as long as its code
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


    /// The patient data a Session opens on: the EHR's data, the source of truth, projected at
    /// the date of the open, when there is any (the adapter answers none for a reading that is
    /// no patient); without any, the patient data the head of the record was signed on, the
    /// last seen, when the head can be read; from nothing, none, so that the User enters it
    /// and a data outage does not block prescribing.
    let sessionPatient
        (now: DateTime)
        (patientData: PatientDataPort)
        (ehr: GenForm.EhrPatientData option)
        (head: StoredVersion option)
        : GenForm.Patient option
        =
        ehr
        |> Option.map (patientData.patient now)
        |> Option.orElse (
            head
            |> Option.bind (fun h ->
                match h with
                | StoredVersion.Readable v -> Some v.Plan.Patient
                | StoredVersion.Unreadable _ -> None
            )
        )


    /// The open, one act, from whatever carried the launch this far: a LaunchRecord at the
    /// callback, an Enrolment once the PIN is set. The Session is written from the head of the
    /// record, held whatever its case, on the platform's reading, else the head's patient
    /// data; the login's other Sessions are closed and marked, so that a User has at most one
    /// open Session. A head this release cannot read is nothing to open with: the Session
    /// opens from nothing, so the next request tells that the record has a newer version, and
    /// a sign is refused against it.
    let private openWith
        (now: DateTime)
        (newId: unit -> string)
        (patientData: PatientDataPort)
        (patientId: string)
        (key: PublicKey)
        (user: UserContext)
        (state: State)
        =
        let id = newId ()
        let head = headOf patientId state
        let ehr = patientData.read patientId

        let opened: OpenedSession =
            {
                User = Some user
                PatientId = Some patientId
                EhrData = ehr
                Patient = sessionPatient now patientData ehr head
                OpenedToken = Some(OpenedToken $"opened-{id}")
                KeyThumbprint = Some(PublicKey.thumbprint key)
                Head = head
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
        // the superseded Sessions need no write: the newer Session of their login tells it
        [ OpenSession(id, session); RecordOpenedWith(id, session, now) ]


    let private recordOutcome now (record: LaunchRecord) outcome (state: State) =
        { state with Launches = state.Launches |> Map.add record.Nonce { record with Outcome = Some outcome } },
        RecordLaunchOutcome(record.Nonce, outcome, now)


    let private openSession now newId patientData (record: LaunchRecord) (standing: UserStanding) (state: State) =
        let state, (id, session), writes =
            openWith now newId patientData record.PatientId record.PublicKey standing.User state

        let state, outcome = recordOutcome now record (LaunchResult.Opened(id, session)) state

        state, CallbackResult.Opened(id, openedUrl), writes @ [ outcome ]


    let private refuse now (record: LaunchRecord) refusal (state: State) =
        let state, outcome = recordOutcome now record (LaunchResult.Refused refusal) state
        state, CallbackResult.Refused(refusal, refusedUrl refusal), [ outcome ]


    /// The launch suspends at the PIN question. One live code per credential: a code that
    /// still stands is reused and nothing is mailed; else a fresh code is mailed to the
    /// address the registry gave on this request. The attempt is this launch's own, with its
    /// browser's key.
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
            // a code already stands for this person: no second mail, no second row
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


    /// What the first half of a callback came to: an answer with its writes, or the identity
    /// and the standing a launch goes on to the open with.
    [<RequireQualifiedAccess>]
    type Redeemed =
        | Answered of State * CallbackResult * Persist list
        | Identified of LaunchRecord * BrowserIdentity * UserStanding


    /// <summary>
    /// The first half of the callback: the state against the cookie, a reloaded callback
    /// answered from the launch's outcome, else the code redeemed once for the identity and
    /// the registry asked once for the Role and the active Patient. Needs the launch record
    /// and the Session its outcome names, nothing keyed by the login: a store loads those
    /// first, and the login's rows after, for <c>openAfterRedeem</c>.
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
    /// The second half of the callback, over the login's Sessions, the credential and the
    /// patient's record: the active Patient checked, the launch suspended at the PIN question
    /// when a Prescriber's credential has no PIN, else the open.
    /// </summary>
    let openAfterRedeem
        (now: DateTime)
        (newId: unit -> string)
        (newCode: unit -> string)
        (codeMac: string -> byte[])
        (patientData: PatientDataPort)
        (send: Mail -> unit)
        (record: LaunchRecord, identity: BrowserIdentity, standing: UserStanding)
        (state: State)
        : State * CallbackResult * Persist list
        =
        let state = dropExpired now state

        if standing.ActivePatientId <> Some record.PatientId then
            refuse now record LaunchRefusal.WrongActivePatient state
        elif
            standing.User.Role = UserRole.Prescriber
            && not (credentialOf standing.User.UserId state |> Credential.pinSet)
        then
            suspend now newId newCode codeMac send record identity standing state
        else
            openSession now newId patientData record standing state


    /// The callback from the IdentityProvider and the checks that follow it, the two halves
    /// over one state: the state against the cookie, the code redeemed for the identity, the
    /// registry asked for the Role and the active Patient, the credential read. A Prescriber
    /// whose credential has no PIN is not refused: the launch suspends until the PIN is set. A
    /// callback reload while the attempt stands is answered with it again; once it is gone, a
    /// relaunch is asked for.
    let callback
        (now: DateTime)
        (newId: unit -> string)
        (newCode: unit -> string)
        (codeMac: string -> byte[])
        (redeemCode: string -> BrowserIdentity option)
        (standing: BrowserIdentity -> UserStanding option)
        (patientData: PatientDataPort)
        (send: Mail -> unit)
        (state: State)
        (cb: Callback)
        : State * CallbackResult * Persist list
        =
        match redeem now redeemCode standing state cb with
        | Redeemed.Answered(state, result, writes) -> state, result, writes
        | Redeemed.Identified(record, identity, standing) ->
            openAfterRedeem now newId newCode codeMac patientData send (record, identity, standing) state


    /// What a browser holding an attempt is told at GetSession: whom the launch is for and where
    /// the code went, while the attempt (that is, its code) stands; nothing once it is gone.
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


    /// The code and every attempt bound to it, gone (the PIN was set, the code is void, or
    /// the browser gave up).
    let private dropCode (now: DateTime) (userId: string) (state: State) =
        // the code as this request read it; there is none to spend when it has already gone
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


    /// The browser gave up on its attempt (CloseSession while enrolling). The code stands for
    /// any other attempt bound to it; when this was the last one it goes too, so that the next
    /// launch mails a fresh code.
    let dropEnrolment (now: DateTime) (attempt: string) (state: State) : State * Persist list =
        match state.Enrolments |> Map.tryFind attempt with
        | None -> state, []
        | Some e ->
            let state = { state with Enrolments = state.Enrolments |> Map.remove attempt }
            let dropped = DropEnrolmentWrite(attempt, now)

            if state.Enrolments |> Map.exists (fun _ o -> o.UserId = e.UserId) then
                // another browser is still enrolling on this code: it stands for that one
                state, [ dropped ]
            else
                let state, writes = dropCode now e.UserId state
                state, dropped :: writes


    /// The PIN comes back with the code. In order: the attempt (and its code) must stand; the
    /// PIN must have the format, else no try is spent; a wrong code counts, and the third voids
    /// the code for every attempt; else one act: the PIN is set with a count of zero, the code
    /// and its attempts are dropped, the User is told (at the address the registry answers
    /// now, else the one the code went to), and the launch continues to the open on the
    /// supplying attempt's key, with the Role the registry answers now and only if the
    /// launch's Patient is still the active one.
    let supplyPin
        (now: DateTime)
        (newId: unit -> string)
        (newSalt: int -> byte[])
        (codeMac: string -> byte[])
        (standing: BrowserIdentity -> UserStanding option)
        (patientData: PatientDataPort)
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
                    // the third wrong code voids it for every attempt bound to it
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

                // the registry asked again, fresh: the address for the mail, the Role re-taken
                // and the active Patient, because the registry may have moved on during the
                // wait. When it cannot answer, the code has already proved the mailbox, so it
                // settles the PIN and the launch continues on what it had.
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
                | Some s when s.ActivePatientId <> Some e.PatientId ->
                    // the PIN is set and told; no Session opens for a Patient that is no longer
                    // the active one: a relaunch is asked for. The PIN stands all the same, so
                    // its writes go whatever the launch comes to
                    state, SupplyPinResult.Refused PinRefusal.WrongActivePatient, settled
                | _ ->
                    let state, (id, session), writes = openWith now newId patientData e.PatientId e.PublicKey user state

                    state, SupplyPinResult.Opened(id, session), settled @ writes


    /// A request from the Session refreshes its idle clock. Applied by every member that takes
    /// the session cookie's id, close excepted: a close ends the Session, it does not keep
    /// it alive. Nothing to refresh when there is no such Session.
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


    /// The User closes the Session, or acknowledges its ending: an open Session ends as closed
    /// and is acknowledged at once, an ended one is acknowledged; neither is told again.
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


    /// The head of the record, when it is not the version the Session opened with: a
    /// Submission is refused as long as such a newer version exists.
    let blockedBy (record: SessionRecord) (patientId: string) (state: State) =
        match headOf patientId state with
        | Some head when Some(StoredVersion.id head) <> record.OpenedWith -> Some(StoredVersion.head head)
        | _ -> None


    /// The head is a row this release cannot read: a sign is refused against it whatever base
    /// the client names, since a sign is only ever accepted against the head. Named by its
    /// identity, as a version that blocks.
    let unreadableHead (patientId: string) (state: State) =
        match headOf patientId state with
        | Some(StoredVersion.Unreadable _ as head) -> Some(StoredVersion.head head)
        | _ -> None


    /// The age a Session holds for an identified patient, as the contract carries it: the age
    /// in days of the patient it opened on, split as the contract splits a number of days.
    /// None without a Session, in a Session whose EHR data names no patient, or when its
    /// patient has no age. No clock: the age is the one the Session opened on, up to and
    /// including the next sign.
    let age (sid: string) (state: State) : Age option =
        state.Sessions
        |> Map.tryFind sid
        |> Option.filter (fun r ->
            r.Opened.EhrData
            |> Option.bind Informedica.GenForm.Lib.EhrPatientData.identity
            |> Option.isSome
        )
        |> Option.bind _.Opened.Patient
        |> Option.bind (Informedica.GenForm.Lib.Patient.Dto.toDto >> ServerApi.Patient.toModel >> _.Age)


    /// For every computing request that names a Session: no Session under this id and an
    /// ending recorded for it, the ending; a Session, touched, and, when the token is the
    /// Session's own, the head compared against the version it opened with: a newer version,
    /// whose and when. The notice informs and gates nothing; the refusal at a Submission stays
    /// the only guard. An anonymous Session, one without a Patient, no head, or a token that
    /// is not the Session's: nothing to say. Beside the notice, the age the Session holds for
    /// an identified patient, which every patient the request carries is put at.
    let seen
        (now: DateTime)
        (sid: string)
        (opened: OpenedToken option)
        (state: State)
        : State * (RecordNotice option * Age option) * Persist list
        =
        match state.Sessions |> Map.tryFind sid with
        | None -> state, (state.Endings |> Map.tryFind sid |> Option.map (fst >> RecordNotice.Ended), None), []
        | Some record ->
            let state, writes = touch now sid state

            let told =
                match record.Opened.User, record.Opened.PatientId with
                | Some _, Some patientId when opened.IsSome && opened = record.Opened.OpenedToken ->
                    blockedBy record patientId state |> Option.map RecordNotice.NewerVersion
                | _ -> None

            state, (told, age sid state), writes


    /// Version id becomes what the Session opened with. No Session, an anonymous one or one
    /// without a Patient: nothing to open. An id the record does not hold for the Session's
    /// Patient (a stale button, a restart), and a version the release cannot read: nothing
    /// opens, the Session as it is; the next request tells what the head is. The version
    /// already open: the token stands. Another version: the OpenedToken is re-minted over it
    /// and the standing challenge and notice of this Session are dropped (a challenge over the
    /// old baseline must not be answerable). Any readable version may be opened; one that is
    /// not the head leaves Submission blocked.
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
                            | StoredVersion.Readable v when v.Id = id -> Some v
                            | _ -> None
                        )
                    )

                match version with
                | None -> state, Some record.Opened, seen
                | Some version when record.OpenedWith = Some id ->
                    let opened = { record.Opened with Head = Some(StoredVersion.Readable version) }
                    let session = { state.Sessions[sid] with Opened = opened }

                    { state with Sessions = state.Sessions |> Map.add sid session },
                    Some opened,
                    seen @ [ RecordOpenedWith(sid, session, now) ]
                | Some version ->
                    let opened =
                        { record.Opened with
                            OpenedToken = Some(OpenedToken $"opened-{newId ()}")
                            Head = Some(StoredVersion.Readable version)
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


    /// An order appears once in a plan.
    let duplicateOrders (plan: GenOrder.OrderPlan) =
        plan
        |> Informedica.GenOrder.Lib.OrderPlan.orders
        |> Array.countBy _.Order.Id
        |> Array.exists (fun (_, n) -> n > 1)


    /// The challenge request, checked in order: the Session with a User and a Patient; the
    /// Role Prescriber; the OpenedToken this Session holds; the patient data re-read: when it
    /// is not what the Session opened with and no notice over this reading was accepted, no
    /// challenge yet but a data notice, replacing any earlier notice and dropping any earlier
    /// challenge (it was over the data before the change); the head readable and not moved
    /// on. Then a challenge over the digest of exactly this plan, replacing the Session's
    /// earlier one and spending the notice. The plan is the domain's, parsed at the boundary;
    /// its own patient data is what the User saw, entered or read, and is recorded as such;
    /// the Patient is the Session's, never the request's. The PIN is not involved: a refusal
    /// here costs no attempt.
    let challenge
        (now: DateTime)
        (newId: unit -> string)
        (digest: GenOrder.OrderPlan -> string)
        (patientData: PatientDataPort)
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
                    // read again and compared as read, so that only the EHR data tells a change;
                    // projected at the date of the open, so that the age the Session opened on
                    // holds; the adapter answers none for a reading that is no patient
                    let currentEhr = patientData.read patientId
                    let current = currentEhr |> Option.map (patientData.patient record.OpenedAt)
                    let accepted = Reads.accepted state.Notices sid notice currentEhr

                    // no reading, or another than the Session opened on: told before the challenge
                    if Reads.changed record.Opened.EhrData currentEhr && accepted.IsNone then
                        let nonce = newId ()

                        let notice =
                            {
                                Nonce = nonce
                                Ehr = currentEhr
                                Data = current
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

                            let challenge =
                                {
                                    Nonce = nonce
                                    Digest = digest plan
                                    Ehr = currentEhr
                                    Reading = current
                                    Expiry = now + challengeLifetime
                                }

                            { state with
                                Notices = state.Notices |> Map.remove sid
                                Challenges = state.Challenges |> Map.add sid challenge
                            },
                            SigningOutcome.ChallengeIssued nonce,
                            seen @ [ WriteChallenge(sid, challenge, now) ]


    /// The commit of a signature, one act, checked in order: the Session with a User and a
    /// Patient; the answer already given to this Session's key; the Role re-taken from the
    /// registry (fails closed when it cannot answer; Rule 38's bounded grace is not built);
    /// the OpenedToken this Session holds; the head readable and not moved on; the challenge
    /// this Session was issued, over exactly this plan by digest; and last the PIN, so that a
    /// Submission that was never going to land costs no attempt. A pure function: on an
    /// accepted sign the returned state already holds the new head, with the challenge spent,
    /// the OpenedToken re-minted over it and the Session's patient set to the reading at the
    /// challenge, else the data signed, and the third value is the write for the adapter to
    /// run; on a refusal it is none. The answer is remembered under the key, refusals too.
    /// Three wrong PINs end the Session (WrongPinLimit), lock signing and mail the User; a
    /// wrong PIN while locked pushes the lock out; a right PIN while locked is refused and
    /// counts nothing.
    let commit
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
                    // the answer is remembered from here on, under this Session and the key, so
                    // that the same Submission sent again is answered once and the same way
                    let remember (state: State) answer writes =
                        { state with Answered = state.Answered |> Map.add (sid, signature.IdemKey) (answer, now) },
                        answer,
                        seen @ writes @ [ RememberAnswer(sid, signature.IdemKey, answer, now) ]

                    let refuse refusal = remember state (SigningOutcome.Refused refusal) []

                    let identity =
                        {
                            Login = record.Login |> Option.defaultValue user.UserId
                            DisplayName = user.DisplayName
                        }

                    match standing identity with
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

                                    // the credential moved whatever the PIN was: a right entry
                                    // clears the count, a wrong one raises it, the third locks
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

                                        let version: GenOrder.OrderPlanVersion =
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
                                                Verified = challenge.Reading.IsSome
                                            }

                                        let token = OpenedToken $"opened-{newId ()}"

                                        // the Session's patient is the EHR's reading at the challenge,
                                        // else the data just signed, so a resume shows what a relaunch
                                        // would; the EHR data is the read just signed on
                                        let opened =
                                            { record with
                                                Opened =
                                                    { record.Opened with
                                                        OpenedToken = Some token
                                                        Head = Some(StoredVersion.Readable version)
                                                        EhrData = Reads.afterCommit challenge record.Opened.EhrData
                                                        Patient =
                                                            challenge.Reading
                                                            |> Option.defaultValue version.Plan.Patient
                                                            |> Some
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
                                                                StoredVersion.Readable version
                                                                :: (versions |> Option.defaultValue [])
                                                            )
                                                        )
                                                Challenges = state.Challenges |> Map.remove sid
                                                Sessions = state.Sessions |> Map.add sid opened
                                            }
                                            (SigningOutcome.Submitted(version, token))
                                            [
                                                credentialWrite
                                                WriteVersion version
                                                RecordOpenedWith(sid, opened, now)
                                                // the challenge this signature answered, used up
                                                SpendChallenge(sid, challenge.Nonce, now)
                                            ]
                                    elif wasLocked then
                                        // this Session did nothing wrong; the lock is the credential's
                                        remember
                                            state
                                            (SigningOutcome.Refused(SigningRefusal.Locked credential.LockedUntil.Value))
                                            [ credentialWrite ]
                                    elif credential |> Credential.attemptsLeft = 0 then
                                        // the wrong-PIN limit is reached now; the Session ends
                                        let subject, body = Mails.pinLimit user.DisplayName

                                        // best effort (MailPort: fire and forget): the ending and the lock
                                        // land whatever the mail does
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
