(*
    DownloadFixtures.fsx
    --------------------
    Refresh the committed MinMax golden-result fixtures used by the
    MinMaxCalculatorTests "Scenarios" tests in ../Tests.fs.

    Source of truth is the Google sheet identified by `urlId` below. Each sheet
    (mult/div/add/sub) holds the expected scenario string in column 0, with a
    header row. We strip the header and write one scenario per line to
    ../fixtures/<sheet>.txt so the tests can run hermetically (offline).

    The tests read these files via System.AppContext.BaseDirectory/fixtures, so
    after refreshing just rebuild the test project (the .fsproj copies them to
    the output dir).

    Run from this directory:
        cd tests/Informedica.GenSOLVER.Tests/Scripts
        dotnet fsi DownloadFixtures.fsx

    Requires Informedica.Utils.Lib to be built (dotnet run build).
*)

#I __SOURCE_DIRECTORY__
#r "../../../src/Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"

open System.IO
open Informedica.Utils.Lib.Web

// The MinMax scenarios sheet. This id is intentionally NOT referenced by the
// tests anymore — it lives only here, in the fixture refresh tool.
let urlId = "171G1GiUuuOjPvfLOFiuuQtq44LjFnmxyoRb1IIblc2A"

let outDir = Path.Combine(__SOURCE_DIRECTORY__, "..", "fixtures")

Directory.CreateDirectory outDir |> ignore

for sheet in [ "mult"; "div"; "add"; "sub" ] do
    match GoogleSheets.getRowsFromSheetSync urlId sheet with
    | Ok rows ->
        let col0 =
            rows
            |> Array.skip 1 // header
            |> Array.map (fun row -> row[0])

        File.WriteAllLines(Path.Combine(outDir, $"%s{sheet}.txt"), col0)
        printfn "%s: wrote %i rows" sheet col0.Length
    | Error e -> printfn "%s: ERROR %A" sheet e
