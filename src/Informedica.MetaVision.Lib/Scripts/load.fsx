
#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: FParsec"
#r "nuget: Newtonsoft.Json"
#r "nuget: Aether"
#r "nuget: Markdig"
#r "nuget: ClosedXML, 0.97"


#r "../../Informedica.Utils.Lib/bin/Debug/net6.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net6.0/Informedica.GenUnits.Lib.dll"
#r "../../Informedica.GenCore.Lib/bin/Debug/net6.0/Informedica.GenCore.Lib.dll"
#r "../../Informedica.ZIndex.Lib/bin/Debug/net6.0/Informedica.ZIndex.Lib.dll"
#r "../../Informedica.ZForm.Lib/bin/Debug/net6.0/Informedica.ZForm.Lib.dll"


open System
open Informedica.Utils.Lib

let zindexPath = __SOURCE_DIRECTORY__ |> Path.combineWith "../../../"

// Check the path to the zindex
zindexPath
|> Path.combineWith "data/zindex/BST000T"
|> File.exists

Environment.CurrentDirectory <- zindexPath

#time

