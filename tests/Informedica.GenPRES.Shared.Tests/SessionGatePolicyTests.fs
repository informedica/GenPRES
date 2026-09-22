namespace Informedica.GenPRES.Shared.Tests


/// The session gate's policy, linked in from the client project.
module SessionGatePolicyTests =

    open Expecto
    open Expecto.Flip
    open Shared
    open Shared.Types
    open SessionMachine
    open SessionGatePolicy


    let opened =
        {
            User = None
            PatientContext = None
            OpenedToken = None
            KeyThumbprint = None
            Head = None
        }


    let gateOf session =
        match gateFor english session with
        | Some gate -> gate
        | None -> failtest $"expected a gate for {session}"


    /// A translator that shows which term was asked for, so a test can assert terms, not prose.
    let named (term: Terms) = $"<{term}>"


    let namedGateOf session =
        match gateFor named session with
        | Some gate -> gate
        | None -> failtest $"expected a gate for {session}"


    let pending: EnrolmentPending =
        {
            DisplayName = "Stub Prescriber (no PIN)"
            MailHint = "n***@stub.example"
        }


    let relaunchRefusals =
        [
            LaunchRefusal.LaunchExpired
            LaunchRefusal.LaunchSpent
            LaunchRefusal.LaunchInvalid
            LaunchRefusal.WrongActivePatient
            LaunchRefusal.EnrolmentRequired
        ]


    [<Tests>]
    let tests =
        testList
            "SessionGatePolicy.gateFor"
            [
                testList
                    "no gate while the app is usable"
                    [
                        for name, session in
                            [
                                "Anonymous", SessionView.Anonymous
                                "Open", SessionView.Open opened
                                "Closing", SessionView.Closing opened
                            ] do
                            test name {
                                gateFor english session |> Expect.isNone "no gate"
                                isGated session |> Expect.isFalse "not gated"
                            }
                    ]

                testList
                    "isGated agrees with gateFor"
                    [
                        for name, session in
                            [
                                "Launching", SessionView.Launching 1
                                "Resuming", SessionView.Resuming
                                "Unreachable", SessionView.Unreachable
                                "Refused", SessionView.Refused LaunchRefusal.NoRole
                                "Ended", SessionView.Ended SessionEnding.SupersededByLaunch
                                "Ended at the PIN limit", SessionView.Ended SessionEnding.WrongPinLimit
                                "Ended unreadable", SessionView.Ended SessionEnding.Unreadable
                                "Enrolling", SessionView.Enrolling(pending, None)
                                "SupplyingPin", SessionView.SupplyingPin pending
                                "EnrolmentFailed", SessionView.EnrolmentFailed PinRefusal.CodeVoid
                            ] do
                            test name {
                                gateFor english session |> Expect.isSome "gate"
                                isGated session |> Expect.isTrue "gated"
                            }
                    ]

                test "Enrolling greets, says where the code went, and shows the form (UC-2)" {
                    let gate = gateOf (SessionView.Enrolling(pending, None))
                    gate.Busy |> Expect.isFalse "not busy"
                    gate.Actions |> Expect.isEmpty "no actions besides the form"
                    gate.Body |> Expect.stringContains "name" "Stub Prescriber (no PIN)"
                    gate.Body |> Expect.stringContains "hint" "n***@stub.example"

                    match gate.Form with
                    | Some form ->
                        form.Error |> Expect.isNone "nothing wrong yet"
                        form.Submit |> Expect.equal "button" "Set PIN"
                    | None -> failtest "expected the form"

                    let named = namedGateOf (SessionView.Enrolling(pending, Some(PinRefusal.WrongCode 2)))
                    named.Title |> Expect.equal "title term" "<Session Gate Enrolment>"

                    named.Form
                    |> Option.bind _.Error
                    |> Expect.equal "the server's answer, as a term" (Some "<Session Enrolment Wrong Code>")

                    (gateOf (SessionView.Enrolling(pending, Some(PinRefusal.WrongCode 2)))).Form
                    |> Option.bind _.Error
                    |> Expect.equal "filled" (Some "The code is not right. 2 tries left.")
                }

                test "SupplyingPin is busy without the form; EnrolmentFailed says why and asks for a relaunch" {
                    let busy = gateOf (SessionView.SupplyingPin pending)
                    busy.Busy |> Expect.isTrue "busy"
                    busy.Form |> Expect.isNone "no form while in flight"

                    let failed = gateOf (SessionView.EnrolmentFailed PinRefusal.CodeVoid)
                    failed.Form |> Expect.isNone "no form"
                    failed.Actions |> Expect.isEmpty "relaunch only"
                    failed.Body |> Expect.stringContains "why" "three wrong tries"

                    failed.Body
                    |> Expect.stringContains "relaunch" "Open GenPRES again from MainEHR."

                    (gateOf (SessionView.EnrolmentFailed PinRefusal.WrongActivePatient)).Body
                    |> Expect.stringContains "patient" "not the patient of this launch"
                }

                test "the form's own check: six-digit code, four-to-six-digit PIN, repeat agrees" {
                    formError english "123456" "2468" "2468" |> Expect.isNone "ok"

                    formError english "12345" "2468" "2468"
                    |> Expect.equal "code" (Some "The confirmation code has six digits.")

                    formError english "12345a" "2468" "2468" |> Expect.isSome "code digits"

                    formError english "123456" "123" "123"
                    |> Expect.equal "pin" (Some "The PIN has four to six digits.")

                    formError english "123456" "1234567" "1234567" |> Expect.isSome "pin long"

                    formError english "123456" "2468" "2469"
                    |> Expect.equal "repeat" (Some "The two PINs differ.")
                }

                test "Ended says why, asks for a relaunch, and offers the anonymous open (Rule 11)" {
                    let gate = gateOf (SessionView.Ended SessionEnding.SupersededByLaunch)
                    gate.Busy |> Expect.isFalse "not busy"
                    gate.Actions |> Expect.equal "continue" [ Action.ContinueWithoutLaunch ]
                    gate.Body |> Expect.stringContains "reason" "newer session"
                    gate.Body |> Expect.stringContains "relaunch" "Open GenPRES again from MainEHR."

                    let named = namedGateOf (SessionView.Ended SessionEnding.SupersededByLaunch)
                    named.Title |> Expect.equal "title term" "<Session Gate Ended>"

                    named.Body
                    |> Expect.equal "body terms" "<Session Ending Superseded> <Session Relaunch>"

                    (namedGateOf (SessionView.Ended SessionEnding.WrongPinLimit)).Body
                    |> Expect.equal "the PIN limit (Rule 28)" "<Session Ending Pin Limit> <Session Relaunch>"

                    (namedGateOf (SessionView.Ended SessionEnding.Unreadable)).Body
                    |> Expect.equal "the store could not be read" "<Session Ending Unreadable> <Session Relaunch>"
                }

                test "Launching is busy, names the attempt, offers nothing" {
                    let gate = gateOf (SessionView.Launching 2)
                    gate.Busy |> Expect.isTrue "busy"
                    gate.Actions |> Expect.isEmpty "no actions"

                    gate.Body
                    |> Expect.stringContains "attempt" $"attempt 2 of {SessionState.maxAttempts}"
                }

                test "Resuming is busy and offers nothing" {
                    let gate = gateOf SessionView.Resuming
                    gate.Busy |> Expect.isTrue "busy"
                    gate.Actions |> Expect.isEmpty "no actions"
                }

                test "Unreachable offers a retry and names the attempts" {
                    let gate = gateOf (SessionView.Unreachable)
                    gate.Busy |> Expect.isFalse "not busy"
                    gate.Actions |> Expect.equal "retry" [ Action.Retry ]
                    gate.Body |> Expect.stringContains "attempts" "3 attempts"
                    gate.Body |> Expect.stringContains "relaunch" "MainEHR"
                }

                testList
                    "refusals that only a relaunch can fix offer nothing and say so"
                    [
                        for refusal in relaunchRefusals do
                            test $"{refusal}" {
                                let gate = gateOf (SessionView.Refused refusal)
                                gate.Busy |> Expect.isFalse "not busy"
                                gate.Actions |> Expect.isEmpty "no actions"
                                // sentence-initial or after "and": the tail is the same
                                gate.Body |> Expect.stringContains "relaunch" "GenPRES again from MainEHR."
                            }
                    ]

                test "NoRole offers the anonymous open, whatever the retry" {
                    // only a missing browser identity is ever retried, so a retryable NoRole
                    // never occurs; the gate keeps the rule regardless
                    for session in
                        [
                            SessionView.Refused LaunchRefusal.NoRole
                            SessionView.Retryable LaunchRefusal.NoRole
                        ] do
                        let gate = gateOf session
                        gate.Actions |> Expect.equal "continue" [ Action.ContinueWithoutLaunch ]
                        gate.Body |> Expect.stringContains "no patient" "no patient is carried over"
                }

                test "NoBrowserIdentity with a retry offers it" {
                    let gate = gateOf (SessionView.Retryable LaunchRefusal.NoBrowserIdentity)

                    gate.Actions |> Expect.equal "retry" [ Action.Retry ]
                    gate.Body |> Expect.stringContains "try again" "Try again"
                }

                test "NoBrowserIdentity without a retry asks for a relaunch" {
                    let gate = gateOf (SessionView.Refused LaunchRefusal.NoBrowserIdentity)
                    gate.Actions |> Expect.isEmpty "no actions"
                    gate.Body |> Expect.stringContains "relaunch" "Open GenPRES again from MainEHR."
                }

                testList
                    "every text is a term, translated by the caller"
                    [
                        test "Launching" {
                            let gate = namedGateOf (SessionView.Launching 2)
                            gate.Title |> Expect.equal "title" "<Session Gate Opening>"
                            gate.Body |> Expect.equal "body" "<Session Gate Opening Text>"
                        }

                        test "Resuming" {
                            let gate = namedGateOf SessionView.Resuming
                            gate.Title |> Expect.equal "title" "<Session Gate Resuming>"
                            gate.Body |> Expect.equal "body" "<Session Gate Resuming Text>"
                        }

                        test "Unreachable" {
                            let gate = namedGateOf (SessionView.Unreachable)
                            gate.Title |> Expect.equal "title" "<Session Gate Unreachable>"

                            gate.Body
                            |> Expect.equal
                                "body"
                                "<Session Gate Unreachable Text> <Session Gate Try Again Or Relaunch>"
                        }

                        for refusal, body in
                            [
                                LaunchRefusal.LaunchExpired, "<Session Refusal Expired> <Session Relaunch>"
                                LaunchRefusal.LaunchSpent, "<Session Refusal Spent> <Session Relaunch>"
                                LaunchRefusal.LaunchInvalid, "<Session Refusal Invalid> <Session Relaunch>"
                                LaunchRefusal.NoRole, "<Session Refusal No Role>"
                                LaunchRefusal.WrongActivePatient, "<Session Refusal Wrong Patient>"
                                LaunchRefusal.EnrolmentRequired, "<Session Refusal Enrolment>"
                                LaunchRefusal.NoBrowserIdentity,
                                "<Session Refusal No Browser Identity> <Session Relaunch>"
                            ] do
                            test $"Refused {refusal} without a retry" {
                                let gate = namedGateOf (SessionView.Refused refusal)
                                gate.Title |> Expect.equal "title" "<Session Gate Refused>"
                                gate.Body |> Expect.equal "body" body
                            }

                        test "Refused NoBrowserIdentity with a retry" {
                            let gate = namedGateOf (SessionView.Retryable LaunchRefusal.NoBrowserIdentity)

                            gate.Body
                            |> Expect.equal "body" "<Session Refusal No Browser Identity> <Session Retry>"
                        }
                    ]

                test "the numbers are filled into the translated text, in order" {
                    let template (term: Terms) =
                        match term with
                        | Terms.``Session Gate Opening Text`` -> "poging {0} van {1}"
                        | Terms.``Session Gate Unreachable Text`` -> "{0} pogingen"
                        | _ -> ""

                    (gateOf (SessionView.Launching 2)).Body
                    |> Expect.equal "english" $"Presenting the launch, attempt 2 of {SessionState.maxAttempts}."

                    match gateFor template (SessionView.Launching 2) with
                    | Some gate ->
                        gate.Body
                        |> Expect.equal "translated" $"poging 2 van {SessionState.maxAttempts}"
                    | None -> failtest "expected a gate"

                    match gateFor template (SessionView.Unreachable) with
                    | Some gate -> gate.Body |> Expect.equal "translated" "3 pogingen"
                    | None -> failtest "expected a gate"
                }

                test "every session term has an English default that is not its name" {
                    for term in
                        [
                            Terms.``Session Gate Opening``
                            Terms.``Session Gate Opening Text``
                            Terms.``Session Gate Resuming``
                            Terms.``Session Gate Resuming Text``
                            Terms.``Session Gate Unreachable``
                            Terms.``Session Gate Unreachable Text``
                            Terms.``Session Gate Refused``
                            Terms.``Session Gate Try Again Or Relaunch``
                            Terms.``Session Relaunch``
                            Terms.``Session Retry``
                            Terms.``Session Refusal Expired``
                            Terms.``Session Refusal Spent``
                            Terms.``Session Refusal Invalid``
                            Terms.``Session Refusal No Browser Identity``
                            Terms.``Session Refusal No Role``
                            Terms.``Session Refusal Wrong Patient``
                            Terms.``Session Refusal Enrolment``
                            Terms.``Session Try Again``
                            Terms.``Session Continue Without Launch``
                            Terms.``Session Close``
                            Terms.``Session Role Prescriber``
                            Terms.``Session Role Reader``
                            Terms.``Session Gate Ended``
                            Terms.``Session Ending Superseded``
                            Terms.``Session Ending Pin Limit``
                            Terms.``Session Ending Unreadable``
                        ] do
                        english term |> Expect.notEqual $"default for {term}" $"{term}"

                    english Terms.Cancel |> Expect.equal "not a session term: its name" "Cancel"
                }

                test "product names keep their case in every body" {
                    let bodies =
                        [
                            yield (gateOf (SessionView.Unreachable)).Body
                            for refusal in relaunchRefusals @ [ LaunchRefusal.NoRole; LaunchRefusal.NoBrowserIdentity ] do
                                yield (gateOf (SessionView.Refused refusal)).Body
                        ]

                    for body in bodies do
                        body.Contains "genpres" |> Expect.isFalse $"lowercased GenPRES in: {body}"
                        body.Contains "mainehr" |> Expect.isFalse $"lowercased MainEHR in: {body}"
                }
            ]
