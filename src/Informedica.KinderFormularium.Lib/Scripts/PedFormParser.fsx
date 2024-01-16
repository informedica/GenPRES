

#time

#load "load.fsx"

#load "../Utils.fs"
#load "../OpenAI.fs"
#load "../Mapping.fs"
#load "../Drug.fs"
#load "../FormularyParsers.fs"
#load "../WebSiteParser.fs"
#load "../Export.fs"


open Informedica.ZIndex.Lib
open Informedica.Utils.Lib.BCL
open Informedica.KinderFormularium.Lib


let formulary = WebSiteParser.getFormulary ()


formulary
//|> Array.filter (fun drug -> drug.Generic |> String.equalsCapInsens "paracetamol")
|> Export.export "kinderformularium.csv"


formulary
|> Array.filter (fun drug -> drug.Generic |> String.equalsCapInsens "paracetamol")
|> Array.map Export.cleanGenericName
|> Array.collect (Drug.mapDrug >> List.toArray)
|> fun xs -> printfn $"{xs |> Array.length}"; xs
|> Export.map
|> List.filter (fun r -> r.route = "ORAAL")
|> List.distinct
|> List.length
