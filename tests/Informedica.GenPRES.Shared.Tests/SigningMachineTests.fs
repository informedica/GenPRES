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
            OrderContexts = [||]
            Patient = patient
            Verified = true
        }

    let requesting = SigningState.requesting plan None "r-1"
    let challenged = SigningState.challenged "c-1" plan None
    let submitting = SigningState.submitting "c-1" plan "k-1"
    let unsent = SigningState.unsent "c-1" plan "k-1"

    let transition = SigningState.transition

    let issued request =
        SigningMsg.ChallengeAnswered(request, Ok(SigningResponse.ChallengeIssued "c-1"))

    let submitted key =
        SigningMsg.SubmitAnswered(key, Ok(SigningResponse.Submitted(signed, OpenedToken "t2")))


open Fixtures


[<Tests>]
let tests =
    testList
        "SigningState.transition"
        [
            test "Sign from Idle asks the challenge over the plan under the request id; elsewhere it is a no-op" {
                transition (SigningMsg.Sign(plan, "r-1")) SigningState.idle
                |> Expect.equal "asked" (requesting, [ SigningEffect.CallChallenge(plan, None, "r-1") ])

                for state in
                    [
                        requesting
                        challenged
                        submitting
                        unsent
                        SigningState.noticed
                            plan
                            {
                                Data = None
                                Token = "d"
                            }
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
                |> Expect.equal "notice" (SigningState.noticed plan notice, [])

                transition
                    (SigningMsg.ChallengeAnswered("r-1", Ok(SigningResponse.Refused SigningRefusal.NotPrescriber)))
                    requesting
                |> Expect.equal "told" (SigningState.idle, [ SigningEffect.TellRefused SigningRefusal.NotPrescriber ])

                transition
                    (SigningMsg.ChallengeAnswered("r-1", Ok(SigningResponse.Refused SigningRefusal.PinLimit)))
                    requesting
                |> Expect.equal "ended" (SigningState.idle, [ SigningEffect.EndSession SessionEnding.WrongPinLimit ])

                transition (SigningMsg.ChallengeAnswered("r-1", Error "down")) requesting
                |> Expect.equal "transport" (SigningState.idle, [ SigningEffect.TellError "down" ])
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

                for state in [ SigningState.idle; challenged; submitting; unsent ] do
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

                transition SigningMsg.Accept (SigningState.noticed plan changed)
                |> Expect.equal
                    "over the reading"
                    (SigningState.requesting shown (Some "d-1") "d-1",
                     [
                         SigningEffect.SetPatient otherData
                         SigningEffect.CallChallenge(shown, Some "d-1", "d-1")
                     ])

                let unverified =
                    {
                        Data = None
                        Token = "d-2"
                    }

                transition SigningMsg.Accept (SigningState.noticed plan unverified)
                |> Expect.equal
                    "over the opened data"
                    (SigningState.requesting plan (Some "d-2") "d-2",
                     [ SigningEffect.CallChallenge(plan, Some "d-2", "d-2") ])

                for state in [ SigningState.idle; requesting; challenged; submitting; unsent ] do
                    transition SigningMsg.Accept state |> Expect.equal $"{state}" (state, [])
            }

            test
                "Confirm sends the PIN over the challenged plan under the caller's key, once at a time; Cancel drops the challenge but not a request in flight" {
                transition (SigningMsg.Confirm("1234", "k-1")) challenged
                |> Expect.equal "sent" (submitting, [ SigningEffect.CallSubmit(plan, "c-1", "1234", "k-1") ])

                transition (SigningMsg.Confirm("1234", "k-2")) submitting
                |> Expect.equal "not twice" (submitting, [])

                transition (SigningMsg.Confirm("1234", "k-2")) SigningState.idle
                |> Expect.equal "nothing to confirm" (SigningState.idle, [])

                for state in
                    [
                        requesting
                        challenged
                        unsent
                        SigningState.noticed
                            plan
                            {
                                Data = None
                                Token = "d"
                            }
                    ] do
                    transition SigningMsg.Cancel state
                    |> Expect.equal $"{state}" (SigningState.idle, [])

                transition SigningMsg.Cancel submitting
                |> Expect.equal "in flight" (submitting, [])

                transition SigningMsg.Cancel SigningState.idle
                |> Expect.equal "idle" (SigningState.idle, [])
            }

            test
                "the Submission answered: signed renews the token and is told; a wrong PIN or a lock keeps the dialog; the limit ends the Session; the rest is told" {
                transition (submitted "k-1") submitting
                |> Expect.equal
                    "signed"
                    (SigningState.idle, [ SigningEffect.RenewToken(OpenedToken "t2"); SigningEffect.TellSigned signed ])

                transition
                    (SigningMsg.SubmitAnswered("k-1", Ok(SigningResponse.Refused(SigningRefusal.PinWrong 2))))
                    submitting
                |> Expect.equal "dialog kept" (SigningState.challenged "c-1" plan (Some(SigningRefusal.PinWrong 2)), [])

                let until = DateTime(2026, 9, 11, 12, 1, 0, DateTimeKind.Utc)

                transition
                    (SigningMsg.SubmitAnswered("k-1", Ok(SigningResponse.Refused(SigningRefusal.Locked until))))
                    submitting
                |> Expect.equal
                    "dialog kept, locked"
                    (SigningState.challenged "c-1" plan (Some(SigningRefusal.Locked until)), [])

                transition
                    (SigningMsg.SubmitAnswered("k-1", Ok(SigningResponse.Refused SigningRefusal.PinLimit)))
                    submitting
                |> Expect.equal "ended" (SigningState.idle, [ SigningEffect.EndSession SessionEnding.WrongPinLimit ])

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
                    |> Expect.equal $"{refusal}" (SigningState.idle, [ SigningEffect.TellRefused refusal ])
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

                for state in [ SigningState.idle; requesting; challenged; unsent ] do
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
                    (SigningState.idle, [ SigningEffect.RenewToken(OpenedToken "t2"); SigningEffect.TellSigned signed ])
            }

            test "the plan submitted is the plan challenged, whatever the cart did meanwhile (ext 3b, 3c)" {
                let state, _ = transition (SigningMsg.Sign(plan, "r-1")) SigningState.idle
                let state, _ = transition (issued "r-1") state
                // the cart moved on: the machine never sees it
                let _, effects = transition (SigningMsg.Confirm("1234", "k-1")) state

                effects
                |> Expect.equal "the challenged plan" [ SigningEffect.CallSubmit(plan, "c-1", "1234", "k-1") ]
            }
        ]


[<Tests>]
let viewTests =
    let notice =
        {
            Data = None
            Token = "d-1"
        }

    testList
        "SigningState.view"
        [
            test "idle and requesting show nothing of the request" {
                SigningState.idle |> SigningState.view |> Expect.equal "idle" SigningView.Idle
                requesting
                |> SigningState.view
                |> Expect.equal "requesting" SigningView.Requesting
            }

            test "noticed and challenged show the plan with the notice or the refusal" {
                SigningState.noticed plan notice
                |> SigningState.view
                |> Expect.equal "noticed" (SigningView.Noticed(plan, notice))

                challenged
                |> SigningState.view
                |> Expect.equal "challenged" (SigningView.Challenged(plan, None))

                SigningState.challenged "c-1" plan (Some(SigningRefusal.PinWrong 2))
                |> SigningState.view
                |> Expect.equal "refused" (SigningView.Challenged(plan, Some(SigningRefusal.PinWrong 2)))
            }

            test "submitting shows the plan without the key; a lost answer shows as challenged again" {
                submitting
                |> SigningState.view
                |> Expect.equal "submitting" (SigningView.Submitting plan)

                unsent
                |> SigningState.view
                |> Expect.equal "unsent" (SigningView.Challenged(plan, None))
            }
        ]


[<Tests>]
let stateViewTests =
    let notice =
        {
            Data = None
            Token = "d-1"
        }

    // the lane record beside the DU: each constructor shows what the corresponding state of
    // the DU shows, so that the machine can move onto the record with the dialog unchanged
    let agrees (name: string) (state: SigningState) (old: Signing) =
        state |> SigningState.view |> Expect.equal name (old |> Signing.view)

    testList
        "SigningState.view agrees with Signing.view"
        [
            test "idle and the challenge asked show nothing of the request" {
                agrees "idle" SigningState.idle Signing.Idle
                agrees "requesting" requesting (Signing.Requesting(plan, None, "r-1"))
                agrees
                    "requesting after a notice"
                    (SigningState.requesting plan (Some "d-1") "d-1")
                    (Signing.Requesting(plan, None, "r-1"))
            }

            test "noticed and challenged show the plan with the notice or the refusal" {
                agrees "noticed" (SigningState.noticed plan notice) (Signing.Noticed(plan, notice))
                agrees "challenged" challenged (Signing.Challenged("c-1", plan, None))

                agrees
                    "refused"
                    (SigningState.challenged "c-1" plan (Some(SigningRefusal.PinWrong 2)))
                    (Signing.Challenged("c-1", plan, Some(SigningRefusal.PinWrong 2)))
            }

            test "submitting shows the plan without the key; a lost answer shows as challenged again" {
                agrees "submitting" submitting (Signing.Submitting("c-1", plan, "k-1"))
                agrees "unsent" unsent (Signing.Unsent("c-1", plan, "k-1"))

                SigningState.unsent "c-1" plan "k-1"
                |> SigningState.view
                |> Expect.equal "the key is the machine's" (SigningView.Challenged(plan, None))
            }
        ]
