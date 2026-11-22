// Issues #1957 and #1958, step 2 of docs/implementation-plans/1957-1958-read-the-sheets-from-the-workbook.md:
// read the Google sheets from one xlsx download of the whole workbook instead of one gviz CSV download per
// sheet. Step 1 migrated the Xlsx module to src/Informedica.Utils.Lib/Xlsx.fs.
//
// The gviz download guesses the header rows, so a sheet without a dummy first row loses its
// header and its first rows (#1957). It also gives each column one type, so a number stored as
// text arrives as an empty cell (#1958). The xlsx export keeps every cell with its own type and
// marks the rows a filter hides, so the filtered rows can still be left out.
//
// Run: dotnet fsi Issue1957SheetWorkbook.fsx            the unit tests
//      dotnet fsi Issue1957SheetWorkbook.fsx --live     also compare with the gviz download of
//                                                       the sheets the code reads (GENPRES_URL_ID)

#I __SOURCE_DIRECTORY__

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

#load "load.fsx"
#r "nuget: Expecto"

open System
open Informedica.Utils.Lib


module Web =


    module GoogleSheets =

        open System.Collections.Concurrent
        open System.Net.Http
        open Informedica.Utils.Lib.Web.GoogleSheets


        let private client = new HttpClient()


        /// Creates the url that downloads the whole spreadsheet with the given id as an xlsx workbook.
        let createWorkbookUrl id =
            $"https://docs.google.com/spreadsheets/d/%s{id}/export?format=xlsx"


        /// Downloads the bytes at the url. Returns an error when the response is not successful or
        /// the network fails.
        let downloadBytesResult url : Async<Result<byte[], string>> =
            async {
                try
                    use! resp = client.GetAsync(Uri(url)) |> Async.AwaitTask

                    if not resp.IsSuccessStatusCode then
                        return
                            Error
                                $"HTTP %i{int resp.StatusCode} (%A{resp.StatusCode}) downloading the workbook.\n\
                                  URL: %s{url}\n\
                                  Hint: check that:\n\
                                  - The spreadsheet is shared publicly (Anyone with the link can view)\n\
                                  - The GENPRES_URL_ID environment variable is set correctly\n\
                                  - The URL ID has not expired or been rotated"
                    else
                        let! bytes = resp.Content.ReadAsByteArrayAsync() |> Async.AwaitTask
                        return Ok bytes
                with ex ->
                    return
                        Error
                            $"Network error downloading the workbook from %s{url}.\n\
                              Exception: %s{ex.GetType().Name}: %s{ex.Message}"
            }


        /// Downloads and reads the workbook of the spreadsheet with the given id.
        let downloadWorkbook dataUrlId =
            async {
                let! bytes = dataUrlId |> createWorkbookUrl |> downloadBytesResult

                return
                    bytes
                    |> Result.bind (fun bs ->
                        bs
                        |> Xlsx.read
                        // Google answers a request for a private spreadsheet with its sign-in page.
                        |> Result.mapError (fun err ->
                            $"%s{err}\nHint: check that the spreadsheet is shared publicly (Anyone with the link can view)."
                        )
                    )
            }


        // The workbooks read since the last clear, by spreadsheet id. Callers that ask at the same
        // time share one download.
        let private workbooks =
            ConcurrentDictionary<string, Lazy<Threading.Tasks.Task<Result<Map<string, string[][]>, string>>>>()


        /// Forgets the workbooks read so far, so the next sheet read downloads the workbook again.
        let clearWorkbookCache () = workbooks.Clear()


        /// Returns the workbook of the spreadsheet with the given id, loaded with load on the first
        /// call after a clear. A failed load is not kept, so the next call tries again.
        let getWorkbookWith load dataUrlId =
            async {
                let entry =
                    workbooks.GetOrAdd(dataUrlId, fun id -> lazy (load id |> Async.StartAsTask))

                let! result = entry.Value |> Async.AwaitTask

                if Result.isError result then
                    workbooks.TryRemove(Collections.Generic.KeyValuePair(dataUrlId, entry)) |> ignore

                return result
            }


        /// Returns the rows of a sheet with every double quote removed from the cells.
        let sheetRows (rows: string[][]) =
            // The CSV download removed every double quote, and the sheets are read that way.
            rows |> Array.map (Array.map _.Replace("\"", ""))


        /// Returns the rows of a sheet as text, the header row first. Returns an error when the
        /// workbook cannot be loaded or has no sheet with that name.
        let getCsvDataFromSheet dataUrlId sheet : Async<Result<string[][], string>> =
            async {
                let! workbook = getWorkbookWith downloadWorkbook dataUrlId

                return
                    workbook
                    |> Result.bind (fun sheets ->
                        match sheets |> Map.tryFind sheet with
                        | Some s -> s |> sheetRows |> Ok
                        | None ->
                            let names = sheets |> Map.keys |> String.concat ", "
                            Error $"The workbook has no sheet '%s{sheet}'. The sheets are: %s{names}."
                    )
                    |> Result.mapError (fun err -> $"Failed to load sheet '%s{sheet}' using URL ID '%s{dataUrlId}'.\n%s{err}")
            }


        /// Returns the rows of a sheet as getCsvDataFromSheet does, waiting for the result, and
        /// writes the error to the console.
        let getCsvDataFromSheetSync dataUrlId sheet =
            getCsvDataFromSheet dataUrlId sheet
            |> Async.RunSynchronously
            |> function
                | Ok result -> result |> Ok
                | Error msg ->
                    ConsoleWriter.writeErrorMessage msg true false
                    msg |> Error


