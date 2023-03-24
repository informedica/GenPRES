namespace Informedica.Utils.Lib


module StopWatch =

    open System.Diagnostics


    let clockFunc f =
        let stopwatch = Stopwatch.StartNew()
        let result = f()
        stopwatch.Stop()
        printfn $"Time taken to execute function: {stopwatch.Elapsed.TotalMilliseconds} ms"
        result

