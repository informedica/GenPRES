// Compute bound to the Session (plan 635), PR 2: the head of the record into the cart at open
// (Rule 19, second half). The Session already opens with the head's id (`OpenedWith`, plan 622);
// now the version itself travels to the client, which loads its orders into the cart at a
// launch, a resume and an enrolment, and after a signature the version just signed is what a
// resume opens on.
//
// Script-first draft (script-only policy) of:
//   - the wire: `SessionOpened.Head: SignedOrderPlan option` → `Shared/Types.fs` (the head
//     types move above `SessionOpened`, which now refers to them);
//   - `Hop.openWith` filling `Head` from `headOf`, and `Hop.commit` setting it to the version
//     appended → `Adapters.fs`.
// The client side (`SessionEffect.LoadCart`, emitted by `Session.opened`; App.fs loading the
// cart through `FilterOrderPlan`) is edited directly, as UI code.
//
// Only what changes is re-stated: the two places the head is written. Run: `dotnet fsi
// Compute.fsx` from this directory (build first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open System
open Shared.Types
open Shared.Models


// ---------------------------------------------------------------------------------------------
// Wire (→ Shared/Types.fs)
// ---------------------------------------------------------------------------------------------

/// What the client keeps of an open Session (launch step 6), now with the version of the
/// record it opened with (Rule 19): its orders go into the cart, and Rule 20 is checked against
/// its id. `None` from nothing.
type SessionOpened =
    {
        User: UserContext option
        PatientContext: PatientContext option
        OpenedToken: OpenedToken option
        KeyThumbprint: string option
        Head: SignedOrderPlan option
    }


// ---------------------------------------------------------------------------------------------
// The head written at open and at commit (→ ServerApi.Adapters.fs, `Hop`)
// ---------------------------------------------------------------------------------------------

module Hop =

    /// The two fields `openWith` derives from the record (Rule 19): the version and its id.
    let headAtOpen (headOf: string -> SignedOrderPlan option) (patientId: string) =
        let head = headOf patientId
        head, head |> Option.map _.Head.Id


    /// What `commit` writes on the Session once the version is appended (Rule 34): the fresh
    /// token, and the version just signed as the one the Session opened with.
    let afterCommit (token: OpenedToken) (plan: SignedOrderPlan) (session: SessionOpened) =
        { session with
            OpenedToken = Some token
            Head = Some plan
        }


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip


let t0 = DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc)

let prescriber =
    {
        UserId = "prescriber"
        DisplayName = "Stub Prescriber"
        Role = UserRole.Prescriber
    }


let signed no : SignedOrderPlan =
    {
        Head =
            {
                Id = $"plan-{no}"
                No = no
                By = prescriber
                SignedAt = t0
            }
        PatientId = "pat-1"
        Base = if no > 1 then Some $"plan-{no - 1}" else None
        Scenarios = [||]
        Patient = Patient.empty
        Verified = true
    }


let records = Map.ofList [ "pat-1", [ signed 2; signed 1 ] ]
let headOf patientId = records |> Map.tryFind patientId |> Option.bind List.tryHead

let opened: SessionOpened =
    {
        User = Some prescriber
        PatientContext =
            Some
                {
                    PatientId = "pat-1"
                    Patient = Patient.empty
                }
        OpenedToken = Some(OpenedToken "opened-1")
        KeyThumbprint = Some "t"
        Head = None
    }


let tests =
    testList
        "the head into the cart (Rule 19)"
        [
            test "a Session over a record opens with the newest version and its id" {
                Hop.headAtOpen headOf "pat-1"
                |> Expect.equal "the head" (Some(signed 2), Some "plan-2")
            }

            test "a Session over no record opens from nothing" {
                Hop.headAtOpen headOf "pat-9" |> Expect.equal "nothing" (None, None)
            }

            test "after a signature the Session holds the fresh token and the version just signed" {
                let after = opened |> Hop.afterCommit (OpenedToken "opened-2") (signed 3)
                after.OpenedToken |> Expect.equal "re-minted" (Some(OpenedToken "opened-2"))
                after.Head |> Expect.equal "the version signed" (Some(signed 3))
                after.User |> Expect.equal "the rest untouched" opened.User
            }
        ]


runTestsWithCLIArgs [] [||] tests |> ignore
