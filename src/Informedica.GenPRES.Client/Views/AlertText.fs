namespace Views


/// The words of what the snackbar shows.
module AlertText =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Client.Core.MachinePolicies

    open Informedica.GenPRES.Shared


    /// The sentence of an alert; terms gives the localized text of a term, or the default given
    /// when the term has none.
    let text (terms: string -> Terms -> string) alert =
        // a term of the signing vocabulary falls back to the English the policy gives
        let signing term = terms (SigningPolicy.english term) term
        let alertTerm term = terms (Alert.english term) term

        match alert with
        | Alert.Alert.DrugNamesNotLoaded -> alertTerm Terms.``Alert Interaction Names Not Loaded``
        | Alert.Alert.InteractionsFound n -> alertTerm Terms.``Alert Interactions Found`` |> TermText.fill [ string n ]
        | Alert.Alert.InvalidPassword -> alertTerm Terms.``Invalid password``
        | Alert.Alert.CloseFailed -> alertTerm Terms.``Alert Close Failed``
        | Alert.Alert.PinNotSent -> alertTerm Terms.``Alert Pin Not Sent``
        | Alert.Alert.RequestFailed -> alertTerm Terms.``Alert Request Failed``
        | Alert.Alert.NoPatientForMedication -> terms "Voer patient gegevens in" Terms.``Patient enter patient data``
        | Alert.Alert.LaunchNotOpened ->
            let text =
                "De start vanuit het EPD is niet geopend: er wordt ondertekend. "
                + "Open de patiënt opnieuw vanuit het EPD."

            terms text Terms.``Url Launch Signing``
        | Alert.Alert.PatientChangeFailed reason
        | Alert.Alert.WorkbenchFailed reason ->
            reason
            |> Option.defaultWith (fun () -> alertTerm Terms.``Alert Something Went Wrong``)
        | Alert.Alert.SigningSendFailed -> signing Terms.``Signing Send Failed``
        | Alert.Alert.OrderPlanSigned signed -> SigningPolicy.signedSentence signing signed
        | Alert.Alert.SigningRefused refusal -> SigningPolicy.refusalSentence signing refusal
        | Alert.Alert.SignedPlanOpened head -> SigningPolicy.versionOpenedSentence signing head
        | Alert.Alert.NewerSignedPlan head -> SigningPolicy.newerPlanSentence signing head
        | Alert.Alert.PatientRefreshFailed -> signing Terms.``Session Refresh Failed``
        | Alert.Alert.ChangeNotApplied refusal ->
            let term = OrderContextRefusalPolicy.bodyTerm refusal

            terms (OrderContextRefusalPolicy.english term) term
        | Alert.Alert.PatientDataChanged summary ->
            terms PatientDataSummaryPolicy.english Terms.``Patient Data Changed``
            |> PatientDataSummaryPolicy.sentence summary


    /// The severity as the snackbar names it.
    let severity alert =
        match Alert.severity alert with
        | Alert.Severity.Error -> "error"
        | Alert.Severity.Warning -> "warning"
        | Alert.Severity.Success -> "success"
