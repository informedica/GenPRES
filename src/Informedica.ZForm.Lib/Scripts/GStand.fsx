

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


GStand.createDoseRules config None None None None "" "" ""
|> Seq.sortBy (fun dr -> dr.Generic |> String.toLower)
|> Seq.map (DoseRule.toString false)
|> String.concat "\n\n"
|> Markdown.toHtml
|> File.writeTextToFile path
