/// The contract data the order tests share: two patients, a scenario, an order context, a plan
/// of one and of two contexts, and a signed order plan.
module Informedica.GenPRES.Client.Core.Tests.OrderFixtures

open System
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Api


let ten = { Informedica.GenPRES.Shared.Models.Patient.Age.ageZero with Age.Years = 10<year> }

/// The data a plan carries, and the patient it is: an age makes the draft a patient.
let draft = { Informedica.GenPRES.Shared.Models.Patient.empty with Age = Some ten }

let asPatient (dto: Patient) =
    match dto |> Informedica.GenPRES.Shared.Models.Patient.validate with
    | Ok pat -> pat
    | Error err -> invalidOp $"the fixture is no patient: %A{err}"

let patient = draft |> asPatient
let otherDraft = { draft with Department = Some "other" }
let other = otherDraft |> asPatient

/// An OrderScenario with its order id and its name set, every other field a default (built by
/// reflection: the order graph is too deep to write by hand).
let scenario (id: string) (name: string) : OrderScenario =
    let rec defaultOf (t: Type) : obj =
        if t = typeof<string> then
            box ""
        elif t = typeof<bool> then
            box false
        elif t = typeof<int> then
            box 0
        elif t = typeof<decimal> then
            box 0m
        elif t = typeof<float> then
            box 0.0
        elif t = typeof<DateTime> then
            box DateTime.MinValue
        elif t.IsArray then
            box (Array.CreateInstance(t.GetElementType(), 0))
        elif t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>> then
            null
        elif Reflection.FSharpType.IsRecord t then
            Reflection.FSharpValue.MakeRecord(
                t,
                Reflection.FSharpType.GetRecordFields t
                |> Array.map (fun f -> defaultOf f.PropertyType)
            )
        elif Reflection.FSharpType.IsUnion t then
            let case = (Reflection.FSharpType.GetUnionCases t)[0]

            Reflection.FSharpValue.MakeUnion(case, case.GetFields() |> Array.map (fun f -> defaultOf f.PropertyType))
        else
            null

    let sc = defaultOf typeof<OrderScenario> :?> OrderScenario

    { sc with
        Name = name
        Order = { sc.Order with Id = id }
    }


let context id name =
    { Informedica.GenPRES.Shared.Models.OrderContext.empty with
        Id = id
        Scenarios = [| scenario $"o-{id}" name |]
    }


let plan contexts =
    Informedica.GenPRES.Shared.Models.OrderPlan.create draft contexts

let one = plan [| context "c-1" "paracetamol" |]
let two = plan [| context "c-1" "paracetamol"; context "c-2" "ibuprofen" |]


let head: SignedOrderPlan =
    {
        Head =
            {
                Id = "plan-1"
                No = 1
                By =
                    {
                        UserId = "u"
                        DisplayName = "Stub Prescriber"
                        Role = UserRole.Prescriber
                    }
                SignedAt = DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc)
            }
        PatientId = "p"
        Base = None
        OrderContexts = two.OrderContexts
        Patient = draft
        Identity = None
        Verified = true
    }
