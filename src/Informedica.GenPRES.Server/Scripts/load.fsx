

#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: FParsec"
#r "nuget: Newtonsoft.Json"
#r "nuget: Aether"
#r "nuget: Markdig"
#r "nuget: ClosedXML"
#r "nuget: FsToolkit.ErrorHandling"
#r "nuget: ConsoleTables"
#r "nuget: IcedTasks"


#r "../../Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.Agents.Lib/bin/Debug/net10.0/Informedica.Agents.Lib.dll"
#r "../../Informedica.Logging.Lib/bin/Debug/net10.0/Informedica.Logging.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net10.0/Informedica.GenUnits.Lib.dll"
#r "../../Informedica.GenCore.Lib/bin/Debug/net10.0/Informedica.GenCore.Lib.dll"
#r "../../Informedica.ZIndex.Lib/bin/Debug/net10.0/Informedica.ZIndex.Lib.dll"
#r "../../Informedica.ZForm.Lib/bin/Debug/net10.0/Informedica.ZForm.Lib.dll"
#r "../../Informedica.GenSolver.Lib/bin/Debug/net10.0/Informedica.GenSolver.Lib.dll"
#r "../../Informedica.GenForm.Lib/bin/Debug/net10.0/Informedica.GenForm.Lib.dll"
#r "../../Informedica.GenOrder.Lib/bin/Debug/net10.0/Informedica.GenOrder.Lib.dll"
#r "../../Informedica.GenPRES.Shared/bin/Debug/net10.0/Informedica.GenPRES.Shared.dll"
// These can be loaded all at once.

#load "../Logging.fs"
#load "../ServerApi.fs"

fsi.AddPrinter<System.DateTime> _.ToShortDateString()


open System
open Informedica.Utils.Lib


let zindexPath = __SOURCE_DIRECTORY__ |> Path.combineWith "../../../"
Environment.CurrentDirectory <- zindexPath