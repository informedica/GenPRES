namespace Informedica.Utils.Lib


module Constants =

    open System.Net

    let HTMLCodeSymbols =
        [
            "check", "&#9989;"
            "crossMark", "&#10060;"
            "rocket", "&#128640;"
            "stopwatch", "&#9201;"
            "hourglass", "&#8987;"
            "clock", "&#128368;"
            "floppyDisk", "&#128190;"
            "error", "&#10060;"
            "info", "&#8505;"
            "warning", "&#9888;"
            "question", "&#8265;"
        ]
        |> List.map (fun (k, v) -> k, v |> WebUtility.HtmlDecode)
        |> Map.ofList



    module Tests =

        /// print all HTML code symbols
        let printAllSymbols () =
            HTMLCodeSymbols
            |> Map.iter (fun k v -> printfn $"%s{k}: %s{v}")
