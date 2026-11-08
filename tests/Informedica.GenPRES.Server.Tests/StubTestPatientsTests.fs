/// The test patients of the stub EHR: six NEO patients of 2 days old and one ICK patient per
/// GenCORE age category, male and female in turn, each read relative to the moment of the read, and the stub launch
/// page that offers them.
module Informedica.GenPRES.Server.Tests.StubTestPatientsTests

open System
open Expecto
open Expecto.Flip
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open ServerApi

module CoreAgeType = Informedica.GenCore.Lib.Patients.AgeType
module CorePatient = Informedica.GenCore.Lib.Patients.Patient
module CoreAgeValue = Informedica.GenCore.Lib.Patients.AgeValue


let now = DateTime(2026, 11, 3, 14, 30, 0)


/// The GenFORM patient of a PatientId, read and projected at a moment.
let projectedAt at pid =
    StubPatientData.read (fun () -> at) pid
    |> Option.map Informedica.GenForm.Lib.Patient.ofEhr


let value (vu: ValueUnit option) = vu |> Option.map (ValueUnit.getValue >> Array.head)


let neo =
    [
        "neo-26wk", "Male", 26N, 600N, 35N
        "neo-28wk", "Female", 28N, 800N, 36N
        "neo-31wk", "Male", 31N, 1100N, 40N
        "neo-34wk", "Female", 34N, 1800N, 42N
        "neo-37wk", "Male", 37N, 2400N, 50N
        "neo-40wk", "Female", 40N, 4000N, 55N
    ]


// the age in days as the projection counts it, with a year of 365 days and a month of 30; the
// weight and height the GenCORE normal values of the gender
let ick =
    [
        "ick-newborn", "Male", Informedica.GenCore.Lib.Patients.NewBorn, 0N, 19N / 5N, 51N
        "ick-infant", "Female", Informedica.GenCore.Lib.Patients.Infant, 30N, 4N, 54N
        "ick-toddler", "Male", Informedica.GenCore.Lib.Patients.Toddler, 365N, 10N, 76N
        "ick-child", "Female", Informedica.GenCore.Lib.Patients.Child, 1095N, 29N / 2N, 97N
        "ick-adolescent", "Male", Informedica.GenCore.Lib.Patients.Adolescent, 3650N, 34N, 144N
        "ick-adult", "Female", Informedica.GenCore.Lib.Patients.Adult, 6570N, 58N, 170N
    ]


/// The age in days of a test patient read on a day, as GenPRES counts it.
let coreAgeInDays (day: DateTime) pid =
    StubPatientData.read (fun () -> day) pid
    |> Option.bind (fun ehr -> (ehr.Patient |> CorePatient.atDate day).Data.Age)
    |> Option.map (CoreAgeValue.getAgeInDays >> int)
    |> Option.get


/// The ICK patients and their age in days.
let ickTargets =
    [
        "ick-newborn", 0
        "ick-infant", 30
        "ick-toddler", 365
        "ick-child", 1095
        "ick-adolescent", 3650
        "ick-adult", 6570
    ]


/// Every day from 2026 to 2028, at nine in the morning.
let threeYears = [ for d in 0 .. 365 * 3 -> DateTime(2026, 1, 1, 9, 0, 0).AddDays(float d) ]


