// Compute bound to the Session (plan 635), PR 1: the request and reply envelopes, the Session
// marked seen, and the notice that the record moved on. No visible change yet: the client sends
// the token and logs the notice.
//
// Script-first draft (script-only policy) of:
//   - the wire: `RecordNotice` → `Shared/Types.fs`; `Request`, `Reply` and
//     `IServerApi.processCommand: Request -> Async<Result<Reply, string[]>>` → `Shared/Api.fs`;
//   - `Hop`: `SessionRecord.Seen` (Rule 9), `touch`, `seen` (Rules 11, 21, 22) → `Adapters.fs`,
//     with `touch` applied by every port member that takes the session cookie's id (`find`,
//     `challenge`, `submit`, `seen`; `close` excepted, as the model excepts `CloseSession`);
//   - `SessionPort.seen` → `Ports.fs`; `processCommand` reading the cookie → `CompositionRoot.fs`.
//
// Only what changes is re-stated: the state here has the three fields `seen` reads. Run:
// `dotnet fsi Compute.fsx` from this directory (build first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open System
open Shared.Types
open Shared.Models
open ServerApi


// ---------------------------------------------------------------------------------------------
// Wire (→ Shared/Types.fs, Shared/Api.fs)
// ---------------------------------------------------------------------------------------------

/// What a reply says about the Session next to its result. Rules 21, 22: a version newer than
/// the one the request's OpenedToken names exists, whose and when; it gates nothing. Rule 11:
/// the server ended this Session, told at the next request.
[<RequireQualifiedAccess>]
type RecordNotice =
    | NewerVersion of OrderPlanHead
    | Ended of SessionEnding


/// Every computing request: the command and the OpenedToken the Session holds (Rule 34).
/// `None` where there is none to send: no Session, an anonymous one, or a client acting before
/// its first token arrived.
type Request =
    {
        Opened: OpenedToken option
        Command: Shared.Api.Command
    }


/// Every computing reply: the result, and what the Session is told with it.
type Reply =
    {
        Response: Shared.Api.Response
        Notice: RecordNotice option
    }


// ---------------------------------------------------------------------------------------------
// The Session seen, and the notice (→ ServerApi.Adapters.fs, `Hop`)
// ---------------------------------------------------------------------------------------------

module Hop =

    /// A Session as the store holds it: what the client learns, the login it belongs to
    /// (Rule 8), the head of the record it opened with (Rule 19), and when it was last seen
    /// (Rule 9; nothing acts on it yet).
    type SessionRecord =
        {
            Session: SessionOpened
            Login: string option
            OpenedWith: string option
            Seen: DateTime
        }


    /// The state of PR 1: only the fields `seen` reads.
    type State =
        {
            Sessions: Map<string, SessionRecord>
            Endings: Map<string, SessionEnding * DateTime>
            Records: Map<string, SignedOrderPlan list>
        }


    let emptyState =
        {
            Sessions = Map.empty
            Endings = Map.empty
            Records = Map.empty
        }


    /// Rule 19: the most recent signed version of a patient's record, if any.
    let headOf (patientId: string) (state: State) =
        state.Records |> Map.tryFind patientId |> Option.bind List.tryHead


    /// Rule 20: the head of the record, when it is not the version the Session opened with.
    let blockedBy (record: SessionRecord) (patientId: string) (state: State) =
        match headOf patientId state with
        | Some head when Some head.Head.Id <> record.OpenedWith -> Some head.Head
        | _ -> None


    /// Rule 9: a request from the Session refreshes its idle clock. Nothing to refresh when
    /// there is no such Session.
    let touch (now: DateTime) (sid: string) (state: State) : State =
        { state with Sessions = state.Sessions |> Map.change sid (Option.map (fun r -> { r with Seen = now })) }


    /// uc-03 step 1, for every computing request that names a Session: no Session under this
    /// id and an ending recorded for it, the ending (Rule 11); a Session, touched, and Rule 21's
    /// comparison when the token is the Session's own: a newer version than the one it opened
    /// with, whose and when (Rule 22: told, never enforced). An anonymous Session, one without
    /// a Patient, no head, or a token that is not the Session's: nothing to say.
    let seen (now: DateTime) (sid: string) (opened: OpenedToken option) (state: State) : State * RecordNotice option =
        match state.Sessions |> Map.tryFind sid with
        | None ->
            state, state.Endings |> Map.tryFind sid |> Option.map (fst >> RecordNotice.Ended)
        | Some record ->
            let state = touch now sid state

            match record.Session.User, record.Session.PatientContext with
            | Some _, Some patient when opened.IsSome && opened = record.Session.OpenedToken ->
                state, blockedBy record patient.PatientId state |> Option.map RecordNotice.NewerVersion
            | _ -> state, None


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip


let t0 = DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc)
let t1 = t0.AddMinutes 5.0

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
        PatientId = "pat-1"
        Base = if no > 1 then Some $"plan-{no - 1}" else None
        Scenarios = [||]
        Patient = Patient.empty
        Verified = true
    }


let session (user: UserContext option) (patientId: string option) (sid: string) (openedWith: string option) : Hop.SessionRecord =
    {
        Session =
            {
                User = user
                PatientContext =
                    patientId
                    |> Option.map (fun pid ->
                        {
                            PatientId = pid
                            Patient = Patient.empty
                        }
                    )
                OpenedToken = Some(OpenedToken $"opened-{sid}")
                KeyThumbprint = Some "t"
            }
        Login = user |> Option.map _.UserId
        OpenedWith = openedWith
        Seen = t0
    }


