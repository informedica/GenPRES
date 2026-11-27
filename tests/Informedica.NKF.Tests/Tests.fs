namespace Informedica.NKF.Tests


module Tests =

    open Expecto
    open Expecto.Flip

    open Informedica.KinderFormularium.Lib
    open Informedica.KinderFormularium.Lib.Drug


    module RegexTests =

        let tests =
            testList
                "Utils.Regex"
                [
                    test "matchFloat extracts numeric portion from alphanumeric string" {
                        "12.5mg" |> Regex.matchFloat |> Expect.equal "should return '12.5'" "12.5"
                    }

                    test "matchAlpha extracts alphabetic portion from alpha string" {
                        "mg" |> Regex.matchAlpha |> Expect.equal "should return 'mg'" "mg"
                    }

                    test "matchFloatAlpha splits value and unit from combined string" {
                        "3.0mcg"
                        |> Regex.matchFloatAlpha
                        |> Expect.equal "should return ('3.0', 'mcg')" ("3.0", "mcg")
                    }
                ]


    module FrequencyTests =

        open Frequency

        let validQty =
            {
                Min = 1
                Max = 3
                Time = 24
                Unit = "uur"
            }

        let tests =
            testList
                "Drug.Frequency"
                [
                    test "isValid returns true for Frequency with positive Max, Time and non-empty Unit" {
                        Frequency validQty |> isValid |> Expect.isTrue "valid Frequency should be valid"
                    }

                    test "isValid returns false when Time is zero" {
                        Frequency { validQty with Time = 0 }
                        |> isValid
                        |> Expect.isFalse "zero Time should be invalid"
                    }

                    test "isValid returns false when Unit is empty" {
                        Frequency { validQty with Unit = "" }
                        |> isValid
                        |> Expect.isFalse "empty Unit should be invalid"
                    }

                    test "isValid returns true for AnteNoctum (no Quantity)" {
                        AnteNoctum |> isValid |> Expect.isTrue "AnteNoctum should always be valid"
                    }

                    test "toDoseType maps Frequency to 'onderhoud'" {
                        Frequency validQty
                        |> toDoseType
                        |> Expect.equal "Frequency → onderhoud" "onderhoud"
                    }

                    test "toDoseType maps PRN to 'prn'" { PRN validQty |> toDoseType |> Expect.equal "PRN → prn" "prn" }

                    test "toDoseType maps Once to 'eenmalig'" {
                        Once |> toDoseType |> Expect.equal "Once → eenmalig" "eenmalig"
                    }

                    test "getFrequency returns semicolon-delimited range for Frequency 1..3" {
                        Frequency validQty
                        |> getFrequency
                        |> Expect.equal "should return '1;2;3'" "1;2;3"
                    }

                    test "getFrequency returns empty string for AnteNoctum" {
                        AnteNoctum
                        |> getFrequency
                        |> Expect.equal "non-range frequencies return empty string" ""
                    }
                ]


    module TargetTests =

        open Target

        let unknownTarget = Unknown("x", "y")
        let allTarget = Target(AllType, AllAge, AllWeight)
        let boyTarget = Target(Boy, AllAge, AllWeight)
        let girlTarget = Target(Girl, AllAge, AllWeight)

        let ageTarget =
            let minAge =
                Some
                    {
                        Quantity = 1.0
                        Unit = "month"
                    }

            let maxAge =
                Some
                    {
                        Quantity = 12.0
                        Unit = "month"
                    }

            Target(AllType, Age(minAge, maxAge), AllWeight)

        let tests =
            testList
                "Drug.Target"
                [
                    test "getTarget returns None for Unknown" {
                        unknownTarget |> getTarget |> Expect.isNone "Unknown → None"
                    }

                    test "getTarget returns Some for Target" {
                        allTarget |> getTarget |> Expect.isSome "Target (...) → Some"
                    }

                    test "genderToString returns 'man' for Boy" {
                        boyTarget |> genderToString |> Expect.equal "Boy → 'man'" "man"
                    }

                    test "genderToString returns 'vrouw' for Girl" {
                        girlTarget |> genderToString |> Expect.equal "Girl → 'vrouw'" "vrouw"
                    }

                    test "genderToString returns empty string for AllType" {
                        allTarget |> genderToString |> Expect.equal "AllType → ''" ""
                    }

                    test "getAge returns (None, None) for AllAge variant" {
                        allTarget |> getAge |> Expect.equal "AllAge → (None, None)" (None, None)
                    }

                    test "getAge extracts min and max from Age variant" {
                        let minOpt, maxOpt = ageTarget |> getAge
                        minOpt |> Expect.isSome "min age should be Some"
                        maxOpt |> Expect.isSome "max age should be Some"
                    }
                ]


    module CreateDrugTests =

        let tests =
            testList
                "Drug.createDrug"
                [
                    test "createDrug populates Id, Atc, Generic and Brand; Doses is empty" {
                        let drug = createDrug "id1" "N02BE01" "paracetamol" "Panadol"
                        drug.Id |> Expect.equal "Id" "id1"
                        drug.Atc |> Expect.equal "Atc" "N02BE01"
                        drug.Generic |> Expect.equal "Generic" "paracetamol"
                        drug.Brand |> Expect.equal "Brand" "Panadol"
                        drug.Doses |> Expect.isEmpty "Doses should be empty"
                    }
                ]


    module CleanGenericNameTests =

        let tests =
            testList
                "Export.cleanGenericName"
                [
                    test "cleanGenericName lowercases the generic name" {
                        let drug = createDrug "" "" "Paracetamol" ""

                        (Export.cleanGenericName drug).Generic
                        |> Expect.equal "should be lower-case" "paracetamol"
                    }

                    test "cleanGenericName leaves plain ASCII names unchanged" {
                        let drug = createDrug "" "" "colistimethaatnatrium" ""
                        let cleaned = (Export.cleanGenericName drug).Generic
                        cleaned |> Expect.equal "should be lowercased" "colistimethaatnatrium"
                    }
                    test "cleanGenericName strips ' (combinatiepreparaat)' suffix" {
                        let drug = createDrug "" "" "co-trimoxazol (combinatiepreparaat)" ""

                        (Export.cleanGenericName drug).Generic
                        |> Expect.equal "suffix removed" "co-trimoxazol"
                    }
                ]


    module NormDoseTests =

        open Informedica.Utils.Lib.BCL

        let tests =
            testList
                "Export norm dose"
                [
                    test "the header has no norm column and keeps the adjusted minimum and maximum" {
                        let header = [] |> Export.toDataString |> List.head |> String.split "\t"

                        header
                        |> List.filter (String.startsWith "Norm")
                        |> Expect.isEmpty "no norm column"

                        [ "MinQtyAdj"; "MaxQtyAdj"; "MinPerTimeAdj"; "MaxPerTimeAdj" ]
                        |> List.forall (fun c -> header |> List.contains c)
                        |> Expect.isTrue "the adjusted minimum and maximum columns"
                    }

                    for min, max, expected in [ "15", "15", true; "10", "20", false; "", "", false; "", "15", false ] do
                        test $"an adjusted dose from '%s{min}' to '%s{max}' is a norm dose: %b{expected}" {
                            Export.isNormDose min max |> Expect.equal "norm dose" expected
                        }
                ]


    [<Tests>]
    let tests =
        testList
            "NKF"
            [
                RegexTests.tests
                FrequencyTests.tests
                TargetTests.tests
                CreateDrugTests.tests
                CleanGenericNameTests.tests
                NormDoseTests.tests
            ]
