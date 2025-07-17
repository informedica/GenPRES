namespace Informedica.GenOrder.Lib

module Filters =

    open Informedica.GenForm.Lib


    /// <summary>
    /// Get all possible indications for a Patient
    /// </summary>
    let getIndications = Api.getPrescriptionRules >> PrescriptionRule.indications


    /// <summary>
    /// Get all possible generics for a Patient
    /// </summary>
    let getGenerics = Api.getPrescriptionRules >> PrescriptionRule.generics


    /// <summary>
    /// Get all possible routes for a Patient
    /// </summary>
    let getRoutes = Api.getPrescriptionRules >> PrescriptionRule.routes


    /// <summary>
    /// Get all possible shapes for a Patient
    /// </summary>
    let getShapes = Api.getPrescriptionRules >> PrescriptionRule.shapes


    /// <summary>
    /// Get all possible frequencies for a Patient
    /// </summary>
    let getFrequencies =  Api.getPrescriptionRules >> PrescriptionRule.frequencies


    /// <summary>
    /// Filter the indications using a Informedica.GenForm.Lib.Filter
    /// </summary>
    let filterIndications = Api.filterPrescriptionRules >> PrescriptionRule.indications


    /// <summary>
    /// Filter the generics using a Informedica.GenForm.Lib.Filter
    /// </summary>
    let filterGenerics = Api.filterPrescriptionRules >> PrescriptionRule.generics


    /// <summary>
    /// Filter the routes using a Informedica.GenForm.Lib.Filter
    /// </summary>
    let filterRoutes = Api.filterPrescriptionRules >> PrescriptionRule.routes


    /// <summary>
    /// Filter the shapes using a Informedica.GenForm.Lib.Filter
    /// </summary>
    let filterShapes = Api.filterPrescriptionRules >> PrescriptionRule.shapes


    let filterDoseTypes = Api.filterPrescriptionRules >> PrescriptionRule.doseTypes


    let filterDiluents = Api.filterPrescriptionRules >> PrescriptionRule.diluents >> Array.map _.Generic


    /// <summary>
    /// Filter the frequencies using a Informedica.GenForm.Lib.Filter
    /// </summary>
    let filterFrequencies =  Api.filterPrescriptionRules >> PrescriptionRule.shapes


module OrderScenario =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib


    let replace s =
        s
        |> String.replace "[" ""
        |> String.replace "]" ""
        |> String.replace "<" ""
        |> String.replace ">" ""


    let create no nm ind shp rte dst dil cmp itm dils cmps itms ord adj ren rrl ids : OrderScenario
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
            ProductsIds = ids
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
            pr.DoseRule.ComponentLimits
            |> Array.map _.Name

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


