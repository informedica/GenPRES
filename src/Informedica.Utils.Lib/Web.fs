namespace Informedica.Utils.Lib


module Web =


    module GoogleSheets =

        open System
        open System.Collections.Concurrent
        open System.Collections.Generic
        open System.Net.Http
        open System.Threading.Tasks
        open Informedica.Utils.Lib.BCL


        let private client = new HttpClient()


        /// The hint for a spreadsheet that cannot be downloaded because it is not shared.
        let sharingHint = "The spreadsheet is shared publicly (Anyone with the link can view)"


        /// Creates the url that downloads the whole spreadsheet with the given id as an xlsx workbook.
        let createWorkbookUrl id =
            $"https://docs.google.com/spreadsheets/d/%s{id}/export?format=xlsx"


        /// Downloads the bytes at the url. Returns an error when the response is not successful, for example
        /// 404 or 403, or when the network fails.
        let downloadResult url =
            async {
                try
                    use! resp = client.GetAsync(Uri(url)) |> Async.AwaitTask

                    if not resp.IsSuccessStatusCode then
                        return
                            Error
                                $"HTTP %i{int resp.StatusCode} (%A{resp.StatusCode}) downloading the workbook.\n\
                                  URL: %s{url}\n\
                                  Hint: check that:\n\
                                  - %s{sharingHint}\n\
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
                let! bytes = dataUrlId |> createWorkbookUrl |> downloadResult

                return
                    bytes
                    // Google answers a request for a private spreadsheet with its sign-in page.
                    |> Result.bind (
                        Xlsx.read
                        >> Result.mapError (fun err -> $"%s{err}\nHint: check that:\n- %s{sharingHint}")
                    )
            }


        // The workbooks read since the last clear, by spreadsheet id. Callers that ask at the same time share
        // one download.
        let private workbooks =
            ConcurrentDictionary<string, Lazy<Task<Result<Map<string, Xlsx.Sheet>, string>>>>()


        /// Forgets the workbooks read so far, so the next sheet read downloads the workbook again.
        let clearWorkbookCache () = workbooks.Clear()


        /// Returns the workbook of the spreadsheet with the given id, loaded with load on the first call after
        /// a clear. A failed load is not kept, so the next call tries again.
        let getWorkbookWith load dataUrlId =
            async {
                let entry = workbooks.GetOrAdd(dataUrlId, fun id -> lazy (load id |> Async.StartAsTask))

                let! result = entry.Value |> Async.AwaitTask

                if Result.isError result then
                    workbooks.TryRemove(KeyValuePair(dataUrlId, entry)) |> ignore

                return result
            }


        /// Removes every double quote from the cells of a sheet, as the CSV download did, because the sheet
        /// readers expect that.
        let sheetRows rows = rows |> Array.map (Array.map (String.replace "\"" ""))


        /// Returns a sheet of the workbook of the spreadsheet with the given id. Returns an error when the
        /// workbook cannot be loaded or has no sheet with that name.
        let getSheet dataUrlId name =
            async {
                let! workbook = getWorkbookWith downloadWorkbook dataUrlId

                return
                    workbook
                    |> Result.bind (fun sheets ->
                        match sheets |> Map.tryFind name with
                        | Some sheet -> Ok sheet
                        | None ->
                            let names = sheets |> Map.keys |> String.concat ", "
                            Error $"The workbook has no sheet '%s{name}'. The sheets are: %s{names}."
                    )
                    |> Result.mapError (fun err ->
                        $"Failed to load sheet '%s{name}' using URL ID '%s{dataUrlId}'.\n%s{err}"
                    )
            }


        /// Returns the warning for the sheet with the given name when it has hidden rows, or None when no row
        /// is hidden.
        let hiddenRowsWarning name (sheet: Xlsx.Sheet) =
            if sheet.HiddenRows = 0 then
                None
            else
                Some $"Sheet '%s{name}': %i{sheet.HiddenRows} rows are hidden, so those rows are not loaded."


        /// Returns the rows of a sheet as text, the header row first, waiting for the result. Returns an error
        /// when the workbook cannot be loaded or has no sheet with that name. Writes an error to the console, and
        /// a warning when the sheet has hidden rows.
        let getRowsFromSheetSync dataUrlId name =
            getSheet dataUrlId name
            |> Async.RunSynchronously
            |> function
                | Ok sheet ->
                    hiddenRowsWarning name sheet
                    |> Option.iter (fun msg -> ConsoleWriter.writeWarningMessage msg true false)

                    sheet.Rows |> sheetRows |> Ok
                | Error msg ->
                    ConsoleWriter.writeErrorMessage msg true false
                    msg |> Error
