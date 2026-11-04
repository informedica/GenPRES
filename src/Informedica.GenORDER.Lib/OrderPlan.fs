namespace Informedica.GenOrder.Lib

// The order plan types' Dtos (ADR-0008, docs/adr/0008-contract-model-dto-mapping-boundary.md),
// after Api.fs since a plan context nests an order context. The order plan rules join them here.

open System
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open FsToolkit.ErrorHandling


module PlanContext =

    /// A context as it enters the plan: its id there, its category, the context, no totals
    /// until an evaluation computes them, and no link.
    let create id category (ctx: OrderContext) : PlanContext =
        {
            Id = id
            Category = category
            Context = ctx
            Totals = Totals.empty
            LinkedTo = None
        }


    /// The context with the patient.
    let withPatient pat (pc: PlanContext) = { pc with Context = { pc.Context with Patient = pat } }


    /// The nutrition category of a context, none for a medication.
    let nutritionCategory (pc: PlanContext) =
        match pc.Category with
        | OrderCategory.Nutrition category -> Some category
        | OrderCategory.Medication -> None


    /// The order the context contributes to the plan, if it is narrowed to one.
    let contribution (pc: PlanContext) = pc.Context |> OrderContext.contribution


    /// The generic of the order of the context by its label, else the generic its filter picks, else the
    /// empty text: only a context that is not narrowed and picks no generic has neither, which the client
    /// never prescribes.
    let generic (pc: PlanContext) =
        pc
        |> contribution
        |> Option.map _.Name
        |> Option.orElse pc.Context.Filter.Generic
        |> Option.defaultValue ""


    /// A filter or scenario pick reads its index in the lists the user saw, and a seed is checked
    /// against the rules as an update of the context is, so each is applied to the context as sent,
    /// before the context is reconciled with the rules: the existing command
    /// it is evaluated as, over the context it changed. Any other command stays as it is.
    let resolveChange (cmd: OrderContext -> OrderContext.Command) (pc: PlanContext) =
        match pc.Context |> cmd |> OrderContext.resolveChange with
        | Some(Ok(changed, asCommand)) -> Ok(asCommand, { pc with Context = changed })
        | Some(Error e) -> Error [ ErrorMsg(e, None) ]
        | None -> Ok(cmd, pc)


    /// The plan context evaluated against the rules as an outcome. For the two commands that
    /// look the rules up, a pick the rules no longer offer for the patient is a refusal with
    /// the context as it was sent, not a pick dropped in silence; a reload refreshes the rules
    /// first, so that the pick is checked against them as they are now. Every other command
    /// runs over the context reconciled, and one over the scenario's order is refused with the
    /// context before the change when the order cannot be solved. The totals are recorded over the answer,
    /// evaluated or refused, from the totals data read after any reload; the id, the category and the
    /// link stay the plan's. An evaluation that fails is the answer.
    let evaluateSent
        (start: System.DateTime)
        logger
        provider
        (totalsData: unit -> Types.Data.TotalsData[])
        (cmd: OrderContext -> OrderContext.Command)
        (pc: PlanContext)
        : Result<Outcome<PlanContext>, Message list>
        =
        let sent = pc.Context

        let recorded answer =
            let ctx = answer |> OrderContext.Command.get

            { pc with
                Context = ctx
                Totals = ctx |> OrderContext.contextTotals (totalsData ())
            }

        let lookUp () =
            let reconciled = sent |> OrderContext.reconcile logger provider

            if Filter.dropped sent.Filter reconciled.Filter then
                Refused(pc, OrderContext.refusal provider sent) |> Ok
            else
                reconciled
                |> OrderContext.UpdateOrderContext
                |> OrderContext.evaluateOutcome start logger provider
                |> Result.map (Outcome.map recorded)

        match sent |> cmd with
        | OrderContext.UpdateOrderContext _ -> lookUp ()
        | OrderContext.ReloadResources _ ->
            Api.reloadCache logger provider
            lookUp ()
        | _ ->
            sent
            |> OrderContext.reconcile logger provider
            |> cmd
            |> OrderContext.evaluateOutcome start logger provider
            |> Result.map (Outcome.map recorded)


    /// The plan context evaluated against the rules as an outcome, a filter or scenario pick
    /// applied to the context as sent first.
    let evaluateOutcome start logger provider totalsData cmd pc =
        pc
        |> resolveChange cmd
        |> Result.bind (fun (cmd, pc) -> pc |> evaluateSent start logger provider totalsData cmd)


    /// The plan context evaluated against the rules, as the message-list contract answers it:
    /// the outcome, with a refusal as its message, so a pick the rules no longer offer is not
    /// dropped in silence when the context is updated.
    let evaluate start logger provider totalsData cmd pc =
        pc
        |> evaluateOutcome start logger provider (fun () -> totalsData) cmd
        |> Result.bind OrderContext.ofOutcome


    /// The serializable shape of a PlanContext: the category as a string, the context and
    /// the totals as their own Dtos, and the link as the id of the main order or null.
    module Dto =

        type Dto =
            {
                Id: string
                Category: string
                Context: OrderContext.Dto.Dto
                Totals: Totals.Dto.Dto
                LinkedTo: string option
            }


        let toDto (pc: PlanContext) : Dto =
            {
                Id = pc.Id
                Category = pc.Category |> OrderCategoryDto.toString
                Context = pc.Context |> OrderContext.Dto.toDto
                Totals = pc.Totals |> Totals.Dto.toDto
                LinkedTo = pc.LinkedTo
            }


        let fromDto (dto: Dto) : Result<PlanContext, DtoError list> =
            Nested.required
                "PlanContext"
                dto
                (fun dto ->
                    let category = dto.Category |> OrderCategoryDto.fromString |> Result.mapError List.singleton

                    let context = Nested.required "Context" dto.Context OrderContext.Dto.fromDto
                    let totals = Nested.required "Totals" dto.Totals Totals.Dto.fromDto

                    match category, context, totals with
                    | Ok category, Ok context, Ok totals ->
                        Ok
                            {
                                Id = dto.Id |> DtoResult.orBlank
                                Category = category
                                Context = context
                                Totals = totals
                                LinkedTo = dto.LinkedTo
                            }
                    | _ ->
                        let errorsOf r =
                            match r with
                            | Error es -> es
                            | Ok _ -> []

                        Error(errorsOf category @ errorsOf context @ errorsOf totals)
                )


