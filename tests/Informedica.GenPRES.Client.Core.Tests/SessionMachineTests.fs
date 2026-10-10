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

    /// Whom the signed version names, which the Session is for from the sign on.
    let who =
        {
            Name = "Stub Testpatiënt"
            BirthYear = 2016
            BirthMonth = 3
            BirthDay = 15
        }

    /// The version a sign gives, naming whom the Session is for from then on.
    let signedVersion: SignedOrderPlan =
        {
            Head =
                {
                    Id = "plan-5"
                    No = 5
                    By = full.User.Value
                    SignedAt = System.DateTime(2026, 9, 12, 12, 0, 0, System.DateTimeKind.Utc)
                }
            PatientId = "p"
            Base = None
            OrderContexts = [||]
            Patient = aged
            Identity = Some who
            Verified = true
        }

    /// A Session renewed after a sign: the token, the version signed as its head, and the
    /// patient with whom the version names in the context it holds.
    let renewed (session: SessionOpened) =
        { session with
            OpenedToken = Some(OpenedToken "t2")
            Head = Some signedVersion
            PatientContext =
                session.PatientContext
                |> Option.map (fun c ->
                    { c with
                        Patient = Some aged
                        Identity = Some who
                    }
                )
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
                "PresentLaunch"
                [
                    test "from Anonymous starts attempt 1 and calls the server" {
                        transition (SessionMsg.PresentLaunch(launchA, keyA)) SessionState.anonymous
                        |> Expect.equal "launching" (launching, [ SessionEffect.CallPresentLaunch(launchA, keyA) ])
                    }

                    test "the same Launch while it is in flight is a no-op, even with another key" {
                        transition (SessionMsg.PresentLaunch(launchA, keyB)) (SessionState.launching launchA keyA 2)
                        |> Expect.equal "unchanged" (SessionState.launching launchA keyA 2, [])
                    }

                    test "another Launch waits for the outcome of the one in flight" {
                        let state, effects = transition (SessionMsg.PresentLaunch(launchB, keyA)) launching

                        effects |> Expect.isEmpty "nothing sent"
                        state |> SessionState.view |> Expect.equal "launching" (SessionView.Launching 1)

                        transition
                            (SessionMsg.LaunchOutcome(launchA, keyA, Ok(LaunchOutcome.RedirectTo "https://idp")))
                            state
                        |> Expect.equal
                            "B presented, A's redirect not followed"
                            (SessionState.launching launchB keyA 1, [ SessionEffect.CallPresentLaunch(launchB, keyA) ])
                    }

                    test "over an open Session or a request out, the launch is presented once the close has answered" {
                        let waiting = SessionState.leaving (SessionFollow.Launch(launchA, keyA))

                        for name, state, effects in
                            [
                                "Open", SessionState.opened full None, [ SessionEffect.CallCloseSession ]
                                "Resuming", SessionState.resuming, [ SessionEffect.CallCloseSession ]
                                "Closing", SessionState.closing full, []
                            ] do
                            transition (SessionMsg.PresentLaunch(launchA, keyA)) state
                            |> Expect.equal name (waiting, effects)

                        transition SessionMsg.SessionClosed waiting
                        |> Expect.equal "presented" (launching, [ SessionEffect.CallPresentLaunch(launchA, keyA) ])
                    }

                    testList
                        "starts a fresh presentation with nothing out and no Session open"
                        [
                            for name, state in
                                [
                                    "Refused", SessionState.refused LaunchRefusal.NoRole
                                    // the same Launch kept after a refusal, or the server unreachable,
                                    // is presented afresh: only a presentation under way is kept
                                    "LaunchRetryable, the same Launch",
                                    SessionState.retryable LaunchRefusal.NoBrowserIdentity launchA keyA
                                    "ServerUnreachable", SessionState.unreachable launchB keyB
                                    "ServerUnreachable, the same Launch", SessionState.unreachable launchA keyA
                                ] do
                                test name {
                                    transition (SessionMsg.PresentLaunch(launchA, keyA)) state
                                    |> Expect.equal
                                        "launching"
                                        (launching, [ SessionEffect.CallPresentLaunch(launchA, keyA) ])
                                }
                        ]
                ]


        let outcomeTests =
            testList
                "LaunchOutcome"
                [
                    test "Opened sets the patient through UpdatePatient and keeps the session's key" {
                        transition (SessionMsg.LaunchOutcome(launchA, keyA, Ok(LaunchOutcome.Opened full))) launching
                        |> Expect.equal
                            "open"
                            (SessionState.opened full None,
                             [
                                 SessionEffect.SetPatientData(Some patient)
                                 SessionEffect.KeepBrowserKey "thumb"
                             ])
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

                        transition (SessionMsg.LaunchOutcome(launchA, keyA, Ok(LaunchOutcome.Opened over))) launching
                        |> Expect.equal
                            "open"
                            (SessionState.opened over None,
                             [
                                 SessionEffect.SetPatientData(Some patient)
                                 SessionEffect.KeepBrowserKey "thumb"
                                 SessionEffect.LoadSignedPlan head
                             ])

                        // a resume opens the same way
                        transition (SessionMsg.Resumed(Ok(ResumeResult.Found over))) SessionState.resuming
                        |> snd
                        |> List.contains (SessionEffect.LoadSignedPlan head)
                        |> Expect.isTrue "loaded at resume"

                        // no patient, no cart to load, whatever the head says
                        let bare = { sessionWith None None with Head = Some head }

                        transition (SessionMsg.LaunchOutcome(launchA, keyA, Ok(LaunchOutcome.Opened bare))) launching
                        |> snd
                        |> Expect.equal "nothing to load" [ SessionEffect.SetPatientData None ]
                    }

                    test "Opened without a patient sets None; without a thumbprint prunes nothing" {
                        let bare = sessionWith None None

                        transition (SessionMsg.LaunchOutcome(launchA, keyA, Ok(LaunchOutcome.Opened bare))) launching
                        |> Expect.equal "open" (SessionState.opened bare None, [ SessionEffect.SetPatientData None ])
                    }

                    test "RedirectTo goes to the url and stays Launching" {
                        transition
                            (SessionMsg.LaunchOutcome(launchA, keyA, Ok(LaunchOutcome.RedirectTo "/authorize?x")))
                            launching
                        |> Expect.equal "redirect" (launching, [ SessionEffect.GoToIdentityProvider "/authorize?x" ])
                    }

                    test "Refused NoBrowserIdentity keeps the Launch and key for a retry" {
                        transition
                            (SessionMsg.LaunchOutcome(
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
                                        (SessionMsg.LaunchOutcome(launchA, keyA, Ok(LaunchOutcome.Refused refusal)))
                                        launching
                                    |> Expect.equal "refused, no retry" (SessionState.refused refusal, [])
                                }
                        ]

                    test "a transport error retries with the same Launch and key, attempts 1 -> 2 -> 3" {
                        let err = SessionMsg.LaunchOutcome(launchA, keyA, Error "down")

                        transition err (SessionState.launching launchA keyA 1)
                        |> Expect.equal
                            "attempt 2"
                            (SessionState.launching launchA keyA 2, [ SessionEffect.CallPresentLaunch(launchA, keyA) ])

                        transition err (SessionState.launching launchA keyA 2)
                        |> Expect.equal
                            "attempt 3"
                            (SessionState.launching launchA keyA 3, [ SessionEffect.CallPresentLaunch(launchA, keyA) ])
                    }

                    test "a transport error on the third attempt is ServerUnreachable" {
                        transition
                            (SessionMsg.LaunchOutcome(launchA, keyA, Error "down"))
                            (SessionState.launching launchA keyA 3)
                        |> Expect.equal "unreachable" (SessionState.unreachable launchA keyA, [])
                    }

                    test "three errors in a row from Anonymous end ServerUnreachable after three calls" {
                        let err = SessionMsg.LaunchOutcome(launchA, keyA, Error "down")

                        let state, effects =
                            run SessionState.anonymous [ SessionMsg.PresentLaunch(launchA, keyA); err; err; err ]

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
                                transition
                                    (SessionMsg.LaunchOutcome(launchB, keyA, Ok(LaunchOutcome.Opened full)))
                                    launching
                                |> Expect.equal "unchanged" (launching, [])
                            }

                            test "an outcome for another key is dropped" {
                                transition
                                    (SessionMsg.LaunchOutcome(launchA, keyB, Ok(LaunchOutcome.Opened full)))
                                    launching
                                |> Expect.equal "unchanged" (launching, [])
                            }

                            test "an outcome after a newer launch came closes its Session and reaches nothing" {
                                let state, effects =
                                    run
                                        SessionState.anonymous
                                        [
                                            SessionMsg.PresentLaunch(launchA, keyA)
                                            SessionMsg.PresentLaunch(launchB, keyA)
                                            SessionMsg.LaunchOutcome(launchA, keyA, Ok(LaunchOutcome.Opened full))
                                        ]

                                state
                                |> Expect.equal
                                    "A's Session closing, B waiting"
                                    (SessionState.leaving (SessionFollow.Launch(launchB, keyA)))

                                effects
                                |> List.exists (
                                    function
                                    | SessionEffect.SetPatientData _ -> true
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
                                            "LaunchRetryable",
                                            SessionState.retryable LaunchRefusal.NoBrowserIdentity launchA keyA
                                            "ServerUnreachable", SessionState.unreachable launchA keyA
                                            "Closing", SessionState.closing full
                                        ] do
                                        test name {
                                            transition
                                                (SessionMsg.LaunchOutcome(launchA, keyA, Ok(LaunchOutcome.Opened full)))
                                                state
                                            |> Expect.equal "unchanged" (state, [])
                                        }
                                ]
                        ]
                ]


        let retryTests =
            testList
                "RetryLaunch"
                [
                    test "from ServerUnreachable presents the same Launch and key again, attempt 1" {
                        transition SessionMsg.RetryLaunch (SessionState.unreachable launchA keyA)
                        |> Expect.equal "launching" (launching, [ SessionEffect.CallPresentLaunch(launchA, keyA) ])
                    }

                    test "from Refused with a retry presents the same Launch and key again" {
                        transition
                            SessionMsg.RetryLaunch
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
                                test name {
                                    transition SessionMsg.RetryLaunch state |> Expect.equal "unchanged" (state, [])
                                }
                        ]
                ]


        let resumeTests =
            testList
                "Resume and Resumed"
                [
                    test "Resume from Anonymous asks for the session" {
                        transition SessionMsg.Resume SessionState.anonymous
                        |> Expect.equal "resuming" (SessionState.resuming, [ SessionEffect.CallResume ])
                    }

                    test "Resume from any other state is a no-op" {
                        for state in [ launching; SessionState.resuming; SessionState.opened full None ] do
                            transition SessionMsg.Resume state |> Expect.equal "unchanged" (state, [])
                    }

                    test "Resumed with a session opens it like a launch" {
                        transition (SessionMsg.Resumed(Ok(ResumeResult.Found full))) SessionState.resuming
                        |> Expect.equal "open" (SessionState.onOpened full)
                    }

                    test "Resumed without a session is Anonymous and keeps the url patient (no SetPatientData)" {
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

                        transition (SessionMsg.PresentLaunch(launchB, keyB)) (SessionState.enrolling pending None)
                        |> Expect.equal "presents" (SessionState.present launchB keyB)
                    }

                    test "from Ended the anonymous open carries nothing over, and a new launch presents" {
                        let ended = SessionState.ended SessionEnding.SupersededByLaunch

                        transition SessionMsg.ContinueAnonymous ended
                        |> Expect.equal "anonymous" (SessionState.anonymous, [ SessionEffect.SetPatientData None ])

                        transition (SessionMsg.PresentLaunch(launchB, keyB)) ended
                        |> Expect.equal "presents" (SessionState.present launchB keyB)
                    }

                    test "the anonymous open from a refusal, retryable or not, and from the server unreachable" {
                        for state in
                            [
                                SessionState.refused LaunchRefusal.NoRole
                                SessionState.retryable LaunchRefusal.NoBrowserIdentity launchA keyA
                                SessionState.unreachable launchA keyA
                            ] do
                            transition SessionMsg.ContinueAnonymous state
                            |> Expect.equal "anonymous" (SessionState.anonymous, [ SessionEffect.SetPatientData None ])

                        for state in [ SessionState.anonymous; launching; SessionState.opened full None ] do
                            transition SessionMsg.ContinueAnonymous state
                            |> Expect.equal "unchanged" (state, [])
                    }

                    test "a refusal at the callback keeps nothing, whatever was under way" {
                        for state in [ SessionState.anonymous; launching; SessionState.opened full None ] do
                            transition (SessionMsg.LaunchRefused LaunchRefusal.LaunchSpent) state
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
                        |> Expect.equal "form back, told" (enrolling, [ SessionEffect.Alert Alert.Alert.PinNotSent ])

                        // an answer lands only on the request in flight
                        transition (SessionMsg.PinAnswered(Ok(PinOutcome.Opened session))) enrolling
                        |> Expect.equal "dropped" (enrolling, [])
                    }
                ]


        let endingTests =
            testList
                "close, the endings, the token"
                [
                    test "CloseSession from Open asks the server and is Closing until SessionClosed" {
                        transition SessionMsg.CloseSession (SessionState.opened full None)
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
                            transition SessionMsg.CloseSession state |> Expect.equal "unchanged" (state, [])
                    }

                    test "SessionClosed from Closing is Anonymous and clears the patient" {
                        transition SessionMsg.SessionClosed (SessionState.closing full)
                        |> Expect.equal "anonymous" (SessionState.anonymous, [ SessionEffect.SetPatientData None ])
                    }

                    test "CloseFailed from Closing returns to Open with the same session and tells the user" {
                        transition (SessionMsg.CloseFailed "down") (SessionState.closing full)
                        |> Expect.equal
                            "still open, told"
                            (SessionState.opened full None, [ SessionEffect.Alert Alert.Alert.CloseFailed ])
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

                    test "a PIN that did not reach the server tells nothing outside its request" {
                        for state in
                            [
                                SessionState.opened full None
                                SessionState.anonymous
                                SessionState.closing full
                            ] do
                            transition (SessionMsg.PinAnswered(Error "down")) state
                            |> Expect.equal "unchanged" (state, [])
                    }

                    test "SessionClosed outside Closing is dropped" {
                        for state in
                            [
                                SessionState.opened full None
                                SessionState.anonymous
                                launching
                                SessionState.resuming
                            ] do
                            transition SessionMsg.SessionClosed state
                            |> Expect.equal "unchanged" (state, [])
                    }

                    test "a launch during a close is presented once the close has answered" {
                        let newer = sessionWith (Some "thumb-2") (Some patient)

                        let state, effects =
                            run
                                (SessionState.opened full None)
                                [
                                    SessionMsg.CloseSession
                                    // the launch waits: the close deletes the cookie the launch sets
                                    SessionMsg.PresentLaunch(launchB, keyB)
                                    SessionMsg.SessionClosed
                                    SessionMsg.LaunchOutcome(launchB, keyB, Ok(LaunchOutcome.Opened newer))
                                ]

                        state |> Expect.equal "the newer session open" (SessionState.opened newer None)

                        effects
                        |> Expect.equal
                            "no SetPatientData None from the close"
                            [
                                SessionEffect.CallCloseSession
                                SessionEffect.CallPresentLaunch(launchB, keyB)
                                SessionEffect.SetPatientData(Some patient)
                                SessionEffect.KeepBrowserKey "thumb-2"
                            ]
                    }

                    test "SignatureEndedSession from Open is Ended and acknowledges it with a close; elsewhere dropped" {
                        transition
                            (SessionMsg.SignatureEndedSession SessionEnding.WrongPinLimit)
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
                            transition (SessionMsg.SignatureEndedSession SessionEnding.WrongPinLimit) state
                            |> Expect.equal $"{state}" (state, [])
                    }

                    test
                        "SignatureRenewedToken from Open replaces the token, takes the version signed as the head and the patient and the identity, to the panel as at a resume; elsewhere dropped" {
                        transition
                            (SessionMsg.SignatureRenewedToken(OpenedToken "t2", aged, signedVersion))
                            (SessionState.opened full None)
                        |> Expect.equal
                            "renewed"
                            (SessionState.opened (renewed full) None, [ SessionEffect.SetPatientData(Some aged) ])

                        // the same SetPatientData a resume of the renewed Session gives
                        SessionState.onOpened (renewed full)
                        |> snd
                        |> List.head
                        |> Expect.equal "as at a resume" (SessionEffect.SetPatientData(Some aged))

                        // a Session without a patient context: the token only
                        let none = sessionWith (Some "thumb") None

                        transition
                            (SessionMsg.SignatureRenewedToken(OpenedToken "t2", aged, signedVersion))
                            (SessionState.opened none None)
                        |> Expect.equal
                            "the token only"
                            (SessionState.opened
                                { none with
                                    OpenedToken = Some(OpenedToken "t2")
                                    Head = Some signedVersion
                                }
                                None,
                             [ SessionEffect.SetPatientData(Some aged) ])

                        for state in [ SessionState.anonymous; SessionState.closing full; launching ] do
                            transition (SessionMsg.SignatureRenewedToken(OpenedToken "t2", aged, signedVersion)) state
                            |> Expect.equal $"{state}" (state, [])
                    }

                    test "the happy path: present, open, close" {
                        let state, effects =
                            run
                                SessionState.anonymous
                                [
                                    SessionMsg.PresentLaunch(launchA, keyA)
                                    SessionMsg.LaunchOutcome(launchA, keyA, Ok(LaunchOutcome.Opened full))
                                    SessionMsg.CloseSession
                                    SessionMsg.SessionClosed
                                ]

                        state |> Expect.equal "anonymous again" SessionState.anonymous

                        effects
                        |> Expect.equal
                            "effects in order"
                            [
                                SessionEffect.CallPresentLaunch(launchA, keyA)
                                SessionEffect.SetPatientData(Some patient)
                                SessionEffect.KeepBrowserKey "thumb"
                                SessionEffect.CallCloseSession
                                SessionEffect.SetPatientData None
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
                "OpenSignedPlan"
                [
                    test "OpenSignedPlan from Open calls the server with the token it starts from; elsewhere dropped" {
                        let opening, effects =
                            transition (SessionMsg.OpenSignedPlan "plan-2") (SessionState.opened full None)

                        effects
                        |> Expect.equal "call" [ SessionEffect.CallOpenSignedPlan("plan-2", full.OpenedToken) ]

                        opening
                        |> SessionState.reopening
                        |> Expect.equal "the open is out" (Some(SessionReopening.OpenSignedPlan "plan-2"))

                        for state in [ SessionState.anonymous; SessionState.closing full ] do
                            transition (SessionMsg.OpenSignedPlan "plan-2") state
                            |> Expect.equal "dropped" (state, [])
                    }

                    test "SignedPlanOpened with the Session replaces it, loads the version into the cart and tells it" {
                        transition
                            (SessionMsg.SignedPlanOpened(full.OpenedToken, Ok(Some reopened)))
                            (SessionState.opened full None)
                        |> Expect.equal
                            "reopened"
                            (SessionState.opened reopened None,
                             [
                                 SessionEffect.LoadSignedPlan head
                                 SessionEffect.TellSignedPlanOpened head.Head
                             ])
                    }

                    test "NewerPlan.receive: news once per version, ordered by No, not by arrival (Rules 20 to 22)" {
                        let first = head.Head

                        let second =
                            { first with
                                Id = "plan-3"
                                No = 3
                            }

                        NewerPlan.receive None first |> Expect.equal "first: news" (Some first, true)

                        NewerPlan.receive (Some first) first
                        |> Expect.equal "again: not news" (Some first, false)

                        NewerPlan.receive (Some first) second
                        |> Expect.equal "newer: news" (Some second, true)

                        // replies land out of order: an older version told last is not news and is not kept
                        NewerPlan.receive (Some second) first
                        |> Expect.equal "older: nothing" (Some second, false)
                    }

                    test "NewerPlan.opened: the notice is spent by a version at least as new, a newer notice stays" {
                        let two = head.Head

                        let three =
                            { two with
                                Id = "plan-3"
                                No = 3
                            }

                        NewerPlan.opened (Some two) two |> Expect.isNone "the version told is open"
                        NewerPlan.opened (Some two) three |> Expect.isNone "a newer one is open"
                        NewerPlan.opened None two |> Expect.isNone "nothing kept"

                        NewerPlan.opened (Some three) two
                        |> Expect.equal "version 3 told while 2 was opening: the offer stays" (Some three)
                    }

                    test "SignedPlanOpened with nothing to open, or a transport failure, leaves the Session as it was" {
                        transition
                            (SessionMsg.SignedPlanOpened(full.OpenedToken, Ok None))
                            (SessionState.opened full None)
                        |> Expect.equal "nothing to open" (SessionState.opened full None, [])

                        transition
                            (SessionMsg.SignedPlanOpened(full.OpenedToken, Error "offline"))
                            (SessionState.opened full None)
                        |> Expect.equal "failed" (SessionState.opened full None, [])
                    }

                    test "Refresh from Open calls the server with the token it starts from; elsewhere dropped" {
                        let refreshing, effects = transition SessionMsg.RefreshPatient (SessionState.opened full None)

                        effects
                        |> Expect.equal "call" [ SessionEffect.CallRefreshPatient full.OpenedToken ]

                        refreshing
                        |> SessionState.reopening
                        |> Expect.equal "the refresh is out" (Some SessionReopening.RefreshPatient)

                        for state in [ SessionState.anonymous; SessionState.closing full ] do
                            transition SessionMsg.RefreshPatient state |> Expect.equal "dropped" (state, [])
                    }

                    test "PatientRefreshed sets the patient read again and nothing else, with a head or without" {
                        let refreshed =
                            { reopened with
                                PatientContext =
                                    full.PatientContext |> Option.map (fun c -> { c with Patient = Some aged })
                            }

                        transition
                            (SessionMsg.PatientRefreshed(full.OpenedToken, Ok(Some refreshed)))
                            (SessionState.opened full None)
                        |> Expect.equal
                            "the patient alone"
                            (SessionState.opened refreshed None, [ SessionEffect.SetPatientData(Some aged) ])

                        let noHead = { refreshed with Head = None }

                        transition
                            (SessionMsg.PatientRefreshed(full.OpenedToken, Ok(Some noHead)))
                            (SessionState.opened full None)
                        |> Expect.equal
                            "the patient alone"
                            (SessionState.opened noHead None, [ SessionEffect.SetPatientData(Some aged) ])
                    }

                    test
                        "PatientRefreshed with nothing or a failure leaves the Session as it was and says so; a stale token says nothing" {
                        for answer in [ Ok None; Error "offline" ] do
                            transition
                                (SessionMsg.PatientRefreshed(full.OpenedToken, answer))
                                (SessionState.opened full None)
                            |> Expect.equal
                                $"%A{answer}"
                                (SessionState.opened full None, [ SessionEffect.TellPatientRefreshFailed ])

                        let newer = { full with OpenedToken = Some(OpenedToken "t-newer") }

                        transition
                            (SessionMsg.PatientRefreshed(full.OpenedToken, Ok(Some reopened)))
                            (SessionState.opened newer None)
                        |> Expect.equal "stale: dropped" (SessionState.opened newer None, [])
                    }

                    test "A second RefreshPatient or OpenSignedPlan while one is out is dropped" {
                        for first in [ SessionMsg.RefreshPatient; SessionMsg.OpenSignedPlan "plan-2" ] do
                            let out, _ = transition first (SessionState.opened full None)

                            for second in [ SessionMsg.RefreshPatient; SessionMsg.OpenSignedPlan "plan-3" ] do
                                transition second out |> Expect.equal $"%A{first}, then %A{second}" (out, [])
                    }

                    test "A refresh out leaves the token to the requests meanwhile, and a notice keeps it out" {
                        let refreshing, _ = transition SessionMsg.RefreshPatient (SessionState.opened full None)

                        refreshing |> SessionState.token |> Expect.equal "the token" full.OpenedToken
                        refreshing |> SessionState.inFlight |> Expect.isFalse "no other request"

                        transition
                            (SessionMsg.NoticeReceived(full.OpenedToken, RecordNotice.NewerVersion head.Head))
                            refreshing
                        |> fst
                        |> SessionState.reopening
                        |> Expect.equal "still out" (Some SessionReopening.RefreshPatient)
                    }

                    test "Every answer ends the refresh or the open: done, nothing, a failure or a stale token" {
                        let newer = { full with OpenedToken = Some(OpenedToken "t-newer") }

                        let cases =
                            [
                                SessionMsg.RefreshPatient,
                                SessionMsg.PatientRefreshed(full.OpenedToken, Ok(Some reopened)),
                                full
                                SessionMsg.RefreshPatient, SessionMsg.PatientRefreshed(full.OpenedToken, Ok None), full
                                SessionMsg.RefreshPatient,
                                SessionMsg.PatientRefreshed(full.OpenedToken, Error "offline"),
                                full
                                SessionMsg.RefreshPatient, SessionMsg.PatientRefreshed(full.OpenedToken, Ok None), newer
                                SessionMsg.OpenSignedPlan "plan-2",
                                SessionMsg.SignedPlanOpened(full.OpenedToken, Ok(Some reopened)),
                                full
                                SessionMsg.OpenSignedPlan "plan-2",
                                SessionMsg.SignedPlanOpened(full.OpenedToken, Ok None),
                                full
                                SessionMsg.OpenSignedPlan "plan-2",
                                SessionMsg.SignedPlanOpened(full.OpenedToken, Error "offline"),
                                full
                                SessionMsg.OpenSignedPlan "plan-2",
                                SessionMsg.SignedPlanOpened(full.OpenedToken, Ok None),
                                newer
                            ]

                        for ask, answer, session in cases do
                            let out, _ = transition ask (SessionState.opened session None)

                            transition answer out
                            |> fst
                            |> SessionState.reopening
                            |> Expect.isNone $"%A{answer} on %A{session.OpenedToken}"
                    }

                    test "SignedPlanOpened lands only on the open Session that still holds the token it started from" {
                        transition
                            (SessionMsg.SignedPlanOpened(full.OpenedToken, Ok(Some reopened)))
                            SessionState.anonymous
                        |> Expect.equal "not open: dropped" (SessionState.anonymous, [])

                        transition
                            (SessionMsg.SignedPlanOpened(full.OpenedToken, Ok(Some reopened)))
                            (SessionState.closing full)
                        |> Expect.equal "closing: dropped" (SessionState.closing full, [])

                        // a relaunch or an earlier OpenSignedPlan changed the token meanwhile
                        let newer = { full with OpenedToken = Some(OpenedToken "t-newer") }

                        transition
                            (SessionMsg.SignedPlanOpened(full.OpenedToken, Ok(Some reopened)))
                            (SessionState.opened newer None)
                        |> Expect.equal "stale: dropped" (SessionState.opened newer None, [])

                        // two quick selections: the first answer lands, the second started from the same
                        // token and is dropped, so the User sees the version the first one opened
                        let afterFirst, _ =
                            transition
                                (SessionMsg.SignedPlanOpened(full.OpenedToken, Ok(Some reopened)))
                                (SessionState.opened full None)

                        transition (SessionMsg.SignedPlanOpened(full.OpenedToken, Ok(Some full))) afterFirst
                        |> Expect.equal "second dropped" (SessionState.opened reopened None, [])
                    }
                ]


        let urlMovedOnTests =
            let pending: EnrolmentPending =
                {
                    DisplayName = "Stub Prescriber (no PIN)"
                    MailHint = "n***@stub.example"
                }

            let away = SessionState.leaving SessionFollow.Anonymous
            let awayToB = SessionState.leaving (SessionFollow.Launch(launchB, keyB))
            let presentB = SessionState.launching launchB keyB 1, [ SessionEffect.CallPresentLaunch(launchB, keyB) ]

            testList
                "UrlMovedOn"
                [
                    test "an open Session is closed, and the close's answer sends nothing, success or failure" {
                        let state, effects = transition SessionMsg.UrlMovedOn (SessionState.opened full None)

                        (state, effects)
                        |> Expect.equal "leaving" (away, [ SessionEffect.CallCloseSession ])
                        state |> SessionState.view |> Expect.equal "anonymous" SessionView.Anonymous
                        state |> SessionState.token |> Expect.isNone "no token"

                        transition SessionMsg.SessionClosed state
                        |> Expect.equal "closed, no patient cleared" (SessionState.anonymous, [])

                        transition (SessionMsg.CloseFailed "down") state
                        |> Expect.equal "failed, nothing reopened" (SessionState.anonymous, [])
                    }

                    test "the user's close out stays out, and its answer clears no patient" {
                        let state, effects = transition SessionMsg.UrlMovedOn (SessionState.closing full)

                        (state, effects) |> Expect.equal "leaving, no second close" (away, [])

                        transition SessionMsg.SessionClosed state
                        |> Expect.equal "no patient cleared" (SessionState.anonymous, [])

                        transition (SessionMsg.CloseFailed "down") state
                        |> Expect.equal "a late failure tells nothing" (SessionState.anonymous, [])
                    }

                    test "a resume out is closed, and its answer is dropped" {
                        let state, effects = transition SessionMsg.UrlMovedOn SessionState.resuming

                        (state, effects)
                        |> Expect.equal "leaving" (away, [ SessionEffect.CallCloseSession ])

                        transition (SessionMsg.Resumed(Ok(ResumeResult.Found full))) state
                        |> Expect.equal "dropped" (away, [])
                    }

                    test "a refresh or an open out is closed, and its answer is dropped" {
                        for ask, answer in
                            [
                                SessionMsg.RefreshPatient, SessionMsg.PatientRefreshed(full.OpenedToken, Ok(Some full))
                                SessionMsg.OpenSignedPlan "plan-5",
                                SessionMsg.SignedPlanOpened(full.OpenedToken, Ok(Some full))
                            ] do
                            let state, effects = run (SessionState.opened full None) [ ask; SessionMsg.UrlMovedOn ]

                            (state, effects |> List.last)
                            |> Expect.equal "leaving" (away, SessionEffect.CallCloseSession)

                            state |> SessionState.reopening |> Expect.isNone "nothing reopening"
                            transition answer state |> Expect.equal "dropped" (away, [])
                    }

                    test "with nothing out the close goes, for the Session the cookie may hold" {
                        transition SessionMsg.UrlMovedOn SessionState.anonymous
                        |> Expect.equal "leaving" (away, [ SessionEffect.CallCloseSession ])

                        transition SessionMsg.UrlMovedOn (SessionState.enrolling pending None)
                        |> Expect.equal "enrolling left" (away, [ SessionEffect.CallCloseSession ])
                    }

                    test "a launch out stays out; the Session it opens is closed and reaches nothing" {
                        let state, effects = transition SessionMsg.UrlMovedOn launching

                        effects |> Expect.isEmpty "nothing sent"
                        state
                        |> SessionState.view
                        |> Expect.equal "anonymous, no gate" SessionView.Anonymous
                        state |> SessionState.inFlight |> Expect.isTrue "the launch still out"

                        transition (SessionMsg.LaunchOutcome(launchA, keyA, Ok(LaunchOutcome.Opened full))) state
                        |> Expect.equal "closed at once" (away, [ SessionEffect.CallCloseSession ])
                    }

                    test "a launch out that ends otherwise leaves the lane anonymous, and a redirect is not followed" {
                        let state, _ = transition SessionMsg.UrlMovedOn launching

                        for result in
                            [
                                Ok(LaunchOutcome.RedirectTo "https://idp")
                                Ok(LaunchOutcome.Refused LaunchRefusal.NoRole)
                                Ok(LaunchOutcome.Refused LaunchRefusal.NoBrowserIdentity)
                                Error "down"
                            ] do
                            transition (SessionMsg.LaunchOutcome(launchA, keyA, result)) state
                            |> Expect.equal "anonymous" (SessionState.anonymous, [])
                    }

                    test "a PIN out stays out; the Session it opens is closed, a refusal leaves the lane anonymous" {
                        let state, effects = transition SessionMsg.UrlMovedOn (SessionState.supplyingPin pending)

                        effects |> Expect.isEmpty "nothing sent"
                        state |> SessionState.view |> Expect.equal "anonymous" SessionView.Anonymous

                        transition (SessionMsg.PinAnswered(Ok(PinOutcome.Opened full))) state
                        |> Expect.equal "closed at once" (away, [ SessionEffect.CallCloseSession ])

                        for answer in
                            [
                                Ok(PinOutcome.Refused(PinRefusal.WrongCode 2))
                                Ok(PinOutcome.Refused PinRefusal.CodeVoid)
                                Error "down"
                            ] do
                            transition (SessionMsg.PinAnswered answer) state
                            |> Expect.equal "anonymous" (SessionState.anonymous, [])
                    }

                    test "a launch with nothing out is presented at once" {
                        transition (SessionMsg.PresentLaunch(launchB, keyB)) SessionState.anonymous
                        |> Expect.equal "presented" presentB
                    }

                    test "a launch over an open Session is presented once the close has answered, success or failure" {
                        let state, effects =
                            transition (SessionMsg.PresentLaunch(launchB, keyB)) (SessionState.opened full None)

                        (state, effects)
                        |> Expect.equal "leaving" (awayToB, [ SessionEffect.CallCloseSession ])
                        state
                        |> SessionState.view
                        |> Expect.equal "launching, gated" (SessionView.Launching 1)

                        transition SessionMsg.SessionClosed state |> Expect.equal "presented" presentB
                        transition (SessionMsg.CloseFailed "down") state
                        |> Expect.equal "presented" presentB
                    }

                    test "a launch over a launch out is presented once the old one has landed and is closed" {
                        let state, _ = transition (SessionMsg.PresentLaunch(launchB, keyB)) launching

                        state |> SessionState.view |> Expect.equal "launching" (SessionView.Launching 1)

                        let closing, effects =
                            transition (SessionMsg.LaunchOutcome(launchA, keyA, Ok(LaunchOutcome.Opened full))) state

                        (closing, effects)
                        |> Expect.equal "the old Session closed" (awayToB, [ SessionEffect.CallCloseSession ])

                        transition SessionMsg.SessionClosed closing |> Expect.equal "presented" presentB

                        transition
                            (SessionMsg.LaunchOutcome(launchA, keyA, Ok(LaunchOutcome.Refused LaunchRefusal.NoRole)))
                            state
                        |> Expect.equal "refused: presented at once" presentB
                    }

                    test "the newest url wins: a seed url clears a waiting launch, a newer launch replaces it" {
                        transition SessionMsg.UrlMovedOn awayToB |> Expect.equal "cleared" (away, [])

                        transition (SessionMsg.PresentLaunch(launchB, keyB)) away
                        |> Expect.equal "replaced" (awayToB, [])
                    }

                    test "a launch presented after the url moved on waits until nothing is out" {
                        transition (SessionMsg.PresentLaunch(launchB, keyB)) away
                        |> Expect.equal "waits for the close" (awayToB, [])

                        let state, _ = transition SessionMsg.UrlMovedOn launching
                        let state, effects = transition (SessionMsg.PresentLaunch(launchB, keyB)) state

                        effects |> Expect.isEmpty "not presented over the launch out"

                        transition (SessionMsg.LaunchOutcome(launchA, keyA, Ok(LaunchOutcome.Opened full))) state
                        |> Expect.equal "the old Session closed" (awayToB, [ SessionEffect.CallCloseSession ])
                    }

                    test "what is still out after the url moved on changes no page, unless a launch follows" {
                        away |> SessionState.inFlight |> Expect.isTrue "the close out"
                        away |> SessionState.changing |> Expect.isFalse "changes nothing"

                        transition SessionMsg.UrlMovedOn launching
                        |> fst
                        |> SessionState.changing
                        |> Expect.isFalse "the launch out changes nothing"

                        awayToB |> SessionState.changing |> Expect.isTrue "a launch follows"
                        SessionState.closing full
                        |> SessionState.changing
                        |> Expect.isTrue "the user's close"
                        SessionState.resuming |> SessionState.changing |> Expect.isTrue "a resume"
                    }

                    test "the same launch again while it is presented keeps it, and its outcome opens the Session" {
                        let state, effects =
                            run launching [ SessionMsg.UrlMovedOn; SessionMsg.PresentLaunch(launchA, keyB) ]

                        (state, effects)
                        |> Expect.equal "presenting A as before, the close not sent" (launching, [])

                        transition (SessionMsg.LaunchOutcome(launchA, keyA, Ok(LaunchOutcome.Opened full))) state
                        |> Expect.equal
                            "open"
                            (SessionState.opened full None,
                             [
                                 SessionEffect.SetPatientData(Some patient)
                                 SessionEffect.KeepBrowserKey "thumb"
                             ])
                    }

                    test "an answer to no request is dropped" {
                        transition (SessionMsg.LaunchOutcome(launchA, keyA, Ok(LaunchOutcome.Opened full))) away
                        |> Expect.equal "dropped" (away, [])

                        transition (SessionMsg.PinAnswered(Ok(PinOutcome.Opened full))) away
                        |> Expect.equal "dropped" (away, [])
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
                    urlMovedOnTests
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
                    |> Expect.equal "retryable" (SessionView.LaunchRetryable LaunchRefusal.NoBrowserIdentity)

                    SessionState.refused LaunchRefusal.NoBrowserIdentity
                    |> SessionState.view
                    |> Expect.equal "refused" (SessionView.LaunchRefused LaunchRefusal.NoBrowserIdentity)

                    SessionState.refused LaunchRefusal.NoRole
                    |> SessionState.view
                    |> Expect.equal "no role" (SessionView.LaunchRefused LaunchRefusal.NoRole)

                    SessionState.unreachable launchA keyA
                    |> SessionState.view
                    |> Expect.equal "unreachable" SessionView.ServerUnreachable
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
    let newerPlanTests =
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
            SessionMsg.NoticeReceived(full.OpenedToken, RecordNotice.NewerVersion head)
        let transition = SessionState.transition

        testList
            "the moved-on notice in the Session lane"
            [
                test "a reply's notice is kept and told once per version, on the open Session it started from" {
                    let opened = SessionState.opened full None

                    transition (told two) opened
                    |> Expect.equal
                        "news: kept and told"
                        (SessionState.opened full (Some two), [ SessionEffect.TellNewerSignedPlan two ])

                    transition (told two) (SessionState.opened full (Some two))
                    |> Expect.equal "the same version again: kept, not told" (SessionState.opened full (Some two), [])

                    transition (told three) (SessionState.opened full (Some two))
                    |> Expect.equal
                        "a newer one: kept and told"
                        (SessionState.opened full (Some three), [ SessionEffect.TellNewerSignedPlan three ])

                    // replies land out of order: an older version told last is not news and is not kept
                    transition (told two) (SessionState.opened full (Some three))
                    |> Expect.equal "an older one: nothing" (SessionState.opened full (Some three), [])

                    SessionState.opened full (Some two)
                    |> SessionState.newerPlan
                    |> Expect.equal "the bar reads it" (Some two)
                }

                test "a notice from another token, or with nothing open or a close under way, is dropped" {
                    let stale = SessionMsg.NoticeReceived(Some(OpenedToken "t-old"), RecordNotice.NewerVersion two)

                    transition stale (SessionState.opened full None)
                    |> Expect.equal "stale: dropped" (SessionState.opened full None, [])

                    for state in [ SessionState.anonymous; SessionState.closing full; SessionState.resuming ] do
                        transition (told two) state |> Expect.equal "dropped" (state, [])

                    // an anonymous reply carries no token; an anonymous Session is told nothing
                    transition (SessionMsg.NoticeReceived(None, RecordNotice.NewerVersion two)) SessionState.anonymous
                    |> Expect.equal "anonymous: dropped" (SessionState.anonymous, [])
                }

                test "a reply's ending ends the Session and acknowledges it with a close, on the token it started from" {
                    transition
                        (SessionMsg.NoticeReceived(full.OpenedToken, RecordNotice.Ended SessionEnding.WrongPinLimit))
                        (SessionState.opened full (Some two))
                    |> Expect.equal
                        "ended"
                        (SessionState.ended SessionEnding.WrongPinLimit, [ SessionEffect.CallCloseSession ])

                    transition
                        (SessionMsg.NoticeReceived(
                            Some(OpenedToken "t-old"),
                            RecordNotice.Ended SessionEnding.WrongPinLimit
                        ))
                        (SessionState.opened full None)
                    |> Expect.equal "stale: dropped" (SessionState.opened full None, [])
                }

                test "a signature refused because the record moved on keeps the head without telling it" {
                    transition (SessionMsg.SignatureBlocked two) (SessionState.opened full None)
                    |> Expect.equal "kept, not told" (SessionState.opened full (Some two), [])

                    transition (SessionMsg.SignatureBlocked two) (SessionState.opened full (Some three))
                    |> Expect.equal "an older one: nothing" (SessionState.opened full (Some three), [])

                    transition (SessionMsg.SignatureBlocked two) SessionState.anonymous
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
                        (SessionMsg.SignedPlanOpened(full.OpenedToken, Ok(Some reopened)))
                        (SessionState.opened full (Some two))
                    |> Expect.equal
                        "spent"
                        (SessionState.opened reopened None,
                         [
                             SessionEffect.LoadSignedPlan signedTwo
                             SessionEffect.TellSignedPlanOpened two
                         ])

                    transition
                        (SessionMsg.SignedPlanOpened(full.OpenedToken, Ok(Some reopened)))
                        (SessionState.opened full (Some three))
                    |> fst
                    |> Expect.equal "the newer notice stays" (SessionState.opened reopened (Some three))

                    transition
                        (SessionMsg.SignedPlanOpened(full.OpenedToken, Ok None))
                        (SessionState.opened full (Some two))
                    |> Expect.equal "nothing to open: kept" (SessionState.opened full (Some two), [])

                    transition
                        (SessionMsg.SignatureRenewedToken(OpenedToken "t2", aged, signedVersion))
                        (SessionState.opened full (Some two))
                    |> Expect.equal
                        "renewed, kept"
                        (SessionState.opened (renewed full) (Some two), [ SessionEffect.SetPatientData(Some aged) ])
                }

                test "the notice goes with the Session: a close, a launch, an ending" {
                    transition SessionMsg.CloseSession (SessionState.opened full (Some two))
                    |> Expect.equal "closing drops it" (SessionState.closing full, [ SessionEffect.CallCloseSession ])

                    transition (SessionMsg.CloseFailed "down") (SessionState.closing full)
                    |> Expect.equal
                        "reopened without it"
                        (SessionState.opened full None, [ SessionEffect.Alert Alert.Alert.CloseFailed ])

                    transition (SessionMsg.PresentLaunch(launchB, keyB)) (SessionState.opened full (Some two))
                    |> fst
                    |> Expect.equal "a launch drops it" (SessionState.leaving (SessionFollow.Launch(launchB, keyB)))

                    transition
                        (SessionMsg.SignatureEndedSession SessionEnding.WrongPinLimit)
                        (SessionState.opened full (Some two))
                    |> fst
                    |> Expect.equal "an ending drops it" (SessionState.ended SessionEnding.WrongPinLimit)
                }
            ]
