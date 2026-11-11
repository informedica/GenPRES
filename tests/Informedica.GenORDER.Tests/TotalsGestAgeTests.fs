/// The Totals rows a patient gets by gestational age: the gestational age the rows are chosen by,
/// and the rows that apply to a patient with an age, a weight and a gestational age.
module TotalsGestAgeTests

open MathNet.Numerics
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip


/// Totals rows of the neonatal fluid groups on day 4, and the patients they are tested on.
module Fixture =

    let br = BigRational.fromInt >> Some


    let row name minW maxW minA maxA minGA maxGA norm : Types.Data.TotalsData =
        {
            Name = name
            MinAge = minA
            MaxAge = maxA
            MinWeight = minW
            MaxWeight = maxW
            MinGestAge = minGA
            MaxGestAge = maxGA
            Unit = Some Units.Volume.milliLiter
            Adj = Some Units.Weight.kiloGram
            TimeUnit = Some Units.Time.day
            MinPerTime = None
            MaxPerTime = None
            MinPerTimeAdj = norm |> fst |> br
            MaxPerTimeAdj = norm |> snd |> br
        }


    let daysVu n =
        n |> BigRational.fromInt |> ValueUnit.singleWithUnit Units.Time.day


    let days = daysVu >> Some


    let grams n =
        n |> BigRational.fromInt |> ValueUnit.singleWithUnit Units.Weight.gram |> Some


    let kg (n: BigRational) = n |> ValueUnit.singleWithUnit Units.Weight.kiloGram |> Some


    let preterm = row "volume" None (br 1000) (br 4) (br 5) None (br 259) (140, 160)
    let pretermHeavy = row "volume" (br 1500) None (br 4) (br 5) None (br 259) (120, 140)
    let term = row "volume" None None (br 4) (br 5) (br 259) None (60, 100)
    let fromTwoKilos = row "volume" (br 2000) None None None None None (120, 160)
    let everyPatient = row "natrium" None None None None None None (2, 4)


open Fixture


[<Tests>]
let tests =
    testList
        "Totals by gestational age"
        [
            testList
                "gestAgeOrTerm"
                [
                    test "the gestational age given" {
                        Totals.gestAgeOrTerm (days 3) (days 200) |> Expect.equal "as given" (days 200)
                    }

                    test "under 28 days and not given: none" {
                        Totals.gestAgeOrTerm (days 27) None |> Expect.isNone "needed"
                    }

                    test "from 28 days and not given: term" {
                        Totals.gestAgeOrTerm (days 28) None |> Expect.equal "37 weeks" (days 259)
                    }

                    test "from 1 year and not given: term" {
                        Totals.gestAgeOrTerm (days 400) None |> Expect.equal "37 weeks" (days 259)
                    }

                    test "without an age and not given: term" {
                        Totals.gestAgeOrTerm None None |> Expect.equal "37 weeks" (days 259)
                    }
                ]

            testList
                "appliesTo"
                [
                    test "a preterm row applies below the maximum gestational age" {
                        preterm
                        |> Totals.appliesTo (days 4) (grams 900) (daysVu 200)
                        |> Expect.isTrue "applies"
                    }

                    test "the maximum gestational age is excluded" {
                        preterm
                        |> Totals.appliesTo (days 4) (grams 900) (daysVu 259)
                        |> Expect.isFalse "does not apply"
                    }

                    test "the minimum gestational age is included" {
                        term
                        |> Totals.appliesTo (days 4) (kg 3N) (daysVu 259)
                        |> Expect.isTrue "applies"
                    }

                    test "a term row does not apply to a preterm" {
                        term
                        |> Totals.appliesTo (days 4) (grams 1800) (daysVu 230)
                        |> Expect.isFalse "does not apply"
                    }

                    test "a row without gestational age bounds does not apply to a preterm" {
                        fromTwoKilos
                        |> Totals.appliesTo (days 3) (grams 2100) (daysVu 230)
                        |> Expect.isFalse "does not apply"
                    }

                    test "a row without gestational age bounds applies to a term patient" {
                        fromTwoKilos
                        |> Totals.appliesTo (days 3) (grams 2100) (daysVu 270)
                        |> Expect.isTrue "applies"
                    }

                    test "a row without patient bounds applies to a preterm" {
                        everyPatient
                        |> Totals.appliesTo (days 3) (grams 900) (daysVu 200)
                        |> Expect.isTrue "applies"
                    }

                    test "the weight in kg is compared in grams" {
                        fromTwoKilos
                        |> Totals.appliesTo None (kg (19N / 10N)) (daysVu 280)
                        |> Expect.isFalse "below 2000 g"
                    }

                    test "the age still limits" {
                        preterm
                        |> Totals.appliesTo (days 5) (grams 900) (daysVu 200)
                        |> Expect.isFalse "day 5 is outside day 4"
                    }

                    test "an unknown age and weight do not limit" {
                        [ term; fromTwoKilos; everyPatient ]
                        |> List.forall (Totals.appliesTo None None (daysVu 259))
                        |> Expect.isTrue "all apply"
                    }
                ]

            testList
                "getTotals"
                [
                    test "under 28 days without a gestational age: no totals" {
                        Totals.getTotals [| term; everyPatient |] (days 4) (kg 3N) None [||]
                        |> Expect.equal "empty" Totals.empty
                    }
                ]

            testList
                "the rows a neonate gets"
                [
                    let all = [| preterm; pretermHeavy; term; fromTwoKilos |]
                    let rows age wght ga = all |> Array.filter (Totals.appliesTo age wght ga)

                    test "a term neonate of 1800 g gets the term row only" {
                        rows (days 4) (grams 1800) (daysVu 270) |> Expect.equal "term" [| term |]
                    }

                    test "a preterm of 1800 g gets the heavy preterm row only" {
                        rows (days 4) (grams 1800) (daysVu 230)
                        |> Expect.equal "preterm > 1500 g" [| pretermHeavy |]
                    }

                    test "a term neonate of 3 kg gets the term row and the row from 2000 g" {
                        rows (days 4) (kg 3N) (daysVu 280)
                        |> Expect.equal "term and from 2000 g" [| term; fromTwoKilos |]
                    }
                ]
        ]
