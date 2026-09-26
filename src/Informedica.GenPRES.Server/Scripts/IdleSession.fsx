/// A Session left idle ends. A Session that has seen no request for the idle lifetime, an hour
/// unless the site sets its own, ends at the next request that names it: the request is told
/// the ending as it is told the other endings, and a relaunch opens a fresh Session, with the
/// EHR data read again and an identified patient's age computed again. Without it a Session
/// left behind stays open for as long as the server runs, and the age it opened on holds across
/// a day.
///
/// - Shared/Types.fs: `SessionEnding.Idle`, the new case. The script cannot add a case to the
///   compiled contract, so the ending is a parameter of `endIdle` here and the tests pass a
///   stand-in; at migration the parameter goes and the case is named.
/// - Session.fs `endIdle`: a Session not seen for the idle lifetime leaves the open Sessions,
///   its ending is recorded as the other endings are, and an `EndSession` row is written. The
///   check reads `Seen`, which every request but a close refreshes, and the clock the machine
///   already takes; no timer and no sweep. The `Seen` comment loses "nothing acts on it yet".
/// - StubAdapters.fs `idleFirst`: every port member whose slice names a Session (find, seen,
///   age, openVersion, challenge, submit) runs `endIdle` before its own step, so no member
///   signature changes and the SQLite store, which loads a Session per request, gets the same
///   check. A close is not wrapped: it ends the Session anyway.
/// - SqlAdapters.fs: the ending written and read as "idle" beside "wrong-pin-limit".
/// - Server.fs `Config`: GENPRES_SESSION_IDLE_MINUTES, parsed by `parseSessionIdle`; unset or
///   blank is the hour, a positive whole number of minutes is taken, anything else refuses the
///   start, since a lifetime mistyped must not end Sessions at a value nobody chose.
/// - Client.Core `SessionGatePolicy`: the gate's text for the new ending, a new term.
///
/// Run: cd src/Informedica.GenPRES.Server/Scripts && dotnet fsi IdleSession.fsx

#I __SOURCE_DIRECTORY__

#load "load.fsx"

#r "nuget: Expecto, 10.2.3"

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

open System
open Expecto
open Expecto.Flip
open Shared.Types
open ServerApi


// ── Server.fs, the setting ────────────────────────────────────────────────────────────────


module Config =

    /// The idle lifetime when GENPRES_SESSION_IDLE_MINUTES is unset: an hour. A Session that
    /// has seen no request for that long is probably left behind, and a relaunch costs one click.
    let defaultSessionIdle = TimeSpan.FromHours 1.0


    /// GENPRES_SESSION_IDLE_MINUTES as the idle lifetime: unset or blank is the default, a
    /// positive whole number of minutes is taken, anything else is the message the server
    /// refuses to start with.
    let parseSessionIdle (raw: string option) : Result<TimeSpan, string> =
        match raw |> Option.map _.Trim() |> Option.filter (String.IsNullOrEmpty >> not) with
        | None -> Ok defaultSessionIdle
        | Some s ->
            match Int32.TryParse s with
            | true, minutes when minutes > 0 -> Ok(TimeSpan.FromMinutes(float minutes))
            | _ ->
                Error
                    $"GENPRES_SESSION_IDLE_MINUTES is '%s{s}', not a positive whole number of minutes. \
                      Leave it unset for the default of %i{int defaultSessionIdle.TotalMinutes} minutes."


// ── Session.fs, the idle end ──────────────────────────────────────────────────────────────


module Session =

    open ServerApi.Session

    /// Whether a Session has gone the idle lifetime without a request: at the lifetime itself
    /// it has.
    let isIdle (idle: TimeSpan) (now: DateTime) (record: SessionRecord) = now - record.Seen >= idle


    /// A Session left idle ends before the request that names it runs: it leaves the open
    /// Sessions and its ending is recorded, so the request finds the ending and tells it, as a
    /// supersession or the PIN limit is told. An open Session seen within the lifetime, an
    /// ended one and an unknown id are left as they are.
    let endIdle
        (ending: SessionEnding)
        (idle: TimeSpan)
        (now: DateTime)
        (sid: string)
        (state: State)
        : State * Persist list
        =
        match state.Sessions |> Map.tryFind sid with
        | Some record when isIdle idle now record ->
            { state with
                Sessions = state.Sessions |> Map.remove sid
                Endings = state.Endings |> Map.add sid (ending, now)
            },
            [ EndSession(sid, StoredEnding.Ended ending, now) ]
        | _ -> state, []


