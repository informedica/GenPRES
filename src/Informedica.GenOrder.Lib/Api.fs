namespace Informedica.GenOrder.Lib



module Filters =

    open Informedica.GenForm.Lib


    /// <summary>
    /// Get all possible indications for a Patient
    /// </summary>
    let getIndications = PrescriptionRule.get >> PrescriptionRule.indications


    /// <summary>
    /// Get all possible generics for a Patient
    /// </summary>
    let getGenerics = PrescriptionRule.get >> PrescriptionRule.generics


    /// <summary>
    /// Get all possible routes for a Patient
    /// </summary>
    let getRoutes = PrescriptionRule.get >> PrescriptionRule.routes


    /// <summary>
    /// Get all possible shapes for a Patient
    /// </summary>
    let getShapes = PrescriptionRule.get >> PrescriptionRule.shapes


    /// <summary>
    /// Get all possible frequencies for a Patient
    /// </summary>
    let getFrequencies =  PrescriptionRule.get >> PrescriptionRule.frequencies


    /// <summary>
    /// Filter the indications using a Informedica.GenForm.Lib.Filter
    /// </summary>
    let filterIndications = PrescriptionRule.filter >> PrescriptionRule.indications


    /// <summary>
    /// Filter the generics using a Informedica.GenForm.Lib.Filter
    /// </summary>
    let filterGenerics = PrescriptionRule.filter >> PrescriptionRule.generics


    /// <summary>
    /// Filter the routes using a Informedica.GenForm.Lib.Filter
    /// </summary>
    let filterRoutes = PrescriptionRule.filter >> PrescriptionRule.routes


    /// <summary>
    /// Filter the shapes using a Informedica.GenForm.Lib.Filter
    /// </summary>
    let filterShapes = PrescriptionRule.filter >> PrescriptionRule.shapes


    let filterDoseTypes = PrescriptionRule.filter >> PrescriptionRule.doseTypes


    let filterDiluents = PrescriptionRule.filter >> PrescriptionRule.diluents >> Array.map _.Generic


    /// <summary>
    /// Filter the frequencies using a Informedica.GenForm.Lib.Filter
    /// </summary>
    let filterFrequencies =  PrescriptionRule.filter >> PrescriptionRule.shapes


module OrderScenario =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib


    module Helpers =


        /// <summary>
        /// Increase the Orderable Quantity and Rate Increment of an Order.
        /// This allows speedy calculation by avoiding large amount
        /// of possible values.
        /// </summary>
        /// <param name="logger">The OrderLogger to use</param>
        /// <param name="ord">The Order to increase the increment of</param>
        let increaseIncrements logger ord = Order.increaseIncrements logger 10 10 ord


        let setNormDose logger normDose ord = Order.solveNormDose logger normDose ord


        let replace s =
            s
            |> String.replace "[" ""
            |> String.replace "]" ""
            |> String.replace "<" ""
            |> String.replace ">" ""


    open Helpers


    let create no nm ind shp rte dst dil cmp itm dils cmps itms ord adj ren rrl : OrderScenario
        =
        {
            No = no
            Name = nm
            Indication = ind
            Shape = shp
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
        }


    let setOrderTableFormat (sc : OrderScenario) =
        let prs, prp, adm =
            sc.Order
            |> Order.Print.printOrderToTableFormat sc.UseAdjust true sc.Items

        { sc with
            Prescription = prs |> Array.map (Array.map replace)
            Preparation = prp |> Array.map (Array.map replace)
            Administration = adm |> Array.map (Array.map replace)
        }


    let fromRule no pr ord =
        let cmps =
            pr.DoseRule.DoseLimits
            |> Array.groupBy _.Component// use only main component items
            |> Array.filter (fst >> String.isNullOrWhiteSpace >> not)

        let itms =
            cmps
            |> Array.map snd
            |> Array.tryHead
            |> Option.defaultValue [||]
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

        let dil =
            // look if the order has a diluent
            dils
            |> Array.tryFind (fun dil ->
                ord.Orderable.Components
                |> List.map (_.Name >> Name.toString)
                |> List.exists ((=) dil)
            )

        let cmps = cmps |> Array.map fst

        let cmp = cmps |> Array.tryExactlyOne

        let itm = itms |> Array.tryExactlyOne

        let useRenalRule = pr.RenalRules |> Array.isEmpty |> not

        create
            no
            pr.DoseRule.Generic
            pr.DoseRule.Indication
            pr.DoseRule.Shape
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
            pr.DoseRule.RenalRule
        |> setOrderTableFormat


    let calcOrderValues (sc : OrderScenario) =
        { sc with
            Order =
                sc.Order
                |> Order.minIncrMaxToValues true OrderLogger.logger.Logger
        }
        |> setOrderTableFormat


    let solveOrder (sc : OrderScenario) =
        { sc with
            Order =
                sc.Order
                |> Order.solveOrder false OrderLogger.logger.Logger
                |> Result.defaultValue sc.Order
        }
        |> setOrderTableFormat


    let solveOrderWithMinDose (sc : OrderScenario) =
        { sc with
            Order =
                sc.Order
                |> Order.setMinDose
                |> Order.solveOrder false OrderLogger.logger.Logger
                |> Result.defaultValue sc.Order
        }
        |> setOrderTableFormat


    let solveOrderWithMaxDose (sc : OrderScenario) =
        { sc with
            Order =
                sc.Order
                |> Order.setMaxDose
                |> Order.solveOrder false OrderLogger.logger.Logger
                |> Result.defaultValue sc.Order
        }
        |> setOrderTableFormat


    let solveOrderWithMedianDose (sc : OrderScenario) =
        { sc with
            Order =
                sc.Order
                |> Order.setMedianDose
                |> Order.solveOrder false OrderLogger.logger.Logger
                |> Result.defaultValue sc.Order
        }
        |> setOrderTableFormat



