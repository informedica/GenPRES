

#time

#load "load.fsx"

open System
let dataUrlId = "1s76xvQJXhfTpV15FuvTZfB-6pkkNTpSB30p51aAca8I"
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
#load "../RenalRule.fs"
#load "../PrescriptionRule.fs"



open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib


Product.get ()
|> Array.filter (fun p ->
    p.Generic = "abacavir" &&
    p.Routes |> Array.exists (String.equalsCapInsens "oraal")
)


let dr =
    DoseRule.get ()
    |> DoseRule.filter
        { Filter.doseFilter with
            Patient =
                { Patient.patient with
                    Locations =  [ CVL ]
                    Department = Some "ICK"
                    Age =
                        Units.Time.year
                        |> ValueUnit.singleWithValue 10N
                        |> Some
                    Weight =
                      Units.Weight.kiloGram
                      |> ValueUnit.singleWithValue (40N)
                      |> Some
                }
            Generic = Some "vancomycine"
            Shape = Some ""
            Route = Some "intraveneus"
        }
    |> Array.head

dr
|> DoseRule.addShapeLimits

Mapping.filterRouteShapeUnit dr.Route dr.Shape NoUnit


SolutionRule.get ()
|> Array.filter (fun sr ->
    sr.Generic = "amikacine" &&
    sr.Route =  "intraveneus" &&
    sr.Department = Some "ICK"
)


let pr =
    { Filter.doseFilter with
        Generic = Some "vancomycine"
        Route = Some "intraveneus"
        DoseType = Continuous "" |> Some
        Patient =
            { Patient.patient with
                Department = Some "ICK"
                Age =
                    Units.Time.year
                    |> ValueUnit.singleWithValue 10N
                    |> Some
                Weight =
                  Units.Weight.kiloGram
                  |> ValueUnit.singleWithValue (40N)
                  |> Some
//                RenalFunction = EGFR(Some 5, Some 5) |> Some
            }
    }
    |> PrescriptionRule.filter


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
//                RenalFunction = EGFR(Some 5, Some 5) |> Some
}
|> PrescriptionRule.get
|> Array.last
|> fun pr -> pr.DoseRule.DoseLimits |> Array.toList

pr[0].DoseRule.DoseLimits |> Array.length
pr[0].RenalRules

let printAllDoseRules () =
    let rs =
        Filter.doseFilter
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
        |> ValueUnit.singleWithValue 16N
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
    { Filter.doseFilter with
        Route = Some "oraal"
    }


Web.getDataFromSheet "" "DoseRules"
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


DoseRule.get ()
|> Array.filter (fun dr ->
    dr.Route |> String.equalsCapInsens "rectaal"
)