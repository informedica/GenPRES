namespace Informedica.GenOrder.Lib

module Filters =

    open Informedica.GenForm.Lib
    open Informedica.GenForm.Lib.Resources
    open Informedica.Logging.Lib

    // Logger-injected variant
    // TODO: the logger arg makes no sense
    let getPrescriptionRules (logger: Logger) (provider: IResourceProvider) =
        Api.getPrescriptionRules provider
        >> function
            | Ok rules -> rules
            | Error _ -> [||]


    // Logger-injected variant
    // TODO: the logger arg makes no sense
    let filterPrescriptionRules (logger: Logger) (provider: IResourceProvider) filter =
        Api.filterPrescriptionRules provider filter
        |> function
            | Ok rules -> rules
            | Error _ -> [||]


    let getIndications logger (provider: IResourceProvider) =
        getPrescriptionRules logger provider >> PrescriptionRule.indications


    let getGenerics logger (provider: IResourceProvider) =
        getPrescriptionRules logger provider >> PrescriptionRule.generics


    let getRoutes logger (provider: IResourceProvider) =
        getPrescriptionRules logger provider >> PrescriptionRule.routes


    let getForms logger (provider: IResourceProvider) =
        getPrescriptionRules logger provider >> PrescriptionRule.forms


    let getFrequencies logger (provider: IResourceProvider) =
        getPrescriptionRules logger provider >> PrescriptionRule.frequencies


    let filterIndications logger (provider: IResourceProvider) =
        filterPrescriptionRules logger provider >> PrescriptionRule.indications


    let filterGenerics logger (provider: IResourceProvider) =
        filterPrescriptionRules logger provider >> PrescriptionRule.generics


    let filterRoutes logger (provider: IResourceProvider) =
        filterPrescriptionRules logger provider >> PrescriptionRule.routes


    let filterForms logger (provider: IResourceProvider) =
        filterPrescriptionRules logger provider >> PrescriptionRule.forms


    let filterDoseTypes logger (provider: IResourceProvider) =
        filterPrescriptionRules logger provider >> PrescriptionRule.doseTypes


    let filterFrequencies (logger: Logger) (provider: IResourceProvider) =
        filterPrescriptionRules logger provider >> PrescriptionRule.frequencies


    let filterDiluents (logger: Logger) (provider: IResourceProvider) =
        filterPrescriptionRules logger provider
        >> PrescriptionRule.diluents
        >> Array.map _.Generic


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


    let create no nm ind frm rte dst dil cmp itm dils cmps itms ord adj ren rrl ids : OrderScenario =
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
            ProductsIds = ids
        }


    let setOrderTableFormat (sc: OrderScenario) =
        let prs, prp, adm =
            sc.Order |> Order.Print.printOrderToTableFormat sc.UseAdjust true sc.Items

        { sc with
            Prescription = prs |> Array.map (Array.map replace)
            Preparation = prp |> Array.map (Array.map replace)
            Administration = adm |> Array.map (Array.map replace)
        }


    let fromRule no pr ord =
        let cmps = pr.DoseRule.ComponentLimits |> Array.map _.Name

        let itms =
            pr.DoseRule.ComponentLimits
            |> Array.collect _.SubstanceLimits
            |> Array.choose (fun dl ->
                match dl.DoseLimitTarget with
                | SubstanceLimitTarget s -> Some s
                | _ -> None
            )
            |> Array.distinct

        let useAdjust = pr.DoseRule |> DoseRule.useAdjust

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

        let cmp = cmps |> Array.tryExactlyOne

        let itm = itms |> Array.tryExactlyOne

        let useRenalRule = pr.RenalRules |> Array.isEmpty |> not

        pr.DoseRule.ComponentLimits
        |> Array.collect _.Products
        |> Array.map _.GPK
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
            cmps
            itms
            ord
            useAdjust
            useRenalRule
            pr.DoseRule.RenalRuleSource
        |> setOrderTableFormat


    /// The serializable shape of an OrderScenario: text blocks as kind and text, the
    /// order as its own Dto, the dose type as a string.
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
                ProductsIds: string[]
            }


        let private blocksToDto (bs: TextBlock[][]) =
            bs |> Array.map (Array.map TextBlock.Dto.toDto)


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
                ProductsIds = sc.ProductsIds
            }


        /// The scenario a Dto is, or every reason it is none. An order that cannot be
        /// created is an error, never a dropped scenario.
        let fromDto (dto: Dto) : Result<OrderScenario, DtoError list> =
            Nested.required
                "OrderScenario"
                dto
                (fun dto ->
                    let doseType =
                        dto.DoseType |> DoseTypeDto.fromString |> Result.mapError List.singleton

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
                                ProductsIds = dto.ProductsIds |> DtoResult.orEmpty
                            }
                    | errors, _, _, _, _, _ -> Error errors
                )


