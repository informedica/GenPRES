// The sealed Launch and the stub LaunchScript page (plan 605, PR 1).
//
// Script-first draft (script-only policy) of:
//   - `LaunchSeal`: the Launch of uc-01 step 1 as MainEHR's LaunchScript will mint it — PatientId,
//     a fresh nonce and an expiry sealed under a key shared with the Server (Concept 3, Rules 3,
//     29). Here the key is made once per host start, so a token from an earlier run is "not
//     sealed under the key". → `ServerApi.Adapters.fs`, module `LaunchSeal`.
//   - (superseded by Hop.fsx, PR 2) the sealed-Launch `SessionStub.present`;
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


// The sealed-Launch `SessionStub.present` this script first drafted was superseded by `Hop`
// (Hop.fsx, PR 2), which presents, redirects and answers the callback over the same seal.


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


    /// The page over the stub directory's choices (in the script: listed here).
    let page =
        pageFor [ "prescriber"; "reader"; "prescriber-other-patient"; "no-pin"; "unknown"; "none" ]


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

            test "the page offers every identity choice and names the no-data patient" {
                StubLaunch.page.Contains "<select name=\"identity\">" |> Expect.isTrue "select"

                for c in [ "prescriber"; "reader"; "prescriber-other-patient"; "no-pin"; "unknown"; "none" ] do
                    StubLaunch.page.Contains $"<option value=\"{c}\">" |> Expect.isTrue c

                StubLaunch.page.Contains "no-data" |> Expect.isTrue "no-data hint"
            }
        ]


runTestsWithCLIArgs [] [||] (testList "Launch.fsx" [ sealTests; stubLaunchTests ]) |> ignore
