namespace Informedica.GenPRES.Client.Core.MachinePolicies

/// What the client reads of the patient of the Session: the patient context, who the patient is,
/// the last signed order plan, and whether the panel can read the patient from the EHR again.
module SessionPatientPolicy =

    open Informedica.GenPRES.Client.Core.StateMachines
    open SessionMachine


    /// The patient context of an open or closing Session; none otherwise.
    let patientContext session =
        match session with
        | SessionView.Open opened
        | SessionView.Closing opened -> opened.PatientContext
        | _ -> None


    /// Who the EHR says the patient is; none for a patient it did not identify.
    let identity session = patientContext session |> Option.bind _.Identity


    /// Whether the EHR identified the patient.
    let identified session = (identity session).IsSome


    /// The last signed order plan of the open Session, the one a refresh of the plan opens; none
    /// before the first signature, and none without an open Session.
    let lastSigned session =
        match session with
        | SessionView.Open opened -> opened.Head
        | _ -> None


    /// Whether the panel offers to read the patient from the EHR again: an open Session for a
    /// patient. A closing Session refreshes nothing: the refresh is sent with the open Session's
    /// token, and the Session takes a refresh only while it is open.
    let canRefresh session =
        match session with
        | SessionView.Open opened -> opened.PatientContext.IsSome
        | _ -> false
