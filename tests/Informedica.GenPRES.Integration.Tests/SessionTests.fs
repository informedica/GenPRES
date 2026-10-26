/// The Session as a user meets it in the browser: opened by a launch from the EHR, kept across
/// a reload, ended from the title bar (UC-1, UC-10: docs/scenarios/integration).
module Informedica.GenPRES.Integration.Tests.SessionTests

open System.Threading.Tasks

open Expecto
open Expecto.Flip

open Informedica.GenPRES.Integration.Tests.Client
open Informedica.GenPRES.Integration.Tests.StubLaunch


/// The tests, run in the sequence of the suite.
let tests =
    testList
        "Browser session"
        [
            testTask "a Prescriber's launch opens a Session that a reload resumes and Close session ends" {
                do!
                    Browser.withPage
                        "session-open-resume-close"
                        (fun baseUrl page ->
                            task {
                                let prescriber = TitleBar.sessionButton (Identity.displayName Identity.Prescriber)

                                // open: the launch hops through the identity provider and the
                                // callback, and the client lands on the Session
                                do! page |> launch baseUrl (newPatientId "session") Identity.Prescriber
                                do! page |> Browser.waitVisible prescriber

                                let! shown = Browser.address page

                                shown |> Expect.stringContains "the client shows the session route" "#/session"

                                shown.Contains "launch="
                                |> Expect.isFalse "the client erases the launch from the address bar"

                                // resume: a reload finds the open Session again through its cookie
                                do! page.ReloadAsync() :> Task
                                do! page |> Browser.waitVisible prescriber

                                // close: the one item of the session menu ends the Session, and
                                // the user leaves the title bar
                                do! page |> TitleBar.closeSession (Identity.displayName Identity.Prescriber)
                            }
                        )
            }

            // UC-10
            testTask
                "Close session leaves nothing behind: a reload opens anonymously, and the next launch tells nothing" {
                do!
                    Browser.withPage
                        "session-close-clean"
                        (fun baseUrl page ->
                            task {
                                let wire = Wire.record page
                                let patientId = newPatientId "close"
                                let prescriber = Identity.displayName Identity.Prescriber

                                // unsigned work on screen, which the close drops unwarned
                                do! page |> Steps.openSession baseUrl patientId Identity.Prescriber
                                do! page |> Steps.prescribe 1

                                do! page |> TitleBar.closeSession prescriber
                                let! _ = wire |> Wire.waitForAnswer "processSession" [ Wire.case "SessionClosed" ]

                                // anonymous: the patient cleared and the disclaimer back
                                do! Disclaimer.accept page

                                let! named = page |> Browser.isVisible TitleBar.anyPatientLine
                                named
                                |> Expect.isFalse "the title bar names no patient once the Session is closed"

                                // a reload finds no Session under a cookie, and once the client has
                                // started, which the gate waits for, no gate tells of one
                                do! page.ReloadAsync() :> Task

                                let! _ = wire |> Wire.waitForAnswer "processSession" [ "{\"SessionResp\":null}" ]
                                do! page |> Browser.waitGone Gate.busy

                                let! gated = page |> Browser.isVisible Gate.selector
                                gated |> Expect.isFalse "a closed Session is no ending the user is told of"

                                // the next launch opens a Session as the first did
                                do! page |> Steps.openSession baseUrl patientId Identity.Prescriber

                                wire
                                |> Wire.answersOf "processSession"
                                |> List.exists _.Contains(Wire.case "SessionEnded")
                                |> Expect.isFalse "no answer tells of an ending: the user closed the Session"
                            }
                        )
            }
        ]
