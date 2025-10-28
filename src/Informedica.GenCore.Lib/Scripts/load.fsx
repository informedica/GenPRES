
#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: Aether"
#r "nuget: FsToolkit.ErrorHandling"
#r "nuget: Validus"
#r "nuget: FParsec"


#r "../../Informedica.Utils.Lib/bin/Debug/net9.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net9.0/Informedica.GenUnits.Lib.dll"

#load "../Measures.fs"
#load "../Aether.fs"
#load "../Validus.fs"
#load "../Calculations.fs"
#load "../ValueUnit.fs"
#load "../MinMax.fs"
#load "../Patient.fs"

open System
open Informedica.Utils.Lib

fsi.AddPrinter<DateTime>(fun dt -> dt.ToString("dd-MMM-yy"))

let zindexPath = __SOURCE_DIRECTORY__ |> Path.combineWith "../../../"
Environment.CurrentDirectory <- zindexPath

