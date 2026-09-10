// Session terms for the gate and the title bar (plan 409 follow-up, uc-01 Refusals).
//
// Script-first draft of the new `Terms` cases: `Shared/Localization.fs` is non-UI source, so the
// cases are proposed here as `SessionTerms` (a DU cannot be extended in a script), with their
// English defaults and Dutch translations, and checked against `Localization.getTerm` over rows
// shaped like the "Localization" sheet. After review, `printCases ()` gives the lines to paste
// into `Terms`, and `printTsv ()` the rows for the sheet and `data/localization/*.tsv`.
//
// Run: `dotnet fsi Localization.fsx` from this directory, or via the FSI MCP after
// `#I "<this directory>"`.

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "../Types.fs"
#load "../Utils.fs"
#load "../Localization.fs"

open Shared
open Shared.Localization


/// The proposed cases. Same naming as the sheet: an area prefix, then the term. Every sentence is
/// its own term (no sentence is interpolated into another), so a translation never has to agree
/// in case or word order with a sentence it is embedded in. Numbers are filled by the caller
/// through `{0}` and `{1}`.
type SessionTerms =
    // gate titles and bodies per phase
    | ``Session Gate Opening``
    | ``Session Gate Opening Text``
    | ``Session Gate Resuming``
    | ``Session Gate Resuming Text``
    | ``Session Gate Unreachable``
    | ``Session Gate Unreachable Text``
    | ``Session Gate Refused``
    | ``Session Gate Try Again Or Relaunch``
    // sentences the gate appends to a refusal
    | ``Session Relaunch``
    | ``Session Retry``
    // one sentence per refusal (uc-01 Refusals)
    | ``Session Refusal Expired``
    | ``Session Refusal Spent``
    | ``Session Refusal Invalid``
    | ``Session Refusal No Browser Identity``
    | ``Session Refusal No Role``
    | ``Session Refusal Wrong Patient``
    | ``Session Refusal Enrolment``
    // buttons and the title bar
    | ``Session Try Again``
    | ``Session Continue Without Launch``
    | ``Session Close``
    | ``Session Role Prescriber``
    | ``Session Role Reader``


/// The English defaults: the strings the client shows today, verbatim.
let english term =
    match term with
    | ``Session Gate Opening`` -> "Opening your session"
    | ``Session Gate Opening Text`` -> "Presenting the launch, attempt {0} of {1}."
    | ``Session Gate Resuming`` -> "Resuming your session"
    | ``Session Gate Resuming Text`` -> "Checking for an open session."
    | ``Session Gate Unreachable`` -> "GenPRES could not be reached"
    | ``Session Gate Unreachable Text`` -> "The server did not answer after {0} attempts."
    | ``Session Gate Refused`` -> "GenPRES could not open your session"
    | ``Session Gate Try Again Or Relaunch`` -> "Try again, or open GenPRES again from MainEHR."
    | ``Session Relaunch`` -> "Open GenPRES again from MainEHR."
    | ``Session Retry`` -> "Try again."
    | ``Session Refusal Expired`` -> "The launch has expired."
    | ``Session Refusal Spent`` -> "This launch has already been used."
    | ``Session Refusal Invalid`` -> "The launch is not valid."
    | ``Session Refusal No Browser Identity`` -> "Your browser could not be identified."
    | ``Session Refusal No Role`` ->
        "You have no role in GenPRES. You can continue without a launch: no patient is carried over."
    | ``Session Refusal Wrong Patient`` ->
        "The patient active in MainEHR is not the patient of this launch. Activate the right patient and open GenPRES again from MainEHR."
    | ``Session Refusal Enrolment`` ->
        "A PIN has to be set before prescribing. Enrolment is not available yet. Open GenPRES again from MainEHR."
    | ``Session Try Again`` -> "Try again"
    | ``Session Continue Without Launch`` -> "Continue without launch"
    | ``Session Close`` -> "Close session"
    | ``Session Role Prescriber`` -> "Prescriber"
    | ``Session Role Reader`` -> "Reader"


