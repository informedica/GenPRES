namespace Informedica.GenPRES.Shared.Tests


/// The session gate's policy, linked in from the client project (plan 409 step 4b).
module SessionGatePolicyTests =

    open Expecto
    open Expecto.Flip
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
        match gateFor session with
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
                            test name { gateFor session |> Expect.isNone "no gate" }
                    ]

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
