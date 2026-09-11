module Informedica.GenPRES.Shared.Tests.SigningMachineTests

open System
open Expecto
open Expecto.Flip
open Shared.Types
open SigningMachine


module Fixtures =

    let patient = Shared.Models.Patient.empty
    let otherData = { patient with Department = Some "ICU" }
    let plan = Shared.Models.OrderPlan.create patient [||]

    let prescriber =
        {
            UserId = "prescriber"
            DisplayName = "Stub Prescriber"
            Role = UserRole.Prescriber
        }

    let signed: SignedOrderPlan =
        {
            Head =
                {
                    Id = "plan-1"
                    No = 1
                    By = prescriber
                    SignedAt = DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc)
                }
            PatientId = "stub-patient"
            Base = None
            Scenarios = [||]
            Patient = patient
            Verified = true
        }

    let requesting = Signing.Requesting(plan, None, "r-1")
    let challenged = Signing.Challenged("c-1", plan, None)
    let submitting = Signing.Submitting("c-1", plan, "k-1")
    let unsent = Signing.Unsent("c-1", plan, "k-1")

    let transition = Signing.transition

    let issued request =
        SigningMsg.ChallengeAnswered(request, Ok(SigningResponse.ChallengeIssued "c-1"))

    let submitted key =
        SigningMsg.SubmitAnswered(key, Ok(SigningResponse.Submitted(signed, OpenedToken "t2")))


open Fixtures


