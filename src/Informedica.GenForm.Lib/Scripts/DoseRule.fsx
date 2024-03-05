

#load "load.fsx"

open System

let dataUrlId = "1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA"
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.SetEnvironmentVariable("GENPRES_URL_ID", dataUrlId)



#load "../Types.fs"
#load "../Utils.fs"
#load "../Mapping.fs"
#load "../VenousAccess.fs"
#load "../Mapping.fs"
#load "../Patient.fs"
#load "../LimitTarget.fs"
#load "../DoseType.fs"
#load "../Product.fs"
#load "../Filter.fs"
#load "../DoseRule.fs"
#load "../SolutionRule.fs"

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


open Informedica.GenUnits.Lib

let filter =
    { Filter.filter with
        Patient =
            { Patient.patient with
                VenousAccess = [VenousAccess.CVL]
                Department = Some "ICK"
                Age =
                    Units.Time.year
                    |> ValueUnit.singleWithValue 12N
                    |> Some
                Weight =
                  Units.Weight.kiloGram
                  |> ValueUnit.singleWithValue (30N)
                  |> Some
            }
    }


DoseRule.get ()
|> Array.take 1
|> DoseRule.filter filter
|> Array.item 0
|> fun dr ->
    SolutionRule.get ()
    |> SolutionRule.filter
        { filter with
            Generic = dr.Generic |> Some
            Shape = dr.Shape |> Some
            Route = dr.Route |> Some
            DoseType = dr.DoseType |> DoseType.toString |> Some
        }


"kCal"
|> Mapping.mapUnit

Mapping.unitMapping

let s = "IE" |> String.toLower |> String.trim
Mapping.unitMapping
|> Array.tryFind (fun r ->
    r.Long |> String.equalsCapInsens s ||
    r.Short |> String.equalsCapInsens s ||
    r.MV |> String.equalsCapInsens s
)
|> function
    | Some r -> $"{r.Short}[{r.Group}]" |> Units.fromString
    | None -> None

