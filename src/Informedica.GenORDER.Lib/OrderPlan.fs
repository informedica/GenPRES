namespace Informedica.GenOrder.Lib

// The plan context, the nutrition rule sets and the order plan, with their Dtos. This file compiles after
// OrderContext.fs because a plan context holds an order context.

open System
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open FsToolkit.ErrorHandling


module PlanContext =

    /// Creates a plan context from an id, a category and an order context, with empty totals and no link.
    let create id category (ctx: OrderContext) : PlanContext =
        {
            Id = id
            Category = category
            Context = ctx
            Totals = Totals.empty
            LinkedTo = None
        }


    /// Sets the patient data of the order context of a plan context.
    let withPatientData patientData (pc: PlanContext) =
        { pc with Context = { pc.Context with PatientData = patientData } }


    /// Returns the nutrition category of a plan context, or None for a medication.
    let nutritionCategory (pc: PlanContext) =
        match pc.Category with
        | OrderCategory.Nutrition category -> Some category
        | OrderCategory.Medication -> None


    /// Returns the order scenario that the context contributes to the plan, when it has exactly one scenario.
    let contribution (pc: PlanContext) = pc.Context |> OrderContext.contribution


    /// Returns the generic of the order of the context, else the generic picked in the filter, else an
    /// empty string.
    let generic (pc: PlanContext) =
        pc
        |> contribution
        |> Option.map _.Name
        |> Option.orElse pc.Context.Filter.Generic
        |> Option.defaultValue ""


    /// Applies a filter pick, a scenario pick or a seed to the context as sent, before the context is
    /// reconciled with the rules, because a pick reads an index in the lists as sent. Returns the existing
    /// command to evaluate with the changed plan context, or an error when the index is out of range.
    /// Any other command is returned unchanged.
    let resolveChange (cmd: OrderContext -> OrderContext.Command) (pc: PlanContext) =
        match pc.Context |> cmd |> OrderContext.resolveChange with
        | Some(Ok(changed, asCommand)) -> Ok(asCommand, { pc with Context = changed })
        | Some(Error e) -> Error [ ErrorMsg(e, None) ]
        | None -> Ok(cmd, pc)


    /// Evaluates the plan context against the rules and returns the outcome. An update of the order context
    /// looks the rules up again, and a reload does so after reloading the resources. A pick that the rules no
    /// longer offer for the patient gives a refusal with the context as sent, so the pick is not lost without
    /// notice. A refusal of the lookup, except one for a missing diluent, has each pick list narrowed by the
    /// other picks. After a successful lookup, restore sets the component quantities of the sent context
    /// again. Any other command runs on the reconciled context. The totals are calculated after the
    /// evaluation, except for the refusal of a pick that the rules no longer offer. The id, the category and
    /// the link do not change. Returns an error when the evaluation fails.
    let evaluateSent
        (start: System.DateTime)
        logger
        provider
        (totalsData: unit -> Types.Data.TotalsData[])
        restore
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

        // A refusal for a missing diluent keeps the pick lists of the lookup.
        let offerListsOfOtherPicks outcome =
            match outcome with
            | Refused(answer, refusal) when refusal <> Refusal.NoDiluent ->
                let ctx =
                    answer
                    |> OrderContext.Command.get
                    |> OrderContext.withListsOfOtherPicks logger provider
                Refused(OrderContext.UpdateOrderContext ctx, refusal)
            | _ -> outcome

        let lookUp () =
            let reconciled = sent |> OrderContext.reconcile logger provider

            if Filter.dropped sent.Filter reconciled.Filter then
                let ctx = sent |> OrderContext.withListsOfOtherPicks logger provider
                Refused({ pc with Context = ctx }, OrderContext.refusal provider sent) |> Ok
            else
                reconciled
                |> OrderContext.UpdateOrderContext
                |> OrderContext.evaluateOutcome start logger provider
                |> Result.map (
                    offerListsOfOtherPicks
                    >> Outcome.mapEvaluated (OrderContext.Command.get >> restore >> OrderContext.UpdateOrderContext)
                    >> Outcome.map recorded
                )

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


    /// Evaluates the plan context against the rules and returns the outcome. A filter or scenario pick is
    /// first applied to the context as sent.
    let evaluateOutcome start logger provider totalsData cmd pc =
        let restore = pc.Context |> cmd |> OrderContext.restoreQuantities logger

        pc
        |> resolveChange cmd
        |> Result.bind (fun (cmd, pc) -> pc |> evaluateSent start logger provider totalsData restore cmd)


    /// Evaluates the plan context against the rules and returns a refusal as an error message, so a pick
    /// that the rules no longer offer is not lost without notice.
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


        let fromDto today (dto: Dto) : Result<PlanContext, DtoError list> =
            Nested.required
                "PlanContext"
                dto
                (fun dto ->
                    let category = dto.Category |> OrderCategoryDto.fromString |> Result.mapError List.singleton

                    let context = Nested.required "Context" dto.Context (OrderContext.Dto.fromDto today)
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


    /// Returns the rule set of a nutrition category, or the error NoRuleSet when there is none.
    let find category sets =
        sets
        |> RuleSet.tryFind category
        |> Result.requireSome (OrderPlanError.NoRuleSet category)


    /// Returns the nutrition category whose rule set names the indication and allows the generic, otherwise
    /// Medication, also when the indication or the generic is missing.
    let orderCategory (sets: RuleSet[]) indication generic =
        match indication, generic with
        | Some ind, Some gen ->
            sets
            |> Array.tryPick (fun s ->
                match s.Kind with
                | RuleSetKind.Nutrition category ->
                    if
                        s.Rows
                        |> RuleSet.distinctOf _.Indication
                        |> Array.exists (String.equalsCapInsens ind)
                        && RuleSet.allows (s.Rows |> RuleSet.distinctOf _.Generic) gen
                    then
                        Some(OrderCategory.Nutrition category)
                    else
                        None
                | _ -> None
            )
            |> Option.defaultValue OrderCategory.Medication
        | _ -> OrderCategory.Medication


    /// Narrows the indication and generic lists of the context filter to the names the rule set allows.
    /// The other lists and the picks do not change.
    let narrow (set: RuleSet) (ctx: OrderContext) : OrderContext =
        let keep allowed (xs: string[]) = xs |> Array.filter (RuleSet.allows allowed)

        { ctx with
            Filter =
                { ctx.Filter with
                    Indications = ctx.Filter.Indications |> keep (set.Rows |> RuleSet.distinctOf _.Indication)
                    Generics = ctx.Filter.Generics |> keep (set.Rows |> RuleSet.distinctOf _.Generic)
                }
        }


    /// Narrows the answer and evaluates it again while a pick list with one entry has no pick, at most five
    /// times, once for each list that can be left open. Stops when an evaluation does not change the
    /// narrowed filter. Returns the error of a failed evaluation.
    let settle (contextOf: 'a -> OrderContext) (narrow: 'a -> 'a) (evaluate: 'a -> Result<'a, 'e>) (answer: 'a) =
        // An evaluation rebuilds the order and loses the component quantities restored after a component
        // selection or a diluent choice. Such a change starts from a settled context, where every list with
        // one entry has its pick, so no evaluation follows it.
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


module OrderPlan =

    module CorePatientData = Informedica.GenCore.Lib.Patients.PatientData

    /// Creates a plan for the patient data with the contexts, an empty filter and empty totals. The contexts
    /// are not evaluated.
    let create (patientData: PatientData) (contexts: PlanContext[]) : OrderPlan =
        {
            PatientData = patientData
            Filtered = [||]
            Contexts = contexts
            Totals = Totals.empty
        }


    /// Returns the order scenario of every context with exactly one scenario, in context order.
    let orders (plan: OrderPlan) = plan.Contexts |> Array.choose PlanContext.contribution


    /// Returns the contexts whose id is in the plan filter, or all contexts when the filter is empty.
    let filtered (plan: OrderPlan) =
        if plan.Filtered |> Array.isEmpty then
            plan.Contexts
        else
            plan.Contexts |> Array.filter (fun c -> plan.Filtered |> Array.contains c.Id)


    /// Calculates the totals of the orders of the contexts with exactly one scenario, for the patient data.
    /// A context without exactly one scenario adds nothing.
    let totalsOver totalsData patientData contexts =
        contexts
        |> Array.choose PlanContext.contribution
        |> Array.map _.Order
        |> OrderContext.totalsFor totalsData patientData


    /// Calculates the totals of every context and of the plan again for the patient data of the plan. The
    /// plan totals cover the filtered contexts. The patient data of a context can be older than that of the
    /// plan, so it is not used.
    let recalculate (totalsData: Informedica.GenForm.Lib.Types.Data.TotalsData[]) (plan: OrderPlan) : OrderPlan =
        { plan with
            Contexts =
                plan.Contexts
                |> Array.map (fun pc ->
                    { pc with Totals = pc.Context |> OrderContext.contextTotalsFor totalsData plan.PatientData }
                )
            Totals = plan |> filtered |> totalsOver totalsData plan.PatientData
        }


    /// Calculates every order of the plan again on the patient data of the plan. An order keeps its amounts;
    /// only its values per kg are solved again. An order that cannot be calculated stays as it was, marked
    /// with a notice. A context without an order only gets the patient data.
    let followPatientData logger provider (plan: OrderPlan) =
        let follow (pc: PlanContext) =
            match pc |> PlanContext.contribution with
            | None -> pc |> PlanContext.withPatientData plan.PatientData
            | Some sc ->
                match
                    pc.Context
                    |> OrderContext.orderOnPatientData logger provider plan.PatientData sc
                with
                // A refused order stays as it was, marked, and the other orders still get the patient data update;
                // the prescriber decides.
                | Evaluated ctx
                | Refused(ctx, _) -> { pc with Context = ctx }

        { plan with Contexts = plan.Contexts |> Array.map follow }


    /// Replaces the plan context with this id by the evaluated context, with the id and the category of the
    /// plan context. A nutrition context gets its pick lists narrowed to the rule set of its category.
    /// Returns NoSuchContext for an unknown id, and NotNarrowed when the evaluated context does not have
    /// exactly one scenario, because a context in the plan always has an order.
    let updateContext
        (ruleSets: RuleSet[])
        id
        (evaluated: PlanContext)
        (plan: OrderPlan)
        : Result<OrderPlan, OrderPlanError>
        =
        match plan.Contexts |> Array.tryFind (fun c -> c.Id = id), evaluated |> PlanContext.contribution with
        | None, _ -> Error(OrderPlanError.NoSuchContext id)
        | Some _, None -> Error(OrderPlanError.NotNarrowed evaluated.Context.Scenarios.Length)
        | Some held, Some _ ->
            let ctx =
                match
                    held
                    |> PlanContext.nutritionCategory
                    |> Option.bind (fun c -> ruleSets |> RuleSet.tryFind c)
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


    /// Returns NoSuchMainOrder when the main order of a linked context is not in the plan, and
    /// MainOrderLinked when the main order is linked itself, which includes a context linked to itself.
    /// Returns None for a context without a link or with a valid link.
    let linkError (plan: OrderPlan) (pc: PlanContext) =
        pc.LinkedTo
        |> Option.bind (fun id ->
            match plan.Contexts |> Array.tryFind (fun c -> c.Id = id) with
            | None -> Some(LinkError.NoSuchMainOrder id)
            | Some main when main.LinkedTo.IsSome -> Some(LinkError.MainOrderLinked id)
            | Some _ -> None
        )


    /// Returns the link rule error of a plan context, or None when the link rules allow it. A linked context
    /// gives NotAllowed when no rule of its main order links it; a main order counts only with exactly one
    /// scenario. A context without a link gives MainOrderRequired when a rule that matches it requires a main
    /// order. A main order that is not in the plan is checked by linkError.
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


    /// Removes the context with this id and every context linked to it from the plan and from the filter.
    let removeOrderContext id (plan: OrderPlan) =
        let goes (c: PlanContext) = c.Id = id || c.LinkedTo = Some id

        let gone, kept = plan.Contexts |> Array.partition goes
        let goneIds = gone |> Array.map _.Id

        { plan with
            Contexts = kept
            Filtered = plan.Filtered |> Array.filter (fun f -> goneIds |> Array.contains f |> not)
        }


    /// Removes the contexts with these ids, each with the contexts linked to it.
    let removeOrderContexts (ids: string[]) (plan: OrderPlan) =
        ids |> Array.fold (fun p id -> p |> removeOrderContext id) plan


    /// Adds a plan context to the plan with a new id. Returns NotNarrowed when the context does not have
    /// exactly one scenario, and OrderHeld when the plan already has that order, because a plan with an
    /// order twice is refused at signing. The link is not checked here.
    let addOrderContext (newId: unit -> string) (context: PlanContext) (plan: OrderPlan) =
        match context |> PlanContext.contribution with
        | None -> Error(OrderPlanError.NotNarrowed context.Context.Scenarios.Length)
        | Some sc when orders plan |> Array.exists (fun s -> s.Order.Id = sc.Order.Id) ->
            let (Id id) = sc.Order.Id
            Error(OrderPlanError.OrderHeld id)
        | Some _ ->
            let added = { context with Id = newId () }
            Ok { plan with Contexts = Array.append plan.Contexts [| added |] }


    /// Adds a main order and its linked orders to the plan, each with a new id, or none of them when one is
    /// refused. The main order gets no link and every linked order gets a link to the main order. An order
    /// is refused for the errors of addOrderContext or a link rule error, as OrderRefused with its generic.
    let addOrderContexts newId routeMapping rules (main: PlanContext) linked plan =
        // The link rules read the link and the order of a context, not its id, so the context as sent is
        // checked against the plan it was added to.
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


    /// The serializable shape of an OrderPlan: the patient data, the filtered ids, every context and the
    /// totals.
    module Dto =

        type Dto =
            {
                PatientData: CorePatientData.Dto.Dto
                Filtered: string[]
                Contexts: PlanContext.Dto.Dto[]
                Totals: Totals.Dto.Dto
            }


        let toDto (plan: OrderPlan) : Dto =
            {
                PatientData = plan.PatientData |> CorePatientData.Dto.toDto
                Filtered = plan.Filtered
                Contexts = plan.Contexts |> Array.map PlanContext.Dto.toDto
                Totals = plan.Totals |> Totals.Dto.toDto
            }


        /// Reads an order plan from a Dto. Returns the errors of the patient data, the contexts and the
        /// totals; when these read, returns a BrokenLink error for every context with a link error. The links
        /// are checked on the whole plan, so the order of the contexts does not matter.
        let fromDto today (dto: Dto) : Result<OrderPlan, DtoError list> =
            Nested.required
                "OrderPlan"
                dto
                (fun dto ->
                    let patient = Nested.required "PatientData" dto.PatientData (Patient.fromDto today)

                    let contexts =
                        dto.Contexts
                        |> DtoResult.orEmpty
                        |> Array.toList
                        |> List.map (fun pc -> Nested.required "Context" pc (PlanContext.Dto.fromDto today))
                        |> DtoResult.sequence
                        |> Result.mapError List.concat

                    let totals = Nested.required "Totals" dto.Totals Totals.Dto.fromDto

                    match patient, contexts, totals with
                    | Ok patient, Ok contexts, Ok totals ->
                        let plan: OrderPlan =
                            {
                                PatientData = patient
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
