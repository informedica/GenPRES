

#time

#load "load.fsx"

#load "../Utils.fs"
#load "../Mapping.fs"
#load "../Drug.fs"
#load "../FormularyParsers.fs"
#load "../WebSiteParser.fs"
#load "../Export.fs"


open System
open FSharpPlus

open Informedica.ZIndex.Lib
open Informedica.Utils.Lib.BCL
open Informedica.KinderFormularium.Lib


WebSiteParser.getEmptyRules ()
|> Array.length


WebSiteParser.getFormulary ()
|> Export.map
|> Export.writeToFile "kinderformularium.csv"



WebSiteParser.getFormulary ()
|> Array.filter (fun d -> d.Generic = "Abatacept")

