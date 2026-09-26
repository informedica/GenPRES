namespace Informedica.GenPRES.Client.Core.Tests


/// The client's session state machine, linked in from the client project.
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
                        Identity = None
                        Patient = Some p
                    }
                )
            OpenedToken = Some(OpenedToken "t")
            KeyThumbprint = thumbprint
            Head = None
        }

    let patient = Shared.Models.Patient.empty

    let full = sessionWith (Some "thumb") (Some patient)

    /// The patient as the Session holds it after a sign: a day older than at the open.
    let aged =
        { patient with
            Age =
                Some
                    { Shared.Models.Patient.Age.ageZero with
                        Age.Years = 10<year>
                        Age.Days = 1<day>
                    }
        }

    /// A Session renewed after a sign: the token, and the patient in the context it holds.
    let renewed (session: SessionOpened) =
        { session with
            OpenedToken = Some(OpenedToken "t2")
            PatientContext = session.PatientContext |> Option.map (fun c -> { c with Patient = Some aged })
        }

    /// The record's transition: the DU's tests, on the constructors.
    module StateTransition =

        let launching = SessionState.launching launchA keyA 1

        let transition = SessionState.transition

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
                        transition (SessionMsg.Present(launchA, keyA)) SessionState.anonymous
                        |> Expect.equal "launching" (launching, [ SessionEffect.CallPresentLaunch(launchA, keyA) ])
                    }

                    test "the same Launch while it is in flight is a no-op, even with another key" {
                        transition (SessionMsg.Present(launchA, keyB)) (SessionState.launching launchA keyA 2)
                        |> Expect.equal "unchanged" (SessionState.launching launchA keyA 2, [])
                    }

                    test "another Launch supersedes the one in flight" {
                        transition (SessionMsg.Present(launchB, keyA)) launching
                        |> Expect.equal
                            "launching B"
                            (SessionState.launching launchB keyA 1, [ SessionEffect.CallPresentLaunch(launchB, keyA) ])
                    }

                    testList
                        "starts a fresh presentation from every other state"
                        [
                            for name, state in
                                [
                                    "Open", SessionState.opened full None
                                    "Refused", SessionState.refused LaunchRefusal.NoRole
                                    // the same Launch kept after a refusal, or the server unreachable,
                                    // is presented afresh: only a presentation under way is kept
                                    "Retryable, the same Launch",
                                    SessionState.retryable LaunchRefusal.NoBrowserIdentity launchA keyA
                                    "Unreachable", SessionState.unreachable launchB keyB
                                    "Unreachable, the same Launch", SessionState.unreachable launchA keyA
                                    "Resuming", SessionState.resuming
                                    "Closing", SessionState.closing full
                                ] do
                                test name {
                                    transition (SessionMsg.Present(launchA, keyA)) state
                                    |> Expect.equal
                                        "launching"
                                        (launching, [ SessionEffect.CallPresentLaunch(launchA, keyA) ])
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
                            (SessionState.opened full None,
                             [ SessionEffect.SetPatient(Some patient); SessionEffect.KeepKey "thumb" ])
                    }

                    test "Opened over a record loads its head into the cart, after the patient" {
                        let head: SignedOrderPlan =
                            {
                                Head =
                                    {
                                        Id = "plan-1"
                                        No = 1
                                        By = full.User.Value
                                        SignedAt = System.DateTime(2026, 9, 11, 12, 0, 0, System.DateTimeKind.Utc)
                                    }
                                PatientId = "p"
                                Base = None
                                OrderContexts = [||]
                                Patient = patient
                                Identity = None
                                Verified = true
                            }

                        let over = { full with Head = Some head }

                        transition (SessionMsg.Outcome(launchA, keyA, Ok(LaunchOutcome.Opened over))) launching
                        |> Expect.equal
                            "open"
                            (SessionState.opened over None,
                             [
                                 SessionEffect.SetPatient(Some patient)
                                 SessionEffect.KeepKey "thumb"
                                 SessionEffect.LoadCart head
                             ])

                        // a resume opens the same way
                        transition (SessionMsg.Resumed(Ok(ResumeResult.Found over))) SessionState.resuming
                        |> snd
                        |> List.contains (SessionEffect.LoadCart head)
                        |> Expect.isTrue "loaded at resume"

                        // no patient, no cart to load, whatever the head says
                        let bare = { sessionWith None None with Head = Some head }

                        transition (SessionMsg.Outcome(launchA, keyA, Ok(LaunchOutcome.Opened bare))) launching
                        |> snd
                        |> Expect.equal "nothing to load" [ SessionEffect.SetPatient None ]
                    }

                    test "Opened without a patient sets None; without a thumbprint prunes nothing" {
                        let bare = sessionWith None None

                        transition (SessionMsg.Outcome(launchA, keyA, Ok(LaunchOutcome.Opened bare))) launching
                        |> Expect.equal "open" (SessionState.opened bare None, [ SessionEffect.SetPatient None ])
                    }

                    test "RedirectTo goes to the url and stays Launching" {
                        transition
                            (SessionMsg.Outcome(launchA, keyA, Ok(LaunchOutcome.RedirectTo "/authorize?x")))
                            launching
                        |> Expect.equal "redirect" (launching, [ SessionEffect.GoTo "/authorize?x" ])
                    }

                    test "Refused NoBrowserIdentity keeps the Launch and key for a retry" {
                        transition
                            (SessionMsg.Outcome(
                                launchA,
                                keyA,
                                Ok(LaunchOutcome.Refused LaunchRefusal.NoBrowserIdentity)
                            ))
                            launching
                        |> Expect.equal
                            "refused with retry"
                            (SessionState.retryable LaunchRefusal.NoBrowserIdentity launchA keyA, [])
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
                                    |> Expect.equal "refused, no retry" (SessionState.refused refusal, [])
                                }
                        ]

                    test "a transport error retries with the same Launch and key, attempts 1 -> 2 -> 3" {
                        let err = SessionMsg.Outcome(launchA, keyA, Error "down")

                        transition err (SessionState.launching launchA keyA 1)
                        |> Expect.equal
                            "attempt 2"
                            (SessionState.launching launchA keyA 2, [ SessionEffect.CallPresentLaunch(launchA, keyA) ])

                        transition err (SessionState.launching launchA keyA 2)
                        |> Expect.equal
                            "attempt 3"
                            (SessionState.launching launchA keyA 3, [ SessionEffect.CallPresentLaunch(launchA, keyA) ])
                    }

                    test "a transport error on the third attempt is Unreachable" {
                        transition
                            (SessionMsg.Outcome(launchA, keyA, Error "down"))
                            (SessionState.launching launchA keyA 3)
                        |> Expect.equal "unreachable" (SessionState.unreachable launchA keyA, [])
                    }

                    test "three errors in a row from Anonymous end Unreachable after three calls" {
                        let err = SessionMsg.Outcome(launchA, keyA, Error "down")

                        let state, effects =
                            run SessionState.anonymous [ SessionMsg.Present(launchA, keyA); err; err; err ]

                        state |> Expect.equal "unreachable" (SessionState.unreachable launchA keyA)

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
                                        SessionState.anonymous
                                        [
                                            SessionMsg.Present(launchA, keyA)
                                            SessionMsg.Present(launchB, keyA)
                                            SessionMsg.Outcome(launchA, keyA, Ok(LaunchOutcome.Opened full))
                                        ]

                                state
                                |> Expect.equal "still launching B" (SessionState.launching launchB keyA 1)

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
                                            "Anonymous", SessionState.anonymous
                                            "Open", SessionState.opened full None
                                            "Resuming", SessionState.resuming
                                            "Refused", SessionState.refused LaunchRefusal.NoRole
                                            // the Launch is kept, but nothing is under way to answer
                                            "Retryable",
                                            SessionState.retryable LaunchRefusal.NoBrowserIdentity launchA keyA
                                            "Unreachable", SessionState.unreachable launchA keyA
                                            "Closing", SessionState.closing full
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
                        transition SessionMsg.Retry (SessionState.unreachable launchA keyA)
                        |> Expect.equal "launching" (launching, [ SessionEffect.CallPresentLaunch(launchA, keyA) ])
                    }

                    test "from Refused with a retry presents the same Launch and key again" {
                        transition
                            SessionMsg.Retry
                            (SessionState.retryable LaunchRefusal.NoBrowserIdentity launchA keyA)
                        |> Expect.equal "launching" (launching, [ SessionEffect.CallPresentLaunch(launchA, keyA) ])
                    }

                    testList
                        "anywhere else is a no-op"
                        [
                            for name, state in
                                [
                                    "Anonymous", SessionState.anonymous
                                    "Launching", launching
                                    "Resuming", SessionState.resuming
                                    "Open", SessionState.opened full None
                                    "Refused without retry", SessionState.refused LaunchRefusal.NoRole
                                ] do
                                test name { transition SessionMsg.Retry state |> Expect.equal "unchanged" (state, []) }
                        ]
                ]


        let resumeTests =
            testList
                "Resume and Resumed"
                [
                    test "Resume from Anonymous asks for the session" {
                        transition SessionMsg.Resume SessionState.anonymous
                        |> Expect.equal "resuming" (SessionState.resuming, [ SessionEffect.CallGetSession ])
                    }

                    test "Resume from any other state is a no-op" {
                        for state in [ launching; SessionState.resuming; SessionState.opened full None ] do
                            transition SessionMsg.Resume state |> Expect.equal "unchanged" (state, [])
                    }

                    test "Resumed with a session opens it like a launch" {
                        transition (SessionMsg.Resumed(Ok(ResumeResult.Found full))) SessionState.resuming
                        |> Expect.equal "open" (SessionState.onOpened full)
                    }

                    test "Resumed without a session is Anonymous and keeps the url patient (no SetPatient)" {
                        transition (SessionMsg.Resumed(Ok ResumeResult.NotFound)) SessionState.resuming
                        |> Expect.equal "anonymous" (SessionState.anonymous, [])
                    }

                    test "Resumed with an ending is Ended and acknowledges it with a close" {
                        transition
                            (SessionMsg.Resumed(Ok(ResumeResult.Ended SessionEnding.SupersededByLaunch)))
                            SessionState.resuming
                        |> Expect.equal
                            "ended, acknowledged"
                            (SessionState.ended SessionEnding.SupersededByLaunch, [ SessionEffect.CallCloseSession ])
                    }

                    test "Resumed with a pending enrolment is Enrolling, and a new launch presents" {
                        let pending: EnrolmentPending =
                            {
                                DisplayName = "Stub Prescriber (no PIN)"
                                MailHint = "n***@stub.example"
                            }

                        transition (SessionMsg.Resumed(Ok(ResumeResult.Enrolling pending))) SessionState.resuming
                        |> Expect.equal "enrolling" (SessionState.enrolling pending None, [])

                        transition (SessionMsg.Present(launchB, keyB)) (SessionState.enrolling pending None)
                        |> Expect.equal "presents" (SessionState.present launchB keyB)
                    }

                    test "from Ended the anonymous open carries nothing over, and a new launch presents" {
                        let ended = SessionState.ended SessionEnding.SupersededByLaunch

                        transition SessionMsg.OpenAnonymous ended
                        |> Expect.equal "anonymous" (SessionState.anonymous, [ SessionEffect.SetPatient None ])

                        transition (SessionMsg.Present(launchB, keyB)) ended
                        |> Expect.equal "presents" (SessionState.present launchB keyB)
                    }

                    test "the anonymous open from a refusal, retryable or not, and from the server unreachable" {
                        for state in
                            [
                                SessionState.refused LaunchRefusal.NoRole
                                SessionState.retryable LaunchRefusal.NoBrowserIdentity launchA keyA
                                SessionState.unreachable launchA keyA
                            ] do
                            transition SessionMsg.OpenAnonymous state
                            |> Expect.equal "anonymous" (SessionState.anonymous, [ SessionEffect.SetPatient None ])

                        for state in [ SessionState.anonymous; launching; SessionState.opened full None ] do
                            transition SessionMsg.OpenAnonymous state
                            |> Expect.equal "unchanged" (state, [])
                    }

                    test "a refusal at the callback keeps nothing, whatever was under way" {
                        for state in [ SessionState.anonymous; launching; SessionState.opened full None ] do
                            transition (SessionMsg.RefusedAtCallback LaunchRefusal.LaunchSpent) state
                            |> Expect.equal "refused, no retry" (SessionState.refused LaunchRefusal.LaunchSpent, [])
                    }

                    test "Resumed with a transport error is Anonymous" {
                        transition (SessionMsg.Resumed(Error "down")) SessionState.resuming
                        |> Expect.equal "anonymous" (SessionState.anonymous, [])
                    }

                    test "Resumed outside Resuming is dropped" {
                        for state in [ SessionState.anonymous; launching; SessionState.opened full None ] do
                            transition (SessionMsg.Resumed(Ok(ResumeResult.Found full))) state
                            |> Expect.equal "unchanged" (state, [])
                    }
                ]


        let pinTests =
            testList
                "the PIN"
                [
                    test "the form is sent once at a time, and the answer opens, keeps the form, or ends it" {
                        let pending: EnrolmentPending =
                            {
                                DisplayName = "Stub Prescriber (no PIN)"
                                MailHint = "n***@stub.example"
                            }

                        let enrolling = SessionState.enrolling pending None
                        let supplying = SessionState.supplyingPin pending

                        transition (SessionMsg.SupplyPin("123456", "2468")) enrolling
                        |> Expect.equal "sent" (supplying, [ SessionEffect.CallSupplyPin("123456", "2468") ])

                        transition (SessionMsg.SupplyPin("123456", "2468")) supplying
                        |> Expect.equal "not twice" (supplying, [])

                        // the refusal the form is answering is spent by the next sending
                        transition
                            (SessionMsg.SupplyPin("123456", "2468"))
                            (SessionState.enrolling pending (Some(PinRefusal.WrongCode 2)))
                        |> Expect.equal "sent again" (supplying, [ SessionEffect.CallSupplyPin("123456", "2468") ])

                        let session = sessionWith (Some "t") (Some patient)

                        transition (SessionMsg.PinAnswered(Ok(PinOutcome.Opened session))) supplying
                        |> Expect.equal "opened" (SessionState.onOpened session)

                        transition (SessionMsg.PinAnswered(Ok(PinOutcome.Refused(PinRefusal.WrongCode 2)))) supplying
                        |> Expect.equal "form kept" (SessionState.enrolling pending (Some(PinRefusal.WrongCode 2)), [])

                        transition (SessionMsg.PinAnswered(Ok(PinOutcome.Refused PinRefusal.PinFormat))) supplying
                        |> Expect.equal "form kept" (SessionState.enrolling pending (Some PinRefusal.PinFormat), [])

                        for terminal in
                            [
                                PinRefusal.CodeVoid
                                PinRefusal.AttemptExpired
                                PinRefusal.WrongActivePatient
                            ] do
                            transition (SessionMsg.PinAnswered(Ok(PinOutcome.Refused terminal))) supplying
                            |> Expect.equal $"{terminal}" (SessionState.enrolmentFailed terminal, [])

                        transition (SessionMsg.PinAnswered(Error "down")) supplying
                        |> Expect.equal "form back" (enrolling, [])

                        // an answer lands only on the request in flight
                        transition (SessionMsg.PinAnswered(Ok(PinOutcome.Opened session))) enrolling
                        |> Expect.equal "dropped" (enrolling, [])
                    }
                ]


        let endingTests =
            testList
                "close, the endings, the token"
                [
                    test "Close from Open asks the server and is Closing until Closed" {
                        transition SessionMsg.Close (SessionState.opened full None)
                        |> Expect.equal "closing" (SessionState.closing full, [ SessionEffect.CallCloseSession ])
                    }

                    test "Close elsewhere is a no-op, Closing included" {
                        for state in
                            [
                                SessionState.anonymous
                                launching
                                SessionState.refused LaunchRefusal.NoRole
                                SessionState.closing full
                            ] do
                            transition SessionMsg.Close state |> Expect.equal "unchanged" (state, [])
                    }

                    test "Closed from Closing is Anonymous and clears the patient" {
                        transition SessionMsg.Closed (SessionState.closing full)
                        |> Expect.equal "anonymous" (SessionState.anonymous, [ SessionEffect.SetPatient None ])
                    }

                    test "CloseFailed from Closing returns to Open with the same session and no effects" {
                        transition (SessionMsg.CloseFailed "down") (SessionState.closing full)
                        |> Expect.equal "still open" (SessionState.opened full None, [])
                    }

                    test "CloseFailed outside Closing is dropped" {
                        for state in
                            [
                                SessionState.opened full None
                                SessionState.anonymous
                                launching
                                SessionState.resuming
                            ] do
                            transition (SessionMsg.CloseFailed "down") state
                            |> Expect.equal "unchanged" (state, [])
                    }

                    test "Closed outside Closing is dropped" {
                        for state in
                            [
                                SessionState.opened full None
                                SessionState.anonymous
                                launching
                                SessionState.resuming
                            ] do
                            transition SessionMsg.Closed state |> Expect.equal "unchanged" (state, [])
                    }

                    test "a close that completes after a newer session opened does not touch it" {
                        let newer = sessionWith (Some "thumb-2") (Some patient)

                        let state, effects =
                            run
                                (SessionState.opened full None)
                                [
                                    SessionMsg.Close
                                    // a new launch supersedes the close in flight
                                    SessionMsg.Present(launchB, keyB)
                                    SessionMsg.Outcome(launchB, keyB, Ok(LaunchOutcome.Opened newer))
                                    // the old close completes now
                                    SessionMsg.Closed
                                ]

                        state
                        |> Expect.equal "the newer session stays open" (SessionState.opened newer None)

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

                    test "EndedByServer from Open is Ended and acknowledges it with a close; elsewhere dropped" {
                        transition
                            (SessionMsg.EndedByServer SessionEnding.WrongPinLimit)
                            (SessionState.opened full None)
                        |> Expect.equal
                            "ended"
                            (SessionState.ended SessionEnding.WrongPinLimit, [ SessionEffect.CallCloseSession ])

                        for state in
                            [
                                SessionState.anonymous
                                SessionState.closing full
                                SessionState.resuming
                                launching
                            ] do
                            transition (SessionMsg.EndedByServer SessionEnding.WrongPinLimit) state
                            |> Expect.equal $"{state}" (state, [])
                    }

                    test
                        "TokenRenewed from Open replaces the token and takes the patient, to the panel as at a resume; elsewhere dropped" {
                        transition (SessionMsg.TokenRenewed(OpenedToken "t2", aged)) (SessionState.opened full None)
                        |> Expect.equal
                            "renewed"
                            (SessionState.opened (renewed full) None, [ SessionEffect.SetPatient(Some aged) ])

                        // the same SetPatient a resume of the renewed Session gives
                        SessionState.onOpened (renewed full)
                        |> snd
                        |> List.head
                        |> Expect.equal "as at a resume" (SessionEffect.SetPatient(Some aged))

                        // a Session without a patient context: the token only
                        let none = sessionWith (Some "thumb") None

                        transition (SessionMsg.TokenRenewed(OpenedToken "t2", aged)) (SessionState.opened none None)
                        |> Expect.equal
                            "the token only"
                            (SessionState.opened { none with OpenedToken = Some(OpenedToken "t2") } None,
                             [ SessionEffect.SetPatient(Some aged) ])

                        for state in [ SessionState.anonymous; SessionState.closing full; launching ] do
                            transition (SessionMsg.TokenRenewed(OpenedToken "t2", aged)) state
                            |> Expect.equal $"{state}" (state, [])
                    }

                    test "the happy path: present, open, close" {
                        let state, effects =
                            run
                                SessionState.anonymous
                                [
                                    SessionMsg.Present(launchA, keyA)
                                    SessionMsg.Outcome(launchA, keyA, Ok(LaunchOutcome.Opened full))
                                    SessionMsg.Close
                                    SessionMsg.Closed
                                ]

                        state |> Expect.equal "anonymous again" SessionState.anonymous

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


        let openVersionTests =
            let head: SignedOrderPlan =
                {
                    Head =
                        {
                            Id = "plan-2"
                            No = 2
                            By = full.User.Value
                            SignedAt = System.DateTime(2026, 9, 11, 12, 0, 0, System.DateTimeKind.Utc)
                        }
                    PatientId = "p"
                    Base = Some "plan-1"
                    OrderContexts = [||]
                    Patient = patient
                    Identity = None
                    Verified = true
                }

            let reopened =
                { full with
                    OpenedToken = Some(OpenedToken "t-2")
                    Head = Some head
                }

            testList
                "OpenVersion"
                [
                    test "OpenVersion from Open calls the server with the token it starts from; elsewhere dropped" {
                        transition (SessionMsg.OpenVersion "plan-2") (SessionState.opened full None)
                        |> Expect.equal
                            "call"
                            (SessionState.opened full None,
                             [ SessionEffect.CallOpenVersion("plan-2", full.OpenedToken) ])

                        for state in [ SessionState.anonymous; SessionState.closing full ] do
                            transition (SessionMsg.OpenVersion "plan-2") state
                            |> Expect.equal "dropped" (state, [])
                    }

                    test "Reopened with the Session replaces it, loads the version into the cart and tells it" {
                        transition
                            (SessionMsg.Reopened(full.OpenedToken, Ok(Some reopened)))
                            (SessionState.opened full None)
                        |> Expect.equal
                            "reopened"
                            (SessionState.opened reopened None,
                             [ SessionEffect.LoadCart head; SessionEffect.TellVersionOpened head.Head ])
                    }

                    test "MovedOn.receive: news once per version, ordered by No, not by arrival (Rules 20 to 22)" {
                        let first = head.Head

                        let second =
                            { first with
                                Id = "plan-3"
                                No = 3
                            }

                        MovedOn.receive None first |> Expect.equal "first: news" (Some first, true)

                        MovedOn.receive (Some first) first
                        |> Expect.equal "again: not news" (Some first, false)

                        MovedOn.receive (Some first) second
                        |> Expect.equal "newer: news" (Some second, true)

                        // replies land out of order: an older version told last is not news and is not kept
                        MovedOn.receive (Some second) first
                        |> Expect.equal "older: nothing" (Some second, false)
                    }

                    test "MovedOn.opened: the notice is spent by a version at least as new, a newer notice stays" {
                        let two = head.Head

                        let three =
                            { two with
                                Id = "plan-3"
                                No = 3
                            }

                        MovedOn.opened (Some two) two |> Expect.isNone "the version told is open"
                        MovedOn.opened (Some two) three |> Expect.isNone "a newer one is open"
                        MovedOn.opened None two |> Expect.isNone "nothing kept"

                        MovedOn.opened (Some three) two
                        |> Expect.equal "version 3 told while 2 was opening: the offer stays" (Some three)
                    }

                    test "Reopened with nothing to open, or a transport failure, leaves the Session as it was" {
                        transition (SessionMsg.Reopened(full.OpenedToken, Ok None)) (SessionState.opened full None)
                        |> Expect.equal "nothing to open" (SessionState.opened full None, [])

                        transition
                            (SessionMsg.Reopened(full.OpenedToken, Error "offline"))
                            (SessionState.opened full None)
                        |> Expect.equal "failed" (SessionState.opened full None, [])
                    }

                    test "Reopened lands only on the open Session that still holds the token it started from" {
                        transition (SessionMsg.Reopened(full.OpenedToken, Ok(Some reopened))) SessionState.anonymous
                        |> Expect.equal "not open: dropped" (SessionState.anonymous, [])

                        transition
                            (SessionMsg.Reopened(full.OpenedToken, Ok(Some reopened)))
                            (SessionState.closing full)
                        |> Expect.equal "closing: dropped" (SessionState.closing full, [])

                        // a relaunch or an earlier OpenVersion changed the token meanwhile
                        let newer = { full with OpenedToken = Some(OpenedToken "t-newer") }

                        transition
                            (SessionMsg.Reopened(full.OpenedToken, Ok(Some reopened)))
                            (SessionState.opened newer None)
                        |> Expect.equal "stale: dropped" (SessionState.opened newer None, [])

                        // two quick selections: the first answer lands, the second started from the same
                        // token and is dropped, so the User sees the version the first one opened
                        let afterFirst, _ =
                            transition
                                (SessionMsg.Reopened(full.OpenedToken, Ok(Some reopened)))
                                (SessionState.opened full None)

                        transition (SessionMsg.Reopened(full.OpenedToken, Ok(Some full))) afterFirst
                        |> Expect.equal "second dropped" (SessionState.opened reopened None, [])
                    }
                ]


        [<Tests>]
        let tests =
            testList
                "SessionState.transition"
                [
                    presentTests
                    outcomeTests
                    retryTests
                    resumeTests
                    pinTests
                    endingTests
                    openVersionTests
                ]


    [<Tests>]
    let viewTests =
        let pending: EnrolmentPending =
            {
                DisplayName = "Stub Prescriber (no PIN)"
                MailHint = "n***@stub.example"
            }

        testList
            "SessionState.view"
            [
                test "no Session: anonymous; the presentation and the resume show the request, the attempt only" {
                    SessionState.anonymous
                    |> SessionState.view
                    |> Expect.equal "anonymous" SessionView.Anonymous

                    SessionState.launching launchA keyA 2
                    |> SessionState.view
                    |> Expect.equal "launching" (SessionView.Launching 2)

                    SessionState.resuming
                    |> SessionState.view
                    |> Expect.equal "resuming" SessionView.Resuming
                }

                test "the open Session, and the close under way over it" {
                    SessionState.opened full None
                    |> SessionState.view
                    |> Expect.equal "open" (SessionView.Open full)

                    SessionState.closing full
                    |> SessionState.view
                    |> Expect.equal "closing" (SessionView.Closing full)

                    SessionState.opened full None
                    |> SessionState.token
                    |> Expect.equal "the token of the open Session" full.OpenedToken

                    SessionState.closing full
                    |> SessionState.token
                    |> Expect.equal "none while closing" None

                    SessionState.launching launchA keyA 1
                    |> SessionState.token
                    |> Expect.equal "none while launching" None
                }

                test "a refusal is retryable when the Launch is kept; the server unreachable carries no count" {
                    SessionState.retryable LaunchRefusal.NoBrowserIdentity launchA keyA
                    |> SessionState.view
                    |> Expect.equal "retryable" (SessionView.Retryable LaunchRefusal.NoBrowserIdentity)

                    SessionState.refused LaunchRefusal.NoBrowserIdentity
                    |> SessionState.view
                    |> Expect.equal "refused" (SessionView.Refused LaunchRefusal.NoBrowserIdentity)

                    SessionState.refused LaunchRefusal.NoRole
                    |> SessionState.view
                    |> Expect.equal "no role" (SessionView.Refused LaunchRefusal.NoRole)

                    SessionState.unreachable launchA keyA
                    |> SessionState.view
                    |> Expect.equal "unreachable" SessionView.Unreachable
                }

                test "the endings and the enrolment keep what the gate shows" {
                    SessionState.ended SessionEnding.WrongPinLimit
                    |> SessionState.view
                    |> Expect.equal "ended" (SessionView.Ended SessionEnding.WrongPinLimit)

                    SessionState.enrolling pending (Some(PinRefusal.WrongCode 2))
                    |> SessionState.view
                    |> Expect.equal "enrolling" (SessionView.Enrolling(pending, Some(PinRefusal.WrongCode 2)))

                    SessionState.supplyingPin pending
                    |> SessionState.view
                    |> Expect.equal "supplying the PIN" (SessionView.SupplyingPin pending)

                    SessionState.enrolmentFailed PinRefusal.CodeVoid
                    |> SessionState.view
                    |> Expect.equal "enrolment failed" (SessionView.EnrolmentFailed PinRefusal.CodeVoid)
                }
            ]


    [<Tests>]
    let movedOnTests =
        let headNo (no: int) : OrderPlanHead =
            {
                Id = $"plan-{no}"
                No = no
                By = full.User.Value
                SignedAt = System.DateTime(2026, 9, 11, 12, 0, 0, System.DateTimeKind.Utc)
            }

        let two = headNo 2
        let three = headNo 3
        let told head =
            SessionMsg.Told(full.OpenedToken, RecordNotice.NewerVersion head)
        let transition = SessionState.transition

        testList
            "the moved-on notice in the Session lane"
            [
                test "a reply's notice is kept and told once per version, on the open Session it started from" {
                    let opened = SessionState.opened full None

                    transition (told two) opened
                    |> Expect.equal
                        "news: kept and told"
                        (SessionState.opened full (Some two), [ SessionEffect.TellMovedOn two ])

                    transition (told two) (SessionState.opened full (Some two))
                    |> Expect.equal "the same version again: kept, not told" (SessionState.opened full (Some two), [])

                    transition (told three) (SessionState.opened full (Some two))
                    |> Expect.equal
                        "a newer one: kept and told"
                        (SessionState.opened full (Some three), [ SessionEffect.TellMovedOn three ])

                    // replies land out of order: an older version told last is not news and is not kept
                    transition (told two) (SessionState.opened full (Some three))
                    |> Expect.equal "an older one: nothing" (SessionState.opened full (Some three), [])

                    SessionState.opened full (Some two)
                    |> SessionState.movedOn
                    |> Expect.equal "the bar reads it" (Some two)
                }

                test "a notice from another token, or with nothing open or a close under way, is dropped" {
                    let stale = SessionMsg.Told(Some(OpenedToken "t-old"), RecordNotice.NewerVersion two)

                    transition stale (SessionState.opened full None)
                    |> Expect.equal "stale: dropped" (SessionState.opened full None, [])

                    for state in [ SessionState.anonymous; SessionState.closing full; SessionState.resuming ] do
                        transition (told two) state |> Expect.equal "dropped" (state, [])

                    // an anonymous reply carries no token; an anonymous Session is told nothing
                    transition (SessionMsg.Told(None, RecordNotice.NewerVersion two)) SessionState.anonymous
                    |> Expect.equal "anonymous: dropped" (SessionState.anonymous, [])
                }

                test "a reply's ending ends the Session and acknowledges it with a close, on the token it started from" {
                    transition
                        (SessionMsg.Told(full.OpenedToken, RecordNotice.Ended SessionEnding.WrongPinLimit))
                        (SessionState.opened full (Some two))
                    |> Expect.equal
                        "ended"
                        (SessionState.ended SessionEnding.WrongPinLimit, [ SessionEffect.CallCloseSession ])

                    transition
                        (SessionMsg.Told(Some(OpenedToken "t-old"), RecordNotice.Ended SessionEnding.WrongPinLimit))
                        (SessionState.opened full None)
                    |> Expect.equal "stale: dropped" (SessionState.opened full None, [])
                }

                test "a signature refused because the record moved on keeps the head without telling it" {
                    transition (SessionMsg.Blocked two) (SessionState.opened full None)
                    |> Expect.equal "kept, not told" (SessionState.opened full (Some two), [])

                    transition (SessionMsg.Blocked two) (SessionState.opened full (Some three))
                    |> Expect.equal "an older one: nothing" (SessionState.opened full (Some three), [])

                    transition (SessionMsg.Blocked two) SessionState.anonymous
                    |> Expect.equal "nothing open: dropped" (SessionState.anonymous, [])
                }

                test
                    "the version opened spends the notice; a newer one told meanwhile stays; the token renewed keeps it" {
                    let signedTwo: SignedOrderPlan =
                        {
                            Head = two
                            PatientId = "p"
                            Base = Some "plan-1"
                            OrderContexts = [||]
                            Patient = patient
                            Identity = None
                            Verified = true
                        }

                    let reopened =
                        { full with
                            OpenedToken = Some(OpenedToken "t-2")
                            Head = Some signedTwo
                        }

                    transition
                        (SessionMsg.Reopened(full.OpenedToken, Ok(Some reopened)))
                        (SessionState.opened full (Some two))
                    |> Expect.equal
                        "spent"
                        (SessionState.opened reopened None,
                         [ SessionEffect.LoadCart signedTwo; SessionEffect.TellVersionOpened two ])

                    transition
                        (SessionMsg.Reopened(full.OpenedToken, Ok(Some reopened)))
                        (SessionState.opened full (Some three))
                    |> fst
                    |> Expect.equal "the newer notice stays" (SessionState.opened reopened (Some three))

                    transition (SessionMsg.Reopened(full.OpenedToken, Ok None)) (SessionState.opened full (Some two))
                    |> Expect.equal "nothing to open: kept" (SessionState.opened full (Some two), [])

                    transition (SessionMsg.TokenRenewed(OpenedToken "t2", aged)) (SessionState.opened full (Some two))
                    |> Expect.equal
                        "renewed, kept"
                        (SessionState.opened (renewed full) (Some two), [ SessionEffect.SetPatient(Some aged) ])
                }

                test "the notice goes with the Session: a close, a launch, an ending" {
                    transition SessionMsg.Close (SessionState.opened full (Some two))
                    |> Expect.equal "closing drops it" (SessionState.closing full, [ SessionEffect.CallCloseSession ])

                    transition (SessionMsg.CloseFailed "down") (SessionState.closing full)
                    |> Expect.equal "reopened without it" (SessionState.opened full None, [])

                    transition (SessionMsg.Present(launchB, keyB)) (SessionState.opened full (Some two))
                    |> fst
                    |> Expect.equal "a launch drops it" (SessionState.launching launchB keyB 1)

                    transition
                        (SessionMsg.EndedByServer SessionEnding.WrongPinLimit)
                        (SessionState.opened full (Some two))
                    |> fst
                    |> Expect.equal "an ending drops it" (SessionState.ended SessionEnding.WrongPinLimit)
                }
            ]
