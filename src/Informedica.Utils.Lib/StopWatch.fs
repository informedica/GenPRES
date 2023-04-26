namespace Informedica.Utils.Lib


module StopWatch =

    open System.Globalization
    open System.Diagnostics



    let clockFunc msg f =
        let stopwatch = Stopwatch.StartNew()
        let result = f()
        stopwatch.Stop()

        let ms = stopwatch.Elapsed.TotalMilliseconds.ToString("G", CultureInfo.InvariantCulture)
        let sw = Constants.HTMLCodeSymbols.TryFind "stopwatch" |> Option.defaultValue ""
        ConsoleWriter.writeInfoMessage $"{sw}  - {ms} ms: {msg}" true false

        result

