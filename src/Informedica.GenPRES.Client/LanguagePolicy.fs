/// <summary>
/// Which UI language the client shows, and why: the url's <c>la</c> parameter and the User's
/// choice outrank the server default (<c>GENPRES_LANG</c>), which outranks the built-in
/// fallback. Pure F#, no React, so it runs under Expecto; App.fs applies it.
/// </summary>
module LanguagePolicy

open Shared.Localization


/// The current language and whether the url or the User chose it. Once chosen, the server
/// default no longer applies, whichever order the messages arrive in.
type Language =
    {
        Current: Locales
        Chosen: bool
    }


module Language =

    /// Until the server settings arrive, and when they never do: Dutch, the client's default.
    let fallback = Dutch


    /// At start: the url's language, else the fallback, not yet chosen.
    let initial (url: Locales option) =
        {
            Current = url |> Option.defaultValue fallback
            Chosen = url.IsSome
        }


    /// A navigation: only an `la` parameter changes the language; without one the current
    /// language stays, chosen or not.
    let onUrl (url: Locales option) (language: Language) =
        match url with
        | Some l ->
            {
                Current = l
                Chosen = true
            }
        | None -> language


    /// The User picks a language.
    let choose (l: Locales) (_: Language) =
        {
            Current = l
            Chosen = true
        }


    /// The server default arrives: it applies unless the url or the User already chose.
    let onServerDefault (l: Locales) (language: Language) =
        if language.Chosen then
            language
        else
            { language with Current = l }
