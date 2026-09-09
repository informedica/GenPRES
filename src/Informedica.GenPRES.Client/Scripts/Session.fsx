// Plan 409, step 3a: the client session state machine.
//
// Launch sequence: docs/scenarios/integration/uc-01-launch.md
// Plan:            docs/implementation-plans/409-client-launch-sequence.md ("Client session
//                  state machine", the rules encoded in `transition`)
//
// Script-only draft. Migration targets:
//   module SessionMachine -> src/Informedica.GenPRES.Client/SessionMachine.fs, compiled before
//                     App.fs (pure F#, opens Shared.Types only, no React; the client's UI
//                     exception lets it be edited directly, but it is drafted here first
//                     because it is logic, not UI)
//   Tests          -> tests/Informedica.GenPRES.Shared.Tests, with SessionMachine.fs linked in
//                     (<Compile Include="../../src/Informedica.GenPRES.Client/SessionMachine.fs" />)
//                     so the machine runs under Expecto in .NET as well as under Fable
//
// Why the file module is `SessionMachine` and not `Session`, as the plan names it: the plan's
// call sites are `Session.Anonymous` (the DU, RequireQualifiedAccess) and `Session.transition`
// (a module function). F# resolves that only with the type-first shadowing pattern of the
// coding standard, `type Session` followed by `module Session`, and that pair cannot live in a
// file-level module that is itself called `Session`: `open Session` then makes `Session.X`
// resolve to the outer module and the union case is reported as unqualified (FS0035). Checked
// in FSI. So the file module is `SessionMachine`, App.fs opens it once, and the plan's call
// sites read exactly as written.
//
// Design points, for the review:
//   - `transition` is total and pure: every (message, state) pair yields a state and a list
//     of effects; App.fs interprets the effects into Cmds. Nothing here touches the browser.
//   - Stale-request guard: an `Outcome` lands only on the `Launching` state that carries the
//     same Launch and public key; a `Resumed` only on `Resuming`. Everything else is dropped.
//   - `RedirectTo` leaves the state `Launching`: the page unloads on `GoTo`, and the client
//     comes back without a Launch, so `init` dispatches `Resume` and the cookie decides.
//   - `Resume` is accepted from `Anonymous` only. `Resumed (Ok None)` and `Resumed (Error _)`
//     both end `Anonymous` with no effects: an anonymous page keeps its URL patient, so no
//     `SetPatient None` here. A transport failure on resume is logged by the interpreter.
//   - `Close` from `Open` asks the server and keeps the state `Open` until `Closed`; the
//     server side deletes the cookie whatever happens (step 2b), so `Closed` is the only
//     ending the client needs.
//   - `OpenAnonymous` is accepted from `Refused` and `Unreachable`; the UI only offers it on
//     `NoRole`, the machine does not care which refusal.
//
// Run: cd src/Informedica.GenPRES.Client/Scripts && dotnet fsi Session.fsx

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.1"
#r "../../Informedica.GenPRES.Shared/bin/Debug/net10.0/Informedica.GenPRES.Shared.dll"

open Shared.Types


