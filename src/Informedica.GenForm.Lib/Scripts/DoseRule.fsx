

#load "load.fsx"

open System

let dataUrlId = "1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA"
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.SetEnvironmentVariable("GENPRES_URL_ID", dataUrlId)



#load "../Types.fs"
#load "../Utils.fs"
#load "../Mapping.fs"
#load "../VenousAccess.fs"
#load "../Patient.fs"
#load "../DoseType.fs"
#load "../Product.fs"
#load "../Filter.fs"
#load "../DoseRule.fs"

#time


open Informedica.GenForm.Lib


open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenCore.Lib.Ranges
open Utils


open DoseRule



Product.get () |> fun xs -> printfn $"{xs |> Array.length}"; xs
|> Array.filter (fun p -> p.Generic |> String.startsWithCapsInsens "morfine")


let data = getData dataUrlId


