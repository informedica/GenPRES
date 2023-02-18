
//#I __SOURCE_DIRECTORY__

#r "nuget: Unquote"

#load "load.fsx"

open MathNet.Numerics

open Informedica.GenUnits.Lib
open Informedica.Utils.Lib.BCL


open Swensen.Unquote
open ValueUnit

let x = create Units.Count.times
let mg = create Units.Mass.milliGram
let mcg = create Units.Mass.microGram
let ml = create Units.Volume.milliLiter
let kg = create Units.Mass.kiloGram
let hr = create Units.Time.hour
let day = create Units.Time.day
let day2 = create (Units.Time.nDay 2N)
let min = create Units.Time.minute
let week2 = create (Units.Time.nWeek 2N)
let day14 = create (Units.Time.nDay 14N)

// Test that 10 ml / 5 ml = 2
test<@
    ([|10N|] |> ml) / ([|5N|] |> ml) =? ([|2N|] |> x)
@>

// Test that 1 kg / 1000 mg = 1000
test<@
    ([|1N|] |> kg) / ([|1000N|] |> mg) =? ([|1000N|] |> x)
@>

// Test that 10 * 2 mg = 20 mg
test<@
    ([|10N|] |> x) * ([|1N|] |> mg) =? ([|10N|] |> mg)
@>

// Test that 10 mg * (2 x / day) = 20 mg / day
test<@
    ([|10N|] |> mg) * (([|2N|] |> x) / ([|1N|] |> day)) =? (([|20N|] |> mg) / ([|1N|] |> day))
@>

// Test that 20 mg * (1 x / 2 day) = 10 mg / day
test<@
    ([|20N|] |> mg) * (([|1N|] |> x) / ([|2N|] |> day)) =? (([|10N|] |> mg) / ([|1N|] |> day))
@>


// Test that 20 mg * (1 x / 2 day) = 10 mg / day
test<@
    ([|20N|] |> mg) * (([|1N|] |> x) / ([|1N|] |> day2)) =? (([|10N|] |> mg) / ([|1N|] |> day))
@>

// Test that 2 day / 1 day = 2
test<@
    ([|1N|] |> day2) / ([|1N|] |> day) =? ([|2N|] |> x) 
@>



// Test that 1 x / 2 week divide by 1 x / 14 days yields 1
test<@
    (([|1N|] |> x ) / ([|1N|] |> week2)) / (([|1N|] |> x) / ([|1N|] |> day14)) = ([|1N|] |> x)
@>


test<@
    ([|1000N|] |> mcg) =? ([|1N|] |> mg) 
@>



let mgPerKgPerDay = 
    (CombiUnit (Units.Mass.milliGram, OpPer, Units.Weight.kiloGram), OpPer,
    Units.Time.day)
    |> CombiUnit


([|90N|] |> create mgPerKgPerDay) >? ([|3000N|] |> create mgPerKgPerDay)
([|3000N|] |> create mgPerKgPerDay) >? ([|90N|] |> create mgPerKgPerDay)

