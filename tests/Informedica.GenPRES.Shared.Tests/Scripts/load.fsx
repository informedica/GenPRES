#load "../../../scripts/load-dependencies.fsx"


#r "../../../src/Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"
#r "../../../src/Informedica.GenPRES.Shared/bin/Debug/net10.0/Informedica.GenPRES.Shared.dll"


open System
open Informedica.Utils.Lib

fsi.AddPrinter<DateTime>(_.ToString("dd-MMM-yy"))

let zindexPath = __SOURCE_DIRECTORY__ |> Path.combineWith "../../../"
Environment.CurrentDirectory <- zindexPath
