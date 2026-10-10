/// What the snackbar can show: one case per sentence, and the severity it is shown with. The text
/// of a case is written where the client turns it into words.
module Alert

open Shared.Types


/// How serious a sentence on the snackbar is.
[<RequireQualifiedAccess>]
type Severity =
    | Error
    | Warning
    | Success


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
    /// The patient change failed, with the first error the server gave.
    | PatientChangeFailed of string
    /// The workbench failed, with the first error the server gave.
    | WorkbenchFailed of string
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
    | Alert.PatientRefreshFailed -> Severity.Warning
    | Alert.OrderPlanSigned _
    | Alert.SignedPlanOpened _ -> Severity.Success
