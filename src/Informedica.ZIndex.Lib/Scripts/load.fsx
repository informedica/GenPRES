#load "../../../scripts/load-dependencies.fsx"


#r "../../Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.GenUNITS.Lib/bin/Debug/net10.0/Informedica.GenUNITS.Lib.dll"
#r "../../Informedica.GenCORE.Lib/bin/Debug/net10.0/Informedica.GenCORE.Lib.dll"

#load "../Types.fs"
#load "../FilePath.fs"
#load "../Json.fs"
#load "../Parser.fs"
#load "../BST001T.fs"
#load "../BST000T.fs"
#load "../Zindex.fs"
#load "../Names.fs"
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
zindexPath |> Path.combineWith "data/zindex/BST000T" |> File.exists

Environment.CurrentDirectory <- zindexPath

#time