module OrderContext =

    open ConsoleTables
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib

    open Informedica.GenUnits.Lib
    open Filters


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
        // DoseRate property commands (ntimes = number of times to adjust, useCalc = use calculated increment)
        | DecreaseOrderableDoseRateProperty of OrderContext * ntimes: int * useCalc: bool
        | IncreaseOrderableDoseRateProperty of OrderContext * ntimes: int * useCalc: bool
        | SetMinOrderableDoseRateProperty of OrderContext
        | SetMaxOrderableDoseRateProperty of OrderContext
        | SetMedianOrderableDoseRateProperty of OrderContext
        // Component Quantity property commands (cmp = component, ntimes = number of times to adjust, useCalc = use calculated increment)
        | DecreaseComponentQuantityProperty of OrderContext * cmp: string * ntimes: int * useCalc: bool
        | IncreaseComponentQuantityProperty of OrderContext * cmp: string * ntimes: int * useCalc: bool
        | SetMinComponentQuantityProperty of OrderContext * cmp: string
        | SetMaxComponentQuantityProperty of OrderContext * cmp: string
        | SetMedianComponentQuantityProperty of OrderContext * cmp: string

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


    module Helpers =

        open Informedica.Logging.Lib

        (*
        /// <summary>
        /// Increase the Orderable Quantity and Rate Increment of an Order.
        /// This allows speedy calculation by avoiding a large amount
        /// of possible values.
        /// </summary>
        /// <param name="logger">The OrderLogger to use</param>
        /// <param name="ord">The Order to increase the increment of</param>
        let increaseIncrements logger ord = Order.increaseIncrements logger 10 10 ord


        let setNormDose logger normDose ord = Order.solveNormDose logger normDose ord
        *)

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


        /// <summary>
        /// Evaluates a single order against a prescription rule.
        /// Processes the order through the pipeline and filters products based on results.
        /// </summary>
        /// <param name="logger">Logger for diagnostics</param>
        /// <param name="pr">The prescription rule to evaluate against</param>
        /// <param name="order">The order to evaluate</param>
        /// <returns>Result containing the evaluated order and updated prescription rule, or error details</returns>
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

                    // Extract component items for product filtering
                    let compItems =
                        [
                            for cmp in ord.Orderable.Components do
                                let cmpQty =
                                    cmp.ComponentQuantity
                                    |> OrderVariable.Quantity.toOrdVar
                                    |> OrderVariable.getValSetValueUnit

                                if cmpQty.IsSome then
                                    for itm in cmp.Items do
                                        let itmQty =
                                            itm.ComponentConcentration
                                            |> OrderVariable.Concentration.toOrdVar
                                            |> OrderVariable.getValSetValueUnit

                                        if itmQty.IsSome then
                                            {
                                                ComponentName = cmp.Name |> Name.toString
                                                ComponentQuantity = cmpQty.Value
                                                ItemName = itm.Name |> Name.toString
                                                ItemConcentration = itmQty.Value
                                            }
                        ]

                    let pr = pr |> PrescriptionRule.filterProducts compItems

                    Ok(ord, pr)
                | Error(ord, m) -> Error(ord, pr, m)

        (*
        /// <summary>
        /// Evaluate a PrescriptionRule. The PrescriptionRule can result in
        /// multiple Orders, depending on the SolutionRules.
        /// </summary>
        /// <param name="logger">Logger for diagnostics</param>
        /// <param name="pr">The prescription rule to evaluate</param>
        /// <returns>
        /// An array of Results, containing the Order and the PrescriptionRule.
        /// </returns>
        let evaluateRule logger (pr : PrescriptionRule) =
            pr
            |> Medication.fromRule logger
            |> Array.choose (Medication.toOrderDto >> Order.Dto.fromDto >> Result.toOption)
            // Note: multiple solution rules can result in multiple medication templates
            |> Array.map (fun ord -> async { return ord |> evaluateOrder logger pr })
            |> Async.Parallel
        *)


        /// <summary>
        /// Evaluates multiple prescription rules in parallel.
        /// Flattens all orders upfront and uses Array.Parallel for optimal performance.
        /// </summary>
        /// <param name="logger">Logger for diagnostics</param>
        /// <param name="prs">Array of prescription rules to evaluate</param>
        /// <returns>Array of successfully evaluated order-rule pairs</returns>
        let evaluateRules (logger: Logger) prs =
            // Flatten all orders from all prescription rules upfront
            let ords =
                prs
                |> Array.collect (fun pr ->
                    pr
                    |> Medication.fromRule logger
                    |> Array.choose (Medication.toOrderDto >> Order.Dto.fromDto >> Result.toOption)
                    |> Array.map (fun ord -> ord, pr)
                )

            if ords |> Array.isEmpty then
                [||]
            else
                // Evaluate all orders in parallel using Array.Parallel for better performance
                ords
                |> Array.Parallel.map (fun (ord, pr) -> evaluateOrder logger pr ord)
                |> Array.filter Result.isOk


        let processEvaluationResults prs =
            prs
            |> Array.mapi (fun i r -> i, r)
            |> Array.choose (
                function
                | i, Ok(ord, pr) -> OrderScenario.fromRule i pr ord |> Some
                | _, Error(ord, ctx, errs) ->
                    // TODO: this never gets written!!
                    errs |> List.map string |> String.concat "\n" |> writeErrorMessage

                    ord |> Order.toString |> String.concat "\n" |> writeWarningMessage

                    ctx |> sprintf "%A" |> writeWarningMessage

                    None
            )


        let printOrder ord =
            ord |> Order.printTable Format.Minimal

            ord


    open Informedica.Logging.Lib
    open Helpers

    module Prescription = Order.Schedule


    let create logger provider (pat: Patient) =
        let pat =
            { pat with Weight = pat.Weight |> Option.map (ValueUnit.convertTo Units.Weight.kiloGram) }

        let prs = pat |> getPrescriptionRules logger provider

        let filter =
            {
                Indications = prs |> PrescriptionRule.indications
                Generics = prs |> PrescriptionRule.generics
                Routes = prs |> PrescriptionRule.routes
                Forms = prs |> PrescriptionRule.forms
                DoseTypes = prs |> PrescriptionRule.doseTypes
                Diluents = [||]
                Components = [||]
                Indication = None
                Generic = None
                Route = None
                Form = None
                DoseType = None
                Diluent = None
                SelectedComponents = [||]
            }

        {
            Filter = filter
            Patient = pat
            Scenarios = [||]
        }


    let getRules logger provider (ctx: OrderContext) =

        match ctx.Patient.Weight, ctx.Patient.Height, ctx.Patient.Department with
        | Some w, Some h, d when d |> Option.isSome ->

            let ind =
                if ctx.Filter.Indication.IsSome then
                    ctx.Filter.Indication
                else
                    ctx.Filter.Indications |> Array.someIfOne

            let gen =
                if ctx.Filter.Generic.IsSome then
                    ctx.Filter.Generic
                else
                    ctx.Filter.Generics |> Array.someIfOne

            let rte =
                if ctx.Filter.Route.IsSome then
                    ctx.Filter.Route
                else
                    ctx.Filter.Routes |> Array.someIfOne

            let frm =
                if ctx.Filter.Form.IsSome then
                    ctx.Filter.Form
                else
                    ctx.Filter.Forms |> Array.someIfOne

            let dst =
                if ctx.Filter.DoseType.IsSome then
                    ctx.Filter.DoseType
                else
                    ctx.Filter.DoseTypes |> Array.someIfOne

            let doseFilter =
                {
                    Indication = ind
                    Generic = gen
                    Route = rte
                    Form = frm
                    DoseType = dst
                    Diluent = ctx.Filter.Diluent
                    Components = ctx.Filter.SelectedComponents |> Array.toList //TODO probably go for lists
                    Patient =
                        {
                            Location = ctx.Patient.Location
                            Department = d
                            Age = ctx.Patient.Age
                            GestAge = ctx.Patient.GestAge
                            PMAge = ctx.Patient.PMAge
                            Weight = Some w
                            Height = Some h
                            WeightMeasured = ctx.Patient.WeightMeasured
                            HeightMeasured = ctx.Patient.HeightMeasured
                            Diagnoses = [||]
                            Gender = ctx.Patient.Gender
                            Access = ctx.Patient.Access
                            RenalFunction = ctx.Patient.RenalFunction
                        }
                }

            let inds = doseFilter |> filterIndications logger provider
            let gens = doseFilter |> filterGenerics logger provider
            let rtes = doseFilter |> filterRoutes logger provider
            let frms = doseFilter |> filterForms logger provider
            let dsts = doseFilter |> filterDoseTypes logger provider

            let ind = inds |> Array.someIfOne
            let gen = gens |> Array.someIfOne
            let rte = rtes |> Array.someIfOne
            let frm = frms |> Array.someIfOne
            let dst = dsts |> Array.someIfOne

            { ctx with
                Filter =
                    { ctx.Filter with
                        Indications = inds
                        Generics = gens
                        Routes = rtes
                        Forms = frms
                        DoseTypes = dsts
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
                |> Api.filterPrescriptionRules provider
            | _ -> Ok [||]
        | _ -> ctx.Patient |> create logger provider, Ok [||]


    let setFilter filter ctx = { ctx with Filter = filter }


    let setFilterItem item ctx =
        let tryItem n xs =
            xs |> Array.tryItem n |> Option.map Array.singleton |> Option.defaultValue xs

        { ctx with
            OrderContext.Filter.Indications =
                match item with
                | FilterItem.Indication n -> ctx.Filter.Indications |> tryItem n
                | _ -> ctx.Filter.Indications
            OrderContext.Filter.Generics =
                match item with
                | FilterItem.Generic n -> ctx.Filter.Generics |> tryItem n
                | _ -> ctx.Filter.Generics
            OrderContext.Filter.Routes =
                match item with
                | FilterItem.Route n -> ctx.Filter.Routes |> tryItem n
                | _ -> ctx.Filter.Routes
            OrderContext.Filter.Forms =
                match item with
                | FilterItem.Form n -> ctx.Filter.Forms |> tryItem n
                | _ -> ctx.Filter.Forms
            OrderContext.Filter.DoseTypes =
                match item with
                | FilterItem.DoseType n -> ctx.Filter.DoseTypes |> tryItem n
                | _ -> ctx.Filter.DoseTypes
            OrderContext.Filter.Diluents =
                match item with
                | FilterItem.Diluent n -> ctx.Filter.Diluents |> tryItem n
                | _ -> ctx.Filter.Diluents
            OrderContext.Filter.SelectedComponents =
                match item with
                | FilterItem.Component ns ->
                    [|
                        for i in ns do
                            yield! ctx.Filter.SelectedComponents |> tryItem i
                    |]

                | _ -> ctx.Filter.SelectedComponents
        }


    let setFilterGeneric gen ctx =
        { ctx with OrderContext.Filter.Generic = Some gen }


    let setFilterRoute rte ctx =
        { ctx with OrderContext.Filter.Route = Some rte }


    let setFilterIndication ind ctx =
        { ctx with OrderContext.Filter.Indication = Some ind }


    let setFilterForm frm ctx =
        { ctx with OrderContext.Filter.Form = Some frm }


    let checkDiluentChange (ctx: OrderContext) =
        ctx.Scenarios
        |> Array.tryExactlyOne
        |> Option.map (fun sc ->
            let ord = sc.Order

            match ctx.Filter.Diluent with
            | None -> false
            | Some dil ->
                // check if diluent is used in order
                ord.Orderable.Components
                |> List.map (_.Name >> Name.toString)
                |> List.exists ((=) dil)
                |> not
        )
        |> Option.defaultValue false


    let checkComponentChange (ctx: OrderContext) =
        ctx.Scenarios
        |> Array.tryExactlyOne
        |> Option.map (fun sc ->
            let ord = sc.Order

            if
                ctx.Filter.SelectedComponents |> Array.isEmpty
                || ctx.Filter.Components |> Array.isEmpty
            then
                false
            else if ord.Orderable.Components |> List.length = 0 then
                false
            else
                // check if there is a component that is used
                // not in selected components
                ord.Orderable.Components
                |> List.skip 1
                |> List.map (_.Name >> Name.toString)
                |> List.sort
                |> (=) (ctx.Filter.SelectedComponents |> Array.sort |> Array.toList)
                |> not
        )
        |> Option.defaultValue false


    let toString stage (ctx: OrderContext) =
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

Patient: {ctx.Patient |> Patient.toString}
Indication: {ctx.Filter.Indication |> Option.defaultValue ""}
Generic: {ctx.Filter.Generic |> Option.defaultValue ""}
Form: {ctx.Filter.Form |> Option.defaultValue ""}
Route: {ctx.Filter.Route |> Option.defaultValue ""}
DoseType: {ctx.Filter.DoseType}
Diluent: {ctx.Filter.Diluent |> Option.defaultValue ""}
SelectedComponents: {ctx.Filter.SelectedComponents |> printArray}
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


    let filterScenariosByPreparation (scs: OrderScenario[]) =
        if scs |> Array.length <= 1 then
            scs
        else
            // filter out prescriptions without preparation when not needed
            let grouped = scs |> Array.groupBy _.DoseType

            [|
                for _, scs in grouped do
                    if scs |> Array.length <= 1 then
                        scs
                    else if
                        scs
                        |> Array.filter (fun sc ->
                            sc.Preparation
                            |> Array.exists (Array.exists Order.Print.textBlockIsEmpty >> not)
                        )
                        |> Array.length = 0
                    then
                        scs
                    else
                        scs
                        |> Array.filter (fun sc ->
                            sc.Preparation
                            |> Array.exists (Array.exists Order.Print.textBlockIsEmpty >> not)
                        )

            |]
            |> Array.collect id


    let updateFilterIfOneScenario ctx =
        match ctx.Scenarios |> Array.tryExactlyOne with
        | None -> ctx
        | Some sc ->
            { ctx with
                Filter =
                    { ctx.Filter with
                        Form = Some sc.Form
                        Diluent = sc.Diluent
                        // set mechanism once, so when a scenario has only
                        // one diluent, the others are still available
                        Diluents =
                            if ctx.Filter.Diluents |> Array.isEmpty then
                                sc.Diluents
                            else
                                ctx.Filter.Diluents
                        // set mechanism once, so when a scenario has only
                        // selected components, the others are still available
                        Components =
                            if ctx.Filter.Components |> Array.isEmpty then
                                sc.Components |> Array.skip 1
                            else
                                ctx.Filter.Components
                    }
            }


    let applyToOrderScenario scenarioF (ctx: OrderContext) =
        match ctx.Scenarios |> Array.tryExactlyOne with
        | None -> ctx
        | Some _ ->
            { ctx with Scenarios = ctx.Scenarios |> Array.map scenarioF }
            |> updateFilterIfOneScenario


    let processScenarioOrder (logger: Logger) cmd (ctx: OrderContext) =
        match ctx.Scenarios |> Array.tryExactlyOne with
        | None ->
            writeErrorMessage "No orders to proces in order context"
            ctx
        | Some sc ->
            { ctx with
                Scenarios =
                    [|
                        { sc with
                            Order =
                                sc.Order
                                |> cmd
                                |> OrderProcessor.processPipeline logger
                                |> Result.defaultValue sc.Order
                        }
                        |> OrderScenario.setOrderTableFormat
                    |]
            }
            |> updateFilterIfOneScenario


    let getScenarios logger provider ctx =
        let inputFilter = ctx.Filter
        let ctx, result = ctx |> getRules logger provider

        let inputHadSelections =
            inputFilter.Generic.IsSome
            || inputFilter.Indication.IsSome
            || inputFilter.Route.IsSome
            || inputFilter.DoseType.IsSome

        let outputIsEmpty =
            ctx.Filter.Generics |> Array.isEmpty && ctx.Filter.Indications |> Array.isEmpty

        match result with
        | Error e when inputHadSelections && outputIsEmpty ->
            // propagate the underlying error when getRules failed
            Error e
        | _ when inputHadSelections && outputIsEmpty ->
            [
                ErrorMsg("Geen doseerregels gevonden voor het geselecteerde filter", None)
            ]
            |> Error
        | _ ->
            let prs =
                match result with
                | Ok prs -> prs
                | Error _ -> [||]

            if prs |> Array.isEmpty then
                ctx
            else
                { ctx with
                    Scenarios =
                        // Note: different prescription rules can exist based on multiple pharmaceutical forms
                        // and multiple solution rules
                        prs
                        |> evaluateRules logger
                        |> function
                            | [||] ->
                                // no valid results so evaluate again
                                // with changed product divisibility
                                prs |> Array.map changeRuleProductsDivisible |> evaluateRules logger
                            | results -> results
                        |> processEvaluationResults
                        |> filterScenariosByPreparation
                }
            |> updateFilterIfOneScenario
            |> Ok


    let reloadResources logger provider ctx =
        Api.reloadCache logger provider

        ctx |> getScenarios logger provider


    let evaluate logger provider cmd =
        // Helper to process property commands when there's exactly one scenario with an order
        let processPropertyCmd ctx propCmd wrapResult =
            match ctx.Scenarios |> Array.tryExactlyOne with
            | Some _ ->
                ctx
                |> processScenarioOrder logger (fun o -> ChangeProperty(o, propCmd))
                |> wrapResult
                |> Ok
            | None ->
                // No single scenario, return ctx unchanged
                wrapResult ctx |> Ok

        match cmd with
        | UpdateOrderContext ctx -> ctx |> getScenarios logger provider |> Result.map UpdateOrderContext
        | ReloadResources ctx -> ctx |> reloadResources logger provider |> Result.map ReloadResources
        // TODO: need to implement validation
        | SelectOrderScenario ctx -> ctx |> processScenarioOrder logger CalcValues |> SelectOrderScenario |> Ok
        | UpdateOrderScenario ctx -> ctx |> processScenarioOrder logger SolveOrder |> UpdateOrderScenario |> Ok
        | ResetOrderScenario ctx -> ctx |> processScenarioOrder logger ReCalcValues |> ResetOrderScenario |> Ok
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


    /// The context as an evaluation starts from it: its patient as the rules take it, its
    /// filter reconciled against the pick lists the rules give that patient, its scenarios
    /// as held.
    let reconcile logger provider (ctx: OrderContext) : OrderContext =
        let fresh = create logger provider ctx.Patient

        { ctx with
            Patient = fresh.Patient
            Filter = Filter.reconcile fresh.Filter ctx.Filter
        }


    /// The totals over the orders of the context's scenarios, for its patient's age and
    /// weight.
    let intake (totalsData: Types.Data.TotalsData[]) (ctx: OrderContext) : Totals =
        let wght =
            ctx.Patient.Weight |> Option.map (ValueUnit.convertTo Units.Weight.kiloGram)

        ctx.Scenarios
        |> Array.map (_.Order >> Order.Dto.toDto)
        |> Totals.getTotals totalsData ctx.Patient.Age wght


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

        log $"Components change: {ctx |> checkComponentChange}"
        log $"Diluent change: {ctx |> checkDiluentChange}\n"

        ctx |> toString $"Order Context" |> log

        (*
        ctx.Scenarios
        |> Array.iter (_.Order >> Order.stringTable >> log)
        *)

        log $"\n===\n"
        cmd


    /// The order a context contributes to the plan: its scenario, once the context is
    /// narrowed to exactly one; nothing while it holds several candidates or none.
    let contribution (ctx: OrderContext) = ctx.Scenarios |> Array.tryExactlyOne


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
                Patient = ctx.Patient |> Informedica.GenForm.Lib.Patient.Dto.toDto
                Scenarios = ctx.Scenarios |> Array.map OrderScenario.Dto.toDto
            }


        /// The context a Dto is, or every reason it is none: the filter's, the patient's
        /// and every scenario's, a scenario that fails never dropped.
        let fromDto (dto: Dto) : Result<OrderContext, DtoError list> =
            Nested.required
                "OrderContext"
                dto
                (fun dto ->
                    let filter = Nested.required "Filter" dto.Filter Filter.Dto.fromDto

                    let patient =
                        Nested.required
                            "Patient"
                            dto.Patient
                            (fun p ->
                                p
                                |> Informedica.GenForm.Lib.Patient.Dto.fromDto
                                |> Result.mapError (List.map DtoError.Patient)
                            )

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
                                Patient = patient
                                Scenarios = scenarios |> List.toArray
                            }
                    | _ ->
                        let errorsOf r =
                            match r with
                            | Error es -> es
                            | Ok _ -> []

                        Error(errorsOf filter @ errorsOf patient @ errorsOf scenarios)
                )


module Formulary =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib

    module Prescription = Order.Schedule


    let getDoseRules provider filter =
        Api.getDoseRules provider |> Api.filterDoseRules provider filter


    let getSolutionRules provider generic form route =
        Api.getSolutionRules provider
        |> Array.filter (fun sr ->
            generic
            |> Option.map (String.equalsCapInsens sr.Generic)
            |> Option.defaultValue true
            && sr.Form
               |> Option.map (fun s ->
                   if form |> Option.isNone then
                       true
                   else
                       form.Value |> String.equalsCapInsens s
               )
               |> Option.defaultValue true
            && route |> Option.map ((=) sr.Route) |> Option.defaultValue true
        )
