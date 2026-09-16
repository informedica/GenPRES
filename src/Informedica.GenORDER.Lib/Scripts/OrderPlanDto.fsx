// Step 1.4 of docs/implementation-plans/725-contract-model-dto-domain-flow.md (issue #733):
// the Dtos of the order plan types: Totals.Dto, OrderContext.Dto, PlanContext.Dto,
// OrderPlan.Dto, Signer.Dto and OrderPlanVersion.Dto. ADR-0008 invariants 1 to 5: one
// aggregate each, toDto total, fromDto a Result that never throws and never drops a part,
// no version field on any Dto (the structure version sits beside the stored root).
//
// Prototype per the script-only policy in AGENTS.md. The library's DtoError cannot grow in
// a script, so the union is shadowed here with the three cases 1.4 adds and the library's
// errors are mapped into it. What migration touches:
//
//   1. Types.fs: DtoError gains `UnknownCategory of string`, `Missing of string` and
//      `Patient of PatientError`.
//   2. Totals.fs: `Totals.Dto` at the end of module Totals.
//   3. Api.fs: `OrderContext.Dto` at the end of module OrderContext, and `OrderCategoryDto`
//      next to `DoseTypeDto`.
//   4. The file now called OrderPlan.fs holds the Dto helpers and comes before Api.fs; it is
//      renamed Dtos.fs. A new OrderPlan.fs after Api.fs holds `PlanContext`, `OrderPlan`,
//      `Signer` and `OrderPlanVersion`, their Dtos now and the order plan rules in Phase 2,
//      which need OrderContext.evaluate from Api.fs.
//   5. tests/Informedica.GenORDER.Tests/Tests.fs: the tests below, in DtoTests.
//
// Run from this directory: dotnet fsi OrderPlanDto.fsx

#I __SOURCE_DIRECTORY__

#r "nuget: Expecto"

#load "load.fsx"

open System
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


// ---------------------------------------------------------------------------
// 1. Types.fs: DtoError with the three cases 1.4 adds
// ---------------------------------------------------------------------------

/// Why a Dto does not parse to its domain value.
[<RequireQualifiedAccess>]
type DtoError =
    // A dose type string the Dto carries that names no dose type
    | UnknownDoseType of string
    // A text block kind the Dto carries that names no kind
    | UnknownTextKind of string
    // The order of a scenario could not be created from its Dto
    | OrderNotCreated of string
    // An order category string the Dto carries that names no category
    | UnknownCategory of string
    // A nested Dto a serializer left null, by field name
    | Missing of string
    // The patient of a context or an order plan is none
    | Patient of PatientError


