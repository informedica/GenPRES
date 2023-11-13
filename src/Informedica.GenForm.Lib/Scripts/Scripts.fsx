

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


{ Filter.filter with
    Department = (Some "ICK")
    AgeInDays = (Some (1N * 365N))
    WeightInGram = (Some (10N * 100N))
    HeightInCm = (Some 100N)
    Generic = (Some "dexamethason")
}
|> PrescriptionRule.filter
|> PrescriptionRule.routes

