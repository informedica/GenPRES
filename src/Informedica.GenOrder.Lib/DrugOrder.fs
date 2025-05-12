namespace Informedica.GenOrder.Lib


module DrugOrder =

    open System
    open Informedica.Utils.Lib
    open MathNet.Numerics
    open Informedica.Utils.Lib.BCL
    open ConsoleWriter.NewLineNoTime
    open Informedica.GenUnits.Lib
    open Informedica.GenForm.Lib
    open Informedica.GenCore.Lib.Ranges

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
            Components = []
            Quantities = None
            Route = ""
            OrderType = AnyOrder
            AdjustUnit = None
            Frequencies = None
            Rates = None
            Time = MinMax.empty
            Dose = None
            DoseCount = MinMax.empty
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
            Dose = None
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
    /// <param name="solutionRule">The SolutionRule for the ProductComponent</param>
    /// <param name="doseLimits">The DoseLimits for the ProductComponent</param>
    let createComponents
        (solutionRule: SolutionRule option)
        (limits : ComponentLimit []) =

        limits
        |> Array.map (fun lim ->
            let shape =
                lim.Products
                |> tryHead _.Shape
                |> fun s ->
                    if s |> String.isNullOrWhiteSpace then "oplosvloeistof"
                    else s

            {
                Name =
                    if lim.Name |> String.isNullOrWhiteSpace then "oplosvloeistof"
                    else lim.Name
                Shape = shape
                Quantities =
                    // hack to prevent too many quantities
                    if solutionRule |> Option.isSome then
                        1N
                        |> ValueUnit.singleWithUnit Units.Volume.milliLiter
                        |> Some
                    else
                        lim.Products
                        |> Array.map _.ShapeQuantities
                        |> ValueUnit.collect
                Divisible =
                    lim.Products
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
                Dose = lim.Limit
                Substances =
                    lim.Products
                    |> Array.collect _.Substances
                    |> Array.groupBy _.Name
                    |> Array.map (fun (n, xs) ->
                        let dl =
                            lim.SubstanceLimits
                            |> Array.tryFind (fun l ->
                                match l.DoseLimitTarget with
                                | SubstanceLimitTarget s ->
                                    s |> String.equalsCapInsens n
                                | _ -> false
                            )

                        {
                            Name = n
                            Concentrations =
                                match dl with
                                | Some dl when dl.DoseUnit |> ValueUnit.Group.eqsGroup Units.Molar.mole ->
                                    xs
                                    |> Array.choose _.MolarConcentration
                                    |> Array.distinct
                                    |> ValueUnit.collect
                                | _ ->
                                    xs
                                    |> Array.choose _.Concentration
                                    |> Array.distinct
                                    |> ValueUnit.collect
                            Dose = dl
                            Solution = None
                        }
                    )
                    |> Array.toList            }
        )
        |> Array.toList


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


    let addSolution sr dro =
        // add an optional solution rule
        match sr with
        | None -> dro
        | Some sr ->
            { dro with
                Dose =
                    { DoseLimit.limit with
                        Rate = sr.DripRate
                        Quantity  = sr.Volume
                        DoseUnit = Units.Volume.milliLiter
                    } |> Some
                Quantities =
                    if sr.Volumes.IsNone then dro.Quantities
                    else
                        sr.Volumes
                DoseCount = //sr.DosePerc
                    // change percentage to count!
                    { MinMax.empty with
                        Min = sr.DosePerc.Max
                        Max = sr.DosePerc.Min
                    }
                Components =
                    let ps =
                        dro.Components
                        |> List.map (fun p ->
                            { p with
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
                        [|
                            {
                                Name = p.Generic
                                Limit = None
                                Products = [| p |]
                                SubstanceLimits = [||]
                            }
                        |]
                        |> createComponents None
                        |> List.append ps
                    | None ->
                        writeWarningMessage "No diluents available"
                        ps
            }


    let create (pat : Patient) au dose (dr : DoseRule) (sr: SolutionRule option) =
        { drugOrder with
            Id = Guid.NewGuid().ToString()
            Name = dr.Generic |> String.toLower
            Components =
                dr.ComponentLimits
                |> createComponents sr
            Quantities = None
            Frequencies = dr.Frequencies
            Time = dr.AdministrationTime
            Route = dr.Route
            DoseCount =
                if sr.IsSome then MinMax.empty // note: dose count will be set in the addSolution
                else
                    // no solution rule, set it to 1
                    let u = Units.Count.times |> Some
                    MinMax.fromTuple Inclusive Inclusive u (Some 1N, Some 1N)

            OrderType =
                match dr.DoseType with
                | Continuous _ -> ContinuousOrder
                | OnceTimed _ -> OnceTimedOrder
                | Once _ -> OnceOrder
                | Discontinuous _ -> DiscontinuousOrder
                | Timed _ -> TimedOrder
                | NoDoseType -> AnyOrder
            Dose = dose
            Adjust =
                if au |> ValueUnit.Group.eqsGroup Units.Weight.kiloGram then
                    pat.Weight
                else pat |> Patient.calcBSA
            AdjustUnit = Some au
        }
        |> addSolution sr


    /// <summary>
    /// Create DrugOrders from a PrescriptionRule
    /// </summary>
    /// <param name="pr">The PrescriptionRule to use</param>
    let fromRule (pr : PrescriptionRule) =
        let au =
            pr.DoseRule.AdjustUnit
            |> Option.defaultValue Units.Weight.kiloGram

        let dose = pr.DoseRule.ShapeLimit

        let create = create pr.Patient au dose pr.DoseRule

        if pr.SolutionRules |> Array.isEmpty then [| create  None |]
        else
            pr.SolutionRules
            |> Array.map Some
            |> Array.map create



    /// <summary>
    /// Map a DrugOrder record to a DrugOrderDto record.
    /// </summary>
    /// <remarks>
    /// The DrugOrder will map the constraints of the DrugOrderDto.
    /// </remarks>
    let toOrderDto (d : DrugOrder) =
        let vuToDto = Option.bind (ValueUnit.Dto.toDto false "English")

        let limToDto = Option.map Limit.getValueUnit >> vuToDto

        let oru = Units.Volume.milliLiter |> Units.per Units.Time.hour
        // assumes the drugorder has products and these have quantities
        let ou =
            d.Components
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

        orbDto.DoseCount.Constraints
        |>  MinMax.setConstraints None d.DoseCount

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
            // assume timed order always solution
            if orbDto.Dose.Quantity.Constraints.ValsOpt.IsNone then
                orbDto.Dose.Quantity.Constraints.IncrOpt <-
                    1N/10N
                    |> createSingleValueUnitDto
                        Units.Volume.milliLiter
            if orbDto.OrderableQuantity.Constraints.ValsOpt.IsNone then
                orbDto.OrderableQuantity.Constraints.IncrOpt <-
                    1N/10N
                    |> createSingleValueUnitDto
                        Units.Volume.milliLiter

            match d.Dose with
            | Some dl ->
                dl |> setOrbDoseRate
                dl |> setOrbDoseQty false
            | None -> ()

        orbDto.Components <-
            [
                for p in d.Components do
                    let cmpDto = Order.Orderable.Component.Dto.dto d.Id d.Name p.Name p.Shape
                    let div =
                        p.Divisible
                        |> Option.bind (fun d ->
                            (1N / d)
                            |> createSingleValueUnitDto ou
                        )

                    cmpDto.ComponentQuantity.Constraints.ValsOpt <- p.Quantities |> vuToDto
                    cmpDto.OrderableQuantity.Constraints.IncrOpt <- div

                    if d.Components |> List.length = 1 then
                        // if there is only one product, the concentration of that product in the
                        // Orderable will be by definition be 1.
                        cmpDto.OrderableConcentration.Constraints.ValsOpt <-
                            1N
                            |> createSingleValueUnitDto Units.Count.times
                        cmpDto.Dose.Quantity.Constraints.IncrOpt <- div

                    // TODO: not good, can vary per product!!
                    orbDto.Dose.Quantity.Constraints.IncrOpt <- div

                    match p.Solution with
                    | Some sl ->
                        cmpDto.OrderableQuantity.Constraints
                        |> MinMax.setConstraints
                            sl.Quantities
                            sl.Quantity

                        cmpDto.OrderableConcentration.Constraints
                        |> MinMax.setConstraints
                            None
                            sl.Concentration
                    | None -> ()

                    let setDoseRate (dl : DoseLimit) =
                        if dl.Rate |> MinMax.isEmpty |> not then
                            cmpDto.Dose.Rate.Constraints
                            |> MinMax.setConstraints
                                   None
                                   dl.Rate

                        if dl.RateAdjust |> MinMax.isEmpty |> not then
                            cmpDto.Dose.RateAdjust.Constraints
                            |> MinMax.setConstraints
                                   None
                                   dl.RateAdjust

                    let setDoseQty (dl : DoseLimit) =
                        if dl.Quantity |> MinMax.isEmpty |> not then
                            cmpDto.Dose.Quantity.Constraints
                            |> MinMax.setConstraints
                                   None
                                   dl.Quantity
                        if dl.QuantityAdjust |> MinMax.isEmpty |> not ||
                           dl.NormQuantityAdjust |> Option.isSome then
                            cmpDto.Dose.QuantityAdjust.Constraints
                            |> MinMax.setConstraints
                                   dl.NormQuantityAdjust
                                   dl.QuantityAdjust

                        if dl.PerTime |> MinMax.isEmpty |> not then
                            cmpDto.Dose.PerTime.Constraints
                            |> MinMax.setConstraints
                                   None
                                   dl.PerTime

                        if dl.PerTimeAdjust |> MinMax.isEmpty |> not ||
                           dl.NormPerTimeAdjust |> Option.isSome then
                            cmpDto.Dose.PerTimeAdjust.Constraints
                            |> MinMax.setConstraints
                                   dl.NormPerTimeAdjust
                                   dl.PerTimeAdjust


                    match d.OrderType with
                    | AnyOrder -> ()
                    | ProcessOrder -> ()
                    | ContinuousOrder ->
                        match p.Dose with
                        | None    -> ()
                        | Some dl -> dl |> setDoseRate

                    | OnceOrder
                    | DiscontinuousOrder ->
                        match p.Dose with
                        | None -> ()
                        | Some dl -> dl |> setDoseQty

                    | OnceTimedOrder
                    | TimedOrder ->
                        match p.Dose with
                        | None -> ()
                        | Some dl ->
                            dl |> setDoseRate
                            dl |> setDoseQty

                    cmpDto.Items <- [
                        for s in p.Substances do
                            let itmDto =
                                Order.Orderable.Item.Dto.dto d.Id d.Name p.Name s.Name

                            itmDto.ComponentConcentration.Constraints.ValsOpt <- s.Concentrations |> vuToDto
                            if d.Components |> List.length = 1 then
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

                    cmpDto
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
            // adjusted by weight
            dto.Adjust.Constraints.MinOpt <-
                (200N /1000N) |> createSingleValueUnitDto d.AdjustUnit.Value

            dto.Adjust.Constraints.MaxOpt <-
                150N |> createSingleValueUnitDto d.AdjustUnit.Value
        // TODO add constraints for BSA
        dto.Adjust.Constraints.ValsOpt <- d.Adjust |> vuToDto

        dto