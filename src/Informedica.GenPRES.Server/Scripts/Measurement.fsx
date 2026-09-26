/// The measurement recorded. A weight, a height or a gestational age the user measures at the
/// bedside stays for the Session, whatever the EHR reads later, and a resume restores it. The
/// Session records it from the patient a computing request carries: only when that patient is
/// one (the draft passes validation), only when the value differs from the one the Session
/// stands on (the row held, else the EHR's), each in its own row dated at the request; clearing
/// a value writes a row that says none, since the latest row decides. The measurements are held
/// beside the EHR data as read, never written into it, so that the challenge keeps comparing
/// the read as read; they go onto the core patient as a dated actual and calculation value
/// where the Session's patient is projected (the merge at a notice and a commit), and onto the
/// contract patient where the client resumes.
///
/// - Session.fs `Measurements` and `Measurement`, with `record`, `ofRows`, `onCore` and `apply`;
///   the State holds them per Session, `seen` takes the request's patient and records; a
///   Persist case writes one row.
/// - Ports.fs `SessionPort.seen` takes the request's patient; Compute.fs `bound` takes
///   `patientOf` beside `aged` and passes the patient the request edits; `patientOf` per family.
/// - Mappers.Session.fs `toOpened` applies the measurements to the patient the client resumes.
/// - The store: a measurement table (session, kind, value or none, at), loaded under the
///   Session's slice, latest row per kind.
///
/// Run: cd src/Informedica.GenPRES.Server/Scripts && dotnet fsi Measurement.fsx

#I __SOURCE_DIRECTORY__

#load "load.fsx"

#r "nuget: Expecto, 10.2.3"

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

open System
open Expecto
open Expecto.Flip
// the contract before the domain, so that its measures and patient win unqualified
open Shared.Types
open Shared.Api
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open ServerApi

module CoreConversions = Informedica.GenCore.Lib.Conversions
module CoreWeightValue = Informedica.GenCore.Lib.Patients.WeightValue
module CoreWeightAtDate = Informedica.GenCore.Lib.Patients.WeightAtDate
module CoreHeightValue = Informedica.GenCore.Lib.Patients.HeightValue
module CoreHeightAtDate = Informedica.GenCore.Lib.Patients.HeightAtDate
module CoreAgeWeeksDays = Informedica.GenCore.Lib.Patients.AgeWeeksDays
module GenFormPatient = Informedica.GenForm.Lib.Patient


// ── Session.fs, what the Session holds of the user's measurements ─────────────────────────


/// A value the user measured in the Session and when; none says the user cleared it.
type Measured<'v> =
    {
        Value: 'v option
        At: DateTime
    }


/// The user's measurements a Session holds, each the latest row: nothing for one the user
/// never touched, so that the EHR's value stands; a value; or cleared.
type Measurements =
    {
        Weight: Measured<int<gram>> option
        Height: Measured<int<cm>> option
        GestAge: Measured<GestAge> option
    }


/// One measurement as the store writes it, in its own row: the kind and the value, none
/// clearing it.
[<RequireQualifiedAccess>]
type Measurement =
    | Weight of int<gram> option
    | Height of int<cm> option
    | GestAge of GestAge option


