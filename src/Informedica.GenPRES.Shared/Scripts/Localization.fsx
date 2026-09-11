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
    // the gate after the server ended the Session (Rule 11, plan 605 PR 4)
    | ``Session Gate Ended``
    | ``Session Ending Superseded``
    // Rule 28 (UC-3, plan 622 PR 1): the Session ended at the third wrong PIN
    | ``Session Ending Pin Limit``
    // the enrolment form (UC-2, plan 615 PR 3): title, body with {0} the name and {1} the
    // hinted mail address, the three field labels, the button, and one sentence per refusal
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
    // signing (UC-3, plan 622 PR 5): the button, the dialog, the data notice (Rule 44), the
    // signed sentence with {0} the version and {1} the signer, and one sentence per refusal
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
    | ``Session Refusal Enrolment`` -> "A PIN has to be set before prescribing. Open GenPRES again from MainEHR."
    | ``Session Try Again`` -> "Try again"
    | ``Session Continue Without Launch`` -> "Continue without launch"
    | ``Session Close`` -> "Close session"
    | ``Session Role Prescriber`` -> "Prescriber"
    | ``Session Role Reader`` -> "Reader"
    | ``Session Gate Ended`` -> "Your session was ended"
    | ``Session Ending Superseded`` ->
        "Another launch of yours opened a newer session, and this one was closed."
    | ``Session Ending Pin Limit`` -> "The PIN was entered wrong three times, and signing is locked for a while."
    | ``Session Gate Enrolment`` -> "Set a PIN to continue"
    | ``Session Gate Enrolment Text`` ->
        "Welcome, {0}. A confirmation code was mailed to {1}. Enter it together with the PIN of your choice: four to six digits."
    | ``Session Enrolment Code`` -> "Confirmation code"
    | ``Session Enrolment Pin`` -> "PIN"
    | ``Session Enrolment Pin Repeat`` -> "Repeat the PIN"
    | ``Session Enrolment Submit`` -> "Set PIN"
    | ``Session Enrolment Code Format`` -> "The confirmation code has six digits."
    | ``Session Enrolment Pin Format`` -> "The PIN has four to six digits."
    | ``Session Enrolment Pins Differ`` -> "The two PINs differ."
    | ``Session Enrolment Wrong Code`` -> "The code is not right. {0} tries left."
    | ``Session Enrolment Code Void`` -> "The code is void after three wrong tries."
    | ``Session Enrolment Expired`` -> "The enrolment has expired."
    | ``Signing Sign`` -> "Sign"
    | ``Signing Dialog Title`` -> "Sign the order plan"
    | ``Signing Dialog Text`` -> "Sign the orders as shown with your PIN, or cancel and edit."
    | ``Signing Pin`` -> "PIN"
    | ``Signing Cancel`` -> "Cancel"
    | ``Signing Proceed`` -> "Continue"
    | ``Signing Signed`` -> "Version {0} was signed by {1}."
    | ``Signing Data Changed`` ->
        "The patient data changed since the session opened. It is shown as it stands now; continue to sign over it, or cancel."
    | ``Signing Data Unverified`` ->
        "The patient data could not be verified. Continue to sign over the data the session opened with, or cancel."
    | ``Signing Refusal No Session`` -> "There is no session to sign in. Open GenPRES again from MainEHR."
    | ``Signing Refusal No Patient`` -> "The plan is not over this session's patient."
    | ``Signing Refusal Not Prescriber`` -> "Only a Prescriber can sign."
    | ``Signing Refusal Blocked`` -> "{0} signed a newer version at {1}. Open the patient again to continue from it."
    | ``Signing Refusal Stale Token`` -> "The session is not current. Reload the page."
    | ``Signing Refusal Challenge Mismatch`` -> "The plan changed since it was shown. Sign again."
    | ``Signing Refusal Challenge Expired`` -> "The signature took too long. Sign again."
    | ``Signing Refusal Pin Wrong`` -> "The PIN is not right. {0} tries left."
    | ``Signing Refusal Pin Limit`` ->
        "The PIN was entered wrong three times. Your session was ended and signing is locked for a while."
    | ``Signing Refusal Locked`` -> "Signing is locked until {0}."


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
        "Er moet een pincode worden ingesteld voordat u kunt voorschrijven. Open GenPRES opnieuw vanuit MainEHR."
    | ``Session Try Again`` -> "Probeer opnieuw"
    | ``Session Continue Without Launch`` -> "Doorgaan zonder launch"
    | ``Session Close`` -> "Sessie sluiten"
    | ``Session Role Prescriber`` -> "Voorschrijver"
    | ``Session Role Reader`` -> "Lezer"
    | ``Session Gate Ended`` -> "Uw sessie is beëindigd"
    | ``Session Ending Superseded`` ->
        "Een andere start van u heeft een nieuwere sessie geopend; deze sessie is gesloten."
    | ``Session Ending Pin Limit`` -> "De pincode is drie keer verkeerd ingevoerd; ondertekenen is een tijdje geblokkeerd."
    | ``Session Gate Enrolment`` -> "Stel een pincode in om verder te gaan"
    | ``Session Gate Enrolment Text`` ->
        "Welkom, {0}. Er is een bevestigingscode gemaild naar {1}. Voer die in samen met de pincode van uw keuze: vier tot zes cijfers."
    | ``Session Enrolment Code`` -> "Bevestigingscode"
    | ``Session Enrolment Pin`` -> "Pincode"
    | ``Session Enrolment Pin Repeat`` -> "Herhaal de pincode"
    | ``Session Enrolment Submit`` -> "Pincode instellen"
    | ``Session Enrolment Code Format`` -> "De bevestigingscode bestaat uit zes cijfers."
    | ``Session Enrolment Pin Format`` -> "De pincode bestaat uit vier tot zes cijfers."
    | ``Session Enrolment Pins Differ`` -> "De twee pincodes verschillen."
    | ``Session Enrolment Wrong Code`` -> "De code klopt niet. Nog {0} pogingen."
    | ``Session Enrolment Code Void`` -> "De code is na drie verkeerde pogingen niet meer geldig."
    | ``Session Enrolment Expired`` -> "De inschrijving is verlopen."
    | ``Signing Sign`` -> "Ondertekenen"
    | ``Signing Dialog Title`` -> "Onderteken het voorschrijfplan"
    | ``Signing Dialog Text`` -> "Onderteken de voorschriften zoals getoond met uw pincode, of annuleer en pas aan."
    | ``Signing Pin`` -> "Pincode"
    | ``Signing Cancel`` -> "Annuleren"
    | ``Signing Proceed`` -> "Doorgaan"
    | ``Signing Signed`` -> "Versie {0} is ondertekend door {1}."
    | ``Signing Data Changed`` ->
        "De patiëntgegevens zijn gewijzigd sinds de sessie werd geopend. Ze worden getoond zoals ze nu zijn; ga door om daarover te ondertekenen, of annuleer."
    | ``Signing Data Unverified`` ->
        "De patiëntgegevens konden niet worden geverifieerd. Ga door om te ondertekenen over de gegevens waarmee de sessie is geopend, of annuleer."
    | ``Signing Refusal No Session`` -> "Er is geen sessie om in te ondertekenen. Open GenPRES opnieuw vanuit MainEHR."
    | ``Signing Refusal No Patient`` -> "Het plan hoort niet bij de patiënt van deze sessie."
    | ``Signing Refusal Not Prescriber`` -> "Alleen een voorschrijver kan ondertekenen."
    | ``Signing Refusal Blocked`` ->
        "{0} heeft om {1} een nieuwere versie ondertekend. Open de patiënt opnieuw om daarvan verder te gaan."
    | ``Signing Refusal Stale Token`` -> "De sessie is niet actueel. Laad de pagina opnieuw."
    | ``Signing Refusal Challenge Mismatch`` -> "Het plan is gewijzigd sinds het werd getoond. Onderteken opnieuw."
    | ``Signing Refusal Challenge Expired`` -> "Het ondertekenen duurde te lang. Onderteken opnieuw."
    | ``Signing Refusal Pin Wrong`` -> "De pincode klopt niet. Nog {0} pogingen."
    | ``Signing Refusal Pin Limit`` ->
        "De pincode is drie keer verkeerd ingevoerd. Uw sessie is beëindigd en ondertekenen is een tijdje geblokkeerd."
    | ``Signing Refusal Locked`` -> "Ondertekenen is geblokkeerd tot {0}."


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

            test "placeholders appear only in the attempt, enrolment, wrong-code and signing texts, in both languages" {
                let withPlaceholders =
                    all
                    |> Array.filter (fun t -> (english t).Contains "{0}" || (dutch t).Contains "{0}")
                    |> Array.sort

                withPlaceholders
                |> Expect.equal
                    "placeholders"
                    ([|
                        ``Session Gate Opening Text``
                        ``Session Gate Unreachable Text``
                        ``Session Gate Enrolment Text``
                        ``Session Enrolment Wrong Code``
                        ``Signing Signed``
                        ``Signing Refusal Blocked``
                        ``Signing Refusal Pin Wrong``
                        ``Signing Refusal Locked``
                     |]
                     |> Array.sort)

                for t in
                    [
                        ``Session Gate Opening Text``
                        ``Session Gate Enrolment Text``
                        ``Signing Signed``
                        ``Signing Refusal Blocked``
                    ] do
                    (english t).Contains "{1}" |> Expect.isTrue $"{{1}} en {t}"
                    (dutch t).Contains "{1}" |> Expect.isTrue $"{{1}} nl {t}"
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
