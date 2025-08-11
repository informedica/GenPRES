

#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: FParsec"
#r "nuget: Newtonsoft.Json"
#r "nuget: Aether"
#r "nuget: Markdig"
#r "nuget: ClosedXML"
#r "nuget: FsToolkit.ErrorHandling"
#r "nuget: ConsoleTables"


#r "../../Informedica.Utils.Lib/bin/Debug/net9.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net9.0/Informedica.GenUnits.Lib.dll"
#r "../../Informedica.GenCore.Lib/bin/Debug/net9.0/Informedica.GenCore.Lib.dll"
#r "../../Informedica.ZIndex.Lib/bin/Debug/net9.0/Informedica.ZIndex.Lib.dll"
#r "../../Informedica.ZForm.Lib/bin/Debug/net9.0/Informedica.ZForm.Lib.dll"
#r "../../Informedica.GenSolver.Lib/bin/Debug/net9.0/Informedica.GenSolver.Lib.dll"
#r "../../Informedica.GenForm.Lib/bin/Debug/net9.0/Informedica.GenForm.Lib.dll"
#r "../../Informedica.GenOrder.Lib/bin/Debug/net9.0/Informedica.GenOrder.Lib.dll"

// These can be loaded all at once.

#load "../../Shared/Types.fs"
#load "../../Shared/Data.fs"
#load "../../Shared/Localization.fs"
#load "../../Shared/Models.fs"
#load "../../Shared/Api.fs"
#load "../ServerApi.fs"

fsi.AddPrinter<System.DateTime> _.ToShortDateString()


open System
open Informedica.Utils.Lib


let zindexPath = __SOURCE_DIRECTORY__ |> Path.combineWith "../../../"
Environment.CurrentDirectory <- zindexPath