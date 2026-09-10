// The sealed Launch and the stub LaunchScript page (plan 605, PR 1).
//
// Script-first draft (script-only policy) of:
//   - `LaunchSeal`: the Launch of uc-01 step 1 as MainEHR's LaunchScript will mint it — PatientId,
//     a fresh nonce and an expiry sealed under a key shared with the Server (Concept 3, Rules 3,
//     29). Here the key is made once per host start, so a token from an earlier run is "not
//     sealed under the key". → `ServerApi.Adapters.fs`, module `LaunchSeal`.
//   - `SessionStub.present` verifying the seal instead of matching words: `expired`, `invalid`
//     and `spent` become behaviour; records are keyed by the nonce (Rule 2). Until the identity
//     hop (PR 2) any valid token opens with the stub prescriber. → `ServerApi.Adapters.fs`.
//   - `StubLaunch`: the page and the redirect of the stub LaunchScript, pure; the Giraffe routes
//     go into `Server.fs` at migration, behind `not IsProd`.
//
// Run: `dotnet fsi Launch.fsx` from this directory (build the solution first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open System
open Shared.Types
open Shared.Models
open ServerApi


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


/// The stub for launch steps 4 and 5 over a sealed Launch (replaces the word-matching stub).
module SessionStub =

    open SessionStub

    /// Answers a presentation and returns the state after it. Pure: the clock, the id source,
    /// the verifier and the patient are parameters.
    ///
    /// - not sealed under the key, or past its expiry: refused, nothing recorded (Rules 3, 29);
    /// - a record under the nonce: the same public key gets the recorded answer (Rule 2, same
    ///   browser); another key is LaunchSpent (it cannot be told from another browser);
    /// - a new nonce opens a Session for the Launch's PatientId and writes the record in the
    ///   same act (Rule 40). The record lives as long as the Launch does.
    let present
        (now: DateTime)
        (newId: unit -> string)
        (verify: Launch -> Result<LaunchSeal.Claims, LaunchRefusal>)
        (patient: Patient)
        (state: State)
        (launch, key)
        : State * LaunchResult
        =
        match verify launch with
        | Error refusal ->
            // nothing is recorded for a refused Launch; records past their expiry go with it (Rule 29)
            { state with Launches = state.Launches |> Map.filter (fun _ r -> now <= r.Expiry) },
            LaunchResult.Refused refusal
        | Ok claims ->
            match state.Launches |> Map.tryFind claims.Nonce with
            | Some record when record.PublicKey = key -> state, record.Result
            | Some _ -> state, LaunchResult.Refused LaunchRefusal.LaunchSpent
            | None ->
                let id = newId ()

                let session =
                    {
                        User = Some stubUser
                        PatientContext =
                            Some
                                {
                                    PatientId = claims.PatientId
                                    Patient = patient
                                }
                        OpenedToken = Some(OpenedToken $"opened-{id}")
                        KeyThumbprint = Some(PublicKey.thumbprint key)
                    }

                let result = LaunchResult.Opened(id, session)

                {
                    Launches =
                        state.Launches
                        |> Map.filter (fun _ r -> now <= r.Expiry)
                        |> Map.add
                            claims.Nonce
                            {
                                PublicKey = key
                                Result = result
                                Expiry = claims.Expiry
                            }
                    Sessions = state.Sessions |> Map.add id session
                },
                result


    /// The port over a single mutable state guarded by a lock; `verify` is the seal check
    /// bound to the host's key.
    let makeSessionPort
        (now: unit -> DateTime)
        (newId: unit -> string)
        (verify: Launch -> Result<LaunchSeal.Claims, LaunchRefusal>)
        (patient: Patient)
        =
        let gate = obj ()
        let mutable state = emptyState

        let update f =
            lock
                gate
                (fun () ->
                    let next, result = f state
                    state <- next
                    result
                )

        {
            present = fun launch -> async { return update (fun s -> present (now ()) newId verify patient s launch) }
            find = fun id -> async { return lock gate (fun () -> state.Sessions |> Map.tryFind id) }
            close = fun id -> async { return update (fun s -> { s with Sessions = s.Sessions |> Map.remove id }, ()) }
        }


