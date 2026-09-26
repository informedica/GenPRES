/// What the Session holds of the user's measurements: recorded from a request only for a
/// draft that is a patient and only on change, in rows the latest of which decides; put on the
/// contract patient where the client resumes.
module Informedica.GenPRES.Server.Tests.MeasurementsTests

open System
open Expecto
open Expecto.Flip
open Shared.Types
open Shared.Api
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open ServerApi
open Informedica.GenPRES.Server.Tests.StubAdapterTests
open Informedica.GenPRES.Server.Tests.StubAdapterTests.StubAdapters

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


/// A Session on the stub patient, open at today.
let sid = "s-1"

let state =
    { Session.emptyState with
        Sessions =
            Map.ofList
                [
                    sid,
                    {
                        Opened =
                            {
                                User = None
                                PatientId = Some "p1"
                                EhrData = Some ehr
                                Patient = Some(StubPatientData.port.patient today ehr)
                                Measured = Measurements.none
                                OpenedToken = Some(OpenedToken "opened-1")
                                KeyThumbprint = Some "t"
                                Head = None
                            }
                        Login = None
                        OpenedWith = None
                        OpenedAt = today
                        Seen = today
                    }
                ]
    }


let seenWith (draft: Patient option) (state: Session.State) =
    let state, _, writes = Session.seen later sid (Some(OpenedToken "opened-1")) draft state
    state, writes


/// The core patient's weight, height and gestational age as the projection reads them, with
/// whether the weight is measured and whether a post-menstrual age follows.
let projected (held: Measurements) =
    let pat =
        { ehr with Patient = Measurements.onCore held ehr.Patient }
        |> StubPatientData.port.patient today

    let value unit vu =
        vu |> ValueUnit.convertTo unit |> ValueUnit.getValue |> Array.head

    pat.Weight |> Option.map (value Units.Weight.kiloGram),
    pat.Height |> Option.map (value Units.Height.centiMeter),
    pat.GestAge |> Option.map (value Units.Time.day),
    pat.WeightMeasured,
    pat.PMAge.IsSome


