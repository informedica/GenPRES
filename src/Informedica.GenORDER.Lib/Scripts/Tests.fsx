#I __SOURCE_DIRECTORY__

#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: FParsec"

#r "nuget: Expecto"
#r "nuget: Expecto.FsCheck"
#r "nuget: Unquote"

#r "../../Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.GenUNITS.Lib/bin/Debug/net10.0/Informedica.GenUNITS.Lib.dll"
#r "../../Informedica.GenSOLVER.Lib/bin/Debug/net10.0/Informedica.GenSOLVER.Lib.dll"

#load "load.fsx"

open System
open System.IO

open Informedica.Utils.Lib

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__


/// Create the necessary test generators
module Generators =

    open Expecto
    open FsCheck
    open MathNet.Numerics


    let bigRGen (n, d) =
        let d = if d = 0 then 1 else d
        let n = abs (n) |> BigRational.FromInt
        let d = abs (d) |> BigRational.FromInt
        n / d

    let bigRGenOpt (n, d) = bigRGen (n, 1) |> Some

    let bigRGenerator =
        gen {
            let! n = Arb.generate<int>
            let! d = Arb.generate<int>
            return bigRGen (n, d)
        }

    type BigRGenerator() =
        static member BigRational() =
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
            |> fun (br1, br2) -> if br1 = br2 then br1, br2 + 1N else br1, br2
        )
        |> Arb.fromGen
        |> Arb.convert MinMax (fun (MinMax(min, max)) -> min, max)

    type ListOf37<'a> = ListOf37 of 'a List

    let listOf37Arb () =
        Gen.listOfLength 37 Arb.generate
        |> Arb.fromGen
        |> Arb.convert ListOf37 (fun (ListOf37 xs) -> xs)

    let config =
        { FsCheckConfig.defaultConfig with
            arbitrary =
                [
                    typeof<BigRGenerator>
                    typeof<ListOf37<_>>.DeclaringType
                    typeof<MinMax>.DeclaringType
                ]
                @ FsCheckConfig.defaultConfig.arbitrary
            maxTest = 1000
        }

    let testProp testName prop =
        prop |> testPropertyWithConfig config testName


module Expecto =

    open Expecto

    let run = runTestsWithCLIArgs [] [| "--summary" |]


open Expecto
open Expecto.Flip
open Informedica.GenOrder.Lib


/// Tests for Medication scenarios
module MedicationTests =

    let private normalizeWords (s: string) =
        s.Split([| ' '; '\t'; '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
        |> String.concat " "

    let tests =
        testList
            "Medication Scenarios"
            [

                test "fullMedication toString outputs all fields" {
                    let actual =
                        Scenarios.fullMedication
                        |> Medication.toString
                        |> String.concat "\n"
                        |> normalizeWords

                    let expected = Scenarios.fullMedicationText |> normalizeWords

                    actual |> Expect.equal "should match expected output with all fields" expected
                }

            ]


// Run the tests
MedicationTests.tests |> Expecto.run
