namespace ServerApi


/// The patient member: a patient change, answered with the patient made complete.
module PatientCommand =

    open Shared.Types
    open Shared.Api


    /// The patient the request edits: the change's own.
    let patientOf (cmd: PatientCommand) =
        match cmd with
        | PatientCommand.ChangePatient pat -> Some pat


    /// The patient made complete at the inbound boundary: an identified patient at the age the
    /// Session holds, whatever age the client sent, an anonymous one at the client's; for both, a
    /// weight or a height that is neither measured nor estimated is estimated from the age. The
    /// normal values are asked only when a measure is missing. A store that fails while the age
    /// is asked is a failure.
    let processCmd (env: AppEnv) (cookie: SessionCookie) (cmd: PatientCommand) =
        async {
            match cmd with
            | PatientCommand.ChangePatient pat ->
                let! age =
                    match cookie.read () with
                    | Some id -> env.session.age id
                    | None -> async { return Ok None }

                return
                    match age with
                    | Ok age -> Ok(pat |> Patient.aged age |> Patient.estimate env.normalValues)
                    | Error refusal -> Error [| $"The Session's age could not be read: %A{refusal}" |]
        }
