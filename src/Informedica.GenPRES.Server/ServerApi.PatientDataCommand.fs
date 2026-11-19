namespace ServerApi


/// Handles patient data edited on the panel and answers with the completed patient data.
module PatientDataCommand =

    open Informedica.GenPRES.Shared.Api


    /// Refuses patient data while the server loads, because GenCORE needs the resources to complete
    /// it.
    let gate (cmd: PatientDataCommand) =
        match cmd with
        | PatientDataCommand.ChangePatientData _
        | PatientDataCommand.EditPatientData _ -> Gate.RequiresLoaded


    /// Completes the patient data through GenCORE with the resources. For an edit command, GenCORE
    /// applies the edit first. Returns the GenCORE messages when a value is invalid.
    let processCmd (env: AppEnv) (cmd: PatientDataCommand) =
        async {
            match cmd with
            | PatientDataCommand.ChangePatientData pat ->
                return pat |> Patient.complete (env.today ()) (env.patientDataResources ())
            | PatientDataCommand.EditPatientData(edit, pat) ->
                return pat |> Patient.edit (env.today ()) (env.patientDataResources ()) edit
        }
