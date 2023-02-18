
#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: Aether"
#r "nuget: FsToolkit.ErrorHandling"
#r "nuget: Validus"


#r "../../Informedica.Utils.Lib/bin/Debug/net6.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net6.0/Informedica.GenUnits.Lib.dll"


open System
open Informedica.Utils.Lib

fsi.AddPrinter<DateTime>(fun dt -> dt.ToString("dd-MMM-yy"))

let zindexPath = __SOURCE_DIRECTORY__ |> Path.combineWith "../../../"
Environment.CurrentDirectory <- zindexPath