/// Dutch, for the sheet; the other four languages stay empty and fall back to English.
let dutch term =
    match term with
    | ``Session Gate Opening`` -> "Uw sessie wordt geopend"
    | ``Session Gate Opening Text`` -> "De launch wordt aangeboden, poging {0} van {1}."
    | ``Session Gate Resuming`` -> "Uw sessie wordt hervat"
    | ``Session Gate Resuming Text`` -> "Er wordt gecontroleerd op een open sessie."
    | ``Session Gate Unreachable`` -> "GenPRES is niet bereikbaar"
    | ``Session Gate Unreachable Text`` -> "De server antwoordde niet na {0} pogingen."
    | ``Session Gate Refused`` -> "GenPRES kon uw sessie niet openen"
    | ``Session Gate Try Again Or Relaunch`` -> "Probeer opnieuw, of open GenPRES opnieuw vanuit MainEHR."
    | ``Session Relaunch`` -> "Open GenPRES opnieuw vanuit MainEHR."
    | ``Session Retry`` -> "Probeer opnieuw."
    | ``Session Refusal Expired`` -> "De launch is verlopen."
    | ``Session Refusal Spent`` -> "Deze launch is al gebruikt."
    | ``Session Refusal Invalid`` -> "De launch is niet geldig."
    | ``Session Refusal No Browser Identity`` -> "Uw browser kon niet worden geïdentificeerd."
    | ``Session Refusal No Role`` ->
        "U heeft geen rol in GenPRES. U kunt doorgaan zonder launch: er wordt geen patiënt overgenomen."
    | ``Session Refusal Wrong Patient`` ->
        "De patiënt die actief is in MainEHR is niet de patiënt van deze launch. Activeer de juiste patiënt en open GenPRES opnieuw vanuit MainEHR."
    | ``Session Refusal Enrolment`` ->
        "Er moet een pincode worden ingesteld voordat u kunt voorschrijven. Inschrijven is nog niet beschikbaar. Open GenPRES opnieuw vanuit MainEHR."
    | ``Session Try Again`` -> "Probeer opnieuw"
    | ``Session Continue Without Launch`` -> "Doorgaan zonder launch"
    | ``Session Close`` -> "Sessie sluiten"
    | ``Session Role Prescriber`` -> "Voorschrijver"
    | ``Session Role Reader`` -> "Lezer"


let all =
    Reflection.FSharpType.GetUnionCases typeof<SessionTerms>
    |> Array.map (fun c -> Reflection.FSharpValue.MakeUnion(c, [||]) :?> SessionTerms)


/// Rows shaped like the sheet: Term, English, Dutch, French, German, Spanish, Italian.
let rows = all |> Array.map (fun t -> [| $"{t}"; english t; dutch t; ""; ""; ""; "" |])


/// Fills `{0}`, `{1}`, ... in a translated term; the client does the same.
let fill (args: string list) (s: string) =
    args |> List.indexed |> List.fold (fun s (i, a) -> s |> String.replace $"{{{i}}}" a) s


let printCases () =
    printfn "    // Session (plan 409): the gate and the session menu"
    all |> Array.iter (fun t -> printfn $"    | ``{t}``")


let printTsv () =
    rows |> Array.iter (fun r -> r |> String.concat "\t" |> printfn "%s")


// ---------------------------------------------------------------------------------------------
// One language vocabulary (GENPRES_LANG, plan: server default language). Today three spellings
// of the same six languages exist: the ISO short codes of `toShortCode`, the display names of
// `tryFromString`, and the url codes of the client's `la` parameter (`en du fr gr sp it`).
// `tryParse` accepts all of them, so the env value and the url parameter share one parser (the
// sheet header keeps `tryFromString`: display names only).
// ---------------------------------------------------------------------------------------------

