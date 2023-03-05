
#r "nuget: Unquote"
#r "nuget: Expecto"
#r "nuget: Expecto.FsCheck"


#load "../../../src/Informedica.GenUnits.Lib/Scripts/load.fsx"

#load "../Tests.fs"

open Expecto
open Expecto.Flip
open Expecto.Logging

open MathNet.Numerics
open Informedica.GenUnits.Lib

let run = runTestsWithCLIArgs [ CLIArguments.Verbosity LogLevel.Verbose ] [||]


open Informedica.GenUnits.Lib.ValueUnit

let toString = toStringEngShort

let inline (>>*) u f = 
    u |> printfn "%A"
    f u

// Some basic units
let mg400 = 400N |> create Units.Mass.milliGram
let gram2 = 2N   |> create Units.Mass.gram
let ml50  = 50N  |> create Units.Volume.milliLiter
let ml5  = 5N    |> create Units.Volume.milliLiter
let l5 = 5N      |> create Units.Volume.liter 
let day2 = 2N    |> create Units.Time.day
let hour3 = 3N   |> create Units.Time.hour

// The count group is a special unit group 
// with only one unit: times. 
let times3 = 3N |> create Units.Count.times
let times100 = 100N |> create Units.Count.times


[<Tests>]
let unitTests =

    let toBase = toBase >> BigRational.ToDouble

    testList "Unit" [
        test "base value of 400 mg " {
            Expect.equal "should equal 0.4 g" 0.4 (mg400 |> toBase)
        }

        test "base value of 50 ml = 0.05 l" {
            Expect.isTrue "should equal 0.05" (ml50 |> toBase = 0.05)
        }

        test "base value of 5 ml = 0.005 l" {
            Expect.isTrue "should equal 0.005" (ml5 |> toBase = 0.005)
        }

        test "base value of 5 l = 5 l" {
            Expect.isTrue "should equal 5" (l5 |> toBase = 5.)
        }        
        
        test "count 3 times 5 ml results in 15 ml" {
            Expect.equal "should equal 0.015 L" 0.015 (ml5 * times3 |> toBase)
        }

        test "base value of 1 day" {
            let vu = (1. |> withUnit (Units.Time.day))
            Expect.equal "" (60. * 60. * 24.) (vu |> toBase)
        }

        test "3 days" {
            let vu = (1. |> withUnit (Units.Time.nDay 3N))
            Expect.equal "" (3. * 60. * 60. * 24.) (vu |> toBase)
        }

        test "there and back again one unit" {
            let vu = 
                mg400 
                |> get 
                |> fun (_, u) ->
                    mg400
                    |> ValueUnit.toBase
                    |> create u 
                    |> toUnit 
                    |> create u
            Expect.equal "" vu mg400
        }

        test "there and back again 2 units" {
            let vu1 = 
                1. |> withUnit (Units.Mass.milliGram |> per Units.Volume.milliLiter)
            let vu2 = 
                vu1     
                |> get 
                |> fun (_, u) ->
                    vu1
                    >>* ValueUnit.toBase
                    >>* create u 
                    >>* toUnit 
                    >>* create u
            Expect.equal "" vu1 vu2
        }

        test "there and back again 3 units" {
            let vu1 = 
                1. 
                |> withUnit (Units.Mass.milliGram 
                |> per Units.Volume.milliLiter
                |> per Units.Time.day)
            let vu2 = 
                vu1     
                |> get 
                |> fun (_, u) ->
                    vu1
                    >>* ValueUnit.toBase
                    >>* create u 
                    >>* toUnit 
                    >>* create u
            Expect.equal "" vu1 vu2
        }
    ]

unitTests
|> run


let simplifyTests =

    testList "simplify" [

        test "no unit" {
            let vu = create NoUnit 1N
            Expect.equal "" vu (vu |> simplify) 
        }

        test "400 mg" {
            Expect.equal "" mg400 (mg400 |> simplify)
        }

        test "400 mg / ml" {
            let vu = 
                Units.Mass.milliGram
                |> per Units.Volume.milliLiter
                |> withValue 400N
            
            vu
            |> simplify
            |> Expect.equal "" vu
        }

        test "400 mg / ml / hour" {
            let vu = 
                Units.Mass.milliGram
                |> per Units.Volume.milliLiter
                |> per Units.Time.hour
                |> withValue 400N

            
            vu
            |> simplify
            |> Expect.equal "" vu
        }
    ]


simplifyTests
|> run