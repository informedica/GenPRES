/// The EHR's patient data as the domain's own patient: the core patient, with the identity on
/// it, plus the data only the rules read, and one projection to the GenFORM patient at a
/// date. Today the port returns the projection alone, with an age in days and no identity,
/// so nothing downstream can say who the patient is or compute the age again.
///
/// Four libraries prototyped at once, each shadowed under its own name:
///
/// - GenCORE.Lib `Patients`: the patient id and the name on the core patient; the birthdate
///   fully specified, a year, a month and a day, where today the month and the day are
///   optional; who the patient is, as `PatientIdentity`, from a name and a birthdate. The
///   script keeps the rest on the original types behind `data`, so that the original
///   functions stay in use; at migration the two fields join the record, the two options go
///   from the birthdate and its Dto's validators require them, and `data` goes.
/// - GenFORM.Lib `GenForm`: `EhrPatientData`, the core patient plus renal function and access;
///   `Patient.ofEhr now`, the projection: age from the birthdate at the date, in days; weight
///   and height from the calculation values, measured when present; gestational age in days
///   and the post-menstrual age calculated from it; the department's own name; access and
///   renal function as given. BSA stays what GenFORM calculates from weight and height.
/// - GenPRES.Server: the port's `read` answering EHR data; the stub building it for its fixed
///   patient, with a constant name and birthdate, none for `no-data`; `OpenedSession` keeping
///   the EHR data as read beside the projection; `SessionMapper.toOpened` putting the
///   identity on the patient context.
/// - GenPRES.Shared `Contract`: `PatientIdentity` on `PatientContext`, the birthdate as three
///   integers, as the url already carries it.
///
/// The projection's age at the open, the estimate over it and the challenge follow in their
/// own step; here the stub is projected at its patient's tenth birthday and compared with
/// today's stub answer, field for field.
///
/// Run: cd src/Informedica.GenPRES.Server/Scripts && dotnet fsi EhrData.fsx

#I __SOURCE_DIRECTORY__

#load "load.fsx"

#r "nuget: Expecto, 10.2.3"

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

open System
open Expecto
open Expecto.Flip
open MathNet.Numerics
// the contract before the domain, so that the domain's units, measures and patient win unqualified
open Shared.Types
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenCore.Lib
open Informedica.GenOrder.Lib
open ServerApi


// ── GenCORE.Lib, Patient.fs ───────────────────────────────────────────────────────────────


