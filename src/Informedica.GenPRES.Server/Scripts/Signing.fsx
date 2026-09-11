// UC-3 prescribe and sign against server-hosted stubs (plan 622), PR 1: the record and the
// head a Session opens with, and the lock on a credential. No change in behaviour yet.
//
// Script-first draft (script-only policy) of:
//   - the wire: `OrderPlanHead`, `SignedOrderPlan`, `SessionEnding.WrongPinLimit`
//     → `Shared/Types.fs`;
//   - `Credential.LockedUntil`, `wrongPinLimit`, `lockBase`, `lockFor`, `isLocked`, `verify`
//     (Rules 23, 28, as the model's `UserCredential.verify`) → `Adapters.fs`;
//   - `Hop`: `State.Records`, `SessionRecord.OpenedWith`, `headOf`, and `openWith` reading the
//     head at open (Rule 19) → `Adapters.fs`.
//
// The plan's naming: the integration design's TreatmentPlan is the code's `OrderPlan`; a
// signed version of it is a `SignedOrderPlan`.
//
// Only what changes is re-stated; `callback` and `supplyPin` keep calling `openWith`
// unchanged. Run: `dotnet fsi Signing.fsx` from this directory (build first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open System
open Shared.Types
open Shared.Models
open ServerApi


// ---------------------------------------------------------------------------------------------
// Wire (→ Shared/Types.fs)
// ---------------------------------------------------------------------------------------------

/// What identifies a signed version of an order plan (Concept 9): its id, its place in the
/// patient's record (`No` orders the record: a clock cannot say which of two landed first,
/// Rule 20), who signed it and when. Enough for Rule 21's notice: whose, and when.
type OrderPlanHead =
    {
        Id: string
        No: int
        By: UserContext
        SignedAt: DateTime
    }


/// A signed version of an order plan, as the record holds it (the integration design's
/// TreatmentPlan): the head, the patient, the version it was signed over (`Base`, `None` for
/// the first), the orders as shown at the signature and the patient data the User saw
/// (Rule 44).
type SignedOrderPlan =
    {
        Head: OrderPlanHead
        PatientId: string
        Base: string option
        Scenarios: OrderScenario[]
        Patient: Patient
    }


/// Why a Session ended other than by the User closing it (Rule 11). The server says it once,
/// at the next request, and the client shows it. Idle and absolute lifetime (Rule 10) come
/// with their own plan.
[<RequireQualifiedAccess>]
type SessionEnding =
    | SupersededByLaunch
    // Rule 28: the third wrong PIN at a signature
    | WrongPinLimit


// ---------------------------------------------------------------------------------------------
// The credential with its lock (→ ServerApi.Adapters.fs, replacing `Credential`)
// ---------------------------------------------------------------------------------------------

/// Concept 7, the UserCredential as the Database keeps it: the PIN hash, the wrong-count of
/// Rule 28 (counted across Sessions, zeroed when the PIN is set or entered right), and the
/// lock on signing, a moment rather than a state: the delay passes on its own.
type Credential =
    {
        PinHash: PinHash option
        WrongCount: int
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


    /// Rule 28: the delay after `count` wrong entries. The entry that reaches the limit locks
    /// for `lockBase`; each one after it doubles that.
    let lockFor (count: int) = lockBase * float (pown 2 (max 0 (count - wrongPinLimit)))


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
    let attemptsLeft (credential: Credential) = max 0 (wrongPinLimit - credential.WrongCount)


// ---------------------------------------------------------------------------------------------
// The record and the head at open (→ ServerApi.Adapters.fs, `Hop`)
// ---------------------------------------------------------------------------------------------

module Hop =

    /// A Session as the store holds it: what the client learns, the login it belongs to
    /// (Rule 8: a User has at most one open Session), and the head of the record it opened
    /// with (Rule 19; `None` from nothing), which a Submission is checked against (Rule 20).
    type SessionRecord =
        {
            Session: SessionOpened
            Login: string option
            OpenedWith: string option
        }


    /// The state of PR 1: only the fields this script changes; `Launches`, `Endings`, `Codes`
    /// and `Enrolments` stay as `ServerApi.Hop.State` has them.
    type State =
        {
            Sessions: Map<string, SessionRecord>
            Endings: Map<string, SessionEnding * DateTime>
            Credentials: Map<string, Credential>
            // UC-3: the signed versions per patient, newest first
            Records: Map<string, SignedOrderPlan list>
        }


    let emptyState =
        {
            Sessions = Map.empty
            Endings = Map.empty
            Credentials = Map.empty
            Records = Map.empty
        }


    let initialState (credentials: Map<string, Credential>) =
        { emptyState with Credentials = credentials }


    /// Rule 19: the most recent signed version of a patient's record, if any.
    let headOf (patientId: string) (state: State) =
        state.Records |> Map.tryFind patientId |> Option.bind List.tryHead


    /// Step 5.7, one act (Rule 40). The Session is written from the head of the record
    /// (Rule 19), the login's other Sessions are closed and marked (Rule 8). Public here so
    /// the script can test it; private in `Adapters.fs`, reached through `callback` and
    /// `supplyPin`.
    let openWith
        (now: DateTime)
        (newId: unit -> string)
        (patientData: string -> Patient option)
        (patientId: string)
        (key: PublicKey)
        (user: UserContext)
        (state: State)
        =
        let id = newId ()

        let session =
            {
                User = Some user
                PatientContext =
                    Some
                        {
                            PatientId = patientId
                            Patient = patientData patientId |> Option.defaultValue Patient.empty
                        }
                OpenedToken = Some(OpenedToken $"opened-{id}")
                KeyThumbprint = Some(PublicKey.thumbprint key)
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
                        OpenedWith = headOf patientId state |> Option.map _.Head.Id
                    }
            Endings =
                superseded
                |> List.fold (fun m sid -> Map.add sid (SessionEnding.SupersededByLaunch, now) m) state.Endings
        },
        (id, session)


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip


