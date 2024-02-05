

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



Product.get ()
|> fun xs -> printfn $"{xs |> Array.length}"; xs
|> Array.filter (fun p -> p.Generic |> String.startsWithCapsInsens "chloorhexidine")


let data = getData dataUrlId

data
|> Array.distinctBy (fun r ->
    r.Brand,
    r.Department,
    r.Diagn,
    r.Gender,
    r.Generic,
    r.Indication,
    r.Route,
    r.Shape,
    r.Substance,
    r.AdjustUnit,
    r.DoseType,
    r.DoseUnit,
    r.Frequencies,
    r.FreqUnit,
    r.DurUnit,
    r.IntervalUnit,
    r.MaxAge,
    r.MaxDur,
    r.MaxInterval,
    r.MaxQty,
    r
)


Web.getDataFromSheet dataUrlId "DoseRules"
|> fun data ->
    let getColumn =
        data
        |> Array.head
        |> Csv.getStringColumn

    data
    |> Array.skip 1
    |> fun xs -> printfn $"{xs |> Array.length}"; xs
    |> Array.distinctBy (fun row ->
        row |> Array.tail
    )

|> Array.length
