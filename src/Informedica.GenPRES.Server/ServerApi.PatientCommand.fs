namespace ServerApi


/// The patient member: a patient data update, answered with the patient made complete.
module PatientCommand =

    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api

    module GestationalAge = Informedica.GenPRES.Shared.Models.GestationalAge


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


    /// The patient made complete at the inbound boundary: the need for the gestational age set from
    /// the age, the gestational age dropped when none is needed, the post-menstrual age calculated
    /// from the age and the gestational age, the adult mark set from the age and the choice of the
    /// user, then a weight or a height that is neither measured nor estimated estimated from them.
    /// The normal values are asked only when a measure is missing.
    let processCmd (env: AppEnv) (cmd: PatientCommand) =
        async {
            match cmd with
            | PatientCommand.ChangePatient pat ->
                let ga = pat.GestationalAge |> GestationalAge.value |> Age.gestationalAge pat.Age

                return
                    Ok(
                        { pat with
                            GestationalAge = ga
                            PostMenstrualAge = ga |> Age.postMenstrualAge pat.Age
                            AdultMark = pat.AdultMark |> Age.adultMark pat.Age
                        }
                        |> Patient.estimate env.normalValues
                    )
        }
