namespace ServerApi


/// The patient member: a patient change, answered with the patient made complete.
module PatientCommand =

    open Shared.Types
    open Shared.Api


    /// The patient the change carries mapped.
    let patients (f: Patient -> Patient) (cmd: PatientCommand) =
        match cmd with
        | PatientCommand.ChangePatient pat -> PatientCommand.ChangePatient(f pat)


    /// The patient the request edits: the change's own.
    let patientOf (cmd: PatientCommand) =
        match cmd with
        | PatientCommand.ChangePatient pat -> Some pat


    /// The patient as it reaches the member: Compute.bound has put the Session's age on an
    /// identified patient and estimated the weight and height it lacks.
    let processCmd (cmd: PatientCommand) =
        match cmd with
        | PatientCommand.ChangePatient pat -> async { return Ok pat }
