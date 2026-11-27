namespace Components


/// Shows a warning when the patient data is not enough for the page, with the same words and in
/// the same place on every page. PatientReadinessPolicy decides the notice from the patient draft.
/// Shows nothing when the page has the patient data it needs.
module PatientNotice =

    open Informedica.GenPRES.Client.Core.Policies


    open Fable.Core
    open Feliz
    open Informedica.GenPRES.Shared


    /// The app environment, and the patient data the page needs.
    type Props =
        {|
            appEnv: obj
            needs: PatientReadinessPolicy.Needs
        |}


    let private noticeSx = {| marginBottom = 1 |}


    [<JSX.Component>]
    let View (props: Props) =
        let envPatient = AppEnv.asEnv<AppEnv.IPatient> props.appEnv
        let localizationTerms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        let context: Global.Context = React.useContext Global.context

        // the translation from the sheet in the language of the user, else the English text of the policy
        let tr term =
            Global.getLocalizedTerm localizationTerms context.Localization (PatientReadinessPolicy.english term) term

        match
            envPatient.Draft
            |> PatientReadinessPolicy.readiness
            |> PatientReadinessPolicy.notice props.needs
        with
        | None -> null
        | Some words ->
            let notice =
                Notice.View
                    {|
                        kind = Notice.Kind.Warning
                        title = None
                        message = words |> PatientReadinessPolicy.message tr
                        action = None
                        onClose = None
                    |}

            JSX.jsx
                $"""
            import Box from '@mui/material/Box';

            <Box sx={noticeSx}>{notice}</Box>
            """
