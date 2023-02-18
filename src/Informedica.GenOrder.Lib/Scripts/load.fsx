
#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: FParsec"
#r "nuget: Newtonsoft.Json"
#r "nuget: Aether"
#r "nuget: Markdig"
#r "nuget: ClosedXML"


#r "../../Informedica.Utils.Lib/bin/Debug/net6.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net6.0/Informedica.GenUnits.Lib.dll"
#r "../../Informedica.GenCore.Lib/bin/Debug/net6.0/Informedica.GenCore.Lib.dll"
#r "../../Informedica.GenSolver.Lib/bin/Debug/net6.0/Informedica.GenSolver.Lib.dll"
#r "../../Informedica.GenForm.Lib/bin/Debug/net6.0/Informedica.GenForm.Lib.dll"
#r "../../Informedica.ZIndex.Lib/bin/Debug/net6.0/Informedica.ZIndex.Lib.dll"

// These can be loaded all at once.

#load "../Types.fs"
#load "../Utils.fs"
#load "../Logging.fs"
#load "../Exceptions.fs"
#load "../WrappedString.fs"
#load "../ValueUnit.fs"
#load "../Variable.fs"
#load "../OrderVariable.fs"
#load "../Solver.fs"
#load "../Order.fs"
#load "../OrderLogger.fs"
#load "../DrugOrder.fs"
#load "../Patient.fs"
#load "../Api.fs"


fsi.AddPrinter<System.DateTime> (fun dt -> dt.ToShortDateString())


open System
open Informedica.Utils.Lib


let zindexPath = __SOURCE_DIRECTORY__ |> Path.combineWith "../../../"
Environment.CurrentDirectory <- zindexPath