/// A challenge standing over the Session's data.
let standing: Session.Challenge =
    {
        Nonce = "c-1"
        Digest = "digest"
        Ehr = Some ehr
        Reading = Some(StubPatientData.port.patient today ehr)
        Expiry = later + Session.challengeLifetime
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

            testList
                "on the core patient, through the projection"
                [
                    test
                        "a weight, a height and a gestational age set are measured, with the post-menstrual age following" {
                        Measurements.ofRows
                            [
                                Measurement.Weight(Some 34000<gram>), later
                                Measurement.Height(Some 150<cm>), later
                                Measurement.GestAge(Some gestAge), later
                            ]
                        |> projected
                        |> Expect.equal
                            "34 kg measured, 150 cm, 255 days, a post-menstrual age"
                            (Some 34N, Some 150N, Some 255N, true, true)
                    }

                    test
                        "cleared, the weight and the height are none and the estimate's, the gestational age gone with the post-menstrual age" {
                        let set = Measurements.ofRows [ Measurement.GestAge(Some gestAge), today ]

                        (Measurements.ofRows
                            [
                                Measurement.Weight None, later
                                Measurement.Height None, later
                                Measurement.GestAge None, later
                            ]
                         |> projected,
                         (projected set |> fun (_, _, g, _, pm) -> g, pm))
                        |> Expect.equal
                            "none, none, none, unmeasured, no post-menstrual age; and set before the clear: 255 days with one"
                            ((None, None, None, false, false), (Some 255N, true))
                    }

                    test "never touched, the patient is as read" {
                        Measurements.none
                        |> projected
                        |> Expect.equal
                            "the EHR's 32 kg measured and 140 cm, no gestational age"
                            (Some 32N, Some 140N, None, true, false)
                    }
                ]

            testList
                "seen records what the request's patient measures"
                [
                    test "a row per value that changed, held from then on; the same again writes nothing" {
                        let state, writes = state |> seenWith (Some(shown |> withWeight (Some 34000<gram>)))

                        let held = state.Sessions[sid].Opened.Measured

                        let _, again = state |> seenWith (Some(shown |> withWeight (Some 34000<gram>)))

                        (writes
                         |> List.filter (
                             function
                             | Session.WriteMeasurement _ -> true
                             | _ -> false
                         ),
                         held.Weight,
                         again)
                        |> Expect.equal
                            "one measurement row; held at the request; then the heartbeat alone"
                            ([ Session.WriteMeasurement(sid, Measurement.Weight(Some 34000<gram>), later) ],
                             Some
                                 {
                                     Value = Some 34000<gram>
                                     At = later
                                 },
                             [ Session.RecordSeen(sid, later) ])
                    }

                    test "a change spends the challenge standing over the data before; no change leaves it" {
                        let ready = { state with Challenges = Map.ofList [ sid, standing ] }

                        let changed, writes = ready |> seenWith (Some(shown |> withWeight (Some 34000<gram>)))
                        let same, again = ready |> seenWith (Some shown)

                        (changed.Challenges |> Map.isEmpty,
                         writes |> List.contains (Session.SpendChallenge(sid, "c-1", later)),
                         same.Challenges |> Map.containsKey sid,
                         again)
                        |> Expect.equal
                            "spent and gone; standing, the heartbeat alone"
                            (true, true, true, [ Session.RecordSeen(sid, later) ])
                    }

                    test "a request without a patient, or the EHR's values sent back, records nothing" {
                        [ state |> seenWith None |> snd; state |> seenWith (Some shown) |> snd ]
                        |> Expect.allEqual "the heartbeat alone" [ Session.RecordSeen(sid, later) ]
                    }

                    test "bound passes the patient the request edits, as sent" {
                        let captured = ref None

                        let env =
                            { makeEnv (formularyAlwaysOk Shared.Models.Formulary.empty) (orderContextAlwaysOk emptyCtx) with
                                session =
                                    { sessionNone with
                                        seen =
                                            fun _ _ draft ->
                                                async {
                                                    captured.Value <- draft
                                                    return None, None
                                                }
                                    }
                            }

                        let cookie: SessionCookie =
                            {
                                read = fun () -> Some sid
                                write = ignore
                                delete = ignore
                            }

                        let ctx =
                            { Shared.Models.OrderContext.empty with Patient = shown |> withWeight (Some 34000<gram>) }

                        Compute.bound
                            env
                            cookie
                            (fun _ -> "test")
                            (fun _ -> Gate.Open)
                            OrderContextCommand.aged
                            OrderContextCommand.patientOf
                            (fun _ -> async { return Ok() })
                            {
                                Opened = None
                                Command = (OrderContextCommand.UpdateOrderContext, ctx)
                            }
                        |> Async.RunSynchronously
                        |> ignore

                        captured.Value |> Expect.equal "the context's patient" (Some ctx.Patient)
                    }

                    test "the patient a request edits, per family" {
                        let ctx = { Shared.Models.OrderContext.empty with Patient = shown }

                        let other =
                            { Shared.Models.OrderContext.empty with Patient = shown |> withWeight (Some 1000<gram>) }

                        let plan = Shared.Models.OrderPlan.create shown [| other |]

                        (OrderContextCommand.patientOf (OrderContextCommand.UpdateOrderContext, ctx),
                         [
                             OrderPlanCommand.Recalculate plan
                             OrderPlanCommand.Navigate(plan, "1", OrderContextCommand.UpdateOrderContext, other)
                             OrderPlanCommand.AddOrderContext(plan, other)
                             OrderPlanCommand.NewOrderContext(plan, NutritionCategory.TPN)
                             OrderPlanCommand.RemoveOrderContexts(plan, [| "1" |])
                             OrderPlanCommand.Open(shown, [| other |])
                         ]
                         |> List.map OrderPlanCommand.patientOf
                         |> List.distinct,
                         FormularyCommand.patientOf { Shared.Models.Formulary.empty with Patient = Some shown },
                         FormularyCommand.patientOf Shared.Models.Formulary.empty,
                         ParenteraliaCommand.patientOf Shared.Models.Parenteralia.empty,
                         InteractionCommand.patientOf InteractionCommand.GetDrugNames)
                        |> Expect.equal
                            "the context's; the plan's, never a context's own; the filter's or none; none; none"
                            (Some shown, [ Some shown ], Some shown, None, None, None)
                    }
                ]
        ]
