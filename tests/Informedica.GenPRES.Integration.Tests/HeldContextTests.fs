/// UC-12, the patient context is held while the plan has new or changed orders: from the first
/// new order to the signature, the patient data takes no change, so that every order a signed
/// version adds rests on one patient context
/// (docs/scenarios/integration/uc-12-held-patient-context.md).
///
/// The gender stands for the patient data here: its controls stay enabled while the context is
/// held and refuse the change, and their values do not depend on the language.
module Informedica.GenPRES.Integration.Tests.HeldContextTests

open PuppeteerSharp

open Expecto
open Expecto.Flip

open Informedica.GenPRES.Integration.Tests.Client
open Informedica.GenPRES.Integration.Tests.StubLaunch


/// Tries to change the gender while the context is held: the panel asks why it cannot instead,
/// and keeps the gender it had.
let attemptGender (value: string) (page: IPage) =
    task {
        let! before = PatientPanel.gender page
        do! page |> PatientPanel.clickGender value
        do! page |> Browser.waitVisible PatientPanel.heldQuestion

        let! after = PatientPanel.gender page
        after |> Expect.equal "the held panel keeps the gender it had" before
    }


/// Changes the gender while the context is released, and waits until the panel holds it.
let changeGender (value: string) (page: IPage) =
    task {
        do! page |> PatientPanel.clickGender value
        do! page |> PatientPanel.waitGender value
    }


/// The tests, run in the sequence of the suite.
let tests =
    testList
        "UC-12 held patient context"
        [
            testTask "the first new order holds the patient data until the plan is signed" {
                do!
                    Browser.withPage
                        "held-until-signed"
                        (fun baseUrl page ->
                            task {
                                let wire = Wire.record page

                                do! page |> Steps.openSession baseUrl (newPatientId "held") Identity.Prescriber

                                // released: no order is new, the data can be changed
                                do! page |> changeGender "male"

                                // the first new order holds it; keeping the orders keeps the hold
                                do! page |> Steps.prescribe baseUrl 1
                                do! page |> attemptGender "female"
                                do! PatientPanel.keepOrders page
                                do! page |> OrderPlan.waitRows 1

                                // the signature makes the plan its version: released
                                do! page |> Steps.sign wire 1 Identity.Prescriber
                                do! page |> changeGender "female"
                            }
                        )
            }

            testTask "removing the new orders releases the patient data" {
                do!
                    Browser.withPage
                        "held-remove"
                        (fun baseUrl page ->
                            task {
                                do! page |> Steps.openSession baseUrl (newPatientId "held") Identity.Prescriber
                                do! page |> Steps.prescribe baseUrl 1

                                do! page |> attemptGender "female"
                                do! PatientPanel.removeNewOrders page
                                do! page |> OrderPlan.waitRows 0

                                do! page |> changeGender "female"
                            }
                        )
            }

            testTask "a refresh from the EHR drops the new orders and reopens the signed version, released" {
                do!
                    Browser.withPage
                        "held-refresh"
                        (fun baseUrl page ->
                            task {
                                let wire = Wire.record page

                                do! page |> Steps.openSession baseUrl (newPatientId "held") Identity.Prescriber
                                do! page |> Steps.prescribe baseUrl 1
                                do! page |> Steps.sign wire 1 Identity.Prescriber

                                // an order on top of version 1 holds the data again
                                do! page |> Steps.prescribe baseUrl 2
                                do! page |> attemptGender "male"
                                do! PatientPanel.refreshFromEhr page

                                // the version again, without the new order
                                do! page |> OrderPlan.waitRows 1
                                do! page |> changeGender "male"
                            }
                        )
            }
        ]
