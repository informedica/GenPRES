module Informedica.GenPRES.Shared.Tests.SigningPolicyTests

open System
open Expecto
open Expecto.Flip
open Shared
open Shared.Types
open SessionMachine
open SigningMachine
open SigningPolicy


module Fixtures =

    let patient = Shared.Models.Patient.empty

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
                            Patient = patient
                        }
                else
                    None
            OpenedToken = Some(OpenedToken "t")
            KeyThumbprint = Some "thumb"
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
                        Terms.``Signing Send Failed``
                    ] do
                    english term |> Expect.notEqual $"default for {term}" $"{term}"

                english Terms.``Session Ending Pin Limit``
                |> Expect.equal
                    "the session terms fall through"
                    (SessionGatePolicy.english Terms.``Session Ending Pin Limit``)
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
                ]
                |> List.iter (fun (refusal, term) -> refusalSentence named refusal |> Expect.equal $"{refusal}" term)

                refusalSentence english (SigningRefusal.PinWrong 2)
                |> Expect.equal "filled" "The PIN is not right. 2 tries left."

                let blocked = refusalSentence english (SigningRefusal.Blocked head)

                blocked
                |> Expect.stringStarts "the signer" "Stub Prescriber B signed a newer version at "

                blocked |> Expect.stringContains "a time of day" (time head.SignedAt)
                time head.SignedAt |> Expect.isMatch "HH:mm" @"^\d{2}:\d{2}$"

                refusalSentence english (SigningRefusal.Locked untilUtc)
                |> Expect.equal "the lock" $"Signing is locked until {time untilUtc}."
            }

            test "the signed sentence names the version and the signer; the notice its case" {
                let signed: SignedOrderPlan =
                    {
                        Head = head
                        PatientId = "p"
                        Base = Some "plan-1"
                        Scenarios = [||]
                        Patient = patient
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

            test "the PIN's own check is the enrolment's: four to six digits" {
                for ok in [ "1234"; "123456" ] do
                    pinError english ok |> Expect.isNone ok

                for bad in [ "123"; "1234567"; "12a4"; "" ] do
                    pinError named bad |> Expect.equal bad (Some "<Session Enrolment Pin Format>")
            }

            test "canSign: an open Session as Prescriber for a patient with at least one order" {
                let withOrders =
                    { Shared.Models.OrderPlan.create patient [||] with
                        Scenarios = [| Unchecked.defaultof<OrderScenario> |]
                    }

                let empty = Shared.Models.OrderPlan.create patient [||]

                canSign (Session.Open(openedAs UserRole.Prescriber true)) withOrders
                |> Expect.isTrue "prescriber"

                canSign (Session.Open(openedAs UserRole.Prescriber true)) empty
                |> Expect.isFalse "nothing to sign"

                canSign (Session.Open(openedAs UserRole.Reader true)) withOrders
                |> Expect.isFalse "reader"

                canSign (Session.Open(openedAs UserRole.Prescriber false)) withOrders
                |> Expect.isFalse "no patient"

                canSign (Session.Open { openedAs UserRole.Prescriber true with User = None }) withOrders
                |> Expect.isFalse "anonymous"

                for session in
                    [
                        Session.Anonymous
                        Session.Resuming
                        Session.Closing(openedAs UserRole.Prescriber true)
                    ] do
                    canSign session withOrders |> Expect.isFalse $"{session}"
            }

            test "the dialog is up while noticed, challenged or submitting" {
                let plan = Shared.Models.OrderPlan.create patient [||]
                dialogOpen Signing.Idle |> Expect.isFalse "idle"
                dialogOpen (Signing.Requesting(plan, None, "r")) |> Expect.isFalse "requesting"

                dialogOpen (
                    Signing.Noticed(
                        plan,
                        {
                            Data = None
                            Token = "d"
                        }
                    )
                )
                |> Expect.isTrue "noticed"

                dialogOpen (Signing.Challenged("c", plan, None)) |> Expect.isTrue "challenged"
                dialogOpen (Signing.Submitting("c", plan, "k")) |> Expect.isTrue "submitting"
                dialogOpen (Signing.Unsent("c", plan, "k")) |> Expect.isTrue "unsent"
            }
        ]
