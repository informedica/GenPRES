namespace Informedica.GenPRES.Client.Core.Policies

/// Decides what a url change does. A url with a patient, a medication or a launch starts the patient,
/// workbench, plan and signing over, and ends a launched Session, so the two never mix. A url with only
/// a page, a language or a disclaimer changes only those.
module UrlPolicy =

    open Informedica.GenPRES.Client.Core.Helpers


    /// The url the app shows, and a newer url that waits for the user's answer.
    [<RequireQualifiedAccess>]
    type UrlState =
        /// The url the app shows.
        | Shown of string list
        /// The url the app shows, and a newer url with a patient, a medication or a launch, as segments
        /// and as parsed parts. The newer url waits until the user confirms that the launched Session,
        /// or the new and changed orders, may be discarded.
        | Asked of shown: string list * asked: string list * Url.UrlParts


    /// Reading and changing the url the app shows.
    module UrlState =

        /// The url the app shows.
        let shown url =
            match url with
            | UrlState.Shown sl
            | UrlState.Asked(sl, _, _) -> sl


        /// The newer url that waits for an answer, if any.
        let asked url =
            match url with
            | UrlState.Asked(_, sl, _) -> Some sl
            | _ -> None


        /// The parsed parts of the newer url that waits for an answer, if any.
        let askedUrl url =
            match url with
            | UrlState.Asked(_, _, parts) -> Some parts
            | _ -> None


        /// Opens the question for this url, given as segments and parsed parts; the shown url stays.
        let ask asked parts url = UrlState.Asked(shown url, asked, parts)


        /// Closes the question; the shown url stays.
        let close url =
            match url with
            | UrlState.Asked(sl, _, _) -> UrlState.Shown sl
            | _ -> url


    /// What a url change is, compared to the url the app shows.
    [<RequireQualifiedAccess>]
    type UrlChange =
        /// The url the app already shows: a url put back, or nothing changed.
        | Unchanged
        /// Only the page, the language or the disclaimer.
        | PageOnly
        /// A patient, a medication or a launch.
        | Seed


    /// What the app does with a url change.
    [<RequireQualifiedAccess>]
    type UrlAction =
        /// Apply the page, the language and the disclaimer alone.
        | ApplyPage
        /// Do nothing.
        | Ignore
        /// Put the url the app shows back in the address bar.
        | PutBack
        /// Ask first whether the launched Session, or the new and changed orders, may be discarded.
        | Ask
        /// Start over on the url.
        | StartOver


    /// Whether a url carries a patient, a medication or a launch.
    let seeds (url: Url.UrlParts) =
        url.Patient.IsSome
        || url.Medication.IsSome
        || (
            match url.Launch with
            | Some(Url.LaunchUrl.Launch _) -> true
            | _ -> false
        )


    /// Classifies a url change: Unchanged when the url is the shown url, otherwise by whether it carries a
    /// patient, a medication or a launch. The page load applies its own url, so the first router report of
    /// that url is also Unchanged.
    let change url sl seeds =
        match url with
        | UrlState.Shown current
        | UrlState.Asked(current, _, _) when current = sl -> UrlChange.Unchanged
        | _ when seeds -> UrlChange.Seed
        | _ -> UrlChange.PageOnly


    /// Decides what to do with a url change. During a signature any change is put back without a question,
    /// because the user must see whether the plan was signed. A page-only change is put back while a request
    /// that disables the menu is pending, so no page shows data that is still changing; otherwise it is
    /// applied. A patient, a medication or a launch starts over, even with pending requests, after asking
    /// when it ends a launched Session or there is unsigned work.
    let action change signingUnderWay anyOut unsignedWork launched =
        match change with
        | UrlChange.Unchanged -> UrlAction.Ignore
        | _ when signingUnderWay -> UrlAction.PutBack
        | UrlChange.PageOnly when anyOut -> UrlAction.PutBack
        | UrlChange.PageOnly -> UrlAction.ApplyPage
        | UrlChange.Seed when launched || unsignedWork -> UrlAction.Ask
        | UrlChange.Seed -> UrlAction.StartOver
