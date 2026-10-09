namespace Views


/// The words of what the snackbar shows.
module AlertText =

    open Shared


    /// The sentence of an alert; terms gives the localized text of a term, or the default given
    /// when the term has none.
    let text (terms: string -> Terms -> string) alert =
        match alert with
        | Alert.Alert.DrugNamesNotLoaded -> "Interactie medicatie namen konden niet worden geladen"


    /// The severity as the snackbar names it.
    let severity alert =
        match Alert.severity alert with
        | Alert.Severity.Error -> "error"
        | Alert.Severity.Warning -> "warning"
