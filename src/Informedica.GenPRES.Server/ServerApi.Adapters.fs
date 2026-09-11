namespace ServerApi

open System
open Shared.Types


module PublicKey =

    open System.Text
    open System.Text.Json
    open System.Security.Cryptography


    let private base64Url (bytes: byte[]) =
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')


    let private sha256 (s: string) =
        s |> Encoding.UTF8.GetBytes |> SHA256.HashData


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
    let randomId () =
        RandomNumberGenerator.GetBytes 32 |> base64Url


module LaunchSeal =

    /// The key the Launch is sealed under. 32 bytes from a CSPRNG (`newKey`); shared with the
    /// LaunchScript in the real integration, made per host start for the stub.
    type Key = Key of byte[]


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


    let mac (Key key) (data: byte[]) =
        System.Security.Cryptography.HMACSHA256.HashData(key, data)


    let private json = System.Text.Json.JsonSerializerOptions()


    /// Seals the claims: `base64url(json) + "." + base64url(HMAC-SHA256(key, json))`.
    let mint (key: Key) (claims: Claims) : Launch =
        let payload =
            {
                pid = claims.PatientId
                nonce = claims.Nonce
                exp = DateTimeOffset(claims.Expiry, TimeSpan.Zero).ToUnixTimeSeconds()
            }

        let bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload, json)
        Launch $"{toBase64Url bytes}.{toBase64Url (mac key bytes)}"


    /// Verifies the seal (constant-time), then the lifetime (Rule 3). Anything that is not a
    /// Launch sealed under the key is `LaunchInvalid`; a Launch past its expiry is
    /// `LaunchExpired`.
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


    /// Derives the hash of a PIN under a fresh salt from `newSalt` (the CSPRNG in the host).
    let make (newSalt: int -> byte[]) (pin: string) : PinHash =
        let salt = newSalt saltLength

        {
            Salt = salt
            Hash = derive salt pin
        }


    /// Whether the PIN derives to the stored hash, compared in constant time.
    let verify (pin: string) (hash: PinHash) =
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(derive hash.Salt pin, hash.Hash)


/// Concept 7, the UserCredential: what the Database holds per person (keyed by UserId, not by
/// login). No PIN yet is a credential without one; the wrong-count is Rule 28's, counted across
/// Sessions and reset to zero when the PIN is set.
type Credential =
    {
        PinHash: PinHash option
        WrongCount: int
        // Rule 28: signing is locked until this moment; a delay, not a state
        LockedUntil: DateTime option
    }


module Credential =

    let empty =
        {
            PinHash = None
            WrongCount = 0
            LockedUntil = None
        }


    /// Rule 24: whether a PIN is set.
    let pinSet (credential: Credential) = credential.PinHash.IsSome


    /// A credential with this PIN, a count of zero and no lock (Rules 28, 37: setting the PIN
    /// resets both).
    let withPin (newSalt: int -> byte[]) (pin: string) : Credential =
        {
            PinHash = Some(PinHash.make newSalt pin)
            WrongCount = 0
            LockedUntil = None
        }


    /// Wrong PINs before the Session ends and signing locks (Rule 28).
    let wrongPinLimit = 3

    /// The first lock (plan 622: one minute; the model counts in ticks).
    let lockBase = TimeSpan.FromMinutes 1.0


    /// The longest lock. The model's delay only grows and decays with time; the decay is not
    /// built, so the stub caps the delay instead (review on #624: an unbounded doubling
    /// overflows the arithmetic long before it overflows anyone's patience).
    let lockMax = TimeSpan.FromHours 24.0


    /// Rule 28: the delay after `count` wrong entries. The entry that reaches the limit locks
    /// for `lockBase`; each one after it doubles that, up to `lockMax`.
    let lockFor (count: int) =
        // 2^11 minutes is already past a day; the bound keeps `pown` in range
        let doublings = min 11 (max 0 (count - wrongPinLimit))
        min lockMax (lockBase * float (pown 2 doublings))


    /// Rule 28: whether signing is locked at this moment.
    let isLocked (now: DateTime) (credential: Credential) =
        match credential.LockedUntil with
        | Some until -> now < until
        | None -> false


    /// Rules 23, 28: whether the PIN is accepted, and the credential as it stands after the
    /// entry. A right PIN while unlocked zeroes the count and clears the lock; a right PIN
    /// while locked is refused and counts nothing; a wrong PIN adds one and, at the limit or
    /// beyond it, locks for `lockFor` from now, so a wrong entry while locked pushes the
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


    /// Rule 28: the wrong entries left before the limit.
    let attemptsLeft (credential: Credential) =
        max 0 (wrongPinLimit - credential.WrongCount)


module Pin =

    /// Four to six digits. V8 names no format; this is the assumption plan 615 records.
    let isValid (pin: string) =
        not (isNull pin)
        && pin.Length >= 4
        && pin.Length <= 6
        && pin |> Seq.forall Char.IsAsciiDigit


module MailHint =

    /// `n***@stub.example`: enough for the User to know which mailbox, not enough for a
    /// shoulder to read the address.
    let ofAddress (address: string) =
        match address.IndexOf '@' with
        | i when i > 0 -> $"{address[0]}***{address.Substring i}"
        | _ -> "***"


/// The two mails of UC-2 (Rule 27). English only; the mail language is a later concern.
module Mails =

    let confirmationCode (displayName: string) (code: string) (minutes: int) : string * string =
        "GenPRES: your confirmation code",
        $"Hello {displayName},\n\nYour confirmation code is {code}. It is valid for {minutes} minutes. Enter it in GenPRES together with the PIN of your choice.\n\nIf you did not open GenPRES just now, somebody tried to enrol in your name; nothing was set."


    let pinSet (displayName: string) : string * string =
        "GenPRES: your PIN was set",
        $"Hello {displayName},\n\nA PIN was set for your GenPRES account just now. If that was not you, tell your administrator."


    /// Rule 27: the third wrong PIN ended a Session and locked signing.
    let pinLimit (displayName: string) : string * string =
        "GenPRES: signing is locked",
        $"Hello {displayName},\n\nThe PIN was entered wrong three times at a signature just now. Your session was ended and signing is locked for a while. If that was not you, tell your administrator."


