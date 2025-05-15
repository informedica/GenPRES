namespace Informedica.GenForm.Tests




/// Create the necessary test generators
module Generators =


    open Expecto
    open FsCheck
    open MathNet.Numerics

    open Informedica.Utils.Lib.BCL


    let bigRGen (n, d) =
        let d = if d = 0 then 1 else d
        let n = abs(n) |> BigRational.FromInt
        let d = abs(d) |> BigRational.FromInt
        n / d


    let bigRGenOpt (n, _) = bigRGen (n, 1) |> Some


    let bigRGenerator =
        gen {
            let! n = Arb.generate<int>
            let! d = Arb.generate<int>
            return bigRGen(n, d)
        }


    type BigRGenerator () =
        static member BigRational () =
            { new Arbitrary<BigRational>() with
                override x.Generator = bigRGenerator
            }



    type MinMax = MinMax of BigRational * BigRational

    let minMaxArb () =
        bigRGenerator
        |> Gen.map abs
        |> Gen.two
        |> Gen.map (fun (br1, br2) ->
            let br1 = br1.Numerator |> BigRational.FromBigInt
            let br2 = br2.Numerator |> BigRational.FromBigInt
            if br1 >= br2 then br2, br1 else br1, br2
            |> fun (br1, br2) ->
                if br1 = br2 then br1, br2 + 1N else br1, br2
        )
        |> Arb.fromGen
        |> Arb.convert MinMax (fun (MinMax (min, max)) -> min, max)


    type ListOf37<'a> = ListOf37 of 'a List
    let listOf37Arb () =
        Gen.listOfLength 37 Arb.generate
        |> Arb.fromGen
        |> Arb.convert ListOf37 (fun (ListOf37 xs) -> xs)


    let config = {
        FsCheckConfig.defaultConfig with
            arbitrary = [
                typeof<BigRGenerator>
                typeof<ListOf37<_>>.DeclaringType
                typeof<MinMax>.DeclaringType
            ] @ FsCheckConfig.defaultConfig.arbitrary
            maxTest = 1000
        }


    let testProp testName prop =
        prop |> testPropertyWithConfig config testName





module Tests =


    open MathNet.Numerics
    open Expecto
    open Expecto.Flip

    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenUnits.Lib
    open Informedica.GenForm.Lib


    module PatientCategoryTests =


        let tests = testList "PatientCategory" [
            let filter = Filter.doseFilter
            let patCat =
                {
                    Department = None
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
                            DoseFilter.Patient.Gender = Female
                       }
                |> Expect.isTrue "should return true"
            }

            test "a filter with female gender and patient category with no gender" {
                { patCat with Gender = AnyGender }
                |> PatientCategory.filter
                       { filter with
                            DoseFilter.Patient.Gender = Female
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
                |> Expect.isTrue "should return true"
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

            test "an empty patient category is a match with another empty patient category" {
                PatientCategory.empty
                |> PatientCategory.isMatch PatientCategory.empty
                |> Expect.isTrue "should return true"
            }

            testList "patient category tests" [

                    fun minAge maxAge ->
                        let minAge = if minAge < 0N then None else Some minAge
                        let maxAge = if maxAge < 0N then None else Some maxAge
                        let minAge, maxAge =
                            if minAge > maxAge then
                                maxAge, minAge
                            else
                                minAge, maxAge

                        let patCatToMatch =
                            { PatientCategory.empty with
                                Age =
                                    {
                                        Min =
                                            minAge
                                            |> Option.map (ValueUnit.singleWithUnit Units.Time.day >> Limit.Inclusive)
                                        Max =
                                            maxAge
                                            |> Option.map (ValueUnit.singleWithUnit Units.Time.day >> Limit.Inclusive)
                                    }
                            }

                        let emptyPatCat = PatientCategory.empty
                        emptyPatCat |> PatientCategory.isMatch patCatToMatch
                    |> Generators.testProp "a patient cat with age should always match an empty patient category"

                    fun minWeight maxWeight ->
                        let minWeight = if minWeight < 0N then None else Some minWeight
                        let maxWeight = if maxWeight < 0N then None else Some maxWeight
                        let minWeight, maxWeight =
                            if minWeight > maxWeight then
                                maxWeight, minWeight
                            else
                                minWeight, maxWeight

                        let patCatToMatch =
                            { PatientCategory.empty with
                                Weight =
                                    {
                                        Min =
                                            minWeight
                                            |> Option.map (ValueUnit.singleWithUnit Units.Weight.kiloGram >> Limit.Inclusive)
                                        Max =
                                            maxWeight
                                            |> Option.map (ValueUnit.singleWithUnit Units.Weight.kiloGram >> Limit.Inclusive)
                                    }
                            }

                        let emptyPatCat = PatientCategory.empty
                        emptyPatCat |> PatientCategory.isMatch patCatToMatch
                    |> Generators.testProp "a patient cat with weight should always match an empty patient category"

                    fun gender ->
                        let patCatToMatch =
                            { PatientCategory.empty with
                                Gender = gender
                            }
                        let emptyPatCat = PatientCategory.empty
                        emptyPatCat |> PatientCategory.isMatch patCatToMatch
                    |> Generators.testProp "a patient cat with a gender should always match an empty patient category"

                    fun location ->
                        let patCatToMatch =
                            { PatientCategory.empty with
                                Location = location
                            }
                        let emptyPatCat = PatientCategory.empty
                        emptyPatCat |> PatientCategory.isMatch patCatToMatch
                    |> Generators.testProp "a patient cat with a location should always match an empty patient category"

                    fun minAge maxAge ->
                        let minAge = if minAge < 0N then Some 0N else Some minAge
                        let maxAge = if maxAge < 0N then Some 0N else Some maxAge
                        let minAge, maxAge =
                            if minAge > maxAge then
                                maxAge, minAge
                            else
                                minAge, maxAge

                        let notEmptyPatCat =
                            { PatientCategory.empty with
                                Age =
                                    {
                                        Min =
                                            minAge
                                            |> Option.map (ValueUnit.singleWithUnit Units.Time.day >> Limit.Inclusive)
                                        Max =
                                            maxAge
                                            |> Option.map (ValueUnit.singleWithUnit Units.Time.day >> Limit.Inclusive)
                                    }
                            }

                        let patCatToWatch = PatientCategory.empty
                        notEmptyPatCat |> PatientCategory.isMatch patCatToWatch
                        |> not
                    |> Generators.testProp "an empty pat cat should never match an patient category with age"

                ]
        ]


    [<Tests>]
    let tests = testList "GenForm Tests" [
        PatientCategoryTests.tests
    ]