
#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: FParsec"

#r "nuget: Expecto"
#r "nuget: Expecto.FsCheck"
#r "nuget: Unquote"


#r "../../Informedica.Utils.Lib/bin/Debug/net6.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net6.0/Informedica.GenUnits.Lib.dll"

#load "../Utils.fs"
#load "../Types.fs"
#load "../Logging.fs"
#load "../Exceptions.fs"
#load "../Variable.fs"


open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenSolver.Lib

module Minimum = Informedica.GenSolver.Lib.Variable.ValueRange.Minimum
module Maximum = Informedica.GenSolver.Lib.Variable.ValueRange.Maximum
module MinMaxCalculator = Variable.ValueRange.MinMaxCalculator


/// Create the necessary test generators
module Generators =

    open Types
    open Expecto
    open FsCheck
    open MathNet.Numerics
    open Informedica.Utils.Lib.BCL


    let bigRGen (n, d) =
        let d = 1
//        let d = if d = 0 then 1 else d
        let n = n |> BigRational.FromInt
        let d = d |> BigRational.FromInt
        n / d


    let bigRGenOpt (n, d) = bigRGen (n, 1) |> Some


    let bigRGenerator =
        gen {
            let! n = Arb.generate<int>
            let! d = Arb.generate<int>
            return bigRGen(n, d)
        }

    let varGenerator =
        let mapToBr min max xs =
            xs
            |> List.map (fun (x1, x2) -> x1, 1)
            |> List.map bigRGen
            |> List.filter (fun x ->
                x > 0N &&
                match min, max with
                | None, None -> x > 0N
                | Some min, None -> x > min
                | Some min, Some max -> x > min && x < max
                | None, Some max -> x < max
            ) |> List.distinct

        let minMax min max minIncl maxIncl =
            match min |> bigRGenOpt, max |> bigRGenOpt with
            | Some min, Some max when min > max -> Some max, Some min
            | Some min, Some max when min = max ->
                if minIncl && maxIncl then Some min, Some max
                else None, None
            | min, max -> min, max

        gen {
            let! minIsNone = Arb.generate<bool>
            let! maxIsNone = Arb.generate<bool>
            let! n = Arb.generate<int>
            let! min = Arb.generate<int * int>
            let! max = Arb.generate<int * int>
            let! incr = Arb.generate<(int * int) list>
            let! vs = Arb.generate<(int * int) list>
            let! minIncl = Arb.generate<bool>
            let! maxIncl = Arb.generate<bool>
            let min, max = minMax min max minIncl maxIncl
            let incr = incr |> mapToBr min max
            let vs =
                vs
                |> mapToBr min max
                |> List.filter (fun x ->
                    incr
                    |> List.exists (fun i ->
                        x |> BigRational.isMultiple i
                    )
                )

            return
                Variable.Dto.createDto
                    $"var_{(abs n).ToString()}"
                    (if minIsNone then None else min)
                    minIncl
                    incr
                    (if maxIsNone then None else max)
                    maxIncl
                    vs
        }

    type BigRGenerator () =
        static member BigRational () =
            { new Arbitrary<BigRational>() with
                override x.Generator = bigRGenerator
            }


    type VarDtoGenerator () =
        static member Variable () =
            { new Arbitrary<Variable.Dto.Dto>() with
                override x.Generator = varGenerator
            }


    let config = {
        FsCheckConfig.defaultConfig with
            arbitrary = [
                typeof<BigRGenerator>
                typeof<VarDtoGenerator>
            ]
            maxTest = 1000
        }


    let testProp testName prop =
        prop |> testPropertyWithConfig config testName


    let run = runTestsWithCLIArgs [] [|"--summary" |]




open MathNet.Numerics
open Informedica.Utils.Lib.BCL

open Expecto
open FsCheck
open Types
open Variable.Operators

module Property = Variable.ValueRange.Property


let generateMinMax n =
    let v1 = (Generators.bigRGenerator |> Arb.fromGen).Generator.Sample(4, n)
    let v2 = (Generators.bigRGenerator |> Arb.fromGen).Generator.Sample(4, n)
    let b1 = Arb.generate<bool> |> Gen.sample 10 n
    let b2 = Arb.generate<bool> |> Gen.sample 10 n
    let s1 = Arb.generate<bool> |> Gen.sample 10 n
    let s2 = Arb.generate<bool> |> Gen.sample 10 n
    let min =
        v1
        |> List.zip b1
        |> List.zip s1
        |> List.map (fun (s,(incl, (br))) ->
            if s then None, false
            else
                Some br, incl
        )
    let max =
        v2
        |> List.zip b2
        |> List.zip s2
        |> List.map (fun (s,(incl, (br))) ->
            if s then None, false
            else
                Some br, incl
        )

    min
    |> List.zip max
    |> List.map (fun ((br1, incl1), (br2, incl2)) ->
        match br1, br2 with
        | Some _, Some _ ->
            if br1 > br2 then (br2, incl2), (br1, incl1)
            else
                if br1 = br2 && (incl1 = incl2 || (not incl2)) then (br2, incl2), (br1, incl1)
                else
                   (br1, incl1), (br2, incl2)
        | _ -> (br1, incl1), (br2, incl2)
    )


let minMax1 = generateMinMax 100
let minMax2 = generateMinMax 100


let testCalc s op =
    minMax1
    |> List.allPairs minMax2
    |> List.map (fun ((min1, max1), (min2, max2)) ->
        (min1, max1),
        (min2, max2),
        MinMaxCalculator.calcMinMax op min1 max1 min2 max2
    )
    |> List.map (fun (r1, r2, r) ->
        let minToStr min =
            min
            |> Option.map (Minimum.toString true)
            |> Option.defaultValue "<"
        let maxToStr max =
            max
            |> Option.map (Maximum.toString true)
            |> Option.defaultValue ">"

        let toMin ((br, b), _) =
            br |> Option.map (fun br ->
                Minimum.create b br
            )
            |> minToStr

        let toMax (_, (br, b)) =
            br |> Option.map (fun br ->
                Maximum.create b br
            )
            |> maxToStr

        $"{r1 |> toMin} .. {r1 |> toMax} {s} {r2 |> toMin} .. {r2 |> toMax} = {r |> fst |> minToStr} .. {r |> snd |> maxToStr} "
    )
    |> List.distinct
    |> List.iteri (printfn "%i. %s")


testCalc "*" (*)
testCalc "/" (/)
testCalc "*" (*)
testCalc "*" (*)


open MinMaxCalculator

let testMatch () =
    minMax1
    |> List.allPairs minMax2
    |> List.map (fun ((min1, max1), (min2, max2)) ->
        try
            match (min1 |> fst, max1 |> fst), (min2 |> fst, max2 |> fst) with
            | NN, NN
            | NN, NP
            | NN, PP
            | NN, NZ
            | NN, ZP
            | NP, NN
            | NP, NP
            | NP, PP
            | NP, NZ
            | NP, ZP
            | PP, NN
            | PP, NP
            | PP, PP
            | PP, NZ
            | PP, ZP
            | NZ, NN
            | NZ, NP
            | NZ, PP
            | NZ, NZ
            | NZ, ZP
            | ZP, NN
            | ZP, NP
            | ZP, PP
            | ZP, NZ
            | ZP, ZP -> "can match"
        with
        | _ -> $"cannot match {min1}, {max1},{min2}, {max2}"
    )

testMatch ()
|> List.distinct


