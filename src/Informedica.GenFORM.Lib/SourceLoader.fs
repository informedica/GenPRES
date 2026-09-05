namespace Informedica.GenForm.Lib


/// <summary>
/// Orchestration / loading layer for external formulary sources. Holds the IO that
/// the pure <c>Source</c> module must not: fetching the Nederlands Kinder Formularium
/// (NKF) medication index that <c>Source.getLink</c> needs to build an NKF link.
/// </summary>
/// <remarks>
/// Split out for the same reason as <c>DoseRuleLoader</c>: <c>Source</c> is a pure leaf
/// consumed by <c>DoseRule</c>, so it cannot hold a network call. This module is the
/// resources-side seam, composed by <c>Resources.Keys.nkfLinkProvider</c>, which decides
/// what a failed fetch means. See issue #529.
/// </remarks>
module SourceLoader =

    open System
    open FSharp.Data
    open FSharp.Data.JsonExtensions


    /// The NKF medication index, as JSON.
    let nkfUrl = "https://www.kinderformularium.nl/geneesmiddelen.json"


    /// Capped well below HttpClient's 100 second default: the formulary link is
    /// decoration, and a hung connection stalls a resource load just as a refused one does.
    let nkfTimeout = TimeSpan.FromSeconds 10.


    // Constructing an HttpClient performs no IO, so this is safe as a top-level value;
    // one instance avoids the socket exhaustion of a client per call.
    let private client = new Net.Http.HttpClient(Timeout = nkfTimeout)


    /// <summary>
    /// Fetch the NKF medication index from <paramref name="url"/>.
    /// </summary>
    /// <remarks>
    /// An IO leaf returning a <c>Result</c>: what a failure means is the registry's
    /// decision, not this function's. See <c>Resources.Keys.nkfLinkProvider</c>. The url is
    /// a parameter so the failure path can be exercised without taking the site down.
    /// </remarks>
    let fetchNKFMedicationsFrom (url: string) : Result<Source.NKFMedication list, Message list> =
        // `Generic` is a field name several record types in this namespace share, so the
        // labels are resolved by this annotated constructor rather than inline.
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
            // The message is what reaches ResourceInfo (the exception itself is dropped
            // there), so carry the reason. HttpClient failures arrive wrapped — an
            // AggregateException around the HttpRequestException — hence the base exception.
            Utils.Result.createError
                $"fetchNKFMedications: could not fetch %s{url}: %s{exn.GetBaseException().Message}"
                exn


    /// Fetch the NKF medication index from <c>nkfUrl</c>.
    let fetchNKFMedications () = fetchNKFMedicationsFrom nkfUrl
