module Informedica.GenPRES.Client.Core.Tests.MachinePolicies.SigningPolicyTests

open Informedica.GenPRES.Client.Core.Models
open Informedica.GenPRES.Client.Core.Helpers
open Informedica.GenPRES.Client.Core.Policies
open Informedica.GenPRES.Client.Core.StateMachines
open Informedica.GenPRES.Client.Core.MachinePolicies
open System
open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared
open Informedica.GenPRES.Shared.Types
open SessionMachine
open SigningMachine
open SigningPolicy


module Fixtures =

    let patient = Informedica.GenPRES.Shared.Models.Patient.empty

    let userOf role =
        {
            UserId = "u"
            DisplayName = "Stub Prescriber B"
            Role = role
        }

    let openedAs role withPatient =
        {
            User = Some(userOf role)
            PatientContext =
                if withPatient then
                    Some
                        {
                            PatientId = "p"
                            Identity = None
                            Patient = Some patient
                        }
                else
                    None
            OpenedToken = Some(OpenedToken "t")
            KeyThumbprint = Some "thumb"
            Head = None
        }

    let head =
        {
            Id = "plan-2"
            No = 2
            By = userOf UserRole.Prescriber
            SignedAt = DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc)
        }

    /// A translator that shows which term was asked for, so a test can assert terms, not prose.
    let named (term: Terms) = $"<{term}>"


open Fixtures


