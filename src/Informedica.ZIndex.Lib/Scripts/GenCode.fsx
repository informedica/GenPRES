
#load "loadGenCode.fsx"

#time


open Informedica.ZIndex.Lib
open Informedica.Utils.Lib

// File
File.exists <| FilePath.GStandPath + "BST000T"

CodeGen.generateZIndex CodeGen.tableList
|> File.writeTextToFile "Zindex.fs"


