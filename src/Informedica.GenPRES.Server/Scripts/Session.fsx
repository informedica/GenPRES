// Plan 409, step 2a: the session contract, the SessionPort and an in-memory stub.
//
// Launch sequence: docs/scenarios/integration/uc-01-launch.md
// Plan:            docs/implementation-plans/409-client-launch-sequence.md
//
// Script-only draft. Migration targets, in this order:
//   Contract    -> src/Informedica.GenPRES.Shared/Types.fs
//   Ports       -> src/Informedica.GenPRES.Server/ServerApi.Ports.fs (+ AppEnv.session)
//   PublicKey   -> src/Informedica.GenPRES.Server/ServerApi.Adapters.fs
//   SessionStub -> src/Informedica.GenPRES.Server/ServerApi.Adapters.fs
//   Tests       -> tests/Informedica.GenPRES.Server.Tests/StubAdapterTests.fs
//
// Deviations from the plan text, on purpose:
//   - the port field is `present`, not `open`: `open` is an F# keyword.
//   - the port answers a server-side `LaunchResult` that carries the session id next to the
//     `SessionOpened`; the plan's `Async<LaunchOutcome>` has nowhere to put the id the cookie
//     needs (Rule 12: the id never enters the client-facing `LaunchOutcome`).
//   - the DUs carry [<RequireQualifiedAccess>] per the coding standard.
//   - the refusal vocabulary covers all seven client reasons, not five: `invalid` and
//     `enrolment` cost nothing here and let the SessionGate texts (step 4b) be exercised.
//
// Run: cd src/Informedica.GenPRES.Server/Scripts && dotnet fsi Session.fsx

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.1"
#r "../../Informedica.GenPRES.Shared/bin/Debug/net10.0/Informedica.GenPRES.Shared.dll"

open System
open Shared.Types


/// === Shared.Types additions ===
/// The client-facing contract of the launch (uc-01 steps 1, 4, 6). No SessionId anywhere:
/// it lives in the cookie (Rule 12).
module Contract =

    /// Opaque, sealed by the MainEHR LaunchScript; the client never reads it.
    type Launch = Launch of string

    /// Public JWK (JSON text) of the browser key pair made at launch step 3.
    type PublicKey = PublicKey of string

    /// Names the TreatmentPlan the Session opened with (Rule 34). Stored now, sent later
    /// inside the signed request of step 7.
    type OpenedToken = OpenedToken of string


    [<RequireQualifiedAccess>]
    type UserRole =
        | Prescriber
        | Reader


    type UserContext =
        {
            UserId: string
            DisplayName: string
            Role: UserRole
        }


    type PatientContext =
        {
            PatientId: string
            Patient: Patient
        }


    /// What the client keeps of an open Session.
    type SessionOpened =
        {
            // None = anonymous session (Rule 14)
            User: UserContext option
            // None = launch without an active patient (ext 1a)
            PatientContext: PatientContext option
            OpenedToken: OpenedToken option
            // RFC 7638 thumbprint of the public key this Session signs with (step 7);
            // the client keeps that private key and prunes the others
            KeyThumbprint: string option
        }


    /// Every refusal ends the same: no Session opens (Rule 7). The case says what the client
    /// offers next (uc-01, Refusals table).
    [<RequireQualifiedAccess>]
    type LaunchRefusal =
        // ext 4a: ask for a relaunch
        | LaunchExpired
        | LaunchSpent
        | LaunchInvalid
        // ext 3c: retry, then relaunch
        | NoBrowserIdentity
        // ext 5a: offer an anonymous open
        | NoRole
        // ext 5b: relaunch after fixing MainEHR
        | WrongActivePatient
        // UC-2, shown as text only for now
        | EnrolmentRequired


    [<RequireQualifiedAccess>]
    type LaunchOutcome =
        | Opened of SessionOpened
        // step 4.2 as a payload: Fable.Remoting's XHR would follow a 302 and try to parse
        // the IdentityProvider's HTML. The stub never returns it.
        | RedirectTo of url: string
        | Refused of LaunchRefusal


open Contract