module Tests =

    open Expecto
    open Expecto.Flip


    let tests =
        testList "Issue 1957 sheet workbook" [

            testList "Web.GoogleSheets.sheetRows" [
                test "the sheet rows have no double quotes" {
                    [| [| "Name" |]; [| "BOTULINE A TOXINE \"MERZ\"" |] |]
                    |> Web.GoogleSheets.sheetRows
                    |> Expect.equal "quotes removed" [| [| "Name" |]; [| "BOTULINE A TOXINE MERZ" |] |]
                }
            ]

            testSequenced <| testList "Web.GoogleSheets.getWorkbookWith" [
                let load (calls: int ref) result =
                    fun (_: string) ->
                        async {
                            calls.Value <- calls.Value + 1
                            return result
                        }

                test "the workbook is loaded once until the cache is cleared" {
                    Web.GoogleSheets.clearWorkbookCache ()
                    let calls = ref 0
                    let load = load calls (Ok Map.empty)

                    Web.GoogleSheets.getWorkbookWith load "id" |> Async.RunSynchronously |> ignore
                    Web.GoogleSheets.getWorkbookWith load "id" |> Async.RunSynchronously |> ignore
                    calls.Value |> Expect.equal "one load" 1

                    Web.GoogleSheets.clearWorkbookCache ()
                    Web.GoogleSheets.getWorkbookWith load "id" |> Async.RunSynchronously |> ignore
                    calls.Value |> Expect.equal "a second load after the clear" 2
                }

                test "each spreadsheet id has its own workbook" {
                    Web.GoogleSheets.clearWorkbookCache ()
                    let calls = ref 0
                    let load = load calls (Ok Map.empty)

                    Web.GoogleSheets.getWorkbookWith load "id1" |> Async.RunSynchronously |> ignore
                    Web.GoogleSheets.getWorkbookWith load "id2" |> Async.RunSynchronously |> ignore
                    calls.Value |> Expect.equal "two loads" 2
                }

                test "a failed load is not kept" {
                    Web.GoogleSheets.clearWorkbookCache ()
                    let calls = ref 0
                    let load = load calls (Error "network down")

                    Web.GoogleSheets.getWorkbookWith load "id" |> Async.RunSynchronously |> Expect.isError "failed"
                    Web.GoogleSheets.getWorkbookWith load "id" |> Async.RunSynchronously |> ignore
                    calls.Value |> Expect.equal "the load is tried again" 2
                }

                test "callers at the same time share one load" {
                    Web.GoogleSheets.clearWorkbookCache ()
                    let calls = ref 0

                    let slow (_: string) =
                        async {
                            Threading.Interlocked.Increment calls |> ignore
                            do! Async.Sleep 200
                            return Ok Map.empty
                        }

                    List.replicate 10 (Web.GoogleSheets.getWorkbookWith slow "id")
                    |> Async.Parallel
                    |> Async.RunSynchronously
                    |> ignore

                    calls.Value |> Expect.equal "one load" 1
                }
            ]
        ]


