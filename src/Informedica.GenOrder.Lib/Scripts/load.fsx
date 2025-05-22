
let stopWatch = System.Diagnostics.Stopwatch ()

stopWatch.Start ()

fsi.AddPrinter<System.DateTime> _.ToShortDateString()


#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: FParsec"
#r "nuget: Newtonsoft.Json"
#r "nuget: Aether"
#r "nuget: Markdig"
#r "nuget: ClosedXML"

#r "nuget: ConsoleTables, 2.4.2"


#r "../../Informedica.Utils.Lib/bin/Debug/net9.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net9.0/Informedica.GenUnits.Lib.dll"
#r "../../Informedica.GenCore.Lib/bin/Debug/net9.0/Informedica.GenCore.Lib.dll"
#r "../../Informedica.GenSolver.Lib/bin/Debug/net9.0/Informedica.GenSolver.Lib.dll"
#r "../../Informedica.GenForm.Lib/bin/Debug/net9.0/Informedica.GenForm.Lib.dll"
#r "../../Informedica.ZIndex.Lib/bin/Debug/net9.0/Informedica.ZIndex.Lib.dll"

// have to load the dll as Rider cannot quickly load source files anymore :-<
#r "../../Informedica.GenOrder.Lib/bin/Debug/net9.0/Informedica.GenOrder.Lib.dll"

// These can be loaded all at once.
// disabled for now as Rider cannot quickly load those files :-<
(*
#load "../Types.fs"
#load "../Utils.fs"
#load "../Logging.fs"
#load "../Exceptions.fs"
#load "../OrderVariable.fs"
#load "../Solver.fs"
#load "../Order.fs"
#load "../Totals.fs"
#load "../OrderLogger.fs"
#load "../DrugOrder.fs"
#load "../Patient.fs"
#load "../Api.fs"
*)


open System
open Informedica.Utils.Lib


let zindexPath = __SOURCE_DIRECTORY__ |> Path.combineWith "../../../"
Environment.CurrentDirectory <- zindexPath

printfn $"elapsed time: {stopWatch.ElapsedMilliseconds / 1000L}"