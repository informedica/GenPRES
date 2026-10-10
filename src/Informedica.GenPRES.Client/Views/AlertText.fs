namespace Views


/// The words of what the snackbar shows.
module AlertText =

    open Shared


    /// The sentence of an alert; terms gives the localized text of a term, or the default given
    /// when the term has none.
    let text (terms: string -> Terms -> string) alert =
        // a term of the signing vocabulary falls back to the English the policy gives
        let signing term = terms (SigningPolicy.english term) term

        match alert with
        | Alert.Alert.DrugNamesNotLoaded -> "Interactie medicatie namen konden niet worden geladen"
        | Alert.Alert.InteractionsFound n -> $"Er zijn %i{n} interactie(s) gevonden"
        | Alert.Alert.InvalidPassword -> "Invalid password"
        | Alert.Alert.CloseFailed -> "De sessie kon niet worden gesloten. Probeer het opnieuw."
        | Alert.Alert.PinNotSent -> "De pincode kon niet worden verstuurd. Probeer het opnieuw."
        | Alert.Alert.RequestFailed -> "Er ging iets mis, herladen"
        | Alert.Alert.NoPatientForMedication -> terms "Voer patient gegevens in" Terms.``Patient enter patient data``
        | Alert.Alert.LaunchNotOpened ->
            let text =
                "De start vanuit het EPD is niet geopend: er wordt ondertekend. "
                + "Open de patiënt opnieuw vanuit het EPD."

            terms text Terms.``Url Launch Signing``
        | Alert.Alert.PatientChangeFailed reason
        | Alert.Alert.WorkbenchFailed reason -> reason
        | Alert.Alert.SigningSendFailed -> signing Terms.``Signing Send Failed``
        | Alert.Alert.OrderPlanSigned signed -> SigningPolicy.signedSentence signing signed
        | Alert.Alert.SigningRefused refusal -> SigningPolicy.refusalSentence signing refusal
        | Alert.Alert.SignedPlanOpened head -> SigningPolicy.versionOpenedSentence signing head
        | Alert.Alert.NewerSignedPlan head -> SigningPolicy.newerPlanSentence signing head
        | Alert.Alert.PatientRefreshFailed -> signing Terms.``Session Refresh Failed``


    /// The severity as the snackbar names it.
    let severity alert =
        match Alert.severity alert with
        | Alert.Severity.Error -> "error"
        | Alert.Severity.Warning -> "warning"
        | Alert.Severity.Success -> "success"