/// === src/Informedica.GenPRES.Client/SessionMachine.fs ===
module SessionMachine =

    /// The client's view of its Session, one phase at a time (uc-01 steps 2 to 6).
    [<RequireQualifiedAccess>]
    type Session =
        | Anonymous
        // the presentation in flight, and which attempt it is
        | Launching of Launch * PublicKey * attempt: int
        // GetSession in flight (reload, IdentityProvider return)
        | Resuming
        | Open of SessionOpened
        // no Session; the Launch and key are kept only when a retry is meaningful
        | Refused of LaunchRefusal * retry: (Launch * PublicKey) option
        // ext 3a: server down after the page was served
        | Unreachable of Launch * PublicKey * attempts: int


    [<RequireQualifiedAccess>]
    type SessionMsg =
        // key pair made by Keys.fs, once per page load
        | Present of Launch * PublicKey
        // Error = transport failure
        | Outcome of Launch * PublicKey * Result<LaunchOutcome, string>
        // from Unreachable, or Refused with a retry
        | Retry
        | Resume
        | Resumed of Result<SessionOpened option, string>
        // from #/session?refused={reason}, launch step 4.5
        | RefusedAtCallback of LaunchRefusal
        | OpenAnonymous
        | Close
        | Closed


    [<RequireQualifiedAccess>]
    type SessionEffect =
        | CallPresentLaunch of Launch * PublicKey
        | CallGetSession
        | CallCloseSession
        // window.location.assign, for RedirectTo
        | GoTo of url: string
        // interpreted as UpdatePatient, so everything derived from the patient reloads
        | SetPatient of Patient option
        // Keys.keep: prune the other private keys
        | KeepKey of thumbprint: string


    module Session =

        /// Presentations are attempted this many times before the UI offers Retry.
        let maxAttempts = 3


        /// Whether a refusal answered by presentLaunch itself is worth retrying with the same
        /// Launch and key: only a missing browser identity is (ext 3c).
        let retryable refusal =
            match refusal with
            | LaunchRefusal.NoBrowserIdentity -> true
            | LaunchRefusal.LaunchExpired
            | LaunchRefusal.LaunchSpent
            | LaunchRefusal.LaunchInvalid
            | LaunchRefusal.NoRole
            | LaunchRefusal.WrongActivePatient
            | LaunchRefusal.EnrolmentRequired -> false


        /// The state and effects of a Session that just opened: the patient goes through
        /// UpdatePatient, and the key of this Session is the one to keep.
        let opened (session: SessionOpened) =
            Session.Open session,
            [
                SessionEffect.SetPatient(session.PatientContext |> Option.map _.Patient)
                match session.KeyThumbprint with
                | Some thumbprint -> SessionEffect.KeepKey thumbprint
                | None -> ()
            ]


        let present launch key =
            Session.Launching(launch, key, 1), [ SessionEffect.CallPresentLaunch(launch, key) ]


        let transition (msg: SessionMsg) (state: Session) : Session * SessionEffect list =
            match msg, state with
            // an in-flight presentation is never replaced by a second one for the same Launch
            | SessionMsg.Present(launch, _), Session.Launching(current, _, _) when launch = current -> state, []
            // in every other state a Present starts a fresh presentation; a different Launch
            // supersedes the current one, whose outcome is then dropped by the guard below
            | SessionMsg.Present(launch, key), _ -> present launch key

            // the stale-request guard: an outcome lands only on the presentation that sent it
            | SessionMsg.Outcome(launch, key, result), Session.Launching(current, currentKey, attempt) when
                launch = current && key = currentKey
                ->
                match result with
                | Ok(LaunchOutcome.Opened session) -> opened session
                | Ok(LaunchOutcome.RedirectTo url) -> state, [ SessionEffect.GoTo url ]
                | Ok(LaunchOutcome.Refused refusal) ->
                    let retry = if retryable refusal then Some(launch, key) else None
                    Session.Refused(refusal, retry), []
                | Error _ when attempt < maxAttempts ->
                    Session.Launching(launch, key, attempt + 1), [ SessionEffect.CallPresentLaunch(launch, key) ]
                | Error _ -> Session.Unreachable(launch, key, attempt), []
            | SessionMsg.Outcome _, _ -> state, []

            // a retry always carries the same Launch and the same key (Rule 2)
            | SessionMsg.Retry, Session.Unreachable(launch, key, _) -> present launch key
            | SessionMsg.Retry, Session.Refused(_, Some(launch, key)) -> present launch key
            | SessionMsg.Retry, _ -> state, []

            | SessionMsg.Resume, Session.Anonymous -> Session.Resuming, [ SessionEffect.CallGetSession ]
            | SessionMsg.Resume, _ -> state, []

            | SessionMsg.Resumed(Ok(Some session)), Session.Resuming -> opened session
            | SessionMsg.Resumed _, Session.Resuming -> Session.Anonymous, []
            | SessionMsg.Resumed _, _ -> state, []

            // the Launch was consumed server-side; nothing is left to retry with
            | SessionMsg.RefusedAtCallback refusal, _ -> Session.Refused(refusal, None), []

            // an anonymous open carries nothing over (Rule 7)
            | SessionMsg.OpenAnonymous, Session.Refused _
            | SessionMsg.OpenAnonymous, Session.Unreachable _ -> Session.Anonymous, [ SessionEffect.SetPatient None ]
            | SessionMsg.OpenAnonymous, _ -> state, []

            | SessionMsg.Close, Session.Open _ -> state, [ SessionEffect.CallCloseSession ]
            | SessionMsg.Close, _ -> state, []

            // a launched patient and everything derived from it leave with the session
            | SessionMsg.Closed, _ -> Session.Anonymous, [ SessionEffect.SetPatient None ]


