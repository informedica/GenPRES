

#load "load.fsx"


open Informedica.Utils.Lib
open Informedica.ZForm.Lib



let config =
    {
        GPKs = []
        IsRate = false
        SubstanceUnit = None
        TimeUnit = None
    }


let path = $"{__SOURCE_DIRECTORY__}/temp.html"


GStand.createDoseRules config None None None None "" "" ""
|> Seq.map (fun dr ->
    dr
    |> DoseRule.toString false
)
|> String.concat "\n"
|> Markdown.toHtml
|> File.writeTextToFile path




