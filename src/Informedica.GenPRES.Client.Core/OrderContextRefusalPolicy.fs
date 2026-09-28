/// The notice of a refused order context: what the prescribing page says when no dose can be
/// shown, from the refusal the server sent and the picks the user made. The texts are Terms,
/// translated by the caller: the view passes the sheet lookup, the tests pass english.
module OrderContextRefusalPolicy

open Shared
open Shared.Types
open Shared.Models


/// What the page shows for a refusal: the title, the body naming the picks, and whom to
/// tell.
type Notice =
    {
        Title: string
        Body: string
        Contact: string
    }


/// The English of the refusal terms: what the notice shows when the sheet has no row for a
/// term, or the terms have not loaded. Any other term falls back to its name.
let english (term: Terms) =
    match term with
    | Terms.``Prescribe Refusal`` -> "No dose can be shown"
    | Terms.``Prescribe Refusal No dose rules`` -> "There is no dose rule for {0}"
    | Terms.``Prescribe Refusal Patient`` ->
        "There are dose rules for {0}, but none covers the age, weight or department of this patient"
    | Terms.``Prescribe Refusal No products`` ->
        "There are dose rules for {0} that cover this patient, but none has a product and dose type to prescribe"
    | Terms.``Prescribe Refusal Contact`` ->
        "Report this to the pharmacy or the application manager, so that the rule can be added"
    | term -> $"{term}"


/// The picks the notice names, in the order the page shows them: indication, medication,
/// route, form and dose type, as far as chosen, one after the other.
let picks (filter: Filter) =
    [
        filter.Indication
        filter.Generic
        filter.Route
        filter.Form
        filter.DoseType |> Option.map DoseType.doseTypeToDescription
    ]
    |> List.choose id
    |> String.concat ", "


/// The body's term per case. The products term speaks for the third case as a whole: a rule
/// dropped for having no dose type is refused under it too, so its text names both.
let bodyTerm (refusal: OrderContextRefusal) =
    match refusal with
    | OrderContextRefusal.NoDoseRules -> Terms.``Prescribe Refusal No dose rules``
    | OrderContextRefusal.NoDoseRulesForPatient -> Terms.``Prescribe Refusal Patient``
    | OrderContextRefusal.NoProducts -> Terms.``Prescribe Refusal No products``


/// Whether the patient as sent lacks a weight or a height, measured or estimated. When rules
/// exist for the picks, the server refuses such a patient under the patient case before any
/// rule is matched; no rule for the picks at all stays the first refusal.
let patientIncomplete (patient: Patient) =
    (patient |> Patient.getWeight).IsNone || (patient |> Patient.getHeight).IsNone


/// The notice for the context refused: the title, the body of the case with the picks
/// filled in, and the contact sentence. None for the patient case when the weight or the
/// height is missing: the page already says what to enter, and a notice blaming the rules
/// and asking to report them would send the user the wrong way.
let notice (tr: Terms -> string) (ctx: OrderContext) (refusal: OrderContextRefusal) : Notice option =
    match refusal with
    | OrderContextRefusal.NoDoseRulesForPatient when ctx.Patient |> patientIncomplete -> None
    | _ ->
        Some
            {
                Title = tr Terms.``Prescribe Refusal``
                Body = tr (bodyTerm refusal) |> TermText.fill [ picks ctx.Filter ]
                Contact = tr Terms.``Prescribe Refusal Contact``
            }


/// The notice's text under its title: the body, then whom to tell; an empty translation
/// adds no sentence.
let message (notice: Notice) = TermText.sentences [ notice.Body; notice.Contact ]
