namespace Informedica.GenPRES.Client.Core.Policies

/// Decides how long a server error stays on the banner. An error is the answer to one kind of
/// request, so the next successful answer of that kind replaces it; an answer of another kind
/// says nothing about it and leaves it. An unreachable server is cleared only by a server
/// check that succeeds, and that check clears every error, since the server answers again.
module ServerErrorPolicy =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Helpers


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
        /// Counting the combined totals of the nutrition page.
        | Totals
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
            /// The first three errors the server gave, each cut at 200 characters.
            Errors: string[]
        }


    /// The error a failed request of source raises, with the first three errors, each cut at 200
    /// characters.
    let raised source (errs: string[]) =
        let cut (s: string) = if s.Length > 200 then s[..199] + "..." else s

        {
            Source = source
            Errors = errs |> Array.truncate 3 |> Array.map cut
        }


    /// The banner after a successful answer of source: gone when that source raised it, or when
    /// the server check succeeded; kept otherwise.
    let clearedBy source (error: ServerError option) =
        match error with
        | Some e when e.Source = source || source = ErrorSource.Server -> None
        | _ -> error


    /// The English of the banner terms, used when the sheet has no row for a term or has not
    /// loaded. Any other term shows its name.
    let english term =
        match term with
        | Terms.``Server Error Title`` -> "Server problem"
        | Terms.``Server Error Unreachable`` -> "The server cannot be reached. Check that the server has been started."
        | Terms.``Server Error Text`` -> "Server error: {0}"
        | _ -> $"{term}"


    /// The sentence of the banner: for the server check, that the server does not answer; for any
    /// other source, the errors the server gave.
    let message tr error =
        match error.Source with
        | ErrorSource.Server -> tr Terms.``Server Error Unreachable``
        | _ ->
            tr Terms.``Server Error Text``
            |> TermText.fill [ error.Errors |> String.concat "; " ]
