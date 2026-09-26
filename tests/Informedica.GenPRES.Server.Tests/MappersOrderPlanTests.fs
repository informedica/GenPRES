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
    |> Medication.toOrderDto Scenarios.testStart
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
        Identity = None
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
                |> SessionMapper.toSigned false None
                |> Expect.equal "the same signed plan" signed
            }

            test "outside L3: the signer's role is not recorded, a Reader comes back a Prescriber" {
                let byReader =
                    { signed with Head = { signed.Head with By = { prescriber with Role = UserRole.Reader } } }

                (byReader |> SessionMapper.ofSigned |> SessionMapper.toSigned false None).Head.By.Role
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

            test
                "what the client keeps of an open Session: the head the signed plan it knows, none when it cannot be read" {
                let version =
                    signed
                    |> SessionMapper.ofSigned
                    |> OrderPlanVersion.Dto.fromDto
                    |> Result.defaultWith (fun e -> failtest $"no version: %A{e}")

                let stored: OpenedSession =
                    {
                        User = Some prescriber
                        PatientId = Some "stub-patient"
                        EhrData = None
                        Patient =
                            StubPatientData.patient
                            |> Patient.parse
                            |> Result.defaultWith (fun e -> failtest $"no patient: %A{e}")
                            |> Some
                        Measured = Measurements.none
                        OpenedToken = Some(OpenedToken "opened-1")
                        KeyThumbprint = Some "thumb"
                        Head = Some(StoredVersion.Readable(version, None))
                    }

                let opened = SessionMapper.toOpened false stored

                opened.User |> Expect.equal "user" (Some prescriber)

                opened.PatientContext
                |> Expect.equal
                    "patient"
                    (Some
                        {
                            PatientId = "stub-patient"
                            Identity = None
                            Patient = Some StubPatientData.patient
                        })

                let identified =
                    { stored with EhrData = Some(StubPatientData.data "stub-patient") }
                    |> SessionMapper.toOpened false

                identified.PatientContext
                |> Option.bind _.Identity
                |> Expect.equal
                    "the identity, the birthdate as three integers"
                    (Some(
                        {
                            Name = StubPatientData.name
                            BirthYear = 2016
                            BirthMonth = 3
                            BirthDay = 15
                        }
                        : NameAndBirthDate
                    ))

                opened.OpenedToken |> Expect.equal "token" (Some(OpenedToken "opened-1"))
                opened.KeyThumbprint |> Expect.equal "thumbprint" (Some "thumb")

                opened.Head
                |> Expect.equal
                    "head"
                    (Some(version |> OrderPlanVersion.Dto.toDto |> SessionMapper.toSigned false None))

                let unreadable =
                    StoredVersion.Unreadable
                        {
                            Id = "plan-2"
                            No = 2
                            PatientId = "stub-patient"
                            Base = Some signed.Head.Id
                            SignedBy =
                                {
                                    UserId = "b"
                                    DisplayName = "B"
                                }
                            SignedAt = signed.Head.SignedAt
                            Identity = None
                            Reason = "json_version 9 is newer than this release knows"
                        }

                (SessionMapper.toOpened false { stored with Head = Some unreadable }).Head
                |> Expect.isNone "an unreadable head cannot be shown"

                SessionMapper.toOpened
                    false
                    { stored with
                        User = None
                        PatientId = None
                        Patient = None
                        Head = None
                    }
                |> Expect.equal
                    "anonymous, from nothing"
                    {
                        User = None
                        PatientContext = None
                        OpenedToken = Some(OpenedToken "opened-1")
                        KeyThumbprint = Some "thumb"
                        Head = None
                    }
            }
        ]
