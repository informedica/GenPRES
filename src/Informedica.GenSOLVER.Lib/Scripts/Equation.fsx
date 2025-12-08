
#load "load.fsx"

open MathNet.Numerics
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib

let a = 
    {
        Name = "a" |> Variable.Name.createExc
        Values =
            [| 1N |]
            |> ValueUnit.withUnit Units.Volume.milliLiter
            |> Variable.ValueRange.ValueSet.create
            |> ValSet
    }