[<Tests>]
let tests =
    testList
        "StubTestPatients"
        [
            test "there are six NEO and six ICK test patients with distinct ids" {
                StubPatientData.testPatients
                |> List.map _.Id
                |> List.distinct
                |> List.length
                |> Expect.equal "twelve distinct ids" 12
            }

            testList
                "NEO"
                [
                    for pid, gender, weeks, gram, cm in neo do
                        test
                            $"%s{pid} is %s{gender}, 2 days old, GA %A{weeks} weeks, %A{gram} gram, %A{cm} cm, on the NEO" {
                            let pat = projectedAt now pid |> Option.get

                            $"%A{pat.Gender}" |> Expect.equal "gender" gender
                            pat.Department |> Expect.equal "department" (Some "NEO")
                            pat.Age |> value |> Expect.equal "age in days" (Some 2N)
                            pat.GestAge
                            |> value
                            |> Expect.equal "gestational age in days" (Some(weeks * 7N))
                            pat.Weight |> value |> Expect.equal "weight in kg" (Some(gram / 1000N))
                            pat.Height |> value |> Expect.equal "height in cm" (Some cm)
                        }
                ]

            testList
                "ICK"
                [
                    for pid, gender, ageType, days, kg, cm in ick do
                        test $"%s{pid} is %s{gender}, %A{days} days old, %A{kg} kg, %A{cm} cm, on the ICK" {
                            let pat = projectedAt now pid |> Option.get

                            $"%A{pat.Gender}" |> Expect.equal "gender" gender
                            StubPatientData.read (fun () -> now) pid
                            |> Option.bind (fun ehr ->
                                (ehr.Patient |> CorePatient.atDate now).Data.Age
                                |> Option.map CoreAgeValue.toAgeType
                            )
                            |> Expect.equal "the age category" (Some ageType)

                            pat.Department |> Expect.equal "department" (Some "ICK")
                            pat.Age |> value |> Expect.equal "age in days" (Some days)
                            pat.GestAge |> Expect.isNone "no gestational age"
                            pat.Weight |> value |> Expect.equal "weight in kg" (Some kg)
                            pat.Height |> value |> Expect.equal "height in cm" (Some cm)
                        }
                ]

            test "a NEO patient is 2 days old on any day the data is read" {
                projectedAt (DateTime(2030, 1, 1)) "neo-26wk"
                |> Option.bind _.Age
                |> Option.map (ValueUnit.getValue >> Array.head)
                |> Expect.equal "2 days" (Some 2N)
            }

            test "another PatientId gives the fixed patient" {
                StubPatientData.read (fun () -> now) "p1"
                |> Expect.equal "the fixed patient, read now" (Some(StubPatientData.data now "p1"))
            }

            test "no-data gives no data" { StubPatientData.read (fun () -> now) "no-data" |> Expect.isNone "no data" }

            test "the launch page has a radio for every test patient and other, the ICK adolescent checked" {
                let page = StubLaunch.page
                let pid = StubLaunch.checkedPatientId

                [
                    StubLaunch.otherPatient
                    yield! StubPatientData.testPatients |> List.map _.Id |> List.filter ((<>) pid)
                ]
                |> List.filter (fun id -> page.Contains $"""name="patient" id="p-%s{id}" value="%s{id}">""" |> not)
                |> Expect.isEmpty "an unchecked radio for every other patient"

                page.Contains $"""name="patient" id="p-%s{pid}" value="%s{pid}" checked>"""
                |> Expect.isTrue "the ICK adolescent is checked"

                page.Split """name="patient" """
                |> Array.tail
                |> Array.filter (fun tag -> tag.Substring(0, tag.IndexOf '>').EndsWith " checked")
                |> Array.length
                |> Expect.equal "one checked patient" 1
            }

            test "the launch page has no row for the fixed patient" {
                StubLaunch.page.Contains $"""id="p-%s{StubLaunch.defaultPatientId}" """
                |> Expect.isFalse "no fixed patient"
            }

            test "the launch page says in plain English what it does" {
                StubLaunch.page.Contains "Opens GenPRES for the chosen patient and user"
                |> Expect.isTrue "plain"
                StubLaunch.page.Contains "LaunchScript" |> Expect.isFalse "no jargon"
            }

            test "the hint of prescriber-other-patient names another patient" {
                StubLaunch.identityHint "prescriber-other-patient"
                |> Expect.equal "hint" "a Prescriber whose active patient is another patient"
            }

            test "the launch page has a radio and a hint for every identity, the first checked" {
                let page = StubLaunch.pageFor [ "prescriber"; "reader" ]

                page.Contains """name="identity" value="prescriber" checked>"""
                |> Expect.isTrue "prescriber checked"

                page.Contains """name="identity" value="reader">"""
                |> Expect.isTrue "reader unchecked"
                page.Contains "<dt>reader</dt><dd>a Reader, who never signs</dd>"
                |> Expect.isTrue "the hint"
            }

            test "the launch page has no script, so the CSP holds" {
                StubLaunch.page.Contains "<script" |> Expect.isFalse "no script"
            }

            test "the launch page opens GenPRES in a new tab without a link back to it" {
                StubLaunch.page.Contains """target="_blank" rel="noopener">"""
                |> Expect.isTrue "new tab"
            }

            test "the mail outbox opens in a new tab without a link back to the launch page" {
                StubLaunch.page.Contains
                    $"""<a href="%s{StubMail.path}" target="_blank" rel="noopener">Mail outbox</a>"""
                |> Expect.isTrue "new tab"
            }

            test "the launch page is titled GenPRES Test Launch" {
                [ "<title>GenPRES Test Launch</title>"; "<h1>GenPRES Test Launch " ]
                |> List.filter (StubLaunch.page.Contains >> not)
                |> Expect.isEmpty "the tab title and the heading"
            }

            test "the launch page has one Launch button" {
                StubLaunch.page.Split "<button" |> Array.length |> Expect.equal "one button" 2
            }

            test "the launch page says that a newer launch ends the session of an older tab" {
                StubLaunch.page.Contains "a newer launch ends the session of an older tab"
                |> Expect.isTrue "the note"
            }

            test "the columns of a test patient are its gender, age, GA, weight and height" {
                StubPatientData.testPatients
                |> List.head
                |> StubLaunch.columns
                |> Expect.equal "NEO 26 weeks" [ "male"; "2 days"; "26 weeks"; "600 g"; "35 cm" ]

                StubLaunch.page.Contains "<th>Gender</th>" |> Expect.isTrue "the gender column"
            }

            test "the chosen patient is the PatientId, other and nothing take the typed one" {
                [
                    StubLaunch.patientIdOf "neo-26wk" "typed"
                    StubLaunch.patientIdOf StubLaunch.otherPatient "typed"
                    StubLaunch.patientIdOf "" "typed"
                ]
                |> Expect.equal "patient, typed, typed" [ "neo-26wk"; "typed"; "typed" ]
            }

            test "an empty Other launches the fixed patient, a typed one without spaces" {
                [
                    StubLaunch.patientIdOf StubLaunch.otherPatient " p1 "
                    StubLaunch.patientIdOf StubLaunch.otherPatient ""
                    StubLaunch.patientIdOf "" "  "
                ]
                |> Expect.equal "trimmed, fixed, fixed" [ "p1"; "123456"; "123456" ]
            }

            testList
                "the age of an ICK patient on any day"
                [
                    test "an ICK patient has the exact age on every day of three years" {
                        [
                            for pid, target in ickTargets do
                                for day in threeYears do
                                    let age = coreAgeInDays day pid

                                    if age <> target then
                                        pid, day.ToShortDateString(), age
                        ]
                        |> Expect.isEmpty "exact"
                    }
                ]
        ]
