

#load "load.fsx"

open System

let dataUrlId = "1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA"
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.SetEnvironmentVariable("GENPRES_URL_ID", dataUrlId)


#load "../Types.fs"
#load "../Utils.fs"
#load "../Mapping.fs"
#load "../Patient.fs"
#load "../LimitTarget.fs"
#load "../DoseType.fs"
#load "../Product.fs"
#load "../Filter.fs"
#load "../DoseRule.fs"
#load "../SolutionRule.fs"

open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib

SolutionRule.get ()
|> Array.filter (fun sr -> sr.SolutionLimits |> Array.length > 1)
|> Array.length

DoseRule.get ()
|> Array.filter (fun dr -> dr.Generic |> String.equalsCapInsens "samenstelling c")
|> Array.head