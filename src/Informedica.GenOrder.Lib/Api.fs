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

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open ConsoleWriter.NewLineNoTime
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib


    let replace s =
        s
        |> String.replace "[" ""
        |> String.replace "]" ""
        |> String.replace "<" ""
        |> String.replace ">" ""


    let create no nm ind shp rte dst cmp itm cmps itms ord adj ren rrl : OrderScenario
        =
        {
            No = no
            Name = nm
            Indication = ind
            Shape = shp
            Route = rte
            DoseType = dst
//            Diluent = dil
            Component = cmp
            Item = itm
//            Diluents = dils
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


    let print (sc : OrderScenario) =
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
//            dil
            cmp
            itm
//            dils
            cmps
            itms
            ord
            useAdjust
            useRenalRule
            pr.DoseRule.RenalRule
        |> print


    let calcOrderValues (sc : OrderScenario) =
        { sc with
            Order =
                let ord = sc.Order

                if ord |> Order.doseIsSolved then
                    let dto =
                        ord
                        |> Order.Dto.toDto
                    dto |> Order.Dto.cleanDose

                    dto
                    |> Order.Dto.fromDto
                    |> Order.applyConstraints
                    |> fun o ->
                        writeInfoMessage "== constraints reapplied"
                        o
                    |> Order.solveMinMax false OrderLogger.logger.Logger
                    |> function
                    | Ok ord ->
                        ord
                        |> Order.minIncrMaxToValues OrderLogger.logger.Logger

                    | Error msgs ->
                        writeErrorMessage $"{msgs}"
                        ord
                else
                    ord
                    |> Order.minIncrMaxToValues OrderLogger.logger.Logger
        }
        |> print


    let solveOrder (sc : OrderScenario) =
        { sc with
            Order =
                sc.Order
                |> Order.solveOrder false OrderLogger.logger.Logger
                |> Result.defaultValue sc.Order
        }
        |> print


module PrescriptionContext =

    open MathNet.Numerics
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib

    open Informedica.GenUnits.Lib
    open Filters



    module Helpers =


        /// <summary>
        /// Increase the Orderable Quantity and Rate Increment of an Order.
        /// This allows speedy calculation by avoiding large amount
        /// of possible values.
        /// </summary>
        /// <param name="logger">The OrderLogger to use</param>
        /// <param name="ord">The Order to increase the increment of</param>
        let increaseIncrements logger ord = Order.increaseIncrements logger 10N 50N ord


        let setNormDose logger normDose ord = Order.solveNormDose logger normDose ord


        let changeRuleProductsDivisible rule =
            { rule with
                DoseRule =
                    { rule.DoseRule with
                        Shape = rule.DoseRule.Generic
                        DoseLimits =
                            rule.DoseRule.DoseLimits
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
                                                    rule.DoseRule.Generic
                                                    rule.DoseRule.Route
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
        /// <param name="rule"></param>
        /// <returns>
        /// An array of Results, containing the Order and the PrescriptionRule.
        /// </returns>
        let evaluateRule logger (rule : PrescriptionRule) =
            let eval rule drugOrder =
                drugOrder
                |> DrugOrder.toOrderDto
                |> Order.Dto.fromDto
                |> Order.solveMinMax false logger
                |> Result.bind (increaseIncrements logger)
                |> Result.bind (fun ord ->
                    match rule.DoseRule |> DoseRule.getNormDose with
                    | Some nd ->
                        ord
                        |> Order.minIncrMaxToValues logger
                        |> setNormDose logger nd
                    | None -> Ok ord
                )
                |> function
                | Ok ord ->
                    let ord =
                        rule.DoseRule.DoseLimits
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
                        rule
                        |> PrescriptionRule.filterProducts
                            shps
                            sbsts

                    Ok (ord, pr)
                | Error (ord, m) ->
                    Error (ord, rule, m)

            rule
            |> DrugOrder.fromRule
            |> Array.map (eval rule)


        let evaluateRules rules =
            rules
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


        let processEvaluationResults rs =
            rs
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

        let rules = pat |> PrescriptionRule.get

        let filter =
            {
                Indications = rules |> PrescriptionRule.indications
                Generics = rules |> PrescriptionRule.generics
                Routes = rules |> PrescriptionRule.routes
                Shapes= rules |> PrescriptionRule.shapes
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
                    Diluent = None
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
            let dils = doseFilter |> filterDiluents

            let ind = inds |> Array.someIfOne
            let gen = gens |> Array.someIfOne
            let rte = rtes |> Array.someIfOne
            let shp = shps |> Array.someIfOne
            let dst = dsts |> Array.someIfOne
            let dil =
                if ctx.Filter.Diluent.IsSome then ctx.Filter.Diluent
                else
                    dils |> Array.someIfOne

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
                    Diluent = dil
                }
                |> PrescriptionRule.filter
            | _ -> [||]
        | _ ->
            ctx.Patient |> create
            , [||]
        |> fun (ctx, rules) ->
            let singleRule = rules |> Array.tryExactlyOne
            // with a single prescription rule we can set
            // the diluents and components to choose or select
            { ctx with
                Filter =
                    { ctx.Filter with
                        Diluents =
                            singleRule
                            |> Option.map _.SolutionRules
                            |> Option.map (Array.collect _.Diluents)
                            |> Option.map (Array.map _.Generic)
                            |> Option.defaultValue [||]
                        Components =
                            singleRule
                            |> Option.map _.DoseRule
                            |> Option.map (_.DoseLimits >> Array.map _.Component)
                            |> Option.defaultValue [||]
                            |> function
                                | xs when xs |> Array.length > 0 -> xs |> Array.tail
                                | _ -> [||]
                    }
            }
            ,
            // additional component selection mechanism
            if singleRule.IsNone then rules
            else
                let r = singleRule.Value
                [|
                    { r with
                        PrescriptionRule.DoseRule.DoseLimits =
                                if r.DoseRule.DoseLimits |> Array.length < 2 then r.DoseRule.DoseLimits
                                else
                                    r.DoseRule.DoseLimits
                                    |> Array.skip 1
                                    |> Array.filter (fun dl ->
                                        ctx.Filter.SelectedComponents |> Array.isEmpty ||
                                        ctx.Filter.SelectedComponents |> Array.exists ((=) dl.Component)
                                    )
                                    |> Array.append [| r.DoseRule.DoseLimits[0] |]
                    }
                |]


    let setFilter filter ctx = { ctx with Filter = filter }


    let setFilterGeneric gen ctx =
        { ctx with PrescriptionContext.Filter.Generic = Some gen }


    let setFilterRoute rte ctx =
        { ctx with PrescriptionContext.Filter.Route = Some rte }


    let setFilterIndication ind ctx =
        { ctx with PrescriptionContext.Filter.Indication = Some ind }


    let setFilterShape shp ctx =
        { ctx with PrescriptionContext.Filter.Shape = Some shp }


    let calcOrderValues (ctx : PrescriptionContext) =
        { ctx with
            Scenarios = ctx.Scenarios |> Array.map OrderScenario.calcOrderValues
        }


    let solveOrders (ctx : PrescriptionContext) =
        { ctx with
            Scenarios = ctx.Scenarios |> Array.map OrderScenario.solveOrder
        }


    let changeDiluent (ctx: PrescriptionContext) =
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


    let changeComponents (ctx: PrescriptionContext) =
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


    let toString (pr : PrescriptionContext) =
        let ords =
            pr.Scenarios
            |> Array.tryExactlyOne
            |> Option.map (_.Order >> Order.toString >> String.concat "\n")
            |> Option.defaultValue ""

        $"""
Patient: {pr.Patient |> Patient.toString}
Indications: {pr.Filter.Indications |> Array.length}
Medications: {pr.Filter.Generics |> Array.length}
Routes: {pr.Filter.Routes |> Array.length}
Indication: {pr.Filter.Indication |> Option.defaultValue ""}
Medication: {pr.Filter.Generic |> Option.defaultValue ""}
Route: {pr.Filter.Route |> Option.defaultValue ""}
DoseType: {pr.Filter.DoseType}
Diluent: {pr.Filter.Diluent}
Scenarios: {pr.Scenarios |> Array.length}
{ords}
"""


    /// <summary>
    /// Use a PrescriptionResult to create a new PrescriptionResult.
    /// </summary>
    let evaluate (ctx : PrescriptionContext) =

        if Env.getItem "GENPRES_LOG" |> Option.map (fun s -> s = "1") |> Option.defaultValue false then
            let path = $"{__SOURCE_DIRECTORY__}/log.txt"
            OrderLogger.logger.Start (Some path) OrderLogger.Level.Informative

        let ctx, rules = ctx |> getRules

        if rules |> Array.isEmpty then ctx
        else
            { ctx with
                Scenarios =
                    rules
                    |> evaluateRules
                    |> function
                    | [||] ->
                        // no valid results so evaluate again
                        // with changed product divisibility
                        rules
                        |> Array.map changeRuleProductsDivisible
                        |> evaluateRules
                        |> processEvaluationResults
                    | results ->
                        results
                        |> processEvaluationResults
                    // TODO: shouldn't need to do this?
                    |> Array.distinctBy (fun sc ->
                        sc.DoseType,
                        sc.Preparation,
                        sc.Prescription,
                        sc.Administration
                    )
                    |> function
                        | scs when scs |> Array.length <= 1 -> scs
                        // filter out prescriptions without preparation when not needed
                        | scs ->
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
            }


    let processOrders (ctx: PrescriptionContext) =
        match ctx.Scenarios |> Array.tryExactlyOne with
        | None -> ctx |> evaluate
        | Some sc ->
            { ctx with
                Scenarios =
                    [|
                        if sc.Order |> Order.doseHasValues then sc |> OrderScenario.solveOrder
                        else sc |> OrderScenario.calcOrderValues
                    |]
            }


module Api =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib

    module Prescription = Order.Prescription


    /// <summary>
    /// Use a PrescriptionResult to create a new PrescriptionResult.
    /// </summary>
    let evaluate (pr : PrescriptionContext) =
        if pr.Scenarios |> Array.length <> 1 ||
           pr |> PrescriptionContext.changeDiluent ||
           pr |> PrescriptionContext.changeComponents then pr |> PrescriptionContext.evaluate
        else
            pr |> PrescriptionContext.processOrders


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