// ── StubAdapters.fs, the idle end before each member ──────────────────────────────────────


module StubDatabase =

    /// A member's step over a Session, with the idle end run first: the ending's row goes out
    /// ahead of the step's own writes, in the one persist the member makes.
    let idleFirst
        (ending: SessionEnding)
        (idle: TimeSpan)
        (now: DateTime)
        (sid: string)
        (step: Session.State -> Session.State * 'a * Session.Persist list)
        (state: Session.State)
        : Session.State * 'a * Session.Persist list
        =
        let state, ended = Session.endIdle ending idle now sid state
        let state, result, writes = step state
        state, result, ended @ writes


// ── Tests ─────────────────────────────────────────────────────────────────────────────────


/// The contract has no idle case yet: a stand-in, so the tests can say which ending was
/// recorded. At migration this is SessionEnding.Idle.
let idleEnding = SessionEnding.Unreadable

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


let tests =
    testList
        "IdleSession"
        [
            testList
                "parseSessionIdle"
                [
                    for raw, expected in
                        [
                            None, Ok hour
                            Some "", Ok hour
                            Some "   ", Ok hour
                            Some "30", Ok(TimeSpan.FromMinutes 30.0)
                            Some " 90 ", Ok(TimeSpan.FromMinutes 90.0)
                        ] do
                        test $"%A{raw} is %A{expected}" {
                            raw |> Config.parseSessionIdle |> Expect.equal "the lifetime" expected
                        }

                    for raw in [ "0"; "-5"; "1.5"; "an hour"; "60m" ] do
                        test $"'%s{raw}' refuses the start" {
                            Some raw
                            |> Config.parseSessionIdle
                            |> Expect.isError "not a positive whole number of minutes"
                        }
                ]

            testList
                "endIdle"
                [
                    test "a Session seen within the lifetime stays open and writes nothing" {
                        let state = stateSeenAt openedAt
                        let now = openedAt + TimeSpan.FromMinutes 59.0

                        state
                        |> Session.endIdle idleEnding hour now sid
                        |> Expect.equal "unchanged" (state, [])
                    }

                    test "a Session at the lifetime ends, recorded and written" {
                        let now = openedAt + hour
                        let state, writes = stateSeenAt openedAt |> Session.endIdle idleEnding hour now sid

                        state.Sessions |> Map.containsKey sid |> Expect.isFalse "no longer open"

                        state.Endings
                        |> Map.tryFind sid
                        |> Expect.equal "the ending, at now" (Some(idleEnding, now))

                        writes
                        |> Expect.equal
                            "one EndSession row"
                            [ Session.EndSession(sid, Session.StoredEnding.Ended idleEnding, now) ]
                    }

                    test "the lifetime runs from the last request, not from the open" {
                        let seen = openedAt + TimeSpan.FromHours 5.0
                        let now = seen + TimeSpan.FromMinutes 30.0
                        let state = stateSeenAt seen

                        state
                        |> Session.endIdle idleEnding hour now sid
                        |> Expect.equal "still open five and a half hours after the open" (state, [])
                    }

                    test "another Session's idleness is not this request's to end" {
                        let state =
                            { Session.emptyState with
                                Sessions = Map [ sid, record sid openedAt; "s2", record "s2" openedAt ]
                            }

                        let now = openedAt + TimeSpan.FromHours 2.0
                        let state, _ = state |> Session.endIdle idleEnding hour now sid

                        state.Sessions |> Map.containsKey "s2" |> Expect.isTrue "s2 waits for its own request"
                    }

                    test "an ended or unknown Session is left as it is" {
                        let ended =
                            { Session.emptyState with
                                Endings = Map [ (sid, (SessionEnding.WrongPinLimit, openedAt)) ]
                            }

                        let now = openedAt + TimeSpan.FromDays 1.0

                        ended
                        |> Session.endIdle idleEnding hour now sid
                        |> Expect.equal "the earlier ending stands" (ended, [])

                        Session.emptyState
                        |> Session.endIdle idleEnding hour now "nobody"
                        |> Expect.equal "nothing to end" (Session.emptyState, [])
                    }
                ]

            testList
                "idleFirst, over the machine's own members"
                [
                    test "find on an idle Session answers the ending, with the ending's row" {
                        let now = openedAt + TimeSpan.FromHours 2.0

                        let _, lookup, writes =
                            stateSeenAt openedAt
                            |> StubDatabase.idleFirst idleEnding hour now sid (Session.find now sid)

                        lookup |> Expect.equal "told as ended" (SessionLookup.Ended idleEnding)

                        writes
                        |> Expect.equal
                            "the ending, and no heartbeat"
                            [ Session.EndSession(sid, Session.StoredEnding.Ended idleEnding, now) ]
                    }

                    test "find within the lifetime finds the Session and refreshes it" {
                        let now = openedAt + TimeSpan.FromMinutes 40.0

                        let state, lookup, writes =
                            stateSeenAt openedAt
                            |> StubDatabase.idleFirst idleEnding hour now sid (Session.find now sid)

                        match lookup with
                        | SessionLookup.Found _ -> ()
                        | other -> failtest $"expected Found, got %A{other}"

                        writes |> Expect.equal "the heartbeat" [ Session.RecordSeen(sid, now) ]

                        state.Sessions[sid].Seen |> Expect.equal "seen now" now
                    }

                    test "a request within the lifetime keeps the Session alive past the first hour" {
                        let step now state =
                            state |> StubDatabase.idleFirst idleEnding hour now sid (Session.find now sid)

                        let state, _, _ = stateSeenAt openedAt |> step (openedAt + TimeSpan.FromMinutes 50.0)
                        let _, lookup, _ = state |> step (openedAt + TimeSpan.FromMinutes 100.0)

                        match lookup with
                        | SessionLookup.Found _ -> ()
                        | other -> failtest $"expected Found, got %A{other}"
                    }

                    test "seen on an idle Session tells the ending and no age" {
                        let now = openedAt + TimeSpan.FromHours 2.0

                        let _, told, _ =
                            stateSeenAt openedAt
                            |> StubDatabase.idleFirst idleEnding hour now sid (Session.seen now sid None None)

                        told |> Expect.equal "the ending" (Some(RecordNotice.Ended idleEnding), None)
                    }

                    test "the ending is told again on the next request, and written once" {
                        let now = openedAt + TimeSpan.FromHours 2.0
                        let later = now + TimeSpan.FromMinutes 1.0

                        let state, _, _ =
                            stateSeenAt openedAt
                            |> StubDatabase.idleFirst idleEnding hour now sid (Session.find now sid)

                        let _, lookup, writes =
                            state |> StubDatabase.idleFirst idleEnding hour later sid (Session.find later sid)

                        lookup |> Expect.equal "still ended" (SessionLookup.Ended idleEnding)
                        writes |> Expect.isEmpty "nothing written the second time"
                    }

                    test "a close of an idle Session, not wrapped, still closes it" {
                        let now = openedAt + TimeSpan.FromHours 2.0
                        let state, writes = stateSeenAt openedAt |> Session.close now sid

                        state.Sessions |> Map.containsKey sid |> Expect.isFalse "closed"

                        writes
                        |> Expect.equal
                            "closed and acknowledged"
                            [
                                Session.EndSession(sid, Session.StoredEnding.Closed, now)
                                Session.AcknowledgeEnding(sid, now)
                            ]
                    }
                ]
        ]


runTestsWithCLIArgs [] [||] tests