module Patients =

    open Informedica.GenCore.Lib.Patients


    /// A birthdate, fully specified: a year, a month and a day. A year alone, or a year and
    /// a month, is no birthdate.
    type BirthDate =
        {
            Year: int<year>
            Month: int<month>
            Day: int<day>
        }


    /// The patient's age as the EHR gives it: a birthdate, an age value, or unknown.
    type PatientAge =
        | AgeValue of AgeValue
        | BirthDate of BirthDate
        | UnknownAge


    module BirthDate =

        let create y m d : BirthDate =
            {
                Year = y
                Month = m
                Day = d
            }


        let toDate (bd: BirthDate) = DateTime(int bd.Year, int bd.Month, int bd.Day)


        let fromDate (dt: DateTime) =
            create
                (dt.Year |> Conversions.yearFromInt)
                (dt.Month |> Conversions.monthFromInt)
                (dt.Day |> Conversions.dayFromInt)


        /// The record as it is today, every part present. Goes at migration.
        let data (bd: BirthDate) : Informedica.GenCore.Lib.Patients.BirthDate =
            BirthDate.create bd.Year (Some bd.Month) (Some bd.Day)


    module PatientAge =

        let birthDate bd = bd |> BirthDate


        let ageValue av = av |> AgeValue


        let unknown = UnknownAge


        /// The union as it is today. Goes at migration.
        let data =
            function
            | BirthDate bd -> bd |> BirthDate.data |> PatientAge.birthDate
            | AgeValue av -> av |> PatientAge.ageValue
            | UnknownAge -> PatientAge.unknown


    module Department =

        /// The name the rules match: the ward's own name; none for any ward, an unknown one
        /// or a ward without a name.
        let name =
            let named (s: string) = if s |> String.isNullOrWhiteSpace then None else Some s

            Department.map None None named named named named named


    /// The core patient with the identity on it: the id the EHR knows the patient by and the
    /// name, beside the data. The rest is the record as it is.
    type Patient =
        {
            /// The id the EHR knows the patient by; empty for a patient entered by hand.
            Id: string
            /// The patient's full name as the EHR gives it; empty when unknown.
            Name: string
            Department: Department
            Diagnoses: string[]
            Gender: Gender
            Age: PatientAge
            Weight: Weight
            Height: Height
            GestationalAge: AgeWeeksDays option
            EnteralAccess: EnteralAccess
            VenousAccess: VenousAccess
        }


    /// Who the patient is, when the EHR gave enough to say: the id, the name and a complete
    /// birthdate. Shown in full to the user, kept by the Session and its store, never printed
    /// by a patient printer or a log line.
    type PatientIdentity =
        {
            Id: string
            Name: string
            BirthDate: BirthDate
        }


    module Patient =

        let create id name dep diagn gend age wght hght gest ent ven : Patient =
            {
                Id = id
                Name = name
                Department = dep
                Diagnoses = diagn
                Gender = gend
                Age = age
                Weight = wght
                Height = hght
                GestationalAge = gest
                EnteralAccess = ent
                VenousAccess = ven
            }


        let unknown =
            create
                ""
                ""
                Department.unknown
                [||]
                UnknownGender
                PatientAge.unknown
                Weight.unknown
                Height.unknown
                None
                UnknownEnteral
                UnknownVenous


        /// The data without the identity, on the record as it is today, so that the functions
        /// over it stay in use unchanged. Goes at migration, when the two fields join the
        /// record.
        let data (pat: Patient) : Informedica.GenCore.Lib.Patients.Patient =
            Patient.create
                pat.Department
                pat.Diagnoses
                pat.Gender
                (pat.Age |> PatientAge.data)
                pat.Weight
                pat.Height
                pat.GestationalAge
                pat.EnteralAccess
                pat.VenousAccess


        /// The patient's identity: none without a name or without a birthdate.
        let identity (pat: Patient) : PatientIdentity option =
            match pat.Age with
            | BirthDate bd when pat.Name |> String.notEmpty ->
                Some
                    {
                        Id = pat.Id
                        Name = pat.Name
                        BirthDate = bd
                    }
            | _ -> None


        let getAgeValue dt = data >> Patient.SetGet.getAgeValue dt


        let calcPostMenstrualAge dt = data >> Patient.calcPostMenstrualAge dt


        /// The printer as it is: the department, gender, age, gestational and post-menstrual
        /// age, the calculation weight and height and the BSA. Neither the name nor the
        /// birthdate, which it prints as an age.
        let toString dt = data >> Patient.toString dt


/// The birthdate module by a name a qualified lookup cannot mistake: from outside the module,
/// Patients.BirthDate is the union case, not the module. Script only.
module CoreBirthDate = Patients.BirthDate


// ── GenFORM.Lib, Types.fs and Patient.fs ──────────────────────────────────────────────────