module Measurements =

    let none: Measurements =
        {
            Weight = None
            Height = None
            GestAge = None
        }


    /// The measurements after one row: the row decides its kind.
    let withRow (held: Measurements) (row: Measurement, at: DateTime) : Measurements =
        match row with
        | Measurement.Weight w -> { held with Weight = Some { Value = w; At = at } }
        | Measurement.Height h -> { held with Height = Some { Value = h; At = at } }
        | Measurement.GestAge g -> { held with GestAge = Some { Value = g; At = at } }


    /// The measurements a Session's rows come to, oldest first: the latest row per kind.
    let ofRows (rows: (Measurement * DateTime) list) : Measurements = rows |> List.fold withRow none


    /// What a request records: for each of the weight, the height and the gestational age, the
    /// draft's value when it differs from the one the Session stands on, the row held else the
    /// EHR's as shown; nothing for a draft that is no patient. The measurements after, and the
    /// rows to write.
    let record
        (now: DateTime)
        (shown: Shared.Types.Patient option)
        (held: Measurements)
        (draft: Shared.Types.Patient)
        : Measurements * Measurement list
        =
        match draft |> Patient.patient with
        | Error _ -> held, []
        | Ok _ ->
            let ehr = shown |> Option.defaultValue Shared.Models.Patient.empty

            // the value stood on, and the draft's when it is another
            let change (held: Measured<'v> option) (ehr: 'v option) (draft: 'v option) =
                let standing = held |> Option.map _.Value |> Option.defaultValue ehr

                if draft = standing then
                    held, None
                else
                    Some { Value = draft; At = now }, Some draft

            let weight, w = change held.Weight ehr.Weight.Measured draft.Weight.Measured
            let height, h = change held.Height ehr.Height.Measured draft.Height.Measured
            let gestAge, g = change held.GestAge ehr.GestationalAge draft.GestationalAge

            {
                Weight = weight
                Height = height
                GestAge = gestAge
            },
            [
                w |> Option.map Measurement.Weight
                h |> Option.map Measurement.Height
                g |> Option.map Measurement.GestAge
            ]
            |> List.choose id


    /// The core patient with the measurements on it: a value as a dated actual and the
    /// calculation value, so that the projection reads it as measured; a cleared one leaves
    /// the actuals as read and no calculation value, so that the projection has none and the
    /// estimate stands; one never touched leaves the patient as read.
    let onCore
        (held: Measurements)
        (core: Informedica.GenCore.Lib.Patients.Patient)
        : Informedica.GenCore.Lib.Patients.Patient
        =
        let weight =
            match held.Weight with
            | None -> core.Weight
            | Some { Value = None } -> { core.Weight with Calculation = None }
            | Some { Value = Some g; At = at } ->
                let w = CoreWeightAtDate.create at (CoreWeightValue.weightInGram (int g))

                { core.Weight with
                    Actual = core.Weight.Actual @ [ w ]
                    Calculation = Some w
                }

        let height =
            match held.Height with
            | None -> core.Height
            | Some { Value = None } -> { core.Height with Calculation = None }
            | Some { Value = Some cm; At = at } ->
                let h = CoreHeightAtDate.create at (CoreHeightValue.heightInCm (decimal cm))

                { core.Height with
                    Actual = core.Height.Actual @ [ h ]
                    Calculation = Some h
                }

        let gestAge =
            match held.GestAge with
            | None -> core.GestationalAge
            | Some { Value = None } -> None
            | Some { Value = Some ga } ->
                CoreAgeWeeksDays.create
                    (CoreConversions.weekFromInt (int ga.Weeks))
                    (CoreConversions.dayFromInt (int ga.Days))
                |> Some

        { core with
            Weight = weight
            Height = height
            GestationalAge = gestAge
        }


    /// The contract patient with the measurements on it, for the client that resumes: a value
    /// as the measured one, a cleared one as none, one never touched as shown.
    let apply (held: Measurements) (shown: Shared.Types.Patient) : Shared.Types.Patient =
        { shown with
            Weight =
                match held.Weight with
                | None -> shown.Weight
                | Some w -> { shown.Weight with Measured = w.Value }
            Height =
                match held.Height with
                | None -> shown.Height
                | Some h -> { shown.Height with Measured = h.Value }
            GestationalAge =
                match held.GestAge with
                | None -> shown.GestationalAge
                | Some g -> g.Value
        }


// ── The patient a request edits, per family (Compute.bound passes it to seen) ─────────────


module OrderContextCommand =

    /// The context's patient.
    let patientOf (_: OrderContextCommand, ctx: OrderContext) = Some ctx.Patient


module OrderPlanCommand =

    /// The plan's patient, the panel's; not a context's own.
    let patientOf (cmd: OrderPlanCommand) =
        match cmd with
        | OrderPlanCommand.Recalculate plan
        | OrderPlanCommand.Navigate(plan, _, _, _)
        | OrderPlanCommand.AddOrderContext(plan, _)
        | OrderPlanCommand.NewOrderContext(plan, _)
        | OrderPlanCommand.RemoveOrderContexts(plan, _) -> Some plan.Patient
        | OrderPlanCommand.Open(pat, _) -> Some pat


module FormularyCommand =

    /// The filter's patient, where it has one.
    let patientOf (form: Formulary) = form.Patient


module ParenteraliaCommand =

    let patientOf (_: Parenteralia) : Shared.Types.Patient option = None


module InteractionCommand =

    let patientOf (_: InteractionCommand) : Shared.Types.Patient option = None


// ── The proof ─────────────────────────────────────────────────────────────────────────────


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


let withWeight (g: int<gram> option) (p: Shared.Types.Patient) =
    { p with Weight = { p.Weight with Measured = g } }


let withHeight (cm: int<cm> option) (p: Shared.Types.Patient) =
    { p with Height = { p.Height with Measured = cm } }


let gestAge: GestAge = { Weeks = 36<week>; Days = 3<day> }


/// The core patient's weight and height as the projection reads them, in kg and cm.
let projected (held: Measurements) =
    let pat = { ehr with Patient = Measurements.onCore held ehr.Patient } |> StubPatientData.port.patient today

    let value unit vu =
        vu |> ValueUnit.convertTo unit |> ValueUnit.getValue |> Array.head

    pat.Weight |> Option.map (value Units.Weight.kiloGram),
    pat.Height |> Option.map (value Units.Height.centiMeter),
    pat.GestAge |> Option.map (value Units.Time.day),
    pat.WeightMeasured


let tests =
    testList
        "the measurement recorded"
        [
            test "a measured weight becomes the latest actual, and the calculation value" {
                let held, rows = Measurements.record later (Some shown) Measurements.none (shown |> withWeight (Some 34000<gram>))

                let core = Measurements.onCore held ehr.Patient

                (rows,
                 held.Weight,
                 core.Weight.Actual |> List.map (fun w -> w.Date, CoreWeightValue.getWeightInKg w.Weight |> decimal),
                 core.Weight.Calculation |> Option.map (fun w -> CoreWeightValue.getWeightInKg w.Weight |> decimal))
                |> Expect.equal
                    "one row; held at the request; the EHR's actual then the user's; the user's for the calculation"
                    ([ Measurement.Weight(Some 34000<gram>) ],
                     Some { Value = Some 34000<gram>; At = later },
                     [ StubPatientData.measuredOn, 32m; later, 34m ],
                     Some 34m)
            }

            test "the same weight again writes nothing, and the EHR's own weight sent back writes nothing" {
                let held, _ = Measurements.record later (Some shown) Measurements.none (shown |> withWeight (Some 34000<gram>))

                (Measurements.record (later.AddHours 1.0) (Some shown) held (shown |> withWeight (Some 34000<gram>)),
                 Measurements.record later (Some shown) Measurements.none shown)
                |> Expect.equal "held as it was, no rows, both times" ((held, []), (Measurements.none, []))
            }

            test "a draft that fails validation writes nothing" {
                let noPatient =
                    { shown with
                        Age = None
                        Height = { shown.Height with Measured = None; Estimated = None }
                    }
                    |> withWeight (Some 34000<gram>)

                Measurements.record later (Some shown) Measurements.none noPatient
                |> Expect.equal "nothing recorded" (Measurements.none, [])
            }

            test "a resume restores the measurement: from the rows, onto the contract patient and through the projection" {
                let held = Measurements.ofRows [ Measurement.Weight(Some 33000<gram>), today; Measurement.Weight(Some 34000<gram>), later ]

                ((Measurements.apply held shown).Weight.Measured, projected held)
                |> Expect.equal
                    "the latest row, measured on both paths"
                    (Some 34000<gram>, (Some 34N, Some 140N, None, true))
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

                let held = Measurements.ofRows [ Measurement.Weight(Some 34000<gram>), today; Measurement.Weight None, later ]

                (rows, (Measurements.apply held shown).Weight.Measured, projected held)
                |> Expect.equal
                    "a row that says none; no measured weight on either path, the actuals untouched"
                    ([ Measurement.Weight None ], None, (None, Some 140N, None, false))
            }

            test "the height and the gestational age are recorded the same way" {
                let draft = shown |> withHeight (Some 150<cm>) |> fun p -> { p with GestationalAge = Some gestAge }

                let held, rows = Measurements.record later (Some shown) Measurements.none draft

                (rows, (Measurements.apply held shown).Height.Measured, (Measurements.apply held shown).GestationalAge, projected held)
                |> Expect.equal
                    "two rows; both on the contract patient; 150 cm and 255 days through the projection"
                    ([ Measurement.Height(Some 150<cm>); Measurement.GestAge(Some gestAge) ],
                     Some 150<cm>,
                     Some gestAge,
                     (Some 32N, Some 150N, Some 255N, true))
            }

            test "the patient a request edits, per family" {
                let ctx = { Shared.Models.OrderContext.empty with Patient = shown }
                let other = { Shared.Models.OrderContext.empty with Patient = shown |> withWeight (Some 1000<gram>) }
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


runTestsWithCLIArgs [] [||] tests
