

#load "load.fsx"

#time

open System
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.ZForm.Lib


Environment.SetEnvironmentVariable("GENPRES_PROD", "1")

let config =
    {
        GPKs = []
        IsRate = false
        SubstanceUnit = None
        TimeUnit = None
    }


let path = $"{__SOURCE_DIRECTORY__}/gstand.html"


GStand.createDoseRules config None None None None "paracetamol" "zetpil" "rectaal"
|> Seq.sortBy (fun dr -> dr.Generic |> String.toLower)
|> Seq.map (Informedica.ZForm.Lib.DoseRule.toString false)
|> String.concat "\n\n"
|> Markdown.toHtml
|> File.writeTextToFile path


open Informedica.ZIndex.Lib


{ Patient = { Age = Some 12.
              Weight = Some 10.
              BSA = None }
  Product = GenericShapeRoute { Generic = "paracetamol"
                                Shape = "zetpil"
                                Route = "rectaal" } }
|> RuleFinder.find []
//|> Array.skip 1
//|> Array.take 1
|> GStand.getSubstanceDoses config
|> Seq.iter (printfn "%A")





let maximizeDosages (dosages : Dosage list) =
//    let maximize = MinMax.foldMaximize true true
    let maxRange (first : DoseRange) (rest : DoseRange list) =
        rest
        |> List.fold (fun acc dr ->
                { acc with Norm = dr.Norm }
        ) first

    dosages
    |> function
        | [] -> None
        | dosage::rest ->
            { dosage with
                SingleDosage =
                    rest
                    |> List.map _.SingleDosage
                    |> maxRange dosage.SingleDosage
            }
            |> Some