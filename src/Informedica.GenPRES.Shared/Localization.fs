// Localization support for the GenPRES application.
//
// The client fetches the "Localization" sheet at startup and keeps it as a string[][] matrix,
// one row per term. getTerm reads a language from a fixed column, so the sheet's column order
// matters.
namespace Informedica.GenPRES.Shared


module Localization =


    /// Supported UI languages.  Add a case here when a new language is
    /// introduced, then update `toString`, `fromString`, `languages`, `getTerm`
    /// and the Localization spreadsheet.
    type Locales =
        | English
        | Dutch
        | French
        | German
        | Spanish
        | Italian
    //        | Chinees


    /// Returns a two-letter ISO 639-1 language code for the locale.
    let toShortCode =
        function
        | English -> "EN"
        | Dutch -> "NL"
        | French -> "FR"
        | German -> "DE"
        | Spanish -> "ES"
        | Italian -> "IT"


    /// Converts a `Locales` value to its human-readable display name as it
    /// appears in the "Localization" spreadsheet header row.
    let toString =
        function
        | English -> "English"
        | Dutch -> "Nederlands"
        | French -> "Français"
        | Spanish -> "Español"
        | German -> "Deutsch"
        | Italian -> "Italiano"
    //        | Chinees -> "中文"


    let languages = [| English; Dutch; French; German; Spanish; Italian |]


    /// <summary>
    /// Parses a language given as an ISO 639-1 code (<c>en</c>, <c>nl</c>, <c>fr</c>, <c>de</c>,
    /// <c>es</c>, <c>it</c>). Case and surrounding whitespace do not matter; anything else,
    /// including a display name and null, is <c>None</c>. One parser for the
    /// <c>GENPRES_LANG</c> setting and the <c>lan</c> url parameter.
    /// </summary>
    let tryParse (s: string) : Locales option =
        if isNull s then
            None
        else
            match s.Trim().ToLower() with
            | "en" -> Some English
            | "nl" -> Some Dutch
            | "fr" -> Some French
            | "de" -> Some German
            | "es" -> Some Spanish
            | "it" -> Some Italian
            | _ -> None
