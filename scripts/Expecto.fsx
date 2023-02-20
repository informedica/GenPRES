

#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: Expecto"
#r "nuget: Expecto.FsCheck"
#r "nuget: Unquote"



module Expecto =

    open Expecto

    let run = runTestsWithCLIArgs [] [| "--summary" |]



/// Create the necessary test generators
module Generators =

    open Expecto
    open FsCheck
    open MathNet.Numerics


    let bigRGen (n, d) =
        let d = if d = 0 then 1 else d
        let n = abs(n) |> BigRational.FromInt
        let d = abs(d) |> BigRational.FromInt
        n / d


    let bigRGenerator =
        gen {
            let! n = Arb.generate<int>
            let! d = Arb.generate<int>
            return bigRGen(n, d)
        }


    let chooseFromList xs =
        gen {
           let! i = Gen.choose (0, List.length xs - 1)
           return xs |> List.item i
        }


    type BigRGenerator () =
        static member BigRational () =
            { new Arbitrary<BigRational>() with
                override x.Generator = bigRGenerator
            }


    let config = {
        FsCheckConfig.defaultConfig with
            arbitrary = [
                typeof<BigRGenerator>
            ]
            maxTest = 10000
        }


    let testProp testName prop =
        prop |> testPropertyWithConfig config testName