let t0 = DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc)
let minutes (n: float) = TimeSpan.FromMinutes n
let salts (n: int) = Array.init n byte
let withPin = Credential.withPin salts "1234"

let prescriber =
    {
        UserId = "prescriber"
        DisplayName = "Stub Prescriber"
        Role = UserRole.Prescriber
    }

let other =
    { prescriber with
        UserId = "prescriber-b"
        DisplayName = "Stub Prescriber B"
    }


let signedBy (user: UserContext) no (at: DateTime) : SignedOrderPlan =
    {
        Head =
            {
                Id = $"plan-{no}"
                No = no
                By = user
                SignedAt = at
            }
        PatientId = "stub-patient"
        Base = (if no > 1 then Some $"plan-{no - 1}" else None)
        Scenarios = [||]
        Patient = Patient.empty
    }


let counter prefix =
    let n = ref 0

    fun () ->
        n.Value <- n.Value + 1
        $"{prefix}-{n.Value}"


/// Wrong entries in a row, from a credential, each a minute after the last.
let wrong (n: int) (start: DateTime) (credential: Credential) =
    [ 1..n ]
    |> List.fold (fun (c, at) _ -> Credential.verify at "0000" c |> snd, at + minutes 1.0) (credential, start)


let credentialTests =
    testList
        "Credential"
        [
            test "empty and withPin carry no lock, and withPin zeroes the count (Rules 28, 37)" {
                Credential.empty.LockedUntil |> Expect.isNone "empty"
                withPin.LockedUntil |> Expect.isNone "set"
                withPin.WrongCount |> Expect.equal "zero" 0
                withPin |> Credential.pinSet |> Expect.isTrue "set"

                let locked, _ = wrong 4 t0 withPin
                let reset = Credential.withPin salts "2468"
                reset.WrongCount |> Expect.equal "a new PIN zeroes the count" 0
                reset.LockedUntil |> Expect.isNone "and clears the lock"
                locked.WrongCount |> Expect.equal "(the old one stood at four)" 4
            }

            test "the delay is one minute at the limit and doubles with each further entry (Rule 28)" {
                Credential.lockFor 0 |> Expect.equal "below the limit: the base" (minutes 1.0)
                Credential.lockFor 3 |> Expect.equal "at the limit" (minutes 1.0)
                Credential.lockFor 4 |> Expect.equal "one past" (minutes 2.0)
                Credential.lockFor 5 |> Expect.equal "two past" (minutes 4.0)
            }

            test "a right PIN is accepted, zeroes the count and clears the lock" {
                let twoWrong, at = wrong 2 t0 withPin
                twoWrong.WrongCount |> Expect.equal "two" 2
                twoWrong.LockedUntil |> Expect.isNone "not locked yet"
                twoWrong |> Credential.attemptsLeft |> Expect.equal "one left" 1

                let ok, after = Credential.verify at "1234" twoWrong
                ok |> Expect.isTrue "accepted"
                after.WrongCount |> Expect.equal "zeroed" 0
                after.LockedUntil |> Expect.isNone "no lock"
            }

            test "the third wrong PIN locks for a minute; a right PIN inside it is refused and counts nothing" {
                let limit, at = wrong 3 t0 withPin
                limit.WrongCount |> Expect.equal "three" 3
                limit.LockedUntil |> Expect.equal "locked from the third entry" (Some(at - minutes 1.0 + minutes 1.0))
                limit |> Credential.isLocked (at - minutes 0.5) |> Expect.isTrue "locked inside the minute"
                limit |> Credential.attemptsLeft |> Expect.equal "none left" 0

                let ok, same = Credential.verify (at - minutes 0.5) "1234" limit
                ok |> Expect.isFalse "refused while locked"
                same |> Expect.equal "unchanged" limit

                let ok, after = Credential.verify at "1234" limit
                ok |> Expect.isTrue "accepted once the minute passed"
                after.WrongCount |> Expect.equal "zeroed" 0
            }

            test "a wrong PIN while locked counts, and pushes the delay out and doubles it" {
                let limit, at = wrong 3 t0 withPin
                let inside = at - minutes 0.5

                let ok, fourth = Credential.verify inside "0000" limit
                ok |> Expect.isFalse "refused"
                fourth.WrongCount |> Expect.equal "four" 4
                fourth.LockedUntil |> Expect.equal "two minutes from this entry" (Some(inside + minutes 2.0))

                let _, fifth = Credential.verify inside "0000" fourth
                fifth.LockedUntil |> Expect.equal "four minutes" (Some(inside + minutes 4.0))
            }

            test "a credential without a PIN accepts nothing and counts the entry" {
                let ok, after = Credential.verify t0 "1234" Credential.empty
                ok |> Expect.isFalse "no PIN"
                after.WrongCount |> Expect.equal "counted" 1
            }

            test "the stub seed carries no lock" {
                for KeyValue(login, c) in StubCredentials.seed salts do
                    // the seed is the source's Credential; the shape here adds the lock
                    c.WrongCount |> Expect.equal $"{login} count" 0
            }
        ]