/// The stub LaunchScript (uc-01 step 1) as a page the server serves in full scope: it mints a
/// sealed Launch for a chosen PatientId and opens the client on it. Pure here; `Server.fs`
/// mounts `GET /stub/launch` (the page) and `POST /stub/launch` (mint + redirect).
module StubLaunch =

    let path = "/stub/launch"


    /// The Launch lifetime (Rule 29): a page load, the identity round trip, a retry or two.
    let lifetime = TimeSpan.FromMinutes 2.0


    /// The form. No inline script or style, so the CSP (`default-src 'self'`) holds.
    let page =
        $"""<!doctype html>
<html lang="en">
<head><meta charset="utf-8"><title>GenPRES stub launch</title></head>
<body>
<h1>Stub LaunchScript</h1>
<p>Stands in for the MainEHR LaunchScript (uc-01 step 1): mints a sealed Launch for the patient
below and opens GenPRES on it. Development and test servers only.</p>
<form method="post" action="{path}">
  <label>PatientId <input name="pid" value="stub-patient" required></label>
  <button type="submit">Launch</button>
</form>
</body>
</html>"""


    /// Where the browser goes after minting: the hash form of decision D1.
    let launchUrl (launch: Launch) =
        match launch with
        | Launch text -> $"/#/session?launch={Uri.EscapeDataString text}"


    /// Mints the Launch for a posted PatientId; blank falls back to the stub patient.
    let mint (now: DateTime) (newNonce: unit -> string) (key: LaunchSeal.Key) (pid: string) =
        let pid = if String.IsNullOrWhiteSpace pid then "stub-patient" else pid.Trim()

        LaunchSeal.mint
            key
            {
                PatientId = pid
                Nonce = newNonce ()
                Expiry = now + lifetime
            }


open Expecto
open Expecto.Flip


let key = LaunchSeal.Key(Array.init 32 byte)
let otherKey = LaunchSeal.Key(Array.init 32 (fun i -> byte (i + 1)))
let t0 = DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc)

let claims =
    {
        LaunchSeal.PatientId = "p-1"
        LaunchSeal.Nonce = "n-1"
        LaunchSeal.Expiry = t0.AddMinutes 2.0
    }


let sealTests =
    testList
        "LaunchSeal"
        [
            test "a minted Launch verifies to its claims" {
                LaunchSeal.mint key claims
                |> LaunchSeal.verify t0 key
                |> Expect.equal "claims" (Ok claims)
            }

            test "the token has two base64url parts and no padding" {
                let (Launch text) = LaunchSeal.mint key claims
                let parts = text.Split('.')
                parts.Length |> Expect.equal "two parts" 2

                for p in parts do
                    (p.Contains "=" || p.Contains "+" || p.Contains "/")
                    |> Expect.isFalse "no padding, no + or /"
            }

            test "another key does not verify" {
                LaunchSeal.mint key claims
                |> LaunchSeal.verify t0 otherKey
                |> Expect.equal "invalid" (Error LaunchRefusal.LaunchInvalid)
            }

            test "a tampered payload does not verify" {
                let (Launch text) = LaunchSeal.mint key claims
                let parts = text.Split('.')

                let tampered =
                    { claims with PatientId = "p-2" }
                    |> LaunchSeal.mint key
                    |> fun (Launch t) -> t.Split('.')[0]

                Launch $"{tampered}.{parts[1]}"
                |> LaunchSeal.verify t0 key
                |> Expect.equal "invalid" (Error LaunchRefusal.LaunchInvalid)
            }

            test "garbage, empty, null and one-part texts are invalid" {
                for text in [ "garbage"; ""; null; "a.b.c"; "!!.??"; "eyJ9.abc" ] do
                    Launch text
                    |> LaunchSeal.verify t0 key
                    |> Expect.equal $"'{text}'" (Error LaunchRefusal.LaunchInvalid)
            }

            test "past the expiry the Launch is expired, not invalid" {
                LaunchSeal.mint key claims
                |> LaunchSeal.verify (t0.AddMinutes 3.0) key
                |> Expect.equal "expired" (Error LaunchRefusal.LaunchExpired)
            }

            test "empty claims are invalid even under the right seal" {
                LaunchSeal.mint key { claims with PatientId = "" }
                |> LaunchSeal.verify t0 key
                |> Expect.equal "invalid" (Error LaunchRefusal.LaunchInvalid)
            }
        ]


let ids = ref 0
let newId () = ids.Value <- ids.Value + 1; $"id-{ids.Value}"
let verify = LaunchSeal.verify t0 key
let keyA = PublicKey "key-a"
let keyB = PublicKey "key-b"
let launch1 = LaunchSeal.mint key claims
let launch2 = LaunchSeal.mint key { claims with Nonce = "n-2"; PatientId = "p-2" }

let presentWith st launch k =
    SessionStub.present t0 newId verify Patient.empty st (launch, k)