let withSession (record: Hop.SessionRecord) sid (state: Hop.State) =
    { state with Sessions = state.Sessions |> Map.add sid record }

let withRecord patientId (versions: SignedOrderPlan list) (state: Hop.State) =
    { state with Records = state.Records |> Map.add patientId versions }

let own sid = Some(OpenedToken $"opened-{sid}")


let tests =
    testList
        "seen (Rules 9, 11, 21, 22)"
        [
            test "an unknown Session with no ending: nothing to say, nothing touched" {
                let state, notice = Hop.emptyState |> Hop.seen t1 "s-1" (own "s-1")
                notice |> Expect.isNone "no notice"
                state |> Expect.equal "unchanged" Hop.emptyState
            }

            test "an unknown Session with an ending recorded: the ending (Rule 11)" {
                let state =
                    { Hop.emptyState with
                        Endings = Map.ofList [ "s-1", (SessionEnding.SupersededByLaunch, t0) ]
                    }

                let _, notice = state |> Hop.seen t1 "s-1" (own "s-1")

                notice
                |> Expect.equal "ended" (Some(RecordNotice.Ended SessionEnding.SupersededByLaunch))
            }

            test "a Session is touched by every seen request (Rule 9)" {
                let state =
                    Hop.emptyState
                    |> withSession (session (Some prescriber) (Some "pat-1") "s-1" None) "s-1"

                let state, _ = state |> Hop.seen t1 "s-1" None
                state.Sessions["s-1"].Seen |> Expect.equal "seen now" t1
            }

            test "touching an unknown Session changes nothing" {
                Hop.emptyState |> Hop.touch t1 "s-9" |> Expect.equal "unchanged" Hop.emptyState
            }

            test "the Session's own token, no head: nothing to say" {
                let state =
                    Hop.emptyState
                    |> withSession (session (Some prescriber) (Some "pat-1") "s-1" None) "s-1"

                let _, notice = state |> Hop.seen t1 "s-1" (own "s-1")
                notice |> Expect.isNone "no notice"
            }

            test "the Session's own token, the head it opened with: nothing to say" {
                let state =
                    Hop.emptyState
                    |> withRecord "pat-1" [ signedBy prescriber 1 t0 ]
                    |> withSession (session (Some prescriber) (Some "pat-1") "s-1" (Some "plan-1")) "s-1"

                let _, notice = state |> Hop.seen t1 "s-1" (own "s-1")
                notice |> Expect.isNone "no notice"
            }

            test "the Session's own token, a newer head: whose and when (Rules 21, 22)" {
                let state =
                    Hop.emptyState
                    |> withRecord "pat-1" [ signedBy other 2 t1; signedBy prescriber 1 t0 ]
                    |> withSession (session (Some prescriber) (Some "pat-1") "s-1" (Some "plan-1")) "s-1"

                let state, notice = state |> Hop.seen t1 "s-1" (own "s-1")

                notice
                |> Expect.equal "newer version" (Some(RecordNotice.NewerVersion (signedBy other 2 t1).Head))

                state.Sessions["s-1"].OpenedWith
                |> Expect.equal "nothing opened: Rule 20 stays the guard" (Some "plan-1")
            }

            test "a Session opened from nothing, a first version signed elsewhere: a notice" {
                let state =
                    Hop.emptyState
                    |> withRecord "pat-1" [ signedBy other 1 t1 ]
                    |> withSession (session (Some prescriber) (Some "pat-1") "s-1" None) "s-1"

                let _, notice = state |> Hop.seen t1 "s-1" (own "s-1")
                notice |> Expect.isSome "newer version"
            }

            test "a token that is not the Session's, a newer head: nothing to say, still touched" {
                let state =
                    Hop.emptyState
                    |> withRecord "pat-1" [ signedBy other 2 t1; signedBy prescriber 1 t0 ]
                    |> withSession (session (Some prescriber) (Some "pat-1") "s-1" (Some "plan-1")) "s-1"

                let state, notice = state |> Hop.seen t1 "s-1" (own "s-2")
                notice |> Expect.isNone "no notice"
                state.Sessions["s-1"].Seen |> Expect.equal "seen" t1

                let _, notice = state |> Hop.seen t1 "s-1" None
                notice |> Expect.isNone "no token, no notice"
            }

            test "an anonymous Session, or one without a Patient: nothing to say" {
                let state =
                    Hop.emptyState
                    |> withRecord "pat-1" [ signedBy other 2 t1 ]
                    |> withSession (session None (Some "pat-1") "s-a" None) "s-a"
                    |> withSession (session (Some prescriber) None "s-n" None) "s-n"

                let _, notice = state |> Hop.seen t1 "s-a" (own "s-a")
                notice |> Expect.isNone "anonymous"

                let _, notice = state |> Hop.seen t1 "s-n" (own "s-n")
                notice |> Expect.isNone "no patient"
            }

            test "the notice is stateless: the same request says it again" {
                let state =
                    Hop.emptyState
                    |> withRecord "pat-1" [ signedBy other 2 t1; signedBy prescriber 1 t0 ]
                    |> withSession (session (Some prescriber) (Some "pat-1") "s-1" (Some "plan-1")) "s-1"

                let state, first = state |> Hop.seen t1 "s-1" (own "s-1")
                let _, second = state |> Hop.seen (t1.AddMinutes 1.0) "s-1" (own "s-1")
                second |> Expect.equal "again" first
            }
        ]


runTestsWithCLIArgs [] [||] tests |> ignore
