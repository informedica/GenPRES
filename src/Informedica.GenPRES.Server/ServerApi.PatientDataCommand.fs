namespace ServerApi


/// Handles patient data edited on the panel and answers with the completed patient data.
module PatientDataCommand =

    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api

    module GestationalAge = Informedica.GenPRES.Shared.Models.GestationalAge


    /// Refuses patient data while the server loads: the estimates need the normal values, also for
    /// a measured weight and height.
    let gate (cmd: PatientDataCommand) =
        match cmd with
        | PatientDataCommand.ChangePatientData _ -> Gate.RequiresLoaded


    /// Completes the patient data: sets the need for the gestational age from the age, drops a
    /// gestational age that is not needed, calculates the post-menstrual age, sets the adult mark,
    /// and adds the weight and height estimates.
    let processCmd (env: AppEnv) (cmd: PatientDataCommand) =
        async {
            match cmd with
            | PatientDataCommand.ChangePatientData pat ->
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
