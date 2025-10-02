
#r "nuget: Newtonsoft.Json"
#r "../../Informedica.Utils.Lib/bin/Debug/net9.0/Informedica.Utils.Lib.dll"

open System
open Informedica.Utils.Lib

let zindexPath = __SOURCE_DIRECTORY__ |> Path.combineWith "../../../"

// Check the path to the zindex
zindexPath
|> Path.combineWith "data/zindex/BST000T"
|> File.exists


#load "../FilePath.fs"
#load "../Json.fs"
#load "../Parser.fs"
#load "../BST001T.fs"
#load "../BST000T.fs"
#load "../CodeGen.fs"


Environment.CurrentDirectory <- zindexPath