module OrderContext =

    open System
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib

    open Informedica.GenUnits.Lib
    open Filters



    module Helpers =


        open OrderScenario.Helpers


        let changeRuleProductsDivisible pr =
            { pr with
                DoseRule =
                    { pr.DoseRule with
                        Shape = pr.DoseRule.Generic
                        DoseLimits =
                            pr.DoseRule.DoseLimits
                            |> Array.filter DoseRule.DoseLimit.isSubstanceLimit
                            |> Array.map (fun dl ->
                                { dl with
                                    Products =
                                        if dl.Products |> Array.isEmpty then
                                            [|
                                                dl.DoseLimitTarget
                                                |> DoseRule.DoseLimit.substanceDoseLimitTargetToString
                                                |> Array.singleton
                                                |> Array.filter String.notEmpty
                                                |> Product.create
                                                    pr.DoseRule.Generic
                                                    pr.DoseRule.Route
                                            |]
                                        else
                                            dl.Products
                                            |> Array.map (fun p ->
                                                { p with Divisible = None }
                                            )
                                }
                            )
                    }
            }


        /// <summary>
        /// Evaluate a PrescriptionRule. The PrescriptionRule can result in
        /// multiple Orders, depending on the SolutionRules.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="pr"></param>
        /// <returns>
        /// An array of Results, containing the Order and the PrescriptionRule.
        /// </returns>
        let evaluateRule logger (pr : PrescriptionRule) =
            let eval pr drugOrder =
                drugOrder
                |> DrugOrder.toOrderDto
                |> Order.Dto.fromDto
                |> Order.solveMinMax false logger
                |> Result.bind (increaseIncrements logger)
                |> Result.bind (fun ord ->
                    match pr.DoseRule |> DoseRule.getNormDose with
                    | Some nd ->
                        ord
                        |> Order.minIncrMaxToValues true logger
                        |> setNormDose logger nd
                    | None -> Ok ord
                )
                |> function
                | Ok ord ->
                    let ord =
                        pr.DoseRule.DoseLimits
                        |> Array.filter DoseRule.DoseLimit.isSubstanceLimit
                        |> Array.fold (fun acc dl ->
                            let sn =
                                dl.DoseLimitTarget
                                |> DoseRule.DoseLimit.substanceDoseLimitTargetToString
                            acc
                            |> Order.setDoseUnit sn dl.DoseUnit
                        ) ord

                    let dto = ord |> Order.Dto.toDto

                    let compItems =
                        [
                            for c in dto.Orderable.Components do
                                    c.ComponentQuantity.Variable.ValsOpt
                                    |> Option.map (fun v ->
                                        {|
                                            shapeQty = v.Value
                                            substs =
                                                [
                                                    for i in c.Items do
                                                        i.ComponentConcentration.Variable.ValsOpt
                                                        |> Option.map (fun v ->
                                                            {|
                                                                name = i.Name
                                                                qty = v.Value
                                                            |}
                                                        )
                                                ]
                                                |> List.choose id
                                        |}
                                    )
                        ]
                        |> List.choose id

                    let shps =
                        dto.Orderable.Components
                        |> List.choose _.ComponentQuantity.Variable.ValsOpt
                        |> List.toArray
                        |> Array.collect _.Value

                    let sbsts =
                        dto.Orderable.Components
                        |> List.toArray
                        |> Array.collect (fun cDto ->
                            cDto.Items
                            |> List.toArray
                            |> Array.choose (fun iDto ->
                                iDto.ComponentConcentration.Variable.ValsOpt
                                |> Option.map (fun v ->
                                    iDto.Name,
                                    v.Value
                                    |> Array.tryHead
                                )
                            )
                        )
                        |> Array.distinct

                    let pr =
                        pr
                        |> PrescriptionRule.filterProducts
                            shps
                            sbsts

                    Ok (ord, pr)
                | Error (ord, m) ->
                    Error (ord, pr, m)

            pr
            |> DrugOrder.fromRule
            |> Array.map (eval pr)


        let evaluateRules prs =
            prs
            |> Array.map (fun pr ->
                async {
                    return
                        pr
                        |> evaluateRule OrderLogger.logger.Logger
                }
            )
            |> Async.Parallel
            |> Async.RunSynchronously
            |> Array.collect id
            |> Array.filter Result.isOk


        let processEvaluationResults prs =
            prs
            |> Array.mapi (fun i r -> (i, r))
            |> Array.choose (function
                | i, Ok (ord, pr) ->
                    OrderScenario.fromRule i pr ord
                    |> Some
                | _, Error (ord, ctx, errs) ->
                    errs
                    |> List.map string
                    |> String.concat "\n"
                    |> writeErrorMessage

                    ord
                    |> Order.toString
                    |> String.concat "\n"
                    |> writeWarningMessage

                    ctx
                    |> sprintf "%A"
                    |> writeWarningMessage

                    None
            )


    open Helpers


    module Prescription = Order.Prescription


    /// <summary>
    /// Create an initial ScenarioResult for a Patient.
    /// </summary>
    let create (pat : Patient) =
        let pat =
            { pat with
                Weight =
                    pat.Weight
                    |> Option.map (ValueUnit.convertTo Units.Weight.kiloGram)
            }

        let prs = pat |> PrescriptionRule.get

        let filter =
            {
                Indications = prs |> PrescriptionRule.indications
                Generics = prs |> PrescriptionRule.generics
                Routes = prs |> PrescriptionRule.routes
                Shapes= prs |> PrescriptionRule.shapes
                DoseTypes = [||]
                Diluents = [||]
                Components = [||]
                Indication = None
                Generic = None
                Route = None
                Shape = None
                DoseType = None
                Diluent = None
                SelectedComponents = [||]
            }

        {
            Filter = filter
            Patient = pat
            Scenarios = [||]
        }


    let getRules ctx =

        match ctx.Patient.Weight, ctx.Patient.Height, ctx.Patient.Department with
        | Some w, Some h, d when d |> Option.isSome ->

            let ind =
                if ctx.Filter.Indication.IsSome then ctx.Filter.Indication
                else ctx.Filter.Indications |> Array.someIfOne
            let gen =
                if ctx.Filter.Generic.IsSome then ctx.Filter.Generic
                else ctx.Filter.Generics |> Array.someIfOne
            let rte =
                if ctx.Filter.Route.IsSome then ctx.Filter.Route
                else ctx.Filter.Routes |> Array.someIfOne
            let shp =
                if ctx.Filter.Shape.IsSome then ctx.Filter.Shape
                else ctx.Filter.Shapes |> Array.someIfOne
            let dst =
                if ctx.Filter.DoseType.IsSome then ctx.Filter.DoseType
                else ctx.Filter.DoseTypes |> Array.someIfOne

            let doseFilter =
                {
                    Indication = ind
                    Generic = gen
                    Route = rte
                    Shape = shp
                    DoseType = dst
                    Diluent = ctx.Filter.Diluent
                    Components = ctx.Filter.SelectedComponents |> Array.toList //TODO probably go for lists
                    Patient = {
                        Department = d
                        Age = ctx.Patient.Age
                        GestAge = ctx.Patient.GestAge
                        PMAge = ctx.Patient.PMAge
                        Weight = Some w
                        Height = Some h
                        Diagnoses = [||]
                        Gender = ctx.Patient.Gender
                        Locations = ctx.Patient.Locations
                        RenalFunction = ctx.Patient.RenalFunction
                    }
                }

            let inds = doseFilter |> filterIndications
            let gens = doseFilter |> filterGenerics
            let rtes = doseFilter |> filterRoutes
            let shps = doseFilter |> filterShapes
            let dsts = doseFilter |> filterDoseTypes

            let ind = inds |> Array.someIfOne
            let gen = gens |> Array.someIfOne
            let rte = rtes |> Array.someIfOne
            let shp = shps |> Array.someIfOne
            let dst = dsts |> Array.someIfOne

            { ctx with
                Filter =
                    { ctx.Filter with
                        Indications = inds
                        Generics = gens
                        Routes = rtes
                        Shapes = shps
                        DoseTypes = dsts
                        Indication = ind
                        Generic = gen
                        Route = rte
                        Shape = shp
                        DoseType = dst
                    }
            },
            match ind, gen, rte, shp, dst with
            | Some _, Some _, Some _, _,      Some _
            | Some _, Some _, _,      Some _, Some _ ->

                { doseFilter with
                    Indication = ind
                    Generic = gen
                    Route = rte
                    Shape = shp
                    DoseType = dst
                }
                |> PrescriptionRule.filter
            | _ -> [||]
        | _ ->
            ctx.Patient |> create
            , [||]


    let setFilter filter ctx = { ctx with Filter = filter }


    let setFilterGeneric gen ctx =
        { ctx with OrderContext.Filter.Generic = Some gen }


    let setFilterRoute rte ctx =
        { ctx with OrderContext.Filter.Route = Some rte }


    let setFilterIndication ind ctx =
        { ctx with OrderContext.Filter.Indication = Some ind }


    let setFilterShape shp ctx =
        { ctx with OrderContext.Filter.Shape = Some shp }


    let calcOrderValues (ctx : OrderContext) =
        { ctx with
            Scenarios = ctx.Scenarios |> Array.map OrderScenario.calcOrderValues
        }


    let solveOrders (ctx : OrderContext) =
        { ctx with
            Scenarios = ctx.Scenarios |> Array.map OrderScenario.solveOrder
        }


    let changeDiluent (ctx: OrderContext) =
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


    let changeComponents (ctx: OrderContext) =
        ctx.Scenarios
        |> Array.tryExactlyOne
        |> Option.map (fun sc ->
            let ord = sc.Order

            if ctx.Filter.SelectedComponents |> Array.isEmpty ||
               ctx.Filter.Components |> Array.isEmpty then false
            else
                if ord.Orderable.Components |> List.length = 0 then false
                else
                    // check if there is a component that is used
                    // not in selected components
                    ord.Orderable.Components
                    |> List.skip 1
                    |> List.map (_.Name >> Name.toString)
                    |> List.sort
                    |> ((=) (ctx.Filter.SelectedComponents |> Array.sort |> Array.toList))
                    |> not
        )
        |> Option.defaultValue false


    let toString stage (pr: OrderContext) =
        let printArray xs =
            if pr.Filter.Generic.IsNone then $"{xs |> Array.length}"
            else
                xs
                |> String.concat ", "

        let scenarios =
            match pr.Scenarios |> Array.tryExactlyOne with
            | Some sc ->
                sc.Order
                |> Order.toString
                |> String.concat "\n"
                |> fun s -> $"\n{s}\n"
            | _ -> $"{pr.Scenarios |> Array.length}"

        $"""
    === {stage} ===

    Patient: {pr.Patient |> Patient.toString}
    Indication: {pr.Filter.Indication |> Option.defaultValue ""}
    Generic: {pr.Filter.Generic |> Option.defaultValue ""}
    Shape: {pr.Filter.Shape |> Option.defaultValue ""}
    Route: {pr.Filter.Route |> Option.defaultValue ""}
    DoseType: {pr.Filter.DoseType}
    Diluent: {pr.Filter.Diluent |> Option.defaultValue ""}
    SelectedComponents: {pr.Filter.SelectedComponents |> printArray}
    Indications: {pr.Filter.Indications |> printArray}
    Medications: {pr.Filter.Generics |> printArray}
    Routes: {pr.Filter.Routes |> printArray}
    DoseTypes: {pr.Filter.DoseTypes |> Array.map DoseType.toString |> printArray}
    Diluents : {pr.Filter.Diluents |> printArray}
    Components: {pr.Filter.Components |> printArray}
    Items: {pr.Scenarios |> Array.collect _.Items |> printArray}
    Scenarios: {scenarios}

    """


    let filterScenariosByPreparation (scs : OrderScenario []) =
        if scs |> Array.length <= 1 then
                scs
        else
            // filter out prescriptions without preparation when not needed
            let grouped = scs |> Array.groupBy _.DoseType
            [|
                for _, scs in grouped do
                    if scs |> Array.length <= 1 then scs
                    else
                        if scs
                           |> Array.filter (fun sc ->
                               sc.Preparation
                               |> Array.exists (Array.exists String.notEmpty))
                           |> Array.length = 0 then scs
                        else
                            scs
                            |> Array.filter (fun sc ->
                               sc.Preparation
                               |> Array.exists (Array.exists String.notEmpty)
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
                        Shape = Some sc.Shape
                        Diluent = sc.Diluent
                        // set once mechanism, so when a scenario has only
                        // one diluent, the others are still available
                        Diluents =
                            if ctx.Filter.Diluents |> Array.isEmpty then sc.Diluents
                            else ctx.Filter.Diluents
                        // set once mechanism, so when a scenario has only
                        // selected components, the others are still available
                        Components =
                            if ctx.Filter.Components |> Array.isEmpty then
                                sc.Components
                                |> Array.skip 1
                            else ctx.Filter.Components
                    }
            }

    open System.Reflection
    open System.IO

    let getAssemblyPath () =
        let assembly = Assembly.GetExecutingAssembly()
        let location = assembly.Location
        Path.GetDirectoryName(location)


    /// <summary>
    /// Use a PrescriptionResult to create a new PrescriptionResult.
    /// </summary>
    let evaluate (ctx : OrderContext) =
        if Env.getItem "GENPRES_LOG" |> Option.map (fun s -> s = "1") |> Option.defaultValue false then
            let path =
                if getAssemblyPath () |> String.isNullOrWhiteSpace then
                    $"{Environment.CurrentDirectory}/log.txt"
                else
                    $"{getAssemblyPath ()}/log.txt"

            OrderLogger.logger.Start (Some path) OrderLogger.Level.Informative

        let ctx, prs = ctx |> getRules

        if prs |> Array.isEmpty then ctx
        else
            { ctx with
                Scenarios =
                    prs
                    |> evaluateRules
                    |> function
                    | [||] ->
                        // no valid results so evaluate again
                        // with changed product divisibility
                        prs
                        |> Array.map changeRuleProductsDivisible
                        |> evaluateRules
                        |> processEvaluationResults
                    | results ->
                        results
                        |> processEvaluationResults
                    |> filterScenariosByPreparation
            }
        |> updateFilterIfOneScenario


    let applyToOrderScenario scenarioF (ctx: OrderContext) =
        match ctx.Scenarios |> Array.tryExactlyOne with
        | None -> ctx
        | Some _ ->
            { ctx with
                Scenarios = ctx.Scenarios |> Array.map scenarioF
            }
            |> updateFilterIfOneScenario


    let minimizeDose = applyToOrderScenario OrderScenario.solveOrderWithMinDose


    let maximizeDose = applyToOrderScenario OrderScenario.solveOrderWithMaxDose


    let medianDose = applyToOrderScenario OrderScenario.solveOrderWithMedianDose


    let processOrders (ctx: OrderContext) =
        match ctx.Scenarios |> Array.tryExactlyOne with
        | None -> ctx |> evaluate
        | Some sc ->
            { ctx with
                Scenarios =
                    [|
                        if sc.Order |> Order.isSolved then sc
                        else
                            if sc.Order |> Order.doseHasValues then sc |> OrderScenario.solveOrder
                            else
                                sc
                                |> OrderScenario.calcOrderValues

                    |]
            }
            |> updateFilterIfOneScenario


    let printCtx msg ctx =
        writeDebugMessage $"\n\n=== {msg |> String.capitalize} ===\n"

        match ctx.Scenarios |> Array.tryExactlyOne with
        | Some sc ->
            writeDebugMessage $"Order dose is solved: {sc.Order |> Order.doseIsSolved}"
            writeDebugMessage $"Order dose has values: {sc.Order |> Order.doseHasValues}"
        | _ -> ()

        writeDebugMessage $"Components change: {ctx |> changeComponents}"
        writeDebugMessage $"Diluent change: {ctx |> changeDiluent}"

        ctx |> toString msg
        |> writeDebugMessage

        writeDebugMessage $"\n===\n"

        ctx


