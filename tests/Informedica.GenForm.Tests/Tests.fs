namespace Informedica.GenForm.Tests



module Tests =


    open MathNet.Numerics
    open Expecto
    open Expecto.Flip

    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenUnits.Lib
    open Informedica.GenForm.Lib


    module PatientCategoryTests =


        let tests = testList "PatientCategory" [
            let filter = Filter.filter
            let patCat =
                {
                    Department = None
                    Diagnoses = [||]
                    Gender = AnyGender
                    Age = MinMax.empty
                    Weight = MinMax.empty
                    BSA = MinMax.empty
                    GestAge = MinMax.empty
                    PMAge = MinMax.empty
                    Location = AnyAccess
                }

            test "an empty filter and empty patient category" {
                patCat
                |> PatientCategory.filter filter
                |> Expect.isTrue "should return true"
            }

            test "an empty filter and patient category with female gender" {
                { patCat with Gender = Female }
                |> PatientCategory.filter filter
                |> Expect.isFalse "should return false"
            }

            test "a filter with female gender and patient category with female gender" {
                { patCat with Gender = Female }
                |> PatientCategory.filter
                       { filter with
                            Patient =
                                { filter.Patient with
                                    Gender = Female
                                }
                       }
                |> Expect.isTrue "should return true"
            }

            test "a filter with female gender and patient category with no gender" {
                { patCat with Gender = AnyGender }
                |> PatientCategory.filter
                       { filter with
                            Patient =
                                { filter.Patient with
                                    Gender = Female
                                }
                       }
                |> Expect.isTrue "should return true"
            }

            test "an empty filter and a patient category with a max age of 7" {
                { patCat with
                    Age =
                        { patCat.Age with
                            Max  =
                                7N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                        }
                }
                |> PatientCategory.filter filter
                |> Expect.isFalse "should return false"
            }

            test "a filter with age 5 and a patient category with a max age of 7" {
                { patCat with
                    Age =
                        { patCat.Age with
                            Max  =
                                7N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                        }
                }
                |> PatientCategory.filter
                    { filter with
                        Patient =
                            { filter.Patient with
                                Age =
                                    5N
                                    |> ValueUnit.singleWithUnit Units.Time.day
                                    |> Some
                            }
                    }
                |> Expect.isTrue "should return true"
            }

            test "a filter with age 5 and a patient category with a min age of 1 week" {
                { patCat with
                    Age =
                        { patCat.Age with
                            Min  =
                                1N
                                |> ValueUnit.singleWithUnit Units.Time.week
                                |> Limit.Inclusive
                                |> Some
                        }
                }
                |> PatientCategory.filter
                    { filter with
                        Patient =
                            { filter.Patient with
                                Age =
                                    5N
                                    |> ValueUnit.singleWithUnit Units.Time.day
                                    |> Some
                            }
                    }
                |> Expect.isFalse "should return false"
            }

            test "a filter with age 5 and a patient category with a min age of 3 and max age of 7" {
                { patCat with
                    Age =
                        {
                            Min =
                                5N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                            Max =
                                7N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                        }
                }
                |> PatientCategory.filter
                    { filter with
                        Patient =
                            { filter.Patient with
                                Age =
                                    5N
                                    |> ValueUnit.singleWithUnit Units.Time.day
                                    |> Some
                            }
                    }
                |> Expect.isTrue "should return true"
            }

            test "a filter with age 5 with a patient category with a min age of 3 and max age of 7 and gender female" {
                { patCat with
                    Gender = Female
                    Age =
                        {
                            Min =
                                5N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                            Max =
                                7N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                        }
                }
                |> PatientCategory.filter
                    { filter with
                        Patient =
                            { filter.Patient with
                                Age =
                                    5N
                                    |> ValueUnit.singleWithUnit Units.Time.day
                                    |> Some
                            }
                    }
                |> Expect.isFalse "should return false"
            }

            test "a filter with age 5, gender female with a patient category with a min age of 3 and max age of 7 and gender female" {
                { patCat with
                    Gender = Female
                    Age =
                        {
                            Min =
                                5N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                            Max =
                                7N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                        }
                }
                |> PatientCategory.filter
                    { filter with
                        Patient =
                            { filter.Patient with
                                Gender = Female
                                Age =
                                    5N
                                    |> ValueUnit.singleWithUnit Units.Time.day
                                    |> Some
                            }
                    }
                |> Expect.isTrue "should return true"
            }

            test "a filter with age 0 and gestational age 30 weeks with an empty patient category" {
                patCat
                |> PatientCategory.filter
                    { filter with
                        Patient =
                            { filter.Patient with
                                Age =
                                    0N
                                    |> ValueUnit.singleWithUnit Units.Time.day
                                    |> Some
                                GestAge =
                                    30N
                                    |> ValueUnit.singleWithUnit Units.Time.week
                                    |> Some
                            }
                    }
                |> Expect.isFalse "should return false"
            }

            test "a filter with age 0 and gestational age 30 weeks with a patient category with min age = 0" {
                { patCat with
                    Age =
                        {
                            Min =
                                0N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                            Max = None
                        }
                }
                |> PatientCategory.filter
                    { filter with
                        Patient =
                            { filter.Patient with
                                Age =
                                    0N
                                    |> ValueUnit.singleWithUnit Units.Time.day
                                    |> Some
                                GestAge =
                                    30N
                                    |> ValueUnit.singleWithUnit Units.Time.week
                                    |> Some
                            }
                    }
                |> Expect.isFalse "should return false"
            }

            test "a filter with age 0 and gestational age 28 weeks, weight = 1.15 kg, height = 46 cm, with a patient category with min age = 0" {
                { patCat with
                    Age =
                        {
                            Min =
                                0N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                            Max = None
                        }
                }
                |> PatientCategory.filter
                    { filter with
                        Patient =
                            { filter.Patient with
                                Age =
                                    0N
                                    |> ValueUnit.singleWithUnit Units.Time.day
                                    |> Some
                                GestAge =
                                    28N
                                    |> ValueUnit.singleWithUnit Units.Time.week
                                    |> Some
                                Weight =
                                    (115N / 100N)
                                    |> ValueUnit.singleWithUnit Units.Weight.kiloGram
                                    |> Some
                                Height =
                                    45N
                                    |> ValueUnit.singleWithUnit Units.Height.centiMeter
                                    |> Some
                            }
                    }
                |> Expect.isFalse "should return false"
            }

            test "a filter with age 0 and gestational age 30 weeks with a patient category with a min age of 0 and max age of 28 and gestational age min 28 and max 32 weeks" {
                { patCat with
                    Age =
                        {
                            Min =
                                0N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                            Max =
                                28N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                        }
                    GestAge =
                        {
                            Min =
                                28N
                                |> ValueUnit.singleWithUnit Units.Time.week
                                |> Limit.Inclusive
                                |> Some
                            Max =
                                32N
                                |> ValueUnit.singleWithUnit Units.Time.week
                                |> Limit.Inclusive
                                |> Some
                        }
                }
                |> PatientCategory.filter
                    { filter with
                        Patient =
                            { filter.Patient with
                                Age =
                                    0N
                                    |> ValueUnit.singleWithUnit Units.Time.day
                                    |> Some
                                GestAge =
                                    30N
                                    |> ValueUnit.singleWithUnit Units.Time.week
                                    |> Some
                            }
                    }
                |> Expect.isTrue "should return true"
            }

            test "a filter with age 0 and gestational age 37 weeks with a patient category with a min age of 0 and max age of 28 and gestational age min 28 and max 32 weeks" {
                { patCat with
                    Age =
                        {
                            Min =
                                0N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                            Max =
                                28N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                        }
                    GestAge =
                        {
                            Min =
                                28N
                                |> ValueUnit.singleWithUnit Units.Time.week
                                |> Limit.Inclusive
                                |> Some
                            Max =
                                32N
                                |> ValueUnit.singleWithUnit Units.Time.week
                                |> Limit.Inclusive
                                |> Some
                        }
                }
                |> PatientCategory.filter
                    { filter with
                        Patient =
                            { filter.Patient with
                                Age =
                                    0N
                                    |> ValueUnit.singleWithUnit Units.Time.day
                                    |> Some
                                GestAge =
                                    33N
                                    |> ValueUnit.singleWithUnit Units.Time.week
                                    |> Some
                            }
                    }
                |> Expect.isFalse "should return false"
            }

            test "a filter with age 8 and gestational age 27 weeks with a patient category with a min age of 0 and max age of 28 and gestational age min 28 and max 32 weeks" {
                { patCat with
                    Age =
                        {
                            Min =
                                0N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                            Max =
                                28N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                        }
                    GestAge =
                        {
                            Min =
                                28N
                                |> ValueUnit.singleWithUnit Units.Time.week
                                |> Limit.Inclusive
                                |> Some
                            Max =
                                32N
                                |> ValueUnit.singleWithUnit Units.Time.week
                                |> Limit.Inclusive
                                |> Some
                        }
                }
                |> PatientCategory.filter
                    { filter with
                        Patient =
                            { filter.Patient with
                                Age =
                                    8N
                                    |> ValueUnit.singleWithUnit Units.Time.day
                                    |> Some
                                GestAge =
                                    27N
                                    |> ValueUnit.singleWithUnit Units.Time.week
                                    |> Some
                            }
                            |> Patient.calcPMAge
                    }
                |> Expect.isFalse "should return false"
            }

            test "a filter with age 0 and gestational age 30 weeks with a patient category with a min age of 0 and max age of 28 and pm age min 28 and max 32 weeks" {
                { patCat with
                    Age =
                        {
                            Min =
                                0N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                            Max =
                                28N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                        }
                    PMAge =
                        {
                            Min =
                                28N
                                |> ValueUnit.singleWithUnit Units.Time.week
                                |> Limit.Inclusive
                                |> Some
                            Max =
                                32N
                                |> ValueUnit.singleWithUnit Units.Time.week
                                |> Limit.Inclusive
                                |> Some
                        }
                }
                |> PatientCategory.filter
                    { filter with
                        Patient =
                            { filter.Patient with
                                Age =
                                    0N
                                    |> ValueUnit.singleWithUnit Units.Time.day
                                    |> Some
                                GestAge =
                                    30N
                                    |> ValueUnit.singleWithUnit Units.Time.week
                                    |> Some
                            }
                            |> Patient.calcPMAge
                    }

                |> Expect.isTrue "should return true"
            }

            test "a filter with age 0 and gestational age 37 weeks with a patient category with a min age of 0 and max age of 28 and pm age min 28 and max 32 weeks" {
                { patCat with
                    Age =
                        {
                            Min =
                                0N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                            Max =
                                28N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                        }
                    PMAge =
                        {
                            Min =
                                28N
                                |> ValueUnit.singleWithUnit Units.Time.week
                                |> Limit.Inclusive
                                |> Some
                            Max =
                                32N
                                |> ValueUnit.singleWithUnit Units.Time.week
                                |> Limit.Inclusive
                                |> Some
                        }
                }
                |> PatientCategory.filter
                    { filter with
                        Patient =
                            { filter.Patient with
                                Age =
                                    0N
                                    |> ValueUnit.singleWithUnit Units.Time.day
                                    |> Some
                                GestAge =
                                    37N
                                    |> ValueUnit.singleWithUnit Units.Time.week
                                    |> Some
                            }
                            |> Patient.calcPMAge
                    }

                |> Expect.isFalse "should return false"
            }

            test "a filter with age 8 and gestational age 27 weeks with a patient category with a min age of 0 and max age of 28 and pm age min 28 and max 32 weeks" {
                { patCat with
                    Age =
                        {
                            Min =
                                0N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                            Max =
                                28N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                        }
                    PMAge =
                        {
                            Min =
                                28N
                                |> ValueUnit.singleWithUnit Units.Time.week
                                |> Limit.Inclusive
                                |> Some
                            Max =
                                32N
                                |> ValueUnit.singleWithUnit Units.Time.week
                                |> Limit.Inclusive
                                |> Some
                        }
                }
                |> PatientCategory.filter
                    { filter with
                        Patient =
                            { filter.Patient with
                                Age =
                                    8N
                                    |> ValueUnit.singleWithUnit Units.Time.day
                                    |> Some
                                GestAge =
                                    27N
                                    |> ValueUnit.singleWithUnit Units.Time.week
                                    |> Some
                            }
                            |> Patient.calcPMAge
                    }
                |> Expect.isTrue "should return true"
            }

            test "a filter with age 0, ga = 32 and weight 1.45 with a patient category with max age = 30 and max gest 37 and max weight 1.5" {
                { patCat with
                    Age =
                        { patCat.Age with
                            Max =
                                30N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                        }
                    Weight =
                        { patCat.Weight with
                            Max =
                                1.5m
                                |> BigRational.FromDecimal
                                |> ValueUnit.singleWithUnit Units.Weight.kiloGram
                                |> Limit.Inclusive
                                |> Some
                        }
                    GestAge =
                        { patCat.GestAge with
                            Max =
                                37N
                                |> ValueUnit.singleWithUnit Units.Time.week
                                |> Limit.Inclusive
                                |> Some
                        }
                }
                |> PatientCategory.filter
                    { filter with
                        Patient =
                            { filter.Patient with
                                Age =
                                    0N
                                    |> ValueUnit.singleWithUnit Units.Time.day
                                    |> Some
                                Weight =
                                    1.45m
                                    |> BigRational.FromDecimal
                                    |> ValueUnit.singleWithUnit Units.Weight.kiloGram
                                    |> Some
                                GestAge =
                                    32N
                                    |> ValueUnit.singleWithUnit Units.Time.week
                                    |> Some
                            }
                    }

                |> Expect.isTrue "should return true"
            }

            test "a filter with age 0, ga = 32 and weight 1.45 with a patient category with min age = 30 and max age = 720" {
                { patCat with
                    Age =
                        {
                            Min =
                                30N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                            Max =
                                720N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                        }
                }
                |> PatientCategory.filter
                    { filter with
                        Patient =
                            { filter.Patient with
                                Age =
                                    0N
                                    |> ValueUnit.singleWithUnit Units.Time.day
                                    |> Some
                                Weight =
                                    (145N/10N)
                                    |> ValueUnit.singleWithUnit Units.Weight.kiloGram
                                    |> Some
                                GestAge =
                                    32N
                                    |> ValueUnit.singleWithUnit Units.Time.week
                                    |> Some
                            }
                    }

                |> Expect.isFalse "should return false"
            }

            test "a filter with age 0, ga = None and weight 3500 gram with a patient category with pm age = 36 and max age = 37" {
                { patCat with
                    Age =
                        {
                            Min =
                                0N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                            Max =
                                28N
                                |> ValueUnit.singleWithUnit Units.Time.day
                                |> Limit.Inclusive
                                |> Some
                        }
                    PMAge =
                        {
                            Min =
                                36N
                                |> ValueUnit.singleWithUnit Units.Time.week
                                |> Limit.Inclusive
                                |> Some
                            Max =
                                37N
                                |> ValueUnit.singleWithUnit Units.Time.week
                                |> Limit.Exclusive
                                |> Some
                        }
                }
                |> PatientCategory.filter
                    { filter with
                        Patient =
                            { filter.Patient with
                                Age =
                                    0N
                                    |> ValueUnit.singleWithUnit Units.Time.day
                                    |> Some
                                Weight =
                                    3500N
                                    |> ValueUnit.singleWithUnit Units.Weight.gram
                                    |> Some
                            }
                    }

                |> Expect.isFalse "should return false"
            }

        ]


    [<Tests>]
    let tests = testList "GenForm Tests" [
        PatientCategoryTests.tests
    ]