let openTests =
    let ids = counter "session"
    let openAs user patientId state = Hop.openWith t0 ids (fun _ -> Some Patient.empty) patientId (PublicKey "key-a") user state

    testList
        "the head at open"
        [
            test "from nothing: no head, and the Session says so (Rule 19)" {
                Hop.headOf "stub-patient" Hop.emptyState |> Expect.isNone "empty record"

                let state, (id, session) = openAs prescriber "stub-patient" Hop.emptyState
                state.Sessions[id].OpenedWith |> Expect.isNone "opened from nothing"
                session.OpenedToken |> Expect.equal "the token, as before" (Some(OpenedToken $"opened-{id}"))
            }

            test "from a record: the newest version is the head, and the Session opened with it" {
                let state =
                    { Hop.emptyState with
                        Records =
                            Map.ofList
                                [
                                    "stub-patient", [ signedBy other 2 (t0 + minutes 1.0); signedBy prescriber 1 t0 ]
                                    "another", [ signedBy prescriber 1 t0 ]
                                ]
                    }

                (Hop.headOf "stub-patient" state |> Option.map _.Head.Id)
                |> Expect.equal "newest first" (Some "plan-2")

                let state, (id, _) = openAs prescriber "stub-patient" state
                state.Sessions[id].OpenedWith |> Expect.equal "the head at open" (Some "plan-2")

                let state, (id2, _) = openAs other "another" state
                state.Sessions[id2].OpenedWith |> Expect.equal "per patient" (Some "plan-1")
            }

            test "a second open of the same login still supersedes the first (Rule 8)" {
                let state, (a, _) = openAs prescriber "stub-patient" Hop.emptyState
                let state, (b, _) = openAs prescriber "stub-patient" state
                state.Sessions |> Map.containsKey a |> Expect.isFalse "a gone"
                state.Sessions |> Map.containsKey b |> Expect.isTrue "b open"

                state.Endings |> Map.tryFind a |> Option.map fst
                |> Expect.equal "marked" (Some SessionEnding.SupersededByLaunch)
            }
        ]


let tests = testList "Signing PR 1" [ credentialTests; openTests ]


runTestsWithCLIArgs [] [||] tests
