namespace Components


/// What every page says when the patient data is not enough for it: one notice, decided by
/// PatientReadiness from the draft alone, in the same words and the same place on every page.
/// Nothing while the page has what it needs; the patient panel above is where the data goes in.
module PatientNotice =


    open Fable.Core
    open Feliz
    open Shared


    /// The page's environment, and what the page needs the patient for.
    type Props =
        {|
            appEnv: obj
            needs: PatientReadiness.Needs
        |}


    let private noticeSx = {| marginBottom = 1 |}


    [<JSX.Component>]
    let View (props: Props) =
        let envPatient = AppEnv.asEnv<AppEnv.IPatient> props.appEnv
        let localizationTerms = (AppEnv.asEnv<AppEnv.ILocalization> props.appEnv).LocalizationTerms

        let context: Global.Context = React.useContext Global.context

        // the sheet's translation in the User's language, else the policy's English
        let tr term =
            Global.getLocalizedTerm localizationTerms context.Localization (PatientReadiness.english term) term

        match
            envPatient.Draft
            |> PatientReadiness.readiness
            |> PatientReadiness.notice props.needs
        with
        | None -> null
        | Some words ->
            let notice =
                Notice.View
                    {|
                        kind = Notice.Kind.Warning
                        title = None
                        message = words |> PatientReadiness.message tr
                        action = None
                        onClose = None
                    |}

            JSX.jsx
                $"""
            import Box from '@mui/material/Box';

            <Box sx={noticeSx}>{notice}</Box>
            """
