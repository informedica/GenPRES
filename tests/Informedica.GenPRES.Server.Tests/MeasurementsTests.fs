/// What the Session holds of the user's measurements: recorded from a request only for a
/// draft that is a patient and only on change, in rows the latest of which decides; put on the
/// contract patient where the client resumes.
module Informedica.GenPRES.Server.Tests.MeasurementsTests

open System
open Expecto
open Expecto.Flip
open Shared.Types
open ServerApi

module GenFormPatient = Informedica.GenForm.Lib.Patient


/// The date of the open, and the request a day later.
let today = DateTime(2026, 9, 26)

let later = today.AddDays 1.0


let ehr = StubPatientData.data "p1"


/// The EHR's projection as the client is shown it: 32 kg and 140 cm measured, no gestational
/// age.
let shown =
    StubPatientData.port.patient today ehr
    |> GenFormPatient.Dto.toDto
    |> Patient.toModel


let withWeight (g: int<gram> option) (p: Patient) = { p with Weight = { p.Weight with Measured = g } }


let withHeight (cm: int<cm> option) (p: Patient) = { p with Height = { p.Height with Measured = cm } }


let gestAge: GestAge =
    {
        Weeks = 36<week>
        Days = 3<day>
    }


[<Tests>]
let tests =
    testList
        "the measurement recorded"
        [
            test "a measured weight becomes the latest, dated at the request" {
                let held, rows =
                    Measurements.record later (Some shown) Measurements.none (shown |> withWeight (Some 34000<gram>))

                (rows, held.Weight)
                |> Expect.equal
                    "one row; held at the request"
                    ([ Measurement.Weight(Some 34000<gram>) ],
                     Some
                         {
                             Value = Some 34000<gram>
                             At = later
                         })
            }

            test "the same weight again writes nothing, and the EHR's own weight sent back writes nothing" {
                let held, _ =
                    Measurements.record later (Some shown) Measurements.none (shown |> withWeight (Some 34000<gram>))

                (Measurements.record (later.AddHours 1.0) (Some shown) held (shown |> withWeight (Some 34000<gram>)),
                 Measurements.record later (Some shown) Measurements.none shown)
                |> Expect.equal "held as it was, no rows, both times" ((held, []), (Measurements.none, []))
            }

            test "a draft that fails validation writes nothing" {
                let noPatient =
                    { shown with
                        Age = None
                        Height =
                            { shown.Height with
                                Measured = None
                                Estimated = None
                            }
                    }
                    |> withWeight (Some 34000<gram>)

                Measurements.record later (Some shown) Measurements.none noPatient
                |> Expect.equal "nothing recorded" (Measurements.none, [])
            }

            test "a resume restores the measurement: from the rows, onto the contract patient" {
                let held =
                    Measurements.ofRows
                        [
                            Measurement.Weight(Some 33000<gram>), today
                            Measurement.Weight(Some 34000<gram>), later
                        ]

                (Measurements.apply held shown).Weight.Measured
                |> Expect.equal "the latest row" (Some 34000<gram>)
            }

            test "a cleared weight stays cleared after a resume, whatever the EHR's actual" {
                // the panel shows the estimate once the measured weight is cleared, and sends it
                let cleared =
                    { shown with
                        Weight =
                            { shown.Weight with
                                Measured = None
                                Estimated = Some 30000<gram>
                            }
                    }

                let _, rows = Measurements.record later (Some shown) Measurements.none cleared

                let held =
                    Measurements.ofRows [ Measurement.Weight(Some 34000<gram>), today; Measurement.Weight None, later ]

                (rows, (Measurements.apply held shown).Weight.Measured)
                |> Expect.equal
                    "a row that says none; no measured weight, whatever the EHR's"
                    ([ Measurement.Weight None ], None)
            }

            test "the height and the gestational age are recorded the same way" {
                let draft =
                    shown
                    |> withHeight (Some 150<cm>)
                    |> fun p -> { p with GestationalAge = Some gestAge }

                let held, rows = Measurements.record later (Some shown) Measurements.none draft
                let resumed = Measurements.apply held shown

                (rows, resumed.Height.Measured, resumed.GestationalAge)
                |> Expect.equal
                    "two rows; both on the contract patient"
                    ([ Measurement.Height(Some 150<cm>); Measurement.GestAge(Some gestAge) ], Some 150<cm>, Some gestAge)
            }

            test "the client resumes on the patient with the measurements on it" {
                let held = Measurements.ofRows [ Measurement.Weight(Some 34000<gram>), later ]

                let opened: OpenedSession =
                    {
                        User = None
                        PatientId = Some "p1"
                        EhrData = Some ehr
                        Patient = Some(StubPatientData.port.patient today ehr)
                        Measured = held
                        OpenedToken = Some(OpenedToken "opened-1")
                        KeyThumbprint = Some "t"
                        Head = None
                    }

                (SessionMapper.toOpened false opened).PatientContext
                |> Option.bind _.Patient
                |> Option.map _.Weight.Measured
                |> Expect.equal "the weight the user measured, not the EHR's" (Some(Some 34000<gram>))
            }
        ]
