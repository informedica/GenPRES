
#load "load.fsx"


open Informedica.ZForm.Lib




let config =
    {
        UseAll = true
        IsRate = false
        SubstanceUnit = None
        TimeUnit = None
    }


let path = $"{__SOURCE_DIRECTORY__}/paracetamol.md"


GStand.createDoseRules config None None None None "paracetamol" "" ""
|> Seq.map (fun dr ->
    dr
    |> DoseRule.toString true
)
|> fun s -> System.IO.File.WriteAllText(path, s |> String.concat "\n")


