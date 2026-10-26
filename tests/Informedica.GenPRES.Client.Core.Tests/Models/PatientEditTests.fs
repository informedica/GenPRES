/// The patient panel's edits: each field written, and what an edit keeps and blanks. The
/// fixtures copy those of Shared's ModelsTests, because the two test projects cannot share a file.
module Informedica.GenPRES.Client.Core.Tests.Models.PatientEditTests

open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Models
open Informedica.GenPRES.Client.Core.Models


/// A ten-year-old's age, as in the Shared patient tests.
let ten = { Patient.Age.ageZero with Age.Years = 10<year> }


/// The estimate tables, as in the Shared patient tests.
module EstimateFixtures =

    let row sex age p3 mean p97 : NormalValue =
        {
            Sex = sex
            Age = age
            P3 = p3
            Mean = mean
            P97 = p97
        }

    let weights = Some [ row "M" 10. 25. 32. 40. ]
    let heights = Some [ row "M" 10. 130. 140. 150. ]

    let estimated (dto: Patient) = dto |> Patient.applyNormalValues weights heights None None


open EstimateFixtures


/// The neonatal tables, as in the Shared patient tests.
module NeonatalEstimateFixtures =

    /// The neonatal tables, in weeks of post-conceptional age, reaching to 42 weeks as the
    /// sheet does.
    let neoWeights = Some [ row "M" 40. 2800. 3500. 4200.; row "M" 42. 3000. 3700. 4500. ]

    let neoHeights = Some [ row "M" 40. 48. 50. 53.; row "M" 42. 50. 52. 55. ]

    let estimated (dto: Patient) =
        dto |> Patient.applyNormalValues weights heights neoWeights neoHeights


    let term: GestAge =
        {
            Weeks = 40<week>
            Days = 0<day>
        }

    let premature: GestAge =
        {
            Weeks = 30<week>
            Days = 0<day>
        }


/// A patient with everything set, measured and estimated, so that every field has something
/// to lose.
let fullPatient: Patient =
    {
        Age =
            Some
                {
                    Years = 2<year>
                    Months = 3<month>
                    Weeks = 1<week>
                    Days = 4<day>
                }
        GestationalAge =
            Some
                {
                    Weeks = 32<week>
                    Days = 5<day>
                }
        Weight =
            {
                EstimatedP3 = Some 10000<gram>
                Estimated = Some 12000<gram>
                EstimatedP97 = Some 14000<gram>
                Measured = Some 12500<gram>
            }
        Height =
            {
                EstimatedP3 = Some 85<cm>
                Estimated = Some 90<cm>
                EstimatedP97 = Some 95<cm>
                Measured = Some 91<cm>
            }
        Gender = Female
        Access = [ CVL; EnteralTube ]
        RenalFunction = Some(EGFR(Some 30, Some 50))
        Location = Some "bed 4"
        Department = Some "ICK"
    }


/// The age setters by name, each applied to a value that fits its field.
let ageSetters =
    [
        "setYear", PatientEdit.setYear (Some "5")
        "setMonth", PatientEdit.setMonth (Some "7")
        "setWeek", PatientEdit.setWeek (Some "2")
        "setDay", PatientEdit.setDay (Some "3")
    ]


let patient (p: Patient option) = p |> Option.defaultWith (fun () -> failtest "no patient")


