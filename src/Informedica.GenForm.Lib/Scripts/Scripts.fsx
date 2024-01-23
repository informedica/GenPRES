

#time

#load "load.fsx"

open System
let dataUrlId = "16ftzbk2CNtPEq3KAOeP7LEexyg3B-E5w52RPOyQVVks"
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
#load "../SolutionRule.fs"
#load "../PrescriptionRule.fs"



open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib


Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.CurrentDirectory

let urlId = Environment.GetEnvironmentVariable("GENPRES_URL_ID")


{ Filter.filter with
    Patient =
        { Filter.filter.Patient with
//            Department = Some "ICK"
            Age =
                1N
                |> ValueUnit.singleWithUnit Units.Time.year
                |> Some
            (*
            GestAge =
                28N
                |> ValueUnit.singleWithUnit Units.Time.week
                |> Some
            *)
        }
    Generic = (Some "morfine")
    //Route = Some "oraal"
}
|> PrescriptionRule.filter
|> Array.map _.DoseRule
|> Array.take 1
|> DoseRule.Print.toMarkdown


let printAllDoseRules () =
    let rs =
        Filter.filter
        |> PrescriptionRule.filter
        |> Array.map _.DoseRule

    let gs (rs : DoseRule[]) =
        rs
        |> Array.map _.Generic
        |> Array.distinct

    DoseRule.Print.printGenerics gs rs


printAllDoseRules ()
|> String.concat "\n\n  ---\n"
|> Informedica.ZForm.Lib.Markdown.toHtml
|> File.writeTextToFile "doserules.html"


SolutionRule.get ()
|> Array.filter (fun sr -> sr.Generic = "adrenaline")
|> SolutionRule.Print.toMarkdown ""


{ Patient.patient with
    Age =
        Units.Time.year
        |> ValueUnit.singleWithValue 1N
        |> Some
}
|> PrescriptionRule.get
|> Array.filter (_.SolutionRules >> Array.isEmpty >> not)
|> Array.item 1
|> fun pr ->
    pr.SolutionRules
    |> SolutionRule.Print.toMarkdown "\n"


DoseRule.get ()
|> DoseRule.filter
    { Filter.filter with
        Patient =
            { Filter.filter.Patient with
    //            Department = Some "ICK"
                Age =
                    0N
                    |> ValueUnit.singleWithUnit Units.Time.day
                    |> Some
                (*
                GestAge =
                    28N
                    |> ValueUnit.singleWithUnit Units.Time.week
                    |> Some
                *)
            }
        Generic = (Some "paracetamol")
        Route = Some "intraveneus"
    }


Web.getDataFromSheet urlId "DoseRules"
|> fun data ->
    let getColumn =
        data
        |> Array.head
        |> Csv.getStringColumn

    data
    |> Array.tail
    |> Array.take 10
    |> Array.map (fun r ->
        let get = getColumn r
        let toBrOpt = BigRational.toBrs >> Array.tryHead

        printfn $"{r[22]}"
        printfn $"""{get "Generic"}: {get "Freqs"}"""
    )

