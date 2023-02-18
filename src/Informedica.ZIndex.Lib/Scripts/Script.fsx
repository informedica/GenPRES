
#load "load.fsx"

#time

open System.Collections.Generic


open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.ZIndex.Lib


// File
File.exists <| FilePath.GStandPath + "BST000T"


Json.clearCache ()
// Load all
printfn "Loading GenPresProduct ..."
GenPresProduct.load true
printfn "Loading ATCGroup ..."
ATCGroup.load ()
printfn "Loading DoseRule ..."
DoseRule.load ()
printfn "Loading Substance"
Substance.load ()


GenPresProduct.getRoutes ()
|> Array.sortBy String.toLower
|> Array.iter (printfn "%s")


DoseRule.routes ()
|> Array.sortBy String.toLower
|> Array.iter (printfn "%s")