module GenForm =

    open Informedica.GenForm.Lib
    open Informedica.GenCore.Lib.Patients


    /// What the EHR returns for a patient id: the core patient, with the identity on it, and
    /// the data only the rules read. Access lives here alone; the core patient's own access
    /// fields are not filled from EHR data.
    type EhrPatientData =
        {
            /// The identity, the birthdate, the gender, the dated measurements, the department.
            Patient: Patients.Patient
            /// The renal function, as the renal rules read it.
            RenalFunction: RenalFunction option
            /// The access devices, venous and enteral, as the rules read them.
            Access: AccessDevice list
        }


    module EhrPatientData =

        let create pat renal access : EhrPatientData =
            {
                Patient = pat
                RenalFunction = renal
                Access = access
            }


        let identity (ehr: EhrPatientData) = ehr.Patient |> Patients.Patient.identity


    module Patient =

        open Informedica.GenForm.Lib.Patient


        let private days (d: int<day>) =
            d |> int |> BigRational.fromInt |> ValueUnit.singleWithUnit Units.Time.day


        let private gender =
            function
            | Male -> Types.Male
            | Female -> Types.Female
            | AnyGender
            | UnknownGender -> Types.AnyGender


        /// The GenFORM patient the EHR data is at a date: the age at that date in days, from
        /// the birthdate or the age value the EHR gave; weight and height from the calculation
        /// values, measured when there are any; the gestational age in days and the
        /// post-menstrual age calculated from it; the department's name; renal function and
        /// access as given; the BSA left to the calculation over weight and height.
        let ofEhr (now: DateTime) (ehr: EhrPatientData) : Types.Patient =
            let pat = ehr.Patient

            let weight =
                pat.Weight.Calculation
                |> Option.map (fun w ->
                    w.Weight
                    |> WeightValue.getWeightInKg
                    |> decimal
                    |> BigRational.fromDecimal
                    |> ValueUnit.singleWithUnit Units.Weight.kiloGram
                )

            let height =
                pat.Height.Calculation
                |> Option.map (fun h ->
                    h.Height
                    |> HeightValue.getHeightCm
                    |> decimal
                    |> BigRational.fromDecimal
                    |> ValueUnit.singleWithUnit Units.Height.centiMeter
                )

            { patient with
                Department = pat.Department |> Patients.Department.name
                Diagnoses = pat.Diagnoses
                Gender = pat.Gender |> gender
                Age =
                    pat
                    |> Patients.Patient.getAgeValue now
                    |> Option.map (AgeValue.getAgeInDays >> days)
                Weight = weight
                Height = height
                WeightMeasured = weight.IsSome
                HeightMeasured = height.IsSome
                GestAge = pat.GestationalAge |> Option.map (AgeWeeksDays.toDays >> days)
                Access = ehr.Access
                RenalFunction = ehr.RenalFunction
            }
            |> calcPMAge


// ── GenPRES.Shared, Types.fs ──────────────────────────────────────────────────────────────


module Contract =

    open Shared.Types


    /// Who the patient of a Session is, as the EHR identifies them: the full name and the
    /// birthdate as three integers, as the url already carries one. Shown in full: the user
    /// sits at the EHR that launched the Session.
    type PatientIdentity =
        {
            Name: string
            BirthYear: int
            BirthMonth: int
            BirthDay: int
        }


    /// The Patient a Session is for: its id, who it is when the EHR said, and its data as read
    /// at the launch, else as signed last; the data is none when neither has any, so the user
    /// enters it.
    type PatientContext =
        {
            PatientId: string
            Identity: PatientIdentity option
            Patient: Patient option
        }


    type SessionOpened =
        {
            User: UserContext option
            PatientContext: PatientContext option
            OpenedToken: OpenedToken option
            KeyThumbprint: string option
            Head: SignedOrderPlan option
        }


// ── GenPRES.Server, Ports.fs, StubAdapters.fs and Mappers.Session.fs ──────────────────────


/// The EHR, read once at the launch and again at a challenge: what it returns for a patient
/// id, as the domain's patient with the identity on it, or none. None is not a refusal: the
/// Session opens without imported data.
type PatientDataPort = { read: string -> GenForm.EhrPatientData option }


/// What the store holds of an open Session: who, for which patient, the EHR data as read and
/// the patient data shown, the token, the key thumbprint, and the head of the record it opened
/// with.
type OpenedSession =
    {
        User: UserContext option
        PatientId: string option
        /// the EHR data as read at the launch; none for no-data and for a launch without EHR
        EhrData: GenForm.EhrPatientData option
        /// the data shown for the patient: the EHR data projected, else the head's, else none
        Patient: Informedica.GenForm.Lib.Types.Patient option
        OpenedToken: OpenedToken option
        KeyThumbprint: string option
        Head: StoredVersion option
    }