[<Tests>]
let tests =
    testList
        "SigningPolicy"
        [
            test "every signing term has an English default that is not its name" {
                for term in
                    [
                        Terms.``Signing Sign``
                        Terms.``Signing Dialog Title``
                        Terms.``Signing Dialog Text``
                        Terms.``Signing Pin``
                        Terms.``Signing Cancel``
                        Terms.``Signing Proceed``
                        Terms.``Signing Signed``
                        Terms.``Signing Data Changed``
                        Terms.``Signing Take Ehr Data``
                        Terms.``Signing Keep Plan Data``
                        Terms.``Signing Notice Ehr``
                        Terms.``Signing Data Unverified``
                        Terms.``Signing Refusal No Session``
                        Terms.``Signing Refusal No Patient``
                        Terms.``Signing Refusal Not Prescriber``
                        Terms.``Signing Refusal Blocked``
                        Terms.``Signing Refusal Stale Token``
                        Terms.``Signing Refusal Challenge Mismatch``
                        Terms.``Signing Refusal Challenge Expired``
                        Terms.``Signing Refusal Pin Wrong``
                        Terms.``Signing Refusal Pin Limit``
                        Terms.``Signing Refusal Locked``
                        Terms.``Signing Refusal Store Failed``
                        Terms.``Signing Refusal Plan Unreadable``
                        Terms.``Signing Send Failed``
                        Terms.``Signing New``
                        Terms.``Signing Changed``
                        Terms.``Signing Removed``
                        Terms.``Signing No Changes``
                    ] do
                    english term |> Expect.notEqual $"default for {term}" $"{term}"

                english Terms.``Session Ending Pin Limit``
                |> Expect.equal
                    "the session terms fall through"
                    (SessionGatePolicy.english Terms.``Session Ending Pin Limit``)

                for term in
                    [
                        Terms.``Session Newer Version``
                        Terms.``Session Open Newest``
                        Terms.``Session Version Opened``
                        Terms.``Session Refresh Failed``
                    ] do
                    english term |> Expect.notEqual $"default for {term}" $"{term}"
            }

            test "the group lock has its English" {
                english Terms.``Plan Context Group Locked``
                |> Expect.equal
                    "the English"
                    "Another order of its group was calculated with other patient data; this order cannot be changed"
            }

            test "the warning before the PIN names the orders not calculated, and is none without them" {
                let plan names =
                    Informedica.GenPRES.Client.Core.Tests.OrderFixtures.draft
                    |> fun pat ->
                        Informedica.GenPRES.Shared.Models.OrderPlan.create
                            pat
                            [|
                                for name, marked in names do
                                    { Informedica.GenPRES.Shared.Models.OrderContext.empty with
                                        Id = name
                                        Scenarios =
                                            [|
                                                { Informedica.GenPRES.Client.Core.Tests.OrderFixtures.scenario name name with
                                                    NotRecalculated = marked
                                                }
                                            |]
                                    }
                            |]

                plan [ "paracetamol", false ]
                |> SigningPolicy.notRecalculatedSentence named
                |> Expect.isNone "every order calculated"

                plan [ "paracetamol", false; "morfine", true; "ibuprofen", true ]
                |> SigningPolicy.notRecalculatedSentence SigningPolicy.english
                |> Expect.equal
                    "the two orders"
                    (Some "morfine, ibuprofen: not calculated on the current patient data.")
            }

            test
                "the moved-on and version-opened sentences name whose version, when and which (Rules 21, 22; UC-4 step 4)" {
                let at = System.DateTime(2026, 9, 11, 12, 30, 0, System.DateTimeKind.Utc)

                let head: OrderPlanHead =
                    {
                        Id = "plan-2"
                        No = 2
                        By =
                            {
                                UserId = "b"
                                DisplayName = "Stub Prescriber B"
                                Role = UserRole.Prescriber
                            }
                        SignedAt = at
                    }

                newerPlanSentence english head
                |> Expect.equal "moved on" $"Stub Prescriber B signed a newer version at {TextFormat.time at}."

                versionOpenedSentence english head
                |> Expect.equal "opened" "Version 2 by Stub Prescriber B is now open."

                ehrMovedOnSentence
                    english
                    (Some
                        {
                            Name = "J. Janssen"
                            BirthYear = 2020
                            BirthMonth = 1
                            BirthDay = 2
                        })
                |> Expect.equal
                    "the patient being signed for"
                    "You are signing for J. Janssen. This patient is no longer active in the EHR."

                ehrMovedOnSentence english None
                |> Expect.equal "no name to show" "This patient is no longer active in the EHR."
            }

            test "one term per refusal; the block names the signer and the time, the tries and the lock are filled" {
                let untilUtc = DateTime(2026, 9, 11, 12, 1, 0, DateTimeKind.Utc)

                [
                    SigningRefusal.NoSession, "<Signing Refusal No Session>"
                    SigningRefusal.NoPatient, "<Signing Refusal No Patient>"
                    SigningRefusal.NotPrescriber, "<Signing Refusal Not Prescriber>"
                    SigningRefusal.Blocked head, "<Signing Refusal Blocked>"
                    SigningRefusal.StaleToken, "<Signing Refusal Stale Token>"
                    SigningRefusal.ChallengeMismatch, "<Signing Refusal Challenge Mismatch>"
                    SigningRefusal.ChallengeExpired, "<Signing Refusal Challenge Expired>"
                    SigningRefusal.PinWrong 2, "<Signing Refusal Pin Wrong>"
                    SigningRefusal.PinLimit, "<Signing Refusal Pin Limit>"
                    SigningRefusal.Locked untilUtc, "<Signing Refusal Locked>"
                    SigningRefusal.StoreFailed, "<Signing Refusal Store Failed>"
                    SigningRefusal.PlanUnreadable, "<Signing Refusal Plan Unreadable>"
                ]
                |> List.iter (fun (refusal, term) -> refusalSentence named refusal |> Expect.equal $"{refusal}" term)

                refusalSentence english (SigningRefusal.PinWrong 2)
                |> Expect.equal "filled" "The PIN is not right. 2 tries left."

                let blocked = refusalSentence english (SigningRefusal.Blocked head)

                blocked
                |> Expect.stringStarts "the signer" "Stub Prescriber B signed a newer version at "

                blocked |> Expect.stringContains "a time of day" (TextFormat.time head.SignedAt)
                TextFormat.time head.SignedAt |> Expect.isMatch "HH:mm" @"^\d{2}:\d{2}$"

                refusalSentence english (SigningRefusal.Locked untilUtc)
                |> Expect.equal "the lock" $"Signing is locked until {TextFormat.time untilUtc}."
            }

            test "the signed sentence names the version and the signer; the notice its case" {
                let signed: SignedOrderPlan =
                    {
                        Head = head
                        PatientId = "p"
                        Base = Some "plan-1"
                        OrderContexts = [||]
                        Patient = patient
                        Identity = None
                        Verified = true
                    }

                signedSentence english signed
                |> Expect.equal "filled" "Version 2 was signed by Stub Prescriber B."

                signedSentence named signed |> Expect.equal "term" "<Signing Signed>"

                noticeSentence
                    named
                    {
                        Data = Some patient
                        Token = "d"
                    }
                |> Expect.equal "changed" "<Signing Data Changed>"

                noticeSentence
                    named
                    {
                        Data = None
                        Token = "d"
                    }
                |> Expect.equal "unverified" "<Signing Data Unverified>"
            }

            testList
                "the lines of the data notice"
                [
                    let notice data =
                        {
                            Data = data
                            Token = "d"
                        }

                    let weighed grams =
                        { patient with Weight = { patient.Weight with Measured = Some grams } }

                    test "only what differs, the plan's beside the EHR's" {
                        noticeRows
                            named
                            (weighed 11000<gram>)
                            (notice (Some { weighed 12500<gram> with Department = Some "ICK" }))
                        |> Expect.equal
                            "weight and department"
                            [
                                {
                                    Label = Terms.``Patient Weight``
                                    Plan = "11 kg"
                                    Ehr = "12.5 kg"
                                }
                                {
                                    Label = Terms.``Patient Department``
                                    Plan = patient.Department |> Option.defaultValue "-"
                                    Ehr = "ICK"
                                }
                            ]
                    }

                    test "the age and the mark as an adult are not shown, even when they differ" {
                        noticeRows
                            named
                            patient
                            (notice (
                                Some
                                    { patient with
                                        Age =
                                            Some(
                                                Informedica.GenPRES.Shared.Models.Patient.Age.create
                                                    24<year>
                                                    (Some 8<month>)
                                                    None
                                                    None
                                            )
                                        AdultMark =
                                            AdultMark.MarkableAdult(
                                                patient |> Informedica.GenPRES.Shared.Models.Patient.isAdult |> not
                                            )
                                    }
                            ))
                        |> Expect.isEmpty "no lines"
                    }

                    test "the order of the access devices is no difference" {
                        noticeRows
                            named
                            { patient with Access = [ CVL; PVL ] }
                            (notice (Some { patient with Access = [ PVL; CVL ] }))
                        |> Expect.isEmpty "no lines"
                    }

                    test "the same value measured instead of estimated is a difference, and the estimate is labelled" {
                        let estimated =
                            { patient with
                                Weight =
                                    { patient.Weight with
                                        Measured = None
                                        Estimated = Some 28000<gram>
                                    }
                            }

                        noticeRows named estimated (notice (Some(weighed 28000<gram>)))
                        |> List.map (fun r -> r.Plan, r.Ehr)
                        |> Expect.equal "estimated beside measured" [ "28 kg (<Patient Estimated>)", "28 kg" ]
                    }

                    test "the gestational age is in words, as in the patient line" {
                        noticeRows
                            named
                            patient
                            (notice (
                                Some
                                    { patient with
                                        GestationalAge =
                                            GestationalAge.OptionalGestationalAge(
                                                Some
                                                    {
                                                        Weeks = 30<week>
                                                        Days = 2<day>
                                                    }
                                            )
                                    }
                            ))
                        |> List.map _.Ehr
                        |> Expect.equal "words" [ "30 <Patient Age weeks> 2 <Patient Age days>" ]
                    }

                    test "an unverified notice has no lines" {
                        noticeRows named patient (notice None) |> Expect.isEmpty "no lines"
                    }

                    test "without the sheet the renal function and the gender are in English" {
                        noticeRows
                            english
                            { patient with
                                Gender = Male
                                RenalFunction = Some(EGFR(Some 30, Some 50))
                            }
                            (notice (
                                Some
                                    { patient with
                                        Gender = Female
                                        RenalFunction = Some PeritonealDialysis
                                    }
                            ))
                        |> List.map (fun r -> r.Plan, r.Ehr)
                        |> Expect.equal "English" [ "Male", "Female"; "30 - 50 mL/min/1.73 m2", "Peritoneal dialysis" ]
                    }
                ]

            test "the PIN's own check is the enrolment's: four to six digits" {
                for ok in [ "1234"; "123456" ] do
                    pinError english ok |> Expect.isNone ok

                for bad in [ "123"; "1234567"; "12a4"; "" ] do
                    pinError named bad |> Expect.equal bad (Some "<Session Enrolment Pin Format>")
            }

            testList
                "canSign"
                [
                    let contextWithOrder id =
                        { Informedica.GenPRES.Shared.Models.OrderContext.empty with
                            Id = id
                            Scenarios = [| Unchecked.defaultof<OrderScenario> |]
                        }

                    let withOrders =
                        Informedica.GenPRES.Shared.Models.OrderPlan.create patient [| contextWithOrder "c-1" |]

                    let empty = Informedica.GenPRES.Shared.Models.OrderPlan.create patient [||]
                    let prescriber = SessionView.Open(openedAs UserRole.Prescriber true)

                    test "a Prescriber can sign a plan with at least one order" {
                        canSign prescriber (PlanDifferencePolicy.differences [||] withOrders) withOrders
                        |> Expect.isTrue "new orders"
                    }

                    test "a Prescriber can sign a plan with orders that equals the version last signed" {
                        // no differences: the plan is the version last signed
                        canSign prescriber [||] withOrders |> Expect.isTrue "orders as signed"
                    }

                    test "a Prescriber can sign a plan whose orders were all removed since the version last signed" {
                        let differences = PlanDifferencePolicy.differences withOrders.OrderContexts empty

                        differences
                        |> Array.map snd
                        |> Expect.equal "the removal is listed" [| PlanDifferencePolicy.Difference.Removed |]

                        canSign prescriber differences empty |> Expect.isTrue "the removals"
                    }

                    test "a plan that never had orders offers no Sign" {
                        canSign prescriber (PlanDifferencePolicy.differences [||] empty) empty
                        |> Expect.isFalse "nothing to sign"
                    }

                    test "a plan is signed only by a Prescriber with a patient in an open Session" {
                        let differences = PlanDifferencePolicy.differences withOrders.OrderContexts empty

                        for plan, differences in [ withOrders, [||]; empty, differences ] do
                            canSign (SessionView.Open(openedAs UserRole.Reader true)) differences plan
                            |> Expect.isFalse "reader"

                            canSign (SessionView.Open(openedAs UserRole.Prescriber false)) differences plan
                            |> Expect.isFalse "no patient"

                            canSign
                                (SessionView.Open { openedAs UserRole.Prescriber true with User = None })
                                differences
                                plan
                            |> Expect.isFalse "anonymous"

                            for session in
                                [
                                    SessionView.Anonymous
                                    SessionView.Resuming
                                    SessionView.Closing(openedAs UserRole.Prescriber true)
                                ] do
                                canSign session differences plan |> Expect.isFalse $"{session}"
                    }
                ]

            test "the dialog is up from the sign on: requesting, noticed, challenged or submitting" {
                let plan = Informedica.GenPRES.Shared.Models.OrderPlan.create patient [||]
                dialogOpen SigningView.Idle |> Expect.isFalse "idle"
                dialogOpen SigningView.RequestingChallenge
                |> Expect.isTrue "requesting: modal from the sign on"

                dialogOpen (
                    SigningView.DataChanged(
                        plan,
                        {
                            Data = None
                            Token = "d"
                        }
                    )
                )
                |> Expect.isTrue "noticed"

                dialogOpen (SigningView.AskingPin(plan, None)) |> Expect.isTrue "challenged"

                dialogOpen (SigningView.AskingPin(plan, Some(SigningRefusal.PinWrong 2)))
                |> Expect.isTrue "refused"

                dialogOpen (SigningView.Submitting plan) |> Expect.isTrue "submitting"
            }

            test "a signature is under way from the sign until it is answered or cancelled" {
                let plan = Informedica.GenPRES.Shared.Models.OrderPlan.create patient [||]

                underWay SigningView.Idle |> Expect.isFalse "idle"
                underWay SigningView.RequestingChallenge |> Expect.isTrue "requesting"

                underWay (
                    SigningView.DataChanged(
                        plan,
                        {
                            Data = None
                            Token = "d"
                        }
                    )
                )
                |> Expect.isTrue "noticed"

                underWay (SigningView.AskingPin(plan, None)) |> Expect.isTrue "challenged"
                underWay (SigningView.Submitting plan) |> Expect.isTrue "submitting"
            }
        ]