/// === tests/Informedica.GenPRES.Shared.Tests ===
module Tests =

    open Expecto
    open Expecto.Flip
    open SessionMachine


    let launchA = Launch "launch-A"
    let launchB = Launch "launch-B"
    let keyA = PublicKey "key-A"
    let keyB = PublicKey "key-B"

    let sessionWith thumbprint patient =
        {
            User =
                Some
                    {
                        UserId = "u"
                        DisplayName = "U"
                        Role = UserRole.Prescriber
                    }
            PatientContext =
                patient
                |> Option.map (fun p ->
                    {
                        PatientId = "p"
                        Patient = p
                    }
                )
            OpenedToken = Some(OpenedToken "t")
            KeyThumbprint = thumbprint
        }

    let patient = Shared.Models.Patient.empty

    let full = sessionWith (Some "thumb") (Some patient)

    let launching = Session.Launching(launchA, keyA, 1)

    let transition = Session.transition

    /// Apply messages in order from a state, collecting every effect.
    let run state msgs =
        msgs
        |> List.fold
            (fun (s, effects) msg ->
                let s', e = transition msg s
                s', effects @ e
            )
            (state, [])


    let presentTests =
        testList
            "Present"
            [
                test "from Anonymous starts attempt 1 and calls the server" {
                    transition (SessionMsg.Present(launchA, keyA)) Session.Anonymous
                    |> Expect.equal
                        "launching"
                        (Session.Launching(launchA, keyA, 1), [ SessionEffect.CallPresentLaunch(launchA, keyA) ])
                }

                test "the same Launch while it is in flight is a no-op, even with another key" {
                    transition (SessionMsg.Present(launchA, keyB)) (Session.Launching(launchA, keyA, 2))
                    |> Expect.equal "unchanged" (Session.Launching(launchA, keyA, 2), [])
                }

                test "another Launch supersedes the one in flight" {
                    transition (SessionMsg.Present(launchB, keyA)) launching
                    |> Expect.equal
                        "launching B"
                        (Session.Launching(launchB, keyA, 1), [ SessionEffect.CallPresentLaunch(launchB, keyA) ])
                }

                testList
                    "starts a fresh presentation from every other state"
                    [
                        for name, state in
                            [
                                "Open", Session.Open full
                                "Refused", Session.Refused(LaunchRefusal.NoRole, None)
                                "Unreachable", Session.Unreachable(launchB, keyB, 3)
                                "Resuming", Session.Resuming
                            ] do
                            test name {
                                transition (SessionMsg.Present(launchA, keyA)) state
                                |> Expect.equal
                                    "launching"
                                    (Session.Launching(launchA, keyA, 1),
                                     [ SessionEffect.CallPresentLaunch(launchA, keyA) ])
                            }
                    ]
            ]


    let outcomeTests =
        testList
            "Outcome"
            [
                test "Opened sets the patient through UpdatePatient and keeps the session's key" {
                    transition (SessionMsg.Outcome(launchA, keyA, Ok(LaunchOutcome.Opened full))) launching
                    |> Expect.equal
                        "open"
                        (Session.Open full,
                         [
                             SessionEffect.SetPatient(Some patient)
                             SessionEffect.KeepKey "thumb"
                         ])
                }

                test "Opened without a patient sets None; without a thumbprint prunes nothing" {
                    let bare = sessionWith None None

                    transition (SessionMsg.Outcome(launchA, keyA, Ok(LaunchOutcome.Opened bare))) launching
                    |> Expect.equal "open" (Session.Open bare, [ SessionEffect.SetPatient None ])
                }

                test "RedirectTo goes to the url and stays Launching" {
                    transition (SessionMsg.Outcome(launchA, keyA, Ok(LaunchOutcome.RedirectTo "/authorize?x"))) launching
                    |> Expect.equal "redirect" (launching, [ SessionEffect.GoTo "/authorize?x" ])
                }

                test "Refused NoBrowserIdentity keeps the Launch and key for a retry" {
                    transition
                        (SessionMsg.Outcome(launchA, keyA, Ok(LaunchOutcome.Refused LaunchRefusal.NoBrowserIdentity)))
                        launching
                    |> Expect.equal
                        "refused with retry"
                        (Session.Refused(LaunchRefusal.NoBrowserIdentity, Some(launchA, keyA)), [])
                }

                testList
                    "every other refusal keeps nothing"
                    [
                        for refusal in
                            [
                                LaunchRefusal.LaunchExpired
                                LaunchRefusal.LaunchSpent
                                LaunchRefusal.LaunchInvalid
                                LaunchRefusal.NoRole
                                LaunchRefusal.WrongActivePatient
                                LaunchRefusal.EnrolmentRequired
                            ] do
                            test $"{refusal}" {
                                transition (SessionMsg.Outcome(launchA, keyA, Ok(LaunchOutcome.Refused refusal))) launching
                                |> Expect.equal "refused, no retry" (Session.Refused(refusal, None), [])
                            }
                    ]

                test "a transport error retries with the same Launch and key, attempts 1 -> 2 -> 3" {
                    let err = SessionMsg.Outcome(launchA, keyA, Error "down")

                    transition err (Session.Launching(launchA, keyA, 1))
                    |> Expect.equal
                        "attempt 2"
                        (Session.Launching(launchA, keyA, 2), [ SessionEffect.CallPresentLaunch(launchA, keyA) ])

                    transition err (Session.Launching(launchA, keyA, 2))
                    |> Expect.equal
                        "attempt 3"
                        (Session.Launching(launchA, keyA, 3), [ SessionEffect.CallPresentLaunch(launchA, keyA) ])
                }

                test "a transport error on the third attempt is Unreachable" {
                    transition (SessionMsg.Outcome(launchA, keyA, Error "down")) (Session.Launching(launchA, keyA, 3))
                    |> Expect.equal "unreachable" (Session.Unreachable(launchA, keyA, 3), [])
                }

                test "three errors in a row from Anonymous end Unreachable after three calls" {
                    let err = SessionMsg.Outcome(launchA, keyA, Error "down")

                    let state, effects =
                        run Session.Anonymous [ SessionMsg.Present(launchA, keyA); err; err; err ]

                    state |> Expect.equal "unreachable" (Session.Unreachable(launchA, keyA, 3))

                    effects
                    |> List.filter (
                        function
                        | SessionEffect.CallPresentLaunch _ -> true
                        | _ -> false
                    )
                    |> List.length
                    |> Expect.equal "three presentations" 3
                }

                testList
                    "the stale-request guard"
                    [
                        test "an outcome for another Launch is dropped" {
                            transition (SessionMsg.Outcome(launchB, keyA, Ok(LaunchOutcome.Opened full))) launching
                            |> Expect.equal "unchanged" (launching, [])
                        }

                        test "an outcome for another key is dropped" {
                            transition (SessionMsg.Outcome(launchA, keyB, Ok(LaunchOutcome.Opened full))) launching
                            |> Expect.equal "unchanged" (launching, [])
                        }

                        test "an outcome after the presentation was superseded is dropped" {
                            let state, effects =
                                run
                                    Session.Anonymous
                                    [
                                        SessionMsg.Present(launchA, keyA)
                                        SessionMsg.Present(launchB, keyA)
                                        SessionMsg.Outcome(launchA, keyA, Ok(LaunchOutcome.Opened full))
                                    ]

                            state |> Expect.equal "still launching B" (Session.Launching(launchB, keyA, 1))

                            effects
                            |> List.exists (
                                function
                                | SessionEffect.SetPatient _ -> true
                                | _ -> false
                            )
                            |> Expect.isFalse "no patient set from the stale outcome"
                        }

                        testList
                            "an outcome in a state that is not Launching is dropped"
                            [
                                for name, state in
                                    [
                                        "Anonymous", Session.Anonymous
                                        "Open", Session.Open full
                                        "Resuming", Session.Resuming
                                        "Refused", Session.Refused(LaunchRefusal.NoRole, None)
                                        "Unreachable", Session.Unreachable(launchA, keyA, 3)
                                    ] do
                                    test name {
                                        transition (SessionMsg.Outcome(launchA, keyA, Ok(LaunchOutcome.Opened full))) state
                                        |> Expect.equal "unchanged" (state, [])
                                    }
                            ]
                    ]
            ]


    let retryTests =
        testList
            "Retry"
            [
                test "from Unreachable presents the same Launch and key again, attempt 1" {
                    transition SessionMsg.Retry (Session.Unreachable(launchA, keyA, 3))
                    |> Expect.equal
                        "launching"
                        (Session.Launching(launchA, keyA, 1), [ SessionEffect.CallPresentLaunch(launchA, keyA) ])
                }

                test "from Refused with a retry presents the same Launch and key again" {
                    transition SessionMsg.Retry (Session.Refused(LaunchRefusal.NoBrowserIdentity, Some(launchA, keyA)))
                    |> Expect.equal
                        "launching"
                        (Session.Launching(launchA, keyA, 1), [ SessionEffect.CallPresentLaunch(launchA, keyA) ])
                }

                testList
                    "anywhere else is a no-op"
                    [
                        for name, state in
                            [
                                "Anonymous", Session.Anonymous
                                "Launching", launching
                                "Resuming", Session.Resuming
                                "Open", Session.Open full
                                "Refused without retry", Session.Refused(LaunchRefusal.NoRole, None)
                            ] do
                            test name {
                                transition SessionMsg.Retry state |> Expect.equal "unchanged" (state, [])
                            }
                    ]
            ]


    let resumeTests =
        testList
            "Resume and Resumed"
            [
                test "Resume from Anonymous asks for the session" {
                    transition SessionMsg.Resume Session.Anonymous
                    |> Expect.equal "resuming" (Session.Resuming, [ SessionEffect.CallGetSession ])
                }

                test "Resume from any other state is a no-op" {
                    for state in [ launching; Session.Resuming; Session.Open full ] do
                        transition SessionMsg.Resume state |> Expect.equal "unchanged" (state, [])
                }

                test "Resumed with a session opens it like a launch" {
                    transition (SessionMsg.Resumed(Ok(Some full))) Session.Resuming
                    |> Expect.equal "open" (Session.opened full)
                }

                test "Resumed without a session is Anonymous and keeps the url patient (no SetPatient)" {
                    transition (SessionMsg.Resumed(Ok None)) Session.Resuming
                    |> Expect.equal "anonymous" (Session.Anonymous, [])
                }

                test "Resumed with a transport error is Anonymous" {
                    transition (SessionMsg.Resumed(Error "down")) Session.Resuming
                    |> Expect.equal "anonymous" (Session.Anonymous, [])
                }

                test "Resumed outside Resuming is dropped" {
                    for state in [ Session.Anonymous; launching; Session.Open full ] do
                        transition (SessionMsg.Resumed(Ok(Some full))) state
                        |> Expect.equal "unchanged" (state, [])
                }
            ]


    let endingTests =
        testList
            "refusal at the callback, anonymous open, close"
            [
                test "RefusedAtCallback is Refused without a retry, from any state" {
                    for state in [ Session.Anonymous; launching; Session.Resuming; Session.Open full ] do
                        transition (SessionMsg.RefusedAtCallback LaunchRefusal.NoBrowserIdentity) state
                        |> Expect.equal "refused, no retry" (Session.Refused(LaunchRefusal.NoBrowserIdentity, None), [])
                }

                test "OpenAnonymous from Refused clears the patient" {
                    transition SessionMsg.OpenAnonymous (Session.Refused(LaunchRefusal.NoRole, None))
                    |> Expect.equal "anonymous" (Session.Anonymous, [ SessionEffect.SetPatient None ])
                }

                test "OpenAnonymous from Unreachable clears the patient" {
                    transition SessionMsg.OpenAnonymous (Session.Unreachable(launchA, keyA, 3))
                    |> Expect.equal "anonymous" (Session.Anonymous, [ SessionEffect.SetPatient None ])
                }

                test "OpenAnonymous from Open is a no-op: an open session is closed, not abandoned" {
                    transition SessionMsg.OpenAnonymous (Session.Open full)
                    |> Expect.equal "unchanged" (Session.Open full, [])
                }

                test "Close from Open asks the server and stays Open until Closed" {
                    transition SessionMsg.Close (Session.Open full)
                    |> Expect.equal "closing" (Session.Open full, [ SessionEffect.CallCloseSession ])
                }

                test "Close elsewhere is a no-op" {
                    for state in [ Session.Anonymous; launching; Session.Refused(LaunchRefusal.NoRole, None) ] do
                        transition SessionMsg.Close state |> Expect.equal "unchanged" (state, [])
                }

                test "Closed is Anonymous and clears the patient, from any state" {
                    for state in [ Session.Open full; Session.Anonymous; launching ] do
                        transition SessionMsg.Closed state
                        |> Expect.equal "anonymous" (Session.Anonymous, [ SessionEffect.SetPatient None ])
                }

                test "the happy path: present, open, close" {
                    let state, effects =
                        run
                            Session.Anonymous
                            [
                                SessionMsg.Present(launchA, keyA)
                                SessionMsg.Outcome(launchA, keyA, Ok(LaunchOutcome.Opened full))
                                SessionMsg.Close
                                SessionMsg.Closed
                            ]

                    state |> Expect.equal "anonymous again" Session.Anonymous

                    effects
                    |> Expect.equal
                        "effects in order"
                        [
                            SessionEffect.CallPresentLaunch(launchA, keyA)
                            SessionEffect.SetPatient(Some patient)
                            SessionEffect.KeepKey "thumb"
                            SessionEffect.CallCloseSession
                            SessionEffect.SetPatient None
                        ]
                }
            ]


    let tests =
        testList
            "Session.transition"
            [
                presentTests
                outcomeTests
                retryTests
                resumeTests
                endingTests
            ]


Expecto.Tests.runTestsWithCLIArgs [] [||] Tests.tests
