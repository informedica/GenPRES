/// UC-1, the launch: how a User gets from a patient open in the EHR to a Session for that
/// patient, and every way a launch is refused (docs/scenarios/integration/uc-01-launch.md).
module Informedica.GenPRES.Integration.Tests.LaunchTests

open System.Threading.Tasks

open Expecto
open Expecto.Flip

open Informedica.GenPRES.Integration.Tests.Client
open Informedica.GenPRES.Integration.Tests.StubLaunch


/// The refusals a launch page choice ends in, other than no-role (UC-11): the identity, the
/// word the callback names, and why the gate offers nothing but a relaunch.
let refusals =
    [
        // the registry keeps another patient active for this login
        Identity.PrescriberOtherPatient, "wrong-patient"
        // the IdentityProvider says nobody is signed on at the browser, after the redirect
        // unloaded the client, so no launch is left to retry with
        Identity.Nobody, "no-identity"
    ]


/// The tests, run in the sequence of the suite.
let tests =
    testList
        "UC-1 the launch"
        [
            testTask "a Prescriber's launch opens a Session on the launched patient" {
                do!
                    Browser.withPage
                        "launch-opens"
                        (fun baseUrl page ->
                            task {
                                let wire = Wire.record page
                                let patientId = newPatientId "launch"

                                do! page |> Steps.openSession baseUrl patientId Identity.Prescriber

                                // the callback opened the Session rather than naming a refusal
                                let! callback = wire |> Wire.waitForRedirect Wire.callbackPath 0

                                callback
                                |> Wire.refusalOf
                                |> Expect.isNone "the callback redirects to the session route without a refusal"

                                // the title bar names the patient as the EHR has it
                                do!
                                    page
                                    |> Browser.waitVisible (
                                        TitleBar.patientLine patientId patientName patientBirthDate
                                    )

                                // the Session is the User's, as the registry gave the Role, on the
                                // launched patient
                                let! _ =
                                    wire
                                    |> Wire.waitForAnswer
                                        "processSession"
                                        [
                                            Wire.case "SessionResp"
                                            "\"UserId\":\"prescriber\""
                                            "\"Role\":\"Prescriber\""
                                            $"\"PatientId\":\"%s{patientId}\""
                                        ]

                                let! shown = Browser.address page

                                shown.Contains "launch="
                                |> Expect.isFalse "the client erases the launch from the address bar"
                            }
                        )
            }

            testList
                "a refused launch opens no Session, and the gate offers a relaunch only"
                [
                    for identity, word in refusals do
                        testTask $"%s{Identity.value identity} is refused %s{word}" {
                            do!
                                Browser.withPage
                                    $"launch-refused-%s{word}"
                                    (fun baseUrl page ->
                                        task {
                                            let wire = Wire.record page

                                            do! page |> launch baseUrl (newPatientId "refused") identity

                                            // the callback is the last hop, and names the refusal
                                            let! callback = wire |> Wire.waitForRedirect Wire.callbackPath 0

                                            callback
                                            |> Wire.refusalOf
                                            |> Expect.equal "the callback names the refusal" (Some word)

                                            do! Gate.waitShown page

                                            let! actions = Gate.actionCount page

                                            actions
                                            |> Expect.equal
                                                "the gate offers nothing: the edge from the EHR is one-way"
                                                0

                                            let! opened = page |> Browser.isVisible TitleBar.anySessionButton
                                            opened |> Expect.isFalse "no Session opens"

                                            let! shown = Browser.address page

                                            shown.Contains "refused="
                                            |> Expect.isFalse "the client erases the refusal from the address bar"
                                        }
                                    )
                        }
                ]

            testTask "a Launch presented again from another browser is refused as spent" {
                do!
                    Browser.withPages
                        "launch-spent"
                        (fun baseUrl first other ->
                            task {
                                let firstWire = Wire.record first
                                let otherWire = Wire.record other

                                do! first |> Steps.openSession baseUrl (newPatientId "spent") Identity.Prescriber

                                // the launch url, as the stub launch page redirected the first
                                // browser to it: the sealed Launch in the hash
                                let launchUrl = firstWire |> Wire.redirectsFrom path |> List.exactlyOne |> _.To

                                // replayed within its two minutes, from a browser with another key
                                do! other.GoToAsync(baseUrl + launchUrl) :> Task
                                do! Gate.waitShown other

                                let! _ = otherWire |> Wire.waitForAnswer "processLaunch" [ Wire.case "LaunchSpent" ]

                                let! opened = other |> Browser.isVisible TitleBar.anySessionButton
                                opened |> Expect.isFalse "the replay opens no Session"

                                // the first browser keeps its Session: nothing opened twice
                                let! kept =
                                    first
                                    |> Browser.isVisible (
                                        TitleBar.sessionButton (Identity.displayName Identity.Prescriber)
                                    )

                                kept |> Expect.isTrue "the Session of the first presentation stays open"
                            }
                        )
            }

            testTask "a reload of the callback within the Launch's lifetime is answered as the first time" {
                do!
                    Browser.withPage
                        "launch-callback-reload"
                        (fun baseUrl page ->
                            task {
                                let wire = Wire.record page
                                let prescriber = Identity.displayName Identity.Prescriber

                                do! page |> Steps.openSession baseUrl (newPatientId "callback") Identity.Prescriber
                                let! first = wire |> Wire.waitForRedirect Wire.callbackPath 0

                                // the same callback url, its one-time code spent: the server
                                // answers from what it recorded of the launch, not from the code
                                do! page.GoToAsync first.From :> Task
                                let! again = wire |> Wire.waitForRedirect Wire.callbackPath 1

                                again.To
                                |> Expect.equal "the reload is sent where the first callback was" first.To

                                do! page |> TitleBar.waitOpen prescriber
                            }
                        )
            }
        ]
