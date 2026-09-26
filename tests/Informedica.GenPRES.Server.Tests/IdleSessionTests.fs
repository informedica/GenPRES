/// A Session left idle ends. A Session that has seen no request for the idle lifetime ends at
/// the next request that names it, and that request, and every one after it, is told the
/// ending; the ending is written once. The lifetime runs from the last request, not from the
/// open, and a request ends only its own Session.
module Informedica.GenPRES.Server.Tests.IdleSessionTests

open System
open Expecto
open Expecto.Flip
open Shared.Types
open ServerApi
open Informedica.GenPRES.Server.Tests.StubAdapterTests.SessionStubTests


let hour = TimeSpan.FromHours 1.0

let openedAt = DateTime(2026, 9, 26, 9, 0, 0)

let prescriber: UserContext =
    {
        UserId = "prescriber"
        DisplayName = "Dr. Stub"
        Role = UserRole.Prescriber
    }


/// A Session as the store holds it after an open, last seen at the time given.
let record (sid: string) (seen: DateTime) : Session.SessionRecord =
    {
        Opened =
            {
                User = Some prescriber
                PatientId = Some "p1"
                EhrData = None
                Patient = None
                Measured = Measurements.none
                OpenedToken = Some(OpenedToken $"opened-{sid}")
                KeyThumbprint = Some "t"
                Head = None
            }
        Login = Some prescriber.UserId
        OpenedWith = None
        OpenedAt = openedAt
        Seen = seen
    }


let sid = "s1"

let stateSeenAt seen =
    { Session.emptyState with Sessions = Map [ (sid, record sid seen) ] }


let ended now =
    [ Session.EndSession(sid, Session.StoredEnding.Ended SessionEnding.Idle, now) ]


/// An in-memory session port over the state given, with a clock the test moves and an idle
/// lifetime of an hour.
let portAt (clock: DateTime ref) (initial: Session.State) =
    let directory = StubDirectory.make (fun () -> clock.Value) (fun () -> $"code-{Guid.NewGuid()}")

    StubDatabase.makeSessionPortWith
        StubDatabase.inMemory
        hour
        (fun () -> clock.Value)
        (fun () -> $"id-{Guid.NewGuid()}")
        (codes ())
        salts
        codeMac
        (fun launch -> LaunchSeal.verify clock.Value sealKey launch)
        directory.idp
        directory.registry
        Informedica.GenPRES.Server.Tests.StubAdapterTests.StubAdapters.patientData
        (StubMail.make ()).port
        initial


let endIdleTests =
    testList
        "Session.endIdle"
        [
            test "a Session seen within the lifetime stays open and writes nothing" {
                let state = stateSeenAt openedAt

                state
                |> Session.endIdle hour (openedAt + TimeSpan.FromMinutes 59.0) sid
                |> Expect.equal "unchanged" (state, [])
            }

            test "a Session at the lifetime ends, recorded and written" {
                let now = openedAt + hour
                let state, writes = stateSeenAt openedAt |> Session.endIdle hour now sid

                state.Sessions |> Map.containsKey sid |> Expect.isFalse "no longer open"
                state.Endings
                |> Map.tryFind sid
                |> Expect.equal "the ending, at now" (Some(SessionEnding.Idle, now))
                writes |> Expect.equal "one EndSession row" (ended now)
            }

            test "the lifetime runs from the last request, not from the open" {
                let seen = openedAt + TimeSpan.FromHours 5.0
                let state = stateSeenAt seen

                state
                |> Session.endIdle hour (seen + TimeSpan.FromMinutes 30.0) sid
                |> Expect.equal "still open five and a half hours after the open" (state, [])
            }

            test "another Session's idleness is not this request's to end" {
                let state =
                    { Session.emptyState with Sessions = Map [ sid, record sid openedAt; "s2", record "s2" openedAt ] }

                let state, _ = state |> Session.endIdle hour (openedAt + TimeSpan.FromHours 2.0) sid

                state.Sessions
                |> Map.containsKey "s2"
                |> Expect.isTrue "s2 waits for its own request"
            }

            test "an ended or unknown Session is left as it is" {
                let endedBefore =
                    { Session.emptyState with Endings = Map [ (sid, (SessionEnding.WrongPinLimit, openedAt)) ] }

                let now = openedAt + TimeSpan.FromDays 1.0

                endedBefore
                |> Session.endIdle hour now sid
                |> Expect.equal "the earlier ending stands" (endedBefore, [])

                Session.emptyState
                |> Session.endIdle hour now "nobody"
                |> Expect.equal "nothing to end" (Session.emptyState, [])
            }
        ]