module Localization =

    open Shared.Localization

    /// Parses a language given as an ISO 639-1 code (`en`, `nl`, `fr`, `de`, `es`, `it`), a
    /// display name (`English`, `Nederlands`, ...) or one of the client's legacy url codes
    /// (`du`, `gr`, `sp`). Case and surrounding whitespace do not matter; anything else is None.
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


open Expecto
open Expecto.Flip


let parseTests =
    testList
        "Localization.tryParse"
        [
            test "every locale round-trips through its short code, upper or lower case" {
                for l in languages do
                    l |> toShortCode |> Localization.tryParse |> Expect.equal $"{l} upper" (Some l)

                    l
                    |> toShortCode
                    |> _.ToLower()
                    |> Localization.tryParse
                    |> Expect.equal $"{l} lower" (Some l)
            }

            test "every locale round-trips through its display name" {
                for l in languages do
                    l |> toString |> Localization.tryParse |> Expect.equal $"{l}" (Some l)
            }

            testList
                "the client's legacy url codes"
                [
                    for code, l in [ "en", English; "du", Dutch; "fr", French; "gr", German; "sp", Spanish; "it", Italian ] do
                        test code { code |> Localization.tryParse |> Expect.equal code (Some l) }
                ]

            test "whitespace is ignored" {
                "  nl " |> Localization.tryParse |> Expect.equal "padded" (Some Dutch)
            }

            test "unknown, empty and null are None" {
                for s in [ "xx"; ""; " "; "dutch"; null ] do
                    s |> Localization.tryParse |> Expect.isNone $"'{s}'"
            }
        ]


let tests =
    testList
        "session terms"
        [
            test "every case has an English default and a Dutch translation" {
                for t in all do
                    english t |> String.isNullOrWhiteSpace |> Expect.isFalse $"English for {t}"
                    dutch t |> String.isNullOrWhiteSpace |> Expect.isFalse $"Dutch for {t}"
            }

            test "every case resolves through getTerm in English and Dutch" {
                for t in all do
                    getTerm rows English t |> Expect.equal $"English {t}" (Some(english t))
                    getTerm rows Dutch t |> Expect.equal $"Dutch {t}" (Some(dutch t))
            }

            test "a language without a translation resolves to None (client falls back to English)" {
                for t in all do
                    getTerm rows French t |> Expect.isNone $"French {t}"
            }

            test "case names are unique and are the row keys" {
                rows
                |> Array.map (fun r -> r[0])
                |> Array.distinct
                |> Array.length
                |> Expect.equal "distinct keys" all.Length
            }

            test "placeholders appear only in the two attempt texts, in both languages" {
                let withPlaceholders =
                    all
                    |> Array.filter (fun t -> (english t).Contains "{0}" || (dutch t).Contains "{0}")
                    |> Array.sort

                withPlaceholders
                |> Expect.equal
                    "placeholders"
                    ([| ``Session Gate Opening Text``; ``Session Gate Unreachable Text`` |] |> Array.sort)

                (english ``Session Gate Opening Text``).Contains "{1}" |> Expect.isTrue "{1} en"
                (dutch ``Session Gate Opening Text``).Contains "{1}" |> Expect.isTrue "{1} nl"
            }

            test "fill replaces the placeholders" {
                english ``Session Gate Opening Text``
                |> fill [ "2"; "3" ]
                |> Expect.equal "filled" "Presenting the launch, attempt 2 of 3."

                dutch ``Session Gate Unreachable Text``
                |> fill [ "3" ]
                |> Expect.equal "filled" "De server antwoordde niet na 3 pogingen."
            }

            test "product names keep their case in every language" {
                for t in all do
                    for s in [ english t; dutch t ] do
                        s.Contains "genpres" |> Expect.isFalse $"lowercased GenPRES in: {s}"
                        s.Contains "mainehr" |> Expect.isFalse $"lowercased MainEHR in: {s}"
            }
        ]


runTestsWithCLIArgs [] [||] (testList "Localization.fsx" [ tests; parseTests ]) |> ignore
