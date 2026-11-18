namespace Informedica.GenForm.Lib


/// Fetches the Nederlands Kinder Formularium (NKF) medication index that Source.getLink needs for
/// an NKF link. The fetch lives here so Source, which DoseRule uses, stays pure.
module SourceLoader =

    open System
    open FSharp.Data
    open FSharp.Data.JsonExtensions


    /// The NKF medication index, as JSON.
    let nkfUrl = "https://www.kinderformularium.nl/geneesmiddelen.json"


    /// The fetch timeout, far below the 100 second HttpClient default, because a connection that hangs
    /// stalls the whole resource load for a link that is only an annotation.
    let nkfTimeout = TimeSpan.FromSeconds 10.


    // Making an HttpClient does no IO, so a top-level value is safe. One client avoids the socket
    // exhaustion of a client per call.
    let private client = new Net.Http.HttpClient(Timeout = nkfTimeout)


    /// Fetches the NKF medication index from the url, or returns an error with the reason. The url is a
    /// parameter so tests can make the fetch fail.
    let fetchNKFMedicationsFrom (url: string) : Result<Source.NKFMedication list, Message list> =
        // Several record types in this namespace have a Generic field, so the annotation picks the type.
        let medication id generic : Source.NKFMedication =
            {
                Id = id
                Generic = generic
            }

        try
            let res =
                client.GetStringAsync url
                |> Async.AwaitTask
                |> Async.RunSynchronously
                |> JsonValue.Parse

            [
                for v in res do
                    medication (v?id.AsString()) (v?generic_name.AsString().Trim().ToLower())
            ]
            |> List.distinct
            |> Ok
        with exn ->
            // Only the message reaches ResourceInfo, so it carries the reason. HttpClient wraps the
            // HttpRequestException in an AggregateException, so the message is of the base exception.
            Utils.Result.createError
                $"fetchNKFMedications: could not fetch %s{url}: %s{exn.GetBaseException().Message}"
                exn


    /// Fetches the NKF medication index from nkfUrl.
    let fetchNKFMedications () = fetchNKFMedicationsFrom nkfUrl