/// The EHR stub: one fixed patient for every patient id, so a launch fills the patient panel
/// from the EHR, except that no-data has no record at all, so the Session opens on what was
/// signed last, or on nothing.
module StubPatientData =

    open Informedica.GenCore.Lib.Patients


    /// The stub patient's name. A stub, and shown as one.
    let name = "Stub Testpatiënt"


    /// The stub patient's birthdate: the fifteenth of March, 2016.
    let birthDate = CoreBirthDate.create 2016<year> 3<month> 15<day>


    /// The date the stub's measurements carry: the patient's tenth birthday, so that the
    /// projection at that date is today's stub reading, ten years, 32 kg, 140 cm.
    let measuredOn = DateTime(2026, 3, 15)


    /// The stub's EHR data for an id: the fixed patient under that id, nothing else known.
    let data (pid: string) : GenForm.EhrPatientData =
        let weight = WeightAtDate.create measuredOn (WeightValue.weightInKg 32m)
        let height = HeightAtDate.create measuredOn (HeightValue.heightInCm 140m)

        Patients.Patient.create
            pid
            name
            Department.unknown
            [||]
            UnknownGender
            (Patients.PatientAge.birthDate birthDate)
            (Weight.create [ weight ] None [] (Some weight))
            (Height.create [ height ] None (Some height))
            None
            UnknownEnteral
            UnknownVenous
        |> fun pat -> GenForm.EhrPatientData.create pat None []


    let port: PatientDataPort =
        {
            read = fun pid -> if pid = "no-data" then None else Some(data pid)
        }


/// The session mapper's half that changes: what the client keeps of an open Session, the
/// identity on the patient context when the EHR data has one.
module SessionMapper =

    open Informedica.GenCore.Lib.Patients


    /// The identity on the wire: the name and the birthdate as three integers.
    let identity (id: Patients.PatientIdentity) : Contract.PatientIdentity =
        {
            Name = id.Name
            BirthYear = int id.BirthDate.Year
            BirthMonth = int id.BirthDate.Month
            BirthDay = int id.BirthDate.Day
        }


    let toOpened (demo: bool) (opened: OpenedSession) : Contract.SessionOpened =
        {
            User = opened.User
            PatientContext =
                opened.PatientId
                |> Option.map (fun id ->
                    {
                        PatientId = id
                        Identity =
                            opened.EhrData
                            |> Option.bind GenForm.EhrPatientData.identity
                            |> Option.map identity
                        Patient =
                            opened.Patient
                            |> Option.map (Informedica.GenForm.Lib.Patient.Dto.toDto >> Patient.toModel)
                    }
                )
            OpenedToken = opened.OpenedToken
            KeyThumbprint = opened.KeyThumbprint
            Head =
                opened.Head
                |> Option.bind (fun head ->
                    match head with
                    | StoredVersion.Readable v ->
                        v |> OrderPlanVersion.Dto.toDto |> ServerApi.SessionMapper.toSigned demo |> Some
                    | StoredVersion.Unreadable _ -> None
                )
        }


// ── The proof ─────────────────────────────────────────────────────────────────────────────


open Informedica.GenCore.Lib.Patients


let tenthBirthday = StubPatientData.measuredOn


/// A Session opened on the stub's EHR data for an id, projected at a date.
let openedOn (now: DateTime) (pid: string) : OpenedSession =
    let ehr = StubPatientData.port.read pid

    {
        User = None
        PatientId = Some pid
        EhrData = ehr
        Patient = ehr |> Option.map (GenForm.Patient.ofEhr now)
        OpenedToken = None
        KeyThumbprint = None
        Head = None
    }


let ageInDays (pat: Informedica.GenForm.Lib.Types.Patient) =
    pat.Age
    |> Option.map (ValueUnit.convertTo Units.Time.day >> ValueUnit.getValue >> Array.head)


