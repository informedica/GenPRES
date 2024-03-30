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
        let rec solve tryAgain sr pr =
            pr
            |> DrugOrder.createDrugOrder sr
            |> DrugOrder.toOrderDto
            |> Order.Dto.fromDto
            |> Order.solveMinMax false logger
            |> Result.bind (increaseIncrements logger)
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
                                        shapeQty =
                                            v.Value
                                            |> Array.map (fst >> BigRational.parse)
                                        substs =
                                            [
                                                for i in c.Items do
                                                    i.ComponentConcentration.Variable.ValsOpt
                                                    |> Option.map (fun v ->
                                                        {|
                                                            name = i.Name
                                                            qty =
                                                                v.Value
                                                                |> Array.map (fst >> BigRational.parse)
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
                    |> Array.collect (fun dto ->
                        dto.Value
                        |> Array.map (fst >> BigRational.parse)
                    )

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
                                |> Array.map (fst >> BigRational.parse)
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
            | Error (ord, _) when tryAgain &&
                                  ord.Prescription |> Prescription.isContinuous |> not
                            ->
                { pr with
                    DoseRule =
                        { pr.DoseRule with
                            Shape = pr.DoseRule.Generic
                            Products =
                                [|
                                    pr.DoseRule.DoseLimits
                                    |> Array.map _.DoseLimitTarget
                                    |> Array.map DoseRule.DoseLimit.substanceDoseLimitTargetToString
                                    |> Array.filter String.notEmpty
                                    |> Array.distinct
                                    |> Product.create
                                        pr.DoseRule.Generic
                                        pr.DoseRule.Route
                                |]
                            DoseLimits =
                                pr.DoseRule .DoseLimits
                                |> Array.filter DoseRule.DoseLimit.isSubstanceLimit
                        }
                }
                |> solve false None
            | Error (ord, m) ->
                Error (ord, pr, m)

        if rule.SolutionRules |> Array.isEmpty then [| solve true None rule |]
        else
            rule.SolutionRules
            |> Array.map (fun sr -> solve true (Some sr) rule)


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
            Indication = None
            Generic = None
            Route = None
            Shape = None
            DoseType = None
            Patient = pat
            Scenarios = [||]
        }


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

            let filter =
                {
                    Indication = ind
                    Generic = gen
                    Route = rte
                    Shape = shp
                    DoseType = dst
                    Patient = {
                        Department = d
                        Age = sc.Patient.Age
                        GestAge = sc.Patient.GestAge
                        PMAge = sc.Patient.PMAge
                        Weight = Some w
                        Height = Some h
                        Diagnoses = [||]
                        Gender = sc.Patient.Gender
                        VenousAccess = sc.Patient.VenousAccess
                    }
                }

            let inds = filter |> filterIndications
            let gens = filter |> filterGenerics
            let rtes = filter |> filterRoutes
            let shps = filter |> filterShapes
            let dsts = filter |> filterDoseTypes

            let ind = inds |> Array.someIfOne
            let gen = gens |> Array.someIfOne
            let rte = rtes |> Array.someIfOne
            let shp = shps |> Array.someIfOne
            let dst = dsts |> Array.someIfOne

            { sc with
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
                Scenarios =
                    match ind, gen, rte, shp, dst with
                    | Some _, Some _,    Some _, _, Some _
                    | Some _, Some _, _, Some _, Some _ ->
                        { filter with
                            Indication = ind
                            Generic = gen
                            Route = rte
                            Shape = shp
                            DoseType = dst
                        }
                        |> PrescriptionRule.filter
                        |> Array.map (fun pr ->
                            async {
                                return
                                    pr
                                    |> evaluate OrderLogger.logger.Logger
                                    |> Array.mapi (fun i r -> (i, r))
                                    |> Array.choose (function
                                        | i, Ok (ord, pr) ->
                                            let ns =
                                                pr.DoseRule.DoseLimits
                                                |> Array.choose (fun dl ->
                                                    match dl.DoseLimitTarget with
                                                    | SubstanceLimitTarget s -> Some s
                                                    | _ -> None
                                                )
                                                |> Array.distinct

                                            let useAdjust = pr.DoseRule |> DoseRule.useAdjust

                                            let prs, prp, adm =
                                                ord
                                                |> Order.Print.printOrderToMd useAdjust ns

                                            {
                                                No = i
                                                Indication = pr.DoseRule.Indication
                                                DoseType = pr.DoseRule.DoseType
                                                Name = pr.DoseRule.Generic
                                                Shape = pr.DoseRule.Shape
                                                Route = pr.DoseRule.Route
                                                Prescription = prs |> replace
                                                Preparation =prp |> replace
                                                Administration = adm |> replace
                                                Order = Some ord
                                                UseAdjust = useAdjust
                                            }
                                            |> Some

                                        | _, Error (_, _, errs) ->
                                            errs
                                            |> List.map string
                                            |> String.concat "\n"
                                            |> fun s -> ConsoleWriter.writeErrorMessage s true false
                                            None
                                    )
                            }
                        )
                        |> Async.Parallel
                        |> Async.RunSynchronously
                        |> Array.collect id
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
                                               |> Array.filter (fun pr -> pr.Preparation |> String.notEmpty)
                                               |> Array.length = 0 then prs
                                            else
                                                prs
                                                |> Array.filter (fun pr -> pr.Preparation |> String.notEmpty)

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
