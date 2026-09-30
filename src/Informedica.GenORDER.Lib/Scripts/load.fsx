#I __SOURCE_DIRECTORY__

let stopWatch = System.Diagnostics.Stopwatch()

stopWatch.Start()

fsi.AddPrinter<System.DateTime> _.ToShortDateString()


#load "../../../scripts/load-dependencies.fsx"


#r "../../Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.Agents.Lib/bin/Debug/net10.0/Informedica.Agents.Lib.dll"
#r "../../Informedica.Logging.Lib/bin/Debug/net10.0/Informedica.Logging.Lib.dll"
#r "../../Informedica.GenUNITS.Lib/bin/Debug/net10.0/Informedica.GenUNITS.Lib.dll"
#r "../../Informedica.GenCORE.Lib/bin/Debug/net10.0/Informedica.GenCORE.Lib.dll"
#r "../../Informedica.GenSOLVER.Lib/bin/Debug/net10.0/Informedica.GenSOLVER.Lib.dll"
#r "../../Informedica.GenFORM.Lib/bin/Debug/net10.0/Informedica.GenFORM.Lib.dll"
#r "../../Informedica.ZIndex.Lib/bin/Debug/net10.0/Informedica.ZIndex.Lib.dll"

// have to load the dll as Rider cannot quickly load source files anymore :-<
#r "../../Informedica.GenORDER.Lib/bin/Debug/net10.0/Informedica.GenORDER.Lib.dll"

// These can be loaded all at once.
// disabled for now as Rider cannot quickly load those files :-<
(*
#load "../Types.fs"
#load "../Utils.fs"
#load "../Logging.fs"
#load "../Exceptions.fs"
#load "../OrderVariable.fs"
#load "../Solver.fs"
#load "../EquationMapping.fs"
#load "../Order.fs"
#load "../OrderProcessor.fs"
#load "../Totals.fs"
#load "../Medication.fs"
#load "../Nutrition.fs"
#load "../Patient.fs"
#load "../OrderLogging.fs"
#load "../Formulary.fs"
#load "../OrderContext.fs"
*)


// load test scenarios
#load "../../../tests/Informedica.GenOrder.Tests/Scenarios.fs"


open System
open Informedica.Utils.Lib


let zindexPath = __SOURCE_DIRECTORY__ |> Path.combineWith "../../../"
Environment.CurrentDirectory <- zindexPath

printfn $"elapsed time: {stopWatch.ElapsedMilliseconds / 1000L}"