module Api =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib

    module Prescription = Order.Prescription


    /// <summary>
    /// Use a PrescriptionResult to create a new PrescriptionResult.
    /// </summary>
    let evaluate (ctx : OrderContext) =
        if ctx.Scenarios |> Array.length <> 1 ||
           ctx |> OrderContext.changeDiluent ||
           ctx |> OrderContext.changeComponents then ctx |> OrderContext.evaluate
        else
            ctx |> OrderContext.processOrders


    let getIntake (wght : Informedica.GenUnits.Lib.ValueUnit option) (dto: Order.Dto.Dto []) : Intake =
        let intake =
            dto
            |> Array.map Order.Dto.fromDto
            |> Intake.calc wght Informedica.GenUnits.Lib.Units.Time.day

        let get n =
                intake
                |> Array.tryFind (fst >> String.equalsCapInsens n)
                |> Option.map (fun (_, var) ->
                    var
                    |> Informedica.GenSolver.Lib.Variable.getValueRange
                    |> Informedica.GenSolver.Lib.Variable.ValueRange.toMarkdown 3
                )

        {
            Volume = get "volume"
            Energy = get "energie"
            Protein = get "eiwit"
            Carbohydrate = get "koolhydraat"
            Fat = get "vet"
            Sodium = get "natrium"
            Potassium = get "kalium"
            Chloride = get "chloor"
            Calcium = get "calcium"
            Phosphate = get "fosfaat"
            Magnesium = get "magnesium"
            Iron = get "ijzer"
            VitaminD = get "VitD"
            Ethanol = get "ethanol"
            Propyleenglycol = get "propyleenglycol"
            BenzylAlcohol = get "benzylalcohol"
            BoricAcid = get "boorzuur"
        }


    let getDoseRules filter =
        DoseRule.get ()
        |> DoseRule.filter filter


    let getSolutionRules generic shape route =
        SolutionRule.get ()
        |> Array.filter (fun sr ->
            generic
            |> Option.map (String.equalsCapInsens sr.Generic)
            |> Option.defaultValue true &&
            sr.Shape
            |> Option.map (fun s ->
                if shape |> Option.isNone then true
                else
                    shape.Value
                    |> String.equalsCapInsens s
            ) |> Option.defaultValue true &&
            route
            |> Option.map ((=) sr.Route)
            |> Option.defaultValue true
        )