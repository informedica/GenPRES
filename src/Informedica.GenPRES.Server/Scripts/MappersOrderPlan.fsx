// Step 3.3 of docs/implementation-plans/725-contract-model-dto-domain-flow.md (issue #752):
// the order plan mapper and the session mapper. The contract model's order plan becomes the
// domain's `OrderPlan.Dto` in one total step and comes back from the Dto alone; a signed order
// plan becomes an `OrderPlanVersion.Dto` and back; a data notice's patient goes through the
// patient mapper; and what the client keeps of an open Session is built from domain parts,
// the shape the session state takes in Phase 5.
//
// Prototype per the script-only policy in AGENTS.md. What migration touches:
//
//   1. A new src/Informedica.GenPRES.Server/ServerApi.Mappers.OrderPlan.fs after the order
//      context mapper: `OrderPlanMapper`.
//   2. A new src/Informedica.GenPRES.Server/ServerApi.Mappers.Session.fs after it:
//      `SessionMapper`.
//   3. tests/Informedica.GenPRES.Server.Tests/MappersOrderPlanTests.fs: the tests below.
//
// Run from this directory: dotnet fsi MappersOrderPlan.fsx

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"
#load "../../../tests/Informedica.GenORDER.Tests/Scenarios.fs"

open System
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
// after the domain, so that the contract model's cases win unqualified
open Shared.Types
open ServerApi


/// The order plan mapper: the contract model's order plan to the domain's Dto and back, its
/// contexts through the order context mapper.
module OrderPlanMapper =

    module Category = OrderContextMapper.Category


    /// A nutrition category from the wire, for a context the plan is asked to make.
    let nutritionCategory (c: NutritionCategory) =
        match c |> OrderCategory.Nutrition |> Category.toDomain with
        | Informedica.GenOrder.Lib.Types.OrderCategory.Nutrition c -> c
        | Informedica.GenOrder.Lib.Types.OrderCategory.Drug -> invalidOp "a nutrition category maps to a nutrition category"


    /// Total: the contract model's order plan as the domain's Dto, nothing dropped.
    let ofModel (plan: OrderPlan) : OrderPlan.Dto.Dto =
        {
            Patient = plan.Patient |> Patient.ofModel
            Filtered = plan.Filtered
            Contexts = plan.OrderContexts |> Array.map OrderContextMapper.ofModel
            Totals = plan.Totals |> OrderContextMapper.totals
        }


    /// The Dto as the contract model's order plan, from the Dto alone; the demo flag on every
    /// context is the server's.
    let toModel (demo: bool) (dto: OrderPlan.Dto.Dto) : OrderPlan =
        {
            Patient = dto.Patient |> Patient.toModel
            Filtered = dto.Filtered
            OrderContexts = dto.Contexts |> Array.map (OrderContextMapper.toModel demo)
            Totals = dto.Totals |> OrderContextMapper.totalsBack
        }


/// The session mapper: a signed order plan as the record's version and back, a data notice's
/// patient, and what the client keeps of an open Session from the parts the state will hold.
module SessionMapper =

    /// Who signed, as the version records it. The role is not recorded: only a Prescriber
    /// signs.
    let signer (user: UserContext) : Signer.Dto.Dto =
        {
            UserId = user.UserId
            DisplayName = user.DisplayName
        }


    let signerBack (dto: Signer.Dto.Dto) : UserContext =
        {
            UserId = dto.UserId
            DisplayName = dto.DisplayName
            Role = UserRole.Prescriber
        }


    /// Total: a signed order plan from the wire as the version's Dto. The contract model
    /// carries the contexts and the patient; the version stores an order plan, so the filter
    /// is empty and the totals are none.
    let ofSigned (signed: SignedOrderPlan) : OrderPlanVersion.Dto.Dto =
        {
            Id = signed.Head.Id
            No = signed.Head.No
            PatientId = signed.PatientId
            Base = signed.Base
            SignedBy = signed.Head.By |> signer
            SignedAt = signed.Head.SignedAt
            Plan =
                {
                    Patient = signed.Patient |> Patient.ofModel
                    Filtered = [||]
                    Contexts = signed.OrderContexts |> Array.map OrderContextMapper.ofModel
                    Totals = Shared.Models.Totals.empty |> OrderContextMapper.totals
                }
            Verified = signed.Verified
        }


    /// The version's Dto as the signed order plan the wire carries.
    let toSigned (demo: bool) (dto: OrderPlanVersion.Dto.Dto) : SignedOrderPlan =
        {
            Head =
                {
                    Id = dto.Id
                    No = dto.No
                    By = dto.SignedBy |> signerBack
                    SignedAt = dto.SignedAt
                }
            PatientId = dto.PatientId
            Base = dto.Base
            OrderContexts = dto.Plan.Contexts |> Array.map (OrderContextMapper.toModel demo)
            Patient = dto.Plan.Patient |> Patient.toModel
            Verified = dto.Verified
        }


    /// The patient data a notice is about, as the Dto; none when the reading could not be
    /// read.
    let noticeData (notice: DataNotice) = notice.Data |> Option.map Patient.ofModel


    let notice (token: string) (data: Informedica.GenForm.Lib.Patient.Dto.Dto option) : DataNotice =
        {
            Data = data |> Option.map Patient.toModel
            Token = token
        }


    /// What the client keeps of an open Session, from the parts the session state holds once
    /// it is on domain values: the user, the patient the Session is for with the data it opened
    /// on, the token, the key thumbprint and the head of the record.
    let opened
        (demo: bool)
        (user: UserContext option)
        (patient: (string * Informedica.GenForm.Lib.Patient.Dto.Dto option) option)
        (token: OpenedToken option)
        (thumbprint: string option)
        (head: OrderPlanVersion.Dto.Dto option)
        : SessionOpened
        =
        {
            User = user
            PatientContext =
                patient
                |> Option.map (fun (id, data) ->
                    {
                        PatientId = id
                        Patient = data |> Option.map Patient.toModel
                    }
                )
            OpenedToken = token
            KeyThumbprint = thumbprint
            Head = head |> Option.map (toSigned demo)
        }


// ---------------------------------------------------------------------------
// Tests, for tests/Informedica.GenPRES.Server.Tests/MappersOrderPlanTests.fs
// ---------------------------------------------------------------------------

module MappersOrderPlanTests =

    open Expecto
    open Expecto.Flip
    // after Expecto, whose FocusState has a Normal case too
    open Shared.Types


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


    let tests =
        testList
            "the order plan and session mappers"
            [
                test "L3: an order plan to the Dto and back is the identity, the demo flag the server's" {
                    plan |> OrderPlanMapper.ofModel |> OrderPlanMapper.toModel false |> Expect.equal "the same plan" plan

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
                    signed |> SessionMapper.ofSigned |> SessionMapper.toSigned false |> Expect.equal "the same signed plan" signed
                }

                test "outside L3: the signer's role is not recorded, a Reader comes back a Prescriber" {
                    let byReader =
                        { signed with
                            Head = { signed.Head with By = { prescriber with Role = UserRole.Reader } }
                        }

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
                    let n = { Data = Some StubPatientData.patient; Token = "t-1" }
                    let data = n |> SessionMapper.noticeData
                    data |> Option.isSome |> Expect.isTrue "a reading"
                    SessionMapper.notice "t-1" data |> Expect.equal "the same notice" n

                    { n with Data = None } |> SessionMapper.noticeData |> Expect.equal "unreadable" None
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


MappersOrderPlanTests.tests |> Expecto.Tests.runTestsWithCLIArgs [] [||] |> ignore
