/// UC-11, a User's authority is withdrawn: the registry returns no standing, and the User keeps
/// anonymous decision support and nothing more
/// (docs/scenarios/integration/uc-11-authority-withdrawn.md).
///
/// The stub registry cannot withdraw a standing while the server runs, so the withdrawal during
/// an open Session, where only the signature fails, is pinned by the server tests and not here.
module Informedica.GenPRES.Integration.Tests.AuthorityWithdrawnTests

open Expecto
open Expecto.Flip

open Informedica.GenPRES.Integration.Tests.Client
open Informedica.GenPRES.Integration.Tests.StubLaunch


/// The tests, run in the sequence of the suite.
let tests =
    testList
        "UC-11 authority withdrawn"
        [
            testTask
                "a login the registry does not know is refused no-role, and continues without a launch carrying nothing over" {
                do!
                    Browser.withPage
                        "authority-no-role"
                        (fun baseUrl page ->
                            task {
                                let wire = Wire.record page

                                do! page |> launch baseUrl (newPatientId "no-role") Identity.Unknown

                                let! callback = wire |> Wire.waitForRedirect Wire.callbackPath 0

                                callback
                                |> Wire.refusalOf
                                |> Expect.equal "the callback names the refusal" (Some "no-role")

                                do! Gate.waitShown page

                                // a relaunch would be answered the same, so the gate offers the
                                // anonymous open instead
                                let! actions = Gate.actionCount page
                                actions |> Expect.equal "the gate offers one action: continue without launch" 1

                                do! Gate.continueWithoutLaunch page
                                do! Disclaimer.accept page

                                let! opened = page |> Browser.isVisible TitleBar.anySessionButton
                                opened |> Expect.isFalse "no User: there is no Session"

                                let! named = page |> Browser.isVisible TitleBar.anyPatientLine
                                named |> Expect.isFalse "no patient is carried over from the launch"
                            }
                        )
            }
        ]
