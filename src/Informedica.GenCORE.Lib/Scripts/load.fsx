#load "../../../scripts/load-dependencies.fsx"


#r "../../Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net10.0/Informedica.GenUnits.Lib.dll"

#load "../Measures.fs"
#load "../Validus.fs"
#load "../Calculations.fs"
#load "../ValueUnit.fs"
#load "../MinMax.fs"
#load "../Patient.fs"

open System
open Informedica.Utils.Lib

fsi.AddPrinter<DateTime>(_.ToString("dd-MMM-yy"))

let zindexPath = __SOURCE_DIRECTORY__ |> Path.combineWith "../../../"
Environment.CurrentDirectory <- zindexPath
