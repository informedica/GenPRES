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


    let replace s =
        s
        |> String.replace "[" ""
        |> String.replace "]" ""
        |> String.replace "<" ""
        |> String.replace ">" ""


    let create no pr ord =
        let cmps =
            pr.DoseRule.DoseLimits
            |> Array.groupBy _.Component// use only main component items
            |> Array.filter (fst >> String.isNullOrWhiteSpace >> not)

        let ns =
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

        let prs, prp, adm =
            ord
            |> Order.Print.printOrderToTableFormat useAdjust true ns

        let diluents =
                pr.SolutionRules
                |> Array.collect _.Diluents
                |> Array.map _.Generic
        {
            No = no
            Indication = pr.DoseRule.Indication
            DoseType = pr.DoseRule.DoseType
            Name = pr.DoseRule.Generic
            Components = cmps |> Array.map fst
            Diluents = diluents
            Items = ns
            Shape = pr.DoseRule.Shape
            Route = pr.DoseRule.Route
            Diluent =
                // look if the order has a diluent
                diluents
                |> Array.tryFind (fun dil ->
                    ord.Orderable.Components
                    |> List.map (_.Name >> Name.toString)
                    |> List.exists ((=) dil)
                )
            Component = cmps |> Array.tryExactlyOne |> Option.map fst
            Item = ns |> Array.tryExactlyOne
            Prescription = prs |> Array.map (Array.map replace)
            Preparation = prp |> Array.map (Array.map replace)
            Administration = adm |> Array.map (Array.map replace)
            Order = ord
            UseAdjust = useAdjust
            UseRenalRule = pr.RenalRules |> Array.isEmpty |> not
            RenalRule = pr.DoseRule.RenalRule
        }


module PrescriptionContext =

    open Informedica.GenUnits.Lib
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib
    open Informedica.Utils.Lib
    open Filters

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
                DoseTypes = rules |> PrescriptionRule.doseTypes
                Diluents = rules |> PrescriptionRule.diluents |> Array.map _.Generic
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


    let getRules pr =

        match pr.Patient.Weight, pr.Patient.Height, pr.Patient.Department with
        | Some w, Some h, d when d |> Option.isSome ->

            let ind =
                if pr.Filter.Indication.IsSome then pr.Filter.Indication
                else pr.Filter.Indications |> Array.someIfOne
            let gen =
                if pr.Filter.Generic.IsSome then pr.Filter.Generic
                else pr.Filter.Generics |> Array.someIfOne
            let rte =
                if pr.Filter.Route.IsSome then pr.Filter.Route
                else pr.Filter.Routes |> Array.someIfOne
            let shp =
                if pr.Filter.Shape.IsSome then pr.Filter.Shape
                else pr.Filter.Shapes |> Array.someIfOne
            let dst =
                if pr.Filter.DoseType.IsSome then pr.Filter.DoseType
                else pr.Filter.DoseTypes |> Array.someIfOne

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
                        Age = pr.Patient.Age
                        GestAge = pr.Patient.GestAge
                        PMAge = pr.Patient.PMAge
                        Weight = Some w
                        Height = Some h
                        Diagnoses = [||]
                        Gender = pr.Patient.Gender
                        Locations = pr.Patient.Locations
                        RenalFunction = pr.Patient.RenalFunction
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
                if pr.Filter.Diluent.IsSome then pr.Filter.Diluent
                else
                    dils |> Array.someIfOne

            { pr with
                Filter =
                    { pr.Filter with
                        Indications = inds
                        Generics = gens
                        Routes = rtes
                        Shapes = shps
                        DoseTypes = dsts
                        Diluents = dils
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
            pr.Patient |> create
            , [||]
        |> fun (pr, rules) ->
            let singleRule = rules |> Array.tryExactlyOne

            { pr with
                Filter =
                    { pr.Filter with
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
                                        pr.Filter.SelectedComponents |> Array.isEmpty ||
                                        pr.Filter.SelectedComponents |> Array.exists ((=) dl.Component)
                                    )
                                    |> Array.append [| r.DoseRule.DoseLimits[0] |]
                    }
                |]


module Api =

    open MathNet.Numerics
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib

    module Prescription = Order.Prescription


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
                    OrderScenario.create i pr ord
                    |> Some
                | _, Error (ord, prctx, errs) ->
                    errs
                    |> List.map string
                    |> String.concat "\n"
                    |> writeErrorMessage

                    ord
                    |> Order.toString
                    |> String.concat "\n"
                    |> writeWarningMessage

                    prctx
                    |> sprintf "%A"
                    |> writeWarningMessage

                    None
            )


    open Helpers


    /// <summary>
    /// Use a PrescriptionResult to create a new PrescriptionResult.
    /// </summary>
    let evaluate (pr : PrescriptionContext) =

        if Env.getItem "GENPRES_LOG" |> Option.map (fun s -> s = "1") |> Option.defaultValue false then
            let path = $"{__SOURCE_DIRECTORY__}/log.txt"
            OrderLogger.logger.Start (Some path) OrderLogger.Level.Informative

        let pr, rules = pr |> PrescriptionContext.getRules

        if rules |> Array.isEmpty then pr
        else
            { pr with
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
                    |> Array.distinctBy (fun pr ->
                        pr.DoseType,
                        pr.Preparation,
                        pr.Prescription,
                        pr.Administration
                    )
                    |> function
                        | prs when prs |> Array.length <= 1 -> prs
                        // filter out prescriptions without preparation when not needed
                        | prs ->
                            let grouped = prs |> Array.groupBy _.DoseType
                            [|
                                for _, prs in grouped do
                                    if prs |> Array.length <= 1 then prs
                                    else
                                        if prs
                                           |> Array.filter (fun pr ->
                                               pr.Preparation
                                               |> Array.exists (Array.exists String.notEmpty))
                                           |> Array.length = 0 then prs
                                        else
                                            prs
                                            |> Array.filter (fun pr ->
                                               pr.Preparation
                                               |> Array.exists (Array.exists String.notEmpty)
                                            )

                            |]
                            |> Array.collect id
            }


    let calcOrderValues (dto : Order.Dto.Dto) =
        try
            dto
            |> Order.Dto.fromDto
            |> fun ord ->
                if ord |> Order.isSolved then
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
            |> fun o ->
                o |> Order.toString |> List.iter (printfn "%s")
                o
            |> Ok
        with
        | e ->
            writeErrorMessage $"error calculating values from min incr max {e}"
            "error calculating values from min incr max"
            |> Error


    let solveOrder (dto : Order.Dto.Dto) =
        dto
        |> Order.Dto.fromDto
        |> Order.solveOrder false OrderLogger.logger.Logger


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