namespace Informedica.GenPRES.Shared.Tests


/// The session gate's policy, linked in from the client project (plan 409 step 4b).
module SessionGatePolicyTests =

    open Expecto
    open Expecto.Flip
    open Shared
    open Shared.Types
    open SessionMachine
    open SessionGatePolicy


    let launch = Launch "l"
    let key = PublicKey "k"

    let opened =
        {
            User = None
            PatientContext = None
            OpenedToken = None
            KeyThumbprint = None
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
                                "Anonymous", Session.Anonymous
                                "Open", Session.Open opened
                                "Closing", Session.Closing opened
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
                                "Launching", Session.Launching(launch, key, 1)
                                "Resuming", Session.Resuming
                                "Unreachable", Session.Unreachable(launch, key, 3)
                                "Refused", Session.Refused(LaunchRefusal.NoRole, None)
                                "Ended", Session.Ended SessionEnding.SupersededByLaunch
                            ] do
                            test name {
                                gateFor english session |> Expect.isSome "gate"
                                isGated session |> Expect.isTrue "gated"
                            }
                    ]

                test "Ended says why, asks for a relaunch, and offers the anonymous open (Rule 11)" {
                    let gate = gateOf (Session.Ended SessionEnding.SupersededByLaunch)
                    gate.Busy |> Expect.isFalse "not busy"
                    gate.Actions |> Expect.equal "continue" [ Action.ContinueWithoutLaunch ]
                    gate.Body |> Expect.stringContains "reason" "newer session"
                    gate.Body |> Expect.stringContains "relaunch" "Open GenPRES again from MainEHR."

                    let named = namedGateOf (Session.Ended SessionEnding.SupersededByLaunch)
                    named.Title |> Expect.equal "title term" "<Session Gate Ended>"

                    named.Body
                    |> Expect.equal "body terms" "<Session Ending Superseded> <Session Relaunch>"
                }

                test "Launching is busy, names the attempt, offers nothing" {
                    let gate = gateOf (Session.Launching(launch, key, 2))
                    gate.Busy |> Expect.isTrue "busy"
                    gate.Actions |> Expect.isEmpty "no actions"

                    gate.Body
                    |> Expect.stringContains "attempt" $"attempt 2 of {Session.maxAttempts}"
                }

                test "Resuming is busy and offers nothing" {
                    let gate = gateOf Session.Resuming
                    gate.Busy |> Expect.isTrue "busy"
                    gate.Actions |> Expect.isEmpty "no actions"
                }

                test "Unreachable offers a retry and names the attempts" {
                    let gate = gateOf (Session.Unreachable(launch, key, 3))
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
                                let gate = gateOf (Session.Refused(refusal, None))
                                gate.Busy |> Expect.isFalse "not busy"
                                gate.Actions |> Expect.isEmpty "no actions"
                                // sentence-initial or after "and": the tail is the same
                                gate.Body |> Expect.stringContains "relaunch" "GenPRES again from MainEHR."
                            }
                    ]

                test "NoRole offers the anonymous open, whatever the retry" {
                    for retry in [ None; Some(launch, key) ] do
                        let gate = gateOf (Session.Refused(LaunchRefusal.NoRole, retry))
                        gate.Actions |> Expect.equal "continue" [ Action.ContinueWithoutLaunch ]
                        gate.Body |> Expect.stringContains "no patient" "no patient is carried over"
                }

                test "NoBrowserIdentity with a retry offers it" {
                    let gate =
                        gateOf (Session.Refused(LaunchRefusal.NoBrowserIdentity, Some(launch, key)))

                    gate.Actions |> Expect.equal "retry" [ Action.Retry ]
                    gate.Body |> Expect.stringContains "try again" "Try again"
                }

                test "NoBrowserIdentity without a retry asks for a relaunch" {
                    let gate = gateOf (Session.Refused(LaunchRefusal.NoBrowserIdentity, None))
                    gate.Actions |> Expect.isEmpty "no actions"
                    gate.Body |> Expect.stringContains "relaunch" "Open GenPRES again from MainEHR."
                }

                testList
                    "every text is a term, translated by the caller"
                    [
                        test "Launching" {
                            let gate = namedGateOf (Session.Launching(launch, key, 2))
                            gate.Title |> Expect.equal "title" "<Session Gate Opening>"
                            gate.Body |> Expect.equal "body" "<Session Gate Opening Text>"
                        }

                        test "Resuming" {
                            let gate = namedGateOf Session.Resuming
                            gate.Title |> Expect.equal "title" "<Session Gate Resuming>"
                            gate.Body |> Expect.equal "body" "<Session Gate Resuming Text>"
                        }

                        test "Unreachable" {
                            let gate = namedGateOf (Session.Unreachable(launch, key, 3))
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
                                let gate = namedGateOf (Session.Refused(refusal, None))
                                gate.Title |> Expect.equal "title" "<Session Gate Refused>"
                                gate.Body |> Expect.equal "body" body
                            }

                        test "Refused NoBrowserIdentity with a retry" {
                            let gate =
                                namedGateOf (Session.Refused(LaunchRefusal.NoBrowserIdentity, Some(launch, key)))

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

                    (gateOf (Session.Launching(launch, key, 2))).Body
                    |> Expect.equal "english" $"Presenting the launch, attempt 2 of {Session.maxAttempts}."

                    match gateFor template (Session.Launching(launch, key, 2)) with
                    | Some gate -> gate.Body |> Expect.equal "translated" $"poging 2 van {Session.maxAttempts}"
                    | None -> failtest "expected a gate"

                    match gateFor template (Session.Unreachable(launch, key, 3)) with
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
                        ] do
                        english term |> Expect.notEqual $"default for {term}" $"{term}"

                    english Terms.Cancel |> Expect.equal "not a session term: its name" "Cancel"
                }

                test "product names keep their case in every body" {
                    let bodies =
                        [
                            yield (gateOf (Session.Unreachable(launch, key, 3))).Body
                            for refusal in relaunchRefusals @ [ LaunchRefusal.NoRole; LaunchRefusal.NoBrowserIdentity ] do
                                yield (gateOf (Session.Refused(refusal, None))).Body
                        ]

                    for body in bodies do
                        body.Contains "genpres" |> Expect.isFalse $"lowercased GenPRES in: {body}"
                        body.Contains "mainehr" |> Expect.isFalse $"lowercased MainEHR in: {body}"
                }
            ]
