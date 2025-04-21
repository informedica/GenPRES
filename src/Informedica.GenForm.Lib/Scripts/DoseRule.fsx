

#load "load.fsx"

open System
open MathNet.Numerics

let dataUrlId = "1yn6UC1OMJ0A2wAyX3r0AA2qlKJ7vEAB6OO0DjneiknE"
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


Product.Reconstitution.get()
|> Array.filter (_.GPK >> String.equals "140376")


DoseRule.get ()
|> Array.filter (_.GPKs >> (Array.exists (String.equals "140376")))
|> Array.skip 0
|> Array.take 1
|> Array.map (fun dr ->
    dr
    |> DoseRule.reconstitute None Locations.AnyAccess
)


DoseRule.get ()
|> Array.tryFind (_.Generic >> String.equalsCapInsens "nutrilon pepti 1")
|> Option.map _.ComponentLimits


Web.getDataUrlIdGenPres ()
|> DoseRule.get_
|> Array.tryFind (fst >> _.Generic >> String.equalsCapInsens "nutrilon pepti 1")
|> Option.map (fun (dr, rs) ->
    dr
    |> DoseRule.addDoseLimits rs
)
|> Option.get


DoseRule.get ()
|> Array.tryFind (_.Generic >> String.equalsCapInsens "noradrenaline")
//|> Option.map _.ComponentLimits