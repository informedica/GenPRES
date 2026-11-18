namespace Informedica.GenPRES.Client.Core.Models

/// Looks up translated terms and the flag of a language.
module LocalizationText =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Localization


    /// Returns the country flag emoji for the locale.
    // GB flag is used for English — acceptable for this European hospital application.
    let toFlag =
        function
        | English -> "\U0001F1EC\U0001F1E7"
        | Dutch -> "\U0001F1F3\U0001F1F1"
        | French -> "\U0001F1EB\U0001F1F7"
        | German -> "\U0001F1E9\U0001F1EA"
        | Spanish -> "\U0001F1EA\U0001F1F8"
        | Italian -> "\U0001F1EE\U0001F1F9"


    /// Looks up the translation of a term for a locale in the rows of the Localization sheet. Returns None
    /// and prints a message when the term or its translation is missing. The column of each language is
    /// fixed (English 1, Dutch 2, ...), so reordering the sheet columns gives wrong translations.
    let getTerm (terms: string[][]) locale term =
        let term = $"{term}".Trim()

        let indx =
            match locale with
            | English -> 1
            | Dutch -> 2
            | French -> 3
            | German -> 4
            | Spanish -> 5
            | Italian -> 6

        terms
        |> Array.tryFind (fun r -> r[0] = term)
        |> Option.map (fun r -> r[indx])
        |> Option.bind (fun s -> if s |> String.isNullOrWhiteSpace then None else Some s)
        |> fun r ->
            if r.IsNone then
                printfn $"cannot find term: {term}"

            r
