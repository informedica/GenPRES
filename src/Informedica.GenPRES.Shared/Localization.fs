// Localization support for the GenPRES application.
//
// The current implementation fetches a "Localization" sheet from Google Sheets
// at startup and stores translations as a `string[][]` matrix.  The column
// indices that map to each language are hardcoded in `getTerm`, which means
// **reordering columns in the spreadsheet silently breaks all translations**.
//
// An improved, more idiomatic approach is the `TranslationMap` API further down in this file
// (`parseCSV`, `getTermFromMap`, `mergeTranslations`), not yet used by the client:
//   - Parse the CSV by column *headers* (language display names) so the
//     implementation is robust to spreadsheet column reordering.
//   - Store translations as `TranslationMap` (`Map<string, Map<Locales, string>>`)
//     instead of `string[][]` — no magic column indices.
//   - `tryLocaleFromString` returns `Option` instead of throwing.
//   - Static fallback translations embedded in the binary so the UI works
//     without a network connection.
//   - `mergeTranslations` overlays remote sheet data over the static fallback
//     so a partial sheet load degrades gracefully.
namespace Shared


/// Compile-time-safe enumeration of all localizable UI strings.
/// Add a new case here whenever a new UI label is introduced, and update the
/// Localization sheet accordingly. New cases are drafted script-first in
/// `Scripts/Localization.fsx` (the script-only policy), which also prints the sheet rows.
type Terms =
    | ``Patient enter patient data``
    | ``Patient Age``
    | ``Patient GA Age``
    | ``Patient Age year``
    | ``Patient Age years``
    | ``Patient Age month``
    | ``Patient Age months``
    | ``Patient Age week``
    | ``Patient Age weeks``
    | ``Patient Age day``
    | ``Patient Age days``
    | ``Patient Estimated``
    | ``Patient Years``
    | ``Patient Months``
    | ``Patient Weeks``
    | ``Patient Days``
    | ``Patient Weight``
    | ``Patient Length``
    | ``Patient remove patient data``
    | ``Emergency List``
    | ``Emergency List show when patient data``
    | ``Emergency List Catagory``
    | ``Emergency List Intervention``
    | ``Emergency List Calculated``
    | ``Emergency List Preparation``
    | ``Emergency List Advice``
    | ``Continuous Medication List``
    | ``Continuous Medication List show when patient data``
    | ``Continuous Medication Catagory``
    | ``Continuous Medication Medication``
    | ``Continuous Medication Quantity``
    | ``Continuous Medication Solution``
    | ``Continuous Medication Dose``
    | ``Continuous Medication Advice``
    | ``Prescribe``
    | ``Prescribe Scenarios``
    | ``Prescribe Indications``
    | ``Prescribe Medications``
    | ``Prescribe Routes``
    | ``Prescribe Prescription``
    | ``Prescribe Preparation``
    | ``Prescribe Administration``
    | ``Order``
    | ``Order Frequency``
    | ``Order Dose``
    | ``Order Adjusted dose``
    | ``Order Quantity``
    | ``Order Concentration``
    | ``Order Drip rate``
    | ``Order Administration time``
    | ``Nutrition``
    | ``Order Plan``
    | ``Formulary``
    | ``Formulary Medications``
    | ``Formulary Indications``
    | ``Formulary Routes``
    | ``Formulary Patients``
    | ``Parenteralia``
    | ``Interactions``
    | ``Interactions Drug 1``
    | ``Interactions Drug 2``
    | ``Interactions Class``
    | ``Interactions None Found``
    | ``Delete``
    | ``Edit``
    | ``Ok ``
    | ``Sort By``
    | ``Disclaimer``
    | ``Disclaimer text``
    | ``Disclaimer accept``
    | Settings
    | ``Reload resources``
    | ``Enter password``
    | Password
    | ``Invalid password``
    | Cancel
    | Confirm
    // Shared terms (used across multiple views)
    | ``Dose Types``
    | ``Dose Type``
    | Route
    | Indication
    | Composition
    | Form
    | ``Pharmaceutical Form``
    | Diluent
    | Components
    // Patient-specific terms
    | ``Patient Male``
    | ``Patient Female``
    | ``Patient Unknown Gender``
    | ``Patient Gender``
    | ``Patient Access``
    | ``Patient Enteral Tube``
    | ``Patient Renal Function``
    // Shared UI terms
    | Print
    | ``Not Configured``
    // Nutrition-specific terms
    | ``Nutrition Parenteral``
    | ``Nutrition Parenteral Section``
    | ``Nutrition TPN``
    | ``Nutrition Enteral Feeding``
    | ``Nutrition Enteral Supplement``
    | ``Nutrition Add Supplement``
    | ``Nutrition Lipids``
    | ``Nutrition Electrolytes Glucose``
    | ``Nutrition Remove Enteral Title``
    | ``Nutrition Remove Enteral Text``
    // Interactions
    | ``Interactions Medication``
    // Session (plan 409): the gate and the session menu
    | ``Session Gate Opening``
    | ``Session Gate Opening Text``
    | ``Session Gate Resuming``
    | ``Session Gate Resuming Text``
    | ``Session Gate Unreachable``
    | ``Session Gate Unreachable Text``
    | ``Session Gate Refused``
    | ``Session Gate Try Again Or Relaunch``
    | ``Session Relaunch``
    | ``Session Retry``
    | ``Session Refusal Expired``
    | ``Session Refusal Spent``
    | ``Session Refusal Invalid``
    | ``Session Refusal No Browser Identity``
    | ``Session Refusal No Role``
    | ``Session Refusal Wrong Patient``
    | ``Session Refusal Enrolment``
    | ``Session Try Again``
    | ``Session Continue Without Launch``
    | ``Session Close``
    | ``Session Role Prescriber``
    | ``Session Role Reader``
    // the gate after the server ended the Session (Rule 11)
    | ``Session Gate Ended``
    | ``Session Ending Superseded``
    // Rule 28 (UC-3, plan 622): the Session ended at the third wrong PIN
    | ``Session Ending Pin Limit``
    // the enrolment form (UC-2, plan 615): title, body with {0} the name and {1} the hinted
    // mail address, the three field labels, the button, and one sentence per refusal
    | ``Session Gate Enrolment``
    | ``Session Gate Enrolment Text``
    | ``Session Enrolment Code``
    | ``Session Enrolment Pin``
    | ``Session Enrolment Pin Repeat``
    | ``Session Enrolment Submit``
    | ``Session Enrolment Code Format``
    | ``Session Enrolment Pin Format``
    | ``Session Enrolment Pins Differ``
    | ``Session Enrolment Wrong Code``
    | ``Session Enrolment Code Void``
    | ``Session Enrolment Expired``
    // signing (UC-3, plan 622): the button, the dialog, the data notice (Rule 44), the signed
    // sentence with {0} the version and {1} the signer, and one sentence per refusal
    | ``Signing Sign``
    | ``Signing Dialog Title``
    | ``Signing Dialog Text``
    | ``Signing Pin``
    | ``Signing Cancel``
    | ``Signing Proceed``
    | ``Signing Signed``
    | ``Signing Data Changed``
    | ``Signing Data Unverified``
    | ``Signing Refusal No Session``
    | ``Signing Refusal No Patient``
    | ``Signing Refusal Not Prescriber``
    | ``Signing Refusal Blocked``
    | ``Signing Refusal Stale Token``
    | ``Signing Refusal Challenge Mismatch``
    | ``Signing Refusal Challenge Expired``
    | ``Signing Refusal Pin Wrong``
    | ``Signing Refusal Pin Limit``
    | ``Signing Refusal Locked``
    | ``Signing Send Failed``
    // Rules 21, 22 (plan 635): the record moved on, told once per version; the button that
    // takes the version up (UC-4 step 4); what is told once it is open
    | ``Session Newer Version``
    | ``Session Open Newest``
    | ``Session Version Opened``


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


    /// Converts a display name string to a `Locales` value.
    ///
    /// ⚠️  This function **throws** for unknown input.  Consider using
    /// `tryFromString` below, which returns `Option<Locales>` instead.
    let fromString (s: string) =
        let s = s.Trim().ToLower()

        match s with
        | _ when s = "english" -> English
        | _ when s = "nederlands" -> Dutch
        | _ when s = "français" -> French
        | _ when s = "español" -> Spanish
        | _ when s = "deutsch" -> German
        | _ when s = "italiano" -> Italian
        //        | _ when s = "中文" -> Chinees
        | _ -> raise (System.FormatException $"{s} is not a known language")


    let languages = [| English; Dutch; French; German; Spanish; Italian |]


    /// Looks up a translated string for `term` in `locale` from a `string[][]`
    /// matrix produced by `Csv.parseCSV` on the "Localization" Google Sheet.
    ///
    /// ⚠️  Column positions are **hardcoded** (English = 1, Dutch = 2, …).
    /// Reordering columns in the spreadsheet will silently return wrong
    /// translations.  See `parseCSV` below for a header-based parser that is
    /// robust to column reordering and uses a typed `TranslationMap` instead of
    /// `string[][]`.
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


    // ── Typed translation map (header-based, robust to column reordering) ────

    /// A typed translation map: term-key → locale → translated string.
    /// Replaces the opaque `string[][]` representation for new code paths.
    type TranslationMap = Map<string, Map<Locales, string>>


    /// Converts a display name string to a `Locales` value, returning `None`
    /// for unknown names instead of throwing (unlike `fromString`).
    let tryFromString (s: string) : Locales option =
        let s = s.Trim().ToLower()

        match s with
        | "english" -> Some English
        | "nederlands" -> Some Dutch
        | "français" -> Some French
        | "español" -> Some Spanish
        | "deutsch" -> Some German
        | "italiano" -> Some Italian
        | _ -> None


    /// <summary>
    /// Parses a language given as an ISO 639-1 code (<c>en</c>, <c>nl</c>, <c>fr</c>, <c>de</c>,
    /// <c>es</c>, <c>it</c>), a display name (<c>English</c>, <c>Nederlands</c>, ...) or one of
    /// the client's legacy url codes (<c>du</c>, <c>gr</c>, <c>sp</c>). Case and surrounding
    /// whitespace do not matter; anything else, including null, is <c>None</c>. One parser for
    /// the <c>GENPRES_LANG</c> setting and the <c>la</c> url parameter. The sheet header keeps
    /// <c>tryFromString</c>: display names only.
    /// </summary>
    let tryParse (s: string) : Locales option =
        if isNull s then
            None
        else
            match s.Trim().ToLower() with
            | "en" -> Some English
            | "nl"
            | "du" -> Some Dutch
            | "fr" -> Some French
            | "de"
            | "gr" -> Some German
            | "es"
            | "sp" -> Some Spanish
            | "it" -> Some Italian
            | s -> tryFromString s


    /// Parses a `string[][]` from `Csv.parseCSV` into a `TranslationMap`.
    ///
    /// The first row is expected to contain column headers. Any column whose
    /// header matches a known locale display name (via `tryFromString`) is
    /// treated as a translation column. The first column holds the term key.
    ///
    /// Robust to column reordering in the source spreadsheet.
    let parseCSV (csv: string[][]) : TranslationMap =
        if csv.Length < 2 then
            Map.empty
        else
            let headers = csv[0]

            let localeColumns =
                headers
                |> Array.mapi (fun i header -> i, tryFromString header)
                |> Array.choose (fun (i, opt) -> opt |> Option.map (fun l -> i, l))

            csv
            |> Array.skip 1
            |> Array.choose (fun row ->
                if row.Length > 0 && not (System.String.IsNullOrWhiteSpace row[0]) then
                    let termKey = row[0].Trim()

                    let translations =
                        localeColumns
                        |> Array.choose (fun (colIdx, locale) ->
                            if colIdx < row.Length && not (System.String.IsNullOrWhiteSpace row[colIdx]) then
                                Some(locale, row[colIdx].Trim())
                            else
                                None
                        )
                        |> Map.ofArray

                    Some(termKey, translations)
                else
                    None
            )
            |> Map.ofArray


    /// Looks up a translated string in a `TranslationMap`.
    /// Returns `None` when the term or locale is absent.
    let getTermFromMap (translations: TranslationMap) (locale: Locales) (term: Terms) : string option =
        let key = $"{term}".Trim()
        translations |> Map.tryFind key |> Option.bind (Map.tryFind locale)


    /// Merges `remote` onto `fallback`: per-locale entries in `remote` take
    /// precedence; terms missing from `remote` are kept from `fallback`.
    let mergeTranslations (fallback: TranslationMap) (remote: TranslationMap) : TranslationMap =
        remote
        |> Map.fold
            (fun acc termKey remoteLocales ->
                let merged =
                    match Map.tryFind termKey acc with
                    | None -> remoteLocales
                    | Some fallbackLocales ->
                        Map.fold (fun m locale txt -> Map.add locale txt m) fallbackLocales remoteLocales

                Map.add termKey merged acc
            )
            fallback


    /// Looks up a term, falling back to `defVal` when absent.
    let getTermOrDefault (translations: TranslationMap) (locale: Locales) (defVal: string) (term: Terms) : string =
        getTermFromMap translations locale term |> Option.defaultValue defVal
