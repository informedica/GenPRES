namespace Informedica.GenOrder.Lib


module Api =

    open MathNet.Numerics
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib


    module Prescription = Order.Prescription


    let replace s =
        s
        |> String.replace "[" ""
        |> String.replace "]" ""
        |> String.replace "<" ""
        |> String.replace ">" ""


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


    /// <summary>
    /// Increase the Orderable Quantity and Rate Increment of an Order.
    /// This allows speedy calculation by avoiding large amount
    /// of possible values.
    /// </summary>
    /// <param name="logger">The OrderLogger to use</param>
    /// <param name="ord">The Order to increase the increment of</param>
    let increaseIncrements logger ord = Order.increaseIncrements logger 10N 50N ord


    let setNormDose logger normDose ord = Order.solveNormDose logger normDose ord


    let adjustRule rule =
        { rule with
            DoseRule =
                { rule.DoseRule with
                    Shape = rule.DoseRule.Generic
                    DoseLimits =
                        rule.DoseRule .DoseLimits
                        |> Array.filter DoseRule.DoseLimit.isSubstanceLimit
                        |> Array.map (fun dl ->
                            { dl with
                                Products =
                                    [|
                                        dl.DoseLimitTarget
                                        |> DoseRule.DoseLimit.substanceDoseLimitTargetToString
                                        |> Array.singleton
                                        |> Array.filter String.notEmpty
                                        |> Product.create
                                            rule.DoseRule.Generic
                                            rule.DoseRule.Route
                                    |]
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
    let evaluate logger (rule : PrescriptionRule) =
        let solve rule drugOrder =
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
        |> Array.map (solve rule)
        |> Array.filter Result.isOk


    /// <summary>
    /// Create an initial ScenarioResult for a Patient.
    /// </summary>
    let scenarioResult pat =
        let rules = pat |> PrescriptionRule.get
        {
            Indications = rules |> PrescriptionRule.indications
            Generics = rules |> PrescriptionRule.generics
            Routes = rules |> PrescriptionRule.routes
            Shapes= rules |> PrescriptionRule.shapes
            DoseTypes = rules |> PrescriptionRule.doseTypes
            Diluents =
                rules
                |> Array.collect _.SolutionRules
                |> Array.collect _.Diluents
                |> Array.map _.Generic
            Indication = None
            Generic = None
            Route = None
            Shape = None
            DoseType = None
            Diluent = None
            Patient = pat
            Scenarios = [||]
        }

    let evaluateRules rules =
        rules
        |> Array.map (fun pr ->
            async {
                return
                    pr
                    |> evaluate OrderLogger.logger.Logger
            }
        )
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Array.collect id
        |> Array.filter Result.isOk


    let createScenario i pr ord =
        let ns =
            pr.DoseRule.DoseLimits
            |> Array.groupBy _.Component// use only main component items
            |> Array.filter (fst >> String.isNullOrWhiteSpace >> not)

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

        {
            No = i
            Indication = pr.DoseRule.Indication
            DoseType = pr.DoseRule.DoseType
            Name = pr.DoseRule.Generic
            Substances = ns
                (*
                pr.DoseRule.DoseLimits
                // take only the substance dose limits from the principal component
                // TODO: need to refactor as this is done in multiple places
                |> Array.groupBy _.Component
                |> Array.filter (fst >> String.isNullOrWhiteSpace >> not)
                |> Array.map snd
                |> Array.tryHead
                |> Option.defaultValue [||]
                // now only main component substance dose limits are used
                |> Array.map _.DoseLimitTarget
                |> Array.filter LimitTarget.isSubstanceLimit
                |> Array.map LimitTarget.limitTargetToString
                *)
            Shape = pr.DoseRule.Shape
            Route = pr.DoseRule.Route
            Diluent =
                pr.SolutionRules
                |> Array.collect _.Diluents
                |> Array.map _.Generic
                |> Array.tryExactlyOne
            Prescription = prs |> Array.map (Array.map replace)
            Preparation = prp |> Array.map (Array.map replace)
            Administration = adm |> Array.map (Array.map replace)
            Order = Some ord
            UseAdjust = useAdjust
            UseRenalRule = pr.RenalRules |> Array.isEmpty |> not
            RenalRule = pr.DoseRule.RenalRule
        }


    let procesResults rs =
        rs
        |> Array.mapi (fun i r -> (i, r))
        |> Array.choose (function
            | i, Ok (ord, pr) ->
                createScenario i pr ord
                |> Some
            | _, Error (_, _, errs) ->
                errs
                |> List.map string
                |> String.concat "\n"
                |> fun s -> ConsoleWriter.writeErrorMessage s true false
                None
        )


    /// <summary>
    /// Use a Filter and a ScenarioResult to create a new ScenarioResult.
    /// </summary>
    let filter (sc : ScenarioResult) =

        if Env.getItem "GENPRES_LOG" |> Option.map (fun s -> s = "1") |> Option.defaultValue false then
            let path = $"{__SOURCE_DIRECTORY__}/log.txt"
            OrderLogger.logger.Start (Some path) OrderLogger.Level.Informative

        match sc.Patient.Weight, sc.Patient.Height, sc.Patient.Department with
        | Some w, Some h, d when d |> Option.isSome ->

            let ind =
                if sc.Indication.IsSome then sc.Indication
                else sc.Indications |> Array.someIfOne
            let gen =
                if sc.Generic.IsSome then sc.Generic
                else sc.Generics |> Array.someIfOne
            let rte =
                if sc.Route.IsSome then sc.Route
                else sc.Routes |> Array.someIfOne
            let shp =
                if sc.Shape.IsSome then sc.Shape
                else sc.Shapes |> Array.someIfOne
            let dst =
                if sc.DoseType.IsSome then sc.DoseType
                else sc.DoseTypes |> Array.someIfOne
            let dil =
                if sc.Diluent.IsSome then sc.Diluent
                else sc.Diluents |> Array.someIfOne

            let filter =
                {
                    Indication = ind
                    Generic = gen
                    Route = rte
                    Shape = shp
                    DoseType = dst
                    Diluent = dil
                    Patient = {
                        Department = d
                        Age = sc.Patient.Age
                        GestAge = sc.Patient.GestAge
                        PMAge = sc.Patient.PMAge
                        Weight = Some w
                        Height = Some h
                        Diagnoses = [||]
                        Gender = sc.Patient.Gender
                        Locations = sc.Patient.Locations
                        RenalFunction = sc.Patient.RenalFunction
                    }
                }

            let inds = filter |> filterIndications
            let gens = filter |> filterGenerics
            let rtes = filter |> filterRoutes
            let shps = filter |> filterShapes
            let dsts = filter |> filterDoseTypes
            let dils = filter |> filterDiluents

            let ind = inds |> Array.someIfOne
            let gen = gens |> Array.someIfOne
            let rte = rtes |> Array.someIfOne
            let shp = shps |> Array.someIfOne
            let dst = dsts |> Array.someIfOne
            let dil = dils |> Array.someIfOne

            { sc with
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
                Diluent = dil
                Scenarios =
                    match ind, gen, rte, shp, dst with
                    | Some _, Some _,    Some _, _, Some _
                    | Some _, Some _, _, Some _, Some _ ->
                        let rules =
                            { filter with
                                Indication = ind
                                Generic = gen
                                Route = rte
                                Shape = shp
                                DoseType = dst
                                Diluent = dil
                            }
                            |> PrescriptionRule.filter

                        rules
                        |> evaluateRules
                        |> function
                        | [||] ->
                            // no valid results so evaluate again
                            // with adjusted rules
                            rules
                            |> Array.map adjustRule
                            |> evaluateRules
                            |> procesResults
                        | results ->
                            results
                            |> procesResults
                        |> Array.distinctBy (fun pr ->
                            pr.DoseType,
                            pr.Preparation,
                            pr.Prescription,
                            pr.Administration
                        )
                        // filter out prescriptions without preparation when not needed
                        |> function
                            | prs when prs |> Array.length <= 1 -> prs
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
                    | _ -> [||]
            }
        | _ ->
            { sc with
                Indications = [||]
                Generics = [||]
                Routes = [||]
                Shapes = [||]
                DoseTypes = [||]
                Scenarios = [||]
            }


    let calc (dto : Order.Dto.Dto) =
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
                        ConsoleWriter.writeInfoMessage "== constraints reapplied" true false
                        o |> Order.toString |> List.iter (printfn "%s")
                        o
                    |> Order.solveMinMax false OrderLogger.logger.Logger
                    |> function
                    | Ok ord ->
                        ord
                        |> Order.minIncrMaxToValues OrderLogger.logger.Logger

                    | Error msgs ->
                        ConsoleWriter.writeErrorMessage $"{msgs}" true false
                        ord
                else
                    ord
                    |> Order.minIncrMaxToValues OrderLogger.logger.Logger
            |> Order.Dto.toDto
            |> Ok
        with
        | e ->
            ConsoleWriter.writeErrorMessage $"error calculating values from min incr max {e}"
                true false
            "error calculating values from min incr max"
            |> Error


    let solve (dto : Order.Dto.Dto) =
        dto
        |> Order.Dto.fromDto
        |> Order.solveOrder false OrderLogger.logger.Logger
        |> Result.map (fun o ->
            o
            |> Order.toString
            |> String.concat "\n"
            |> sprintf "solved order:\n%s"
            |> fun s -> ConsoleWriter.writeInfoMessage s true false

            o
        )
        |> Result.map Order.Dto.toDto


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