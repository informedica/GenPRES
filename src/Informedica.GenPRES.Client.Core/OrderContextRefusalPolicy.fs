/// Decides what the prescribing page says when the server refuses an order context. The texts
/// are Terms, translated by the caller.
module OrderContextRefusalPolicy

open Shared
open Shared.Types
open Shared.Models


/// What the page shows for a refusal.
type Notice =
    {
        /// The title.
        Title: string
        /// Why no dose can be shown, naming the picks.
        Body: string
        /// Whom to tell.
        Contact: string
    }


/// The English of the refusal terms, used when the sheet has no row for a term or has not
/// loaded. Any other term shows its name.
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


/// The picks the notice names, in the page's order: indication, medication, route, form and
/// dose type, as far as chosen.
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


/// The term of the body for each refusal. The products term also covers a rule dropped for
/// having no dose type, so its text names both.
let bodyTerm (refusal: OrderContextRefusal) =
    match refusal with
    | OrderContextRefusal.NoDoseRules -> Terms.``Prescribe Refusal No dose rules``
    | OrderContextRefusal.NoDoseRulesForPatient -> Terms.``Prescribe Refusal Patient``
    | OrderContextRefusal.NoProducts -> Terms.``Prescribe Refusal No products``


/// Whether the patient lacks a weight or a height, measured or estimated. When rules exist for
/// the picks, the server refuses such a patient before matching any rule.
let patientIncomplete (patient: Patient) =
    (patient |> Patient.getWeight).IsNone || (patient |> Patient.getHeight).IsNone


/// The notice for a refused context. None when the patient lacks a weight or height: the page
/// already says what to enter, and a notice blaming the rules would mislead.
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


/// The text under the notice's title: the body, then whom to tell.
let message (notice: Notice) = TermText.sentences [ notice.Body; notice.Contact ]
