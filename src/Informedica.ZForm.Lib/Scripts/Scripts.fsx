
#load "load.fsx"

#time

open System

open Informedica.Utils.Lib
open Informedica.ZIndex.Lib
open Informedica.ZForm.Lib



let path =
    $"{__SOURCE_DIRECTORY__}/temp.html"


{ Dto.dto with
     Generic = "adrenaline"
     Shape = ""
     Route = "iv"
}
|> Dto.processDto
|> fun dto ->
    printfn $"{dto}"
    { dto with
          GPK =
              dto.GPK
              |> Seq.tryHead
              |> Option.map (fun gpk -> [ gpk ])
              |> Option.defaultValue dto.GPK
    }
    |> Dto.processDto
    |> fun dto  ->
        dto.Text
        |> Markdown.toHtml
        |> File.writeTextToFile path