[<Tests>]
let ageSetterTests =
    testList
        "Patient age setters keep what was measured"
        [
            testList
                "an age edit keeps the measured weight and height"
                [
                    for name, set in ageSetters do
                        test $"{name}" {
                            let p = Some fullPatient |> set |> patient
                            p.Weight.Measured |> Expect.equal "the weight" fullPatient.Weight.Measured
                            p.Height.Measured |> Expect.equal "the height" fullPatient.Height.Measured
                        }
                ]

            testList
                "an age edit keeps the gestational age, gender, access, renal function, location and department"
                [
                    for name, set in ageSetters do
                        test $"{name}" {
                            let p = Some fullPatient |> set |> patient
                            p.GestationalAge
                            |> Expect.equal "the gestational age" fullPatient.GestationalAge
                            p.Gender |> Expect.equal "the gender" fullPatient.Gender
                            p.Access |> Expect.equal "the access" fullPatient.Access
                            p.RenalFunction |> Expect.equal "the renal function" fullPatient.RenalFunction
                            p.Location |> Expect.equal "the location" fullPatient.Location
                            p.Department |> Expect.equal "the department" fullPatient.Department
                        }
                ]

            testList
                "the estimates are blank after an age edit"
                [
                    for name, set in ageSetters do
                        test $"{name}" {
                            let p = Some fullPatient |> set |> patient
                            p.Weight.Estimated |> Expect.isNone "weight estimate"
                            p.Weight.EstimatedP3 |> Expect.isNone "weight p3"
                            p.Weight.EstimatedP97 |> Expect.isNone "weight p97"
                            p.Height.Estimated |> Expect.isNone "height estimate"
                            p.Height.EstimatedP3 |> Expect.isNone "height p3"
                            p.Height.EstimatedP97 |> Expect.isNone "height p97"
                        }
                ]

            testList
                "each setter writes its own part of the age"
                [
                    test "setYear" {
                        Some fullPatient
                        |> PatientEdit.setYear (Some "5")
                        |> patient
                        |> _.Age
                        |> Expect.equal "years" (Some { fullPatient.Age.Value with Years = 5<year> })
                    }

                    test "setMonth" {
                        Some fullPatient
                        |> PatientEdit.setMonth (Some "7")
                        |> patient
                        |> _.Age
                        |> Expect.equal "months" (Some { fullPatient.Age.Value with Months = 7<month> })
                    }

                    test "setWeek" {
                        Some fullPatient
                        |> PatientEdit.setWeek (Some "2")
                        |> patient
                        |> _.Age
                        |> Expect.equal "weeks" (Some { fullPatient.Age.Value with Weeks = 2<week> })
                    }

                    test "setDay" {
                        Some fullPatient
                        |> PatientEdit.setDay (Some "3")
                        |> patient
                        |> _.Age
                        |> Expect.equal "days" (Some { fullPatient.Age.Value with Days = 3<day> })
                    }
                ]

            testList
                "a draft's age"
                [
                    test "a blank draft given a year is a patient of that age alone" {
                        None
                        |> PatientEdit.setYear (Some "5")
                        |> patient
                        |> Expect.equal
                            "five years, nothing else"
                            { Patient.empty with Age = Some { Patient.Age.ageZero with Years = 5<year> } }
                    }

                    test "a blank draft with a year cleared is the blank draft, as it was" {
                        None |> PatientEdit.setYear None |> Expect.equal "blank" (Some Patient.empty)
                    }

                    test "a newborn on day zero is a patient with an age" {
                        None
                        |> PatientEdit.setDay (Some "0")
                        |> patient
                        |> _.Age
                        |> Expect.equal "age zero" (Some Patient.Age.ageZero)
                    }

                    test "clearing one age part keeps the age with that part zero" {
                        Some fullPatient
                        |> PatientEdit.setMonth None
                        |> patient
                        |> _.Age
                        |> Expect.equal "months zero" (Some { fullPatient.Age.Value with Months = 0<month> })
                    }

                    test "a value that is not a number reads as zero" {
                        Some fullPatient
                        |> PatientEdit.setYear (Some "five")
                        |> patient
                        |> _.Age
                        |> Expect.equal "years zero" (Some { fullPatient.Age.Value with Years = 0<year> })
                    }
                ]
        ]


/// The gestational-age, weight and height setters by name, each applied to a value that fits
/// its field.
let measureSetters =
    [
        "setGAWeek", PatientEdit.setGAWeek (Some "36")
        "setGADay", PatientEdit.setGADay (Some "2")
        "setWeight", PatientEdit.setWeight (Some "13000")
        "setHeight", PatientEdit.setHeight (Some "95")
    ]


/// The full patient with the measures estimated only: what the panel holds after an age and a
/// gender were typed and the tables answered.
let estimatedPatient: Patient =
    { fullPatient with
        Weight = { fullPatient.Weight with Measured = None }
        Height = { fullPatient.Height with Measured = None }
    }


