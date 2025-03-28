

#load "load.fsx"

open System

let dataUrlId = "1s76xvQJXhfTpV15FuvTZfB-6pkkNTpSB30p51aAca8I"
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.SetEnvironmentVariable("GENPRES_URL_ID", dataUrlId)



#load "../Types.fs"
#load "../Utils.fs"
#load "../Mapping.fs"
#load "../Mapping.fs"
#load "../Patient.fs"
#load "../LimitTarget.fs"
#load "../DoseType.fs"
#load "../Product.fs"
#load "../Filter.fs"
#load "../DoseRule.fs"

#time


open Informedica.GenForm.Lib


open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL


DoseRule.get ()
|> Array.tryFind (_.Generic >> String.equalsCapInsens "nutrilon pepti 1")
|> Option.map _.ComponentLimits
