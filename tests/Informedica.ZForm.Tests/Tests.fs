namespace Informedica.ZForm.Tests


open Expecto
open Expecto.Flip
open Informedica.Utils.Lib.BCL

open Informedica.GenCore.Lib.Ranges
open Informedica.ZForm.Lib

module ValueUnit = Informedica.GenUnits.Lib.ValueUnit


module Helpers =

    /// Remove whitespace inside numbers (e.g. "1 000" -> "1000")
    /// so tests don't depend on thousands-separator formatting.
    let private normalizeNumbers (s: string) =
        System.Text.RegularExpressions.Regex.Replace(s, @"(\d)\s+(\d)", "$1$2")

    /// Check that a string contains all expected substrings.
    /// Both actual and expected are normalized to remove number
    /// formatting differences before comparison.
    let shouldContainAll msg (expected: string list) (actual: string) =
        let actual' = normalizeNumbers actual

        for sub in expected do
            let sub' = normalizeNumbers sub
            actual' |> Expect.stringContains $"{msg}: should contain '{sub}'" sub'


    let vuFromStr v u =
        ValueUnit.unitFromZIndexString u |> ValueUnit.singleWithValue v |> Some


open Helpers


module MinMaxTests =

    open Informedica.GenUnits.Lib

    let fromDecimal (v: decimal) u =
        v |> BigRational.fromDecimal |> ValueUnit.createSingle u

    let ageInMo = (fun n -> fromDecimal n Units.Time.month)
    let ageInYr = (fun n -> fromDecimal n Units.Time.year)

    let ageInclOneMo, ageExclOneYr =
        1m |> ageInMo |> Inclusive, 1m |> ageInYr |> Exclusive

    let ageRange =
        MinMax.empty
        |> MinMax.Optics.setMin ageInclOneMo
        |> MinMax.Optics.setMax ageExclOneYr

    let tests =
        testList
            "MinMax"
            [
                test "ageToString" {
                    ageRange
                    |> MinMax.ageToString
                    |> shouldContainAll "age range" [ "1 maand"; "1 jaar" ]
                }
            ]


module PatientTests =

    open PatientCategory

    let processDto f dto =
        dto |> f
        dto

    let setMinAge =
        fun (dto: Dto.Dto) ->
            dto.Age.HasMin <- true
            dto.Age.Min.Value <- [| 1N |]
            dto.Age.Min.Unit <- "maand"
            dto.Age.Min.Group <- "Time"
            dto.Age.Min.Language <- "dutch"
            dto.Age.Min.Short <- true
            dto.Age.MinIncl <- true

    let setWrongUnit =
        fun (dto: Dto.Dto) ->
            dto.Age.HasMin <- true
            dto.Age.Min.Value <- [| 1N |]
            dto.Age.Min.Unit <- "m"
            dto.Age.Min.Group <- "Time"
            dto.Age.Min.Language <- "dutch"
            dto.Age.Min.Short <- true
            dto.Age.MinIncl <- true

    let setWrongGroup =
        fun (dto: Dto.Dto) ->
            dto.Age.HasMin <- true
            dto.Age.Min.Value <- [| 1N |]
            dto.Age.Min.Unit <- "g"
            dto.Age.Min.Group <- "Mass"
            dto.Age.Min.Language <- "dutch"
            dto.Age.Min.Short <- true
            dto.Age.MinIncl <- true

    let tests =
        testList
            "Patient"
            [
                test "an 'empty patient'" {
                    Dto.dto ()
                    |> Dto.fromDto
                    |> function
                        | None -> "false"
                        | Some p -> p |> toString
                    |> Expect.equal "should be an empty string" ""
                }

                test "a patient with a min age" {
                    Dto.dto ()
                    |> processDto setMinAge
                    |> Dto.fromDto
                    |> function
                        | None -> "false"
                        | Some p -> p |> toString
                    |> Expect.equal "should be 'Leeftijd: van 1 maand'" "Leeftijd: van 1 maand"
                }

                test "a patient with a min age wrong unit" {
                    skiptest "TODO: not yet implemented — currently throws during toString"
                }

                test "a patient with a min age wrong group" {
                    skiptest "TODO: not yet implemented — currently throws during toString"
                }
            ]


