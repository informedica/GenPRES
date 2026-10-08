/// <summary>
/// Decides which language the client shows: the url's <c>la</c> parameter and the user's choice
/// outrank the server default (<c>GENPRES_LANG</c>), which outranks the built-in fallback.
/// </summary>
module LanguagePolicy

open Shared.Localization


/// The current language, and whether the url or the user chose it. Once chosen, the server
/// default no longer applies, whatever order the messages arrive in.
type Language =
    {
        /// The language shown.
        Current: Locales
        /// Whether the url or the user chose it.
        Chosen: bool
    }


/// Functions over Language.
module Language =

    /// Dutch: the language until the server settings arrive, and if they never do.
    let fallback = Dutch


    /// The language at start: the url's, chosen, or else the fallback, not chosen.
    let initial (url: Locales option) =
        {
            Current = url |> Option.defaultValue fallback
            Chosen = url.IsSome
        }


    /// The language after a navigation: an la parameter changes it; without one it stays.
    let onUrl (url: Locales option) (language: Language) =
        match url with
        | Some l ->
            {
                Current = l
                Chosen = true
            }
        | None -> language


    /// The language the user picks.
    let choose (l: Locales) (_: Language) =
        {
            Current = l
            Chosen = true
        }


    /// The language once the server default arrives: the default, unless the url already chose.
    /// The user cannot choose before, since the start-up holds the application until the
    /// settings have landed.
    let onServerDefault (l: Locales) (language: Language) =
        if language.Chosen then
            language
        else
            { language with Current = l }
