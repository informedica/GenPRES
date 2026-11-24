namespace Informedica.GenOrder.Lib

module FilterHelpers =

    open Informedica.Logging.Lib
    open Informedica.GenForm.Lib


    /// Returns the value of an Ok result. Logs the GenForm messages of an Error and returns the fallback.
    let orLogErrors logger fallback =
        function
        | Ok x -> x
        | Error errs ->
            errs |> Events.GenFormErrors |> OrderLogging.logOrderEventError logger
            fallback


    let getPrescriptionRules logger provider = Api.getPrescriptionRules provider >> orLogErrors logger [||]


    /// Returns the prescription rules for the filter that can be prescribed for the patient data of the
    /// filter. Logs a warning when no rule is left.
    let getPrescribableRules logger provider filter =
        Api.filterPrescriptionRules provider filter
        |> Result.map (fun prs ->
            let prs = prs |> Array.filter (PrescriptionRule.isPrescribable filter.PatientData)

            if prs |> Array.isEmpty then
                filter |> Api.noRulesWarning |> Logging.logWarning logger

            prs
        )


    /// Returns the pick lists of the prescribable rules for the dose filter. Logs the error and returns
    /// empty pick lists when the rule lookup fails.
    let optionsFor logger provider doseFilter =
        doseFilter
        |> getPrescribableRules logger provider
        |> Result.map PrescriptionRule.filterOptions
        |> orLogErrors logger PrescriptionRule.emptyFilterOptions


module OrderScenario =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib


    let replace (tb: TextBlock) =
        match tb with
        | Valid s
        | Caution s
        | Warning s
        | Alert s ->
            if s |> String.isNullOrWhiteSpace then
                tb
            else
                let s =
                    s
                    |> String.replace "[" ""
                    |> String.replace "]" ""
                    |> String.replace "<" ""
                    |> String.replace ">" ""

                match tb with
                | Valid _ -> s |> Valid
                | Caution _ -> s |> Caution
                | Warning _ -> s |> Warning
                | Alert _ -> s |> Alert


    let create no nm ind frm rte dst dil cmp itm dils cmps itms ord adj ren rrl fits ids : OrderScenario =
        {
            No = no
            Name = nm
            Indication = ind
            Form = frm
            Route = rte
            DoseType = dst
            Diluent = dil
            Component = cmp
            Item = itm
            Diluents = dils
            Components = cmps
            Items = itms
            Prescription = [||]
            Preparation = [||]
            Administration = [||]
            Order = ord
            UseAdjust = adj
            UseRenalRule = ren
            RenalRule = rrl
            FitsOnlyWithoutDivisible = fits
            RulesNoLongerApply = false
            NotRecalculated = false
            ProductsIds = ids
            Access = AnyAccess
        }


    /// One access device per medication that Medication.fromRule makes from the rule, in the same
    /// order: the device of each solution rule, or a single AnyAccess when the rule has none.
    let accessOfRule pr =
        if pr.SolutionRules |> Array.isEmpty then
            [| AnyAccess |]
        else
            pr.SolutionRules |> Array.map _.PatientCategory.Access


    /// The warning on an order with a Divisible but no limit, and on an order that fits its rule
    /// only without the Divisible.
    let notPreparableWarning =
        "Dosis niet te bereiden met de deelbaarheid van het product: "
        + "controle door de apotheek of een tweede validatie is nodig"


    /// The notice on an order when the rules for the current patient data differ from the rules for
    /// the patient data of its context.
    let rulesNotice = "De regels gelden niet meer voor deze order: beoordeel de order of maak een nieuwe"


    /// The notice on an order that could not be calculated on the current patient data.
    let notRecalculatedNotice =
        "Deze order is niet berekend op de huidige patiëntgegevens. Beoordeel de order en verwijder deze als die niet past."


    /// The note on an order of a product without a Divisible.
    let noPreparationNote = "Geen bereiding"


    /// Whether the orderable quantity of the main component has a defined increment, which the
    /// Divisible gives.
    let hasStep ord =
        ord
        |> Order.mainComponentQuantity
        |> Option.exists OrderVariable.hasIncrConstraint


    /// Whether the orderable quantity of the main component has a maximum or a single value.
    let isBounded ord =
        ord |> Order.mainComponentQuantity |> Option.exists OrderVariable.hasMaxValue


    /// Whether the order has a quantity that can be made: a step, and a limit to count up to. Only
    /// the main component counts, because a diluent has no maximum when it fills up to a volume.
    let isPreparable ord = ord |> hasStep && ord |> isBounded


    /// Returns the notice on why an order has no preparation. An order with a Divisible but no maximum,
    /// or an order that fits its rule only without the Divisible, gets a warning. An order without a
    /// Divisible gets a caution. A preparable order that fits its rule gets no notice.
    let preparationNotice fitsOnlyWithoutDivisible ord =
        match fitsOnlyWithoutDivisible, ord |> hasStep, ord |> isBounded with
        | true, _, _
        | _, true, false -> notPreparableWarning |> Warning |> Some
        | _, true, true -> None
        | _, false, _ -> noPreparationNote |> Caution |> Some


    let setOrderTableFormat (sc: OrderScenario) =
        let prs, prp, adm = sc.Order |> Order.Print.printOrderToTableFormat sc.UseAdjust true sc.Items

        { sc with
            Prescription =
                [|
                    if sc.NotRecalculated then
                        notRecalculatedNotice |> Warning

                    if sc.RulesNoLongerApply then
                        rulesNotice |> Warning

                    yield! sc.Order |> preparationNotice sc.FitsOnlyWithoutDivisible |> Option.toArray
                |]
                |> Array.map Array.singleton
                |> Array.append (prs |> Array.map (Array.map replace))
            Preparation =
                if sc.Order |> isPreparable then
                    prp |> Array.map (Array.map replace)
                else
                    [||]
            Administration = adm |> Array.map (Array.map replace)
        }


    /// The scenario of an order made from a prescription rule. Its Components are the optional
    /// components of the rule, see PrescriptionRule.optionalComponents.
    let fromRule isAdditive fitsOnlyWithoutDivisible no pr ord =
        let cmpNames = pr.DoseRule.ComponentLimits |> Array.map _.Name
        let optionalCmps = pr.DoseRule.ComponentLimits |> PrescriptionRule.optionalComponents isAdditive

        let itms =
            pr.DoseRule.ComponentLimits
            |> Array.collect _.SubstanceLimits
            |> Array.choose (fun dl ->
                match dl.DoseLimitTarget with
                | SubstanceLimitTarget s -> Some s
                | _ -> None
            )
            |> Array.distinct

        // the order has a per kg or per m2 dose when an adjusted dose has a limit, also beside an
        // absolute limit of the same dose
        let useAdjust = ord |> EquationMapping.usesAdjust

        let dils =
            pr.SolutionRules
            |> Array.collect _.Diluents
            |> Array.map _.Generic
            |> Array.distinct

        let dil =
            // look if the order has a diluent
            dils
            |> Array.tryFind (fun dil ->
                ord.Orderable.Components
                |> List.map (_.Name >> Name.toString)
                |> List.exists ((=) dil)
            )

        let cmp = cmpNames |> Array.tryExactlyOne

        let itm = itms |> Array.tryExactlyOne

        let useRenalRule = pr.RenalRules |> Array.isEmpty |> not

        ord
        |> Order.productIds
        |> create
            no
            (pr.DoseRule.Generic |> Generic.toString)
            pr.DoseRule.Indication
            (pr.DoseRule.Generic.Form |> PharmaceuticalForm.toString)
            pr.DoseRule.Route
            pr.DoseRule.DoseType
            dil
            cmp
            itm
            dils
            optionalCmps
            itms
            ord
            useAdjust
            useRenalRule
            pr.DoseRule.RenalRuleSource
            fitsOnlyWithoutDivisible
        |> setOrderTableFormat


    /// The serializable shape of an OrderScenario. Text blocks are stored as kind and text, the order
    /// as an order Dto and the dose type as a string.
    module Dto =

        type Dto =
            {
                No: int
                Name: string
                Indication: string
                Form: string
                Route: string
                DoseType: string
                Diluent: string option
                Component: string option
                Item: string option
                Diluents: string[]
                Components: string[]
                Items: string[]
                Prescription: TextBlock.Dto.Dto[][]
                Preparation: TextBlock.Dto.Dto[][]
                Administration: TextBlock.Dto.Dto[][]
                Order: Order.Dto.Dto
                UseAdjust: bool
                UseRenalRule: bool
                RenalRule: string option
                FitsOnlyWithoutDivisible: bool
                RulesNoLongerApply: bool
                NotRecalculated: bool
                ProductsIds: string[]
                Access: string
            }


        let private blocksToDto (bs: TextBlock[][]) = bs |> Array.map (Array.map TextBlock.Dto.toDto)


        let private blocksFromDto (bs: TextBlock.Dto.Dto[][]) =
            bs
            |> Array.toList
            |> List.map (fun line ->
                let line = line |> DtoResult.orEmpty

                line
                |> Array.toList
                |> List.map TextBlock.Dto.fromDto
                |> DtoResult.sequence
                |> Result.map List.toArray
            )
            |> DtoResult.sequence
            |> Result.map List.toArray
            |> Result.mapError List.concat


        let toDto (sc: OrderScenario) : Dto =
            {
                No = sc.No
                Name = sc.Name
                Indication = sc.Indication
                Form = sc.Form
                Route = sc.Route
                DoseType = sc.DoseType |> DoseTypeDto.toString
                Diluent = sc.Diluent
                Component = sc.Component
                Item = sc.Item
                Diluents = sc.Diluents
                Components = sc.Components
                Items = sc.Items
                Prescription = sc.Prescription |> blocksToDto
                Preparation = sc.Preparation |> blocksToDto
                Administration = sc.Administration |> blocksToDto
                Order = sc.Order |> Order.Dto.toDto
                UseAdjust = sc.UseAdjust
                UseRenalRule = sc.UseRenalRule
                RenalRule = sc.RenalRule
                FitsOnlyWithoutDivisible = sc.FitsOnlyWithoutDivisible
                RulesNoLongerApply = sc.RulesNoLongerApply
                NotRecalculated = sc.NotRecalculated
                ProductsIds = sc.ProductsIds
                Access = sc.Access |> AccessDevice.toString
            }


        /// Reads a scenario from a Dto. Returns every error found; an order that cannot be created
        /// gives OrderNotCreated.
        let fromDto (dto: Dto) : Result<OrderScenario, DtoError list> =
            Nested.required
                "OrderScenario"
                dto
                (fun dto ->
                    let doseType = dto.DoseType |> DoseTypeDto.fromString |> Result.mapError List.singleton

                    let prescription = dto.Prescription |> DtoResult.orEmpty |> blocksFromDto
                    let preparation = dto.Preparation |> DtoResult.orEmpty |> blocksFromDto
                    let administration = dto.Administration |> DtoResult.orEmpty |> blocksFromDto

                    let order =
                        try
                            dto.Order
                            |> Order.Dto.fromDto
                            |> Result.mapError (fun m -> [ DtoError.OrderNotCreated $"{m}" ])
                        with exn ->
                            Error [ DtoError.OrderNotCreated exn.Message ]

                    let errorsOf (r: Result<_, DtoError list>) =
                        match r with
                        | Error es -> es
                        | Ok _ -> []

                    let allErrors =
                        errorsOf doseType
                        @ errorsOf prescription
                        @ errorsOf preparation
                        @ errorsOf administration
                        @ errorsOf order

                    match allErrors, doseType, prescription, preparation, administration, order with
                    | [], Ok doseType, Ok prescription, Ok preparation, Ok administration, Ok order ->
                        Ok
                            {
                                No = dto.No
                                Name = dto.Name
                                Indication = dto.Indication
                                Form = dto.Form
                                Route = dto.Route
                                DoseType = doseType
                                Diluent = dto.Diluent
                                Component = dto.Component
                                Item = dto.Item
                                Diluents = dto.Diluents |> DtoResult.orEmpty
                                Components = dto.Components |> DtoResult.orEmpty
                                Items = dto.Items |> DtoResult.orEmpty
                                Prescription = prescription
                                Preparation = preparation
                                Administration = administration
                                Order = order
                                UseAdjust = dto.UseAdjust
                                UseRenalRule = dto.UseRenalRule
                                RenalRule = dto.RenalRule
                                // false when the field is missing from the Dto
                                FitsOnlyWithoutDivisible = dto.FitsOnlyWithoutDivisible
                                // false when the field is missing from the Dto
                                RulesNoLongerApply = dto.RulesNoLongerApply
                                // false when the field is missing from the Dto
                                NotRecalculated = dto.NotRecalculated
                                ProductsIds = dto.ProductsIds |> DtoResult.orEmpty
                                // AnyAccess when the access is missing or unknown
                                Access =
                                    dto.Access
                                    |> Option.ofObj
                                    |> Option.bind AccessDevice.tryFromString
                                    |> Option.defaultValue AnyAccess
                            }
                    | errors, _, _, _, _, _ -> Error errors
                )


