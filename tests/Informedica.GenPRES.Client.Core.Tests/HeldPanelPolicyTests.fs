namespace Informedica.GenPRES.Client.Core.Tests


/// What the patient panel offers around a held patient context.
module HeldPanelPolicyTests =

    open Expecto
    open Expecto.Flip
    open SessionMachine
    open HeldPanelPolicy
    open SessionMachineTests


    /// A Session for a patient the EHR identified.
    let identified =
        { full with PatientContext = full.PatientContext |> Option.map (fun c -> { c with Identity = Some who }) }

    /// The same Session after a signature: the version signed is its head.
    let signedOnce = { identified with Head = Some signedVersion }


    [<Tests>]
    let tests =
        testList
            "HeldPanelPolicy"
            [
                test "held for an identified patient with a new or changed order, and not otherwise" {
                    held (SessionView.Open identified) [| "c1" |]
                    |> Expect.isTrue "identified, changed"
                    held (SessionView.Closing identified) [| "c1" |] |> Expect.isTrue "closing"
                    held (SessionView.Open identified) [||] |> Expect.isFalse "nothing changed"
                    held (SessionView.Open full) [| "c1" |] |> Expect.isFalse "not identified"
                    held SessionView.Anonymous [| "c1" |] |> Expect.isFalse "anonymous"
                }

                test "the held dialog offers remove alone without a signed order plan" {
                    heldActions (SessionView.Open identified)
                    |> Expect.equal "remove" [ HeldAction.Remove ]
                }

                test "the held dialog offers to open the last signed order plan once there is one" {
                    heldActions (SessionView.Open signedOnce)
                    |> Expect.equal "remove, open" [ HeldAction.Remove; HeldAction.OpenLastSigned "plan-5" ]
                }

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
