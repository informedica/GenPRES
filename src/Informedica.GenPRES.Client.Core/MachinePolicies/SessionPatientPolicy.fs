namespace Informedica.GenPRES.Client.Core.MachinePolicies

/// Reads the patient of the Session: the patient context, the patient identity, the last signed order
/// plan, and whether the panel can read the patient from the EHR again.
module SessionPatientPolicy =

    open Informedica.GenPRES.Client.Core.StateMachines
    open SessionMachine


    /// The patient context of an open or closing Session; None otherwise.
    let patientContext session =
        match session with
        | SessionView.Open opened
        | SessionView.Closing opened -> opened.PatientContext
        | _ -> None


    /// The patient identity from the EHR; None when the EHR did not identify the patient.
    let identity session = patientContext session |> Option.bind _.PatientIdentity


    /// Whether the EHR identified the patient.
    let identified session = (identity session).IsSome


    /// The last signed order plan of the open Session, which a plan refresh opens. None before the first
    /// signature, and None without an open Session.
    let lastSigned session =
        match session with
        | SessionView.Open opened -> opened.Head
        | _ -> None


    /// Whether the panel offers to read the patient from the EHR again: only for an open Session with a
    /// patient context. A closing Session cannot refresh, because the refresh uses the token of the open
    /// Session and the Session accepts a refresh only while it is open.
    let canRefresh session =
        match session with
        | SessionView.Open opened -> opened.PatientContext.IsSome
        | _ -> false
