/// The order plan and session mappers: an order plan to the domain's Dto and back, a signed
/// order plan as the record's version and back, a data notice's patient, the opened session.
module Informedica.GenPRES.Server.Tests.MappersOrderPlanTests

open System
open Expecto
open Expecto.Flip
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
// after Expecto, whose FocusState has a Normal case too
open Shared.Types
open ServerApi


let order: Order =
    Scenarios.pcmSupp
    |> Medication.toOrderDto
    |> Mappers.Order.mapFromOrderToShared [| "paracetamol" |]


let scenario name : OrderScenario =
    Shared.Models.OrderScenario.create
        "koorts"
        name
        "zetpil"
        "rect"
        (Discontinuous "3-4 x/dag")
        None
        (Some "paracetamol")
        (Some "paracetamol")
        [||]
        [| "paracetamol" |]
        [| "paracetamol" |]
        [| [| Valid [| Normal "paracetamol "; Bold "240 mg" |] |] |]
        [||]
        [||]
        order
        true
        false
        None
        [| "gpk-1" |]


let context id category name : OrderContext =
    { Shared.Models.OrderContext.empty with
        Id = id
        Category = category
        DemoVersion = false
        Filter = { Shared.Models.OrderContext.filter with Generic = Some name }
        Patient = StubPatientData.patient
        Scenarios = [| scenario name |]
        Intake = { Shared.Models.Totals.empty with Volume = [| Normal "10 ml" |] }
    }


let plan: OrderPlan =
    {
        Patient = StubPatientData.patient
        Filtered = [| "c-1" |]
        OrderContexts =
            [|
                context "c-1" OrderCategory.Drug "paracetamol"
                context "c-2" (OrderCategory.Nutrition NutritionCategory.TPN) "glucose"
            |]
        Totals = { Shared.Models.Totals.empty with Energy = [| Bold "50 kcal" |] }
    }


let prescriber: UserContext =
    {
        UserId = "u-1"
        DisplayName = "Stub Prescriber"
        Role = UserRole.Prescriber
    }


let signed: SignedOrderPlan =
    {
        Head =
            {
                Id = "plan-2"
                No = 2
                By = prescriber
                SignedAt = DateTime(2026, 9, 17, 9, 30, 0, DateTimeKind.Utc)
            }
        PatientId = "stub-patient"
        Base = Some "plan-1"
        OrderContexts = plan.OrderContexts
        Patient = StubPatientData.patient
        Verified = true
    }


[<Tests>]
let tests =
    testList
        "the order plan and session mappers"
        [
            test "L3: an order plan to the Dto and back is the identity, the demo flag the server's" {
                plan
                |> OrderPlanMapper.ofModel
                |> OrderPlanMapper.toModel false
                |> Expect.equal "the same plan" plan

                (plan |> OrderPlanMapper.ofModel |> OrderPlanMapper.toModel true).OrderContexts
                |> Array.forall _.DemoVersion
                |> Expect.isTrue "demo on every context"
            }

            test "the plan's Dto carries the categories as strings and parses" {
                let dto = plan |> OrderPlanMapper.ofModel

                dto.Contexts
                |> Array.map _.Category
                |> Expect.equal "category strings" [| "drug"; "nutrition:tpn" |]

                match dto |> OrderPlan.Dto.fromDto with
                | Ok p ->
                    p.Filtered |> Expect.equal "the filter" [| "c-1" |]
                    p.Contexts |> Array.map _.Id |> Expect.equal "the contexts" [| "c-1"; "c-2" |]
                    p.Totals.Energy |> Expect.equal "the totals" (Some "#50 kcal#")
                | Error e -> failtest $"no order plan: %A{e}"
            }

            test "every nutrition category both ways" {
                for c in
                    [
                        NutritionCategory.EnteralFeeding
                        NutritionCategory.EnteralSupplement
                        NutritionCategory.TPN
                        NutritionCategory.Lipid
                        NutritionCategory.ElectrolyteGlucose
                    ] do
                    c
                    |> OrderPlanMapper.nutritionCategory
                    |> Informedica.GenOrder.Lib.Types.OrderCategory.Nutrition
                    |> OrderContextMapper.Category.ofDomain
                    |> Expect.equal $"{c}" (OrderCategory.Nutrition c)
            }

            test "L3: a signed order plan to the version's Dto and back, on what the version records" {
                signed
                |> SessionMapper.ofSigned
                |> SessionMapper.toSigned false
                |> Expect.equal "the same signed plan" signed
            }

            test "outside L3: the signer's role is not recorded, a Reader comes back a Prescriber" {
                let byReader =
                    { signed with Head = { signed.Head with By = { prescriber with Role = UserRole.Reader } } }

                (byReader |> SessionMapper.ofSigned |> SessionMapper.toSigned false).Head.By.Role
                |> Expect.equal "only a Prescriber signs" UserRole.Prescriber
            }

            test "the version's Dto parses: the record reads what the mapper writes" {
                match signed |> SessionMapper.ofSigned |> OrderPlanVersion.Dto.fromDto with
                | Ok v ->
                    v.Id |> Expect.equal "id" "plan-2"
                    v.No |> Expect.equal "no" 2
                    v.Base |> Expect.equal "base" (Some "plan-1")
                    v.SignedBy.DisplayName |> Expect.equal "signer" "Stub Prescriber"
                    v.Verified |> Expect.isTrue "verified"
                    v.Plan.Contexts.Length |> Expect.equal "the contexts" 2
                    v.Plan.Filtered |> Expect.isEmpty "no filter on the wire"
                    v.Plan.Totals |> Expect.equal "no totals on the wire" Totals.empty
                | Error e -> failtest $"no version: %A{e}"
            }

            test "a data notice's patient goes through the patient mapper, none stays none" {
                let n =
                    {
                        Data = Some StubPatientData.patient
                        Token = "t-1"
                    }

                let data = n |> SessionMapper.noticeData
                data |> Option.isSome |> Expect.isTrue "a reading"
                SessionMapper.notice "t-1" data |> Expect.equal "the same notice" n

                { n with Data = None }
                |> SessionMapper.noticeData
                |> Expect.equal "unreadable" None
            }

            test "an opened session from its parts, the head the signed plan the client knows" {
                let opened =
                    SessionMapper.opened
                        false
                        (Some prescriber)
                        (Some("stub-patient", Some(StubPatientData.patient |> Patient.ofModel)))
                        (Some(OpenedToken "opened-1"))
                        (Some "thumb")
                        (Some(signed |> SessionMapper.ofSigned))

                opened.User |> Expect.equal "user" (Some prescriber)

                opened.PatientContext
                |> Expect.equal
                    "patient"
                    (Some
                        {
                            PatientId = "stub-patient"
                            Patient = Some StubPatientData.patient
                        })

                opened.OpenedToken |> Expect.equal "token" (Some(OpenedToken "opened-1"))
                opened.KeyThumbprint |> Expect.equal "thumbprint" (Some "thumb")
                opened.Head |> Expect.equal "head" (Some signed)

                (SessionMapper.opened false None None None None None).Head
                |> Expect.equal "from nothing" None
            }
        ]
