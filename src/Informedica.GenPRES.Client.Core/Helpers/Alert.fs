namespace Informedica.GenPRES.Client.Core.Helpers

/// What the snackbar can show: one case per sentence, the severity it is shown with, and the
/// English of the alert terms; the client translates them.
module Alert =

    open Informedica.GenPRES.Client.Core.Models

    open Informedica.GenPRES.Shared.Types


    /// How serious a sentence on the snackbar is.
    [<RequireQualifiedAccess>]
    type Severity =
        | Error
        | Warning
        | Success


    /// What the summary at a change of patient data says of the orders in the plan.
    type PatientDataSummary =
        {
            /// The orders in the plan.
            Orders: int
            /// The orders with a notification or the general notice.
            Marked: int
        }


    /// A sentence the snackbar shows.
    [<RequireQualifiedAccess>]
    type Alert =
        /// The drug names of the interactions page did not load, after three tries.
        | DrugNamesNotLoaded
        /// The interactions checked found this many.
        | InteractionsFound of int
        /// The server did not take the admin password.
        | InvalidPassword
        /// The close of the session never reached the server; the session stays open.
        | CloseFailed
        /// The PIN never reached the server; the form comes back as it was.
        | PinNotSent
        /// A request failed; the error banner says what the server answered.
        | RequestFailed
        /// A medication was chosen without a patient, and dropped.
        | NoPatientForMedication
        /// A launch url came while a signature was under way, and was not opened.
        | LaunchNotOpened
        /// The patient change failed, with the first error the server gave; none when the server
        /// gave no error text.
        | PatientChangeFailed of string option
        /// The workbench failed, with the first error the server gave; none when the server gave no
        /// error text.
        | WorkbenchFailed of string option
        /// The signature did not reach the server.
        | SigningSendFailed
        /// The order plan was signed.
        | OrderPlanSigned of SignedOrderPlan
        /// The signature was refused.
        | SigningRefused of SigningRefusal
        /// The signed order plan asked for is open.
        | SignedPlanOpened of OrderPlanHead
        /// A newer signed order plan exists.
        | NewerSignedPlan of OrderPlanHead
        /// The patient could not be read again from the EHR; the orders stay as they are.
        | PatientRefreshFailed
        /// The change could not be applied, for the reason the server gave; the order stays as it
        /// was.
        | ChangeNotApplied of OrderContextRefusal
        /// The patient data changed: the orders were updated and the workbench was cleared.
        | PatientDataChanged of PatientDataSummary


    /// The severity an alert is shown with.
    let severity alert =
        match alert with
        | Alert.InvalidPassword
        | Alert.CloseFailed
        | Alert.PinNotSent
        | Alert.RequestFailed
        | Alert.SigningSendFailed -> Severity.Error
        | Alert.DrugNamesNotLoaded
        | Alert.InteractionsFound _
        | Alert.NoPatientForMedication
        | Alert.LaunchNotOpened
        | Alert.PatientChangeFailed _
        | Alert.WorkbenchFailed _
        | Alert.SigningRefused _
        | Alert.NewerSignedPlan _
        | Alert.PatientRefreshFailed
        | Alert.ChangeNotApplied _ -> Severity.Warning
        | Alert.PatientDataChanged summary when summary.Marked > 0 -> Severity.Warning
        | Alert.PatientDataChanged _
        | Alert.OrderPlanSigned _
        | Alert.SignedPlanOpened _ -> Severity.Success


    /// The English of the alert terms, used when the sheet has no row for a term or has not
    /// loaded. Any other term shows its name.
    let english term =
        match term with
        | Terms.``Alert Interaction Names Not Loaded`` ->
            "The medication names for the interaction check could not be loaded."
        | Terms.``Alert Interactions Found`` -> "{0} interaction(s) found."
        | Terms.``Invalid password`` -> "Invalid password"
        | Terms.``Alert Close Failed`` -> "The session could not be closed. Try again."
        | Terms.``Alert Pin Not Sent`` -> "The PIN could not be sent. Try again."
        | Terms.``Alert Request Failed`` -> "Something went wrong; reload the page."
        | Terms.``Alert Something Went Wrong`` -> "Something went wrong."
        | _ -> $"{term}"
