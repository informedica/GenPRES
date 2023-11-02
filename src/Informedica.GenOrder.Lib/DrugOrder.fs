namespace Informedica.GenOrder.Lib



module DrugOrder =

    open MathNet.Numerics
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib

    type MinMax = Informedica.GenForm.Lib.Types.MinMax

    module DoseRule = Informedica.GenForm.Lib.DoseRule
    module DoseLimit = DoseRule.DoseLimit
    module MinMax = Informedica.GenForm.Lib.MinMax


    /// <summary>
    /// Create a value unit dto from a string and a sequence of big rationals.
    /// </summary>
    /// <param name="u">The unit as a string.</param>
    /// <param name="brs">The big rationals as a sequence.</param>
    /// <remarks>
    /// If the unit is null or an empty empty string, the function returns None.
    /// </remarks>
    let createValueUnitDto u brs =
        if u |> String.isNullOrWhiteSpace then None
        else
            let vuDto = ValueUnit.Dto.dto()
            vuDto.Value <-
                brs
                |> Seq.toArray
                |> Array.map (fun v ->
                    v |> string,
                    v |> BigRational.toDecimal
                )
            vuDto.Unit <- u
            vuDto |> Some


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
        let setConstraints un (brs : BigRational []) (minMax : MinMax) (dto: Informedica.GenSolver.Lib.Variable.Dto.Dto) =
            let min =
                match minMax.Minimum, brs with
                | None, [|br|] -> br - br / 10N |> Some
                | _  -> minMax.Minimum

            let max =
                match minMax.Maximum, brs with
                | None, [|br|] -> br + br / 10N |> Some
                | _  -> minMax.Maximum

            match min with
            | None -> ()
            | Some min ->
                dto.MinIncl <- true
                dto.MinOpt <- min |> createSingleValueUnitDto un

            match max with
            | None -> ()
            | Some max ->
                dto.MaxIncl <- true
                dto.MaxOpt <- max |> createSingleValueUnitDto un

            dto


    /// An empty DrugOrder record.
    let drugOrder =
        {
            Id = ""
            Name = ""
            Products = []
            Quantities = []
            Unit = ""
            Route = ""
            OrderType = AnyOrder
            Frequencies = []
            FreqUnit = ""
            Rates = []
            RateUnit = ""
            Time = MinMax.none
            TimeUnit = ""
            Dose = None
            DoseCount = None
            Adjust = None
            AdjustUnit = ""
        }


    /// An empty Product Component record.
    let productComponent =
        {
            Name = ""
            Shape = ""
            Quantities = []
            TimeUnit = ""
            RateUnit = ""
            Divisible = Some 1N
            Substances = []
        }


    /// An empty Substance Item record.
    let substanceItem =
        {
            Name = ""
            Concentrations = []
            Unit = ""
            TimeUnit = ""
            Dose = None //DoseLimit.limit
            Solution = None
        }


    /// Short hand for Units.stringWithGroup to
    /// append the unit group to a unit.
    let unitGroup = Units.stringWithGroup


    /// <summary>
    /// Map a DrugOrder record to a DrugOrderDto record.
    /// </summary>
    /// <remarks>
    /// The DrugOrder will mainly map to the Order constraints.
    /// </remarks>
    let toOrderDto (d : DrugOrder) =
        let toArr = Option.map Array.singleton >> Option.defaultValue [||]

        let standDoseRate un (orbDto : Order.Orderable.Dto.Dto) =
            orbDto.Dose.Rate.Constraints.IncrOpt <- 1N/10N |> createSingleValueUnitDto un
            orbDto.Dose.Rate.Constraints.MinIncl <- true
            orbDto.Dose.Rate.Constraints.MinOpt <- 1N/10N |> createSingleValueUnitDto un
            orbDto.Dose.Rate.Constraints.MaxIncl <- true
            orbDto.Dose.Rate.Constraints.MaxOpt <- 1000N |> createSingleValueUnitDto un

        // create the units
        let cu = "x[Count]"
        let ml = "ml[Volume]"

        let ou = d.Unit |> unitGroup
        let au =
            match d.AdjustUnit with
            | s when s = "kg" -> "kg[Weight]"
            | s when s = "m2" -> "m2[BSA]"
            | _ -> $"cannot parse adjust unit: {d.AdjustUnit}" |> failwith
        let du =
            match d.Dose with
            | Some dl -> dl.DoseUnit |> unitGroup
            | None -> ou
        let ft = $"{d.FreqUnit}[Time]"
        let ru = $"{d.RateUnit}[Time]"
        let tu = $"{d.TimeUnit}[Time]"

        let ofu = $"{cu}/{ft}"
        let oru = $"{ml}/{ru}"
        let ora = $"{ml}/{au}/{ru}"
        let oda = $"{du}/{au}"
        let opt = $"{du}/{ft}"
        let pta = $"{du}/{au}/{ft}"

        let orbDto = Order.Orderable.Dto.dto d.Id d.Name

        orbDto.DoseCount.Constraints.ValsOpt <-
            d.DoseCount
            |> Option.bind (createSingleValueUnitDto cu)

        orbDto.OrderableQuantity.Constraints.ValsOpt <- d.Quantities |> createValueUnitDto ou

        let setOrbDoseRate (dl : DoseLimit) =
            orbDto.Dose.Rate.Constraints.MinIncl <- dl.Rate.Minimum.IsSome
            orbDto.Dose.Rate.Constraints.MinOpt <- dl.Rate.Minimum |> Option.bind (createSingleValueUnitDto oru)
            orbDto.Dose.Rate.Constraints.MinIncl <- dl.Rate.Maximum.IsSome
            orbDto.Dose.Rate.Constraints.MinOpt <- dl.Rate.Maximum |> Option.bind (createSingleValueUnitDto oru)

            orbDto.Dose.RateAdjust.Constraints.MinIncl <- dl.RateAdjust.Minimum.IsSome
            orbDto.Dose.RateAdjust.Constraints.MinOpt <- dl.RateAdjust.Minimum |> Option.bind (createSingleValueUnitDto ora)
            orbDto.Dose.RateAdjust.Constraints.MinIncl <- dl.RateAdjust.Maximum.IsSome
            orbDto.Dose.RateAdjust.Constraints.MinOpt <- dl.RateAdjust.Maximum |> Option.bind (createSingleValueUnitDto ora)

        let setOrbDoseQty (dl : DoseLimit) =
            orbDto.Dose.Quantity.Constraints.ValsOpt <- dl.NormQuantity |> createValueUnitDto du

            orbDto.Dose.Quantity.Constraints.MinIncl <- dl.Quantity.Minimum.IsSome
            orbDto.Dose.Quantity.Constraints.MinOpt <- dl.Quantity.Minimum |> Option.bind (createSingleValueUnitDto du)
            orbDto.Dose.Quantity.Constraints.MaxIncl <- dl.Quantity.Maximum.IsSome
            orbDto.Dose.Quantity.Constraints.MaxOpt <- dl.Quantity.Maximum |> Option.bind (createSingleValueUnitDto du)

            orbDto.Dose.QuantityAdjust.Constraints.MinIncl <- dl.QuantityAdjust.Minimum.IsSome
            orbDto.Dose.QuantityAdjust.Constraints.MinOpt <- dl.QuantityAdjust.Minimum |> Option.bind (createSingleValueUnitDto oda)
            orbDto.Dose.QuantityAdjust.Constraints.MaxIncl <- dl.QuantityAdjust.Maximum.IsSome
            orbDto.Dose.QuantityAdjust.Constraints.MaxOpt <- dl.QuantityAdjust.Maximum |> Option.bind (createSingleValueUnitDto oda)

            orbDto.Dose.PerTime.Constraints.MinIncl <- dl.PerTime.Minimum.IsSome
            orbDto.Dose.PerTime.Constraints.MinOpt <- dl.PerTime.Minimum |> Option.bind (createSingleValueUnitDto opt)
            orbDto.Dose.PerTime.Constraints.MaxIncl <- dl.PerTime.Maximum.IsSome
            orbDto.Dose.PerTime.Constraints.MaxOpt <- dl.PerTime.Maximum |> Option.bind (createSingleValueUnitDto opt)

            orbDto.Dose.PerTimeAdjust.Constraints.MinIncl <- dl.PerTimeAdjust.Minimum.IsSome
            orbDto.Dose.PerTimeAdjust.Constraints.MinOpt <- dl.PerTimeAdjust.Minimum |> Option.bind (createSingleValueUnitDto pta)
            orbDto.Dose.PerTimeAdjust.Constraints.MaxIncl <- dl.PerTimeAdjust.Maximum.IsSome
            orbDto.Dose.PerTimeAdjust.Constraints.MaxOpt <- dl.PerTimeAdjust.Maximum |> Option.bind (createSingleValueUnitDto pta)

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

                    cdto.ComponentQuantity.Constraints.ValsOpt <- p.Quantities |> createValueUnitDto ou
                    if p.Divisible.IsSome then
                        cdto.OrderableQuantity.Constraints.IncrOpt <- 1N / p.Divisible.Value |> createSingleValueUnitDto ou
                    if d.Products |> List.length = 1 then
                        // if there is only one product, the concentration of that product in the
                        // Orderable will be by definition be 1.
                        cdto.OrderableConcentration.Constraints.ValsOpt <- 1N |> createSingleValueUnitDto cu

                        if p.Divisible.IsSome then
                            cdto.Dose.Quantity.Constraints.IncrOpt <- 1N / p.Divisible.Value |> createSingleValueUnitDto ou

                    cdto.Items <- [
                        for s in p.Substances do
                            let su = s.Unit |> unitGroup
                            let du =
                                match s.Dose with
                                | Some dl ->
                                    if dl.DoseUnit |> String.isNullOrWhiteSpace then su
                                    else
                                        dl.DoseUnit |> unitGroup
                                | None -> ""

                            let itmDto =
                                Order.Orderable.Item.Dto.dto d.Id d.Name p.Name s.Name

                            itmDto.ComponentConcentration.Constraints.ValsOpt <- s.Concentrations |> createValueUnitDto $"{su}/{ou}"
                            if d.Products |> List.length = 1 then
                                // when only one product, the orderable concentration is the same as the component concentration
                                itmDto.OrderableConcentration.Constraints.ValsOpt <- itmDto.ComponentConcentration.Constraints.ValsOpt

                            match s.Solution with
                            | Some sl ->
                                let su = sl.Unit |> unitGroup // note the solution substance unit can differ from the component substance unit!
                                itmDto.OrderableQuantity.Constraints.MinIncl <- sl.Quantity.Minimum.IsSome
                                itmDto.OrderableQuantity.Constraints.MinOpt <- sl.Quantity.Minimum |> Option.bind (createSingleValueUnitDto su)
                                itmDto.OrderableQuantity.Constraints.MaxIncl <- sl.Quantity.Maximum.IsSome
                                itmDto.OrderableQuantity.Constraints.MaxOpt <- sl.Quantity.Maximum |> Option.bind (createSingleValueUnitDto su)
                                itmDto.OrderableConcentration.Constraints.MinIncl <- sl.Concentration.Minimum.IsSome
                                itmDto.OrderableConcentration.Constraints.MinOpt <- sl.Concentration.Minimum |> Option.bind (createSingleValueUnitDto $"{su}/{ou}")
                                itmDto.OrderableConcentration.Constraints.MaxIncl <- sl.Concentration.Maximum.IsSome
                                itmDto.OrderableConcentration.Constraints.MaxOpt <- sl.Concentration.Maximum |> Option.bind (createSingleValueUnitDto $"{su}/{ou}")
                            | None -> ()

                            let setDoseRate (dl : DoseLimit) =
                                let dru = $"{du}/{dl.RateUnit}[Time]"
                                let dra = $"{du}/{au}/{dl.RateUnit}[Time]"

                                itmDto.Dose.Rate.Constraints <-
                                    itmDto.Dose.Rate.Constraints
                                    |> MinMax.setConstraints dru dl.NormRate dl.Rate

                                itmDto.Dose.RateAdjust.Constraints <-
                                    itmDto.Dose.RateAdjust.Constraints
                                    |> MinMax.setConstraints dra (dl.NormRateAdjust |> toArr) dl.RateAdjust

                            let setDoseQty (dl : DoseLimit) =
                                    itmDto.Dose.Quantity.Constraints <-
                                        itmDto.Dose.Quantity.Constraints
                                        |> MinMax.setConstraints du dl.NormQuantity dl.Quantity

                                    itmDto.Dose.QuantityAdjust.Constraints <-
                                        itmDto.Dose.QuantityAdjust.Constraints
                                        |> MinMax.setConstraints $"{du}/{au}" (dl.NormQuantityAdjust |> toArr) dl.QuantityAdjust

                                    itmDto.Dose.PerTime.Constraints <-
                                        itmDto.Dose.PerTime.Constraints
                                        |> MinMax.setConstraints $"{du}/{s.TimeUnit}[Time]" dl.NormPerTime dl.PerTime

                                    itmDto.Dose.PerTimeAdjust.Constraints <-
                                        itmDto.Dose.PerTimeAdjust.Constraints
                                        |> MinMax.setConstraints $"{du}/{au}/{s.TimeUnit}[Time]" (dl.NormPerTimeAdjust |> toArr) dl.PerTimeAdjust


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

        dto.Prescription.Frequency.Constraints.ValsOpt <- d.Frequencies |> createValueUnitDto ofu

        dto.Prescription.Time.Constraints.MinIncl <- d.Time.Minimum.IsSome
        dto.Prescription.Time.Constraints.MinOpt <- d.Time.Minimum |> Option.bind (createSingleValueUnitDto tu)
        dto.Prescription.Time.Constraints.MaxIncl <- d.Time.Maximum.IsSome
        dto.Prescription.Time.Constraints.MaxOpt <- d.Time.Maximum |> Option.bind (createSingleValueUnitDto tu)

        if au |> String.contains "kg" then
            dto.Adjust.Constraints.MinOpt <-
                (200N /1000N) |> createSingleValueUnitDto au

        if au |> String.contains "kg" then
            dto.Adjust.Constraints.MaxOpt <- 150N |> createSingleValueUnitDto au

        dto.Adjust.Constraints.ValsOpt <-
            d.Adjust
            |> Option.bind (createSingleValueUnitDto au)

        dto


