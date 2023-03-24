//#I __SOURCE_DIRECTORY__

#load "load.fsx"

open System
open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open System.Globalization

let clockFunc f =
    let stopwatch = System.Diagnostics.Stopwatch.StartNew()
    let result = f()
    stopwatch.Stop()
    printfn "Time taken to execute function: %f ms" stopwatch.Elapsed.TotalMilliseconds
    result


Double.getPrecision 1 0.2516490855

0.2516490855.ToString("G", CultureInfo.InvariantCulture)
