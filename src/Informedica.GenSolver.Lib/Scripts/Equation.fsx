
#load "load.fsx"

open MathNet.Numerics
open Informedica.GenSolver.Lib

let a = 
    {
        Name = "a" |> Variable.Name.create
        Values =
            [| |]
    }