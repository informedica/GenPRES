/// Decides how long a server error stays on the banner. An error is the answer to one kind of
/// request, so the next successful answer of that kind replaces it; an answer of another kind
/// says nothing about it and leaves it. An unreachable server is cleared only by a server
/// check that succeeds, and that check clears every error, since the server answers again.
module ServerErrorPolicy


/// The kind of request an error answered.
[<RequireQualifiedAccess>]
type ErrorSource =
    /// A command on the order plan.
    | OrderPlan
    /// Loading the formulary.
    | Formulary
    /// Loading the parenteralia.
    | Parenteralia
    /// Checking the interactions.
    | Interactions
    /// Logging in to the admin pages.
    | Login
    /// Listing the log files.
    | LogFiles
    /// Analysing one log file.
    | LogAnalysis
    /// Reloading the resources.
    | Reload
    /// The server check: the server did not answer at all.
    | Server


/// An error on the banner and the kind of request that raised it.
type ServerError =
    {
        /// The kind of request the error answered.
        Source: ErrorSource
        /// The text the banner shows.
        Message: string
    }


/// The error a failed request of source raises: for the server check, the sentence that it does
/// not answer; for any other source the first three errors, each cut at 200 characters.
let raised source (errs: string[]) =
    let cut (s: string) = if s.Length > 200 then s[..199] + "..." else s

    {
        Source = source
        Message =
            match source with
            | ErrorSource.Server -> "De server is niet bereikbaar. Controleer of de server is gestart."
            | _ ->
                let errs = errs |> Array.truncate 3 |> Array.map cut |> String.concat "; "
                $"Server fout: %s{errs}"
    }


/// The banner after a successful answer of source: gone when that source raised it, or when
/// the server check succeeded; kept otherwise.
let clearedBy source (error: ServerError option) =
    match error with
    | Some e when e.Source = source || source = ErrorSource.Server -> None
    | _ -> error