/// Script only: the library's errors, read into the shadow union.
module DtoError =

    let ofLib (e: Types.DtoError) =
        match e with
        | Types.DtoError.UnknownDoseType s -> DtoError.UnknownDoseType s
        | Types.DtoError.UnknownTextKind s -> DtoError.UnknownTextKind s
        | Types.DtoError.OrderNotCreated s -> DtoError.OrderNotCreated s


    let ofLibList (r: Result<'a, Types.DtoError list>) = r |> Result.mapError (List.map ofLib)


// ---------------------------------------------------------------------------
// 2. Totals.fs
// ---------------------------------------------------------------------------

module Totals =

    open Informedica.GenOrder.Lib.Totals


    /// The serializable shape of the totals: the same string option fields, its own type.
    module Dto =

        type Dto =
            {
                Volume: string option
                Energy: string option
                Protein: string option
                Carbohydrate: string option
                Fat: string option
                Sodium: string option
                Potassium: string option
                Chloride: string option
                Calcium: string option
                Phosphate: string option
                Magnesium: string option
                Iron: string option
                VitaminD: string option
                Ethanol: string option
                Propyleenglycol: string option
                BenzylAlcohol: string option
                BoricAcid: string option
            }


        let toDto (t: Totals) : Dto =
            {
                Volume = t.Volume
                Energy = t.Energy
                Protein = t.Protein
                Carbohydrate = t.Carbohydrate
                Fat = t.Fat
                Sodium = t.Sodium
                Potassium = t.Potassium
                Chloride = t.Chloride
                Calcium = t.Calcium
                Phosphate = t.Phosphate
                Magnesium = t.Magnesium
                Iron = t.Iron
                VitaminD = t.VitaminD
                Ethanol = t.Ethanol
                Propyleenglycol = t.Propyleenglycol
                BenzylAlcohol = t.BenzylAlcohol
                BoricAcid = t.BoricAcid
            }


        /// A field copy; every Dto is totals.
        let fromDto (dto: Dto) : Result<Totals, DtoError list> =
            Ok
                {
                    Volume = dto.Volume
                    Energy = dto.Energy
                    Protein = dto.Protein
                    Carbohydrate = dto.Carbohydrate
                    Fat = dto.Fat
                    Sodium = dto.Sodium
                    Potassium = dto.Potassium
                    Chloride = dto.Chloride
                    Calcium = dto.Calcium
                    Phosphate = dto.Phosphate
                    Magnesium = dto.Magnesium
                    Iron = dto.Iron
                    VitaminD = dto.VitaminD
                    Ethanol = dto.Ethanol
                    Propyleenglycol = dto.Propyleenglycol
                    BenzylAlcohol = dto.BenzylAlcohol
                    BoricAcid = dto.BoricAcid
                }


// ---------------------------------------------------------------------------
// 3. Api.fs: the category strings and OrderContext.Dto
// ---------------------------------------------------------------------------

/// The string form of an order category, for the Dtos: `drug`, or `nutrition:` and the
/// category's name.
module OrderCategoryDto =

    let nutrition =
        [
            NutritionCategory.EnteralFeeding, "enteral-feeding"
            NutritionCategory.EnteralSupplement, "enteral-supplement"
            NutritionCategory.TPN, "tpn"
            NutritionCategory.Lipid, "lipid"
            NutritionCategory.ElectrolyteGlucose, "electrolyte-glucose"
        ]


    let toString =
        function
        | OrderCategory.Drug -> "drug"
        | OrderCategory.Nutrition c ->
            let name = nutrition |> List.find (fst >> (=) c) |> snd
            $"nutrition:{name}"


    let fromString (s: string) : Result<OrderCategory, DtoError> =
        match s |> DtoResult.orBlank |> String.toLower |> String.trim with
        | "drug" -> Ok OrderCategory.Drug
        | s when s.StartsWith "nutrition:" ->
            let name = s.Substring("nutrition:".Length)

            match nutrition |> List.tryFind (snd >> (=) name) with
            | Some(c, _) -> Ok(OrderCategory.Nutrition c)
            | None -> Error(DtoError.UnknownCategory s)
        | _ -> Error(DtoError.UnknownCategory(DtoResult.orBlank s))


/// A nested Dto a serializer left null is an error, by the field's name.
module Nested =

    let required name (dto: 'a) (read: 'a -> Result<'b, DtoError list>) =
        if isNull (box dto) then
            Error [ DtoError.Missing name ]
        else
            read dto


module OrderContext =

    open Informedica.GenOrder.Lib.OrderContext


    /// The serializable shape of an OrderContext: its filter, its patient and its scenarios,
    /// each as its own Dto.
    module Dto =

        type Dto =
            {
                Filter: Filter.Dto.Dto
                Patient: Patient.Dto.Dto
                Scenarios: OrderScenario.Dto.Dto[]
            }


        let toDto (ctx: OrderContext) : Dto =
            {
                Filter = ctx.Filter |> Filter.Dto.toDto
                Patient = ctx.Patient |> Patient.Dto.toDto
                Scenarios = ctx.Scenarios |> Array.map OrderScenario.Dto.toDto
            }


        /// The context a Dto is, or every reason it is none: the filter's, the patient's
        /// and every scenario's, a scenario that fails never dropped.
        let fromDto (dto: Dto) : Result<OrderContext, DtoError list> =
            let filter =
                Nested.required "Filter" dto.Filter (Filter.Dto.fromDto >> DtoError.ofLibList)

            let patient =
                Nested.required "Patient" dto.Patient (fun p ->
                    p |> Patient.Dto.fromDto |> Result.mapError (List.map DtoError.Patient)
                )

            let scenarios =
                dto.Scenarios
                |> DtoResult.orEmpty
                |> Array.toList
                |> List.map (fun sc ->
                    Nested.required "Scenario" sc (OrderScenario.Dto.fromDto >> DtoError.ofLibList)
                )
                |> DtoResult.sequence
                |> Result.mapError List.concat

            match filter, patient, scenarios with
            | Ok filter, Ok patient, Ok scenarios ->
                Ok
                    {
                        Filter = filter
                        Patient = patient
                        Scenarios = scenarios |> List.toArray
                    }
            | _ ->
                let errorsOf r =
                    match r with
                    | Error es -> es
                    | Ok _ -> []

                Error(errorsOf filter @ errorsOf patient @ errorsOf scenarios)


// ---------------------------------------------------------------------------
// 4. OrderPlan.fs, after Api.fs
// ---------------------------------------------------------------------------

module PlanContext =

    /// The serializable shape of a PlanContext: the category as a string, the context and
    /// the intake as their own Dtos.
    module Dto =

        type Dto =
            {
                Id: string
                Category: string
                Context: OrderContext.Dto.Dto
                Intake: Totals.Dto.Dto
            }


        let toDto (pc: PlanContext) : Dto =
            {
                Id = pc.Id
                Category = pc.Category |> OrderCategoryDto.toString
                Context = pc.Context |> OrderContext.Dto.toDto
                Intake = pc.Intake |> Totals.Dto.toDto
            }


        let fromDto (dto: Dto) : Result<PlanContext, DtoError list> =
            let category = dto.Category |> OrderCategoryDto.fromString |> Result.mapError List.singleton
            let context = Nested.required "Context" dto.Context OrderContext.Dto.fromDto
            let intake = Nested.required "Intake" dto.Intake Totals.Dto.fromDto

            match category, context, intake with
            | Ok category, Ok context, Ok intake ->
                Ok
                    {
                        Id = dto.Id |> DtoResult.orBlank
                        Category = category
                        Context = context
                        Intake = intake
                    }
            | _ ->
                let errorsOf r =
                    match r with
                    | Error es -> es
                    | Ok _ -> []

                Error(errorsOf category @ errorsOf context @ errorsOf intake)


module OrderPlan =

    /// The serializable shape of an OrderPlan: the patient, the filtered ids, every
    /// context and the totals.
    module Dto =

        type Dto =
            {
                Patient: Patient.Dto.Dto
                Filtered: string[]
                Contexts: PlanContext.Dto.Dto[]
                Totals: Totals.Dto.Dto
            }


        let toDto (plan: OrderPlan) : Dto =
            {
                Patient = plan.Patient |> Patient.Dto.toDto
                Filtered = plan.Filtered
                Contexts = plan.Contexts |> Array.map PlanContext.Dto.toDto
                Totals = plan.Totals |> Totals.Dto.toDto
            }


        /// The order plan a Dto is, or every reason it is none, over the patient and every
        /// context.
        let fromDto (dto: Dto) : Result<OrderPlan, DtoError list> =
            let patient =
                Nested.required "Patient" dto.Patient (fun p ->
                    p |> Patient.Dto.fromDto |> Result.mapError (List.map DtoError.Patient)
                )

            let contexts =
                dto.Contexts
                |> DtoResult.orEmpty
                |> Array.toList
                |> List.map (fun pc -> Nested.required "Context" pc PlanContext.Dto.fromDto)
                |> DtoResult.sequence
                |> Result.mapError List.concat

            let totals = Nested.required "Totals" dto.Totals Totals.Dto.fromDto

            match patient, contexts, totals with
            | Ok patient, Ok contexts, Ok totals ->
                Ok
                    {
                        Patient = patient
                        Filtered = dto.Filtered |> DtoResult.orEmpty
                        Contexts = contexts |> List.toArray
                        Totals = totals
                    }
            | _ ->
                let errorsOf r =
                    match r with
                    | Error es -> es
                    | Ok _ -> []

                Error(errorsOf patient @ errorsOf contexts @ errorsOf totals)


module Signer =

    module Dto =

        type Dto = { UserId: string; DisplayName: string }


        let toDto (s: Signer) : Dto =
            {
                UserId = s.UserId
                DisplayName = s.DisplayName
            }


        let fromDto (dto: Dto) : Result<Signer, DtoError list> =
            Ok
                {
                    UserId = dto.UserId |> DtoResult.orBlank
                    DisplayName = dto.DisplayName |> DtoResult.orBlank
                }


module OrderPlanVersion =

    /// The serializable shape of an order plan version, the stored root: its identity, the
    /// signer, the time, and the whole order plan. No structure version here; the database
    /// keeps that beside the row.
    module Dto =

        type Dto =
            {
                Id: string
                No: int
                PatientId: string
                Base: string option
                SignedBy: Signer.Dto.Dto
                SignedAt: DateTime
                Plan: OrderPlan.Dto.Dto
                Verified: bool
            }


        let toDto (v: OrderPlanVersion) : Dto =
            {
                Id = v.Id
                No = v.No
                PatientId = v.PatientId
                Base = v.Base
                SignedBy = v.SignedBy |> Signer.Dto.toDto
                SignedAt = v.SignedAt
                Plan = v.Plan |> OrderPlan.Dto.toDto
                Verified = v.Verified
            }


        let fromDto (dto: Dto) : Result<OrderPlanVersion, DtoError list> =
            let signer = Nested.required "SignedBy" dto.SignedBy Signer.Dto.fromDto
            let plan = Nested.required "Plan" dto.Plan OrderPlan.Dto.fromDto

            match signer, plan with
            | Ok signer, Ok plan ->
                Ok
                    {
                        Id = dto.Id |> DtoResult.orBlank
                        No = dto.No
                        PatientId = dto.PatientId |> DtoResult.orBlank
                        Base = dto.Base
                        SignedBy = signer
                        SignedAt = dto.SignedAt
                        Plan = plan
                        Verified = dto.Verified
                    }
            | _ ->
                let errorsOf r =
                    match r with
                    | Error es -> es
                    | Ok _ -> []

                Error(errorsOf signer @ errorsOf plan)


// ---------------------------------------------------------------------------
// 5. Tests, for DtoTests.
// ---------------------------------------------------------------------------

open Expecto
open Expecto.Flip


module Fixtures =

    let order =
        match Scenarios.pcmSupp |> Medication.toOrderDto |> Order.Dto.fromDto with
        | Ok o -> o
        | Error e -> failwith $"fixture order could not be created: {e}"

    let kg = Units.Weight.kiloGram
    let cm = Units.Height.centiMeter
    let day = Units.Time.day

    let child =
        { Patient.patient with
            Department = Some "ICK"
            Gender = Male
            Age = Some(ValueUnit.singleWithUnit day 3650N)
            Weight = Some(ValueUnit.singleWithUnit kg 32N)
            Height = Some(ValueUnit.singleWithUnit cm 140N)
            Access = [ PVL ]
        }

    let filter: Filter =
        {
            Indications = [| "koorts"; "pijn" |]
            Generics = [| "paracetamol" |]
            Routes = [| "rect" |]
            Forms = [| "zetpil" |]
            DoseTypes = [| Informedica.GenForm.Lib.Types.Discontinuous "3-4 x/dag" |]
            Diluents = [||]
            Components = [| "paracetamol" |]
            Indication = Some "koorts"
            Generic = Some "paracetamol"
            Route = Some "rect"
            Form = Some "zetpil"
            DoseType = Some(Informedica.GenForm.Lib.Types.Discontinuous "3-4 x/dag")
            Diluent = None
            SelectedComponents = [| "paracetamol" |]
        }

    let scenario: OrderScenario =
        {
            No = 1
            Name = "paracetamol"
            Indication = "koorts"
            Form = "zetpil"
            Route = "rect"
            DoseType = Informedica.GenForm.Lib.Types.Discontinuous "3-4 x/dag"
            Diluent = None
            Component = Some "paracetamol"
            Item = Some "paracetamol"
            Diluents = [||]
            Components = [| "paracetamol" |]
            Items = [| "paracetamol" |]
            Prescription = [| [| Valid "paracetamol 240 mg 3 x/dag" |] |]
            Preparation = [| [| Valid "zetpil 240 mg" |] |]
            Administration = [| [| Warning "rectaal" |] |]
            Order = order
            UseAdjust = true
            UseRenalRule = false
            RenalRule = None
            ProductsIds = [| "gpk-1" |]
        }

    let context: OrderContext =
        {
            Filter = filter
            Patient = child
            Scenarios = [| scenario |]
        }

    let totals: Totals =
        {
            Volume = Some "100 ml"
            Energy = Some "50 kcal"
            Protein = None
            Carbohydrate = None
            Fat = None
            Sodium = Some "3 mmol"
            Potassium = None
            Chloride = None
            Calcium = None
            Phosphate = None
            Magnesium = None
            Iron = None
            VitaminD = None
            Ethanol = None
            Propyleenglycol = None
            BenzylAlcohol = None
            BoricAcid = None
        }

    let planContext: PlanContext =
        {
            Id = "ctx-1"
            Category = OrderCategory.Drug
            Context = context
            Intake = totals
        }

    let feeding: PlanContext =
        { planContext with
            Id = "ctx-2"
            Category = OrderCategory.Nutrition NutritionCategory.EnteralFeeding
        }

    let plan: OrderPlan =
        {
            Patient = child
            Filtered = [| "ctx-1" |]
            Contexts = [| planContext; feeding |]
            Totals = totals
        }

    let version: OrderPlanVersion =
        {
            Id = "v-1"
            No = 1
            PatientId = "stub-patient"
            Base = None
            SignedBy =
                {
                    UserId = "u-1"
                    DisplayName = "Stub Prescriber"
                }
            SignedAt = DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc)
            Plan = plan
            Verified = true
        }


let l1 name (x: 'a) (toDto: 'a -> 'd) (fromDto: 'd -> Result<'a, DtoError list>) =
    test $"L1 for {name}" { x |> toDto |> fromDto |> Expect.equal "the same value" (Ok x) }


let l2 name (x: 'a) (toDto: 'a -> 'd) (fromDto: 'd -> Result<'a, DtoError list>) =
    test $"L2 for {name}, in canonical form" {
        let dto = x |> toDto

        dto
        |> fromDto
        |> Result.map (toDto >> Canonical.serialize)
        |> Expect.equal "the same form" (Ok(Canonical.serialize dto))
    }


let tests =
    testList
        "Order plan Dtos"
        [
            testList
                "laws"
                [
                    l1 "totals" Fixtures.totals Totals.Dto.toDto Totals.Dto.fromDto
                    l2 "totals" Fixtures.totals Totals.Dto.toDto Totals.Dto.fromDto
                    l1 "an order context" Fixtures.context OrderContext.Dto.toDto OrderContext.Dto.fromDto
                    l2 "an order context" Fixtures.context OrderContext.Dto.toDto OrderContext.Dto.fromDto
                    l1 "a plan context" Fixtures.feeding PlanContext.Dto.toDto PlanContext.Dto.fromDto
                    l2 "a plan context" Fixtures.feeding PlanContext.Dto.toDto PlanContext.Dto.fromDto
                    l1 "an order plan" Fixtures.plan OrderPlan.Dto.toDto OrderPlan.Dto.fromDto
                    l2 "an order plan" Fixtures.plan OrderPlan.Dto.toDto OrderPlan.Dto.fromDto
                    l1 "an order plan version" Fixtures.version OrderPlanVersion.Dto.toDto OrderPlanVersion.Dto.fromDto
                    l2 "an order plan version" Fixtures.version OrderPlanVersion.Dto.toDto OrderPlanVersion.Dto.fromDto
                ]

            testList
                "categories"
                [
                    test "every category round-trips as a string" {
                        [
                            OrderCategory.Drug
                            OrderCategory.Nutrition NutritionCategory.EnteralFeeding
                            OrderCategory.Nutrition NutritionCategory.EnteralSupplement
                            OrderCategory.Nutrition NutritionCategory.TPN
                            OrderCategory.Nutrition NutritionCategory.Lipid
                            OrderCategory.Nutrition NutritionCategory.ElectrolyteGlucose
                        ]
                        |> List.map (fun c -> c |> OrderCategoryDto.toString |> OrderCategoryDto.fromString)
                        |> List.forall Result.isOk
                        |> Expect.isTrue "each read back"
                    }

                    test "an unknown category is an error" {
                        { (Fixtures.feeding |> PlanContext.Dto.toDto) with
                            Category = "nutrition:soup"
                        }
                        |> PlanContext.Dto.fromDto
                        |> Expect.equal "named" (Error [ DtoError.UnknownCategory "nutrition:soup" ])
                    }
                ]

            testList
                "fromDto refuses"
                [
                    test "an order plan whose patient is below the minimum data, with every context's errors" {
                        let dto = Fixtures.plan |> OrderPlan.Dto.toDto

                        let brokenContext =
                            { dto.Contexts[0] with
                                Category = "x"
                            }

                        { dto with
                            Patient = { dto.Patient with AgeDays = None; WeightMeasured = false }
                            Contexts = [| brokenContext; dto.Contexts[1] |]
                        }
                        |> OrderPlan.Dto.fromDto
                        |> Expect.equal
                            "both reported"
                            (Error
                                [
                                    DtoError.Patient PatientError.NoAgeOrMeasuredWeightAndHeight
                                    DtoError.UnknownCategory "x"
                                ])
                    }

                    test "a scenario whose order cannot be created is an error, not dropped" {
                        let dto = Fixtures.context |> OrderContext.Dto.toDto
                        let broken = Order.Dto.Dto(dto.Scenarios[0].Order.Id, "paracetamol")
                        broken.Orderable <- Unchecked.defaultof<_>

                        match
                            { dto with
                                Scenarios = [| { dto.Scenarios[0] with Order = broken } |]
                            }
                            |> OrderContext.Dto.fromDto
                        with
                        | Error [ DtoError.OrderNotCreated _ ] -> ()
                        | other -> failtest $"expected one OrderNotCreated, got {other}"
                    }

                    test "null nested Dtos are missing, null arrays empty, never a crash" {
                        let dto = Fixtures.version |> OrderPlanVersion.Dto.toDto

                        { dto with
                            SignedBy = Unchecked.defaultof<_>
                            Plan = { dto.Plan with Totals = Unchecked.defaultof<_>; Filtered = null }
                        }
                        |> OrderPlanVersion.Dto.fromDto
                        |> Expect.equal "both named" (Error [ DtoError.Missing "SignedBy"; DtoError.Missing "Totals" ])

                        match { dto with Plan = { dto.Plan with Filtered = null; Contexts = null } } |> OrderPlanVersion.Dto.fromDto with
                        | Ok v ->
                            v.Plan.Filtered |> Expect.isEmpty "no filter"
                            v.Plan.Contexts |> Expect.isEmpty "no contexts"
                        | Error e -> failtest $"expected a version, got {e}"
                    }
                ]

            testList
                "the stored root"
                [
                    test "an order plan version reads back from its canonical form, in canonical form" {
                        let dto = Fixtures.version |> OrderPlanVersion.Dto.toDto
                        let s = dto |> Canonical.serialize

                        s
                        |> Canonical.deserialize<OrderPlanVersion.Dto.Dto>
                        |> Canonical.serialize
                        |> Expect.equal "the same form" s
                    }

                    test "a version read back parses to the version that was written" {
                        Fixtures.version
                        |> OrderPlanVersion.Dto.toDto
                        |> Canonical.serialize
                        |> Canonical.deserialize<OrderPlanVersion.Dto.Dto>
                        |> OrderPlanVersion.Dto.fromDto
                        |> Expect.equal "the same version" (Ok Fixtures.version)
                    }

                    test "two versions equal as values digest equal, a re-ordered plan does not" {
                        let a = Fixtures.version |> OrderPlanVersion.Dto.toDto |> Canonical.serialize

                        let b =
                            { Fixtures.version with
                                Plan =
                                    { Fixtures.plan with
                                        Contexts = [| Fixtures.feeding; Fixtures.planContext |]
                                    }
                            }
                            |> OrderPlanVersion.Dto.toDto
                            |> Canonical.serialize

                        Fixtures.version
                        |> OrderPlanVersion.Dto.toDto
                        |> Canonical.serialize
                        |> Expect.equal "equal" a

                        a |> Expect.notEqual "a different order plan" b
                    }
                ]
        ]


runTestsWithCLIArgs [] [| "--summary" |] tests
