

#load "load.fsx"
#load "../Types.fs"
#load "../Utils.fs"
#load "../Mapping.fs"
#load "../VenousAccess.fs"
#load "../Patient.fs"
#load "../DoseType.fs"
#load "../Product.fs"
#load "../Filter.fs"
#load "../DoseRule.fs"
#load "../Check.fs"
#load "../SolutionRule.fs"
#load "../PrescriptionRule.fs"


open System
open Informedica.GenForm.Lib


module GStand = Informedica.ZForm.Lib.GStand
module RuleFinder = Informedica.ZIndex.Lib.RuleFinder


Environment.SetEnvironmentVariable("GENPRES_PROD", "1")


DoseRule.get ()
|> Array.filter (fun dr ->
    dr.Generic = "abatacept" &&
//    dr.Shape = "" &&
    dr.Route = "iv"
)
|> Check.checkAll


open MathNet.Numerics
open Informedica.GenUnits.Lib

let range = Informedica.GenCore.Lib.Ranges.MinMax.empty

let vu1 =
    (1N |> ValueUnit.singleWithUnit Units.Mass.gram |> Some)
let vu2 =
    (1000N |> ValueUnit.singleWithUnit Units.Mass.milliGram |> Some)

vu1
|> MinMax.inRange
    { range with
        Max =
            (1000N |> ValueUnit.singleWithUnit Units.Mass.milliGram |> Some)
            |> Option.map (Informedica.GenCore.Lib.Ranges.Limit.inclusive)
    }


let refRange =
    Informedica.GenCore.Lib.Ranges.MinMax.empty
    |> Informedica.GenCore.Lib.Ranges.MinMax.setMax
        (vu2 |> Option.map (Informedica.GenCore.Lib.Ranges.Limit.inclusive))

let testRange =
    Informedica.GenCore.Lib.Ranges.MinMax.empty
    |> Informedica.GenCore.Lib.Ranges.MinMax.setMax
        (vu1 |> Option.map (Informedica.GenCore.Lib.Ranges.Limit.inclusive))

Check.checkInRangeOf "" refRange testRange