[<Tests>]
let measureSetterTests =
    testList
        "Patient gestational-age, weight and height setters keep what was measured"
        [
            testList
                "a gestational-age edit keeps the measured weight and height"
                [
                    for name, set in measureSetters |> List.take 2 do
                        test $"{name}" {
                            let p = Some fullPatient |> set |> patient
                            p.Weight.Measured |> Expect.equal "the weight" fullPatient.Weight.Measured
                            p.Height.Measured |> Expect.equal "the height" fullPatient.Height.Measured
                        }
                ]

            testList
                "a weight or height edit keeps the age and the gestational age"
                [
                    for name, set in measureSetters |> List.skip 2 do
                        test $"{name}" {
                            let p = Some fullPatient |> set |> patient
                            p.Age |> Expect.equal "the age" fullPatient.Age
                            p.GestationalAge
                            |> Expect.equal "the gestational age" fullPatient.GestationalAge
                        }
                ]

            testList
                "every setter keeps the gender, access, renal function, location and department"
                [
                    for name, set in measureSetters do
                        test $"{name}" {
                            let p = Some fullPatient |> set |> patient
                            p.Gender |> Expect.equal "the gender" fullPatient.Gender
                            p.Access |> Expect.equal "the access" fullPatient.Access
                            p.RenalFunction |> Expect.equal "the renal function" fullPatient.RenalFunction
                            p.Location |> Expect.equal "the location" fullPatient.Location
                            p.Department |> Expect.equal "the department" fullPatient.Department
                        }
                ]

            testList
                "the estimates are blank after every setter"
                [
                    for name, set in measureSetters do
                        test $"{name}" {
                            let p = Some fullPatient |> set |> patient
                            p.Weight.Estimated |> Expect.isNone "weight estimate"
                            p.Weight.EstimatedP3 |> Expect.isNone "weight p3"
                            p.Weight.EstimatedP97 |> Expect.isNone "weight p97"
                            p.Height.Estimated |> Expect.isNone "height estimate"
                            p.Height.EstimatedP3 |> Expect.isNone "height p3"
                            p.Height.EstimatedP97 |> Expect.isNone "height p97"
                        }
                ]

            testList
                "no setter writes an estimate as measured"
                [
                    test "a weight typed for an estimated height leaves the height unmeasured" {
                        Some estimatedPatient
                        |> PatientEdit.setWeight (Some "13000")
                        |> patient
                        |> _.Height.Measured
                        |> Expect.isNone "not measured"
                    }

                    test "a height typed for an estimated weight leaves the weight unmeasured" {
                        Some estimatedPatient
                        |> PatientEdit.setHeight (Some "95")
                        |> patient
                        |> _.Weight.Measured
                        |> Expect.isNone "not measured"
                    }
                ]

            testList
                "each setter writes its own field"
                [
                    test "setGAWeek" {
                        Some fullPatient
                        |> PatientEdit.setGAWeek (Some "36")
                        |> patient
                        |> _.GestationalAge
                        |> Expect.equal "weeks" (Some { fullPatient.GestationalAge.Value with Weeks = 36<week> })
                    }

                    test "setGADay" {
                        Some fullPatient
                        |> PatientEdit.setGADay (Some "2")
                        |> patient
                        |> _.GestationalAge
                        |> Expect.equal "days" (Some { fullPatient.GestationalAge.Value with Days = 2<day> })
                    }

                    test "setWeight" {
                        Some fullPatient
                        |> PatientEdit.setWeight (Some "13000")
                        |> patient
                        |> _.Weight.Measured
                        |> Expect.equal "grams" (Some 13000<gram>)
                    }

                    test "setHeight" {
                        Some fullPatient
                        |> PatientEdit.setHeight (Some "95")
                        |> patient
                        |> _.Height.Measured
                        |> Expect.equal "centimetres" (Some 95<cm>)
                    }
                ]

            testList
                "a draft's gestational age and measures"
                [
                    test "a gestational age started from its days alone is a term one" {
                        None
                        |> PatientEdit.setGADay (Some "2")
                        |> patient
                        |> _.GestationalAge
                        |> Expect.equal
                            "37 weeks 2 days"
                            (Some
                                {
                                    Weeks = 37<week>
                                    Days = 2<day>
                                })
                    }

                    test "clearing the gestational weeks reads as term" {
                        Some fullPatient
                        |> PatientEdit.setGAWeek None
                        |> patient
                        |> _.GestationalAge
                        |> Expect.equal
                            "term"
                            (Some
                                {
                                    Weeks = 37<week>
                                    Days = 5<day>
                                })
                    }

                    test "a blank draft with the gestational days cleared is the blank draft" {
                        None |> PatientEdit.setGADay None |> Expect.equal "blank" (Some Patient.empty)
                    }

                    test "a blank draft given a weight is a patient of that weight alone" {
                        None
                        |> PatientEdit.setWeight (Some "13000")
                        |> patient
                        |> Expect.equal
                            "13000 grams, nothing else"
                            { Patient.empty with Weight = { Patient.empty.Weight with Measured = Some 13000<gram> } }
                    }

                    test "a measure cleared is a measure gone, the other kept" {
                        let p = Some fullPatient |> PatientEdit.setWeight None |> patient
                        p.Weight.Measured |> Expect.isNone "weight gone"
                        p.Height.Measured |> Expect.equal "height kept" fullPatient.Height.Measured
                    }

                    test "a value that is not a number clears the field" {
                        Some fullPatient
                        |> PatientEdit.setHeight (Some "tall")
                        |> patient
                        |> _.Height.Measured
                        |> Expect.isNone "not a number"
                    }
                ]
        ]


[<Tests>]
let estimateTests =
    testList
        "an edit and the estimate"
        [
            test "a gender chosen keeps the measured values and drops the estimates" {
                let dto =
                    { Patient.empty with
                        Age = Some ten
                        Gender = Male
                        Patient.Weight.Measured = Some 30000<gram>
                    }
                    |> estimated

                Some dto
                |> PatientEdit.setGender "female"
                |> Option.map (fun p -> p.Gender, p.Weight.Measured, p.Weight.Estimated, p.Height.Estimated)
                |> Expect.equal "female, measured kept, estimates gone" (Some(Female, Some 30000<gram>, None, None))
            }

            test "a gender chosen first is a draft with the gender and nothing else" {
                None
                |> PatientEdit.setGender "male"
                |> Expect.equal "the gender alone" (Some { Patient.empty with Gender = Male })
            }

            test "an age typed after the gestational age keeps it and is estimated by the age" {
                Some
                    { Patient.empty with
                        Age = Some { Patient.Age.ageZero with Weeks = 1<week> }
                        GestationalAge = Some NeonatalEstimateFixtures.premature
                        Gender = Male
                    }
                |> PatientEdit.setYear (Some "2")
                |> patient
                |> NeonatalEstimateFixtures.estimated
                |> fun dto -> dto.GestationalAge, dto.Weight.Estimated
                |> Expect.equal
                    "kept, and the age tables answer"
                    (Some NeonatalEstimateFixtures.premature, Some 32000<gram>)
            }
        ]