/// === ServerApi.Ports additions ===
module Ports =

    /// The adapter's answer to a presentation. The session id is the server's to put in the
    /// cookie; the composition root maps this to the client's `LaunchOutcome` without it.
    [<RequireQualifiedAccess>]
    type LaunchResult =
        | Opened of sessionId: string * SessionOpened
        | RedirectTo of url: string
        | Refused of LaunchRefusal


    type SessionPort =
        {
            // idempotent per public key within the Launch lifetime (Rule 2, uc-01 Retries)
            present: Launch * PublicKey -> Async<LaunchResult>
            // by session id from the cookie
            find: string -> Async<SessionOpened option>
            // Rule 10: explicit close
            close: string -> Async<unit>
        }


open Ports


/// === ServerApi.Adapters: PublicKey ===
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


/// === ServerApi.Adapters: SessionStub ===
/// In-memory stand-in for launch steps 4 and 5. It never redirects: the answer comes from the
/// Launch text. The record it keeps per Launch is the LaunchRecord the real server appends at
/// step 4.2, with the Launch text standing in for the nonce.
module SessionStub =

    /// One record per Launch (Rules 2, 40, 45): the public key that presented it, the answer
    /// it got, and when both are forgotten (Rule 29).
    type LaunchRecord =
        {
            PublicKey: PublicKey
            Result: LaunchResult
            Expiry: DateTime
        }


    type State =
        {
            Launches: Map<string, LaunchRecord>
            Sessions: Map<string, SessionOpened>
        }


    let emptyState =
        {
            Launches = Map.empty
            Sessions = Map.empty
        }


    /// The Launch texts that refuse; anything else opens a Session.
    let refusalOf text =
        match text with
        | "expired" -> Some LaunchRefusal.LaunchExpired
        | "spent" -> Some LaunchRefusal.LaunchSpent
        | "invalid" -> Some LaunchRefusal.LaunchInvalid
        | "no-identity" -> Some LaunchRefusal.NoBrowserIdentity
        | "no-role" -> Some LaunchRefusal.NoRole
        | "wrong-patient" -> Some LaunchRefusal.WrongActivePatient
        | "enrolment" -> Some LaunchRefusal.EnrolmentRequired
        | _ -> None


    let stubUser =
        {
            UserId = "stub-prescriber"
            DisplayName = "Stub Prescriber"
            Role = UserRole.Prescriber
        }


    /// Answers a presentation and returns the state after it. Pure: the clock, the id source,
    /// the lifetime and the patient are parameters.
    ///
    /// - a Launch with a record: expired -> LaunchExpired and the record is dropped; the same
    ///   public key -> the recorded answer (Rule 2, same browser); another key -> LaunchSpent
    ///   (it cannot be told from another browser).
    /// - a Launch without a record: a refusal word refuses and records nothing, so a retry
    ///   re-verifies; anything else opens a Session and writes the record in the same act
    ///   (Rule 40).
    let present
        (now: DateTime)
        (newId: unit -> string)
        (lifetime: TimeSpan)
        (patient: Patient)
        (state: State)
        (Launch text, key)
        : State * LaunchResult =
        match state.Launches |> Map.tryFind text with
        | Some record when now > record.Expiry ->
            { state with
                Launches = state.Launches |> Map.remove text
            },
            LaunchResult.Refused LaunchRefusal.LaunchExpired
        | Some record when record.PublicKey = key -> state, record.Result
        | Some _ -> state, LaunchResult.Refused LaunchRefusal.LaunchSpent
        | None ->
            match refusalOf text with
            | Some refusal -> state, LaunchResult.Refused refusal
            | None ->
                let id = newId ()

                let session =
                    {
                        User = Some stubUser
                        PatientContext =
                            Some
                                {
                                    PatientId = "stub-patient"
                                    Patient = patient
                                }
                        OpenedToken = Some(OpenedToken $"opened-{id}")
                        KeyThumbprint = Some(PublicKey.thumbprint key)
                    }

                let result = LaunchResult.Opened(id, session)

                {
                    Launches =
                        state.Launches
                        |> Map.add
                            text
                            {
                                PublicKey = key
                                Result = result
                                Expiry = now + lifetime
                            }
                    Sessions = state.Sessions |> Map.add id session
                },
                result


    /// The port over a single mutable state guarded by a lock. `now` and `newId` are injected
    /// (ADR-0001, effects as parameters); production passes `DateTime.UtcNow` and a random id.
    let makeSessionPort (now: unit -> DateTime) (newId: unit -> string) (lifetime: TimeSpan) (patient: Patient) =
        let gate = obj ()
        let mutable state = emptyState

        let update f =
            lock gate (fun () ->
                let next, result = f state
                state <- next
                result
            )

        {
            present = fun launch -> async { return update (fun s -> present (now ()) newId lifetime patient s launch) }
            find = fun id -> async { return state.Sessions |> Map.tryFind id }
            close =
                fun id ->
                    async {
                        return
                            update (fun s ->
                                { s with
                                    Sessions = s.Sessions |> Map.remove id
                                },
                                ()
                            )
                    }
        }