/// Functions on the outcome of an evaluation.
module Outcome =

    /// Maps the value of an evaluated or a refused outcome; a refused outcome keeps its refusal.
    let map f =
        function
        | Evaluated a -> Evaluated(f a)
        | Refused(a, r) -> Refused(f a, r)


    /// Returns the value of an evaluated or a refused outcome.
    let get =
        function
        | Evaluated a
        | Refused(a, _) -> a


    /// Maps the value of an evaluated outcome; a refused outcome is not changed.
    let mapEvaluated f =
        function
        | Evaluated a -> Evaluated(f a)
        | refused -> refused


module OrderContext =

    open ConsoleTables
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib

    open Informedica.GenUnits.Lib
    open FilterHelpers

    module CorePatientData = Informedica.GenCore.Lib.Patients.PatientData


    type Command =
        | UpdateOrderContext of OrderContext
        | SelectOrderScenario of OrderContext
        | UpdateOrderScenario of OrderContext
        | ResetOrderScenario of OrderContext
        | ReloadResources of OrderContext
        // Frequency property commands
        | DecreaseScheduleFrequencyProperty of OrderContext
        | IncreaseScheduleFrequencyProperty of OrderContext
        | SetMinScheduleFrequencyProperty of OrderContext
        | SetMaxScheduleFrequencyProperty of OrderContext
        | SetMedianScheduleFrequencyProperty of OrderContext
        // DoseQuantity property commands (ntimes = number of times to adjust, useCalc = use calculated increment)
        | DecreaseOrderableDoseQuantityProperty of OrderContext * ntimes: int * useCalc: bool
        | IncreaseOrderableDoseQuantityProperty of OrderContext * ntimes: int * useCalc: bool
        | SetMinOrderableDoseQuantityProperty of OrderContext
        | SetMaxOrderableDoseQuantityProperty of OrderContext
        | SetMedianOrderableDoseQuantityProperty of OrderContext
        /// The dose quantity at a percentage of its range.
        | SetOrderableDoseQuantityPercProperty of OrderContext * perc: int
        // DoseRate property commands (ntimes = number of times to adjust, useCalc = use calculated increment)
        | DecreaseOrderableDoseRateProperty of OrderContext * ntimes: int * useCalc: bool
        | IncreaseOrderableDoseRateProperty of OrderContext * ntimes: int * useCalc: bool
        | SetMinOrderableDoseRateProperty of OrderContext
        | SetMaxOrderableDoseRateProperty of OrderContext
        | SetMedianOrderableDoseRateProperty of OrderContext
        // Component Quantity property commands (cmp = component, ntimes = number of times to adjust,
        // useCalc = use calculated increment)
        | DecreaseComponentQuantityProperty of OrderContext * cmp: string * ntimes: int * useCalc: bool
        | IncreaseComponentQuantityProperty of OrderContext * cmp: string * ntimes: int * useCalc: bool
        | SetMinComponentQuantityProperty of OrderContext * cmp: string
        | SetMaxComponentQuantityProperty of OrderContext * cmp: string
        | SetMedianComponentQuantityProperty of OrderContext * cmp: string
        /// Sets a filter field to its nth option, counted from 0, or empties it with None, and looks
        /// up the rules again.
        | ChangeFilter of OrderContext * OrderCategory * FilterField * nth: int option
        /// Selects the components at these positions of the options and looks up the rules again.
        | SetNthComponents of OrderContext * nth: int[]
        /// Empties the filter, the scenarios and the argumentation, keeps the patient data, and looks
        /// up the rules again.
        | ClearAllFilter of OrderContext
        /// Selects the nth scenario, counted from 0, and calculates its order.
        | SelectNthOrderScenario of OrderContext * nth: int
        /// Sets one order value to its nth value with the property command and solves the order.
        | SetNthOrderValue of OrderContext * ChangePropertyCommand
        /// Recalculates one order value with the property command. Refused as not solved when there is
        /// no command or the command has no recalculation in the order processor.
        | ReCalcOrderValue of OrderContext * ChangePropertyCommand option
        /// Sets the filter choices by name from a seed source and looks up the rules again. The source
        /// decides which other choices stay, see seedFilter.
        | SeedFilter of
            OrderContext *
            SeedSource *
            indication: string option *
            generic: string option *
            route: string option *
            form: string option *
            doseType: DoseType option

    module Command =


        let get =
            function
            | UpdateOrderContext ctx -> ctx
            | SelectOrderScenario ctx -> ctx
            | UpdateOrderScenario ctx -> ctx
            | ResetOrderScenario ctx -> ctx
            | ReloadResources ctx -> ctx
            // Frequency property commands
            | DecreaseScheduleFrequencyProperty ctx -> ctx
            | IncreaseScheduleFrequencyProperty ctx -> ctx
            | SetMinScheduleFrequencyProperty ctx -> ctx
            | SetMaxScheduleFrequencyProperty ctx -> ctx
            | SetMedianScheduleFrequencyProperty ctx -> ctx
            // DoseQuantity property commands
            | DecreaseOrderableDoseQuantityProperty(ctx, _, _) -> ctx
            | IncreaseOrderableDoseQuantityProperty(ctx, _, _) -> ctx
            | SetMinOrderableDoseQuantityProperty ctx -> ctx
            | SetMaxOrderableDoseQuantityProperty ctx -> ctx
            | SetMedianOrderableDoseQuantityProperty ctx -> ctx
            | SetOrderableDoseQuantityPercProperty(ctx, _) -> ctx
            // DoseRate property commands
            | DecreaseOrderableDoseRateProperty(ctx, _, _) -> ctx
            | IncreaseOrderableDoseRateProperty(ctx, _, _) -> ctx
            | SetMinOrderableDoseRateProperty ctx -> ctx
            | SetMaxOrderableDoseRateProperty ctx -> ctx
            | SetMedianOrderableDoseRateProperty ctx -> ctx
            // Component Quantity property commands
            | DecreaseComponentQuantityProperty(ctx, _, _, _) -> ctx
            | IncreaseComponentQuantityProperty(ctx, _, _, _) -> ctx
            | SetMinComponentQuantityProperty(ctx, _) -> ctx
            | SetMaxComponentQuantityProperty(ctx, _) -> ctx
            | SetMedianComponentQuantityProperty(ctx, _) -> ctx
            | ChangeFilter(ctx, _, _, _) -> ctx
            | SetNthComponents(ctx, _) -> ctx
            | ClearAllFilter ctx -> ctx
            | SelectNthOrderScenario(ctx, _) -> ctx
            | SetNthOrderValue(ctx, _) -> ctx
            | ReCalcOrderValue(ctx, _) -> ctx
            | SeedFilter(ctx, _, _, _, _, _, _) -> ctx


        let toString (cmd: Command) =
            match cmd with
            | UpdateOrderContext _ -> "UpdateOrderContext"
            | SelectOrderScenario _ -> "SelectOrderScenario"
            | UpdateOrderScenario _ -> "UpdateOrderScenario"
            | ResetOrderScenario _ -> "ResetOrderScenario"
            | ReloadResources _ -> "ReloadResources"
            | DecreaseScheduleFrequencyProperty _ -> "DecreaseScheduleFrequencyProperty"
            | IncreaseScheduleFrequencyProperty _ -> "IncreaseScheduleFrequencyProperty"
            | SetMinScheduleFrequencyProperty _ -> "SetMinScheduleFrequencyProperty"
            | SetMaxScheduleFrequencyProperty _ -> "SetMaxScheduleFrequencyProperty"
            | SetMedianScheduleFrequencyProperty _ -> "SetMedianScheduleFrequencyProperty"
            | DecreaseOrderableDoseQuantityProperty(_, ntimes, useCalc) ->
                $"DecreaseOrderableDoseQuantityProperty ntimes={ntimes} useCalc={useCalc}"
            | IncreaseOrderableDoseQuantityProperty(_, ntimes, useCalc) ->
                $"IncreaseOrderableDoseQuantityProperty ntimes={ntimes} useCalc={useCalc}"
            | SetMinOrderableDoseQuantityProperty _ -> "SetMinOrderableDoseQuantityProperty"
            | SetMaxOrderableDoseQuantityProperty _ -> "SetMaxOrderableDoseQuantityProperty"
            | SetMedianOrderableDoseQuantityProperty _ -> "SetMedianOrderableDoseQuantityProperty"
            | SetOrderableDoseQuantityPercProperty(_, perc) -> $"SetOrderableDoseQuantityPercProperty perc=%i{perc}"
            | DecreaseOrderableDoseRateProperty(_, ntimes, useCalc) ->
                $"DecreaseOrderableDoseRateProperty ntimes={ntimes} useCalc={useCalc}"
            | IncreaseOrderableDoseRateProperty(_, ntimes, useCalc) ->
                $"IncreaseOrderableDoseRateProperty ntimes={ntimes} useCalc={useCalc}"
            | SetMinOrderableDoseRateProperty _ -> "SetMinOrderableDoseRateProperty"
            | SetMaxOrderableDoseRateProperty _ -> "SetMaxOrderableDoseRateProperty"
            | SetMedianOrderableDoseRateProperty _ -> "SetMedianOrderableDoseRateProperty"
            | DecreaseComponentQuantityProperty(_, cmp, ntimes, useCalc) ->
                $"DecreaseComponentQuantityProperty cmp={cmp} ntimes={ntimes} useCalc={useCalc}"
            | IncreaseComponentQuantityProperty(_, cmp, ntimes, useCalc) ->
                $"IncreaseComponentQuantityProperty cmp={cmp} ntimes={ntimes} useCalc={useCalc}"
            | SetMinComponentQuantityProperty(_, cmp) -> $"SetMinComponentQuantityProperty cmp={cmp}"
            | SetMaxComponentQuantityProperty(_, cmp) -> $"SetMaxComponentQuantityProperty cmp={cmp}"
            | SetMedianComponentQuantityProperty(_, cmp) -> $"SetMedianComponentQuantityProperty cmp={cmp}"
            | ChangeFilter(_, _, field, n) -> $"ChangeFilter %A{field} nth=%A{n}"
            | SetNthComponents(_, ns) -> $"SetNthComponents nth=%A{ns}"
            | ClearAllFilter _ -> "ClearAllFilter"
            | SelectNthOrderScenario(_, n) -> $"SelectNthOrderScenario nth=%i{n}"
            | SetNthOrderValue(_, change) -> $"SetNthOrderValue %A{change}"
            | ReCalcOrderValue(_, change) -> $"ReCalcOrderValue %A{change}"
            | SeedFilter(_, source, _, gen, _, _, _) -> $"SeedFilter %A{source} %A{gen}"


    module Helpers =

        open Informedica.Logging.Lib


        /// Splits a component limit into one limit per Divisible, solvent and predilution of its products, sorted
        /// so the scenarios come in a fixed order; the products without a Divisible come first, and within one
        /// Divisible and solvent the undiluted product comes first. A limit whose products all agree on these is
        /// not split.
        let limitsByDivisible (cl: ComponentLimit) =
            match
                cl.Products
                |> Array.groupBy (fun p ->
                    p.Divisible,
                    p.Reconstitution |> Array.map _.Rule.Solvent,
                    p.Predilution
                    |> Option.map (fun pd -> pd.Rule.MedVolume, pd.Rule.DiluentVolume, pd.Rule.Diluent)
                )
                |> Array.sortBy fst
            with
            | [||]
            | [| _ |] -> [| cl |]
            | groups -> groups |> Array.map (fun (_, ps) -> { cl with Products = ps })


        /// Splits a prescription rule into one rule per combination of the Divisible groups of its
        /// component limits, so a product does not take the increment of another product. Each split
        /// rule gets its own form limits, such as the drop size, from addFormLimits. A rule whose
        /// component limits are not split stays as it is.
        let splitByDivisible addFormLimits pr =
            let groups = pr.DoseRule.ComponentLimits |> Array.map limitsByDivisible

            if groups |> Array.forall (Array.length >> (=) 1) then
                [| pr |]
            else
                groups
                |> Array.map Array.toList
                |> Array.toList
                |> List.combinations
                |> List.map (fun cls ->
                    { pr with DoseRule = { pr.DoseRule with ComponentLimits = cls |> List.toArray } |> addFormLimits }
                )
                |> List.toArray


        /// Returns the split rules in which every component limit with products holds a picked product.
        /// Returns all split rules when none does, so a pick of another medication hides nothing.
        let pickedGroups products groups =
            let isPicked (p: Product) =
                products
                |> Array.contains (
                    Order.pickId p.GPK (p |> Medication.reconstitutionOf) (p |> Medication.predilutionOf)
                )

            let holdsPick (cl: ComponentLimit) =
                cl.Products |> Array.isEmpty || cl.Products |> Array.exists isPicked

            match groups |> Array.filter (_.DoseRule.ComponentLimits >> Array.forall holdsPick) with
            | [||] -> groups
            | picked -> picked


        let changeRuleProductsDivisible pr =
            { pr with
                DoseRule =
                    { pr.DoseRule with
                        ComponentLimits =
                            pr.DoseRule.ComponentLimits
                            |> Array.map (fun cl ->
                                { cl with
                                    Products =
                                        if cl.Products |> Array.isEmpty then
                                            [|
                                                cl.SubstanceLimits
                                                |> Array.map (_.DoseLimitTarget >> LimitTarget.substanceTargetToString)
                                                |> Product.create
                                                    (pr.DoseRule.Generic |> Generic.toString)
                                                    (pr.DoseRule.Generic.Form |> PharmaceuticalForm.toString)
                                                    pr.DoseRule.Route
                                            |]
                                        else
                                            cl.Products |> Array.map (fun p -> { p with Divisible = None })
                                }
                            )
                    }
            }


        /// <summary>Calculates the min and max values of an order and sets the dose units from the
        /// substance limits of the rule.</summary>
        /// <param name="logger">The logger.</param>
        /// <param name="pr">The prescription rule of the order.</param>
        /// <param name="order">The order to calculate.</param>
        /// <returns>The order with the rule, or the order, the rule and the error.</returns>
        let evaluateOrder logger (pr: PrescriptionRule) order =
            order
            |> CalcMinMax
            |> OrderProcessor.processPipeline logger
            |> function
                | Ok ord ->
                    // Set dose units from substance limits
                    let ord =
                        pr.DoseRule.ComponentLimits
                        |> Array.collect _.SubstanceLimits
                        |> Array.filter DoseLimit.isSubstanceLimit
                        |> Array.fold
                            (fun acc dl ->
                                let sn = dl.DoseLimitTarget |> LimitTarget.substanceTargetToString
                                acc |> Order.setDoseUnit sn dl.DoseUnit
                            )
                            ord

                    Ok(ord, pr)
                | Error(ord, m) -> Error(ord, pr, m)


        /// <summary>Makes the orders of the prescription rules and calculates them in parallel. A
        /// medication that cannot become an order is logged and left out.</summary>
        /// <param name="start">The start of the orders in UTC.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="prs">The prescription rules.</param>
        /// <returns>Only the Ok results: each order with its rule and access device.</returns>
        let evaluateRules (start: System.DateTime) (logger: Logger) prs =
            // Flatten all orders from all prescription rules upfront
            let ords =
                prs
                |> Array.collect (fun pr ->
                    // one access device per medication; zip throws when the two differ in length
                    pr
                    |> OrderScenario.accessOfRule
                    |> Array.zip (pr |> Medication.fromRule logger)
                    |> Array.choose (fun (med, access) ->
                        match med |> Medication.toOrder start with
                        | Ok ord -> Some(ord, pr, access)
                        // log the medication that cannot become an order, so it is not left
                        // out of the scenarios unnoticed
                        | Error msg ->
                            $"no order for %s{med.Name}: %A{msg}"
                            |> Events.OrderScenario
                            |> Logging.OrderMessage.OrderEventMessage
                            |> Logging.logError logger

                            None
                    )
                )

            if ords |> Array.isEmpty then
                [||]
            else
                // Evaluate all orders in parallel using Array.Parallel for better performance
                ords
                |> Array.Parallel.map (fun (ord, pr, access) ->
                    evaluateOrder logger pr ord |> Result.map (fun (ord, pr) -> ord, pr, access)
                )
                |> Array.filter Result.isOk


        let processEvaluationResults isAdditive fitsOnlyWithoutDivisible prs =
            prs
            |> Array.mapi (fun i r -> i, r)
            |> Array.choose (
                function
                | i, Ok(ord, pr, access) ->
                    { OrderScenario.fromRule isAdditive fitsOnlyWithoutDivisible i pr ord with Access = access }
                    |> Some
                | _, Error(ord, ctx, errs) ->
                    // TODO: this never gets written!! (evaluateRules already filters to Ok
                    // results before this function ever sees them)
                    ignore (ord, ctx, errs)

                    None
            )


        let printOrder ord =
            ord |> Order.printTable Format.Minimal

            ord


    open Informedica.Logging.Lib
    open Helpers

    module Prescription = Order.Schedule


    /// The filter with no options and no choices.
    let emptyFilter =
        {
            Indications = [||]
            Generics = [||]
            Routes = [||]
            Forms = [||]
            DoseTypes = [||]
            Diluents = [||]
            Components = [||]
            DoseRuleComponents = [||]
            Indication = None
            Generic = None
            Route = None
            Form = None
            DoseType = None
            Diluent = None
            SelectedComponents = ComponentSelection.AllComponents
            PickedProducts = [||]
        }


    /// Whether the rules are looked up for the patient data: always for an adult, for a child only
    /// with a weight. A height is not needed; a rule that needs a height is left out because it
    /// cannot be prescribed.
    let selectsRules patientData =
        patientData |> PatientData.isAdult
        || patientData |> CorePatientData.weightInKg |> Option.isSome


    /// Creates a context for the patient data with the pick lists of the rules that can be prescribed
    /// for the patient. The pick lists are empty for a child without a weight.
    let create logger provider (patientData: PatientData) =
        let opts =
            if patientData |> selectsRules then
                patientData
                |> getPrescriptionRules logger provider
                |> Array.filter (PrescriptionRule.isPrescribable patientData)
                |> PrescriptionRule.filterOptions
            else
                PrescriptionRule.emptyFilterOptions

        let filter =
            { emptyFilter with
                Indications = opts.Indications
                Generics = opts.Generics
                Routes = opts.Routes
                Forms = opts.Forms
                DoseTypes = opts.DoseTypes
            }

        {
            Filter = filter
            PatientData = patientData
            Scenarios = [||]
            Argumentation = None
        }


    /// Returns the five filter picks as the rule lookup reads them: the choice of a filter field, or
    /// else the only option of that field. The patient data of the dose filter is not set.
    let filterPicks (ctx: OrderContext) : DoseFilter =
        let pick chosen offered =
            if chosen |> Option.isSome then
                chosen
            else
                offered |> Array.someIfOne

        { Filter.doseFilter with
            Indication = pick ctx.Filter.Indication ctx.Filter.Indications
            Generic = pick ctx.Filter.Generic ctx.Filter.Generics
            Route = pick ctx.Filter.Route ctx.Filter.Routes
            Form = pick ctx.Filter.Form ctx.Filter.Forms
            DoseType = pick ctx.Filter.DoseType ctx.Filter.DoseTypes
        }


    /// Sets each filter choice to its pick from filterPicks. A choice that is made stays, a list with
    /// one option sets that option as the choice, and a list of more options stays without a choice.
    let withFilterPicks (ctx: OrderContext) =
        let picks = ctx |> filterPicks

        { ctx with
            Filter =
                { ctx.Filter with
                    Indication = picks.Indication
                    Generic = picks.Generic
                    Route = picks.Route
                    Form = picks.Form
                    DoseType = picks.DoseType
                }
        }


    /// Whether the rule lookup would read a choice that the filter does not have: a list with one
    /// option and no choice.
    let choicesOpen ctx = (ctx |> withFilterPicks).Filter <> ctx.Filter


    /// Returns the patient data of the context without its diagnoses, as the rule lookup takes it.
    let matchedPatientData (ctx: OrderContext) = { ctx.PatientData with Diagnoses = [||] }


    /// Returns the dose filter for the picks, the diluent and the selected components of the context,
    /// with the given patient data.
    let doseFilterFor patientData ctx =
        { filterPicks ctx with
            Diluent = ctx.Filter.Diluent
            Components = ctx.Filter.SelectedComponents
            PatientData = { ctx with PatientData = patientData } |> matchedPatientData
        }


    /// Returns the context with its pick lists narrowed to the options of the prescribable rules, and
    /// the rules themselves. A list with one option sets its choice. The rules are only returned when the
    /// indication, the generic, the dose type and the route or the form are picked.
    /// For a child without a weight the context is made afresh and there are no rules.
    let getRules logger (provider: Resources.IResourceProvider) (ctx: OrderContext) =

        if ctx.PatientData |> selectsRules then

            let doseFilter = ctx |> doseFilterFor ctx.PatientData

            let opts = doseFilter |> optionsFor logger provider

            let ind = opts.Indications |> Array.someIfOne
            let gen = opts.Generics |> Array.someIfOne
            let rte = opts.Routes |> Array.someIfOne
            let frm = opts.Forms |> Array.someIfOne
            let dst = opts.DoseTypes |> Array.someIfOne

            { ctx with
                Filter =
                    { ctx.Filter with
                        Indications = opts.Indications
                        Generics = opts.Generics
                        Routes = opts.Routes
                        Forms = opts.Forms
                        DoseTypes = opts.DoseTypes
                        Indication = ind
                        Generic = gen
                        Route = rte
                        Form = frm
                        DoseType = dst
                    }
            },
            match ind, gen, rte, frm, dst with
            | Some _, Some _, Some _, _, Some _
            | Some _, Some _, _, Some _, Some _ ->

                { doseFilter with
                    Indication = ind
                    Generic = gen
                    Route = rte
                    Form = frm
                    DoseType = dst
                }
                |> getPrescribableRules logger provider
            | _ -> Ok [||]
        else
            // the argumentation stays: it is text of the clinician, not of a rule
            let fresh = ctx.PatientData |> create logger provider

            { fresh with Argumentation = ctx.Argumentation }, Ok [||]


    /// The prescribable rules for the picks of the context and the patient. A child without a weight
    /// has none.
    let rulesFor logger provider patientData ctx =
        if patientData |> selectsRules then
            ctx |> doseFilterFor patientData |> getPrescribableRules logger provider
        else
            Ok [||]


    let setFilter filter ctx = { ctx with Filter = filter }


    /// The filter fields the rule lookup reads. The diluent and the components belong to the
    /// scenario, not to the lookup.
    let filterFields =
        [
            FilterField.Indication
            FilterField.Generic
            FilterField.Route
            FilterField.Form
            FilterField.DoseType
        ]


    /// The order in which the filter choices are made, as steps. Emptying a choice clears the
    /// choices in the later steps, not the other choices in its own step. For a medication the
    /// indication and the generic share one step and narrow each other; for a nutrition order the
    /// generic comes first and the indication follows from it.
    let chain category =
        match category with
        | OrderCategory.Medication ->
            [
                [ FilterField.Indication; FilterField.Generic ]
                [ FilterField.Route ]
                [ FilterField.Form ]
                [ FilterField.DoseType ]
            ]
        | OrderCategory.Nutrition _ ->
            [
                [ FilterField.Generic ]
                [ FilterField.Indication ]
                [ FilterField.DoseType ]
            ]


    /// Clears the choice of one filter field and leaves its options.
    let clearChoice field (f: Filter) =
        match field with
        | FilterField.Indication -> { f with Indication = None }
        | FilterField.Generic -> { f with Generic = None }
        | FilterField.Route -> { f with Route = None }
        | FilterField.Form -> { f with Form = None }
        | FilterField.DoseType -> { f with DoseType = None }
        | FilterField.Diluent -> { f with Diluent = None }
        | FilterField.Components -> { f with SelectedComponents = ComponentSelection.AllComponents }


    /// Clears the choice and the options of one filter field. The options are cleared too because the
    /// rule lookup reads a single option as a choice, so the field would come back chosen. The next
    /// evaluation fills the options again.
    let clearField field (f: Filter) =
        match field with
        | FilterField.Indication ->
            { f with
                Indications = [||]
                Indication = None
            }
        | FilterField.Generic ->
            { f with
                Generics = [||]
                Generic = None
            }
        | FilterField.Route ->
            { f with
                Routes = [||]
                Route = None
            }
        | FilterField.Form ->
            { f with
                Forms = [||]
                Form = None
            }
        | FilterField.DoseType ->
            { f with
                DoseTypes = [||]
                DoseType = None
            }
        | FilterField.Diluent ->
            { f with
                Diluents = [||]
                Diluent = None
            }
        | FilterField.Components ->
            { f with
                Components = [||]
                DoseRuleComponents = [||]
                SelectedComponents = ComponentSelection.AllComponents
            }


    /// Whether any of the five filter choices the rule lookup reads is made.
    let anyChosen (f: Filter) =
        f.Indication.IsSome
        || f.Generic.IsSome
        || f.Route.IsSome
        || f.Form.IsSome
        || f.DoseType.IsSome


    /// Applies a change to one filter choice and removes the scenarios and the picked products.
    /// Emptying a choice also clears its options and the choices in the later steps; when no choice is
    /// left, the options of all lookup fields are cleared. A change to a field the rule lookup reads
    /// can give another dose rule, so it also clears the components, the dose rule components and the
    /// component selection.
    let applyChange category field clearOwn write (ctx: OrderContext) =
        let below =
            if not clearOwn then
                []
            else
                match category |> chain |> List.skipWhile (List.contains field >> not) with
                | [] -> []
                | _ :: rest -> rest |> List.concat

        let filter =
            below
            |> List.fold (fun f x -> f |> clearChoice x) ctx.Filter
            |> fun f -> if clearOwn then f |> clearField field else f
            |> write
            // another dose rule has other components; the next scenario sets them again
            |> fun f ->
                if filterFields |> List.contains field then
                    f |> clearField FilterField.Components
                else
                    f

        let filter =
            if filter |> anyChosen then
                filter
            else
                filterFields |> List.fold (fun f x -> f |> clearField x) filter

        { ctx with
            Filter = { filter with PickedProducts = [||] }
            Scenarios = [||]
        }


    /// Sets or empties the choice of a filter field. Returns the context unchanged when the field
    /// already has that choice.
    let change category field (chosen: string option) (dt: DoseType option) (ctx: OrderContext) =
        let held, write =
            match field with
            | FilterField.Indication ->
                ctx.Filter.Indication = chosen, (fun (f: Filter) -> { f with Indication = chosen })
            | FilterField.Generic -> ctx.Filter.Generic = chosen, (fun (f: Filter) -> { f with Generic = chosen })
            | FilterField.Route -> ctx.Filter.Route = chosen, (fun (f: Filter) -> { f with Route = chosen })
            | FilterField.Form -> ctx.Filter.Form = chosen, (fun (f: Filter) -> { f with Form = chosen })
            | FilterField.DoseType -> ctx.Filter.DoseType = dt, (fun (f: Filter) -> { f with DoseType = dt })
            | FilterField.Diluent -> ctx.Filter.Diluent = chosen, (fun (f: Filter) -> { f with Diluent = chosen })
            | FilterField.Components ->
                // no chosen component selects all components
                let selection =
                    match chosen with
                    | None -> ComponentSelection.AllComponents
                    | Some c -> ComponentSelection.Selected [ c ]

                ctx.Filter.SelectedComponents = selection,
                (fun (f: Filter) -> { f with SelectedComponents = selection })

        let emptied =
            match field with
            | FilterField.DoseType -> dt.IsNone
            | _ -> chosen.IsNone

        if held then
            ctx
        else
            ctx |> applyChange category field emptied write


    /// Returns the nth item of the options, or an error when the index is out of range.
    let nth what n (xs: 'a[]) =
        xs
        |> Array.tryItem n
        |> Option.map Ok
        |> Option.defaultValue (Error $"%s{what}: index %i{n} out of %i{xs.Length}")


    /// The diluent set to its nth option, or emptied with None.
    let changeDiluent (n: int option) (ctx: OrderContext) =
        match n with
        | None -> Ok { ctx with OrderContext.Filter.Diluent = None }
        | Some n ->
            ctx.Filter.Diluents
            |> nth "diluent" n
            |> Result.map (fun d -> { ctx with OrderContext.Filter.Diluent = Some d })


    /// Selects the components at these positions of the options. Returns an error when an index is
    /// out of range.
    let setNthComponents (ns: int[]) (ctx: OrderContext) =
        ns
        |> Array.map (fun n -> ctx.Filter.Components |> nth "component" n)
        |> Array.fold
            (fun acc r ->
                match acc, r with
                | Ok cs, Ok c -> Ok(Array.append cs [| c |])
                | Error e, _
                | _, Error e -> Error e
            )
            (Ok [||])
        // an empty list of positions selects no optional component
        |> Result.map (fun cs ->
            { ctx with OrderContext.Filter.SelectedComponents = cs |> Array.toList |> ComponentSelection.Selected }
        )


    /// Sets a filter field to its nth option, counted from 0, or empties it with None. A diluent or
    /// a component change does not clear other choices, because the rule lookup does not read them.
    /// A component pick selects one component; setNthComponents selects several.
    let changeFilter category field (n: int option) (ctx: OrderContext) =
        let pick what (xs: string[]) =
            match n with
            | None -> Ok(ctx |> change category field None None)
            | Some n ->
                xs
                |> nth what n
                |> Result.map (fun x -> ctx |> change category field (Some x) None)

        match field with
        | FilterField.Indication -> pick "indication" ctx.Filter.Indications
        | FilterField.Generic -> pick "generic" ctx.Filter.Generics
        | FilterField.Route -> pick "route" ctx.Filter.Routes
        | FilterField.Form -> pick "form" ctx.Filter.Forms
        | FilterField.DoseType ->
            match n with
            | None -> Ok(ctx |> change category field None None)
            | Some n ->
                ctx.Filter.DoseTypes
                |> nth "dose type" n
                |> Result.map (fun dt -> ctx |> change category field None (Some dt))
        | FilterField.Diluent -> ctx |> changeDiluent n
        | FilterField.Components ->
            match n with
            | None -> Ok(ctx |> change category field None None)
            | Some n -> ctx |> setNthComponents [| n |]


    /// Empties the filter, the scenarios and the argumentation; the patient data stays.
    let clearAll (ctx: OrderContext) =
        { ctx with
            Filter = emptyFilter
            Scenarios = [||]
            Argumentation = None
        }


    /// Narrows the context to its nth scenario and sets the form of that scenario as the filter form.
    let selectNthScenario n (ctx: OrderContext) =
        ctx.Scenarios
        |> nth "scenario" n
        |> Result.map (fun sc ->
            { ctx with
                OrderContext.Filter.Form = Some sc.Form
                Scenarios = [| sc |]
            }
        )


    /// Sets the filter choices from a seed source. A Formulary or Parenteralia seed removes the
    /// scenarios and keeps the diluent only when the generic, the route and the form are unchanged, for
    /// Formulary also the indication. A Url seed keeps the diluent and the scenarios. A Reload changes
    /// nothing. The picked products and the components
    /// stay only when all five choices are unchanged.
    let seedFilter source ind gen rte frm dt (ctx: OrderContext) =
        let f = ctx.Filter
        let same = gen = f.Generic && rte = f.Route && frm = f.Form
        // all five choices unchanged means the same dose rule
        let samePicks = same && ind = f.Indication && dt = f.DoseType

        let write keep (ctx: OrderContext) =
            let written =
                { ctx with
                    OrderContext.Filter.Indication = ind
                    OrderContext.Filter.Generic = gen
                    OrderContext.Filter.Route = rte
                    OrderContext.Filter.Form = frm
                    OrderContext.Filter.DoseType = dt
                    OrderContext.Filter.Diluent = if keep then ctx.Filter.Diluent else None
                    OrderContext.Filter.PickedProducts = if samePicks then f.PickedProducts else [||]
                }

            // other choices can give another dose rule with other components
            if samePicks then
                written
            else
                { written with Filter = written.Filter |> clearField FilterField.Components }

        match source with
        | SeedSource.Formulary -> { (ctx |> write (same && ind = f.Indication)) with Scenarios = [||] }
        | SeedSource.Parenteralia -> { (ctx |> write same) with Scenarios = [||] }
        | SeedSource.Url -> ctx |> write true
        | SeedSource.Reload -> ctx


    let setFilterGeneric gen ctx = { ctx with OrderContext.Filter.Generic = Some gen }


    let setFilterRoute rte ctx = { ctx with OrderContext.Filter.Route = Some rte }


    let setFilterIndication ind ctx = { ctx with OrderContext.Filter.Indication = Some ind }


    let setFilterForm frm ctx = { ctx with OrderContext.Filter.Form = Some frm }


    let toString stage (ctx: OrderContext) =
        let selection =
            match ctx.Filter.SelectedComponents with
            | ComponentSelection.AllComponents -> "all"
            | ComponentSelection.Selected cs -> cs |> String.concat ", "

        let printArray xs =
            if ctx.Filter.Generic.IsNone || ctx.Filter.Route.IsNone || xs |> Array.length > 10 then
                $"{xs |> Array.length}"
            else
                xs |> String.concat ", "

        let scenarios =
            match ctx.Scenarios |> Array.tryExactlyOne with
            | Some sc ->
                $"""

Scenario Diluent: {sc.Diluent |> Option.defaultValue ""}
Scenario Component: {sc.Component |> Option.defaultValue ""}
Scenario Item: {sc.Item |> Option.defaultValue ""}
Order State: {sc.Order |> OrderProcessor.printState}
"""
            | _ -> $"{ctx.Scenarios |> Array.length}"

        $"""

=== {stage} ===

Patient: {ctx.PatientData |> PatientData.toString}
Indication: {ctx.Filter.Indication |> Option.defaultValue ""}
Generic: {ctx.Filter.Generic |> Option.defaultValue ""}
Form: {ctx.Filter.Form |> Option.defaultValue ""}
Route: {ctx.Filter.Route |> Option.defaultValue ""}
DoseType: {ctx.Filter.DoseType}
Diluent: {ctx.Filter.Diluent |> Option.defaultValue ""}
SelectedComponents: {selection}
Indications: {ctx.Filter.Indications |> printArray}
Generics: {ctx.Filter.Generics |> printArray}
Forms: {ctx.Filter.Forms |> printArray}
Routes: {ctx.Filter.Routes |> printArray}
DoseTypes: {ctx.Filter.DoseTypes |> Array.map DoseType.toString |> printArray}
Diluents : {ctx.Filter.Diluents |> printArray}
Components: {ctx.Filter.Components |> printArray}
Items: {ctx.Scenarios |> Array.collect _.Items |> printArray}
Scenarios: {scenarios}

"""


    /// The scenarios per dose type: only the preparable ones when there are any, otherwise all.
    let filterScenariosByPreparation (scs: OrderScenario[]) =
        scs
        |> Array.groupBy _.DoseType
        |> Array.collect (fun (_, scs) ->
            match scs |> Array.filter (_.Order >> OrderScenario.isPreparable) with
            | [||] -> scs
            | preparable -> preparable
        )


    let updateFilterIfOneScenario ctx =
        match ctx.Scenarios |> Array.tryExactlyOne with
        | None -> ctx
        | Some sc ->
            { ctx with
                Filter =
                    { ctx.Filter with
                        Form = Some sc.Form
                        PickedProducts = sc.ProductsIds
                        Diluent = sc.Diluent
                        // set once from the first scenario, so the other diluents stay available
                        // when a later scenario has only one
                        Diluents =
                            if ctx.Filter.Diluents |> Array.isEmpty then
                                sc.Diluents
                            else
                                ctx.Filter.Diluents
                        // set once from the first scenario: a scenario made after a selection has
                        // only the selected additives, and the options still list the others
                        Components =
                            if ctx.Filter.Components |> Array.isEmpty then
                                sc.Components
                            else
                                ctx.Filter.Components
                        // set once from the first scenario, without its diluent, which a diluent choice changes
                        DoseRuleComponents =
                            if ctx.Filter.DoseRuleComponents |> Array.isEmpty then
                                sc.Order.Orderable.Components
                                |> List.map (_.Name >> Name.toString)
                                |> List.filter (fun n -> Some n <> sc.Diluent)
                                |> List.toArray
                            else
                                ctx.Filter.DoseRuleComponents
                    }
            }


    let applyToOrderScenario scenarioF (ctx: OrderContext) =
        match ctx.Scenarios |> Array.tryExactlyOne with
        | None -> ctx
        | Some _ ->
            { ctx with Scenarios = ctx.Scenarios |> Array.map scenarioF }
            |> updateFilterIfOneScenario


    /// Processes the order of the single scenario with f. Refuses the context as it was sent with
    /// NotSolved when f fails. Logs a context without exactly one scenario and returns it unchanged.
    let processScenarioOrder (logger: Logger) f (ctx: OrderContext) =
        match ctx.Scenarios |> Array.tryExactlyOne with
        | None ->
            "No orders to proces in order context"
            |> Events.OrderScenario
            |> Logging.OrderMessage.OrderEventMessage
            |> Logging.logError logger

            Evaluated ctx
        | Some sc ->
            match sc.Order |> f with
            | Ok ord ->
                { ctx with
                    Scenarios =
                        [|
                            { sc with
                                Order = ord
                                ProductsIds = ord |> Order.productIds
                            }
                            |> OrderScenario.setOrderTableFormat
                        |]
                }
                |> updateFilterIfOneScenario
                |> Evaluated
            | Error _ -> Refused(ctx, Refusal.NotSolved)


    /// The value of the patient the order is adjusted with, in the unit of the order. None when the
    /// patient does not have that value, or the order holds no adjust value.
    let adjustOfPatient patientData ord =
        ord
        |> Order.adjustValue
        |> Option.map ValueUnit.getUnit
        |> Option.bind (fun u ->
            Some u
            |> PatientData.adjustValue patientData
            |> Option.map (ValueUnit.convertTo u)
        )


    /// Whether the rules no longer apply to the order: compares the rules for the patient data of the
    /// context with the rules for the current patient data. Logs the error and returns None when a
    /// lookup failed.
    let rulesVerdict logger ord atContext current =
        match atContext, current with
        | Ok atContext, Ok current ->
            let solutionRules =
                if ord |> Order.isOneDose then
                    SolutionRuleComparison.Counted
                else
                    SolutionRuleComparison.Ignored

            PrescriptionRule.sameRules solutionRules atContext current |> not |> Some
        | Error errs, _
        | _, Error errs ->
            errs |> Events.GenFormErrors |> OrderLogging.logOrderEventError logger
            None


    /// Splits each rule into one rule per group of products with the same Divisible, as the scenarios
    /// are made.
    let rulesByDivisible (provider: Resources.IResourceProvider) rules =
        let addFormLimits = DoseRule.addFormLimits (provider.GetRouteMappings()) (provider.GetFormRoutes())

        rules |> Array.map (splitByDivisible addFormLimits)


    /// Returns the rules the scenario was made from: split by Divisible to the groups of the picked
    /// products, or with the Divisible removed when the order fits only without it.
    let ruleOfScenario provider (sc: OrderScenario) (ctx: OrderContext) rules =
        if sc.FitsOnlyWithoutDivisible then
            rules |> Array.map changeRuleProductsDivisible
        else
            rules
            |> rulesByDivisible provider
            |> Array.collect (pickedGroups ctx.Filter.PickedProducts)


    /// Builds the order of a single rule with the given order id. Returns None when there is not
    /// exactly one rule, when the rule gives more than one medication, or when the medication gives no
    /// order.
    let orderOfRule start logger orderId (rules: PrescriptionRule[]) =
        rules
        |> Array.tryExactlyOne
        |> Option.map (Medication.fromRule logger)
        |> Option.bind Array.tryExactlyOne
        |> Option.bind (fun med -> { med with Id = orderId } |> Medication.toOrder start |> Result.toOption)


    /// Updates the single order of the context for new patient data. The scenario gets the notice when
    /// the rules for the new patient data differ from the rules for the patient data of the context; a
    /// failed lookup leaves the notice unchanged. When the rules are the same, the limits that depend on
    /// the patient come from the current rule; without a single current rule the limits stay and a
    /// warning is logged. The order is calculated again when its adjust value or its limits changed; the
    /// dose the patient gets stays. The context keeps the patient data its rules were chosen for, so the
    /// rules can always be compared. An order that cannot be solved is refused unchanged and marked as not
    /// recalculated. The mark is removed when the new patient data has an adjust value and either the
    /// current rule is known or the adjust value changed.
    let orderOnPatientData logger provider patientData (sc: OrderScenario) ctx =
        let (Id orderId) = sc.Order.Id
        let currentRules = ctx |> rulesFor logger provider patientData
        let noLongerApply =
            rulesVerdict logger sc.Order (ctx |> rulesFor logger provider ctx.PatientData) currentRules

        let orderOfCurrentRule =
            match noLongerApply, currentRules with
            | Some false, Ok rules ->
                let built =
                    rules
                    |> ruleOfScenario provider sc ctx
                    |> orderOfRule (sc.Order |> Order.startOf) logger orderId

                if built |> Option.isNone then
                    $"the limits of %s{sc.Name} stay, the rule of the order is not known"
                    |> Events.OrderScenario
                    |> Informedica.GenOrder.Lib.Logging.logWarning logger

                built
            | _ -> None

        let adjust = sc.Order |> adjustOfPatient patientData

        let recalculate ord =
            let orderWithLimits =
                orderOfCurrentRule
                |> Option.fold (fun o built -> o |> Order.withDefinedLimitsOf built) ord

            let limitsMoved = (orderWithLimits |> Order.definedLimits) <> (ord |> Order.definedLimits)

            match adjust with
            | Some adjust when limitsMoved || Some adjust <> (ord |> Order.adjustValue) ->
                Readjust(orderWithLimits, adjust) |> OrderProcessor.processPipeline logger
            | _ -> Ok ord

        let notice = noLongerApply |> Option.defaultValue sc.RulesNoLongerApply

        // the mark is removed when the patient data has an adjust value and the current rule is known
        // or the adjust value changed
        let standsOnPatient =
            adjust.IsSome
            && (orderOfCurrentRule.IsSome || adjust <> (sc.Order |> Order.adjustValue))

        match
            { ctx with
                Scenarios =
                    [|
                        { sc with
                            RulesNoLongerApply = notice
                            NotRecalculated = sc.NotRecalculated && not standsOnPatient
                        }
                    |]
            }
            |> processScenarioOrder logger recalculate
        with
        | Refused(_, refusal) ->
            let marked =
                { sc with
                    RulesNoLongerApply = notice
                    NotRecalculated = true
                }
                |> OrderScenario.setOrderTableFormat

            Refused({ ctx with Scenarios = [| marked |] }, refusal)
        | evaluated -> evaluated


    /// The quantity in the orderable of each component, when every component holds a single quantity;
    /// None otherwise.
    let componentQuantities (ord: Order) =
        ord.Orderable.Components
        |> List.foldBack (fun cmp acc ->
            let qty = cmp.OrderableQuantity |> OrderVariable.Quantity.toOrdVar

            if qty |> OrderVariable.hasOneValue then
                qty
                |> OrderVariable.getValSetValueUnit
                |> Option.map2 (fun acc q -> (cmp.Name |> Name.toString, q) :: acc) acc
            else
                None
        )
        <| Some []


    /// Sets the component quantities of the previous context again on a context rebuilt after a component
    /// selection or a diluent choice: each component by name, and the quantity of the previous diluent on
    /// the new diluent. Nothing is set when a component of the previous order has no single quantity, or
    /// when solving fails.
    let setComponentQuantities logger (previous: OrderContext) (ctx: OrderContext) =
        match previous.Scenarios |> Array.tryExactlyOne, ctx.Scenarios |> Array.tryExactlyOne with
        | Some prev, Some sc ->
            match prev.Order |> componentQuantities with
            | None -> ctx
            | Some qtys ->
                let cmpName name =
                    match prev.Diluent, sc.Diluent with
                    | Some p, Some d when name = p -> d
                    | _ -> name

                let qtys = qtys |> List.map (fun (n, q) -> cmpName n, q)

                match
                    ctx
                    |> processScenarioOrder
                        logger
                        (fun ord -> SetComponentQuantities(ord, qtys) |> OrderProcessor.processPipeline logger)
                with
                | Evaluated changed -> changed
                | Refused _ -> ctx
        | _ -> ctx


    /// Sets the component quantities of the context as sent on the evaluated context after a component
    /// selection or a diluent choice. Any other command returns the context unchanged.
    let restoreQuantities logger cmd =
        match cmd with
        | ChangeFilter(sent, _, FilterField.Diluent, _)
        | SetNthComponents(sent, _) -> setComponentQuantities logger sent
        | _ -> id


    /// Applies a filter change, a component or scenario selection, or a seed to its context. Returns the
    /// changed context with the command it is evaluated as, or an error when an index is past the options.
    /// Returns None for any other command.
    let resolveChange cmd =
        let over asCommand changed = changed |> Result.map (fun ctx -> ctx, asCommand) |> Some

        match cmd with
        | ChangeFilter(ctx, category, field, n) -> ctx |> changeFilter category field n |> over UpdateOrderContext
        | SetNthComponents(ctx, ns) -> ctx |> setNthComponents ns |> over UpdateOrderContext
        | ClearAllFilter ctx -> Ok(clearAll ctx) |> over UpdateOrderContext
        | SelectNthOrderScenario(ctx, n) -> ctx |> selectNthScenario n |> over SelectOrderScenario
        | SeedFilter(ctx, source, ind, gen, rte, frm, dt) ->
            Ok(ctx |> seedFilter source ind gen rte frm dt) |> over UpdateOrderContext
        | _ -> None


    /// Changes the order of the single scenario with the property command, without solving it.
    let changeOrder logger change ctx =
        ctx
        |> applyToOrderScenario (fun sc ->
            { sc with Order = sc.Order |> OrderProcessor.processChangeProperty logger change }
        )


    /// The error message that ofOutcome returns for a refusal without a context.
    let noDoseRulesMessage = "Geen doseerregels gevonden voor het geselecteerde filter"


    /// Whether the patient lacks a measurement for every prescription rule: at least one rule, and
    /// none of them can be prescribed for the patient. No rules at all is not a missing measurement.
    let measurementMissing patientData prs =
        prs |> Array.isEmpty |> not
        && prs |> Array.forall (PrescriptionRule.isPrescribable patientData >> not)


    /// Sets the scenarios on the context. Refuses with NoDiluent when there are no scenarios and every
    /// medication lacks a needed diluent. The medications are only made when there are no scenarios.
    let withScenarios (ctx: OrderContext) meds scenarios =
        let answered = { ctx with Scenarios = scenarios }

        if scenarios |> Array.isEmpty && meds () |> Medication.allLackNeededDiluent then
            Refused(answered, Refusal.NoDiluent)
        else
            answered |> updateFilterIfOneScenario |> Evaluated


    /// Returns the refusal for an empty result: NoDoseRules when no dose rule fits the picks in the
    /// department of the patient, NoDoseRulesForPatient when none fits the patient, MeasurementMissing
    /// when the patient lacks a measurement for every prescription rule, and otherwise NoProducts, when
    /// the rule lookup dropped the rules for having no product or no dose type.
    let refusalOf (forPicks: 'a[]) (forPatient: 'a[]) measurementMissing =
        if forPicks |> Array.isEmpty then
            Refusal.NoDoseRules
        elif forPatient |> Array.isEmpty then
            Refusal.NoDoseRulesForPatient
        elif measurementMissing then
            Refusal.MeasurementMissing
        else
            Refusal.NoProducts


    /// The dose rules applying to the department of the patient:
    /// every rule with limits, and a dose rule without limits only when a solution rule for the
    /// department limits every component of it.
    let applyingToDepartment provider patientData drs =
        let routeMapping = Api.getRouteMapping provider
        let department = patientData |> PatientData.department

        let solutionRules =
            Api.getSolutionRules provider
            |> Array.filter (fun solRule ->
                department
                |> PatientCategory.departmentMatches solRule.PatientCategory.Department
            )

        drs
        |> Array.filter (fun dr ->
            dr |> DoseRule.hasNoLimits |> not
            || dr |> SolutionRule.anyLimitsDoseRule routeMapping solutionRules
        )


    /// Returns the refusal for the context from the dose rules of the provider, see refusalOf. For a
    /// child without a weight no rule fits the patient. A dose rule without limits counts for the picks
    /// only when a solution rule of the department limits it. It counts for the patient only when a
    /// solution rule for the patient limits it, or when it has no products, which gives NoProducts.
    let refusal provider (ctx: OrderContext) =
        // only the department of the patient, so a rule of that department counts for the picks
        let forPicks =
            Api.getDoseRules provider
            |> Api.filterDoseRules
                provider
                { filterPicks ctx with
                    PatientData = { PatientData.unknown with Department = ctx.PatientData.Department }
                }
            |> applyingToDepartment provider ctx.PatientData

        if ctx.PatientData |> selectsRules then
            let filter = { filterPicks ctx with PatientData = ctx |> matchedPatientData }

            let prs = filter |> Api.filterPrescriptionRules provider |> Result.defaultValue [||]

            let prescribedIds = prs |> Array.map _.DoseRule.Id |> Set.ofArray

            // reconstitutes the dose rule for the patient, as the rule lookup does, before reading its products
            let hasNoProducts dr =
                let pat = filter.PatientData

                dr
                |> DoseRule.reconstitute
                    (Api.getRouteMapping provider)
                    pat.Location
                    (pat |> PatientData.department)
                    (Product.Reconstitution.matchesPatient pat)
                |> fst
                |> _.ComponentLimits
                |> Array.collect _.Products
                |> Array.isEmpty

            // a rule without limits counts when a prescription rule was made from it, which needs a solution
            // rule that limits it; a rule without products also counts, so the refusal is NoProducts
            let forPatient =
                forPicks
                |> Api.filterDoseRules provider filter
                |> Array.filter (fun dr ->
                    dr |> DoseRule.hasNoLimits |> not
                    || prescribedIds.Contains dr.Id
                    || dr |> hasNoProducts
                )

            prs |> measurementMissing filter.PatientData |> refusalOf forPicks forPatient
        else
            refusalOf forPicks [||] false


    /// Sets each pick list of a refused context to the options the rules for the patient give with
    /// all the other picks, so a pick that does not fit can be changed. The picks are not changed. For a
    /// child without a weight the lists are not changed.
    let withListsOfOtherPicks logger provider (ctx: OrderContext) =
        // the options for the picks of the context, with one pick cleared
        let optionsWithout clear =
            { Filter.doseFilter with
                Indication = ctx.Filter.Indication
                Generic = ctx.Filter.Generic
                Route = ctx.Filter.Route
                Form = ctx.Filter.Form
                DoseType = ctx.Filter.DoseType
                PatientData = ctx |> matchedPatientData
            }
            |> clear
            |> optionsFor logger provider

        if ctx.PatientData |> selectsRules then
            { ctx with
                Filter =
                    { ctx.Filter with
                        Indications = (optionsWithout (fun f -> { f with Indication = None })).Indications
                        Generics = (optionsWithout (fun f -> { f with Generic = None })).Generics
                        Routes = (optionsWithout (fun f -> { f with Route = None })).Routes
                        Forms = (optionsWithout (fun f -> { f with Form = None })).Forms
                        DoseTypes = (optionsWithout (fun f -> { f with DoseType = None })).DoseTypes
                    }
            }
        else
            ctx


    /// Looks up the rules for the context and makes its scenarios. Picks that match no rule are refused
    /// with the context as it was sent. When every medication lacks a needed diluent, the context with
    /// the narrowed picks is refused with NoDiluent. Returns the error when the rule lookup fails.
    let getScenarios
        (start: System.DateTime)
        logger
        provider
        (sent: OrderContext)
        : Result<Outcome<OrderContext>, Message list>
        =
        let inputFilter = sent.Filter
        let ctx, result = sent |> getRules logger provider

        let inputHadSelections =
            inputFilter.Generic.IsSome
            || inputFilter.Indication.IsSome
            || inputFilter.Route.IsSome
            || inputFilter.Form.IsSome
            || inputFilter.DoseType.IsSome

        let outputIsEmpty = ctx.Filter.Generics |> Array.isEmpty && ctx.Filter.Indications |> Array.isEmpty

        match result with
        | Error e when inputHadSelections && outputIsEmpty ->
            // propagate the underlying error when getRules failed
            Error e
        | _ when inputHadSelections && outputIsEmpty -> Refused(sent, refusal provider sent) |> Ok
        | _ ->
            let prs =
                match result with
                | Ok prs -> prs
                | Error _ -> [||]

            if prs |> Array.isEmpty then
                ctx |> updateFilterIfOneScenario |> Evaluated |> Ok
            else
                // Note: different prescription rules can exist based on multiple pharmaceutical forms
                // and multiple solution rules
                let groups = prs |> rulesByDivisible provider
                let allGroups = groups |> Array.concat
                let picked = groups |> Array.collect (pickedGroups ctx.Filter.PickedProducts)
                let evaluated = picked |> evaluateRules start logger

                // when the picked groups give no order, try all groups before the retry without Divisible
                let evaluated =
                    if evaluated |> Array.isEmpty && picked.Length < allGroups.Length then
                        allGroups |> evaluateRules start logger
                    else
                        evaluated

                let fitsOnlyWithoutDivisible, results =
                    match evaluated with
                    | [||] ->
                        // no valid results so evaluate again
                        // with changed product divisibility
                        true, prs |> Array.map changeRuleProductsDivisible |> evaluateRules start logger
                    | results -> false, results

                let scenarios =
                    results
                    |> processEvaluationResults (Api.isAdditive provider) fitsOnlyWithoutDivisible
                    |> filterScenariosByPreparation

                let meds () = prs |> Array.collect (Medication.fromRule logger)

                scenarios |> withScenarios ctx meds |> Ok


    let reloadResources (start: System.DateTime) logger provider ctx =
        Api.reloadCache logger provider

        ctx |> getScenarios start logger provider


    /// Whether the change sets or recalculates a dose per kg or per m2. A new change of a dose per kg
    /// or per m2 must be added here; the last case answers false for every change not listed.
    let changesAdjustedDose change =
        match change with
        | SetNthComponentDoseQuantityAdjust _
        | SetNthItemDoseQuantityAdjust _
        | SetNthItemDosePerTimeAdjust _
        | SetNthItemDoseRateAdjust _
        | ReCalcComponentDoseQuantityAdjust _
        | ReCalcItemDoseQuantityAdjust _
        | ReCalcItemDosePerTimeAdjust _
        | ReCalcItemDoseRateAdjust _ -> true
        | _ -> false


    /// Whether the one scenario of the context holds an order without a dose per kg or per m2.
    let hasNoAdjustedDose (ctx: OrderContext) =
        ctx.Scenarios
        |> Array.tryExactlyOne
        |> Option.exists (_.Order >> EquationMapping.usesAdjust >> not)


    /// Puts the command as it was sent in a refused outcome, so a value that cannot be solved leaves
    /// the order as it was before the value was set.
    let refusedAsSent sent outcome =
        match outcome with
        | Refused(_, refusal) -> Refused(sent, refusal)
        | Evaluated _ -> outcome


    /// Evaluates a command and returns the outcome. A command that looks up the rules is refused when
    /// no rule fits the picks or every medication lacks its diluent. A command on the scenario order is
    /// refused when the order cannot be solved, or when it changes a dose per kg or per m2 that the order
    /// does not have. Returns an error when the rule lookup fails or an index is past the options.
    let rec evaluateOutcome (start: System.DateTime) logger provider cmd : Result<Outcome<Command>, Message list> =
        // Helper to process property commands when there's exactly one scenario with an order
        let processPropertyCmd ctx propCmd wrapResult =
            match ctx.Scenarios |> Array.tryExactlyOne with
            | Some _ ->
                ctx
                |> processScenarioOrder
                    logger
                    (fun o -> ChangeProperty(o, propCmd) |> OrderProcessor.processPipeline logger)
                |> Outcome.map wrapResult
                |> Ok
            | None ->
                // No single scenario, return ctx unchanged
                wrapResult ctx |> Evaluated |> Ok

        // evaluates the changed context as asCommand and wraps the result with wrap; an index past
        // the options is an error
        let evaluateAs asCommand wrap changed =
            changed
            |> Result.mapError (fun e -> [ ErrorMsg(e, None) ])
            |> Result.bind (asCommand >> evaluateOutcome start logger provider)
            |> Result.map (Outcome.map (Command.get >> wrap))

        // like evaluateAs, and applies f to an evaluated context only
        let evaluateAsWith f asCommand wrap changed =
            changed
            |> evaluateAs asCommand id
            |> Result.map (Outcome.mapEvaluated f >> Outcome.map wrap)

        match cmd with
        | UpdateOrderContext ctx ->
            ctx
            |> getScenarios start logger provider
            |> Result.map (Outcome.map UpdateOrderContext)
        | ReloadResources ctx ->
            ctx
            |> reloadResources start logger provider
            |> Result.map (Outcome.map ReloadResources)
        // TODO: need to implement validation
        | SelectOrderScenario ctx ->
            ctx
            |> processScenarioOrder logger (CalcValues >> OrderProcessor.processPipeline logger)
            |> Outcome.map SelectOrderScenario
            |> Ok
        | UpdateOrderScenario ctx ->
            ctx
            |> processScenarioOrder logger (SolveOrder >> OrderProcessor.processPipeline logger)
            |> Outcome.map UpdateOrderScenario
            |> Ok
        | ResetOrderScenario ctx ->
            ctx
            |> processScenarioOrder logger (ReCalcValues >> OrderProcessor.processPipeline logger)
            |> Outcome.map ResetOrderScenario
            |> Ok
        // Frequency property commands
        | DecreaseScheduleFrequencyProperty ctx ->
            processPropertyCmd ctx DecreaseScheduleFrequency DecreaseScheduleFrequencyProperty
        | IncreaseScheduleFrequencyProperty ctx ->
            processPropertyCmd ctx IncreaseScheduleFrequency IncreaseScheduleFrequencyProperty
        | SetMinScheduleFrequencyProperty ctx ->
            processPropertyCmd ctx SetMinScheduleFrequency SetMinScheduleFrequencyProperty
        | SetMaxScheduleFrequencyProperty ctx ->
            processPropertyCmd ctx SetMaxScheduleFrequency SetMaxScheduleFrequencyProperty
        | SetMedianScheduleFrequencyProperty ctx ->
            processPropertyCmd ctx SetMedianScheduleFrequency SetMedianScheduleFrequencyProperty
        // Dose Quantity property commands
        | DecreaseOrderableDoseQuantityProperty(ctx, ntimes, useCalc) ->
            processPropertyCmd
                ctx
                (DecreaseOrderableDoseQuantity(ntimes, useCalc))
                (fun ctx -> DecreaseOrderableDoseQuantityProperty(ctx, ntimes, useCalc))
        | IncreaseOrderableDoseQuantityProperty(ctx, ntimes, useCalc) ->
            processPropertyCmd
                ctx
                (IncreaseOrderableDoseQuantity(ntimes, useCalc))
                (fun ctx -> IncreaseOrderableDoseQuantityProperty(ctx, ntimes, useCalc))
        | SetMinOrderableDoseQuantityProperty ctx ->
            processPropertyCmd ctx SetMinOrderableDoseQuantity SetMinOrderableDoseQuantityProperty
        | SetMaxOrderableDoseQuantityProperty ctx ->
            processPropertyCmd ctx SetMaxOrderableDoseQuantity SetMaxOrderableDoseQuantityProperty
        | SetMedianOrderableDoseQuantityProperty ctx ->
            processPropertyCmd ctx SetMedianOrderableDoseQuantity SetMedianOrderableDoseQuantityProperty
        | SetOrderableDoseQuantityPercProperty(ctx, perc) ->
            processPropertyCmd
                ctx
                (SetOrderableDoseQuantityPerc perc)
                (fun ctx -> SetOrderableDoseQuantityPercProperty(ctx, perc))
        // Dose Rate property commands
        | DecreaseOrderableDoseRateProperty(ctx, ntimes, useCalc) ->
            processPropertyCmd
                ctx
                (DecreaseOrderableDoseRate(ntimes, useCalc))
                (fun ctx -> DecreaseOrderableDoseRateProperty(ctx, ntimes, useCalc))
        | IncreaseOrderableDoseRateProperty(ctx, ntimes, useCalc) ->
            processPropertyCmd
                ctx
                (IncreaseOrderableDoseRate(ntimes, useCalc))
                (fun ctx -> IncreaseOrderableDoseRateProperty(ctx, ntimes, useCalc))
        | SetMinOrderableDoseRateProperty ctx ->
            processPropertyCmd ctx SetMinOrderableDoseRate SetMinOrderableDoseRateProperty
        | SetMaxOrderableDoseRateProperty ctx ->
            processPropertyCmd ctx SetMaxOrderableDoseRate SetMaxOrderableDoseRateProperty
        | SetMedianOrderableDoseRateProperty ctx ->
            processPropertyCmd ctx SetMedianOrderableDoseRate SetMedianOrderableDoseRateProperty
        // Component Quantity property commands
        | DecreaseComponentQuantityProperty(ctx, cmp, ntimes, useCalc) ->
            processPropertyCmd
                ctx
                (DecreaseComponentOrderableQuantity(cmp, ntimes, useCalc))
                (fun ctx -> DecreaseComponentQuantityProperty(ctx, cmp, ntimes, useCalc))
        | IncreaseComponentQuantityProperty(ctx, cmp, ntimes, useCalc) ->
            processPropertyCmd
                ctx
                (IncreaseComponentOrderableQuantity(cmp, ntimes, useCalc))
                (fun ctx -> IncreaseComponentQuantityProperty(ctx, cmp, ntimes, useCalc))
        | SetMinComponentQuantityProperty(ctx, cmp) ->
            processPropertyCmd
                ctx
                (SetMinComponentOrderableQuantity cmp)
                (fun ctx -> SetMinComponentQuantityProperty(ctx, cmp))
        | SetMaxComponentQuantityProperty(ctx, cmp) ->
            processPropertyCmd
                ctx
                (SetMaxComponentOrderableQuantity cmp)
                (fun ctx -> SetMaxComponentQuantityProperty(ctx, cmp))
        | SetMedianComponentQuantityProperty(ctx, cmp) ->
            processPropertyCmd
                ctx
                (SetMedianComponentOrderableQuantity cmp)
                (fun ctx -> SetMedianComponentQuantityProperty(ctx, cmp))
        | ChangeFilter(sent, category, field, n) ->
            sent
            |> changeFilter category field n
            |> evaluateAsWith
                (restoreQuantities logger cmd)
                UpdateOrderContext
                (fun ctx -> ChangeFilter(ctx, category, field, n))
        | SetNthComponents(sent, ns) ->
            sent
            |> setNthComponents ns
            |> evaluateAsWith (restoreQuantities logger cmd) UpdateOrderContext (fun ctx -> SetNthComponents(ctx, ns))
        | ClearAllFilter ctx -> Ok(clearAll ctx) |> evaluateAs UpdateOrderContext ClearAllFilter
        | SelectNthOrderScenario(ctx, n) ->
            ctx
            |> selectNthScenario n
            |> evaluateAs SelectOrderScenario (fun ctx -> SelectNthOrderScenario(ctx, n))
        // a dose per kg or per m2 on an order without one is not changed
        | SetNthOrderValue(ctx, change)
        | ReCalcOrderValue(ctx, Some change) when change |> changesAdjustedDose && ctx |> hasNoAdjustedDose ->
            Ok(Refused(cmd, Refusal.NoAdjustedDose))
        // a change the order processor handles is solved on the order alone, without a rule lookup
        | SetNthOrderValue(ctx, change) when change |> OrderProcessor.CommandBridge.ofChangeProperty |> Option.isSome ->
            ctx
            |> processScenarioOrder logger (fun o -> ChangeProperty(o, change) |> OrderProcessor.processPipeline logger)
            |> Outcome.map (fun ctx -> SetNthOrderValue(ctx, change))
            |> refusedAsSent cmd
            |> Ok
        | SetNthOrderValue(ctx, change) ->
            Ok(ctx |> changeOrder logger change)
            |> evaluateAs UpdateOrderScenario (fun ctx -> SetNthOrderValue(ctx, change))
            |> Result.map (refusedAsSent cmd)
        // a recalculation the order processor handles is solved on the order alone, without a rule lookup
        | ReCalcOrderValue(ctx, Some change) when
            change |> OrderProcessor.CommandBridge.ofChangeProperty |> Option.isSome
            ->
            ctx
            |> processScenarioOrder logger (fun o -> ChangeProperty(o, change) |> OrderProcessor.processPipeline logger)
            |> Outcome.map (fun ctx -> ReCalcOrderValue(ctx, Some change))
            |> refusedAsSent cmd
            |> Ok
        // any other recalculation is refused as not solved, with the command as it was sent
        | ReCalcOrderValue _ -> Ok(Refused(cmd, Refusal.NotSolved))
        | SeedFilter(ctx, source, ind, gen, rte, frm, dt) ->
            Ok(ctx |> seedFilter source ind gen rte frm dt)
            |> evaluateAs UpdateOrderContext (fun ctx -> SeedFilter(ctx, source, ind, gen, rte, frm, dt))


    /// Maps an outcome to a result with messages. An evaluated outcome and a NotSolved, NoAdjustedDose
    /// or NoDiluent refusal return their value; any other refusal returns noDoseRulesMessage.
    let ofOutcome outcome =
        match outcome with
        | Evaluated a
        // these refusals return the context as it was sent
        | Refused(a, Refusal.NotSolved)
        | Refused(a, Refusal.NoAdjustedDose)
        // this refusal returns the context without scenarios
        | Refused(a, Refusal.NoDiluent) -> Ok a
        | Refused _ -> Error [ ErrorMsg(noDoseRulesMessage, None) ]


    /// Evaluates a command and maps the outcome to a result with messages, see ofOutcome.
    let evaluate (start: System.DateTime) logger provider cmd : Result<Command, Message list> =
        cmd |> evaluateOutcome start logger provider |> Result.bind ofOutcome


    /// Matches the filter choices of the context against the pick lists that the rules give for its
    /// patient data; a choice that is not in its list is removed. The scenarios are not changed.
    let reconcile logger provider (ctx: OrderContext) : OrderContext =
        let fresh = create logger provider ctx.PatientData

        { ctx with
            OrderContext.PatientData = fresh.PatientData
            Filter = Filter.reconcile fresh.Filter ctx.Filter
        }


    /// The totals over the orders, for the age, weight and gestational age of the patient.
    let totalsFor (totalsData: TotalsData[]) (patientData: PatientData) orders =
        orders
        |> Totals.getTotals
            totalsData
            (patientData |> CorePatientData.ageInDays)
            (patientData |> CorePatientData.weightInKg)
            (patientData |> CorePatientData.gestationalAgeInDays)


    /// The totals over the orders of the context's scenarios, for the age, weight and gestational age
    /// of the patient.
    let contextTotalsFor (totalsData: TotalsData[]) (patientData: PatientData) (ctx: OrderContext) : Totals =
        ctx.Scenarios |> Array.map _.Order |> totalsFor totalsData patientData


    /// The totals over the orders of the context's scenarios, for its patient's age, weight and
    /// gestational age.
    let contextTotals totalsData (ctx: OrderContext) = ctx |> contextTotalsFor totalsData ctx.PatientData


    let logOrderContext (logger: Logger) msg cmd =
        let log (s: string) =
            s
            |> Events.OrderScenario
            |> Logging.OrderMessage.OrderEventMessage
            |> Logging.logDebug logger

        log $"\n\n=== {cmd |> Command.toString |> String.toUpper} {msg |> String.toUpper} ===\n"
        let ctx = cmd |> Command.get

        match ctx.Scenarios |> Array.tryExactlyOne with
        | Some sc ->
            [
                $"Order is empty: {sc.Order |> Order.areAllConstraintsNotApplied}"
                $"Order has constraints: {sc.Order |> Order.hasConstraints}"
                $"Order within constraints: {sc.Order |> Order.isWithinConstraints true}"
                ""
                $"Order has values: {sc.Order |> Order.hasValues}"
                $"Order is solved: {sc.Order |> Order.isSolved}"
                ""
                $"Doses have values: {sc.Order |> Order.doseHasValues}"
                $"Doses are solved: {sc.Order |> Order.doseIsSolved}"
            ]
            |> String.concat "\n"
            |> log

            if sc.Order |> Order.isWithinConstraints true |> not then
                sc.Order
                |> Order.checkConstraints true
                |> List.map (OrderVariable.toStringWithConstraints true false)
                |> String.concat "\n"
                |> sprintf "Variables outside constraints:\n%s"
                |> log
        | _ -> ()


        ctx |> toString $"Order Context" |> log


        log $"\n===\n"
        cmd


    /// Returns the scenario of a context with exactly one scenario, the order it adds to the plan.
    /// Returns None for several scenarios or none.
    let contribution (ctx: OrderContext) = ctx.Scenarios |> Array.tryExactlyOne


    /// Whether the order context matches a side of a link rule, by the order it is narrowed to: the
    /// generic label, the indication, the route and the dose type of its scenario. A context that is
    /// not narrowed to one order matches no side.
    let matchesLinkSide routeMapping (side: LinkSide) (ctx: OrderContext) =
        match ctx |> contribution with
        | Some sc -> LinkRule.matches routeMapping side sc.Indication sc.Name sc.Route sc.DoseType
        | None -> false


    /// The link rules for which the order context is the main order, when the context is narrowed to
    /// one order; none otherwise. Their linked sides are the orders that can be linked to it.
    let rulesForMainOrder routeMapping (rules: LinkRule[]) (ctx: OrderContext) =
        rules |> Array.filter (fun r -> ctx |> matchesLinkSide routeMapping r.Main)


    /// The order context a side of a link rule offers for the patient, before it is evaluated: the
    /// indication, the generic and the route set when the side gives them; a blank field is left open.
    let ofLinkSide (side: LinkSide) patientData =
        {
            Filter =
                { emptyFilter with
                    Indication = LinkRule.fieldOf side.Indication
                    Generic = LinkRule.fieldOf side.Generic
                    Route = LinkRule.fieldOf side.Route
                }
            PatientData = patientData
            Scenarios = [||]
            Argumentation = None
        }


    /// The context's dose types narrowed to the dose types the side matches.
    let narrowToLinkSide (side: LinkSide) (ctx: OrderContext) =
        { ctx with
            Filter =
                { ctx.Filter with
                    DoseTypes = ctx.Filter.DoseTypes |> Array.filter (LinkRule.doseTypeMatches side.DoseType)
                }
        }


    /// Narrows the indication and generic lists of the context filter to the names the rule set allows.
    /// The other lists and the picks do not change.
    let narrowToRuleSet (set: RuleSet) (ctx: OrderContext) =
        let allowed names (xs: string[]) = xs |> Array.filter (RuleSet.allows names)

        { ctx with
            Filter =
                { ctx.Filter with
                    Indications = ctx.Filter.Indications |> allowed (set.Rows |> RuleSet.distinctOf _.Indication)
                    Generics = ctx.Filter.Generics |> allowed (set.Rows |> RuleSet.distinctOf _.Generic)
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

            if rounds > 0 && (contextOf narrowed |> choicesOpen) then
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


    /// Returns the dose rules for the context as it is, before a command changes it: the stand-alone dose rules,
    /// plus the dose rules that need a main order when the context selects them. The context selects them with a
    /// picked indication and generic, where a single-option list counts as picked, and a dose type list of exactly
    /// one. Only a linked offer, whose dose types are narrowed to the link side, has such a list, because a pick
    /// or a seed does not change the lists of an evaluated context. Prescribing still refuses an order that needs
    /// a main order and has none. Returns an empty array when a resource is missing from the provider.
    let doseRulesOf (provider: Resources.IResourceProvider) (ctx: OrderContext) =
        let picks = ctx |> filterPicks

        // a reload between two reads can leave a resource out of the provider; then there are no dose rules
        try
            let standAlone = provider.Get Resources.Keys.standAloneDoseRules

            match picks.Indication, picks.Generic, ctx.Filter.DoseTypes with
            | Some _, Some _, [| doseType |] ->
                let routes = provider.GetRouteMappings()

                // Every form of the dose, so clearing the form lists the other forms again; the lookup after the
                // command applies the picked form.
                provider.GetDoseRules()
                |> DoseRule.filter
                    routes
                    { picks with
                        Form = None
                        DoseType = Some doseType
                    }
                |> Array.filter (LinkRule.needsMainOrder routes (provider.Get Resources.Keys.linkRules))
                |> Array.append standAlone
            | _ -> standAlone
        with :? System.Collections.Generic.KeyNotFoundException ->
            [||]


    /// Wraps the provider so its dose rules are the dose rules of the context from doseRulesOf. Every dose rule
    /// lookup of an evaluation with the wrapped provider reads them: the pick lists, the scenarios and the refusal.
    let withDoseRulesOf (ctx: OrderContext) (provider: Resources.IResourceProvider) =
        { new Resources.IResourceProvider with
            member _.Get key = provider.Get key
            member _.GetData() = provider.GetData()
            member _.GetUnitMappings() = provider.GetUnitMappings()
            member _.GetRouteMappings() = provider.GetRouteMappings()
            member _.GetValidForms() = provider.GetValidForms()
            member _.GetFormRoutes() = provider.GetFormRoutes()
            member _.GetFormularyProducts() = provider.GetFormularyProducts()
            member _.GetReconstitution() = provider.GetReconstitution()
            member _.GetParenteralMeds() = provider.GetParenteralMeds()
            member _.GetEnteralFeeding() = provider.GetEnteralFeeding()
            member _.GetProducts() = provider.GetProducts()

            member _.GetDoseRules() = ctx |> doseRulesOf provider

            member _.GetSolutionRules() = provider.GetSolutionRules()
            member _.GetRenalRules() = provider.GetRenalRules()
            member _.GetTotals() = provider.GetTotals()
            member _.GetGStandProvider() = provider.GetGStandProvider()
            member _.GetDepartments() = provider.GetDepartments()
            member _.GetLocations() = provider.GetLocations()
            member _.GetResourceInfo() = provider.GetResourceInfo()
        }


    /// The serializable shape of an OrderContext. The filter, the patient data and the scenarios are
    /// stored as their own Dtos, the argumentation as text.
    module Dto =

        type Dto =
            {
                Filter: Filter.Dto.Dto
                PatientData: CorePatientData.Dto.Dto
                Scenarios: OrderScenario.Dto.Dto[]
                Argumentation: string option
            }


        let toDto (ctx: OrderContext) : Dto =
            {
                Filter = ctx.Filter |> Filter.Dto.toDto
                PatientData = ctx.PatientData |> CorePatientData.Dto.toDto
                Scenarios = ctx.Scenarios |> Array.map OrderScenario.Dto.toDto
                Argumentation = ctx.Argumentation
            }


        /// Reads an order context from a Dto on today's date. Returns the errors of the filter,
        /// the patient data and every scenario.
        let fromDto today (dto: Dto) : Result<OrderContext, DtoError list> =
            Nested.required
                "OrderContext"
                dto
                (fun dto ->
                    let filter = Nested.required "Filter" dto.Filter Filter.Dto.fromDto

                    let patient = Nested.required "PatientData" dto.PatientData (Patient.fromDto today)

                    let scenarios =
                        dto.Scenarios
                        |> DtoResult.orEmpty
                        |> Array.toList
                        |> List.map (fun sc -> Nested.required "Scenario" sc OrderScenario.Dto.fromDto)
                        |> DtoResult.sequence
                        |> Result.mapError List.concat

                    match filter, patient, scenarios with
                    | Ok filter, Ok patient, Ok scenarios ->
                        Ok
                            {
                                Filter = filter
                                PatientData = patient
                                Scenarios = scenarios |> List.toArray
                                Argumentation = dto.Argumentation
                            }
                    | _ ->
                        let errorsOf r =
                            match r with
                            | Error es -> es
                            | Ok _ -> []

                        Error(errorsOf filter @ errorsOf patient @ errorsOf scenarios)
                )
