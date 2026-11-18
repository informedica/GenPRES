namespace Informedica.GenPRES.Client.Core.Policies


/// Decides what the prescribing page says when the server refuses an order context. The texts
/// are Terms, translated by the caller.
module OrderContextRefusalPolicy =

    open Informedica.GenPRES.Client.Core.Models

    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models


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
        | Terms.``Prescribe Refusal Measurement`` ->
            "The dose rules for {0} need a weight, or a weight and a height, that this patient does not have. Enter them to see a dose"
        | Terms.``Prescribe Refusal No diluent`` ->
            "For {0}, each medication is given undiluted, and its concentration limit allows none of its product concentrations"
        | Terms.``Prescribe Refusal Not solved`` ->
            "This change cannot be applied: the order cannot be calculated with it. The order stays as it was"
        | Terms.``Prescribe Refusal No adjusted dose`` ->
            "This change cannot be applied: the order has no dose per kg or per m2. The order stays as it was"
        | Terms.``Prescribe Refusal Contact`` ->
            "Report this to the pharmacy or the application manager, so that the rule can be added"
        | term -> $"{term}"


    /// Joins the chosen filter values in page order: indication, medication, route, form and dose type.
    let picks (filter: Filter) =
        [
            filter.Indication
            filter.Generic
            filter.Route
            filter.Form
            filter.DoseType |> Option.map DoseTypeText.doseTypeToDescription
        ]
        |> List.choose id
        |> String.concat ", "


    /// The body term for each refusal. The products term also covers a rule without a dose type, so its
    /// text names both.
    let bodyTerm (refusal: OrderContextRefusal) =
        match refusal with
        | OrderContextRefusal.NoDoseRules -> Terms.``Prescribe Refusal No dose rules``
        | OrderContextRefusal.NoDoseRulesForPatient -> Terms.``Prescribe Refusal Patient``
        | OrderContextRefusal.NoProducts -> Terms.``Prescribe Refusal No products``
        | OrderContextRefusal.MeasurementMissing -> Terms.``Prescribe Refusal Measurement``
        | OrderContextRefusal.NoDiluent -> Terms.``Prescribe Refusal No diluent``
        | OrderContextRefusal.NotSolved -> Terms.``Prescribe Refusal Not solved``
        | OrderContextRefusal.NoAdjustedDose -> Terms.``Prescribe Refusal No adjusted dose``


    /// Whether the refusal is of a change in the dose dialog, not of the picks: the order stays as it
    /// was sent, and the snackbar says why.
    let refusesChange (refusal: OrderContextRefusal) =
        match refusal with
        | OrderContextRefusal.NotSolved
        | OrderContextRefusal.NoAdjustedDose -> true
        | _ -> false


    /// Returns the notice for a refused context. None when the patient data cannot select rules, such as a
    /// child without a weight: the page already asks for it, and a notice blaming the rules would mislead.
    /// None for a refused change, which the snackbar reports. A missing measurement has no contact line,
    /// because the rules are right and the patient needs the measurement.
    let notice (tr: Terms -> string) (ctx: OrderContext) (refusal: OrderContextRefusal) : Notice option =
        match refusal with
        | OrderContextRefusal.NoDoseRulesForPatient when ctx.PatientData |> PatientData.selectsRules |> not -> None
        | _ when refusal |> refusesChange -> None
        | _ ->
            Some
                {
                    Title = tr Terms.``Prescribe Refusal``
                    Body = tr (bodyTerm refusal) |> TermText.fill [ picks ctx.Filter ]
                    Contact =
                        match refusal with
                        | OrderContextRefusal.MeasurementMissing -> ""
                        | _ -> tr Terms.``Prescribe Refusal Contact``
                }


    /// The text under the notice's title: the body, then whom to tell.
    let message (notice: Notice) = TermText.sentences [ notice.Body; notice.Contact ]