let tests =
    testList
        "EHR data"
        [
            testList
                "the projection"
                [
                    test "the stub projected at its tenth birthday is today's stub reading, field for field" {
                        let expected = ServerApi.StubPatientData.port.read "p1"

                        StubPatientData.port.read "p1"
                        |> Option.map (GenForm.Patient.ofEhr tenthBirthday)
                        |> Expect.equal "ten years, 32 kg measured, 140 cm measured, nothing else" expected
                    }

                    test "the age is the birthdate's at the date, in days" {
                        let at dt =
                            StubPatientData.data "p1" |> GenForm.Patient.ofEhr dt |> ageInDays

                        (at tenthBirthday, at (tenthBirthday.AddDays 1.0), at (tenthBirthday.AddYears 1))
                        |> Expect.equal "3650, 3651, 4015 days" (Some 3650N, Some 3651N, Some 4015N)
                    }

                    test "an age value the EHR gave is projected as its days" {
                        let ehr =
                            StubPatientData.data "p1"
                            |> fun ehr ->
                                { ehr with
                                    Patient =
                                        { ehr.Patient with
                                            Age =
                                                AgeValue.create (Some 2<year>) (Some 3<month>) None (Some 4<day>)
                                                |> Patients.PatientAge.ageValue
                                        }
                                }

                        ehr
                        |> GenForm.Patient.ofEhr tenthBirthday
                        |> ageInDays
                        |> Expect.equal "2 years, 3 months and 4 days" (Some 824N)
                    }

                    test "a measurement without a calculation value is no measure" {
                        let ehr = StubPatientData.data "p1"

                        let ehr =
                            { ehr with
                                Patient =
                                    { ehr.Patient with
                                        Weight = { ehr.Patient.Weight with Calculation = None }
                                    }
                            }

                        let pat = ehr |> GenForm.Patient.ofEhr tenthBirthday

                        (pat.Weight, pat.WeightMeasured, pat.Height.IsSome, pat.HeightMeasured)
                        |> Expect.equal "no weight, not measured; the height as before" (None, false, true, true)
                    }

                    test "gestational age in days, PMA calculated, department named, rule data as given" {
                        let ehr = StubPatientData.data "p1"

                        let ehr =
                            { ehr with
                                Patient =
                                    { ehr.Patient with
                                        Department = Department.pediatricICU "ICK"
                                        GestationalAge = Some(AgeWeeksDays.create 36<week> 2<day>)
                                    }
                                RenalFunction = Some(Informedica.GenForm.Lib.Types.RenalFunction.EGFR(Some 30, Some 60))
                                Access =
                                    [
                                        Informedica.GenForm.Lib.Types.CVL
                                        Informedica.GenForm.Lib.Types.EnteralTube
                                    ]
                            }

                        let pat = ehr |> GenForm.Patient.ofEhr tenthBirthday

                        let days (vu: ValueUnit option) =
                            vu
                            |> Option.map (ValueUnit.convertTo Units.Time.day >> ValueUnit.getValue >> Array.head)

                        (pat.GestAge |> days, pat.PMAge |> days, pat.Department, pat.RenalFunction, pat.Access)
                        |> Expect.equal
                            "254 days, 3904 days, ICK, the eGFR, the two devices"
                            (Some 254N,
                             Some 3904N,
                             Some "ICK",
                             Some(Informedica.GenForm.Lib.Types.RenalFunction.EGFR(Some 30, Some 60)),
                             [ Informedica.GenForm.Lib.Types.CVL; Informedica.GenForm.Lib.Types.EnteralTube ])
                    }

                    test "any or unknown ward, or a ward without a name, is no department" {
                        [
                            AnyDepartment
                            UnknownDepartment
                            Department.pediatricICU ""
                            Department.pediatricICU "  "
                        ]
                        |> List.map Patients.Department.name
                        |> Expect.allEqual "none" None
                    }
                ]

            testList
                "the identity"
                [
                    test "EHR data round-trips to the client context with the name and the birthdate" {
                        let opened = openedOn tenthBirthday "p1" |> SessionMapper.toOpened false

                        opened.PatientContext
                        |> Option.map (fun ctx -> ctx.PatientId, ctx.Identity, ctx.Patient.IsSome)
                        |> Expect.equal
                            "the id, the identity, the data"
                            (Some(
                                "p1",
                                Some
                                    {
                                        Contract.PatientIdentity.Name = StubPatientData.name
                                        BirthYear = 2016
                                        BirthMonth = 3
                                        BirthDay = 15
                                    },
                                true
                            ))
                    }

                    test "no-data has none: no EHR data, no identity, no patient data" {
                        let opened = openedOn tenthBirthday "no-data" |> SessionMapper.toOpened false

                        opened.PatientContext
                        |> Option.map (fun ctx -> ctx.PatientId, ctx.Identity, ctx.Patient)
                        |> Expect.equal "the id alone" (Some("no-data", None, None))
                    }

                    test "an age value in place of a birthdate opens anonymous, with the data" {
                        let ehr = StubPatientData.data "p1"

                        let ehr =
                            { ehr with
                                Patient =
                                    { ehr.Patient with
                                        Age = AgeValue.ten |> Patients.PatientAge.ageValue
                                    }
                            }

                        let opened: OpenedSession =
                            { openedOn tenthBirthday "p1" with
                                EhrData = Some ehr
                                Patient = Some(ehr |> GenForm.Patient.ofEhr tenthBirthday)
                            }

                        (opened |> SessionMapper.toOpened false).PatientContext
                        |> Option.map (fun ctx -> ctx.Identity, ctx.Patient.IsSome)
                        |> Expect.equal "no identity, the data" (Some(None, true))
                    }

                    test "a patient without a name has no identity" {
                        let ehr = StubPatientData.data "p1"

                        { ehr with
                            Patient = { ehr.Patient with Name = "" }
                        }
                        |> GenForm.EhrPatientData.identity
                        |> Expect.isNone "no name, no identity"
                    }

                    test "the identity is the id, the name and the birthdate" {
                        StubPatientData.data "p1"
                        |> GenForm.EhrPatientData.identity
                        |> Expect.equal
                            "the stub's"
                            (Some
                                {
                                    Patients.PatientIdentity.Id = "p1"
                                    Name = StubPatientData.name
                                    BirthDate = StubPatientData.birthDate
                                })
                    }
                ]

            testList
                "the birthdate"
                [
                    test "is a day, and round-trips through a date" {
                        StubPatientData.birthDate
                        |> CoreBirthDate.toDate
                        |> fun dt -> dt, dt |> CoreBirthDate.fromDate
                        |> Expect.equal
                            "the fifteenth of March, 2016"
                            (DateTime(2016, 3, 15), StubPatientData.birthDate)
                    }

                    test "is the record of today with every part present" {
                        StubPatientData.birthDate
                        |> CoreBirthDate.data
                        |> Expect.equal "as today" (BirthDate.create 2016<year> (Some 3<month>) (Some 15<day>))
                    }
                ]

            testList
                "the printers"
                [
                    test "the core printer writes neither the name nor the birthdate" {
                        let printed =
                            (StubPatientData.data "p1").Patient
                            |> Patients.Patient.toString tenthBirthday
                            |> String.concat ", "

                        [ StubPatientData.name; "2016"; "15-03"; "03-15"; "15-Mar-16" ]
                        |> List.map (fun s -> printed.Contains s)
                        |> Expect.allEqual $"nothing of the identity in: %s{printed}" false
                    }

                    test "the GenFORM printer writes neither the name nor the birthdate" {
                        let printed =
                            StubPatientData.data "p1"
                            |> GenForm.Patient.ofEhr tenthBirthday
                            |> Informedica.GenForm.Lib.Patient.toString

                        [ StubPatientData.name; "2016"; "15-03"; "03-15" ]
                        |> List.map (fun s -> printed.Contains s)
                        |> Expect.allEqual $"nothing of the identity in: %s{printed}" false
                    }
                ]
        ]


runTestsWithCLIArgs [] [||] tests
