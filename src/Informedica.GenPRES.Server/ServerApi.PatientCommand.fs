namespace ServerApi


/// The patient member: a patient data update, answered with the patient made complete.
module PatientCommand =

    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api


    /// A patient that lacks a weight or a height is refused while the server loads, since its
    /// estimate needs the normal values; any other patient is answered.
    let gate (cmd: PatientCommand) =
        match cmd with
        | PatientCommand.ChangePatient pat ->
            let lacks (measured: 'a option) (estimated: 'a option) = measured.IsNone && estimated.IsNone

            if
                lacks pat.Weight.Measured pat.Weight.Estimated
                || lacks pat.Height.Measured pat.Height.Estimated
            then
                Gate.RequiresLoaded
            else
                Gate.Open


    /// The patient made complete at the inbound boundary: a weight or a height that is neither
    /// measured nor estimated is estimated from the age the patient comes with. The normal values
    /// are asked only when a measure is missing.
    let processCmd (env: AppEnv) (cmd: PatientCommand) =
        async {
            match cmd with
            | PatientCommand.ChangePatient pat -> return Ok(pat |> Patient.estimate env.normalValues)
        }
