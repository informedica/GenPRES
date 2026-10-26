/// UC-8, a Session ends out from under the User: the server cannot reach a client, so the old
/// tab looks alive until its next request, which is told the ending
/// (docs/scenarios/integration/uc-08-session-ends.md).
///
/// The ending drawn is the newer launch of the same User elsewhere; the wrong-PIN limit is
/// UC-5's. An idle Session is told the same way, but only after the idle lifetime, an hour
/// unless GENPRES_SESSION_IDLE_MINUTES sets another, so it is left to the server tests.
module Informedica.GenPRES.Integration.Tests.SessionEndingTests

open System.Threading.Tasks

open Expecto
open Expecto.Flip

open Informedica.GenPRES.Integration.Tests.Client
open Informedica.GenPRES.Integration.Tests.StubLaunch


/// The tests, run in the sequence of the suite.
let tests =
    testList
        "UC-8 session ends"
        [
            testTask
                "a Session superseded by a newer launch elsewhere is told at the old tab's next request, and its work goes with it" {
                do!
                    Browser.withPages
                        "ended-next-request"
                        (fun baseUrl old newer ->
                            task {
                                let oldWire = Wire.record old
                                let patientId = newPatientId "superseded"
                                let prescriber = Identity.displayName Identity.Prescriber

                                // unsigned work on the old tab
                                do! old |> Steps.openSession baseUrl patientId Identity.Prescriber
                                do! old |> Steps.prescribe 1

                                // the same User launches again elsewhere: the old tab cannot be told
                                do! newer |> Steps.openSession baseUrl patientId Identity.Prescriber

                                let! stillShown = old |> Browser.isVisible (TitleBar.sessionButton prescriber)
                                stillShown |> Expect.isTrue "the old tab still shows what it last heard"

                                // its next request is told, beside the answer it asked for: a
                                // medication picked on the prescribing page
                                do! Prescribe.show old
                                do! old |> Prescribe.choose Prescribe.Field.Medication Prescribe.paracetamol.Generic
                                do! Gate.waitShown old

                                let! _ =
                                    oldWire
                                    |> Wire.waitForAnswer "processOrderContext" [ Wire.case "SupersededByLaunch" ]

                                // being told is acknowledging: the client closes the ended Session
                                let! _ = oldWire |> Wire.waitForAnswer "processSession" [ Wire.case "SessionClosed" ]

                                let! actions = Gate.actionCount old
                                actions |> Expect.equal "the gate offers to continue without launch" 1

                                // continuing carries nothing over: no User, no patient, no orders
                                do! Gate.continueWithoutLaunch old
                                do! Disclaimer.accept old

                                let! opened = old |> Browser.isVisible TitleBar.anySessionButton
                                opened |> Expect.isFalse "the old tab has no Session"

                                // the newer Session stands
                                let! kept = newer |> Browser.isVisible (TitleBar.sessionButton prescriber)
                                kept |> Expect.isTrue "the newer launch keeps its Session"
                            }
                        )
            }

            testTask "a reload of a superseded tab is told the ending in the same way" {
                do!
                    Browser.withPages
                        "ended-reload"
                        (fun baseUrl old newer ->
                            task {
                                let oldWire = Wire.record old
                                let patientId = newPatientId "superseded"

                                do! old |> Steps.openSession baseUrl patientId Identity.Prescriber
                                do! newer |> Steps.openSession baseUrl patientId Identity.Prescriber

                                do! old.ReloadAsync() :> Task
                                do! Gate.waitShown old

                                let! _ =
                                    oldWire
                                    |> Wire.waitForAnswer
                                        "processSession"
                                        [ "{\"SessionEnded\":\"SupersededByLaunch\"}" ]

                                let! opened = old |> Browser.isVisible TitleBar.anySessionButton
                                opened |> Expect.isFalse "the reloaded tab opens no Session"
                            }
                        )
            }
        ]