/// Launch steps 4 and 5 over server-hosted stubs (plan 605): the LaunchRecord keyed by the
/// nonce (4.2), the redirect to the IdentityProvider, the callback (4.5) with the step-5 ladder,
/// the Rule 45 replay and the Rule 40 single act with the Rule 8 closes; the credential half of
/// the Database and the suspended launch of UC-2 (plan 615). Pure over a state record;
/// `makeSessionPort` wraps it in a lock over the actor ports.
module Hop =

    /// The refusal words of `#/session?refused=<word>`; the client's `parseRefusal` reads them.
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

    let refusedUrl refusal =
        $"/#/session?refused={refusalWord refusal}"


    /// A Session as the store holds it: what the client learns, the login it belongs to
    /// (Rule 8: a User has at most one open Session), the head of the record it opened with
    /// (Rule 19; `None` from nothing), which a Submission is checked against (Rule 20), and
    /// when it was last seen (Rule 9; nothing acts on it yet, Rule 10's lifetimes are not built).
    type SessionRecord =
        {
            Session: SessionOpened
            Login: string option
            OpenedWith: string option
            Seen: DateTime
        }


    /// A confirmation code as the Database keeps it (Rule 37, "the code as a mac"): one per
    /// credential, with the address it went to, its expiry and the wrong tries so far.
    type PendingCode =
        {
            UserId: string
            MailAddress: string
            CodeMac: byte[]
            Expiry: DateTime
            Tries: int
        }


    /// One suspended launch (UC-2): what the open needs once the PIN is set, and the public key
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


    /// A data notice as the store holds it (Rule 44): one per Session, the platform's reading
    /// it was told over (`None`: unreadable), for two minutes.
    type Notice =
        {
            Nonce: string
            Data: Patient option
            Expiry: DateTime
        }


    /// A signing challenge as the store holds it (Concept 17): one per Session, over exactly
    /// the patient data and the orders shown (Rule 43), whether that data was the platform's
    /// reading when it was issued (Rule 44), for two minutes.
    type Challenge =
        {
            Nonce: string
            Patient: Patient
            Scenarios: OrderScenario[]
            Verified: bool
            Expiry: DateTime
        }


    type LaunchRecord =
        {
            Nonce: string
            State: string
            PatientId: string
            PublicKey: PublicKey
            Expiry: DateTime
            Outcome: LaunchResult option
        }


    type State =
        {
            Launches: Map<string, LaunchRecord>
            Sessions: Map<string, SessionRecord>
            Endings: Map<string, SessionEnding * DateTime>
            Credentials: Map<string, Credential>
            // UC-2: the live confirmation code per person
            Codes: Map<string, PendingCode>
            // UC-2: the suspended launches by attempt
            Enrolments: Map<string, Enrolment>
            // UC-3: the signed versions of each patient's order plan, newest first
            Records: Map<string, SignedOrderPlan list>
            // UC-3: the live data notice per Session
            Notices: Map<string, Notice>
            // UC-3: the live challenge per Session
            Challenges: Map<string, Challenge>
            // UC-3, Rule 45: what a Submission was answered, by Session and by the client's key
            Answered: Map<string * string, SigningResponse * DateTime>
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


    let initialState (credentials: Map<string, Credential>) =
        { emptyState with Credentials = credentials }


    /// How long a confirmation code lives: a mail round trip, not Rule 30's gap. Bounds the
    /// half-finished launch too (plan 615's deviation from the model).
    let codeLifetime = TimeSpan.FromMinutes 15.0

    /// Wrong codes before the code is void (ext 2b).
    let maxTries = 3

    /// How long a challenge lives: the launch's two minutes, the time to read the modal and
    /// enter a PIN, so what is signed was checked against the platform moments ago (Rule 44).
    let challengeLifetime = TimeSpan.FromMinutes 2.0


    let credentialOf (userId: string) (state: State) =
        state.Credentials |> Map.tryFind userId |> Option.defaultValue Credential.empty


    /// Rule 19: the most recent signed version of a patient's record, if any.
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
        : State * LaunchResult
        =
        let state = dropExpired now state

        match verify launch with
        | Error refusal -> state, LaunchResult.Refused refusal
        | Ok claims ->
            match state.Launches |> Map.tryFind claims.Nonce with
            | Some record when record.PublicKey = key -> state, answerOf authorizeUrl record
            | Some _ -> state, LaunchResult.Refused LaunchRefusal.LaunchSpent
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

                { state with Launches = state.Launches |> Map.add claims.Nonce record }, answerOf authorizeUrl record


    /// Step 5.7, one act (Rule 40), from whatever carried the launch this far: a LaunchRecord
    /// at the callback, an Enrolment once the PIN is set. The Session is written from the head
    /// of the record (Rule 19), the login's other Sessions are closed and marked (Rule 8).
    let private openWith
        (now: DateTime)
        (newId: unit -> string)
        (patientData: string -> Patient option)
        (patientId: string)
        (key: PublicKey)
        (user: UserContext)
        (state: State)
        =
        let id = newId ()
        let head = headOf patientId state

        let session =
            {
                User = Some user
                PatientContext =
                    Some
                        {
                            PatientId = patientId
                            Patient = patientData patientId |> Option.defaultValue Shared.Models.Patient.empty
                        }
                OpenedToken = Some(OpenedToken $"opened-{id}")
                KeyThumbprint = Some(PublicKey.thumbprint key)
                Head = head
            }

        let login = Some user.UserId

        let superseded =
            state.Sessions
            |> Map.filter (fun sid s -> sid <> id && s.Login = login)
            |> Map.toList
            |> List.map fst

        { state with
            Sessions =
                superseded
                |> List.fold (fun m sid -> Map.remove sid m) state.Sessions
                |> Map.add
                    id
                    {
                        Session = session
                        Login = login
                        OpenedWith = head |> Option.map _.Head.Id
                        Seen = now
                    }
            Endings =
                superseded
                |> List.fold (fun m sid -> Map.add sid (SessionEnding.SupersededByLaunch, now) m) state.Endings
        },
        (id, session)


    let private recordOutcome (record: LaunchRecord) outcome (state: State) =
        { state with Launches = state.Launches |> Map.add record.Nonce { record with Outcome = Some outcome } }


    let private openSession now newId patientData (record: LaunchRecord) (standing: UserStanding) (state: State) =
        let state, (id, session) =
            openWith now newId patientData record.PatientId record.PublicKey standing.User state

        recordOutcome record (LaunchResult.Opened(id, session)) state, CallbackResult.Opened(id, openedUrl)


    let private refuse (record: LaunchRecord) refusal (state: State) =
        recordOutcome record (LaunchResult.Refused refusal) state, CallbackResult.Refused(refusal, refusedUrl refusal)


    /// UC-2: the launch suspends at the PIN question. One live code per credential (Rule 37,
    /// ext 2a): a code that still stands is reused and nothing is mailed; else a fresh code is
    /// mailed to the address the registry gave on this request (Rule 27). The attempt is this
    /// launch's own, with its browser's key.
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

        let state =
            match state.Codes |> Map.tryFind userId with
            | Some _ -> state
            | None ->
                let code = newCode ()

                let subject, body =
                    Mails.confirmationCode identity.DisplayName code (int codeLifetime.TotalMinutes)

                send
                    {
                        To = standing.MailAddress
                        Subject = subject
                        Body = body
                    }

                { state with
                    Codes =
                        state.Codes
                        |> Map.add
                            userId
                            {
                                UserId = userId
                                MailAddress = standing.MailAddress
                                CodeMac = codeMac code
                                Expiry = now + codeLifetime
                                Tries = 0
                            }
                }

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

        { state with Enrolments = state.Enrolments |> Map.add attempt enrolment }
        |> recordOutcome record (LaunchResult.Enrolling attempt),
        CallbackResult.Enrolling(attempt, openedUrl, until)


    /// Step 4.5 and step 5. As before, except at 5.4: a Prescriber whose credential has no PIN
    /// is not refused, the launch suspends (Rules 7, 25). A callback reload while the attempt
    /// stands is answered with it again (Rule 45); once it is gone, a relaunch is asked for.
    let callback
        (now: DateTime)
        (newId: unit -> string)
        (newCode: unit -> string)
        (codeMac: string -> byte[])
        (redeem: string -> BrowserIdentity option)
        (standing: BrowserIdentity -> UserStanding option)
        (patientData: string -> Patient option)
        (send: Mail -> unit)
        (state: State)
        (cb: Callback)
        : State * CallbackResult
        =
        let state = dropExpired now state

        let byState =
            state.Launches
            |> Map.toSeq
            |> Seq.map snd
            |> Seq.tryFind (fun r -> r.State = cb.State)

        let invalid =
            CallbackResult.Refused(LaunchRefusal.LaunchInvalid, refusedUrl LaunchRefusal.LaunchInvalid)

        match cb.StateCookie, byState with
        | Some cookie, Some record when cookie = cb.State && cb.State <> "" ->
            match record.Outcome with
            | Some(LaunchResult.Opened(id, _)) when state.Sessions |> Map.containsKey id ->
                state, CallbackResult.Opened(id, openedUrl)
            | Some(LaunchResult.Opened _) -> state, CallbackResult.Superseded openedUrl
            | Some(LaunchResult.Refused refusal) -> state, CallbackResult.Refused(refusal, refusedUrl refusal)
            | Some(LaunchResult.Enrolling attempt) ->
                match state.Enrolments |> Map.tryFind attempt with
                | Some e -> state, CallbackResult.Enrolling(attempt, openedUrl, state.Codes[e.UserId].Expiry)
                | None ->
                    state,
                    CallbackResult.Refused(LaunchRefusal.EnrolmentRequired, refusedUrl LaunchRefusal.EnrolmentRequired)
            | Some(LaunchResult.RedirectTo _)
            | None ->
                let identity =
                    match cb.Error, cb.Code with
                    | None, Some code -> redeem code
                    | _ -> None

                match identity with
                | None -> refuse record LaunchRefusal.NoBrowserIdentity state
                | Some identity ->
                    match standing identity with
                    | None -> refuse record LaunchRefusal.NoRole state
                    | Some standing when standing.ActivePatientId <> Some record.PatientId ->
                        refuse record LaunchRefusal.WrongActivePatient state
                    | Some standing when
                        standing.User.Role = UserRole.Prescriber
                        && not (credentialOf standing.User.UserId state |> Credential.pinSet)
                        ->
                        suspend now newId newCode codeMac send record identity standing state
                    | Some standing -> openSession now newId patientData record standing state
        | _, None -> state, invalid
        | _ -> state, invalid


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
    let private dropCode (userId: string) (state: State) =
        { state with
            Codes = state.Codes |> Map.remove userId
            Enrolments = state.Enrolments |> Map.filter (fun _ e -> e.UserId <> userId)
        }


    /// The browser gave up on its attempt (CloseSession while enrolling). The code stands for
    /// any other attempt bound to it; when this was the last one it goes too, so that the next
    /// launch mails a fresh code.
    let dropEnrolment (attempt: string) (state: State) : State =
        match state.Enrolments |> Map.tryFind attempt with
        | None -> state
        | Some e ->
            let state = { state with Enrolments = state.Enrolments |> Map.remove attempt }

            if state.Enrolments |> Map.exists (fun _ o -> o.UserId = e.UserId) then
                state
            else
                dropCode e.UserId state


    /// UC-2, the PIN comes back with the code. In order: the attempt (and its code) must stand;
    /// the PIN must have the format, else no try is spent; a wrong code counts, and the third
    /// voids the code for every attempt (ext 2b); else one act (Rules 37, 40): the PIN is set
    /// with a count of zero (Rule 28), the code and its attempts are dropped, the User is
    /// told (Rule 27, at the address the registry answers now, else the one the code went to),
    /// and the launch continues at 5.5 to 5.7 on the supplying attempt's key, with the Role the
    /// registry answers now and only if the launch's Patient is still the active one (Rule 6).
    let supplyPin
        (now: DateTime)
        (newId: unit -> string)
        (newSalt: int -> byte[])
        (codeMac: string -> byte[])
        (standing: BrowserIdentity -> UserStanding option)
        (patientData: string -> Patient option)
        (send: Mail -> unit)
        (attempt: string)
        (code: string)
        (pin: string)
        (state: State)
        : State * SupplyPinResult
        =
        let state = dropExpired now state

        match state.Enrolments |> Map.tryFind attempt with
        | None -> state, SupplyPinResult.Refused PinRefusal.AttemptExpired
        | Some e ->
            let pending = state.Codes[e.UserId]

            if not (Pin.isValid pin) then
                state, SupplyPinResult.Refused PinRefusal.PinFormat
            elif
                not (
                    System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(codeMac code, pending.CodeMac)
                )
            then
                let tries = pending.Tries + 1

                if tries >= maxTries then
                    dropCode e.UserId state, SupplyPinResult.Refused PinRefusal.CodeVoid
                else
                    { state with Codes = state.Codes |> Map.add e.UserId { pending with Tries = tries } },
                    SupplyPinResult.Refused(PinRefusal.WrongCode(maxTries - tries))
            else
                let identity =
                    {
                        Login = e.Login
                        DisplayName = e.DisplayName
                    }

                // 5.3 again, fresh: the address for the mail (Rule 27), the Role re-taken and
                // the active Patient (Rule 6), because the registry may have moved on during
                // the wait. When it cannot answer, the code settles the PIN (Rule 37, uc-02 last
                // bullet) and the launch continues on what it had.
                let fresh = standing identity

                let address =
                    fresh |> Option.map _.MailAddress |> Option.defaultValue pending.MailAddress

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

                let state =
                    { state with Credentials = state.Credentials |> Map.add e.UserId (Credential.withPin newSalt pin) }
                    |> dropCode e.UserId

                match fresh with
                | Some s when s.ActivePatientId <> Some e.PatientId ->
                    // the PIN is set and told; no Session opens for a Patient that is no longer
                    // the active one (Rule 6): a relaunch is asked for
                    state, SupplyPinResult.Refused PinRefusal.WrongActivePatient
                | _ ->
                    let state, (id, session) =
                        openWith now newId patientData e.PatientId e.PublicKey user state

                    state, SupplyPinResult.Opened(id, session)


    /// Rule 9: a request from the Session refreshes its idle clock. Applied by every member that
    /// takes the session cookie's id, `close` excepted (the model excepts `CloseSession`).
    /// Nothing to refresh when there is no such Session.
    let touch (now: DateTime) (sid: string) (state: State) : State =
        { state with Sessions = state.Sessions |> Map.change sid (Option.map (fun r -> { r with Seen = now })) }


    let find (now: DateTime) (id: string) (state: State) : State * SessionLookup =
        match state.Sessions |> Map.tryFind id with
        | Some record -> touch now id state, SessionLookup.Found record.Session
        | None ->
            match state.Endings |> Map.tryFind id with
            | Some(ending, _) -> state, SessionLookup.Ended ending
            | None -> state, SessionLookup.NotFound


    let close (id: string) (state: State) : State =
        { state with
            Sessions = state.Sessions |> Map.remove id
            Endings = state.Endings |> Map.remove id
        }


    /// Rule 20: the head of the record, when it is not the version the Session opened with.
    let blockedBy (record: SessionRecord) (patientId: string) (state: State) =
        match headOf patientId state with
        | Some head when Some head.Head.Id <> record.OpenedWith -> Some head.Head
        | _ -> None


    /// uc-03 step 1, for every computing request that names a Session: no Session under this
    /// id and an ending recorded for it, the ending (Rule 11); a Session, touched, and Rule 21's
    /// comparison when the token is the Session's own: a version newer than the one it opened
    /// with, whose and when (Rule 22: told, never enforced; Rule 20 stays the only guard). An
    /// anonymous Session, one without a Patient, no head, or a token that is not the Session's:
    /// nothing to say.
    let seen (now: DateTime) (sid: string) (opened: OpenedToken option) (state: State) : State * RecordNotice option =
        match state.Sessions |> Map.tryFind sid with
        | None -> state, state.Endings |> Map.tryFind sid |> Option.map (fst >> RecordNotice.Ended)
        | Some record ->
            let state = touch now sid state

            match record.Session.User, record.Session.PatientContext with
            | Some _, Some patient when opened.IsSome && opened = record.Session.OpenedToken ->
                state, blockedBy record patient.PatientId state |> Option.map RecordNotice.NewerVersion
            | _ -> state, None


    /// Rules 18 to 20, UC-4 step 4: version `id` becomes what the Session opened with. No
    /// Session, an anonymous one or one without a Patient: nothing to open (Rule 13). An id the
    /// record does not hold for the Session's Patient (a stale button, a restart): nothing
    /// opens, the Session as it is; the next request tells what the head is (Rule 21). The
    /// version already open: the token stands. Another version: the OpenedToken is re-minted
    /// over it (Rule 34) and the standing challenge and notice of this Session are dropped (a
    /// challenge over the old baseline must not be answerable). Any version may be opened
    /// (Rule 18); one that is not the head leaves Submission blocked (Rule 20).
    let openVersion
        (now: DateTime)
        (newId: unit -> string)
        (sid: string)
        (id: string)
        (state: State)
        : State * SessionOpened option
        =
        match state.Sessions |> Map.tryFind sid with
        | None -> state, None
        | Some record ->
            let state = touch now sid state

            match record.Session.User, record.Session.PatientContext with
            | None, _
            | _, None -> state, None
            | Some _, Some patient ->
                let version =
                    state.Records
                    |> Map.tryFind patient.PatientId
                    |> Option.bind (List.tryFind (fun v -> v.Head.Id = id))

                match version with
                | None -> state, Some record.Session
                | Some version when record.OpenedWith = Some id ->
                    let session = { record.Session with Head = Some version }

                    { state with Sessions = state.Sessions |> Map.add sid { record with Session = session } },
                    Some session
                | Some version ->
                    let session =
                        { record.Session with
                            OpenedToken = Some(OpenedToken $"opened-{newId ()}")
                            Head = Some version
                        }

                    { state with
                        Sessions =
                            state.Sessions
                            |> Map.add
                                sid
                                { record with
                                    Session = session
                                    OpenedWith = Some id
                                }
                        Challenges = state.Challenges |> Map.remove sid
                        Notices = state.Notices |> Map.remove sid
                    },
                    Some session


    /// Concept 10: an order appears once in a plan.
    let duplicateOrders (scenarios: OrderScenario[]) =
        scenarios |> Array.countBy _.Order.Id |> Array.exists (fun (_, n) -> n > 1)


    /// uc-03 step 2, in order: the Session with a User and a Patient; the Role Prescriber; the
    /// OpenedToken this Session holds (Rule 34); the patient data re-read (Rule 44): when it is
    /// not what the Session opened with and no notice over this reading was accepted, no
    /// challenge yet but a `DataNotice`, replacing any earlier notice and dropping any earlier
    /// challenge (it was over the data before the change); the record not moved on (Rule 20).
    /// Then a challenge over exactly this plan (Rule 43), replacing the Session's earlier one
    /// and spending the notice. The plan's own patient data is what the User saw, entered or
    /// read, and is recorded as such (Rule 44); the Patient is the Session's (Rule 33). The
    /// PIN is not involved: a refusal here costs no attempt (Rule 28).
    let challenge
        (now: DateTime)
        (newId: unit -> string)
        (patientData: string -> Patient option)
        (sid: string)
        (plan: OrderPlan, opened: OpenedToken, notice: string option)
        (state: State)
        : State * SigningResponse
        =
        let state = dropExpired now state |> touch now sid
        let refuse refusal = state, SigningResponse.Refused refusal

        match state.Sessions |> Map.tryFind sid with
        | None -> refuse SigningRefusal.NoSession
        | Some record ->
            match record.Session.User, record.Session.PatientContext with
            | None, _ -> refuse SigningRefusal.NotPrescriber
            | Some _, None -> refuse SigningRefusal.NoPatient
            | Some user, Some patient ->
                if user.Role <> UserRole.Prescriber then
                    refuse SigningRefusal.NotPrescriber
                elif record.Session.OpenedToken <> Some opened then
                    refuse SigningRefusal.StaleToken
                else
                    let current = patientData patient.PatientId

                    let accepted =
                        notice
                        |> Option.bind (fun token ->
                            state.Notices
                            |> Map.tryFind sid
                            |> Option.filter (fun n -> n.Nonce = token && n.Data = current)
                        )

                    if current <> Some patient.Patient && accepted.IsNone then
                        let nonce = newId ()

                        { state with
                            Notices =
                                state.Notices
                                |> Map.add
                                    sid
                                    {
                                        Nonce = nonce
                                        Data = current
                                        Expiry = now + challengeLifetime
                                    }
                            // a challenge over the data before the change must not be signed
                            Challenges = state.Challenges |> Map.remove sid
                        },
                        SigningResponse.DataNotice
                            {
                                Data = current
                                Token = nonce
                            }
                    // Concept 10: no challenge over a plan that names an order twice
                    elif duplicateOrders plan.Scenarios then
                        refuse SigningRefusal.ChallengeMismatch
                    else
                        match blockedBy record patient.PatientId state with
                        | Some head -> refuse (SigningRefusal.Blocked head)
                        | None ->
                            let nonce = newId ()

                            { state with
                                Notices = state.Notices |> Map.remove sid
                                Challenges =
                                    state.Challenges
                                    |> Map.add
                                        sid
                                        {
                                            Nonce = nonce
                                            Patient = plan.Patient
                                            Scenarios = plan.Scenarios
                                            Verified = current.IsSome
                                            Expiry = now + challengeLifetime
                                        }
                            },
                            SigningResponse.ChallengeIssued nonce


    /// uc-03 step 3, one act in the model's order (`dbCommit`): the Session with a User and a
    /// Patient; the answer already given to this Session's key (Rule 45); the Role re-taken
    /// from the registry (Rule 38, fails closed); the OpenedToken this Session holds (Rule 34);
    /// the record not moved on (Rule 20); the challenge this Session was issued, over exactly
    /// this plan (Rule 43); and last the PIN (Rules 23, 28), so that a Submission that was
    /// never going to land costs no attempt. Then the version is appended, the challenge
    /// spent, the OpenedToken re-minted over the new head, and the answer remembered under the
    /// key, refusals too. Three wrong PINs end the Session (`WrongPinLimit`), lock signing and
    /// mail the User (Rule 27); a wrong PIN while locked pushes the lock out; a right PIN while
    /// locked is refused and counts nothing.
    let commit
        (now: DateTime)
        (newId: unit -> string)
        (standing: BrowserIdentity -> UserStanding option)
        (send: Mail -> unit)
        (sid: string)
        (submission: Submission)
        (state: State)
        : State * SigningResponse
        =
        let state = dropExpired now state |> touch now sid
        let refuse refusal = state, SigningResponse.Refused refusal

        match state.Sessions |> Map.tryFind sid with
        | None -> refuse SigningRefusal.NoSession
        | Some record ->
            match record.Session.User, record.Session.PatientContext with
            | None, _ -> refuse SigningRefusal.NotPrescriber
            | Some _, None -> refuse SigningRefusal.NoPatient
            | Some user, Some patient ->
                match state.Answered |> Map.tryFind (sid, submission.IdemKey) with
                | Some(answer, _) -> state, answer
                | None ->
                    // the answer is remembered from here on, under this Session and the key
                    let remember (state: State) answer =
                        { state with Answered = state.Answered |> Map.add (sid, submission.IdemKey) (answer, now) },
                        answer

                    let refuse refusal =
                        remember state (SigningResponse.Refused refusal)

                    let identity =
                        {
                            Login = record.Login |> Option.defaultValue user.UserId
                            DisplayName = user.DisplayName
                        }

                    match standing identity with
                    | Some fresh when fresh.User.Role = UserRole.Prescriber ->
                        if record.Session.OpenedToken <> Some submission.Opened then
                            refuse SigningRefusal.StaleToken
                        else
                            match blockedBy record patient.PatientId state with
                            | Some head -> refuse (SigningRefusal.Blocked head)
                            | None ->
                                match state.Challenges |> Map.tryFind sid with
                                | None -> refuse SigningRefusal.ChallengeExpired
                                | Some challenge when
                                    challenge.Nonce <> submission.Challenge
                                    || challenge.Patient <> submission.Plan.Patient
                                    || challenge.Scenarios <> submission.Plan.Scenarios
                                    || duplicateOrders submission.Plan.Scenarios
                                    ->
                                    refuse SigningRefusal.ChallengeMismatch
                                | Some challenge ->
                                    let credential = credentialOf user.UserId state
                                    let wasLocked = Credential.isLocked now credential
                                    let right, credential = Credential.verify now submission.Pin credential

                                    let state =
                                        { state with Credentials = state.Credentials |> Map.add user.UserId credential }

                                    if right then
                                        let id = newId ()

                                        let plan =
                                            {
                                                Head =
                                                    {
                                                        Id = id
                                                        No =
                                                            1
                                                            + (state.Records
                                                               |> Map.tryFind patient.PatientId
                                                               |> Option.map List.length
                                                               |> Option.defaultValue 0)
                                                        By = user
                                                        SignedAt = now
                                                    }
                                                PatientId = patient.PatientId
                                                Base = record.OpenedWith
                                                Scenarios = challenge.Scenarios
                                                Patient = challenge.Patient
                                                Verified = challenge.Verified
                                            }

                                        let token = OpenedToken $"opened-{newId ()}"

                                        let opened =
                                            { record with
                                                Session =
                                                    { record.Session with
                                                        OpenedToken = Some token
                                                        Head = Some plan
                                                    }
                                                OpenedWith = Some id
                                            }

                                        remember
                                            { state with
                                                Records =
                                                    state.Records
                                                    |> Map.change
                                                        patient.PatientId
                                                        (fun versions ->
                                                            Some(plan :: (versions |> Option.defaultValue []))
                                                        )
                                                Challenges = state.Challenges |> Map.remove sid
                                                Sessions = state.Sessions |> Map.add sid opened
                                            }
                                            (SigningResponse.Submitted(plan, token))
                                    elif wasLocked then
                                        // this Session did nothing wrong; the lock is the credential's
                                        remember
                                            state
                                            (SigningResponse.Refused(SigningRefusal.Locked credential.LockedUntil.Value))
                                    elif credential |> Credential.attemptsLeft = 0 then
                                        // Rule 28: the limit is reached now; the Session ends (Rule 10)
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
                                            (SigningResponse.Refused SigningRefusal.PinLimit)
                                    else
                                        remember
                                            state
                                            (SigningResponse.Refused(
                                                SigningRefusal.PinWrong(credential |> Credential.attemptsLeft)
                                            ))
                    | _ -> refuse SigningRefusal.NotPrescriber


    /// Six digits from a random source (the CSPRNG in the host).
    let newCode (randomBelow: int -> int) () = (randomBelow 1_000_000).ToString "D6"


    /// The mac of a code under the host key (Rule 37).
    let codeMac (key: LaunchSeal.Key) (code: string) =
        LaunchSeal.mac key (Text.Encoding.UTF8.GetBytes code)


    let makeSessionPort
        (now: unit -> DateTime)
        (newId: unit -> string)
        (newCode: unit -> string)
        (newSalt: int -> byte[])
        (codeMac: string -> byte[])
        (verify: Launch -> Result<LaunchSeal.Claims, LaunchRefusal>)
        (idp: IdentityProviderPort)
        (registry: UserRegistryPort)
        (patientData: PatientDataPort)
        (mail: MailPort)
        (initial: State)
        : SessionPort
        =
        let gate = obj ()
        let mutable state = initial

        let update f =
            lock
                gate
                (fun () ->
                    let next, result = f state
                    state <- next
                    result
                )

        {
            present =
                fun launch -> async { return update (fun s -> present (now ()) newId verify idp.authorizeUrl s launch) }
            callback =
                fun cb ->
                    async {
                        return
                            update (fun s ->
                                callback
                                    (now ())
                                    newId
                                    newCode
                                    codeMac
                                    idp.redeem
                                    registry.standing
                                    patientData.read
                                    mail.send
                                    s
                                    cb
                            )
                    }
            find = fun id -> async { return update (find (now ()) id) }
            close = fun id -> async { return update (fun s -> close id s, ()) }
            findEnrolment = fun attempt -> async { return update (findEnrolment (now ()) attempt) }
            supplyPin =
                fun attempt code pin ->
                    async {
                        return
                            update (fun s ->
                                supplyPin
                                    (now ())
                                    newId
                                    newSalt
                                    codeMac
                                    registry.standing
                                    patientData.read
                                    mail.send
                                    attempt
                                    code
                                    pin
                                    s
                            )
                    }
            dropEnrolment = fun attempt -> async { return update (fun s -> dropEnrolment attempt s, ()) }
            challenge =
                fun sid request ->
                    async { return update (fun s -> challenge (now ()) newId patientData.read sid request s) }
            submit =
                fun sid submission ->
                    async {
                        return update (fun s -> commit (now ()) newId registry.standing mail.send sid submission s)
                    }
            seen = fun sid opened -> async { return update (seen (now ()) sid opened) }
            openVersion = fun sid id -> async { return update (openVersion (now ()) newId sid id) }
        }


/// The IdentityProvider and the UserRegistry as one stub directory over an identity choice made
/// on the stub launch page: `prescriber`, `reader`, `prescriber-other-patient`, `no-pin`,
/// `unknown` (a login the registry does not know), `none` (no identity: the stub IdP's
/// `/authorize` never issues a code for it). The active Patient of a choice is the one the
/// launch page was given, except for `prescriber-other-patient`.
module StubDirectory =

    let choices =
        [
            "prescriber"
            "prescriber-b"
            "reader"
            "prescriber-other-patient"
            "no-pin"
            "unknown"
            "none"
        ]


    let identityOf choice =
        {
            Login = choice
            DisplayName =
                match choice with
                | "prescriber" -> "Stub Prescriber"
                // UC-3: a second Prescriber on the same patient (Rule 20)
                | "prescriber-b" -> "Stub Prescriber B"
                | "reader" -> "Stub Reader"
                | "prescriber-other-patient" -> "Stub Prescriber (other patient)"
                | "no-pin" -> "Stub Prescriber (no PIN)"
                | other -> $"Stub {other}"
        }


    /// Rule 27: the address the registry gives for a login. The stub's is derived from it.
    let mailAddressOf (identity: BrowserIdentity) = $"{identity.Login}@stub.example"


    /// The registry's answer for a login, given the Patient the launch page made active. Whether
    /// a PIN is set is not the registry's to say (Rule 24): `no-pin` differs from `prescriber`
    /// only in the credential store's seed.
    let standingOf (activePatientId: string) (identity: BrowserIdentity) : UserStanding option =
        let standing role activePatientId =
            Some
                {
                    User =
                        {
                            UserId = identity.Login
                            DisplayName = identity.DisplayName
                            Role = role
                        }
                    ActivePatientId = Some activePatientId
                    MailAddress = mailAddressOf identity
                }

        match identity.Login with
        | "prescriber"
        | "prescriber-b"
        | "no-pin" -> standing UserRole.Prescriber activePatientId
        | "reader" -> standing UserRole.Reader activePatientId
        | "prescriber-other-patient" -> standing UserRole.Prescriber "other-patient"
        | _ -> None


    /// The directory: one-time codes (the stub IdP) and the active Patient per login (the stub
    /// registry), behind a lock. `issue` is what `/authorize` calls once it read the identity
    /// choice and the active Patient from the stub cookie.
    type Directory =
        {
            idp: IdentityProviderPort
            registry: UserRegistryPort
            issue: string -> string -> string
        }


    /// A code lives as long as a Launch (Rule 29); older ones are pruned on the next issue.
    let codeLifetime = TimeSpan.FromMinutes 2.0


    /// One active Patient per login, as MainEHR has: a later launch of the same login for
    /// another Patient makes an earlier, still open launch wrong-patient (ext 5b).
    let make (now: unit -> DateTime) (newCode: unit -> string) : Directory =
        let gate = obj ()
        let codes = Collections.Generic.Dictionary<string, BrowserIdentity * DateTime>()
        let active = Collections.Generic.Dictionary<string, string>()

        let prune () =
            let cutoff = now () - codeLifetime

            for stale in
                codes
                |> Seq.filter (fun kv -> snd kv.Value < cutoff)
                |> Seq.map _.Key
                |> Seq.toList do
                codes.Remove stale |> ignore

        {
            idp =
                {
                    authorizeUrl = fun state -> $"/authorize?state={Uri.EscapeDataString state}"
                    redeem =
                        fun code ->
                            lock
                                gate
                                (fun () ->
                                    match codes.TryGetValue code with
                                    | true, (identity, issued) when now () - issued <= codeLifetime ->
                                        codes.Remove code |> ignore
                                        Some identity
                                    | _ -> None
                                )
                }
            registry =
                {
                    standing =
                        fun identity ->
                            lock
                                gate
                                (fun () ->
                                    match active.TryGetValue identity.Login with
                                    | true, pid -> standingOf pid identity
                                    | _ -> standingOf "" identity
                                )
                }
            issue =
                fun choice activePatientId ->
                    lock
                        gate
                        (fun () ->
                            prune ()
                            let code = newCode ()
                            codes[code] <- identityOf choice, now ()
                            active[choice] <- activePatientId
                            code
                        )
        }


/// The PatientDataPlatform stub: nothing to import, except that `no-data` has no record at all
/// (ext 6a).
module StubPatientData =

    let port: PatientDataPort =
        {
            read =
                fun pid ->
                    if pid = "no-data" then
                        None
                    else
                        Some Shared.Models.Patient.empty
        }


/// The credential half of the Database, seeded for the stub logins: the Prescribers that sign
/// have the PIN `1234`, `no-pin` has none and enrols (UC-2), a Reader has no credential
/// (Rule 26). Per host start; a PIN set by enrolment lives as long as the host.
module StubCredentials =

    /// The PIN every seeded stub Prescriber has. Development and test servers only.
    let stubPin = "1234"


    let seed (newSalt: int -> byte[]) : Map<string, Credential> =
        [
            "prescriber", Credential.withPin newSalt stubPin
            "prescriber-b", Credential.withPin newSalt stubPin
            "prescriber-other-patient", Credential.withPin newSalt stubPin
            "no-pin", Credential.empty
        ]
        |> Map.ofList


/// The MailService stub (Actor M): an outbox behind a lock and a page that shows it, newest
/// first, so the tester reads a confirmation code where a User would read their mail.
/// `Server.fs` mounts `GET /stub/mail` in full scope only.
module StubMail =

    let path = "/stub/mail"


    type Outbox =
        {
            port: MailPort
            // newest first
            sent: unit -> Mail list
        }


    let make () : Outbox =
        let gate = obj ()
        let mutable mails: Mail list = []

        {
            port = { send = fun mail -> lock gate (fun () -> mails <- mail :: mails) }
            sent = fun () -> lock gate (fun () -> mails)
        }


    let private encode (s: string) = System.Net.WebUtility.HtmlEncode s


    /// The outbox as a page. No inline script or style, so the CSP (`default-src 'self'`)
    /// holds; every field is HTML-encoded, the body keeps its line breaks.
    let page (mails: Mail list) =
        let items =
            match mails with
            | [] -> "<p>No mail sent yet.</p>"
            | mails ->
                mails
                |> List.map (fun m ->
                    $"""<article>
<h2>{encode m.Subject}</h2>
<p>To: <code>{encode m.To}</code></p>
<pre>{encode m.Body}</pre>
</article>"""
                )
                |> String.concat "\n"

        $"""<!doctype html>
<html lang="en">
<head><meta charset="utf-8"><title>GenPRES stub mail</title></head>
<body>
<h1>Stub MailService outbox</h1>
<p>Stands in for the MailService (uc-02, Rule 27): every mail the server sent since it started,
newest first. Development and test servers only.</p>
{items}
</body>
</html>"""


/// The stub LaunchScript (uc-01 step 1) as a page the server serves in full scope: it mints a
/// sealed Launch for a chosen PatientId and opens the client on it. Pure here; `Server.fs`
/// mounts `GET /stub/launch` (the page) and `POST /stub/launch` (mint + redirect).
module StubLaunch =

    let path = "/stub/launch"


    /// The Launch lifetime (Rule 29): a page load, the identity round trip, a retry or two.
    let lifetime = TimeSpan.FromMinutes 2.0


    /// The form for a list of identity choices (the stub directory's). No inline script or
    /// style, so the CSP (`default-src 'self'`) holds. The PatientId `no-data` opens a Session
    /// without imported data (ext 6a).
    let pageFor (choices: string list) =
        let options =
            choices
            |> List.map (fun c -> $"""    <option value="{c}">{c}</option>""")
            |> String.concat "\n"

        $"""<!doctype html>
<html lang="en">
<head><meta charset="utf-8"><title>GenPRES stub launch</title></head>
<body>
<h1>Stub LaunchScript</h1>
<p>Stands in for the MainEHR LaunchScript (uc-01 step 1): mints a sealed Launch for the patient
below and opens GenPRES on it. Development and test servers only.</p>
<form method="post" action="{path}">
  <p><label>PatientId <input name="pid" value="stub-patient" required></label>
  <small>(<code>no-data</code>: a patient without imported data)</small></p>
  <p><label>Identity at the browser
  <select name="identity">
{options}
  </select></label>
  <small>(who the stub IdentityProvider says is there; <code>none</code>: nobody)</small></p>
  <button type="submit">Launch</button>
</form>
</body>
</html>"""


    /// The page over the stub directory's choices.
    let page = pageFor StubDirectory.choices


    /// Where the browser goes after minting: the hash form of decision D1.
    let launchUrl (launch: Launch) =
        match launch with
        | Launch text -> $"/#/session?launch={Uri.EscapeDataString text}"


    /// Mints the Launch for a posted PatientId; blank falls back to the stub patient.
    let mint (now: DateTime) (newNonce: unit -> string) (key: LaunchSeal.Key) (pid: string) =
        let pid =
            if String.IsNullOrWhiteSpace pid then
                "stub-patient"
            else
                pid.Trim()

        LaunchSeal.mint
            key
            {
                PatientId = pid
                Nonce = newNonce ()
                Expiry = now + lifetime
            }


    let choices = StubDirectory.choices

    let identityCookieName = "genpres_stub_identity"


    /// The stub identity cookie value for a choice and the Patient the page made active.
    let identityCookie (choice: string) (pid: string) =
        $"{Uri.EscapeDataString choice}.{Uri.EscapeDataString pid}"


    /// The choice and the Patient back from the cookie; None for anything else.
    let parseIdentityCookie (value: string option) : (string * string) option =
        match value with
        | Some v when not (String.IsNullOrWhiteSpace v) ->
            match v.Split('.') with
            | [| choice; pid |] when choices |> List.contains (Uri.UnescapeDataString choice) ->
                Some(Uri.UnescapeDataString choice, Uri.UnescapeDataString pid)
            | _ -> None
        | _ -> None


module Adapters =

    open Informedica.GenForm.Lib


    let private interactionJsonCache =
        lazy
            (let path =
                System.IO.Path.Combine(Informedica.Utils.Lib.AppPath.interactionsDir (), "Data.JSON")
                |> System.IO.Path.GetFullPath

             if System.IO.File.Exists(path) then
                 System.IO.File.ReadAllText(path) |> Some
             else
                 None)


    let loadInteractionJson () = interactionJsonCache.Value


    let toSharedDrugInteraction (di: Informedica.GenInteract.Lib.DrugInteraction) : Shared.Types.DrugInteraction =
        {
            Name = di.Name
            Drug1 = di.Drug1
            Drug2 = di.Drug2
        }


    let private resolveLogger () =
        match Logging.loggingLevel with
        | None -> None, Informedica.GenOrder.Lib.OrderLogging.noOp
        | Some level ->
            let agent = Logging.getLogger level Logging.OrderLogger
            (Some agent, agent.Logger)


    let private setComponentName name agent =
        async {
            match agent with
            | Some a -> do! a |> Logging.setComponentName (Some name)
            | None -> ()
        }


    let private makeFormularyPort (provider: Resources.IResourceProvider) : FormularyPort =
        {
            getFormulary = fun form -> async { return form |> FormularyService.get provider }

            getParenteralia =
                fun par -> async { return par |> ParenteraliaService.get provider |> Result.mapError Array.singleton }
        }


    let private makeOrderContextPort agent logger (provider: Resources.IResourceProvider) : OrderContextPort =
        {
            evaluate =
                fun ctxCmd ctx ->
                    async {
                        do! setComponentName "OrderContext" agent

                        return ctx |> OrderContextService.evaluate logger provider ctxCmd
                    }
        }


    let private makeOrderPlanPort
        agent
        (provider: Resources.IResourceProvider)
        (orderCtxPort: OrderContextPort)
        : OrderPlanPort
        =
        {
            updateOrderPlan =
                fun tp cmdOpt ->
                    async {
                        do! setComponentName "OrderPlan" agent

                        let! updated = OrderPlanService.updateOrderPlan orderCtxPort tp cmdOpt
                        let totals = provider.GetTotals()
                        return updated |> OrderPlanService.calculateTotals totals |> Ok
                    }

            filterOrderPlan =
                fun tp ->
                    async {
                        let totals = provider.GetTotals()
                        return tp |> OrderPlanService.calculateTotals totals |> Ok
                    }
        }


    let private makeNutritionPlanPort
        (orderCtxPort: OrderContextPort)
        logger
        (provider: Resources.IResourceProvider)
        : NutritionPlanPort
        =
        {
            initNutritionPlan =
                fun patient ->
                    async {
                        let totals = provider.GetTotals()
                        return NutritionPlanService.initNutritionPlan logger totals patient
                    }

            addNutritionContext =
                fun (plan, category) ->
                    let totals = provider.GetTotals()
                    NutritionPlanService.addNutritionContext totals orderCtxPort (plan, category)

            removeNutritionContext =
                fun (plan, id) ->
                    async {
                        let totals = provider.GetTotals()
                        return NutritionPlanService.removeNutritionContext totals (plan, id)
                    }

            updateNutritionOrderContext =
                fun (plan, label, ctx) ->
                    let totals = provider.GetTotals()
                    NutritionPlanService.updateNutritionOrderContext totals orderCtxPort (plan, label, ctx)

            selectNutritionOrderScenario =
                fun (plan, label, ctx) ->
                    let totals = provider.GetTotals()
                    NutritionPlanService.selectNutritionOrderScenario totals orderCtxPort (plan, label, ctx)

            navigateNutritionOrderContext =
                fun (plan, label, ctxCmd, ctx) ->
                    let totals = provider.GetTotals()
                    NutritionPlanService.navigateNutritionOrderContext totals orderCtxPort (plan, label, ctxCmd, ctx)
        }


    /// The session port of a server that does not launch: every Launch is refused as
    /// invalid and no session is ever found. Used in production until the scope switch (#580)
    /// decides what a production server exposes.
    let sessionDisabled: SessionPort =
        {
            present = fun _ -> async { return LaunchResult.Refused LaunchRefusal.LaunchInvalid }
            callback =
                fun _ ->
                    async {
                        return
                            CallbackResult.Refused(
                                LaunchRefusal.LaunchInvalid,
                                Hop.refusedUrl LaunchRefusal.LaunchInvalid
                            )
                    }
            find = fun _ -> async { return SessionLookup.NotFound }
            close = fun _ -> async { return () }
            findEnrolment = fun _ -> async { return None }
            supplyPin = fun _ _ _ -> async { return SupplyPinResult.Refused PinRefusal.AttemptExpired }
            dropEnrolment = fun _ -> async { return () }
            challenge = fun _ _ -> async { return SigningResponse.Refused SigningRefusal.NoSession }
            submit = fun _ _ -> async { return SigningResponse.Refused SigningRefusal.NoSession }
            seen = fun _ _ -> async { return None }
            openVersion = fun _ _ -> async { return None }
        }


    let makeAppEnvWith
        (launchKey: LaunchSeal.Key)
        (directory: StubDirectory.Directory)
        (mail: MailPort)
        (provider: Resources.IResourceProvider)
        : AppEnv
        =
        let agent, logger = resolveLogger ()
        let orderCtxPort = makeOrderContextPort agent logger provider

        {
            formulary = makeFormularyPort provider
            orderContext = orderCtxPort
            orderPlan = makeOrderPlanPort agent provider orderCtxPort
            nutritionPlan = makeNutritionPlanPort orderCtxPort logger provider
            interaction =
                {
                    checkInteractions =
                        fun drugs ->
                            async {
                                try
                                    let result =
                                        Informedica.GenInteract.Lib.Api.checkInteractions (loadInteractionJson ()) drugs
                                        |> List.map toSharedDrugInteraction

                                    return Ok result
                                with ex ->
                                    return Error [| ex.Message |]
                            }

                    getDrugNames =
                        fun () ->
                            async {
                                try
                                    let result = Informedica.GenInteract.Lib.Api.getDrugNames (loadInteractionJson ())

                                    return Ok result
                                with ex ->
                                    return Error [| ex.Message |]
                            }
                }
            logAnalyzer =
                {
                    listLogFiles =
                        fun () ->
                            async {
                                try
                                    return Ok(LogAnalyzer.listLogFiles ())
                                with ex ->
                                    return Error [| ex.Message |]
                            }
                    analyzeLogFile = fun fileName -> async { return LogAnalyzer.analyzeFile fileName }
                }
            requireLoaded =
                fun () ->
                    let info = provider.GetResourceInfo()

                    if info.IsLoaded then
                        None
                    else
                        info.Messages |> Array.map (fun msg -> FormLogging.formatMessage msg) |> Some
            // plan 409 step 2: an in-memory stub with a two-minute Launch lifetime (Rule 29);
            // its sessions live as long as this AppEnv
            session =
                Hop.makeSessionPort
                    (fun () -> DateTime.UtcNow)
                    PublicKey.randomId
                    // the confirmation code and the salt from the CSPRNG, the code mac under
                    // the host key (UC-2, Rule 37)
                    (Hop.newCode System.Security.Cryptography.RandomNumberGenerator.GetInt32)
                    System.Security.Cryptography.RandomNumberGenerator.GetBytes
                    (Hop.codeMac launchKey)
                    (fun launch -> LaunchSeal.verify DateTime.UtcNow launchKey launch)
                    directory.idp
                    directory.registry
                    StubPatientData.port
                    mail
                    // the credential store, seeded per stub login (plan 615)
                    (Hop.initialState (StubCredentials.seed System.Security.Cryptography.RandomNumberGenerator.GetBytes))
        }


    /// An env with its own seal key and stub directory: what tests and the MCP host build. The
    /// server builds `makeAppEnvWith` so that its stub pages share the key and the directory.
    let makeAppEnv (provider: Informedica.GenForm.Lib.Resources.IResourceProvider) =
        makeAppEnvWith
            (LaunchSeal.newKey System.Security.Cryptography.RandomNumberGenerator.GetBytes)
            (StubDirectory.make (fun () -> DateTime.UtcNow) PublicKey.randomId)
            (StubMail.make ()).port
            provider
