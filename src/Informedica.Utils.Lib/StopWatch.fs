namespace Informedica.Utils.Lib


module StopWatch =

    open System.Globalization
    open System.Diagnostics



    /// Measures the time it takes to execute a function
    /// Prints the result to the console with the given message and the time in milliseconds
    let clockFunc msg f =
        let stopwatch = Stopwatch.StartNew()
        let result = f()
        stopwatch.Stop()

        let ms = stopwatch.Elapsed.TotalMilliseconds.ToString("G", CultureInfo.InvariantCulture)
        let sw = Constants.HTMLCodeSymbols.TryFind "stopwatch" |> Option.defaultValue ""
        ConsoleWriter.writeInfoMessage $"%s{sw}  - %s{ms} ms: %s{msg}" true false

        result

