namespace ServerApi


/// The patient member: a patient data update, answered with the patient made complete.
module PatientCommand =

    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api

    module GestationalAge = Informedica.GenPRES.Shared.Models.GestationalAge


    /// Every patient change is refused while the server loads, since its estimate needs the normal
    /// values, also next to a measured weight and height.
    let gate (cmd: PatientCommand) =
        match cmd with
        | PatientCommand.ChangePatient _ -> Gate.RequiresLoaded


    /// The patient made complete at the inbound boundary: the need for the gestational age set from
    /// the age, the gestational age dropped when none is needed, the post-menstrual age calculated
    /// from the age and the gestational age, the adult mark set from the age and the choice of the
    /// user, then the full estimate of the weight and the height from them, next to a measured
    /// value too.
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
