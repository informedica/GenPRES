/// Decides what a change of the url does. A url with a patient, a medication or a launch starts
/// the patient, the workbench, the plan and the signing over, and leaves a launched Session, so
/// the two never mix; a url with only a page, a language or a disclaimer changes those alone.
module UrlPolicy


/// The url the app shows, and the newer url the question waits on.
[<RequireQualifiedAccess>]
type UrlState =
    /// The url the app shows.
    | Shown of string list
    /// The url the app shows, and the newer url with a patient, a medication or a launch that
    /// waits for the answer to whether the launched Session, or the new and changed orders, may
    /// go, as segments and as read when it came.
    | Asked of shown: string list * asked: string list * Url.UrlParts


/// Reading and changing the url the app shows.
module UrlState =

    /// The url the app shows.
    let shown url =
        match url with
        | UrlState.Shown sl
        | UrlState.Asked(sl, _, _) -> sl


    /// The url the question waits on, if it is open.
    let asked url =
        match url with
        | UrlState.Asked(_, sl, _) -> Some sl
        | _ -> None


    /// What the url the question waits on carries, if it is open.
    let askedUrl url =
        match url with
        | UrlState.Asked(_, _, parts) -> Some parts
        | _ -> None


    /// The question opened on this url, as segments and as read, over the url the app shows.
    let ask asked parts url = UrlState.Asked(shown url, asked, parts)


    /// The question closed; the url the app shows stays.
    let close url =
        match url with
        | UrlState.Asked(sl, _, _) -> UrlState.Shown sl
        | _ -> url


/// What a url change is, against the url the app shows.
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
    /// Ask first whether the launched Session, or the new and changed orders, may go.
    | Ask
    /// Start over on the url.
    | StartOver


/// What the url change is: unchanged when the url is the one shown, and otherwise by whether it
/// carries a patient, a medication or a launch. The page load applies its url itself, so the router's
/// first report, of that same url, is unchanged too.
let change url sl seeds =
    match url with
    | UrlState.Shown current
    | UrlState.Asked(current, _, _) when current = sl -> UrlChange.Unchanged
    | _ when seeds -> UrlChange.Seed
    | _ -> UrlChange.PageOnly


/// What to do with the url change. A signature under way puts any change back, without a
/// question: whether the plan was signed must be seen before leaving. A page alone is put back
/// while anything that holds the menu is out, so that no page is shown whose data is still
/// changing, and applied otherwise. A patient, a medication or a launch starts over, whatever
/// is out, after asking when it leaves a launched Session or there is work that is not signed.
let action change signingUnderWay out unsignedWork launched =
    match change with
    | UrlChange.Unchanged -> UrlAction.Ignore
    | _ when signingUnderWay -> UrlAction.PutBack
    | UrlChange.PageOnly when Busy.any out -> UrlAction.PutBack
    | UrlChange.PageOnly -> UrlAction.ApplyPage
    | UrlChange.Seed when launched || unsignedWork -> UrlAction.Ask
    | UrlChange.Seed -> UrlAction.StartOver