let idleFirstTests =
    testList
        "StubDatabase.idleFirst"
        [
            test "find on an idle Session answers the ending, with the ending's row and no heartbeat" {
                let now = openedAt + TimeSpan.FromHours 2.0

                let _, lookup, writes =
                    stateSeenAt openedAt
                    |> StubDatabase.idleFirst hour (fun () -> now) sid (fun at -> Session.find at sid)

                lookup |> Expect.equal "told as ended" (SessionLookup.Ended SessionEnding.Idle)
                writes |> Expect.equal "the ending alone" (ended now)
            }

            test "find within the lifetime finds the Session and refreshes it" {
                let now = openedAt + TimeSpan.FromMinutes 40.0

                let state, lookup, writes =
                    stateSeenAt openedAt
                    |> StubDatabase.idleFirst hour (fun () -> now) sid (fun at -> Session.find at sid)

                match lookup with
                | SessionLookup.Found _ -> ()
                | other -> failtest $"expected Found, got %A{other}"

                writes |> Expect.equal "the heartbeat" [ Session.RecordSeen(sid, now) ]
                state.Sessions[sid].Seen |> Expect.equal "seen now" now
            }

            test "the clock is read when the step runs, not when the step is built" {
                let clock = ref (openedAt + TimeSpan.FromMinutes 10.0)

                // built early, as a request builds its step before it waits for the port's lock
                let step = StubDatabase.idleFirst hour (fun () -> clock.Value) sid (fun at -> Session.find at sid)

                clock.Value <- openedAt + TimeSpan.FromMinutes 30.0
                let state, _, _ = stateSeenAt openedAt |> step

                state.Sessions[sid].Seen |> Expect.equal "the time it ran" clock.Value
            }
        ]


let portTests =
    testList
        "the session port"
        [
            testAsync "a request within the lifetime keeps the Session alive past the first hour" {
                let clock = ref openedAt
                let port = portAt clock (stateSeenAt openedAt)

                clock.Value <- openedAt + TimeSpan.FromMinutes 50.0
                let! _ = port.find sid

                clock.Value <- openedAt + TimeSpan.FromMinutes 100.0

                match! port.find sid with
                | SessionLookup.Found _ -> ()
                | other -> failtest $"expected Found, got %A{other}"
            }

            testAsync "a Session left idle is told the ending, on this request and the next" {
                let clock = ref (openedAt + TimeSpan.FromHours 2.0)
                let port = portAt clock (stateSeenAt openedAt)

                match! port.find sid with
                | SessionLookup.Ended SessionEnding.Idle -> ()
                | other -> failtest $"expected ended idle, got %A{other}"

                clock.Value <- clock.Value + TimeSpan.FromMinutes 1.0

                match! port.find sid with
                | SessionLookup.Ended SessionEnding.Idle -> ()
                | other -> failtest $"expected still ended idle, got %A{other}"
            }

            testAsync "a computing request in an idle Session is told the ending and no age" {
                let clock = ref (openedAt + TimeSpan.FromHours 2.0)
                let port = portAt clock (stateSeenAt openedAt)

                let! told = port.seen sid None None

                told
                |> Expect.equal "the ending" (Some(RecordNotice.Ended SessionEnding.Idle), None)
            }

            testAsync "a signing request keeps the Session alive, also one refused before its challenge" {
                let clock = ref openedAt
                let port = portAt clock (stateSeenAt openedAt)

                // the signing member asks the age first, and may be refused right after it
                clock.Value <- openedAt + TimeSpan.FromMinutes 50.0
                let! _ = port.age sid

                clock.Value <- openedAt + TimeSpan.FromMinutes 100.0

                match! port.find sid with
                | SessionLookup.Found _ -> ()
                | other -> failtest $"expected Found, got %A{other}"
            }

            testAsync "a signing request's age in an idle Session is none" {
                let clock = ref (openedAt + TimeSpan.FromHours 2.0)
                let port = portAt clock (stateSeenAt openedAt)

                let! age = port.age sid

                age |> Expect.isNone "no Session to hold an age"
            }
        ]


[<Tests>]
let tests = testList "IdleSession" [ endIdleTests; idleFirstTests; portTests ]
