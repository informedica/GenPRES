namespace Informedica.GenPRES.Client.Core.Tests.MachinePolicies


/// What the patient panel reads of the Session.
module SessionPatientPolicyTests =

    open Informedica.GenPRES.Client.Core.StateMachines
    open Informedica.GenPRES.Client.Core.MachinePolicies
    open Informedica.GenPRES.Client.Core.Tests.StateMachines

    open Expecto
    open Expecto.Flip
    open SessionMachine
    open SessionPatientPolicy
    open SessionMachineTests


    /// A Session for a patient the EHR identified.
    let identified =
        { full with PatientContext = full.PatientContext |> Option.map (fun c -> { c with Identity = Some who }) }

    /// The same Session after a signature: the version signed is its head.
    let signedOnce = { identified with Head = Some signedVersion }


    [<Tests>]
    let tests =
        testList
            "SessionPatientPolicy"
            [
                test "the last signed order plan is the open Session's head" {
                    lastSigned (SessionView.Open signedOnce)
                    |> Expect.equal "the head" (Some signedVersion)
                    lastSigned (SessionView.Closing signedOnce) |> Expect.isNone "closing"
                    lastSigned SessionView.Anonymous |> Expect.isNone "anonymous"
                }

                test "the panel refreshes from the EHR with an open Session for a patient" {
                    canRefresh (SessionView.Open full) |> Expect.isTrue "open"
                    canRefresh (SessionView.Open(sessionWith None None))
                    |> Expect.isFalse "no patient"
                    canRefresh (SessionView.Closing full) |> Expect.isFalse "closing"
                    canRefresh SessionView.Anonymous |> Expect.isFalse "anonymous"
                }
            ]