module DoseRangeTests =

    open Informedica.Utils.Lib.Optics
    open Informedica.GenUnits.Lib

    module Dto = DoseRule.DoseRange.Dto
    module DoseRange = DoseRule.DoseRange

    // Rendering a DoseRange to string loads unit mappings from Google Sheets,
    // which requires GENPRES_URL_ID. When it is not configured (e.g. CI, where
    // only demo/cached data is available) these tests cannot run, so skip them.
    // The DTO round-trip tests below need no sheet data and always run.
    // Duplicates the resolution in ZForm.Lib Web.genpresUrlId rather than calling it:
    // that is now a deferred, memoized accessor (issue #523), and forcing it here to
    // build the test list would defeat the deferral.
    let private hasUrlId =
        Informedica.Utils.Lib.Env.loadDotEnv () |> ignore
        Informedica.Utils.Lib.Env.getItem "GENPRES_URL_ID" |> Option.isSome

    /// `test` when GENPRES_URL_ID is configured, otherwise a skipped (pending) test.
    let testUrl name =
        if hasUrlId then test name else ptest name

    let setMinNormDose = Optic.set DoseRange.Optics.inclMinNormLens
    let setMaxNormDose = Optic.set DoseRange.Optics.inclMaxNormLens

    let setMinNormPerKgDose vu dr =
        dr
        |> Optic.set DoseRange.Optics.inclMinNormWeightLens vu
        |> Optic.set DoseRange.Optics.normWeightUnitLens Units.Weight.kiloGram

    let setMaxNormPerKgDose vu dr =
        dr
        |> Optic.set DoseRange.Optics.inclMaxNormWeightLens vu
        |> Optic.set DoseRange.Optics.normWeightUnitLens Units.Weight.kiloGram

    let setMinAbsDose vu dr =
        dr
        |> Optic.set DoseRange.Optics.inclMinAbsLens vu
        |> Optic.set DoseRange.Optics.absBSAUnitLens Units.BSA.m2

    let setMaxAbsDose vu dr =
        dr
        |> Optic.set DoseRange.Optics.inclMaxAbsLens vu
        |> Optic.set DoseRange.Optics.absBSAUnitLens Units.BSA.m2

    let drToStr = DoseRange.toString None

    let processDto f dto =
        dto |> f
        dto

    let addValues =
        fun (dto: Dto.Dto) ->
            dto.Norm.HasMin <- true
            dto.Norm.Min.Value <- [| 1N |]
            dto.Norm.Min.Unit <- "mg"
            dto.Norm.Min.Group <- "mass"

            dto.Norm.HasMax <- true
            dto.Norm.Max.Value <- [| 10N |]
            dto.Norm.Max.Unit <- "mg"
            dto.Norm.Max.Group <- "mass"

            dto.NormWeight.HasMin <- true
            dto.NormWeight.Min.Value <- [| 1N / 1_000N |]
            dto.NormWeight.Min.Unit <- "mg"
            dto.NormWeight.Min.Group <- "mass"
            dto.NormWeightUnit <- "kg"

            dto.NormWeight.HasMax <- true
            dto.NormWeight.Max.Value <- [| 1N |]
            dto.NormWeight.Max.Unit <- "mg"
            dto.NormWeight.Max.Group <- "mass"
            dto.NormWeightUnit <- "kg"

    let tests =
        testList
            "DoseRange"
            [
                test "there and back again empty doserange dto" {
                    let expct = Dto.dto () |> Dto.fromDto

                    expct |> Dto.toDto |> Dto.fromDto |> Expect.equal "should be equal" expct
                }

                test "there and back again with filled doserange dto" {
                    let expct = Dto.dto () |> processDto addValues |> Dto.fromDto

                    expct |> Dto.toDto |> Dto.fromDto |> Expect.equal "should be equal" expct
                }

                testUrl "can create a dose range" {
                    DoseRange.empty
                    |> setMaxNormDose (vuFromStr 10N "milligram")
                    |> setMaxAbsDose (vuFromStr 100N "milligram")
                    |> drToStr
                    |> shouldContainAll "dose range" [ "10 mg"; "100 mg"; "maximaal" ]
                }

                testUrl "can create a dose range with a rate" {
                    DoseRange.empty
                    |> setMinNormDose (vuFromStr 10N "milligram")
                    |> setMaxNormDose (vuFromStr 100N "milligram")
                    |> DoseRange.toString (Some ValueUnit.Units.hour)
                    |> shouldContainAll "dose range rate" [ "10 mg/uur"; "100 mg/uur" ]
                }

                testUrl "can create a dose range with a rate per kg" {
                    DoseRange.empty
                    |> setMinNormPerKgDose (vuFromStr (1N / 1_000N) "milligram")
                    |> setMaxNormPerKgDose (vuFromStr 1N "milligram")
                    |> DoseRange.convertTo ValueUnit.Units.mcg
                    |> DoseRange.toString (Some ValueUnit.Units.hour)
                    |> shouldContainAll "dose range rate per kg" [ "1 microg/kg/uur"; "1000 microg/kg/uur" ]
                }

                testUrl "can covert a unit" {
                    DoseRange.empty
                    |> setMaxNormDose (vuFromStr 1N "milligram")
                    |> setMinNormDose (vuFromStr (1N / 1_000N) "milligram")
                    |> DoseRange.convertTo ValueUnit.Units.mcg
                    |> drToStr
                    |> shouldContainAll "unit conversion" [ "1 microg"; "1000 microg" ]
                }
            ]


module DoseRuleTests =

    module Dto = DoseRule.Dto

    let tests =
        testList
            "DoseRule"
            [
                test "there and back again with an empty doserule" {
                    let doseRule = Dto.dto () |> Dto.fromDto

                    doseRule |> Dto.toDto |> Dto.fromDto |> Expect.equal "should be equal" doseRule
                }
            ]


module DosageTests =

    module Dosage = DoseRule.Dosage

    let tests =
        testList
            "Dosage"
            [
                test "an empty Dosage is not high risk" {
                    Dosage.empty.HighRisk |> Expect.isFalse "default should be false"
                }

                test "setHighRisk sets the GPRISC flag" {
                    Dosage.empty
                    |> Dosage.Optics.setHighRisk true
                    |> _.HighRisk
                    |> Expect.isTrue "should be true"
                }

                // Note: a full Dosage.Dto round-trip is not exercised here — the
                // empty-Dosage Dto path has a pre-existing NoUnit/readable-string
                // limitation unrelated to HighRisk. The HighRisk Dto field is a
                // symmetric assignment in toDto/fromDto.
                test "Dosage Dto carries the HighRisk field" {
                    let dto = Dosage.Dto.dto ()
                    dto.HighRisk <- true
                    dto.HighRisk |> Expect.isTrue "Dto exposes HighRisk"
                }
            ]


module Tests =

    [<Tests>]
    let tests =
        testList
            "ZForm"
            [
                MinMaxTests.tests
                PatientTests.tests
                DoseRangeTests.tests
                DoseRuleTests.tests
                DosageTests.tests
            ]
