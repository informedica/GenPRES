
#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: FParsec"
#r "nuget: Newtonsoft.Json"
#r "nuget: Aether"
#r "nuget: Markdig"
#r "nuget: FSharp.Data"
#r "nuget: HtmlAgilityPack"
#r "nuget: FSharpPlus, 1.6.0-RC2"


#r "../../Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net10.0/Informedica.GenUnits.Lib.dll"
#r "../../Informedica.ZIndex.Lib/bin/Debug/net10.0/Informedica.ZIndex.Lib.dll"
#r "../../Informedica.ZForm.Lib//bin/Debug/net10.0/Informedica.ZForm.Lib.dll"
#r "../../Informedica.GenCore.Lib//bin/Debug/net10.0/Informedica.GenCore.Lib.dll"


open System
open Informedica.Utils.Lib


let zindexPath = __SOURCE_DIRECTORY__ |> Path.combineWith "../../../"
Environment.CurrentDirectory <- zindexPath

Environment.SetEnvironmentVariable("GENPRES_PROD", "1")