open Expecto

let exitCode = runTestsWithCLIArgs [] [||] Tests.tests


/// Compares the new workbook read with the gviz download (with headers=1, so the header guess
/// does not hide the differences) for the sheets the code reads, and prints every difference.
module Live =

    let sheetsReadByCode =
        [
            "Formulary"
            "Reconstitution"
            "SolutionRules"
            "DoseRules"
            "RenalRules"
            "Units"
            "Routes"
            "ValidForms"
            "FormRoute"
            "Totals"
            "RuleSets"
            "LinkRules"
            "Kinderformularium"
            "Frequencies"
            "norm_weight"
            "norm_height"
            "norm_weight_neo"
            "norm_height_neo"
            "calc_weight"
            "calc_height"
            "egfr_bands"
        ]


    let gviz dataUrlId sheet =
        $"https://docs.google.com/spreadsheets/d/%s{dataUrlId}/gviz/tq?tqx=out:csv&headers=1&sheet=%s{sheet}"
        |> Informedica.Utils.Lib.Web.GoogleSheets.downloadResult
        |> Async.RunSynchronously
        |> Result.map Csv.parseCSV


    let compare dataUrlId =
        Web.GoogleSheets.clearWorkbookCache ()
        let sw = Diagnostics.Stopwatch.StartNew()

        match Web.GoogleSheets.getWorkbookWith Web.GoogleSheets.downloadWorkbook dataUrlId |> Async.RunSynchronously with
        | Error e -> printfn $"workbook failed: %s{e}"
        | Ok sheets ->
            printfn $"workbook downloaded and read in %i{sw.ElapsedMilliseconds} ms"

            for name in sheetsReadByCode do
                match sheets |> Map.tryFind name, gviz dataUrlId name with
                | None, _ -> printfn $"%s{name}: not in the workbook"
                | _, Error e -> printfn $"%s{name}: gviz failed: %s{e}"
                | Some sheet, Ok old ->
                    let rows = Web.GoogleSheets.sheetRows sheet
                    let cell (rows: string[][]) r c =
                        if r < rows.Length && c < rows[r].Length then rows[r][c] else ""

                    let width = max (old |> Array.map Array.length |> Array.fold max 0) (rows |> Array.map Array.length |> Array.fold max 0)

                    let diffs =
                        [
                            for r in 0 .. (max old.Length rows.Length) - 1 do
                                for c in 0 .. width - 1 do
                                    let a, b = cell rows r c, cell old r c
                                    if a <> b then (r + 1, cell rows 0 c, a, b)
                        ]

                    printfn $"%s{name}: rows %i{rows.Length} (gviz %i{old.Length}), differences %i{diffs.Length}"

                    for (r, col, a, b) in diffs |> List.truncate 50 do
                        printfn $"    line %i{r} %s{col}: workbook '%s{a}', gviz '%s{b}'"


if fsi.CommandLineArgs |> Array.contains "--live" then
    Env.loadDotEnv () |> ignore

    match Env.getItem "GENPRES_URL_ID" with
    | Some id -> Live.compare id
    | None -> printfn "GENPRES_URL_ID is not set"
