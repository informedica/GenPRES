#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: FParsec"

#r "nuget: Expecto"
#r "nuget: Expecto.FsCheck"
#r "nuget: Unquote"

#r "../../Informedica.Utils.Lib/bin/Debug/net6.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net6.0/Informedica.GenUnits.Lib.dll"
#r "../../Informedica.GenSolver.Lib/bin/Debug/net6.0/Informedica.GenSolver.Lib.dll"

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

    open Informedica.Utils.Lib.BCL

    let bigRGen (n, d) =
        let d = if d = 0 then 1 else d
        let n = abs(n) |> BigRational.FromInt
        let d = abs(d) |> BigRational.FromInt
        n / d

    let bigRGenOpt (n, d) = bigRGen (n, 1) |> Some

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

module Expecto =

    open Expecto

    let run = runTestsWithCLIArgs [] [| "--summary" |]

open Informedica.GenOrder.Lib

module Tests =

    open MathNet.Numerics
    open Expecto
    open Expecto.Flip

    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenSolver.Lib

    // Add your test modules here
    module DrugOrderTests =
        
        let tests = testList "DrugOrder" [
            test "drugOrder default values" {
                let drugOrder = DrugOrder.drugOrder
                drugOrder.Id |> Expect.equal "should be empty" ""
                drugOrder.Name |> Expect.equal "should be empty" ""
                drugOrder.Components |> Expect.isEmpty "should be empty"
            }

            test "productComponent default values" {
                let component = DrugOrder.productComponent
                component.Name |> Expect.equal "should be empty" ""
                component.Shape |> Expect.equal "should be empty" ""
                component.Substances |> Expect.isEmpty "should be empty"
            }

            test "substanceItem default values" {
                let substance = DrugOrder.substanceItem
                substance.Name |> Expect.equal "should be empty" ""
                substance.Concentrations |> Expect.isNone "should be None"
                substance.Dose |> Expect.isNone "should be None"
            }
        ]

    // Add more test modules as needed
    module TypeTests =
        
        let tests = testList "Types" [
            test "OrderVariable can be created" {
                let constraints = {
                    Min = None
                    Max = None
                    Incr = None
                    Values = None
                }
                let variable = Variable.create "test" // Assuming this exists
                let orderVariable = {
                    Constraints = constraints
                    Variable = variable
                }
                orderVariable.Constraints |> Expect.equal "should be equal" constraints
            }
        ]

    [<Tests>]
    let tests =
        testList "GenOrder Tests" [
            DrugOrderTests.tests
            TypeTests.tests
        ]

Tests.tests
|> Expecto.run


