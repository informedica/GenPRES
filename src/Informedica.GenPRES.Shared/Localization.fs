// The languages of the application. The client reads the translations from the Localization sheet,
// one row per term and one fixed column per language, so reordering the sheet columns gives wrong translations.
namespace Informedica.GenPRES.Shared


module Localization =


    /// The languages the application supports. A new language also needs a case in toShortCode, toString,
    /// languages, tryParse, the client lookup of terms and a column in the Localization sheet.
    type Locales =
        | English
        | Dutch
        | French
        | German
        | Spanish
        | Italian


    /// Returns a two-letter ISO 639-1 language code for the locale.
    let toShortCode =
        function
        | English -> "EN"
        | Dutch -> "NL"
        | French -> "FR"
        | German -> "DE"
        | Spanish -> "ES"
        | Italian -> "IT"


    /// Returns the name of the language in that language, as in the header row of the Localization sheet.
    let toString =
        function
        | English -> "English"
        | Dutch -> "Nederlands"
        | French -> "Français"
        | Spanish -> "Español"
        | German -> "Deutsch"
        | Italian -> "Italiano"


    let languages = [| English; Dutch; French; German; Spanish; Italian |]


    /// Parses a two-letter language code (en, nl, fr, de, es or it), ignoring case and surrounding white space.
    /// Returns None for null and any other text, a language name included. Parses both the GENPRES_LANG setting
    /// and the lan url parameter.
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
