namespace Informedica.GenPRES.Shared.Tests


/// The client's session state machine, linked in from the client project (plan 409 step 3a).
module SessionMachineTests =

    open Expecto
    open Expecto.Flip
    open Shared.Types
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
                                "Closing", Session.Closing full
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
                    transition
                        (SessionMsg.Outcome(launchA, keyA, Ok(LaunchOutcome.RedirectTo "/authorize?x")))
                        launching
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
                                transition
                                    (SessionMsg.Outcome(launchA, keyA, Ok(LaunchOutcome.Refused refusal)))
                                    launching
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
                                        "Closing", Session.Closing full
                                    ] do
                                    test name {
                                        transition
                                            (SessionMsg.Outcome(launchA, keyA, Ok(LaunchOutcome.Opened full)))
                                            state
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
                            test name { transition SessionMsg.Retry state |> Expect.equal "unchanged" (state, []) }
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
                    transition (SessionMsg.Resumed(Ok(ResumeResult.Found full))) Session.Resuming
                    |> Expect.equal "open" (Session.opened full)
                }

                test "Resumed without a session is Anonymous and keeps the url patient (no SetPatient)" {
                    transition (SessionMsg.Resumed(Ok ResumeResult.NotFound)) Session.Resuming
                    |> Expect.equal "anonymous" (Session.Anonymous, [])
                }

                test "Resumed with an ending is Ended and acknowledges it with a close (Rule 11)" {
                    transition
                        (SessionMsg.Resumed(Ok(ResumeResult.Ended SessionEnding.SupersededByLaunch)))
                        Session.Resuming
                    |> Expect.equal
                        "ended, acknowledged"
                        (Session.Ended SessionEnding.SupersededByLaunch, [ SessionEffect.CallCloseSession ])
                }

                test "Resumed with a pending enrolment is Enrolling, and a new launch presents (UC-2)" {
                    let pending: EnrolmentPending =
                        {
                            DisplayName = "Stub Prescriber (no PIN)"
                            MailHint = "n***@stub.example"
                        }

                    transition (SessionMsg.Resumed(Ok(ResumeResult.Enrolling pending))) Session.Resuming
                    |> Expect.equal "enrolling" (Session.Enrolling(pending, None), [])

                    transition (SessionMsg.Present(launchB, keyB)) (Session.Enrolling(pending, None))
                    |> Expect.equal "presents" (Session.present launchB keyB)
                }

                test "the form is sent once at a time, and the answer opens, keeps the form, or ends it (UC-2)" {
                    let pending: EnrolmentPending =
                        {
                            DisplayName = "Stub Prescriber (no PIN)"
                            MailHint = "n***@stub.example"
                        }

                    let enrolling = Session.Enrolling(pending, None)
                    let supplying = Session.SupplyingPin pending

                    transition (SessionMsg.SupplyPin("123456", "2468")) enrolling
                    |> Expect.equal "sent" (supplying, [ SessionEffect.CallSupplyPin("123456", "2468") ])

                    transition (SessionMsg.SupplyPin("123456", "2468")) supplying
                    |> Expect.equal "not twice" (supplying, [])

                    let session = sessionWith (Some "t") (Some patient)

                    transition (SessionMsg.PinAnswered(Ok(PinOutcome.Opened session))) supplying
                    |> Expect.equal "opened" (Session.opened session)

                    transition (SessionMsg.PinAnswered(Ok(PinOutcome.Refused(PinRefusal.WrongCode 2)))) supplying
                    |> Expect.equal "form kept" (Session.Enrolling(pending, Some(PinRefusal.WrongCode 2)), [])

                    transition (SessionMsg.PinAnswered(Ok(PinOutcome.Refused PinRefusal.PinFormat))) supplying
                    |> Expect.equal "form kept" (Session.Enrolling(pending, Some PinRefusal.PinFormat), [])

                    for terminal in
                        [
                            PinRefusal.CodeVoid
                            PinRefusal.AttemptExpired
                            PinRefusal.WrongActivePatient
                        ] do
                        transition (SessionMsg.PinAnswered(Ok(PinOutcome.Refused terminal))) supplying
                        |> Expect.equal $"{terminal}" (Session.EnrolmentFailed terminal, [])

                    transition (SessionMsg.PinAnswered(Error "down")) supplying
                    |> Expect.equal "form back" (enrolling, [])

                    // an answer lands only on the request in flight
                    transition (SessionMsg.PinAnswered(Ok(PinOutcome.Opened session))) enrolling
                    |> Expect.equal "dropped" (enrolling, [])
                }

                test "from Ended the anonymous open carries nothing over, and a new launch presents" {
                    let ended = Session.Ended SessionEnding.SupersededByLaunch

                    transition SessionMsg.OpenAnonymous ended
                    |> Expect.equal "anonymous" (Session.Anonymous, [ SessionEffect.SetPatient None ])

                    transition (SessionMsg.Present(launchB, keyB)) ended
                    |> Expect.equal "presents" (Session.present launchB keyB)
                }

                test "Resumed with a transport error is Anonymous" {
                    transition (SessionMsg.Resumed(Error "down")) Session.Resuming
                    |> Expect.equal "anonymous" (Session.Anonymous, [])
                }

                test "Resumed outside Resuming is dropped" {
                    for state in [ Session.Anonymous; launching; Session.Open full ] do
                        transition (SessionMsg.Resumed(Ok(ResumeResult.Found full))) state
                        |> Expect.equal "unchanged" (state, [])
                }
            ]


    let endingTests =
        testList
            "refusal at the callback, anonymous open, close"
            [
                test "RefusedAtCallback is Refused without a retry, from any state" {
                    for state in
                        [
                            Session.Anonymous
                            launching
                            Session.Resuming
                            Session.Open full
                        ] do
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

                test "Close from Open asks the server and is Closing until Closed" {
                    transition SessionMsg.Close (Session.Open full)
                    |> Expect.equal "closing" (Session.Closing full, [ SessionEffect.CallCloseSession ])
                }

                test "Close elsewhere is a no-op, Closing included" {
                    for state in
                        [
                            Session.Anonymous
                            launching
                            Session.Refused(LaunchRefusal.NoRole, None)
                            Session.Closing full
                        ] do
                        transition SessionMsg.Close state |> Expect.equal "unchanged" (state, [])
                }

                test "Closed from Closing is Anonymous and clears the patient" {
                    transition SessionMsg.Closed (Session.Closing full)
                    |> Expect.equal "anonymous" (Session.Anonymous, [ SessionEffect.SetPatient None ])
                }

                test "CloseFailed from Closing returns to Open with the same session and no effects" {
                    transition (SessionMsg.CloseFailed "down") (Session.Closing full)
                    |> Expect.equal "still open" (Session.Open full, [])
                }

                test "CloseFailed outside Closing is dropped" {
                    for state in
                        [
                            Session.Open full
                            Session.Anonymous
                            launching
                            Session.Resuming
                        ] do
                        transition (SessionMsg.CloseFailed "down") state
                        |> Expect.equal "unchanged" (state, [])
                }

                test "Closed outside Closing is dropped" {
                    for state in
                        [
                            Session.Open full
                            Session.Anonymous
                            launching
                            Session.Resuming
                        ] do
                        transition SessionMsg.Closed state |> Expect.equal "unchanged" (state, [])
                }

                test "a close that completes after a newer session opened does not touch it" {
                    let newer = sessionWith (Some "thumb-2") (Some patient)

                    let state, effects =
                        run
                            (Session.Open full)
                            [
                                SessionMsg.Close
                                // a new launch supersedes the close in flight
                                SessionMsg.Present(launchB, keyB)
                                SessionMsg.Outcome(launchB, keyB, Ok(LaunchOutcome.Opened newer))
                                // the old close completes now
                                SessionMsg.Closed
                            ]

                    state |> Expect.equal "the newer session stays open" (Session.Open newer)

                    effects
                    |> Expect.equal
                        "no SetPatient None after the newer session opened"
                        [
                            SessionEffect.CallCloseSession
                            SessionEffect.CallPresentLaunch(launchB, keyB)
                            SessionEffect.SetPatient(Some patient)
                            SessionEffect.KeepKey "thumb-2"
                        ]
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


    [<Tests>]
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
