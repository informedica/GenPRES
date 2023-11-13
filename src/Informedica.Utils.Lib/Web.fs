namespace Informedica.Utils.Lib



module Web =


    module GoogleSheets =

        open System
        open System.IO
        open System.Net.Http


        /// Create a url to download a sheet from a google spreadsheet
        /// The id is the unique id of the spreadsheet and the sheet is the name of the sheet
        let createUrl sheet id =
            $"https://docs.google.com/spreadsheets/d/%s{id}/gviz/tq?tqx=out:csv&sheet=%s{sheet}"


        /// Instantiated http client
        let client = new HttpClient()


        /// Download a sheet from a google spreadsheet
        let download url =
            async {
                use! resp = client.GetAsync(Uri(url)) |> Async.AwaitTask
                use! stream = resp.Content.ReadAsStreamAsync() |> Async.AwaitTask
                use reader = new StreamReader(stream)
                return reader.ReadToEnd()
            }


        /// Get the data from a sheet in a google spreadsheet
        /// Return the data as a array of string arrays where
        /// each array represents a row in the sheet
        /// TODO: wrap in async
        let getDataFromSheet dataUrlId sheet =
            createUrl sheet dataUrlId
            |> download
            |> Async.RunSynchronously
            |> Csv.parseCSV

