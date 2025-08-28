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

    module MinMax = MinMax
    module Limit = Limit


    let private tryHead m = (Array.map m) >> Array.tryHead >> Option.defaultValue ""


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
        /// Set constraints on a Variable dto based on norm values and MinMax record.
        /// A min or max value is set only if the MinMax record is not None or
        /// the sequence of big rationals has a single value. In that case the
        /// min or max value is set to the big rational minus or plus 10%.
        /// </summary>
        /// <param name="norm">A sequence of big rationals for normalization.</param>
        /// <param name="minMax">The MinMax record containing constraints.</param>
        /// <param name="dto">The Variable dto to apply constraints to.</param>
        let setConstraints
            (norm : ValueUnit option)
            (minMax : MinMax)
            (dto: Informedica.GenSolver.Lib.Variable.Dto.Dto) =

            let vuToDto = Option.bind (ValueUnit.Dto.toDto false "English")

            let limToVu = Option.map Limit.getValueUnit

            let times0_90 = 90N/100N |> ValueUnit.singleWithUnit Units.Count.times
            let times1_10 = 11N/10N |> ValueUnit.singleWithUnit Units.Count.times

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
            Dose = None
            Solution = None
        }


    /// Shorthand for Units.stringWithGroup to append the unit group to a unit.
    let unitGroup = Units.stringWithGroup


    /// <summary>
    /// Create a ProductComponent from a list of Products.
    /// DoseLimits are used to set the Dose for the ProductComponent.
    /// If noSubst is true, the substances will not be added to the ProductComponent.
    /// The freqUnit is used to set the TimeUnit for the Frequencies.
    /// </summary>
    /// <param name="solutionRule">The SolutionRule for the ProductComponent</param>
    /// <param name="limits">The ComponentLimits for the ProductComponent</param>
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
                    // Hack to prevent too many quantities
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


    /// Add an optional solution rule to a DrugOrder
    let addSolution sr dro =
        match sr with
        | None -> dro
        | Some sr ->
            { dro with
                Dose =
                    { DoseLimit.limit with
                        Rate = sr.DripRate
                        Quantity  = sr.Volume
                        QuantityAdjust = sr.VolumeAdjust
                        DoseUnit = Units.Volume.milliLiter
                    } |> Some
                Quantities =
                    if sr.Volumes.IsNone then dro.Quantities
                    else
                        sr.Volumes
                DoseCount =
                    // Change percentage to count!
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
                                GPKs = [| p.GPK |]
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


    /// Create a DrugOrder from patient information and dose rules
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
                if sr.IsSome then MinMax.empty // Note: dose count will be set in the addSolution
                else
                    // No solution rule, set it to 1
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


    module ToOrderDtoHelpers =
    
        let vuToDto = Option.bind (ValueUnit.Dto.toDto false ValueUnit.Dto.dutch)
        let limToDto = Option.map Limit.getValueUnit >> vuToDto
    
        /// Create the base Order DTO based on order type
        let createBaseOrderDto (d : DrugOrder) =
            match d.OrderType with
            | AnyOrder -> failwith "Not implemented yet, the order type cannot be 'Any'"
            | ProcessOrder -> failwith "Not implemented yet, the order type cannot be 'Process'"
            | OnceOrder -> Order.Dto.once d.Id d.Name d.Route []
            | OnceTimedOrder -> Order.Dto.onceTimed d.Id d.Name d.Route []
            | ContinuousOrder -> Order.Dto.continuous d.Id d.Name d.Route []
            | DiscontinuousOrder -> Order.Dto.discontinuous d.Id d.Name d.Route []
            | TimedOrder -> Order.Dto.timed d.Id d.Name d.Route []

        /// Calculate divisibility increment for a component
        let calculateDivisibility (p : ProductComponent) =
            p.Divisible
            |> Option.bind (fun d ->
                let ou = p.Quantities |> Option.map ValueUnit.getUnit |> Option.defaultValue NoUnit
                1N / d |> createSingleValueUnitDto ou
            )

        /// Apply solution constraints to a component
        let setComponentSolutionConstraints (cmpDto : Order.Orderable.Component.Dto.Dto) (sl : SolutionLimit) =
            cmpDto.OrderableQuantity.Constraints |> MinMax.setConstraints sl.Quantities sl.Quantity
            cmpDto.OrderableConcentration.Constraints |> MinMax.setConstraints None sl.Concentration

        /// Apply solution constraints to an item
        let setItemSolutionConstraints (itmDto : Order.Orderable.Item.Dto.Dto) (sl : SolutionLimit) =
            itmDto.OrderableQuantity.Constraints |> MinMax.setConstraints sl.Quantities sl.Quantity
            itmDto.OrderableConcentration.Constraints |> MinMax.setConstraints None sl.Concentration

        /// Set specific constraints for timed orders
        let setTimedOrderConstraints (orbDto : Order.Orderable.Dto.Dto) =
            // Assume timed order always solution
            if orbDto.Dose.Quantity.Constraints.ValsOpt.IsNone then
                orbDto.Dose.Quantity.Constraints.IncrOpt <- 
                    1N/10N |> createSingleValueUnitDto Units.Volume.milliLiter
        
            if orbDto.OrderableQuantity.Constraints.ValsOpt.IsNone then
                orbDto.OrderableQuantity.Constraints.IncrOpt <- 
                    1N/10N |> createSingleValueUnitDto Units.Volume.milliLiter

        /// Set basic item-level constraints
        let setItemQtyConcConstraints (itmDto : Order.Orderable.Item.Dto.Dto) (d : DrugOrder) (s : SubstanceItem) =
            itmDto.ComponentConcentration.Constraints.ValsOpt <- s.Concentrations |> vuToDto
        
            // Handle single component case
            if d.Components |> List.length = 1 then
                itmDto.OrderableConcentration.Constraints.ValsOpt <- itmDto.ComponentConcentration.Constraints.ValsOpt
        
            // Apply solution constraints if present
            s.Solution |> Option.iter (setItemSolutionConstraints itmDto)

        /// Set item dose constraints based on order type
        let setItemDoseConstraints (itmDto : Order.Orderable.Item.Dto.Dto) (d : DrugOrder) (s : SubstanceItem) =
            let setDoseRate (dl : DoseLimit) =
                itmDto.Dose.Rate.Constraints |> MinMax.setConstraints None dl.Rate
                itmDto.Dose.RateAdjust.Constraints |> MinMax.setConstraints None dl.RateAdjust
        
            let setDoseQty (dl : DoseLimit) =
                itmDto.Dose.Quantity.Constraints |> MinMax.setConstraints None dl.Quantity
                itmDto.Dose.QuantityAdjust.Constraints |> MinMax.setConstraints dl.NormQuantityAdjust dl.QuantityAdjust
                itmDto.Dose.PerTime.Constraints |> MinMax.setConstraints None dl.PerTime
                itmDto.Dose.PerTimeAdjust.Constraints |> MinMax.setConstraints dl.NormPerTimeAdjust dl.PerTimeAdjust
        
            match d.OrderType with
            | AnyOrder | ProcessOrder -> ()
            | ContinuousOrder -> s.Dose |> Option.iter setDoseRate
            | OnceOrder | DiscontinuousOrder -> s.Dose |> Option.iter setDoseQty
            | OnceTimedOrder | TimedOrder -> 
                s.Dose |> Option.iter (fun dl ->
                    setDoseRate dl
                    setDoseQty dl
                )

        /// Create a single item DTO with all its constraints
        let createSingleItemDto (d : DrugOrder) (p : ProductComponent) (s : SubstanceItem) =
            let itmDto = Order.Orderable.Item.Dto.dto d.Id d.Name p.Name s.Name
        
            // Set basic item constraints
            setItemQtyConcConstraints itmDto d s
        
            // Set item dose constraints based on order type
            setItemDoseConstraints itmDto d s
        
            itmDto

        /// Create item DTOs for a component
        let createItemDtos (d : DrugOrder) (p : ProductComponent) =
            [ for s in p.Substances -> createSingleItemDto d p s ]

        /// Set basic component-level constraints
        let setComponentQtyConcConstraints (cmpDto : Order.Orderable.Component.Dto.Dto) (d : DrugOrder) (p : ProductComponent) =
            let div = calculateDivisibility p
        
            cmpDto.ComponentQuantity.Constraints.ValsOpt <- p.Quantities |> vuToDto
            cmpDto.OrderableQuantity.Constraints.IncrOpt <- div
        
            // Handle single component case
            if d.Components |> List.length = 1 then
                cmpDto.OrderableConcentration.Constraints.ValsOpt <- 
                    1N |> createSingleValueUnitDto Units.Count.times
                cmpDto.Dose.Quantity.Constraints.IncrOpt <- div
        
            // Apply solution constraints if present
            p.Solution |> Option.iter (setComponentSolutionConstraints cmpDto)

        /// Set component dose constraints based on order type
        let setComponentDoseConstraints (cmpDto : Order.Orderable.Component.Dto.Dto) (d : DrugOrder) (p : ProductComponent) =
            let setDoseRate (dl : DoseLimit) =
                if dl.Rate |> MinMax.isEmpty |> not then
                    cmpDto.Dose.Rate.Constraints |> MinMax.setConstraints None dl.Rate
                if dl.RateAdjust |> MinMax.isEmpty |> not then
                    cmpDto.Dose.RateAdjust.Constraints |> MinMax.setConstraints None dl.RateAdjust
        
            let setDoseQty (dl : DoseLimit) =
                if dl.Quantity |> MinMax.isEmpty |> not then
                    cmpDto.Dose.Quantity.Constraints |> MinMax.setConstraints None dl.Quantity
                if dl.QuantityAdjust |> MinMax.isEmpty |> not || dl.NormQuantityAdjust |> Option.isSome then
                    cmpDto.Dose.QuantityAdjust.Constraints |> MinMax.setConstraints dl.NormQuantityAdjust dl.QuantityAdjust
                if dl.PerTime |> MinMax.isEmpty |> not then
                    cmpDto.Dose.PerTime.Constraints |> MinMax.setConstraints None dl.PerTime
                if dl.PerTimeAdjust |> MinMax.isEmpty |> not || dl.NormPerTimeAdjust |> Option.isSome then
                    cmpDto.Dose.PerTimeAdjust.Constraints |> MinMax.setConstraints dl.NormPerTimeAdjust dl.PerTimeAdjust
        
            match d.OrderType with
            | AnyOrder | ProcessOrder -> ()
            | ContinuousOrder -> p.Dose |> Option.iter setDoseRate
            | OnceOrder | DiscontinuousOrder -> p.Dose |> Option.iter setDoseQty
            | OnceTimedOrder | TimedOrder -> 
                p.Dose |> Option.iter (fun dl ->
                    setDoseRate dl
                    setDoseQty dl
                )

        /// Create a single component DTO with all its constraints and items
        let createSingleComponentDto (d : DrugOrder) (p : ProductComponent) =
            let cmpDto = Order.Orderable.Component.Dto.dto d.Id d.Name p.Name p.Shape
        
            // Set basic component constraints
            setComponentQtyConcConstraints cmpDto d p
        
            // Set component dose constraints based on order type
            setComponentDoseConstraints cmpDto d p
        
            // Create and set item DTOs
            cmpDto.Items <- createItemDtos d p
        
            cmpDto

        /// Create component DTOs from DrugOrder components
        let createComponentDtos (d : DrugOrder) =
            [ for p in d.Components -> createSingleComponentDto d p ]

        /// Set basic orderable-level constraints
        let setOrderableConstraints (orbDto : Order.Orderable.Dto.Dto) (d : DrugOrder) =
            let zero =
                d.Components
                |> List.tryHead
                |> Option.bind (fun p ->
                    p.Quantities 
                    |> Option.map ValueUnit.getUnit
                    |> Option.bind (fun u ->
                        0N |> createSingleValueUnitDto u
                    )
                )

            orbDto.DoseCount.Constraints |> MinMax.setConstraints None d.DoseCount

            match d.Quantities with
            | None ->
                orbDto.OrderableQuantity.Constraints.MinOpt <- zero
                orbDto.OrderableQuantity.Constraints.MinIncl <- false

            | Some _ ->
                orbDto.OrderableQuantity.Constraints.ValsOpt <- d.Quantities |> vuToDto

        /// Set dose constraints on orderable based on order type
        let setOrderableDoseConstraints (orbDto : Order.Orderable.Dto.Dto) (d : DrugOrder) =
            let orderableUnit =
                d.Components
                |> List.tryHead
                |> Option.bind (fun p ->
                    p.Quantities 
                    |> Option.map ValueUnit.getUnit
                )

            let rateUnit = orderableUnit |> Option.map (Units.per Units.Time.hour)

            let freqTimeUnit =
                d.Frequencies
                |> Option.map (ValueUnit.getUnit >> ValueUnit.getUnits)
                |> function
                | Some [ _; tu ] -> Some tu
                | _ -> None
        
            let setStandardDoseRate () =
                match rateUnit with
                | None -> ()
                | Some u ->
                    orbDto.Dose.Rate.Constraints.IncrOpt <- 1N/10N |> createSingleValueUnitDto u
                    orbDto.Dose.Rate.Constraints.MinIncl <- true
                    orbDto.Dose.Rate.Constraints.MinOpt <- 1N/10N |> createSingleValueUnitDto u
                    orbDto.Dose.Rate.Constraints.MaxIncl <- true
                    orbDto.Dose.Rate.Constraints.MaxOpt <- 1000N |> createSingleValueUnitDto u
        
            let setOrbDoseRate (dl : DoseLimit option) =
                match dl with
                | None -> ()
                | Some dl ->
                    orbDto.Dose.Rate.Constraints |> MinMax.setConstraints None dl.Rate
                    orbDto.Dose.RateAdjust.Constraints |> MinMax.setConstraints None dl.RateAdjust
        
            let setOrbDoseQty isOnce (dl : DoseLimit option) =
                match dl with
                | None -> 
                    match orderableUnit with
                    | Some u ->
                        orbDto.Dose.Quantity.Constraints.MinOpt <-
                            0N |> createSingleValueUnitDto u
                        orbDto.Dose.Quantity.Constraints.MinIncl <- false
                    | None -> ()

                    match orderableUnit, freqTimeUnit with
                    | Some u, Some tu ->
                        orbDto.Dose.PerTime.Constraints.MinOpt <-
                            0N |> createSingleValueUnitDto (u |> Units.per tu)
                        orbDto.Dose.PerTime.Constraints.MinIncl <- false
                    | _ -> ()

                | Some dl ->
                    orbDto.Dose.Quantity.Constraints |> MinMax.setConstraints None dl.Quantity
                    orbDto.Dose.QuantityAdjust.Constraints |> MinMax.setConstraints dl.NormQuantityAdjust dl.QuantityAdjust

                    // make sure that orderable dose quantity has constraints with a unit
                    if dl.Quantity |> MinMax.isEmpty then
                        match orderableUnit with
                        | Some u ->
                            orbDto.Dose.Quantity.Constraints.MinOpt <-
                                0N |> createSingleValueUnitDto u
                            orbDto.Dose.Quantity.Constraints.MinIncl <- false
                        | None -> ()
                
                    if not isOnce then
                        orbDto.Dose.PerTime.Constraints |> MinMax.setConstraints None dl.PerTime
                        // make sure that orderable dose per time has constraints with a unit
                        if dl.PerTime |> MinMax.isEmpty then
                            match orderableUnit, freqTimeUnit with
                            | Some u, Some tu ->
                                orbDto.Dose.PerTime.Constraints.MinOpt <-
                                    0N |> createSingleValueUnitDto (u |> Units.per tu)
                                orbDto.Dose.PerTime.Constraints.MinIncl <- false
                            | _ -> ()

                        orbDto.Dose.PerTimeAdjust.Constraints |> MinMax.setConstraints dl.NormPerTimeAdjust dl.PerTimeAdjust
        
            match d.OrderType with
            | AnyOrder | ProcessOrder -> ()
            | ContinuousOrder ->
                setStandardDoseRate()
                d.Dose |> setOrbDoseRate
            | OnceOrder ->
                d.Dose |> setOrbDoseQty true
            | OnceTimedOrder ->
                setStandardDoseRate()
                d.Dose |> setOrbDoseRate
                d.Dose |> setOrbDoseQty true
                // Assume timed order always solution
                orbDto.Dose.Quantity.Constraints.IncrOpt <- 
                    1N/10N |> createSingleValueUnitDto Units.Volume.milliLiter
            | DiscontinuousOrder ->
                d.Dose |> setOrbDoseQty false
            | TimedOrder ->
                setStandardDoseRate()
                setTimedOrderConstraints orbDto
                d.Dose |> setOrbDoseRate
                d.Dose |> setOrbDoseQty false

        /// Create and configure the Orderable DTO with all constraints
        let createOrderableDto (d : DrugOrder) =
            let orbDto = Order.Orderable.Dto.dto d.Id d.Name
        
            // Set basic orderable constraints
            setOrderableConstraints orbDto d
        
            // Set dose constraints based on order type
            setOrderableDoseConstraints orbDto d
        
            // Create and set component DTOs
            orbDto.Components <- createComponentDtos d
        
            orbDto

        /// Set prescription-level constraints (frequency and time)
        let setPrescriptionConstraints (dto : Order.Dto.Dto) (d : DrugOrder) =
            dto.Prescription.Frequency.Constraints.ValsOpt <- d.Frequencies |> vuToDto
            dto.Prescription.Time.Constraints.MinIncl <- d.Time.Min.IsSome
            dto.Prescription.Time.Constraints.MinOpt <- d.Time.Min |> limToDto
            dto.Prescription.Time.Constraints.MaxIncl <- d.Time.Max.IsSome
            dto.Prescription.Time.Constraints.MaxOpt <- d.Time.Max |> limToDto

        /// Set patient adjustment constraints (weight/BSA based)
        let setAdjustmentConstraints (dto : Order.Dto.Dto) (d : DrugOrder) =
            // Handle weight-based adjustment
            if d.AdjustUnit 
               |> Option.map (ValueUnit.Group.eqsGroup Units.Weight.kiloGram)
               |> Option.defaultValue false then
                dto.Adjust.Constraints.MinOpt <- (200N/1000N) |> createSingleValueUnitDto d.AdjustUnit.Value
                dto.Adjust.Constraints.MaxOpt <- 150N |> createSingleValueUnitDto d.AdjustUnit.Value
        
            // TODO: add constraints for BSA
            dto.Adjust.Constraints.ValsOpt <- d.Adjust |> vuToDto


    /// <summary>
    /// Convert a DrugOrder to an Order DTO for the solver system
    /// </summary>
    /// <param name="d">The DrugOrder to convert</param>
    let toOrderDto (d : DrugOrder) =
        // Create the base DTO structure
        let dto = ToOrderDtoHelpers.createBaseOrderDto d
        
        // Set up the orderable with all its constraints
        let orbDto = ToOrderDtoHelpers.createOrderableDto d
        dto.Orderable <- orbDto
        
        // Apply prescription constraints
        ToOrderDtoHelpers.setPrescriptionConstraints dto d
        
        // Apply patient adjustment constraints
        ToOrderDtoHelpers.setAdjustmentConstraints dto d
        
        dto