[<Tests>]
let tests =
    testList
        "Signing.transition"
        [
            test "Sign from Idle asks the challenge over the plan under the request id; elsewhere it is a no-op" {
                transition (SigningMsg.Sign(plan, "r-1")) Signing.Idle
                |> Expect.equal "asked" (requesting, [ SigningEffect.CallChallenge(plan, None, "r-1") ])

                for state in
                    [
                        requesting
                        challenged
                        submitting
                        unsent
                        Signing.Noticed(
                            plan,
                            {
                                Data = None
                                Token = "d"
                            }
                        )
                    ] do
                    transition (SigningMsg.Sign(plan, "r-9")) state
                    |> Expect.equal $"{state}" (state, [])
            }

            test
                "the challenge answered: issued opens the dialog, a notice asks, a refusal is told, the PIN limit ends the Session" {
                transition (issued "r-1") requesting |> Expect.equal "dialog" (challenged, [])

                let notice =
                    {
                        Data = Some otherData
                        Token = "d-1"
                    }

                transition (SigningMsg.ChallengeAnswered("r-1", Ok(SigningResponse.DataNotice notice))) requesting
                |> Expect.equal "notice" (Signing.Noticed(plan, notice), [])

                transition
                    (SigningMsg.ChallengeAnswered("r-1", Ok(SigningResponse.Refused SigningRefusal.NotPrescriber)))
                    requesting
                |> Expect.equal "told" (Signing.Idle, [ SigningEffect.TellRefused SigningRefusal.NotPrescriber ])

                transition
                    (SigningMsg.ChallengeAnswered("r-1", Ok(SigningResponse.Refused SigningRefusal.PinLimit)))
                    requesting
                |> Expect.equal "ended" (Signing.Idle, [ SigningEffect.EndSession SessionEnding.WrongPinLimit ])

                transition (SigningMsg.ChallengeAnswered("r-1", Error "down")) requesting
                |> Expect.equal "transport" (Signing.Idle, [ SigningEffect.TellError "down" ])
            }

            test
                "an answer lands only on the request it answers: another request id, or no request in flight, is dropped" {
                // a challenge asked by an earlier Session answers after a later one asked its own
                transition (issued "r-old") requesting
                |> Expect.equal "another request" (requesting, [])

                transition
                    (SigningMsg.ChallengeAnswered("r-old", Ok(SigningResponse.Refused SigningRefusal.PinLimit)))
                    requesting
                |> Expect.equal "not even the PIN limit" (requesting, [])

                for state in [ Signing.Idle; challenged; submitting; unsent ] do
                    transition (issued "r-1") state |> Expect.equal $"{state}" (state, [])
            }

            test
                "a notice accepted: the challenge is asked again with the token as the request id, over the data as it stands (Rule 44)" {
                let changed =
                    {
                        Data = Some otherData
                        Token = "d-1"
                    }

                let shown = { plan with Patient = otherData }

                transition SigningMsg.Accept (Signing.Noticed(plan, changed))
                |> Expect.equal
                    "over the reading"
                    (Signing.Requesting(shown, Some "d-1", "d-1"),
                     [
                         SigningEffect.SetPatient otherData
                         SigningEffect.CallChallenge(shown, Some "d-1", "d-1")
                     ])

                let unverified =
                    {
                        Data = None
                        Token = "d-2"
                    }

                transition SigningMsg.Accept (Signing.Noticed(plan, unverified))
                |> Expect.equal
                    "over the opened data"
                    (Signing.Requesting(plan, Some "d-2", "d-2"),
                     [ SigningEffect.CallChallenge(plan, Some "d-2", "d-2") ])

                for state in [ Signing.Idle; requesting; challenged; submitting; unsent ] do
                    transition SigningMsg.Accept state |> Expect.equal $"{state}" (state, [])
            }

            test
                "Confirm sends the PIN over the challenged plan under the caller's key, once at a time; Cancel drops the challenge but not a request in flight" {
                transition (SigningMsg.Confirm("1234", "k-1")) challenged
                |> Expect.equal "sent" (submitting, [ SigningEffect.CallSubmit(plan, "c-1", "1234", "k-1") ])

                transition (SigningMsg.Confirm("1234", "k-2")) submitting
                |> Expect.equal "not twice" (submitting, [])

                transition (SigningMsg.Confirm("1234", "k-2")) Signing.Idle
                |> Expect.equal "nothing to confirm" (Signing.Idle, [])

                for state in
                    [
                        requesting
                        challenged
                        unsent
                        Signing.Noticed(
                            plan,
                            {
                                Data = None
                                Token = "d"
                            }
                        )
                    ] do
                    transition SigningMsg.Cancel state |> Expect.equal $"{state}" (Signing.Idle, [])

                transition SigningMsg.Cancel submitting
                |> Expect.equal "in flight" (submitting, [])

                transition SigningMsg.Cancel Signing.Idle
                |> Expect.equal "idle" (Signing.Idle, [])
            }

            test
                "the Submission answered: signed renews the token and is told; a wrong PIN or a lock keeps the dialog; the limit ends the Session; the rest is told" {
                transition (submitted "k-1") submitting
                |> Expect.equal
                    "signed"
                    (Signing.Idle,
                     [
                         SigningEffect.RenewToken(OpenedToken "t2")
                         SigningEffect.TellSigned signed
                     ])

                transition
                    (SigningMsg.SubmitAnswered("k-1", Ok(SigningResponse.Refused(SigningRefusal.PinWrong 2))))
                    submitting
                |> Expect.equal "dialog kept" (Signing.Challenged("c-1", plan, Some(SigningRefusal.PinWrong 2)), [])

                let until = DateTime(2026, 9, 11, 12, 1, 0, DateTimeKind.Utc)

                transition
                    (SigningMsg.SubmitAnswered("k-1", Ok(SigningResponse.Refused(SigningRefusal.Locked until))))
                    submitting
                |> Expect.equal
                    "dialog kept, locked"
                    (Signing.Challenged("c-1", plan, Some(SigningRefusal.Locked until)), [])

                transition
                    (SigningMsg.SubmitAnswered("k-1", Ok(SigningResponse.Refused SigningRefusal.PinLimit)))
                    submitting
                |> Expect.equal "ended" (Signing.Idle, [ SigningEffect.EndSession SessionEnding.WrongPinLimit ])

                for refusal in
                    [
                        SigningRefusal.NoSession
                        SigningRefusal.NoPatient
                        SigningRefusal.NotPrescriber
                        SigningRefusal.Blocked signed.Head
                        SigningRefusal.StaleToken
                        SigningRefusal.ChallengeMismatch
                        SigningRefusal.ChallengeExpired
                    ] do
                    transition (SigningMsg.SubmitAnswered("k-1", Ok(SigningResponse.Refused refusal))) submitting
                    |> Expect.equal $"{refusal}" (Signing.Idle, [ SigningEffect.TellRefused refusal ])
            }

            test
                "a Submission's answer lands only under its key: an earlier Session's signature never touches a later one" {
                // signed under another key: neither the token renewed nor the signature told
                transition (submitted "k-old") submitting
                |> Expect.equal "another key" (submitting, [])

                transition
                    (SigningMsg.SubmitAnswered("k-old", Ok(SigningResponse.Refused SigningRefusal.PinLimit)))
                    submitting
                |> Expect.equal "not even the PIN limit" (submitting, [])

                for state in [ Signing.Idle; requesting; challenged; unsent ] do
                    transition (submitted "k-1") state |> Expect.equal $"{state}" (state, [])
            }

            test
                "a lost answer: the dialog comes back and the retry goes out under the same key, so the server answers what it did (Rule 45, ext 3d)" {
                transition (SigningMsg.SubmitAnswered("k-1", Error "down")) submitting
                |> Expect.equal "unsent, told" (unsent, [ SigningEffect.TellError "down" ])

                // the caller mints a fresh key; the machine keeps the one the lost Submission had
                transition (SigningMsg.Confirm("1234", "k-fresh")) unsent
                |> Expect.equal "same key" (submitting, [ SigningEffect.CallSubmit(plan, "c-1", "1234", "k-1") ])

                // the signature had landed: the remembered answer is what comes back, under that key
                transition (submitted "k-1") submitting
                |> Expect.equal
                    "signed after all"
                    (Signing.Idle,
                     [
                         SigningEffect.RenewToken(OpenedToken "t2")
                         SigningEffect.TellSigned signed
                     ])
            }

            test "the plan submitted is the plan challenged, whatever the cart did meanwhile (ext 3b, 3c)" {
                let state, _ = transition (SigningMsg.Sign(plan, "r-1")) Signing.Idle
                let state, _ = transition (issued "r-1") state
                // the cart moved on: the machine never sees it
                let _, effects = transition (SigningMsg.Confirm("1234", "k-1")) state

                effects
                |> Expect.equal "the challenged plan" [ SigningEffect.CallSubmit(plan, "c-1", "1234", "k-1") ]
            }
        ]
