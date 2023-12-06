

#time

#load "load.fsx"


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


open System



open Informedica.Utils.Lib
open Informedica.GenForm.Lib

Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.CurrentDirectory


{ Filter.filter with
//    GestAgeInDays = (Some (28N * 7N))
//    Generic = (Some "paracetamol")
    Route = Some "rect"
}
|> PrescriptionRule.filter
|> Array.map (fun pr -> pr.DoseRule)
|> Array.take 1
|> DoseRule.Print.toMarkdown


let printAllDoseRules () =
    let rs =
        Filter.filter
        |> PrescriptionRule.filter
        |> Array.map (fun pr -> pr.DoseRule)

    let gs (rs : DoseRule[]) =
        rs
        |> Array.map (fun dr -> dr.Generic)
        |> Array.distinct

    DoseRule.Print.printGenerics gs rs


printAllDoseRules ()
|> String.concat "\n\n  ---\n"
|> Informedica.ZForm.Lib.Markdown.toHtml
|> File.writeTextToFile "doserules.html"

