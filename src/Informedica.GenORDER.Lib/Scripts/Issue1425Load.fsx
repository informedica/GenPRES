// load.fsx with the GenORDER sources in place of the DLL, so every version is timed on the same
// compile. --define:VARIANT loads the equation changes of #1421 (DROPALL and NOCMPRATE the rejected
// versions), --define:RATEWINDOW the rate step of #1425; without them the sources as they are.

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
// the GenORDER sources, with the equation mapping and the increments of #1425 and #1421
#load "../Types.fs"
#load "../Utils.fs"
#load "../Logging.fs"
#load "../Exceptions.fs"
#load "../OrderVariable.fs"
#load "../Solver.fs"
#if VARIANT
#load "Issue1425EquationMapping.fsx"
#else
#load "../EquationMapping.fs"
#endif
#load "../Order.fs"
#if RATEWINDOW
#load "Issue1425Order.fsx"
#endif
#load "../OrderProcessor.fs"
#load "../Totals.fs"
#load "../Dtos.fs"
#load "../Medication.fs"



// load test scenarios
#load "../../../tests/Informedica.GenOrder.Tests/Scenarios.fs"


open System
open Informedica.Utils.Lib


let zindexPath = __SOURCE_DIRECTORY__ |> Path.combineWith "../../../"
Environment.CurrentDirectory <- zindexPath

printfn $"elapsed time: {stopWatch.ElapsedMilliseconds / 1000L}"
