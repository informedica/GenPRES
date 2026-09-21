namespace Informedica.GenOrder.Lib

// The order plan types' Dtos (ADR-0008, docs/adr/0008-contract-model-dto-mapping-boundary.md),
// after Api.fs since a plan context nests an order context. The order plan rules join them here.

open System
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib


module PlanContext =

    /// A context as it enters the plan: its id there, its category, the context, and no
    /// intake until an evaluation computes one.
    let create id category (ctx: OrderContext) : PlanContext =
        {
            Id = id
            Category = category
            Context = ctx
            Intake = Totals.empty
        }


    /// The nutrition category of a context, none for a drug.
    let nutritionCategory (pc: PlanContext) =
        match pc.Category with
        | OrderCategory.Nutrition category -> Some category
        | OrderCategory.Drug -> None


    /// The order the context contributes to the plan, if it is narrowed to one.
    let contribution (pc: PlanContext) = pc.Context |> OrderContext.contribution


    /// The plan context evaluated with the three steps passed in: its context reconciled,
    /// the command run over it, and its intake recorded over the answer. The id and the
    /// category stay the plan's. An evaluation that fails is the answer.
    let evaluateWith
        (reconcile: OrderContext -> OrderContext)
        (evaluate: OrderContext.Command -> Result<OrderContext.Command, 'e>)
        (intake: OrderContext -> Totals)
        (cmd: OrderContext -> OrderContext.Command)
        (pc: PlanContext)
        : Result<PlanContext, 'e>
        =
        pc.Context
        |> reconcile
        |> cmd
        |> evaluate
        |> Result.map (fun answer ->
            let ctx = answer |> OrderContext.Command.get

            { pc with
                Context = ctx
                Intake = ctx |> intake
            }
        )


    /// The plan context evaluated against the rules: reconciled, evaluated and its intake
    /// computed over the totals data.
    let evaluate
        (start: System.DateTime)
        logger
        provider
        (totalsData: Types.Data.TotalsData[])
        (cmd: OrderContext -> OrderContext.Command)
        (pc: PlanContext)
        =
        pc
        |> evaluateWith
            (OrderContext.reconcile logger provider)
            (OrderContext.evaluate start logger provider)
            (OrderContext.intake totalsData)
            cmd


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
            Nested.required
                "PlanContext"
                dto
                (fun dto ->
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
                )


module NutritionRuleSet =

    /// The set serving a category, if the composition root supplied one.
    let tryFind category (sets: NutritionRuleSet[]) = sets |> Array.tryFind (fun s -> s.Category = category)


    /// The context's pick lists narrowed to the set: when the set names indications or
    /// generics, only those are kept; an empty list in the set restricts nothing. The dose
    /// types and the selection pass through.
    let narrow (set: NutritionRuleSet) (ctx: OrderContext) : OrderContext =
        let keep (allowed: string[]) (xs: string[]) =
            if allowed |> Array.isEmpty then
                xs
            else
                xs |> Array.filter (fun x -> allowed |> Array.contains x)

        { ctx with
            Filter =
                { ctx.Filter with
                    Indications = ctx.Filter.Indications |> keep set.Indications
                    Generics = ctx.Filter.Generics |> keep set.Generics
                }
        }


    /// A nutrition workbench's pick lists discovered: the context evaluated as it is, and
    /// of the indications, generics and dose types the evaluation offers, only those the
    /// workbench already carried, so a category's rule set bounds what is offered. The
    /// evaluation is passed in, so the discovery is what it does with the answer.
    let discover (evaluate: OrderContext -> Result<OrderContext, 'e>) (ctx: OrderContext) =
        let offered (xs: 'a[]) (ys: 'a[]) = ys |> Array.filter (fun y -> xs |> Array.contains y)

        ctx
        |> evaluate
        |> Result.map (fun resolved ->
            { resolved with
                Filter =
                    { resolved.Filter with
                        Indications = resolved.Filter.Indications |> offered ctx.Filter.Indications
                        Generics = resolved.Filter.Generics |> offered ctx.Filter.Generics
                        DoseTypes = resolved.Filter.DoseTypes |> offered ctx.Filter.DoseTypes
                    }
            }
        )


module OrderPlan =

    /// The plan for a patient with the contexts given, nothing filtered and no totals: what
    /// a signed version opens on, its contexts as they were, nothing evaluated.
    let create (pat: Patient) (contexts: PlanContext[]) : OrderPlan =
        {
            Patient = pat
            Filtered = [||]
            Contexts = contexts
            Totals = Totals.empty
        }


    /// The nutrition contexts of the plan.
    let nutritionContexts (plan: OrderPlan) =
        plan.Contexts |> Array.filter (PlanContext.nutritionCategory >> Option.isSome)


    /// The orders the plan's contexts contribute: the one scenario of every context narrowed
    /// to one, in context order.
    let orders (plan: OrderPlan) = plan.Contexts |> Array.choose PlanContext.contribution


    /// The contexts the filter keeps: those named by id, all of them when it is empty.
    let filtered (plan: OrderPlan) =
        if plan.Filtered |> Array.isEmpty then
            plan.Contexts
        else
            plan.Contexts |> Array.filter (fun c -> plan.Filtered |> Array.contains c.Id)


    /// The plan with its totals recomputed over the orders of the contexts the filter keeps,
    /// all of them when it is empty, for its patient's age and weight.
    let recalculate (totalsData: Informedica.GenForm.Lib.Types.Data.TotalsData[]) (plan: OrderPlan) : OrderPlan =
        let wght = plan.Patient.Weight |> Option.map (ValueUnit.convertTo Units.Weight.kiloGram)

        { plan with
            Totals =
                plan
                |> filtered
                |> Array.choose PlanContext.contribution
                |> Array.map _.Order
                |> Totals.getTotals totalsData plan.Patient.Age wght
        }


    /// Whether the plan holds a context of the nutrition category.
    let holds category (plan: OrderPlan) =
        plan.Contexts
        |> Array.exists (fun c -> c.Category = OrderCategory.Nutrition category)


    /// Whether the plan may take a context of the category: one context per nutrition
    /// category, except supplements (any number, each under a feeding) and electrolyte and
    /// glucose lines (any number). A line's generic is chosen after the line is admitted, so
    /// nothing here compares generics; two lines on one generic are the prescriber's to avoid.
    let admits category (plan: OrderPlan) : Result<unit, OrderPlanError> =
        match category with
        | NutritionCategory.EnteralSupplement when plan |> holds NutritionCategory.EnteralFeeding |> not ->
            Error OrderPlanError.SupplementNeedsFeeding
        | NutritionCategory.EnteralSupplement
        | NutritionCategory.ElectrolyteGlucose -> Ok()
        | _ when plan |> holds category -> Error(OrderPlanError.CategoryHeld category)
        | _ -> Ok()


    /// The evaluated context into the context named, keeping the id and the category the
    /// plan gave it and, for a nutrition context, its pick lists narrowed to its category's
    /// rule set. The plan's orders follow, since they are what its contexts contribute.
    let updateContext
        (ruleSets: NutritionRuleSet[])
        id
        (evaluated: PlanContext)
        (plan: OrderPlan)
        : Result<OrderPlan, OrderPlanError>
        =
        match plan.Contexts |> Array.tryFind (fun c -> c.Id = id) with
        | None -> Error(OrderPlanError.NoSuchContext id)
        | Some held ->
            let ctx =
                match
                    held
                    |> PlanContext.nutritionCategory
                    |> Option.bind (fun c -> ruleSets |> NutritionRuleSet.tryFind c)
                with
                | Some set -> evaluated.Context |> NutritionRuleSet.narrow set
                | None -> evaluated.Context

            let updated =
                { evaluated with
                    Id = held.Id
                    Category = held.Category
                    Context = ctx
                }

            { plan with Contexts = plan.Contexts |> Array.map (fun c -> if c.Id = id then updated else c) }
            |> Ok


    /// The context removed, and every enteral supplement with a feeding: the plan holds one
    /// feeding at most and a supplement only under it, so the feeding's supplements are all of
    /// them. Each takes its order with it, and leaves the filter.
    let removeOrderContext id (plan: OrderPlan) =
        let cascade =
            plan.Contexts
            |> Array.exists (fun c ->
                c.Id = id
                && c.Category = OrderCategory.Nutrition NutritionCategory.EnteralFeeding
            )

        let goes (c: PlanContext) =
            c.Id = id
            || (cascade
                && c.Category = OrderCategory.Nutrition NutritionCategory.EnteralSupplement)

        let gone, kept = plan.Contexts |> Array.partition goes
        let goneIds = gone |> Array.map _.Id

        { plan with
            Contexts = kept
            Filtered = plan.Filtered |> Array.filter (fun f -> goneIds |> Array.contains f |> not)
        }


    /// The contexts named removed, every kind, each with its order; a feeding takes its
    /// supplements with it.
    let removeOrderContexts (ids: string[]) (plan: OrderPlan) =
        ids |> Array.fold (fun p id -> p |> removeOrderContext id) plan


    /// The prescribing workbench into the plan as a drug context with a minted id, its one
    /// scenario the order it contributes and its intake as evaluated. Refused when the
    /// workbench is not narrowed to one scenario, and when the plan already holds that order:
    /// the signing challenge would refuse the plan later, so it is said now.
    let addOrderContext (newId: unit -> string) (workbench: PlanContext) (plan: OrderPlan) =
        match workbench |> PlanContext.contribution with
        | None -> Error(OrderPlanError.NotNarrowed workbench.Context.Scenarios.Length)
        | Some sc when orders plan |> Array.exists (fun s -> s.Order.Id = sc.Order.Id) ->
            let (Id id) = sc.Order.Id
            Error(OrderPlanError.OrderHeld id)
        | Some _ ->
            let added =
                { workbench with
                    Id = newId ()
                    Category = OrderCategory.Drug
                }

            { plan with Contexts = Array.append plan.Contexts [| added |] } |> Ok


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
                Patient = plan.Patient |> Informedica.GenForm.Lib.Patient.Dto.toDto
                Filtered = plan.Filtered
                Contexts = plan.Contexts |> Array.map PlanContext.Dto.toDto
                Totals = plan.Totals |> Totals.Dto.toDto
            }


        /// The order plan a Dto is, or every reason it is none, over the patient and every
        /// context.
        let fromDto (dto: Dto) : Result<OrderPlan, DtoError list> =
            Nested.required
                "OrderPlan"
                dto
                (fun dto ->
                    let patient =
                        Nested.required
                            "Patient"
                            dto.Patient
                            (fun p ->
                                p
                                |> Informedica.GenForm.Lib.Patient.Dto.fromDto
                                |> Result.mapError (List.map DtoError.Patient)
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
                )


module Signer =

    module Dto =

        type Dto =
            {
                UserId: string
                DisplayName: string
            }


        let toDto (s: Signer) : Dto =
            {
                UserId = s.UserId
                DisplayName = s.DisplayName
            }


        let fromDto (dto: Dto) : Result<Signer, DtoError list> =
            Nested.required
                "Signer"
                dto
                (fun dto ->
                    Ok
                        {
                            UserId = dto.UserId |> DtoResult.orBlank
                            DisplayName = dto.DisplayName |> DtoResult.orBlank
                        }
                )


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
            Nested.required
                "OrderPlanVersion"
                dto
                (fun dto ->
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
                )
