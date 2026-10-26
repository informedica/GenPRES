/// UC-4, two Users on one patient: neither sees the other's work, the first to sign wins the
/// version, and the other is told at their next request
/// (docs/scenarios/integration/uc-04-two-users.md).
module Informedica.GenPRES.Integration.Tests.TwoUsersTests

open System.Threading.Tasks

open Expecto
open Expecto.Flip

open Informedica.GenPRES.Integration.Tests.Client
open Informedica.GenPRES.Integration.Tests.StubLaunch


/// The tests, run in the sequence of the suite.
let tests =
    testList
        "UC-4 two users"
        [
            testTask
                "B is told when A signs first, is refused over the older version, and signs version 2 once B opens A's" {
                do!
                    Browser.withPages
                        "two-users"
                        (fun baseUrl a b ->
                            task {
                                let wireA = Wire.record a
                                let wireB = Wire.record b
                                let patientId = newPatientId "two-users"
                                let nameA = Identity.displayName Identity.Prescriber

                                // both open on the patient's record as it stands: no version
                                do! a |> Steps.openSession baseUrl patientId Identity.Prescriber
                                do! b |> Steps.openSession baseUrl patientId Identity.PrescriberB

                                // A signs first
                                do! a |> Steps.prescribe baseUrl 1
                                do! a |> Steps.sign wireA 1 Identity.Prescriber

                                // B's next request, any computing one, is told; nothing is blocked
                                do! b |> Steps.prescribe baseUrl 1
                                let! _ = wireB |> Wire.waitForAnswer "processOrderContext" [ Wire.case "NewerVersion" ]
                                let! _ = b |> Snackbar.waitSaying [ nameA ]
                                do! b |> Browser.waitVisible OrderPlan.movedOnBar

                                // signing over the version B opened with is refused, A's named
                                do! OrderPlan.waitSignable b
                                do! b.ClickAsync OrderPlan.signButton

                                let! _ =
                                    wireB
                                    |> Wire.waitForAnswer
                                        "processSigning"
                                        [ Wire.case "Blocked"; "\"UserId\":\"prescriber\"" ]

                                // settled at the challenge, before a PIN B was never going to spend:
                                // the dialog closes on the refusal
                                do! SignDialog.waitClosed b

                                wireB
                                |> Wire.answersOf "processSigning"
                                |> List.length
                                |> Expect.equal "one signing request was sent: the challenge, refused" 1

                                // opening A's version is what unblocks B: the cart is A's
                                do! OrderPlan.openNewestVersion b

                                let! _ = b |> Snackbar.waitSaying [ "1"; nameA ]

                                do! b |> OrderPlan.waitRows 1

                                // and B's signature lands on top of it, its base A's version
                                do! b |> Steps.prescribe baseUrl 2
                                do! b |> Steps.sign wireB 2 Identity.PrescriberB
                            }
                        )
            }

            testTask "when both sign at once over the same version, one version is appended and the other is refused" {
                do!
                    Browser.withPages
                        "two-users-race"
                        (fun baseUrl a b ->
                            task {
                                let wireA = Wire.record a
                                let wireB = Wire.record b
                                let patientId = newPatientId "race"

                                do! a |> Steps.openSession baseUrl patientId Identity.Prescriber
                                do! b |> Steps.openSession baseUrl patientId Identity.PrescriberB

                                // both hold a challenge over the same base
                                for page in [ a; b ] do
                                    do! page |> Steps.prescribe baseUrl 1
                                    do! SignDialog.request page
                                    do! page |> Browser.waitVisible $"%s{SignDialog.pinField}:not([disabled])"
                                    do! page |> Browser.fill SignDialog.pinField seededPin

                                // and both press Sign at once
                                let press (page: PuppeteerSharp.IPage) = page.ClickAsync SignDialog.primary
                                do! Task.WhenAll(press a, press b)

                                let settled (wire: Wire.Recording) () =
                                    wire
                                    |> Wire.answersOf "processSigning"
                                    |> List.exists (fun body ->
                                        body.Contains(Wire.case "Submitted") || body.Contains(Wire.case "Blocked")
                                    )

                                do! Browser.waitUntil "A's signature to be answered" (settled wireA)
                                do! Browser.waitUntil "B's signature to be answered" (settled wireB)

                                let outcome (wire: Wire.Recording) =
                                    match
                                        wire
                                        |> Wire.answersOf "processSigning"
                                        |> List.tryFind (fun body ->
                                            body.Contains(Wire.case "Submitted") || body.Contains(Wire.case "Blocked")
                                        )
                                    with
                                    | Some body when body.Contains(Wire.case "Submitted") -> "signed"
                                    | Some _ -> "blocked"
                                    | None -> "unanswered"

                                [ outcome wireA; outcome wireB ]
                                |> List.sort
                                |> Expect.equal
                                    "the head check and the append are one act: one signs, the other finds the head moved"
                                    [ "blocked"; "signed" ]

                                Wire.answersOf "processSigning" wireA @ Wire.answersOf "processSigning" wireB
                                |> List.exists _.Contains("\"No\":2")
                                |> Expect.isFalse "only version 1 was appended"
                            }
                        )
            }
        ]