/// === tests/Informedica.GenPRES.Server.Tests/StubAdapterTests.fs additions ===
module Tests =

    open Expecto
    open Expecto.Flip
    open SessionStub


    let lifetime = TimeSpan.FromMinutes 2.0

    let t0 = DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc)

    let keyA = PublicKey "key-A"
    let keyB = PublicKey "key-B"


    /// A port with a settable clock and a counting id source.
    let makePort () =
        let clock = ref t0
        let count = ref 0

        let port =
            makeSessionPort
                (fun () -> clock.Value)
                (fun () ->
                    count.Value <- count.Value + 1
                    $"session-{count.Value}"
                )
                lifetime
                Shared.Models.Patient.empty

        port, clock


    let refusalWords =
        [
            "expired", LaunchRefusal.LaunchExpired
            "spent", LaunchRefusal.LaunchSpent
            "invalid", LaunchRefusal.LaunchInvalid
            "no-identity", LaunchRefusal.NoBrowserIdentity
            "no-role", LaunchRefusal.NoRole
            "wrong-patient", LaunchRefusal.WrongActivePatient
            "enrolment", LaunchRefusal.EnrolmentRequired
        ]


    let thumbprintTests =
        testList
            "PublicKey.thumbprint"
            [
                test "RFC 7638 section 3.1 example" {
                    // members deliberately out of order and with whitespace: the thumbprint
                    // covers the required members only, in lexicographic order, unpadded
                    let jwk =
                        """{
                          "kty": "RSA",
                          "n": "0vx7agoebGcQSuuPiLJXZptN9nndrQmbXEps2aiAFbWhM78LhWx4cbbfAAtVT86zwu1RK7aPFFxuhDR1L6tSoc_BJECPebWKRXjBZCiFV4n3oknjhMstn64tZ_2W-5JsGY4Hc5n9yBXArwl93lqt7_RN5w6Cf0h4QyQ5v-65YGjQR0_FDW2QvzqY368QQMicAtaSqzs8KJZgnYb9c7d0zgdAZHzu6qMQvRL5hajrn1n91CbOpbISD08qNLyrdkt-bFTWhAI4vMQFh6WeZu0fM4lFd2NcRwr3XPksINHaQ-G_xBniIqbw0Ls1jF44-csFCur-kEgU8awapJzKnqDKgw",
                          "e": "AQAB",
                          "alg": "RS256",
                          "kid": "2011-04-29"
                        }"""

                    PublicKey jwk
                    |> PublicKey.thumbprint
                    |> Expect.equal "should match the RFC example" "NzbLsXh8uDCcd-6MNwXF4W_7noWXFZAfHkxZsRGC9Xs"
                }

                test "EC key: only crv, kty, x, y count" {
                    let a = PublicKey """{"kty":"EC","crv":"P-256","x":"X","y":"Y","kid":"one"}"""
                    let b = PublicKey """{"y":"Y","x":"X","crv":"P-256","kty":"EC","use":"sig"}"""

                    PublicKey.thumbprint a
                    |> Expect.equal "should ignore order and extra members" (PublicKey.thumbprint b)
                }

                test "text that is not a JWK still gets a stable thumbprint" {
                    PublicKey.thumbprint keyA
                    |> Expect.equal "should be deterministic" (PublicKey.thumbprint keyA)

                    PublicKey.thumbprint keyA
                    |> Expect.notEqual "should differ per key" (PublicKey.thumbprint keyB)
                }
            ]


    let stubTests =
        testList
            "Session stub"
            [
                testList
                    "refusal mapping"
                    [
                        for word, refusal in refusalWords do
                            testAsync $"launch={word} refuses with {refusal} and records nothing" {
                                let port, _ = makePort ()

                                let! first = port.present (Launch word, keyA)
                                // another key gets the same answer: nothing was recorded
                                let! second = port.present (Launch word, keyB)

                                first |> Expect.equal "first" (LaunchResult.Refused refusal)
                                second |> Expect.equal "second, other key" (LaunchResult.Refused refusal)

                                let! found = port.find "session-1"
                                found |> Expect.isNone "no session opened"
                            }
                    ]

                testAsync "any other launch opens a session with a stub prescriber and patient" {
                    let port, _ = makePort ()

                    match! port.present (Launch "demo", keyA) with
                    | LaunchResult.Opened(id, session) ->
                        id |> Expect.equal "session id" "session-1"

                        session.User
                        |> Option.map _.Role
                        |> Expect.equal "role" (Some UserRole.Prescriber)

                        session.PatientContext
                        |> Option.map _.PatientId
                        |> Expect.equal "patient" (Some "stub-patient")

                        session.OpenedToken |> Expect.isSome "opened token"

                        session.KeyThumbprint
                        |> Expect.equal "thumbprint of the presented key" (Some(PublicKey.thumbprint keyA))

                        let! found = port.find id
                        found |> Expect.equal "find returns the session" (Some session)
                    | other -> failtest $"expected Opened, got {other}"
                }

                testAsync "a second presentation with the same key within the lifetime is answered as the first" {
                    let port, clock = makePort ()

                    let! first = port.present (Launch "demo", keyA)
                    clock.Value <- t0 + TimeSpan.FromSeconds 90.0
                    let! second = port.present (Launch "demo", keyA)

                    second |> Expect.equal "same session, same content" first

                    let! second' = port.find "session-2"
                    second' |> Expect.isNone "nothing opened twice"
                }

                testAsync "a second presentation with another key is LaunchSpent and opens nothing" {
                    let port, _ = makePort ()

                    let! _ = port.present (Launch "demo", keyA)
                    let! other = port.present (Launch "demo", keyB)

                    other
                    |> Expect.equal "spent" (LaunchResult.Refused LaunchRefusal.LaunchSpent)

                    let! first = port.find "session-1"
                    first |> Expect.isSome "the first session is untouched"

                    let! second = port.find "session-2"
                    second |> Expect.isNone "no second session"
                }

                testAsync "after the lifetime the launch is LaunchExpired, even for the same key" {
                    let port, clock = makePort ()

                    let! _ = port.present (Launch "demo", keyA)
                    clock.Value <- t0 + lifetime + TimeSpan.FromSeconds 1.0
                    let! late = port.present (Launch "demo", keyA)

                    late
                    |> Expect.equal "expired" (LaunchResult.Refused LaunchRefusal.LaunchExpired)
                }

                testAsync "close removes the session" {
                    let port, _ = makePort ()

                    let! _ = port.present (Launch "demo", keyA)
                    do! port.close "session-1"

                    let! found = port.find "session-1"
                    found |> Expect.isNone "closed"
                }

                testAsync "find of an unknown id is None" {
                    let port, _ = makePort ()
                    let! found = port.find "nope"
                    found |> Expect.isNone "unknown"
                }

                test "present is pure: the same state and input give the same answer" {
                    let ids = ref 0

                    let newId () =
                        ids.Value <- ids.Value + 1
                        $"s{ids.Value}"

                    let state1, r1 =
                        present t0 newId lifetime Shared.Models.Patient.empty emptyState (Launch "demo", keyA)

                    let _, r2 = present t0 newId lifetime Shared.Models.Patient.empty state1 (Launch "demo", keyA)

                    r2 |> Expect.equal "recorded answer" r1
                    state1.Launches |> Map.count |> Expect.equal "one record" 1
                    state1.Sessions |> Map.count |> Expect.equal "one session" 1
                }
            ]


    let tests = testList "Session" [ thumbprintTests; stubTests ]


Expecto.Tests.runTestsWithCLIArgs [] [||] Tests.tests
