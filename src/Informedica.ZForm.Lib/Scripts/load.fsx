
#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: FParsec"
#r "nuget: Newtonsoft.Json"
#r "nuget: Aether"
#r "nuget: Markdig"

#r "../../Informedica.Utils.Lib/bin/Debug/net6.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net6.0/Informedica.GenUnits.Lib.dll"
#r "../../Informedica.GenCore.Lib/bin/Debug/net6.0/Informedica.GenCore.Lib.dll"
#r "../../Informedica.ZIndex.Lib/bin/Debug/net6.0/Informedica.ZIndex.Lib.dll"

#load "../Types.fs"
#load "../Utils.fs"
#load "../Markdown.fs"
#load "../Mapping.fs"
#load "../ValueUnit.fs"
#load "../ValueUnit.fs"
#load "../Product.fs"
#load "../PatientCategory.fs"
#load "../DoseRule.fs"
#load "../GStand.fs"
#load "../Dto.fs"

open System
open Informedica.Utils.Lib

let zindexPath = __SOURCE_DIRECTORY__ |> Path.combineWith "../../../"

// Check the path to the zindex
zindexPath
|> Path.combineWith "data/zindex/BST000T"
|> File.exists

Environment.CurrentDirectory <- zindexPath

#time

