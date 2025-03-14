

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



open Informedica.GenUnits.Lib


let filter =
    { Filter.doseFilter with
        Generic = Some "Samenstelling D"
        Indication = Some "TPN"
        DoseType = "dag 3" |> DoseType.Timed |> Some
        Patient =
            { Patient.patient with
                Locations =  [ CVL ]
                Department = Some "ICK"
                Age =
                    Units.Time.year
                    |> ValueUnit.singleWithValue 6N
                    |> Some
                Weight =
                  Units.Weight.kiloGram
                  |> ValueUnit.singleWithValue (22N)
                  |> Some
            }
    }


let dr =
    DoseRule.get ()
//    |> Array.take 1
    |> DoseRule.filter filter //|> Array.length
    |> Array.item 0

dr.DoseLimits |> Array.length


dr
|> fun dr ->
    SolutionRule.get ()
    |> SolutionRule.filter
        { filter with
            Generic = dr.Generic |> Some
            //Shape = dr.Shape |> Some
            //Route = dr.Route |> Some
            DoseType = dr.DoseType |> DoseType.toString |> Some
        }


SolutionRule.get ()
|> Array.filter(fun sr -> sr.Generic = "alprostadil")