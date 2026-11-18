namespace ServerApi

open Informedica.GenPRES.Shared.Types


/// Handles a formulary request.
module FormularyCommand =

    /// Returns a short name for the log instead of the whole request.
    let toString (_: Formulary) = "Formulary"


    /// Returns the formulary. Returns the messages for invalid patient data. Without patient data
    /// the formulary is not filtered on a patient.
    let processCmd (env: AppEnv) (form: Formulary) =
        match Patient.patientOption form.PatientData with
        | Ok _ -> env.formulary.getFormulary form
        | Error errs -> async { return Error errs }


/// Handles a parenteralia request.
module ParenteraliaCommand =

    let toString (_: Parenteralia) = "Parenteralia"


    let processCmd (env: AppEnv) (par: Parenteralia) = env.formulary.getParenteralia par
