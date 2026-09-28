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
    | ``Session Ending Unreadable``
    | ``Session Ending Idle``
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
    | ``Signing Refusal Store Failed``
    | ``Signing Refusal Plan Unreadable``
    | ``Signing Send Failed``
    // Rules 21, 22 (plan 635 PR 4): the record moved on, told once per version with {0} who
    // signed it and {1} when; the button that takes the version up (UC-4 step 4); and what
    // is told once it is open, {0} the version number and {1} who signed it
    | ``Session Newer Version``
    | ``Session Open Newest``
    | ``Session Version Opened``


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
    | ``Session Ending Unreadable`` -> "This session could not be read back after an update of GenPRES."
    | ``Session Ending Idle`` ->
        "This session was not used for too long and was closed, so that nothing is prescribed on patient data read long ago."
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
    | ``Signing Refusal Store Failed`` -> "The version could not be stored. Nothing changed; sign again."
    | ``Signing Refusal Plan Unreadable`` -> "The plan could not be read. Reload the page and sign again."
    | ``Signing Send Failed`` -> "The signature could not be sent. Try again."
    | ``Session Newer Version`` -> "{0} signed a newer version at {1}."
    | ``Session Open Newest`` -> "Open the newest version"
    | ``Session Version Opened`` -> "Version {0} by {1} is now open."


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
    | ``Session Ending Unreadable`` -> "Deze sessie kon na een update van GenPRES niet meer worden gelezen."
    | ``Session Ending Idle`` ->
        "Deze sessie is te lang niet gebruikt en is gesloten, zodat er niet wordt voorgeschreven op patiëntgegevens van lang geleden."
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
    | ``Signing Refusal Store Failed`` -> "De versie kon niet worden opgeslagen. Er is niets veranderd; onderteken opnieuw."
    | ``Signing Refusal Plan Unreadable`` -> "Het plan kon niet worden gelezen. Laad de pagina opnieuw en onderteken opnieuw."
    | ``Signing Send Failed`` -> "De handtekening kon niet worden verstuurd. Probeer het opnieuw."
    | ``Session Newer Version`` -> "{0} heeft om {1} een nieuwere versie ondertekend."
    | ``Session Open Newest`` -> "Open de nieuwste versie"
    | ``Session Version Opened`` -> "Versie {0} van {1} is nu geopend."


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
                        ``Session Newer Version``
                        ``Session Version Opened``
                     |]
                     |> Array.sort)

                for t in
                    [
                        ``Session Gate Opening Text``
                        ``Session Gate Enrolment Text``
                        ``Signing Signed``
                        ``Signing Refusal Blocked``
                        ``Session Newer Version``
                        ``Session Version Opened``
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


// ---------------------------------------------------------------------------------------------
// Issue #633: the page term ``Treatment Plan`` becomes ``Order Plan``. The code's type has been
// `OrderPlan` since 9c0d573e and the integration design followed (#632); the term key of the
// page title is the last place with the old word. The key is the case name, so the `Terms`
// case, the `Global.pageToString` arm and the sheet row all change together. The English and
// the Dutch value change (the page title is "Order Plan" in both); the other four are the old
// row's, verbatim.
// ---------------------------------------------------------------------------------------------

/// The row as the sheet and `data/localization/*.tsv` should read after the rename.
let orderPlanRow =
    [|
        "Order Plan"
        "Order Plan"
        "Order Plan"
        "Plan de traitement"
        "Behandlungsplan"
        "Plan de tratamiento"
        "Piano di trattamento"
    |]


/// The row it replaces.
let treatmentPlanRow =
    orderPlanRow
    |> Array.mapi (fun i s ->
        match i with
        | 0
        | 1 -> "Treatment Plan"
        | 2 -> "Behandel Plan"
        | _ -> s
    )


let renameTests =
    testList
        "order plan term"
        [
            test "the new key resolves in every language" {
                for l in languages do
                    getTerm [| orderPlanRow |] l "Order Plan"
                    |> Expect.isSome $"Order Plan in {l}"
            }

            test "the new row differs from the old one in the key, the English and the Dutch value only" {
                Array.zip treatmentPlanRow orderPlanRow
                |> Array.indexed
                |> Array.filter (fun (_, (a, b)) -> a <> b)
                |> Array.map fst
                |> Expect.equal "changed columns" [| 0; 1; 2 |]
            }

            test "the old key no longer resolves once the row is replaced" {
                getTerm [| orderPlanRow |] English "Treatment Plan"
                |> Expect.isNone "Treatment Plan"
            }
        ]


let printRenamedRow () =
    orderPlanRow |> String.concat "\t" |> printfn "%s"


// --- The patient panel says what is missing (plan 646, step 5) -----------------------------
//
// A draft below the minimum is no patient: the panel shows its data and, under it, what is
// missing. One sentence, its own term, → `Terms`, after ``Patient enter patient data``; the row
// → the sheet and `data/localization/*.tsv`.

/// → `Shared/Localization.fs`, `Terms`.
type PatientTerms = | ``Patient enter age or weight and height``


let patientRow: string[] =
    [|
        "Patient enter age or weight and height"
        "Enter an age, or a weight and a height"
        "Voer een leeftijd in, of een gewicht en een lengte"
        "Saisissez un âge, ou un poids et une taille"
        "Geben Sie ein Alter ein, oder ein Gewicht und eine Größe"
        "Ingrese una edad, o un peso y una talla"
        "Inserisci un'età, o un peso e un'altezza"
    |]


let patientTests =
    testList
        "patient minimum term"
        [
            test "the key is the case's name, and resolves in every language" {
                $"{``Patient enter age or weight and height``}"
                |> Expect.equal "the key" patientRow[0]

                for l in languages do
                    getTerm [| patientRow |] l patientRow[0] |> Expect.isSome $"in {l}"
            }

            test "the Dutch says an age, or a weight and a height" {
                getTerm [| patientRow |] Dutch patientRow[0]
                |> Expect.equal "Dutch" (Some "Voer een leeftijd in, of een gewicht en een lengte")
            }
        ]


let printPatientRow () =
    patientRow |> String.concat "\t" |> printfn "%s"


// --- The prescribing page says which dimension is missing (plan 646, step 6) ----------------
//
// A patient without an age loses every dose rule with an age bound, silently, since a value
// that is missing never matches a bounded range; a patient with an age only and no estimate has no weight
// and height for the rules to gate on, and the server refuses it. The page says so, one
// sentence each, → `Terms`, after ``Prescribe Administration``; the rows → the sheet.

/// → `Shared/Localization.fs`, `Terms`.
type PrescribeTerms =
    | ``Prescribe Age unknown``
    | ``Prescribe Weight and height unknown``
    // one of the two missing, the other measured or estimated: the notice names the one
    | ``Prescribe Weight unknown``
    | ``Prescribe Height unknown``


let prescribeRows: string[][] =
    [|
        [|
            "Prescribe Age unknown"
            "Age unknown: only dose rules without an age bound are offered"
            "Leeftijd onbekend: alleen doseerregels zonder leeftijdsgrens worden getoond"
            "Âge inconnu : seules les règles de dosage sans limite d'âge sont proposées"
            "Alter unbekannt: nur Dosierregeln ohne Altersgrenze werden angeboten"
            "Edad desconocida: solo se ofrecen reglas de dosificación sin límite de edad"
            "Età sconosciuta: vengono proposte solo le regole di dosaggio senza limite di età"
        |]
        [|
            "Prescribe Weight and height unknown"
            "Weight and height unknown: enter them, there is no estimate"
            "Gewicht en lengte onbekend: voer ze in, er is geen schatting"
            "Poids et taille inconnus : saisissez-les, il n'y a pas d'estimation"
            "Gewicht und Größe unbekannt: geben Sie sie ein, es gibt keine Schätzung"
            "Peso y talla desconocidos: introdúzcalos, no hay estimación"
            "Peso e altezza sconosciuti: inseriscili, non c'è una stima"
        |]
        [|
            "Prescribe Weight unknown"
            "Weight unknown: enter it, there is no estimate"
            "Gewicht onbekend: voer het in, er is geen schatting"
            "Poids inconnu : saisissez-le, il n'y a pas d'estimation"
            "Gewicht unbekannt: geben Sie es ein, es gibt keine Schätzung"
            "Peso desconocido: introdúzcalo, no hay estimación"
            "Peso sconosciuto: inseriscilo, non c'è una stima"
        |]
        [|
            "Prescribe Height unknown"
            "Height unknown: enter it, there is no estimate"
            "Lengte onbekend: voer die in, er is geen schatting"
            "Taille inconnue : saisissez-la, il n'y a pas d'estimation"
            "Größe unbekannt: geben Sie sie ein, es gibt keine Schätzung"
            "Talla desconocida: introdúzcala, no hay estimación"
            "Altezza sconosciuta: inseriscila, non c'è una stima"
        |]
    |]


let prescribeTests =
    testList
        "missing dimension terms"
        [
            test "the keys are the cases' names, and resolve in every language" {
                [
                    ``Prescribe Age unknown``
                    ``Prescribe Weight and height unknown``
                    ``Prescribe Weight unknown``
                    ``Prescribe Height unknown``
                ]
                |> List.map (fun t -> $"{t}")
                |> Expect.equal "the keys" (prescribeRows |> Array.map (fun r -> r[0]) |> Array.toList)

                for r in prescribeRows do
                    for l in languages do
                        getTerm prescribeRows l r[0] |> Expect.isSome $"{r[0]} in {l}"
            }
        ]


let printPrescribeRows () =
    prescribeRows |> Array.iter (fun r -> r |> String.concat "\t" |> printfn "%s")


// --- The word on a reset (plan 982, step 3) --------------------------------------------------
//
// The control that puts a page's choices back as they were is written out in three places and
// localized in none: the prescribing page says "Verwijder", which is what a delete says, and the
// dose dialog and the nutrition page both say "Reset" in English whatever language the user is
// reading. One word for the act, like `Delete` and `Ok `, with no area before it, since the same
// act is offered on several pages. → `Terms`, beside `Delete`; the row → the sheet and
// `data/localization/*.tsv`.

/// → `Shared/Localization.fs`, `Terms`.
type ResetTerms = | ``Reset``


let resetRows: string[][] =
    [|
        [|
            "Reset"
            "Reset"
            "Reset"
            "Réinitialiser"
            "Zurücksetzen"
            "Restablecer"
            "Reimposta"
        |]
    |]


let resetTests =
    testList
        "the word on a reset"
        [
            test "the key is the case's name, and resolves in every language" {
                [ ``Reset`` ]
                |> List.map (fun t -> $"{t}")
                |> Expect.equal "the key" (resetRows |> Array.map (fun r -> r[0]) |> Array.toList)

                for r in resetRows do
                    for l in languages do
                        getTerm resetRows l r[0] |> Expect.isSome $"{r[0]} in {l}"
            }

            test "the word stands alone, so it can be read on a button of its own" {
                resetRows[0]
                |> Array.forall (fun s -> s.Contains "{0}" |> not && s.Trim() = s)
                |> Expect.isTrue "no placeholder and no stray space"
            }
        ]


let printResetRow () =
    resetRows |> Array.iter (fun r -> r |> String.concat "\t" |> printfn "%s")


// --- The reason when no dose rule allows the pick (plan 985, step 3) -------------------------
//
// Migrated to `Types.fs` and `Localization.fs` on 2026-09-28; kept as the draft and its tests.
//
// The server answers a typed refusal in the reply instead of a Dutch message the client matched
// on: no dose rule for the picks at all, rules that cover no patient like this one, or rules
// for the patient that have no product or no dose type to prescribe. The client renders it as
// a notice on the page the user is on: a title, one body per case naming the picks through
// `{0}`, and one contact sentence for every site. The two types → `Shared/Types.fs`, after
// `SigningResponse`; the terms → `Terms`, after ``Prescribe Height unknown``; the rows → the
// sheet and `data/localization/*.tsv`. The API signature does not change here; step 4 does.

/// → `Shared/Types.fs`. Why an evaluation answers no scenarios; the same three cases as
/// GenORDER's Refusal, in the contract's own words.
[<RequireQualifiedAccess>]
type OrderContextRefusal =
    /// No dose rule exists for the picks at all.
    | NoDoseRules
    /// Dose rules exist for the picks, and none of them covers this patient.
    | NoDoseRulesForPatient
    /// Dose rules cover the picks and the patient, and none of them can be prescribed: no
    /// product, or no dose type.
    | NoProducts


/// → `Shared/Types.fs`. The answer to an order-context command. A payload like `LaunchOutcome`:
/// the context evaluated, or the context as it was sent, its picks kept, with the refusal.
/// The error channel stays for failures.
[<RequireQualifiedAccess>]
type OrderContextResponse =
    | Evaluated of Types.OrderContext
    | Refused of Types.OrderContext * OrderContextRefusal


/// → `Shared/Localization.fs`, `Terms`.
type RefusalTerms =
    // the title of the notice
    | ``Prescribe Refusal``
    // one body per case, the picks filled into {0}
    | ``Prescribe Refusal No dose rules``
    | ``Prescribe Refusal Patient``
    | ``Prescribe Refusal No products``
    // whom to tell, one sentence for every site
    | ``Prescribe Refusal Contact``


let refusalRows: string[][] =
    [|
        [|
            "Prescribe Refusal"
            "No dose can be shown"
            "Er kan geen dosering worden getoond"
            "Aucune dose ne peut être affichée"
            "Es kann keine Dosierung angezeigt werden"
            "No se puede mostrar ninguna dosis"
            "Non è possibile mostrare alcuna dose"
        |]
        [|
            "Prescribe Refusal No dose rules"
            "There is no dose rule for {0}"
            "Er is geen doseerregel voor {0}"
            "Il n'y a pas de règle de dosage pour {0}"
            "Es gibt keine Dosierregel für {0}"
            "No hay ninguna regla de dosificación para {0}"
            "Non esiste una regola di dosaggio per {0}"
        |]
        [|
            "Prescribe Refusal Patient"
            "There are dose rules for {0}, but none covers the age, weight or department of this patient"
            "Er zijn doseerregels voor {0}, maar geen ervan geldt voor de leeftijd, het gewicht of de afdeling van deze patiënt"
            "Il existe des règles de dosage pour {0}, mais aucune ne couvre l'âge, le poids ou le service de ce patient"
            "Es gibt Dosierregeln für {0}, aber keine gilt für Alter, Gewicht oder Abteilung dieses Patienten"
            "Hay reglas de dosificación para {0}, pero ninguna cubre la edad, el peso o el departamento de este paciente"
            "Esistono regole di dosaggio per {0}, ma nessuna copre l'età, il peso o il reparto di questo paziente"
        |]
        [|
            "Prescribe Refusal No products"
            "There are dose rules for {0} that cover this patient, but none has a product that can be prescribed"
            "Er zijn doseerregels voor {0} die voor deze patiënt gelden, maar geen ervan heeft een product dat voorgeschreven kan worden"
            "Il existe des règles de dosage pour {0} qui couvrent ce patient, mais aucune n'a de produit prescriptible"
            "Es gibt Dosierregeln für {0}, die für diesen Patienten gelten, aber keine hat ein verordenbares Produkt"
            "Hay reglas de dosificación para {0} que cubren a este paciente, pero ninguna tiene un producto que se pueda prescribir"
            "Esistono regole di dosaggio per {0} che coprono questo paziente, ma nessuna ha un prodotto prescrivibile"
        |]
        [|
            "Prescribe Refusal Contact"
            "Report this to the pharmacy or the application manager, so that the rule can be added"
            "Meld dit bij de apotheek of de applicatiebeheerder, zodat de regel toegevoegd kan worden"
            "Signalez-le à la pharmacie ou au gestionnaire de l'application, afin que la règle puisse être ajoutée"
            "Melden Sie dies der Apotheke oder dem Anwendungsbetreuer, damit die Regel ergänzt werden kann"
            "Comuníquelo a la farmacia o al administrador de la aplicación, para que se pueda añadir la regla"
            "Segnalalo alla farmacia o al responsabile dell'applicazione, in modo che la regola possa essere aggiunta"
        |]
    |]


let refusalTests =
    testList
        "refusal terms"
        [
            test "the keys are the cases' names, and resolve in every language" {
                [
                    ``Prescribe Refusal``
                    ``Prescribe Refusal No dose rules``
                    ``Prescribe Refusal Patient``
                    ``Prescribe Refusal No products``
                    ``Prescribe Refusal Contact``
                ]
                |> List.map (fun t -> $"{t}")
                |> Expect.equal "the keys" (refusalRows |> Array.map (fun r -> r[0]) |> Array.toList)

                for r in refusalRows do
                    for l in languages do
                        getTerm refusalRows l r[0] |> Expect.isSome $"{r[0]} in {l}"
            }

            test "every body takes the picks, the title and the contact sentence take none" {
                for r in refusalRows do
                    let takesPicks = r[0] <> "Prescribe Refusal" && r[0] <> "Prescribe Refusal Contact"

                    for text in r[1..] do
                        text.Contains "{0}" |> Expect.equal $"{r[0]}: {text}" takesPicks
            }

            test "the picks fill the body" {
                getTerm refusalRows Dutch "Prescribe Refusal Patient"
                |> Option.map (fill [ "salbutamol, intraveneus" ])
                |> Expect.equal
                    "filled"
                    (Some
                        "Er zijn doseerregels voor salbutamol, intraveneus, maar geen ervan geldt voor de leeftijd, het gewicht of de afdeling van deze patiënt")
            }
        ]


let printRefusalRows () =
    refusalRows |> Array.iter (fun r -> r |> String.concat "\t" |> printfn "%s")


// ---------------------------------------------------------------------------------------------
// The argumentation (plan 985, step 9): the field's label in the dose dialog and the sign
// dialog, and the line under the field that says what to write.
// ---------------------------------------------------------------------------------------------

/// → `Shared/Localization.fs`, `Terms`, after ``Order Administration time``.
type ArgumentationTerms =
    | ``Order Argumentation``
    | ``Order Argumentation Helper``


let argumentationRows: string[][] =
    [|
        [|
            "Order Argumentation"
            "Argumentation"
            "Argumentatie"
            "Argumentation"
            "Begründung"
            "Argumentación"
            "Argomentazione"
        |]
        [|
            "Order Argumentation Helper"
            "Why the dose leaves what the rules allow"
            "Waarom de dosering afwijkt van wat de regels toestaan"
            "Pourquoi la dose s'écarte de ce que les règles permettent"
            "Warum die Dosierung von dem abweicht, was die Regeln erlauben"
            "Por qué la dosis se aparta de lo que permiten las reglas"
            "Perché la dose si discosta da quanto consentono le regole"
        |]
    |]


let argumentationTests =
    testList
        "argumentation terms"
        [
            test "the keys are the cases' names, and resolve in every language" {
                [ ``Order Argumentation``; ``Order Argumentation Helper`` ]
                |> List.map (fun t -> $"{t}")
                |> Expect.equal "the keys" (argumentationRows |> Array.map (fun r -> r[0]) |> Array.toList)

                for r in argumentationRows do
                    for l in languages do
                        getTerm argumentationRows l r[0] |> Expect.isSome $"{r[0]} in {l}"
            }
        ]


let printArgumentationRows () =
    argumentationRows |> Array.iter (fun r -> r |> String.concat "\t" |> printfn "%s")


runTestsWithCLIArgs
    []
    [||]
    (testList
        "Localization.fsx"
        [
            tests
            parseTests
            renameTests
            patientTests
            prescribeTests
            resetTests
            refusalTests
            argumentationTests
        ])
|> ignore
