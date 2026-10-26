namespace Informedica.GenPRES.Client.Core.MachinePolicies

/// Decides what the patient panel offers around a held patient context: whether the context is
/// held, the ways out the held dialog offers, and whether the panel can read the patient from
/// the EHR again. The context is held for an identified patient whose order plan has an order
/// that is new or changed since the version last opened or signed: every order a signed version
/// adds rests on one patient context, so the panel cannot change it until the plan is signed or
/// those orders go.
module HeldPanelPolicy =

    open Informedica.GenPRES.Client.Core.StateMachines
    open SessionMachine


    /// A way out of the held patient context, besides signing, which is the plan's own button.
    [<RequireQualifiedAccess>]
    type HeldAction =
        /// Remove the new and changed orders.
        | Remove
        /// Open the last signed order plan with this id; the new and changed orders go with it.
        | OpenLastSigned of id: string


    /// The patient context of an open or closing Session; none otherwise.
    let patientContext session =
        match session with
        | SessionView.Open opened
        | SessionView.Closing opened -> opened.PatientContext
        | _ -> None


    /// Whether the patient context is held: an identified patient, and the ids of the orders new or
    /// changed since the version last opened or signed not empty. Anonymous use and the url mode are
    /// never held.
    let held session (changed: string[]) =
        (patientContext session |> Option.bind _.Identity).IsSome
        && not (Array.isEmpty changed)


    /// The last signed order plan of the open Session, the one a refresh of the plan opens; none
    /// before the first signature, and none without an open Session.
    let lastSigned session =
        match session with
        | SessionView.Open opened -> opened.Head
        | _ -> None


    /// The ways out the held dialog offers: remove the new and changed orders, and open the last
    /// signed order plan when there is one.
    let heldActions session =
        [
            HeldAction.Remove
            match lastSigned session with
            | Some signed -> HeldAction.OpenLastSigned signed.Head.Id
            | None -> ()
        ]


    /// Whether the panel offers to read the patient from the EHR again: an open Session for a
    /// patient. A closing Session refreshes nothing: the refresh is sent with the open Session's
    /// token, and the Session takes a refresh only while it is open.
    let canRefresh session =
        match session with
        | SessionView.Open opened -> opened.PatientContext.IsSome
        | _ -> false
