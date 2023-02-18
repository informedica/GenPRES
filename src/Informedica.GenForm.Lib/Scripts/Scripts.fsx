

#time

#load "load.fsx"


#load "../Types.fs"
#load "../Utils.fs"
#load "../Mapping.fs"
#load "../MinMax.fs"
#load "../Patient.fs"
#load "../DoseType.fs"
#load "../Product.fs"
#load "../Filter.fs"
#load "../DoseRule.fs"
#load "../SolutionRule.fs"
#load "../PrescriptionRule.fs"


open System
open System.IO


open MathNet.Numerics

open Informedica.Utils.Lib
open Informedica.GenForm.Lib



{ Patient.patient with
    Department = "ICK"
    Age = 365N * 4N |> Some
    Weight = 17000N |> Some
    Location = CVL
}
|> PrescriptionRule.get
|> Array.filter (fun r -> r.DoseRule.Generic = "acetazolamide")
|> Array.item 0
