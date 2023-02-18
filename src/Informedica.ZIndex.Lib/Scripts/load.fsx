
#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: FParsec"
#r "nuget: Newtonsoft.Json"
#r "nuget: Aether"


#r "../../Informedica.Utils.Lib/bin/Debug/net6.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net6.0/Informedica.GenUnits.Lib.dll"
#r "../../Informedica.GenCore.Lib/bin/Debug/net6.0/Informedica.GenCore.Lib.dll"

#load "../Types.fs"
#load "../FilePath.fs"
#load "../Json.fs"
#load "../Parser.fs"
#load "../BST001T.fs"
#load "../Zindex.fs"
#load "../Assortment.fs"
#load "../Names.fs"
#load "../Route.fs"
#load "../Substance.fs"
#load "../ConsumerProduct.fs"
#load "../TradeProduct.fs"
#load "../PrescriptionProduct.fs"
#load "../GenericProduct.fs"
#load "../GenPresProduct.fs"
#load "../DoseRule.fs"
#load "../ATCGroup.fs"
#load "../RuleFinder.fs"


open System
open Informedica.Utils.Lib


let zindexPath = __SOURCE_DIRECTORY__ |> Path.combineWith "../../../"

// Check the path to the zindex
zindexPath
|> Path.combineWith "data/zindex/BST000T"
|> File.exists

Environment.CurrentDirectory <- zindexPath

#time