module NutritionRuleSet =

    open Informedica.Utils.Lib.BCL


    /// The set serving a category, if the rule sets have one.
    let tryFind category (sets: NutritionRuleSet[]) = sets |> Array.tryFind (fun s -> s.Category = category)


    /// The rule set of a category, refused when the rule sets have none for it.
    let find category sets =
        sets
        |> tryFind category
        |> Result.requireSome (OrderPlanError.NoRuleSet category)


    /// Every nutrition category.
    let categories =
        [|
            NutritionCategory.EnteralFeeding
            NutritionCategory.EnteralSupplement
            NutritionCategory.TPN
            NutritionCategory.Lipid
            NutritionCategory.ElectrolyteGlucose
        |]


    /// The name of a category in the "RuleSets" sheet.
    let sheetName =
        function
        | NutritionCategory.EnteralFeeding -> "EnteralFeeding"
        | NutritionCategory.EnteralSupplement -> "EnteralSupplement"
        | NutritionCategory.TPN -> "TPN"
        | NutritionCategory.Lipid -> "Lipid"
        | NutritionCategory.ElectrolyteGlucose -> "ElectrolyteGlucose"


    /// The nutrition category a name in the "RuleSets" sheet gives, refused for a name the code
    /// does not know.
    let categoryOf name =
        categories
        |> Array.tryFind (sheetName >> (=) name)
        |> Result.requireSome (OrderPlanError.UnknownCategory name)


    /// The rule set of every category the rows name: the label from the first row of the category,
    /// and its indications and generics, each once, in the order of the rows. Refused, naming each
    /// one once, when a row names a category the code does not know.
    let ofData (rows: Types.Data.RuleSetData[]) =
        rows
        |> Array.groupBy _.Category
        |> Array.toList
        |> List.traverseResultA (fun (name, rs) ->
            let distinct f =
                rs |> Array.map f |> Array.filter String.notEmpty |> Array.distinct

            categoryOf name
            |> Result.map (fun category ->
                {
                    Category = category
                    Label = rs[0].Label
                    Indications = distinct _.Indication
                    Generics = distinct _.Generic
                }
            )
        )
        |> Result.map List.toArray


    /// Whether a list of a rule set allows the name: an empty list allows any name, and names compare
    /// without regard to case.
    let allows (allowed: string[]) name =
        allowed |> Array.isEmpty
        || allowed |> Array.exists (String.equalsCapInsens name)


    /// The category of an order by its indication and generic label: the nutrition category whose rule
    /// set names the indication and allows the generic; the medication category when no rule set does,
    /// and when the indication or the generic is absent.
    let orderCategory (sets: NutritionRuleSet[]) indication generic =
        match indication, generic with
        | Some ind, Some gen ->
            sets
            |> Array.tryFind (fun s ->
                s.Indications |> Array.exists (String.equalsCapInsens ind)
                && allows s.Generics gen
            )
            |> Option.map (_.Category >> OrderCategory.Nutrition)
            |> Option.defaultValue OrderCategory.Medication
        | _ -> OrderCategory.Medication


    /// The context's pick lists narrowed to the set: only the indications and generics the set
    /// allows are kept. The dose types and the selection pass through.
    let narrow (set: NutritionRuleSet) (ctx: OrderContext) : OrderContext =
        let keep allowed (xs: string[]) = xs |> Array.filter (allows allowed)

        { ctx with
            Filter =
                { ctx.Filter with
                    Indications = ctx.Filter.Indications |> keep set.Indications
                    Generics = ctx.Filter.Generics |> keep set.Generics
                }
        }


    /// An answer narrowed, and evaluated again while that leaves a choice open: the rule
    /// lookup reads a list of one as its choice, so the next evaluation makes it. It stops when
    /// an evaluation changes nothing the narrowing keeps, and after five rounds, one for each
    /// field a choice can stay open in.
    let settle (contextOf: 'a -> OrderContext) (narrow: 'a -> 'a) (evaluate: 'a -> Result<'a, 'e>) (answer: 'a) =
        let rec go rounds a =
            let narrowed = narrow a

            if rounds > 0 && (contextOf narrowed |> OrderContext.choicesOpen) then
                narrowed
                |> evaluate
                |> Result.bind (fun e ->
                    if (contextOf (narrow e)).Filter = (contextOf narrowed).Filter then
                        Ok(narrow e)
                    else
                        go (rounds - 1) e
                )
            else
                Ok narrowed

        go 5 answer


    /// An order context of the rule set before anything is chosen: the patient, the indications and
    /// generics of the rule set, nothing else offered and nothing selected.
    let emptyContext (set: NutritionRuleSet option) pat : OrderContext =
        {
            Filter =
                { OrderContext.emptyFilter with
                    Indications = set |> Option.map _.Indications |> Option.defaultValue [||]
                    Generics = set |> Option.map _.Generics |> Option.defaultValue [||]
                }
            Patient = pat
            Scenarios = [||]
            Argumentation = None
        }


    /// The pick lists of a nutrition context discovered: the context evaluated as it is, and
    /// of the indications, generics and dose types the evaluation offers, only those the
    /// context already carried, so a category's rule set bounds what is offered; settled, so
    /// a list that leaves one option has it chosen. The evaluation is passed in, so the
    /// discovery is what it does with the answer.
    let discover (evaluate: OrderContext -> Result<OrderContext, 'e>) (ctx: OrderContext) =
        let offered (xs: 'a[]) (ys: 'a[]) = ys |> Array.filter (fun y -> xs |> Array.contains y)

        let narrowed (resolved: OrderContext) =
            { resolved with
                Filter =
                    { resolved.Filter with
                        Indications = resolved.Filter.Indications |> offered ctx.Filter.Indications
                        Generics = resolved.Filter.Generics |> offered ctx.Filter.Generics
                        DoseTypes = resolved.Filter.DoseTypes |> offered ctx.Filter.DoseTypes
                    }
            }

        ctx |> evaluate |> Result.bind (settle id narrowed evaluate)


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
    /// all of them when it is empty, and the totals of every context, for its patient's age and
    /// weight. A context can hold an older patient, so its totals are not taken from there.
    let recalculate (totalsData: Informedica.GenForm.Lib.Types.Data.TotalsData[]) (plan: OrderPlan) : OrderPlan =
        { plan with
            Contexts =
                plan.Contexts
                |> Array.map (fun pc ->
                    { pc with Totals = pc.Context |> OrderContext.contextTotalsFor totalsData plan.Patient }
                )
            Totals =
                plan
                |> filtered
                |> Array.choose PlanContext.contribution
                |> Array.map _.Order
                |> OrderContext.totalsFor totalsData plan.Patient
        }


    /// Every order of the plan calculated on the patient of the plan. A context with an order keeps
    /// its patient. A context without an order goes through restart, which evaluates a nutrition
    /// context that is not narrowed again; when that fails it takes the patient and keeps its lists, so
    /// that it cannot block a change of data. An order that cannot be calculated refuses the update, so that a
    /// plan is never partly on new data. This guards a case not seen: the amounts stay, and only the
    /// values per kg are solved.
    let followPatientData logger provider restart (plan: OrderPlan) =
        let follow (pc: PlanContext) =
            match pc |> PlanContext.contribution with
            | None ->
                pc
                |> restart plan.Patient
                |> Result.defaultWith (fun e ->
                    $"context %s{pc.Id} keeps its lists, it could not start again: %A{e}"
                    |> Events.OrderScenario
                    |> Informedica.GenOrder.Lib.Logging.logWarning logger

                    pc |> PlanContext.withPatient plan.Patient
                )
            | Some sc ->
                match pc.Context |> OrderContext.orderOnPatientData logger provider plan.Patient sc with
                // a refused order stands, marked, and the rest of the plan follows: the prescriber decides
                | Evaluated ctx
                | Refused(ctx, _) -> { pc with Context = ctx }

        { plan with Contexts = plan.Contexts |> Array.map follow }


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


    /// Why the plan refuses the link of the context, none when the context is not linked or the
    /// link holds: the main order is in the plan and is not linked itself. A context linked to
    /// itself is refused as linked to a linked order.
    let linkError (plan: OrderPlan) (pc: PlanContext) =
        pc.LinkedTo
        |> Option.bind (fun id ->
            match plan.Contexts |> Array.tryFind (fun c -> c.Id = id) with
            | None -> Some(LinkError.NoSuchMainOrder id)
            | Some main when main.LinkedTo.IsSome -> Some(LinkError.MainOrderLinked id)
            | Some _ -> None
        )


    /// Why the link rules refuse the link of the plan context, none when they allow it. A linked
    /// context needs a rule that links it to its main order, and the main order counts only when it is
    /// narrowed to one order, as for the offer. A context that is not linked is refused when a rule that
    /// matches it requires a main order. A main order that is not in the plan is left to linkError, and
    /// the add refuses a context that is not narrowed before it checks a link.
    let linkRuleError routeMapping (rules: LinkRule[]) (plan: OrderPlan) (pc: PlanContext) =
        let matches = OrderContext.matchesLinkSide routeMapping

        let rulesForLinkedOrder = rules |> Array.filter (fun r -> pc.Context |> matches r.Linked)

        let allowedUnder (main: PlanContext) =
            main.Context
            |> OrderContext.rulesForMainOrder routeMapping rules
            |> Array.exists (fun r -> pc.Context |> matches r.Linked)

        match pc.LinkedTo with
        | Some id ->
            plan.Contexts
            |> Array.tryFind (fun c -> c.Id = id)
            |> Option.filter (allowedUnder >> not)
            |> Option.map (PlanContext.generic >> LinkError.NotAllowed)
        | None when
            rulesForLinkedOrder
            |> Array.exists (fun r -> r.Requirement = LinkRequirement.Required)
            ->
            Some LinkError.MainOrderRequired
        | None -> None


    /// The context removed with every context linked to it, each with its order, and gone from
    /// the filter. A feeding that goes, named or linked to the context named, also takes every
    /// supplement without a link: a supplement added without a main order belongs to the feeding of
    /// the plan, until step 10 of plan 1612 links every supplement.
    let removeOrderContext id (plan: OrderPlan) =
        let direct (c: PlanContext) = c.Id = id || c.LinkedTo = Some id

        let feeding =
            plan.Contexts
            |> Array.exists (fun c ->
                direct c
                && c.Category = OrderCategory.Nutrition NutritionCategory.EnteralFeeding
            )

        let goes (c: PlanContext) =
            direct c
            || (feeding
                && c.LinkedTo.IsNone
                && c.Category = OrderCategory.Nutrition NutritionCategory.EnteralSupplement)

        let gone, kept = plan.Contexts |> Array.partition goes
        let goneIds = gone |> Array.map _.Id

        { plan with
            Contexts = kept
            Filtered = plan.Filtered |> Array.filter (fun f -> goneIds |> Array.contains f |> not)
        }


    /// The contexts named removed, each with the contexts linked to it.
    let removeOrderContexts (ids: string[]) (plan: OrderPlan) =
        ids |> Array.fold (fun p id -> p |> removeOrderContext id) plan


    /// An order context into the plan with a new id, its category and its link as sent, its
    /// one scenario the order it contributes and its totals as evaluated. Refused when the
    /// context is not narrowed to one scenario, when the plan already holds that order (the
    /// signing challenge would refuse the plan later, so it is said now), and when the plan does
    /// not admit its nutrition category. The shape of the link is checked wherever a plan is read.
    let addOrderContext (newId: unit -> string) (context: PlanContext) (plan: OrderPlan) =
        let admitted () =
            match context |> PlanContext.nutritionCategory with
            | Some category -> plan |> admits category
            | None -> Ok()

        match context |> PlanContext.contribution with
        | None -> Error(OrderPlanError.NotNarrowed context.Context.Scenarios.Length)
        | Some sc when orders plan |> Array.exists (fun s -> s.Order.Id = sc.Order.Id) ->
            let (Id id) = sc.Order.Id
            Error(OrderPlanError.OrderHeld id)
        | Some _ ->
            admitted ()
            |> Result.map (fun () ->
                let added = { context with Id = newId () }
                { plan with Contexts = Array.append plan.Contexts [| added |] }
            )


    /// The main order and its linked orders into the plan, each with a new id; when one of them is refused,
    /// none of them enters the plan. The main order is not linked, and each linked order is linked to the
    /// main order, whatever links the contexts were sent with. An order is refused for the reasons a single
    /// add refuses it, and when the link rules refuse its link; the refusal names the order by its generic.
    let addOrderContexts newId routeMapping rules (main: PlanContext) linked plan =
        // the link rules read the link and the order of a context, not its id, so the context is checked
        // as sent, in the plan it entered
        let add id (pc: PlanContext) plan =
            plan
            |> addOrderContext id pc
            |> Result.bind (fun added ->
                match pc |> linkRuleError routeMapping rules added with
                | Some e -> Error(OrderPlanError.BrokenLink e)
                | None -> Ok added
            )
            |> Result.mapError (fun e -> OrderPlanError.OrderRefused(PlanContext.generic pc, e))

        let mainId = newId ()

        linked
        |> Array.fold
            (fun acc pc -> acc |> Result.bind (add newId { pc with LinkedTo = Some mainId }))
            (plan |> add (fun () -> mainId) { main with LinkedTo = None })


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
        /// context, then every context whose link the plan refuses. The links are checked over
        /// the plan as read, so the order of the contexts does not matter.
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
                        let plan: OrderPlan =
                            {
                                Patient = patient
                                Filtered = dto.Filtered |> DtoResult.orEmpty
                                Contexts = contexts |> List.toArray
                                Totals = totals
                            }

                        match
                            plan.Contexts
                            |> Array.choose (fun pc ->
                                pc |> linkError plan |> Option.map (fun e -> DtoError.BrokenLink(pc.Id, e))
                            )
                            |> Array.toList
                        with
                        | [] -> Ok plan
                        | es -> Error es
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
                EhrData: EhrPatientData.Dto.Dto option
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
                EhrData = v.EhrData |> Option.map EhrPatientData.Dto.toDto
            }


        let fromDto (dto: Dto) : Result<OrderPlanVersion, DtoError list> =
            Nested.required
                "OrderPlanVersion"
                dto
                (fun dto ->
                    let signer = Nested.required "SignedBy" dto.SignedBy Signer.Dto.fromDto
                    let plan = Nested.required "Plan" dto.Plan OrderPlan.Dto.fromDto

                    let ehr =
                        dto.EhrData
                        |> Option.traverseResult EhrPatientData.Dto.fromDto
                        |> Result.mapError (List.map DtoError.EhrData)

                    match signer, plan, ehr with
                    | Ok signer, Ok plan, Ok ehr ->
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
                                EhrData = ehr
                            }
                    | _ ->
                        let errorsOf r =
                            match r with
                            | Error es -> es
                            | Ok _ -> []

                        Error(errorsOf signer @ errorsOf plan @ errorsOf ehr)
                )