let stubTests =
    testList
        "SessionStub.present over a sealed Launch"
        [
            test "a valid Launch opens a Session for its PatientId and records the nonce" {
                let st, result = presentWith SessionStub.emptyState launch1 keyA

                match result with
                | LaunchResult.Opened(_, session) ->
                    session.PatientContext |> Option.map _.PatientId |> Expect.equal "patient" (Some "p-1")
                    session.KeyThumbprint |> Expect.equal "thumbprint" (Some(PublicKey.thumbprint keyA))
                | other -> failtest $"expected Opened, got {other}"

                st.Launches |> Map.containsKey "n-1" |> Expect.isTrue "record under the nonce"
                st.Sessions |> Map.count |> Expect.equal "one session" 1
            }

            test "the same key within the lifetime is answered as the first time (Rule 2)" {
                let st, first = presentWith SessionStub.emptyState launch1 keyA
                let st2, again = presentWith st launch1 keyA
                again |> Expect.equal "same answer" first
                st2 |> Expect.equal "same state" st
            }

            test "another key is spent and opens nothing" {
                let st, _ = presentWith SessionStub.emptyState launch1 keyA
                let st2, result = presentWith st launch1 keyB
                result |> Expect.equal "spent" (LaunchResult.Refused LaunchRefusal.LaunchSpent)
                st2.Sessions |> Map.count |> Expect.equal "still one" 1
            }

            test "a second Launch with another nonce opens a second Session" {
                let st, _ = presentWith SessionStub.emptyState launch1 keyA
                let st2, result = presentWith st launch2 keyB

                match result with
                | LaunchResult.Opened(_, session) ->
                    session.PatientContext |> Option.map _.PatientId |> Expect.equal "patient" (Some "p-2")
                | other -> failtest $"expected Opened, got {other}"

                st2.Sessions |> Map.count |> Expect.equal "two" 2
            }

            test "not sealed under the key: invalid, nothing recorded" {
                let st, result =
                    presentWith SessionStub.emptyState (LaunchSeal.mint otherKey claims) keyA

                result |> Expect.equal "invalid" (LaunchResult.Refused LaunchRefusal.LaunchInvalid)
                st |> Expect.equal "unchanged" SessionStub.emptyState
            }

            test "past its expiry: expired, even for the same key, and the record is gone" {
                let st, _ = presentWith SessionStub.emptyState launch1 keyA
                let later = t0.AddMinutes 3.0

                let st2, result =
                    SessionStub.present later newId (LaunchSeal.verify later key) Patient.empty st (launch1, keyA)

                result |> Expect.equal "expired" (LaunchResult.Refused LaunchRefusal.LaunchExpired)
                st2.Launches |> Map.containsKey "n-1" |> Expect.isFalse "record dropped"
            }
        ]


let stubLaunchTests =
    testList
        "StubLaunch"
        [
            test "mint gives a Launch that verifies to the posted PatientId, two minutes long" {
                StubLaunch.mint t0 (fun () -> "nonce-x") key "p-9"
                |> LaunchSeal.verify t0 key
                |> Expect.equal
                    "claims"
                    (Ok
                        {
                            LaunchSeal.PatientId = "p-9"
                            LaunchSeal.Nonce = "nonce-x"
                            LaunchSeal.Expiry = t0 + StubLaunch.lifetime
                        })
            }

            test "a blank PatientId falls back to the stub patient" {
                StubLaunch.mint t0 (fun () -> "n") key "  "
                |> LaunchSeal.verify t0 key
                |> Result.map _.PatientId
                |> Expect.equal "stub-patient" (Ok "stub-patient")
            }

            test "the launch url is the hash form with the token escaped" {
                StubLaunch.launchUrl (Launch "a.b")
                |> Expect.equal "url" "/#/session?launch=a.b"

                StubLaunch.launchUrl (Launch "a+b/c=")
                |> Expect.equal "escaped" "/#/session?launch=a%2Bb%2Fc%3D"
            }

            test "the page has no inline script and posts to its own path" {
                StubLaunch.page.Contains "<script" |> Expect.isFalse "no script"
                StubLaunch.page.Contains $"action=\"{StubLaunch.path}\"" |> Expect.isTrue "posts to itself"
                StubLaunch.page.Contains "name=\"pid\"" |> Expect.isTrue "pid field"
            }
        ]


runTestsWithCLIArgs [] [||] (testList "Launch.fsx" [ sealTests; stubTests; stubLaunchTests ]) |> ignore