module OrderContext =

    open System
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


    module Command =


        let get = function
            | UpdateOrderContext ctx -> ctx
            | SelectOrderScenario ctx -> ctx
            | UpdateOrderScenario ctx -> ctx
            | ResetOrderScenario ctx -> ctx
            | ReloadResources ctx -> ctx


        let toString = function
            | UpdateOrderContext _ -> "UpdateOrderContext"
            | SelectOrderScenario _ -> "SelectOrderScenario"
            | UpdateOrderScenario _ -> "UpdateOrderScenario"
            | ResetOrderScenario _ -> "ResetOrderScenario"
            | ReloadResources _ -> "ReloadResources"

        let apply f cmd =
            match cmd with
            | UpdateOrderContext ctx -> f ctx
            | SelectOrderScenario ctx -> f ctx
            | UpdateOrderScenario ctx -> f ctx
            | ResetOrderScenario ctx -> f ctx
            | ReloadResources ctx -> f ctx


        let map f cmd =
            match cmd with
            | UpdateOrderContext ctx -> UpdateOrderContext (f ctx)
            | SelectOrderScenario ctx -> SelectOrderScenario (f ctx)
            | UpdateOrderScenario ctx -> UpdateOrderScenario (f ctx)
            | ResetOrderScenario ctx -> ResetOrderScenario (f ctx)
            | ReloadResources ctx -> ReloadResources (f ctx)


        let mapInv cmd f = map f cmd



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
                                                    pr.DoseRule.Generic
                                                    pr.DoseRule.Route
                                            |]
                                        else
                                            cl.Products
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
                |> CalcMinMax
                |> Order.processPipeLine logger (pr.DoseRule |> DoseRule.getNormDose)
                |> function
                | Ok ord ->
                    let ord =
                        pr.DoseRule.ComponentLimits
                        |> Array.collect _.SubstanceLimits
                        |> Array.filter DoseLimit.isSubstanceLimit
                        |> Array.fold (fun acc dl ->
                            let sn =
                                dl.DoseLimitTarget
                                |> LimitTarget.substanceTargetToString
                            acc
                            |> Order.setDoseUnit sn dl.DoseUnit
                        ) ord

                    let compItems =
                        [
                            for cmp in ord.Orderable.Components do
                                    let cmpQty =
                                        cmp.ComponentQuantity
                                        |> OrderVariable.Quantity.toOrdVar
                                        |> OrderVariable.getValueUnit
                                    if cmpQty.IsSome then
                                        for itm in cmp.Items do
                                            let itmQty =
                                                itm.ComponentConcentration
                                                |> OrderVariable.Concentration.toOrdVar
                                                |> OrderVariable.getValueUnit
                                            if itmQty.IsSome then
                                                {
                                                    ComponentName = cmp.Name |> Name.toString
                                                    ComponentQuantity = cmpQty.Value
                                                    ItemName = itm.Name |> Name.toString
                                                    ItemConcentration = itmQty.Value
                                                }
                        ]

                    let pr =
                        pr
                        |> PrescriptionRule.filterProducts compItems

                    Ok (ord, pr)
                | Error (ord, m) ->
                    Error (ord, pr, m)

            pr
            |> DrugOrder.fromRule
            // Note: multiple solution rules can result in multiple drugorders
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


        let printOrder ord =
            ord
            |> Order.printTable Format.Minimal

            ord


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

        let prs = pat |> Api.getPrescriptionRules

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
                |> Api.filterPrescriptionRules
            | _ -> [||]
        | _ ->
            ctx.Patient |> create
            , [||]


    let setFilter filter ctx = { ctx with Filter = filter }


    let setFilterItem item ctx =
        let tryItem n xs =
            xs
            |> Array.tryItem n
            |> Option.map Array.singleton
            |> Option.defaultValue xs
        {
            ctx with
                OrderContext.Filter.Indications =
                    match item with
                    | FilterItem.Indication n ->
                        ctx.Filter.Indications |> tryItem n
                    | _ -> ctx.Filter.Indications
                OrderContext.Filter.Generics =
                    match item with
                    | FilterItem.Generic n ->
                        ctx.Filter.Generics |> tryItem n
                    | _ -> ctx.Filter.Generics
                OrderContext.Filter.Routes =
                    match item with
                    | FilterItem.Route n ->
                        ctx.Filter.Routes |> tryItem n
                    | _ -> ctx.Filter.Routes
                OrderContext.Filter.Shapes =
                    match item with
                    | FilterItem.Shape n ->
                        ctx.Filter.Shapes |> tryItem n
                    | _ -> ctx.Filter.Shapes
                OrderContext.Filter.DoseTypes =
                    match item with
                    | FilterItem.DoseType n ->
                        ctx.Filter.DoseTypes |> tryItem n
                    | _ -> ctx.Filter.DoseTypes
                OrderContext.Filter.Diluents =
                    match item with
                    | FilterItem.Diluent n ->
                        ctx.Filter.Diluents |> tryItem n
                    | _ -> ctx.Filter.Diluents
                OrderContext.Filter.SelectedComponents =
                    match item with
                    | FilterItem.Component ns ->
                        [|
                            for i in ns do
                                yield!
                                    ctx.Filter.SelectedComponents
                                    |> tryItem i
                        |]

                    | _ -> ctx.Filter.SelectedComponents
        }


    let setFilterGeneric gen ctx =
        { ctx with OrderContext.Filter.Generic = Some gen }


    let setFilterRoute rte ctx =
        { ctx with OrderContext.Filter.Route = Some rte }


    let setFilterIndication ind ctx =
        { ctx with OrderContext.Filter.Indication = Some ind }


    let setFilterShape shp ctx =
        { ctx with OrderContext.Filter.Shape = Some shp }


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


    let toString stage (ctx: OrderContext) =
        let printArray xs =
            if ctx.Filter.Generic.IsNone ||
               ctx.Filter.Route.IsNone ||
               xs |> Array.length > 10
                then $"{xs |> Array.length}"
            else
                xs
                |> String.concat ", "

        let scenarios =
            match ctx.Scenarios |> Array.tryExactlyOne with
            | Some sc ->
                    $"""

Scenario Diluent: {sc.Diluent |> Option.defaultValue ""}
Scenario Component: {sc.Component |> Option.defaultValue ""}
Scenario Item: {sc.Item |> Option.defaultValue ""}
Order State: {sc.Order |> Order.printState}
"""
            | _ -> $"{ctx.Scenarios |> Array.length}"

        $"""

=== {stage} ===

Patient: {ctx.Patient |> Patient.toString}
Indication: {ctx.Filter.Indication |> Option.defaultValue ""}
Generic: {ctx.Filter.Generic |> Option.defaultValue ""}
Shape: {ctx.Filter.Shape |> Option.defaultValue ""}
Route: {ctx.Filter.Route |> Option.defaultValue ""}
DoseType: {ctx.Filter.DoseType}
Diluent: {ctx.Filter.Diluent |> Option.defaultValue ""}
SelectedComponents: {ctx.Filter.SelectedComponents |> printArray}
Indications: {ctx.Filter.Indications |> printArray}
Medications: {ctx.Filter.Generics |> printArray}
Routes: {ctx.Filter.Routes |> printArray}
DoseTypes: {ctx.Filter.DoseTypes |> Array.map DoseType.toString |> printArray}
Diluents : {ctx.Filter.Diluents |> printArray}
Components: {ctx.Filter.Components |> printArray}
Items: {ctx.Scenarios |> Array.collect _.Items |> printArray}
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


    let applyToOrderScenario scenarioF (ctx: OrderContext) =
        match ctx.Scenarios |> Array.tryExactlyOne with
        | None -> ctx
        | Some _ ->
            { ctx with
                Scenarios = ctx.Scenarios |> Array.map scenarioF
            }
            |> updateFilterIfOneScenario


    let processOrders cmd (ctx: OrderContext) =
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
                                |> Order.processPipeLine OrderLogger.logger.Logger None
                                |> Result.defaultValue sc.Order
                        }
                        |> OrderScenario.setOrderTableFormat
                    |]
            }
            |> updateFilterIfOneScenario


    let getScenarios ctx =
        let ctx, prs = ctx |> getRules

        if prs |> Array.isEmpty then ctx
        else
            { ctx with
                Scenarios =
                    // Note: different prescription rules can exist based on multiple shapes
                    // and multiple solution rules
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


    let reloadResources ctx =
        Api.reloadCache ()
        ctx |>getScenarios


    let activateLogger () =
        if Env.getItem "GENPRES_LOG"
           |> Option.map (fun s -> s = "1")
           |> Option.defaultValue false then

            let path =
                if getAssemblyPath () |> String.isNullOrWhiteSpace then
                    $"{Environment.CurrentDirectory}/log.txt"
                else
                    $"{getAssemblyPath ()}/log.txt"

            OrderLogger.logger.Start (Some path) OrderLogger.Level.Informative


    /// <summary>
    /// </summary>
    let evaluate cmd =
        activateLogger ()

        match cmd with
        | UpdateOrderContext ctx -> ctx |> getScenarios |> UpdateOrderContext
        | SelectOrderScenario ctx -> ctx |> processOrders CalcValues |> SelectOrderScenario
        | UpdateOrderScenario ctx -> ctx |> processOrders SolveOrder |> UpdateOrderScenario
        | ResetOrderScenario ctx -> ctx |> processOrders ReCalcValues |> ResetOrderScenario
        | ReloadResources ctx -> ctx |> reloadResources |> ReloadResources

    let printCtx msg cmd =
        writeDebugMessage $"\n\n=== {cmd |> Command.toString |> String.toUpper} {msg |> String.toUpper} ===\n"
        let ctx = cmd |> Command.get

        match ctx.Scenarios |> Array.tryExactlyOne with
        | Some sc ->
            [
                $"Order is empty: {sc.Order |> Order.isEmpty}"
                $"Order has constraints: {sc.Order |> Order.hasConstraints}"
                $"Order within constraints: {sc.Order |> Order.isWithinConstraints}"
                ""
                $"Order has values: {sc.Order |> Order.hasValues}"
                $"Order is solved: {sc.Order |> Order.isSolved}"
                ""
                $"Doses have values: {sc.Order |> Order.doseHasValues}"
                $"Doses are solved: {sc.Order |> Order.doseIsSolved}"
            ]
            |> String.concat "\n"
            |> writeDebugMessage

            if sc.Order |> Order.isWithinConstraints |> not then
                sc.Order
                |> Order.checkConstraints
                |> List.map (OrderVariable.toStringWithConstraints true false)
                |> String.concat "\n"
                |> sprintf "Variables outside constraints:\n%s"
                |> writeDebugMessage

        | _ -> ()

        writeDebugMessage $"Components change: {ctx |> checkComponentChange}"
        writeDebugMessage $"Diluent change: {ctx |> checkDiluentChange}\n"

        ctx
        |> toString $"Order Context"
        |> writeDebugMessage

        ctx.Scenarios
        |> Array.tryExactlyOne
        |> Option.iter (_.Order >> Order.printTable Format.Minimal)

        writeDebugMessage $"\n===\n"

        cmd


module Formulary =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib

    module Prescription = Order.Prescription



    let getDoseRules filter =
        Api.getDoseRules ()
        |> Api.filterDoseRules filter


    let getSolutionRules generic shape route =
        Api.getSolutionRules ()
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