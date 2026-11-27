namespace Informedica.GenPRES.Client.Core.Helpers

/// The alerts the snackbar shows, with their severity and the English text of the alert terms.
module Alert =

    open Informedica.GenPRES.Client.Core.Models

    open Informedica.GenPRES.Shared.Types


    /// How serious a sentence on the snackbar is.
    [<RequireQualifiedAccess>]
    type Severity =
        | Error
        | Warning
        | Success


    /// The order counts in the summary after a patient data update.
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
        /// The medication names of the interactions page did not load after three tries.
        | DrugNamesNotLoaded
        /// The interaction check found this number of interactions.
        | InteractionsFound of int
        /// The server did not take the admin password.
        | InvalidPassword
        /// The request to close the session did not reach the server; the session stays open.
        | CloseFailed
        /// The PIN did not reach the server; the form shows again as it was.
        | PinNotSent
        /// A request failed; the error banner says what the server answered.
        | RequestFailed
        /// A medication was chosen without a patient and is not used.
        | NoPatientForMedication
        /// A launch url arrived during signing and was not opened.
        | LaunchNotOpened
        /// A patient request failed, with the first server error, or None when the server gave no
        /// error text.
        | PatientChangeFailed of string option
        /// A request of the workbench or a slot failed, with the first server error, or None when the
        /// server gave no error text.
        | WorkbenchFailed of string option
        /// The signature did not reach the server.
        | SigningSendFailed
        /// The order plan was signed.
        | OrderPlanSigned of SignedOrderPlan
        /// The signature was refused.
        | SigningRefused of SigningRefusal
        /// The requested signed order plan is opened.
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


    /// Returns the English text of an alert term, used when the sheet has no translation or has not
    /// loaded. Returns the name of any other term.
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
