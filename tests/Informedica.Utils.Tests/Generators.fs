namespace Informedica.Utils.Tests




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


