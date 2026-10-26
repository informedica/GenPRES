/// The signing machine states the machine and policy tests share.
module Informedica.GenPRES.Client.Core.Tests.StateMachines.SigningFixtures

open Informedica.GenPRES.Client.Core.StateMachines
open System
open Informedica.GenPRES.Shared.Types
open SigningMachine


let patient = Informedica.GenPRES.Shared.Models.Patient.empty
let otherData = { patient with Department = Some "ICU" }
let plan = Informedica.GenPRES.Shared.Models.OrderPlan.create patient [||]

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
        Identity = None
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
    SigningMsg.SubmitAnswered(key, Ok(SigningResponse.Submitted(signed, OpenedToken "t2", patient)))

let who =
    {
        Name = "Stub Testpatiënt"
        BirthYear = 2016
        BirthMonth = 3
        BirthDay = 15
    }
