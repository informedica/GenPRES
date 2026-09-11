// UC-3 prescribe and sign against server-hosted stubs (plan 622), PR 4: a second stub
// Prescriber, `prescriber-b`, so that Rule 20 (the record moved on) can be shown in two
// browsers on one patient.
//
// Script-first draft (script-only policy) of the four stub sites in `Adapters.fs`:
// `StubDirectory.choices`, `identityOf`, `standingOf`, `StubCredentials.seed`. The test runs
// the source's `Hop.callback`, `challenge` and `commit` for two browsers with the shadowed
// registry: B signs, A is blocked with B's head. Run: `dotnet fsi Signing.fsx` from this
// directory (build first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open System
open Shared.Types
open Shared.Models
open ServerApi


// ---------------------------------------------------------------------------------------------
// The stubs (→ ServerApi.Adapters.fs)
// ---------------------------------------------------------------------------------------------

module StubDirectory =

    open ServerApi.StubDirectory

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


module StubCredentials =

    let stubPin = ServerApi.StubCredentials.stubPin


    let seed (newSalt: int -> byte[]) : Map<string, Credential> =
        [
            "prescriber", Credential.withPin newSalt stubPin
            "prescriber-b", Credential.withPin newSalt stubPin
            "prescriber-other-patient", Credential.withPin newSalt stubPin
            "no-pin", Credential.empty
        ]
        |> Map.ofList


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip


let t0 = DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc)
let salts (n: int) = Array.init n byte
let sealKey = LaunchSeal.Key(Array.init LaunchSeal.keyLength byte)
let keyA = PublicKey "key-a"
let keyB = PublicKey "key-b"
let codeMac = Hop.codeMac sealKey


let counter prefix =
    let n = ref 0

    fun () ->
        n.Value <- n.Value + 1
        $"{prefix}-{n.Value}"


let mint nonce =
    LaunchSeal.mint
        sealKey
        {
            PatientId = "stub-patient"
            Nonce = nonce
            Expiry = t0.AddMinutes 2.0
        }


/// The whole hop for `choice` in one browser, with the shadowed registry.
let openAs (ids: unit -> string) (directory: ServerApi.StubDirectory.Directory) nonce key choice state =
    let state, result =
        Hop.present t0 ids (LaunchSeal.verify t0 sealKey) directory.idp.authorizeUrl state (mint nonce, key)

    match result with
    | LaunchResult.RedirectTo(_, st) ->
        let cb =
            {
                State = st
                StateCookie = Some st
                Code = Some(directory.issue choice "stub-patient")
                Error = None
            }

        let state, result =
            Hop.callback
                t0
                ids
                (counter "code")
                codeMac
                directory.idp.redeem
                (StubDirectory.standingOf "stub-patient")
                StubPatientData.port.read
                ignore
                state
                cb

        match result with
        | CallbackResult.Opened(sid, _) -> state, sid
        | other -> failtest $"expected Opened for {choice}, got {other}"
    | other -> failtest $"expected RedirectTo, got {other}"


let tests =
    testList
        "Signing PR 4"
        [
            test "prescriber-b is a Prescriber for the launched patient, with the stub PIN" {
                StubDirectory.choices |> Expect.contains "a choice" "prescriber-b"
                (StubDirectory.identityOf "prescriber-b").DisplayName |> Expect.equal "name" "Stub Prescriber B"

                StubDirectory.standingOf "stub-patient" (StubDirectory.identityOf "prescriber-b")
                |> Option.map (fun s -> s.User.Role, s.ActivePatientId)
                |> Expect.equal "standing" (Some(UserRole.Prescriber, Some "stub-patient"))

                let seed = StubCredentials.seed salts

                seed["prescriber-b"].PinHash
                |> Option.map (PinHash.verify StubCredentials.stubPin)
                |> Expect.equal "the stub PIN" (Some true)
            }

            test "two browsers on one patient: B signs, A is blocked with B's head (Rule 20)" {
                let ids = counter "id"
                let directory = ServerApi.StubDirectory.make (fun () -> t0) (counter "c")
                let state = Hop.initialState (StubCredentials.seed salts)
                let state, a = openAs ids directory "n-a" keyA "prescriber" state
                let state, b = openAs ids directory "n-b" keyB "prescriber-b" state
                state.Sessions |> Map.count |> Expect.equal "both open (different logins)" 2

                let plan = OrderPlan.create Patient.empty [||]
                let tokenOf sid = state.Sessions[sid].Session.OpenedToken.Value
                let state, forA = Hop.challenge t0 ids StubPatientData.port.read a (plan, tokenOf a, None) state
                let state, forB = Hop.challenge t0 ids StubPatientData.port.read b (plan, tokenOf b, None) state

                let nonce =
                    function
                    | SigningResponse.ChallengeIssued n -> n
                    | other -> failtest $"expected ChallengeIssued, got {other}"

                let submission sid challenge key : Submission =
                    {
                        Plan = plan
                        Opened = tokenOf sid
                        Challenge = challenge
                        Pin = StubCredentials.stubPin
                        IdemKey = key
                    }

                let standing = StubDirectory.standingOf "stub-patient"
                let state, signedByB = Hop.commit t0 ids standing ignore b (submission b (nonce forB) "k-b") state

                let head =
                    match signedByB with
                    | SigningResponse.Submitted(signed, _) -> signed.Head
                    | other -> failtest $"expected Submitted, got {other}"

                head.By.UserId |> Expect.equal "B signed" "prescriber-b"

                Hop.commit t0 ids standing ignore a (submission a (nonce forA) "k-a") state
                |> snd
                |> Expect.equal "A blocked by B" (SigningResponse.Refused(SigningRefusal.Blocked head))
            }
        ]


runTestsWithCLIArgs [] [||] tests
