
#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: FParsec"
#r "nuget: Newtonsoft.Json"
#r "nuget: FSharp.Data"
#r "nuget: HtmlAgilityPack"
#r "nuget: FSharpPlus, 1.6.0-RC2"


#r "../../Informedica.Utils.Lib/bin/Debug/net8.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net8.0/Informedica.GenUnits.Lib.dll"
#r "../../Informedica.GenCore.Lib/bin/Debug/net8.0/Informedica.GenCore.Lib.dll"


#time

#load "../Utils.fs"
#load "../Drug.fs"
#load "../FormularyParsers.fs"
#load "../WebSiteParser.fs"

open System
open FSharpPlus
open Informedica.Utils.Lib.BCL
open Informedica.KinderFormularium.Lib


WebSiteParser.medications ()
//|> List.distinctBy (fun m -> m.Id, m.Generic.Trim().ToLower())
|> List.length



WebSiteParser.getFormulary () //|> Array.length
(*
|> Array.filter (fun d ->
    d.Doses
    |> List.exists (fun dd ->
        dd.Routes
        |> List.exists (fun dr ->
            dr.Schedules
            |> List.exists(fun ds ->
                match ds.Target with
                | Drug.Target.Unknown _ ->
                    true
                | _ -> false
            )
        )
    )
)
*)
|> Array.toList
|> List.collect (fun d ->
    d.Doses
    |> List.collect (fun dd ->
        dd.Routes
        |> List.collect (fun dr ->
            dr.Schedules
            |> List.collect(fun ds ->
                match ds.Target with
                | Drug.Target.Unknown (s, _) -> [s.ToLower().Trim()]
                | _ -> [ ds.TargetText  ]
            )
        )
    )
)
|> List.distinct
|> List.map String.toLower
|> List.sort
|> fun xs ->
    xs |> List.iteri (printfn "%i\t%s")
    xs
|> List.map (fun s -> s |> FormularyParser.TargetParser.parse)
|> List.filter (fun t -> match t with Drug.Target.Unknown _ -> false | _ -> true)
|> List.distinct
|> List.iteri (printfn "%i. %A")



WebSiteParser.getFormulary()
|> Array.mapi (fun i d ->
    $"{i}. {d.Generic} {d.Doses |> List.length}"
)
|> Array.iter (printfn "%s")


let drug =
    (WebSiteParser._medications ())[1]


WebSiteParser.getDoc id drug.Id drug.Generic

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
let path = $"{Environment.CurrentDirectory}/kinderformularium.txt"


fun (s : string) ->
    System.IO.File.AppendAllText(path, s)
|> WebSiteParser.printFormulary

