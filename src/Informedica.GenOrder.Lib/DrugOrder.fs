namespace Informedica.GenOrder.Lib



module DrugOrder =

    open System
    open MathNet.Numerics
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenForm.Lib

//    type MinMax = Informedica.GenForm.Lib.Types.MinMax

//    module DoseRule = Informedica.GenForm.Lib.DoseRule
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
    /// If the unit is null or an empty empty string, the function returns None.
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
    /// If the unit is null or an empty empty string, the function returns None.
    /// </remarks>
    let createSingleValueUnitDto u br =
        createValueUnitDto u [| br |]


    module MinMax =

        /// <summary>
        /// Set the min and max values of a Variable dto using a MinMax record or
        /// a single big rational. If the MinMax record is None, and there is a
        /// single big rational, the function will use the big rational to set
        /// the min and max values.
        /// </summary>
        /// <param name="un">The unit as a string.</param>
        /// <param name="brs">A sequence of big rationals.</param>
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

            let times0_95 = (95N/100N) |> ValueUnit.singleWithUnit Units.Count.times
            let times1_10 = (11N/10N) |> ValueUnit.singleWithUnit Units.Count.times

            let min =
                match minMax.Min, norm with
                | None, Some norm -> norm * times0_95 |> Some
                | _  -> minMax.Min |> limToVu
                |> vuToDto

            let max =
                match minMax.Max, norm with
                | None, Some norm -> norm * times1_10 |> Some
                | _  -> minMax.Max |> limToVu
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

            dto


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


    /// Short hand for Units.stringWithGroup to
    /// append the unit group to a unit.
    let unitGroup = Units.stringWithGroup



    /// <summary>
    /// Create a ProductComponent from a list of Products.
    /// DoseLimits are used to set the Dose for the ProductComponent.
    /// If noSubst is true, the substances will not be added to the ProductComponent.
    /// The freqUnit is used to set the TimeUnit for the Frequencies.
    /// </summary>
    /// <param name="noSubst">Whether or not to add the substances to the ProductComponent</param>
    /// <param name="freqUnit">The TimeUnit for the Frequencies</param>
    /// <param name="doseLimits">The DoseLimits for the ProductComponent</param>
    /// <param name="ps">The Products to create the ProductComponent from</param>
    let createProductComponent noSubst (doseLimits : DoseLimit []) (ps : Product []) =
        let qt =
            ps
            |> Array.map (fun p ->
                p.ShapeQuantities
            )
            |> ValueUnit.collect


        { productComponent with
            Name =
                ps
                |> tryHead (fun p -> p.Shape)
                |> fun s ->
                    if s |> String.isNullOrWhiteSpace then "oplosvloeistof"
                    else s
            Shape =
                ps
                |> tryHead (fun p -> p.Shape)
                |> fun s ->
                    if s |> String.isNullOrWhiteSpace then "oplosvloeistof"
                    else s
            Quantities = qt
            Divisible =
                ps
                |> Array.choose (fun p -> p.Divisible)
                |> Array.tryHead
            Substances =
                if noSubst then []
                else
                    ps
                    |> Array.collect (fun p -> p.Substances)
                    |> Array.groupBy (fun s -> s.Name)
                    |> Array.map (fun (n, xs) ->
                        {
                            Name = n
                            Concentrations =
                                match qt with
                                | None -> None
                                | Some qt ->
                                    xs
                                    |> Array.choose (fun s -> s.Quantity)
                                    |> Array.distinct
                                    |> ValueUnit.collect
                                    |> Option.map (fun vu -> vu / qt)
                            Dose =
                                doseLimits
                                |> Array.tryFind (fun l ->
                                    match l.DoseLimitTarget with
                                    | SubstanceDoseLimitTarget s ->
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
            match sls |> Array.tryFind (fun sl -> sl.Substance |> String.equalsCapInsens item.Name) with
            | None -> item
            | Some sl ->
                { item with
                    Solution = Some sl
                }
        )


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
            Id = "1" //Guid.NewGuid().ToString()
            Name = pr.DoseRule.Generic
            Products =
                pr.DoseRule.Products
                |> createProductComponent noSubst substLimits
                |> List.singleton
            Quantities = None
            Frequencies = pr.DoseRule.Frequencies
            Time = pr.DoseRule.AdministrationTime
            Route = pr.DoseRule.Route
            DoseCount =
                if pr.SolutionRules |> Array.isEmpty |> not then None
                else
                    Units.Count.times
                    |> ValueUnit.withSingleValue 1N
                    |> Some
            OrderType =
                match pr.DoseRule.DoseType with
                | Informedica.GenForm.Lib.Types.Continuous -> ContinuousOrder
                | _ when pr.DoseRule.AdministrationTime = MinMax.empty -> DiscontinuousOrder
                | _ -> TimedOrder
            Dose = dose
            Adjust =
                if au |> ValueUnit.Group.eqsGroup Units.Weight.kiloGram then
                    pr.Patient.Weight
                else pr.Patient |> Patient.calcBSA
            AdjustUnit = Some au
        }
        |> fun dro ->
                // add an optional solution rule
                match sr with
                | None -> dro
                | Some sr ->
                    { dro with
                        Dose =
                            { DoseRule.DoseLimit.limit with
                                Quantity  = sr.Volume
                                DoseUnit = Units.Volume.milliLiter
                            } |> Some
                        Quantities = sr.Volumes
                        DoseCount =
                            sr.DosePerc.Max
                            |> Option.map Limit.getValueUnit
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

                            let s =
                                // ugly hack to get default solution
                                sr.Solutions
                                |> Array.tryHead
                                |> Option.defaultValue "x"

                            parenteral
                            |> Array.tryFind (fun p ->
                                    s |> String.notEmpty &&
                                    p.Generic |> String.startsWith s
                                )
                            |> function
                            | Some p ->
                                [|p|]
                                |> createProductComponent true  [||]
                                |> List.singleton
                                |> List.append ps
                            | None ->
                                printfn $"couldn't find {s} in parenterals"
                                ps
                    }



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
            |> List.head

        let standDoseRate un (orbDto : Order.Orderable.Dto.Dto) =
            orbDto.Dose.Rate.Constraints.IncrOpt <- 1N/10N |> createSingleValueUnitDto un
            orbDto.Dose.Rate.Constraints.MinIncl <- true
            orbDto.Dose.Rate.Constraints.MinOpt <- 1N/10N |> createSingleValueUnitDto un
            orbDto.Dose.Rate.Constraints.MaxIncl <- true
            orbDto.Dose.Rate.Constraints.MaxOpt <- 1000N |> createSingleValueUnitDto un

        let orbDto = Order.Orderable.Dto.dto d.Id d.Name

        orbDto.DoseCount.Constraints.ValsOpt <- d.DoseCount |> vuToDto

        orbDto.OrderableQuantity.Constraints.ValsOpt <- d.Quantities |> vuToDto

        let setOrbDoseRate (dl : DoseLimit) =
            orbDto.Dose.Rate.Constraints.MinIncl <- dl.Rate.Min.IsSome
            orbDto.Dose.Rate.Constraints.MinOpt <- dl.Rate.Min |> limToDto
            orbDto.Dose.Rate.Constraints.MinIncl <- dl.Rate.Max.IsSome
            orbDto.Dose.Rate.Constraints.MinOpt <- dl.Rate.Max |> limToDto

            orbDto.Dose.RateAdjust.Constraints.MinIncl <- dl.RateAdjust.Min.IsSome
            orbDto.Dose.RateAdjust.Constraints.MinOpt <- dl.RateAdjust.Min |> limToDto
            orbDto.Dose.RateAdjust.Constraints.MinIncl <- dl.RateAdjust.Max.IsSome
            orbDto.Dose.RateAdjust.Constraints.MinOpt <- dl.RateAdjust.Max |> limToDto

        let setOrbDoseQty (dl : DoseLimit) =
            orbDto.Dose.Quantity.Constraints.MinIncl <- dl.Quantity.Min.IsSome
            orbDto.Dose.Quantity.Constraints.MinOpt <- dl.Quantity.Min |> limToDto
            orbDto.Dose.Quantity.Constraints.MaxIncl <- dl.Quantity.Max.IsSome
            orbDto.Dose.Quantity.Constraints.MaxOpt <- dl.Quantity.Max |> limToDto

            orbDto.Dose.QuantityAdjust.Constraints.MinIncl <- dl.QuantityAdjust.Min.IsSome
            orbDto.Dose.QuantityAdjust.Constraints.MinOpt <- dl.QuantityAdjust.Min |> limToDto
            orbDto.Dose.QuantityAdjust.Constraints.MaxIncl <- dl.QuantityAdjust.Max.IsSome
            orbDto.Dose.QuantityAdjust.Constraints.MaxOpt <- dl.QuantityAdjust.Max |> limToDto

            orbDto.Dose.PerTime.Constraints.MinIncl <- dl.PerTime.Min.IsSome
            orbDto.Dose.PerTime.Constraints.MinOpt <- dl.PerTime.Min |> limToDto
            orbDto.Dose.PerTime.Constraints.MaxIncl <- dl.PerTime.Max.IsSome
            orbDto.Dose.PerTime.Constraints.MaxOpt <- dl.PerTime.Max |> limToDto

            orbDto.Dose.PerTimeAdjust.Constraints.MinIncl <- dl.PerTimeAdjust.Min.IsSome
            orbDto.Dose.PerTimeAdjust.Constraints.MinOpt <- dl.PerTimeAdjust.Min |> limToDto
            orbDto.Dose.PerTimeAdjust.Constraints.MaxIncl <- dl.PerTimeAdjust.Max.IsSome
            orbDto.Dose.PerTimeAdjust.Constraints.MaxOpt <- dl.PerTimeAdjust.Max |> limToDto

        match d.OrderType with
        | AnyOrder
        | ProcessOrder -> ()

        | ContinuousOrder ->
            orbDto |> standDoseRate oru

            match d.Dose with
            | Some dl -> dl |> setOrbDoseRate
            | None -> ()

        | DiscontinuousOrder ->
            match d.Dose with
            | Some dl -> dl |> setOrbDoseQty
            | None -> ()

        | TimedOrder ->
            orbDto |> standDoseRate oru

            match d.Dose with
            | Some dl ->
                dl |> setOrbDoseRate
                dl |> setOrbDoseQty
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

                        orbDto.Dose.Quantity.Constraints.IncrOpt <- div
                        cdto.Dose.Quantity.Constraints.IncrOpt <- div

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
                                itmDto.OrderableQuantity.Constraints.MinIncl <- sl.Quantity.Min.IsSome
                                itmDto.OrderableQuantity.Constraints.MinOpt <- sl.Quantity.Min |> limToDto
                                itmDto.OrderableQuantity.Constraints.MaxIncl <- sl.Quantity.Max.IsSome
                                itmDto.OrderableQuantity.Constraints.MaxOpt <- sl.Quantity.Max |> limToDto
                                itmDto.OrderableConcentration.Constraints.MinIncl <- sl.Concentration.Min.IsSome
                                itmDto.OrderableConcentration.Constraints.MinOpt <- sl.Concentration.Min |> limToDto
                                itmDto.OrderableConcentration.Constraints.MaxIncl <- sl.Concentration.Max.IsSome
                                itmDto.OrderableConcentration.Constraints.MaxOpt <- sl.Concentration.Max |> limToDto
                            | None -> ()

                            let setDoseRate (dl : DoseLimit) =

                                itmDto.Dose.Rate.Constraints <-
                                    itmDto.Dose.Rate.Constraints
                                    |> MinMax.setConstraints
                                           None
                                           dl.Rate

                                itmDto.Dose.RateAdjust.Constraints <-
                                    itmDto.Dose.RateAdjust.Constraints
                                    |> MinMax.setConstraints None dl.RateAdjust

                            let setDoseQty (dl : DoseLimit) =
                                    itmDto.Dose.Quantity.Constraints <-
                                        itmDto.Dose.Quantity.Constraints
                                        |> MinMax.setConstraints
                                               None
                                               dl.Quantity

                                    itmDto.Dose.QuantityAdjust.Constraints <-
                                        itmDto.Dose.QuantityAdjust.Constraints
                                        |> MinMax.setConstraints
                                               (dl.NormQuantityAdjust)
                                               dl.QuantityAdjust

                                    itmDto.Dose.PerTime.Constraints <-
                                        itmDto.Dose.PerTime.Constraints
                                        |> MinMax.setConstraints
                                               None
                                               dl.PerTime

                                    itmDto.Dose.PerTimeAdjust.Constraints <-
                                        itmDto.Dose.PerTimeAdjust.Constraints
                                        |> MinMax.setConstraints
                                               (dl.NormPerTimeAdjust)
                                               dl.PerTimeAdjust


                            match d.OrderType with
                            | AnyOrder -> ()
                            | ProcessOrder -> ()
                            | ContinuousOrder ->
                                match s.Dose with
                                | None    -> ()
                                | Some dl -> dl |> setDoseRate

                            | DiscontinuousOrder ->
                                match s.Dose with
                                | None -> ()
                                | Some dl -> dl |> setDoseQty

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


