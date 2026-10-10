/// What the snackbar can show: one case per sentence, and the severity it is shown with. The text
/// of a case is written where the client turns it into words.
module Alert


/// How serious a sentence on the snackbar is.
[<RequireQualifiedAccess>]
type Severity =
    | Error
    | Warning


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


/// The severity an alert is shown with.
let severity alert =
    match alert with
    | Alert.InvalidPassword
    | Alert.CloseFailed
    | Alert.PinNotSent -> Severity.Error
    | Alert.DrugNamesNotLoaded
    | Alert.InteractionsFound _ -> Severity.Warning
