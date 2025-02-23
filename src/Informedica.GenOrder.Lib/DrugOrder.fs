namespace Informedica.GenOrder.Lib



module DrugOrder =

    open System
    open Informedica.Utils.Lib
    open MathNet.Numerics
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenForm.Lib

    module DoseLimit = DoseRule.DoseLimit
    module MinMax = Informedica.GenCore.Lib.Ranges.MinMax
    module Limit = Informedica.GenCore.Lib.Ranges.Limit


    let private tryHead m = (Array.map m) >> Array.tryHead >> (Option.defaultValue "")



    /// <summary>
    /// Create a value unit dto from a string and a sequence of big rationals.
    /// </summary>
    /// <param name="u">The unit as a string.</param>
    /// <param name="brs">The big rationals as a sequence.</param>
    /// <remarks>
    /// If the unit is null or an empty string, the function returns None.
    /// </remarks>
    let createValueUnitDto u brs =
        if u = NoUnit then None
        else
            brs
            |> ValueUnit.withUnit u
            |> ValueUnit.Dto.toDto false "English"

    /// <summary>
    /// Create a single value unit dto from a string and a big rational.
    /// </summary>
    /// <param name="u">The unit as a string.</param>
    /// <param name="br">The big rational.</param>
    /// <remarks>
    /// If the unit is null or an empty string, the function returns None.
    /// </remarks>
    let createSingleValueUnitDto u br =
        createValueUnitDto u [| br |]


    module MinMax =

        /// <summary>
        /// </summary>
        /// <param name="norm">A sequence of big rationals.</param>
        /// <param name="minMax">The MinMax record.</param>
        /// <param name="dto">The Variable dto.</param>
        /// <remarks>
        /// A min or max value is set only if the MinMax record is not None or
        /// the sequence of big rationals has a single value. In that case the
        /// min or max value is set to the big rational minus or plus 10%.
        /// </remarks>
        let setConstraints
            (norm : ValueUnit option)
            (minMax : MinMax)
            (dto: Informedica.GenSolver.Lib.Variable.Dto.Dto) =

            let vuToDto = Option.bind (ValueUnit.Dto.toDto false "English")

            let limToVu = Option.map Limit.getValueUnit

            let times0_90 = (90N/100N) |> ValueUnit.singleWithUnit Units.Count.times
            let times1_10 = (11N/10N) |> ValueUnit.singleWithUnit Units.Count.times

            let min =
                match minMax.Min, norm with
                | None, Some norm -> norm * times0_90 |> Some
                | _  -> minMax.Min |> limToVu
                |> Option.bind ValueUnit.minValue
                |> vuToDto

            let max =
                match minMax.Max, norm with
                | None, Some norm -> norm * times1_10 |> Some
                | _  -> minMax.Max |> limToVu
                |> Option.bind ValueUnit.maxValue
                |> vuToDto

            match min with
            | None -> ()
            | Some _ ->
                dto.MinIncl <- true
                dto.MinOpt <- min
            match max with
            | None -> ()
            | Some _ ->
                dto.MaxIncl <- true
                dto.MaxOpt <- max

            //dto


    /// An empty DrugOrder record.
    let drugOrder =
        {
            Id = ""
            Name = ""
            Products = []
            Quantities = None
            Route = ""
            OrderType = AnyOrder
            AdjustUnit = None
            Frequencies = None
            Rates = None
            Time = MinMax.empty
            Dose = None
            DoseCount = None
            Adjust = None
        }


    /// An empty Product Component record.
    let productComponent =
        {
            Name = ""
            Shape = ""
            Quantities = None
            Divisible = None
            Solution = None
            Substances = []
        }


    /// An empty Substance Item record.
    let substanceItem =
        {
            Name = ""
            Concentrations = None
            Dose = None //DoseLimit.limit
            Solution = None
        }


    /// Shorthand for Units.stringWithGroup to
    /// append the unit group to a unit.
    let unitGroup = Units.stringWithGroup



    /// <summary>
    /// Create a ProductComponent from a list of Products.
    /// DoseLimits are used to set the Dose for the ProductComponent.
    /// If noSubst is true, the substances will not be added to the ProductComponent.
    /// The freqUnit is used to set the TimeUnit for the Frequencies.
    /// </summary>
    /// <param name="noSubst">Whether to add the substances to the ProductComponent</param>
    /// <param name="solutionRule">The SolutionRule for the ProductComponent</param>
    /// <param name="doseLimits">The DoseLimits for the ProductComponent</param>
    /// <param name="ps">The Products to create the ProductComponent from</param>
    let createProductComponent
        noSubst
        (solutionRule: SolutionRule option)
        (doseLimits : DoseLimit [])
        (ps : Product []) =

        let shape =
            ps
            |> tryHead _.Shape
            |> fun s ->
                if s |> String.isNullOrWhiteSpace then "oplosvloeistof"
                else s

        {
            Name =
                ps
                |> tryHead _.Generic
                |> fun s ->
                    if s |> String.isNullOrWhiteSpace then "oplosvloeistof"
                    else s
            Shape = shape
            Quantities =
                // hack to prevent too many quantities
                if solutionRule |> Option.isSome then None
                else
                    ps
                    |> Array.map _.ShapeQuantities
                    |> ValueUnit.collect
            Divisible =
                ps
                |> Array.choose _.Divisible
                |> Array.tryHead
            Solution =
                match solutionRule with
                | None -> None
                | Some r ->
                    r.SolutionLimits
                    |> Array.tryFind (fun sl ->
                        match sl.SolutionLimitTarget with
                        | ShapeLimitTarget s -> s |> String.equalsCapInsens shape
                        | _ -> false
                    )
            Substances =
                if noSubst then []
                else
                    ps
                    |> Array.collect _.Substances
                    |> Array.groupBy _.Name
                    |> Array.map (fun (n, xs) ->
                        {
                            Name = n
                            Concentrations =
                                xs
                                |> Array.choose _.Concentration
                                |> Array.distinct
                                |> ValueUnit.collect
                            Dose =
                                doseLimits
                                |> Array.tryFind (fun l ->
                                    match l.DoseLimitTarget with
                                    | SubstanceLimitTarget s ->
                                        s |> String.equalsCapInsens n
                                    | _ -> false
                                )
                            Solution = None
                        }
                    )
                    |> Array.toList
        }


    /// <summary>
    /// Set the SolutionLimits for a list of SubstanceItems.
    /// </summary>
    /// <param name="sls">The SolutionLimits to set</param>
    /// <param name="items">The SubstanceItems to set the SolutionLimits for</param>
    let setSolutionLimit (sls : SolutionLimit[]) (items : SubstanceItem list) =
        items
        |> List.map (fun item ->
            match sls |> Array.tryFind (fun sl ->
                match sl.SolutionLimitTarget with
                | SubstanceLimitTarget s -> s |> String.equalsCapInsens item.Name
                | _ -> false
            ) with
            | None -> item
            | Some sl ->
                { item with
                    Solution = Some sl
                }
        )

    let addSolution sr (parenteral : Product[]) dro =
        // add an optional solution rule
        match sr with
        | None -> dro
        | Some sr ->
            { dro with
                Dose =
                    { DoseRule.DoseLimit.limit with
                        Rate = sr.DripRate
                        Quantity  = sr.Volume
                        DoseUnit = Units.Volume.milliLiter
                    } |> Some
                Quantities = sr.Volumes
                DoseCount =
                    sr.DosePerc.Max
                    |> Option.map Limit.getValueUnit
                    |> Option.map (fun dc ->
                        (Units.Count.times |> ValueUnit.one) / dc
                    )

                Products =
                    let ps =
                        dro.Products
                        |> List.map (fun p ->
                            { p with
                                Name = dro.Name
                                Shape = p.Shape
                                Substances =
                                    p.Substances
                                    |> setSolutionLimit sr.SolutionLimits
                            }
                        )

                    sr.Diluents
                    |> Array.tryHead
                    |> function
                    | Some p ->
                        [|p|]
                        |> createProductComponent false None [||]
                        |> List.singleton
                        |> List.append ps
                    | None ->
                        ConsoleWriter.writeInfoMessage
                            $"No diluents available"
                            true false
                        ps
            }


    /// <summary>
    /// Create a DrugOrder from a PrescriptionRule and a SolutionRule.
    /// </summary>
    /// <param name="sr">The optional SolutionRule to use</param>
    /// <param name="pr">The PrescriptionRule to use</param>
    let createDrugOrder (sr: SolutionRule option) (pr : PrescriptionRule) =
        let parenteral = Product.Parenteral.get ()
            // adjust unit defaults to kg
        let au =
            pr.DoseRule.AdjustUnit
            |> Option.defaultValue Units.Weight.kiloGram

        let dose =
            pr.DoseRule.DoseLimits
            |> Array.filter DoseRule.DoseLimit.isShapeLimit
            |> Array.tryHead

        // if no subst, dose is based on shape
        let noSubst =
            pr.DoseRule.DoseLimits
            |> Array.filter DoseRule.DoseLimit.isSubstanceLimit
            |> Array.filter (fun d ->
                d.DoseUnit = NoUnit ||
                d.DoseUnit |> ValueUnit.Group.eqsGroup Units.Count.times
            )
            |> Array.isEmpty |> not

        let substLimits =
            pr.DoseRule.DoseLimits
            |> Array.filter DoseRule.DoseLimit.isSubstanceLimit

        { drugOrder with
            Id = Guid.NewGuid().ToString()
            Name = pr.DoseRule.Generic |> String.toLower
            Products =
                pr.DoseRule.Products
                |> createProductComponent noSubst sr substLimits
                |> List.singleton
            Quantities = None
            Frequencies = pr.DoseRule.Frequencies
            Time = pr.DoseRule.AdministrationTime
            Route = pr.DoseRule.Route
            DoseCount =
                if pr.SolutionRules |> Array.isEmpty |> not then None
                else
                    Units.Count.times
                    |> ValueUnit.singleWithValue 1N
                    |> Some
            OrderType =
                match pr.DoseRule.DoseType with
                | Continuous _ -> ContinuousOrder
                | OnceTimed _ -> OnceTimedOrder
                | Once _ -> OnceOrder
                | Discontinuous _ -> DiscontinuousOrder
                | Timed _ -> TimedOrder
                | NoDoseType -> AnyOrder
            Dose = dose
            Adjust =
                if au |> ValueUnit.Group.eqsGroup Units.Weight.kiloGram then
                    pr.Patient.Weight
                else pr.Patient |> Patient.calcBSA
            AdjustUnit = Some au
        }
        |> addSolution sr parenteral



    /// <summary>
    /// Map a DrugOrder record to a DrugOrderDto record.
    /// </summary>
    /// <remarks>
    /// The DrugOrder will mainly mapping the constraints of the DrugOrderDto.
    /// </remarks>
    let toOrderDto (d : DrugOrder) =
        let vuToDto = Option.bind (ValueUnit.Dto.toDto false "English")

        let limToDto = Option.map Limit.getValueUnit >> vuToDto

        let oru = Units.Volume.milliLiter |> Units.per Units.Time.hour
        // assumes the drugorder has products and these have quantities
        let ou =
            d.Products
            |> List.map (fun p ->
                p.Quantities
                |> Option.map (fun q -> q |> ValueUnit.getUnit)
            )
            |> List.choose id
            |> List.tryHead
            |> Option.defaultValue NoUnit

        let standDoseRate un (orbDto : Order.Orderable.Dto.Dto) =
            orbDto.Dose.Rate.Constraints.IncrOpt <- 1N/10N |> createSingleValueUnitDto un
            orbDto.Dose.Rate.Constraints.MinIncl <- true
            orbDto.Dose.Rate.Constraints.MinOpt <- 1N/10N |> createSingleValueUnitDto un
            orbDto.Dose.Rate.Constraints.MaxIncl <- true
            orbDto.Dose.Rate.Constraints.MaxOpt <- 1000N |> createSingleValueUnitDto un

        let orbDto = Order.Orderable.Dto.dto d.Id d.Name

        orbDto.DoseCount.Constraints.ValsOpt <-
            d.DoseCount
            |> vuToDto

        orbDto.OrderableQuantity.Constraints.ValsOpt <- d.Quantities |> vuToDto

        let setOrbDoseRate (dl : DoseLimit) =

            orbDto.Dose.Rate.Constraints
            |> MinMax.setConstraints
                None
                dl.Rate

            orbDto.Dose.RateAdjust.Constraints
            |> MinMax.setConstraints
                None
                dl.RateAdjust

        let setOrbDoseQty isOnce (dl : DoseLimit) =
            orbDto.Dose.Quantity.Constraints
            |> MinMax.setConstraints
                None
                dl.Quantity

            orbDto.Dose.QuantityAdjust.Constraints
            |> MinMax.setConstraints
                dl.NormQuantityAdjust
                dl.QuantityAdjust

            if not isOnce then
                orbDto.Dose.PerTime.Constraints
                |> MinMax.setConstraints
                    None
                    dl.PerTime

                orbDto.Dose.PerTimeAdjust.Constraints
                |> MinMax.setConstraints
                    dl.NormPerTimeAdjust
                    dl.PerTimeAdjust

        match d.OrderType with
        | AnyOrder
        | ProcessOrder -> ()

        | ContinuousOrder ->
            orbDto |> standDoseRate oru

            match d.Dose with
            | Some dl -> dl |> setOrbDoseRate
            | None -> ()

        | OnceOrder ->
            match d.Dose with
            | Some dl ->
                dl |> setOrbDoseQty true
            | None -> ()

        | OnceTimedOrder ->
            orbDto |> standDoseRate oru

            match d.Dose with
            | Some dl ->
                dl |> setOrbDoseRate
                dl |> setOrbDoseQty true
                // assume timed order always solution
                orbDto.Dose.Quantity.Constraints.IncrOpt <-
                    1N/10N
                    |> createSingleValueUnitDto
                        Units.Volume.milliLiter
            | None -> ()

        | DiscontinuousOrder ->
            match d.Dose with
            | Some dl ->
                dl |> setOrbDoseQty false
            | None -> ()

        | TimedOrder ->
            orbDto |> standDoseRate oru

            match d.Dose with
            | Some dl ->
                dl |> setOrbDoseRate
                dl |> setOrbDoseQty false
                // assume timed order always solution
                orbDto.Dose.Quantity.Constraints.IncrOpt <-
                    1N/10N
                    |> createSingleValueUnitDto
                        Units.Volume.milliLiter
            | None -> ()

        orbDto.Components <-
            [
                for p in d.Products do
                    let cdto = Order.Orderable.Component.Dto.dto d.Id d.Name p.Name p.Shape
                    let div =
                        p.Divisible
                        |> Option.bind (fun d ->
                            (1N / d)
                            |> createSingleValueUnitDto ou
                        )

                    cdto.ComponentQuantity.Constraints.ValsOpt <- p.Quantities |> vuToDto
                    cdto.OrderableQuantity.Constraints.IncrOpt <- div

                    if d.Products |> List.length = 1 then
                        // if there is only one product, the concentration of that product in the
                        // Orderable will be by definition be 1.
                        cdto.OrderableConcentration.Constraints.ValsOpt <-
                            1N
                            |> createSingleValueUnitDto Units.Count.times
                        cdto.Dose.Quantity.Constraints.IncrOpt <- div

                    orbDto.Dose.Quantity.Constraints.IncrOpt <- div

                    match p.Solution with
                    | Some sl ->
                        cdto.OrderableQuantity.Constraints
                        |> MinMax.setConstraints
                            sl.Quantities
                            sl.Quantity

                        cdto.OrderableConcentration.Constraints
                        |> MinMax.setConstraints
                            None
                            sl.Concentration
                    | None -> ()

                    cdto.Items <- [
                        for s in p.Substances do
                            let itmDto =
                                Order.Orderable.Item.Dto.dto d.Id d.Name p.Name s.Name

                            itmDto.ComponentConcentration.Constraints.ValsOpt <- s.Concentrations |> vuToDto
                            if d.Products |> List.length = 1 then
                                // when only one product, the orderable concentration is the same as the component concentration
                                itmDto.OrderableConcentration.Constraints.ValsOpt <- itmDto.ComponentConcentration.Constraints.ValsOpt

                            match s.Solution with
                            | Some sl ->
                                itmDto.OrderableQuantity.Constraints
                                |> MinMax.setConstraints
                                    sl.Quantities
                                    sl.Quantity

                                itmDto.OrderableConcentration.Constraints
                                |> MinMax.setConstraints
                                    None
                                    sl.Concentration
                            | None -> ()

                            let setDoseRate (dl : DoseLimit) =

                                itmDto.Dose.Rate.Constraints
                                |> MinMax.setConstraints
                                       None
                                       dl.Rate

                                itmDto.Dose.RateAdjust.Constraints
                                |> MinMax.setConstraints
                                       None
                                       dl.RateAdjust

                            let setDoseQty (dl : DoseLimit) =
                                    itmDto.Dose.Quantity.Constraints
                                    |> MinMax.setConstraints
                                           None
                                           dl.Quantity

                                    itmDto.Dose.QuantityAdjust.Constraints
                                    |> MinMax.setConstraints
                                           dl.NormQuantityAdjust
                                           dl.QuantityAdjust

                                    itmDto.Dose.PerTime.Constraints
                                    |> MinMax.setConstraints
                                           None
                                           dl.PerTime

                                    itmDto.Dose.PerTimeAdjust.Constraints
                                    |> MinMax.setConstraints
                                           dl.NormPerTimeAdjust
                                           dl.PerTimeAdjust


                            match d.OrderType with
                            | AnyOrder -> ()
                            | ProcessOrder -> ()
                            | ContinuousOrder ->
                                match s.Dose with
                                | None    -> ()
                                | Some dl -> dl |> setDoseRate

                            | OnceOrder
                            | DiscontinuousOrder ->
                                match s.Dose with
                                | None -> ()
                                | Some dl -> dl |> setDoseQty

                            | OnceTimedOrder
                            | TimedOrder ->
                                match s.Dose with
                                | None -> ()
                                | Some dl ->
                                    dl |> setDoseRate
                                    dl |> setDoseQty
                            itmDto
                    ]

                    cdto
            ]

        let dto =
            match d.OrderType with
            | AnyOrder ->
                "the order type cannot by 'Any'"
                |> failwith
            | ProcessOrder ->
                "the order type cannot by 'Any'"
                |> failwith
            | OnceOrder ->
                Order.Dto.once d.Id d.Name d.Route []
            | OnceTimedOrder ->
                Order.Dto.onceTimed d.Id d.Name d.Route []
            | ContinuousOrder ->
                Order.Dto.continuous d.Id d.Name d.Route []
            | DiscontinuousOrder ->
                Order.Dto.discontinuous d.Id d.Name d.Route []
            | TimedOrder ->
                Order.Dto.timed d.Id d.Name d.Route []

        dto.Orderable <- orbDto

        dto.Prescription.Frequency.Constraints.ValsOpt <- d.Frequencies |> vuToDto

        dto.Prescription.Time.Constraints.MinIncl <- d.Time.Min.IsSome
        dto.Prescription.Time.Constraints.MinOpt <- d.Time.Min |> limToDto
        dto.Prescription.Time.Constraints.MaxIncl <- d.Time.Max.IsSome
        dto.Prescription.Time.Constraints.MaxOpt <- d.Time.Max |> limToDto

        if d.AdjustUnit
           |> Option.map (ValueUnit.Group.eqsGroup Units.Weight.kiloGram)
           |> Option.defaultValue false then
            dto.Adjust.Constraints.MinOpt <-
                (200N /1000N) |> createSingleValueUnitDto d.AdjustUnit.Value

            dto.Adjust.Constraints.MaxOpt <-
                150N |> createSingleValueUnitDto d.AdjustUnit.Value

        dto.Adjust.Constraints.ValsOpt <- d.Adjust |> vuToDto

        dto


