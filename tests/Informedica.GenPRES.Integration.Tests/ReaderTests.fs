/// UC-9, a Reader consults a patient: they see the version that counts, are told when it moves
/// on, and nothing they do can be signed (docs/scenarios/integration/uc-09-reader.md).
module Informedica.GenPRES.Integration.Tests.ReaderTests

open Expecto
open Expecto.Flip

open Informedica.GenPRES.Integration.Tests.Client
open Informedica.GenPRES.Integration.Tests.StubLaunch


/// The tests, run in the sequence of the suite.
let tests =
    testList
        "UC-9 reader"
        [
            testTask
                "a Reader reads the newest version, is told of a newer one and opens it, and is never offered to sign" {
                do!
                    Browser.withPages
                        "reader"
                        (fun baseUrl prescriber reader ->
                            task {
                                let prescriberWire = Wire.record prescriber
                                let readerWire = Wire.record reader
                                let patientId = newPatientId "reader"
                                let nameA = Identity.displayName Identity.Prescriber

                                // the patient has a version
                                do! prescriber |> Steps.openSession baseUrl patientId Identity.Prescriber
                                do! prescriber |> Steps.prescribe 1
                                do! prescriber |> Steps.sign prescriberWire 1 Identity.Prescriber

                                // the Reader's launch asks no PIN and opens on that version
                                do! reader |> Steps.openSession baseUrl patientId Identity.Reader

                                let! _ =
                                    readerWire
                                    |> Wire.waitForAnswer "processSession" [ "\"Role\":\"Reader\""; "\"No\":1" ]

                                do! OrderPlan.show reader
                                do! reader |> OrderPlan.waitRows 1

                                let! signable = OrderPlan.offersSign reader
                                signable |> Expect.isFalse "a Reader is offered no Sign"

                                // the Prescriber signs a newer version meanwhile
                                do! prescriber |> Steps.prescribe 2
                                do! prescriber |> Steps.sign prescriberWire 2 Identity.Prescriber

                                // the Reader prescribes like anyone, and is told like anyone
                                do! reader |> Steps.prescribe 2

                                let! _ =
                                    readerWire
                                    |> Wire.waitForAnswer "processOrderContext" [ Wire.case "NewerVersion" ]

                                let! _ = reader |> Snackbar.waitSaying [ nameA ]

                                let! signable = OrderPlan.offersSign reader
                                signable
                                |> Expect.isFalse "a Reader's own order is not offered for signing either"

                                // any version may be opened, and the newest is what counts
                                do! OrderPlan.openNewestVersion reader

                                let! _ = reader |> Snackbar.waitSaying [ "2"; nameA ]

                                do! reader |> OrderPlan.waitRows 2

                                readerWire
                                |> Wire.answersOf "processSigning"
                                |> Expect.isEmpty "the Reader's client never asked to sign"
                            }
                        )
            }
        ]
