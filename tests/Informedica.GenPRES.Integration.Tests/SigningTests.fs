/// UC-3, prescribe and sign: the only way a signed version of an order plan comes into being
/// (docs/scenarios/integration/uc-03-prescribe-and-sign.md).
module Informedica.GenPRES.Integration.Tests.SigningTests

open Expecto
open Expecto.Flip

open Informedica.GenPRES.Integration.Tests.Client
open Informedica.GenPRES.Integration.Tests.StubLaunch


/// A PIN that is not the seeded one.
let wrongPin = "9876"


/// The tests, run in the sequence of the suite.
let tests =
    testList
        "UC-3 prescribe and sign"
        [
            testTask
                "the PIN signs the plan as version 1, a wrong PIN is told with the tries left, and the next right PIN signs version 2" {
                do!
                    Browser.withPage
                        "signing-versions"
                        (fun baseUrl page ->
                            task {
                                let wire = Wire.record page

                                do! page |> Steps.openSession baseUrl (newPatientId "signing") Identity.Prescriber
                                do! page |> Steps.prescribe 1

                                // the dialog lists the order as it will be signed
                                do! SignDialog.request page
                                let! listed = SignDialog.orders page

                                listed
                                |> Expect.sequenceEqual
                                    "the dialog lists the new order"
                                    [ Prescribe.paracetamol.Generic ]

                                do! SignDialog.cancel page

                                // the right PIN also zeroes the count of wrong ones on the credential
                                do! page |> Steps.sign wire 1 Identity.Prescriber

                                // signing again, unchanged: a wrong PIN keeps the dialog
                                do! SignDialog.request page
                                let! listed = SignDialog.orders page
                                listed |> Expect.isEmpty "nothing changed since version 1"

                                do! page |> SignDialog.sign wrongPin

                                let! _ = wire |> Wire.waitForAnswer "processSigning" [ "\"PinWrong\":2" ]

                                let! _ = page |> Browser.waitWords SignDialog.error [ "2" ]

                                let! stays = page |> Browser.isVisible SignDialog.selector
                                stays |> Expect.isTrue "the dialog stays open after a wrong PIN"

                                // the right PIN, in the same dialog
                                do! page |> SignDialog.sign seededPin
                                do! SignDialog.waitClosed page

                                let! _ = page |> Snackbar.waitSaying [ "2"; Identity.displayName Identity.Prescriber ]

                                let! _ =
                                    wire
                                    |> Wire.waitForAnswer "processSigning" [ Wire.case "Submitted"; "\"No\":2" ]

                                ()
                            }
                        )
            }

            testTask "a cancelled dialog submits nothing, so the first signature is still version 1" {
                do!
                    Browser.withPage
                        "signing-cancel"
                        (fun baseUrl page ->
                            task {
                                let wire = Wire.record page

                                do! page |> Steps.openSession baseUrl (newPatientId "cancel") Identity.Prescriber
                                do! page |> Steps.prescribe 1

                                // asking to sign asks for a challenge, and nothing more
                                do! SignDialog.request page
                                let! _ = wire |> Wire.waitForAnswer "processSigning" [ Wire.case "ChallengeIssued" ]
                                do! SignDialog.cancel page

                                wire
                                |> Wire.answersOf "processSigning"
                                |> List.exists _.Contains(Wire.case "Submitted")
                                |> Expect.isFalse "the cancel submitted nothing"

                                do! page |> Steps.sign wire 1 Identity.Prescriber
                            }
                        )
            }

            testTask "the next launch opens the plan on the version just signed" {
                do!
                    Browser.withPage
                        "signing-relaunch"
                        (fun baseUrl page ->
                            task {
                                let wire = Wire.record page
                                let patientId = newPatientId "relaunch"

                                do! page |> Steps.openSession baseUrl patientId Identity.Prescriber
                                do! page |> Steps.prescribe 1
                                do! page |> Steps.sign wire 1 Identity.Prescriber

                                do! page |> Steps.openSession baseUrl patientId Identity.Prescriber

                                let! _ =
                                    wire
                                    |> Wire.waitForAnswer "processSession" [ Wire.case "SessionResp"; "\"No\":1" ]

                                // the cart is the version: its order, and nothing new to sign
                                do! OrderPlan.show page
                                do! page |> OrderPlan.waitRows 1

                                do! SignDialog.request page
                                let! listed = SignDialog.orders page
                                listed |> Expect.isEmpty "the plan is the version it opened on"
                                do! SignDialog.cancel page
                            }
                        )
            }

            testTask "a patient the platform has no data for is signed after the data notice, as unverified" {
                do!
                    Browser.withPage
                        "signing-unverified"
                        (fun baseUrl page ->
                            task {
                                let wire = Wire.record page

                                // the Session opens on the data signed last for the patient, or on
                                // none: the user enters what the rules need
                                do! page |> Steps.openSession baseUrl noData Identity.Prescriber
                                do! page |> PatientPanel.choose PatientPanel.Field.Years "10"
                                do! page |> PatientPanel.choose PatientPanel.Field.Weight "32"
                                do! page |> PatientPanel.choose PatientPanel.Field.Height "140"

                                do! page |> Prescribe.prescribe Prescribe.paracetamol

                                // the platform cannot be read again: told before any PIN is asked
                                do! SignDialog.request page
                                let! _ = wire |> Wire.waitForAnswer "processSigning" [ Wire.case "DataNotice" ]

                                let! asked = SignDialog.asksPin page
                                asked |> Expect.isFalse "the data notice asks no PIN"

                                do! SignDialog.proceed page
                                do! page |> SignDialog.sign seededPin
                                do! SignDialog.waitClosed page

                                let! _ = page |> Snackbar.waitSaying [ Identity.displayName Identity.Prescriber ]

                                let! _ =
                                    wire
                                    |> Wire.waitForAnswer
                                        "processSigning"
                                        [ Wire.case "Submitted"; "\"Verified\":false" ]

                                ()
                            }
                        )
            }
        ]
