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


    let createValueUnitDto u br =
        if u |> String.isNullOrWhiteSpace then None
        else
            let vuDto = ValueUnit.Dto.dto()
            vuDto.Value <- br |> Seq.toArray |> Array.map BigRational.toDecimal
            vuDto.Unit <- u
            vuDto |> Some


    let createSingleValueUnitDto u br =
        createValueUnitDto u [| br |]


    module MinMax =

        let setConstraints un (brs : BigRational []) (minMax : MinMax) (dto: OrderVariable.Dto.VarDto) =
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
                dto.Min <- min |> createSingleValueUnitDto un

            match max with
            | None -> ()
            | Some max ->
                dto.MaxIncl <- true
                dto.Max <- max |> createSingleValueUnitDto un

            dto


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


    let substanceItem =
        {
            Name = ""
            Concentrations = []
            Unit = ""
            TimeUnit = ""
            Dose = None //DoseLimit.limit
            Solution = None
        }


    let unitGroup u =
        if u = "kg" then "kg[Weight]"
        else
            Units.units
            |> List.filter (fun ud ->
                ud.Group <> Group.WeightGroup
            )
            |> List.tryFind (fun ud ->
                [
                    ud.Abbreviation.Dut
                    ud.Abbreviation.Eng
                    ud.Name.Dut
                    ud.Name.Eng
                ]
                |> List.append ud.Synonyms
                |> List.exists(String.equalsCapInsens u)
            )
            |> function
            | Some ud ->
                ud.Group
                |> ValueUnit.Group.toString
            | None -> "General"
            |> sprintf "%s[%s]" u


    let toOrder (d : DrugOrder) =
        let toArr = Option.map Array.singleton >> Option.defaultValue [||]

        let standDoseRate un (orbDto : Order.Orderable.Dto.Dto) =
            orbDto.Dose.Rate.Constraints.Incr <- 1N/10N |> createSingleValueUnitDto un
            orbDto.Dose.Rate.Constraints.MinIncl <- true
            orbDto.Dose.Rate.Constraints.Min <- 1N/10N |> createSingleValueUnitDto un
            orbDto.Dose.Rate.Constraints.MaxIncl <- true
            orbDto.Dose.Rate.Constraints.Max <- 1000N |> createSingleValueUnitDto un

        // create the units
        let cu = "x[Count]"
        let ml = "ml[Volume]"

        let ou = d.Unit |> unitGroup
        let au = d.AdjustUnit |> unitGroup
        let du =
            match d.Dose with
            | Some dl -> dl.DoseUnit |> unitGroup
            | None -> ou
        let ft = d.FreqUnit |> unitGroup
        let ru = d.RateUnit |> unitGroup
        let tu = d.TimeUnit |> unitGroup

        let ofu = $"{cu}/{ft}"
        let oru = $"{ml}/{ru}"
        let ora = $"{ml}/{au}/{ru}"
        let oda = $"{du}/{au}"
        let opt = $"{du}/{ft}"
        let pta = $"{du}/{au}/{ft}"

        let orbDto = Order.Orderable.Dto.dto d.Id d.Name

        orbDto.DoseCount.Constraints.Vals <-
            d.DoseCount
            |> Option.bind (createSingleValueUnitDto cu)

        orbDto.OrderableQuantity.Constraints.Vals <- d.Quantities |> createValueUnitDto ou

        let setOrbDoseRate (dl : DoseLimit) =
            orbDto.Dose.Rate.Constraints.MinIncl <- dl.Rate.Minimum.IsSome
            orbDto.Dose.Rate.Constraints.Min <- dl.Rate.Minimum |> Option.bind (createSingleValueUnitDto oru)
            orbDto.Dose.Rate.Constraints.MinIncl <- dl.Rate.Maximum.IsSome
            orbDto.Dose.Rate.Constraints.Min <- dl.Rate.Maximum |> Option.bind (createSingleValueUnitDto oru)

            orbDto.Dose.RateAdjust.Constraints.MinIncl <- dl.RateAdjust.Minimum.IsSome
            orbDto.Dose.RateAdjust.Constraints.Min <- dl.RateAdjust.Minimum |> Option.bind (createSingleValueUnitDto ora)
            orbDto.Dose.RateAdjust.Constraints.MinIncl <- dl.RateAdjust.Maximum.IsSome
            orbDto.Dose.RateAdjust.Constraints.Min <- dl.RateAdjust.Maximum |> Option.bind (createSingleValueUnitDto ora)

        let setOrbDoseQty (dl : DoseLimit) =
            orbDto.Dose.Quantity.Constraints.Vals <- dl.NormQuantity |> createValueUnitDto du

            orbDto.Dose.Quantity.Constraints.MinIncl <- dl.Quantity.Minimum.IsSome
            orbDto.Dose.Quantity.Constraints.Min <- dl.Quantity.Minimum |> Option.bind (createSingleValueUnitDto du)
            orbDto.Dose.Quantity.Constraints.MaxIncl <- dl.Quantity.Maximum.IsSome
            orbDto.Dose.Quantity.Constraints.Max <- dl.Quantity.Maximum |> Option.bind (createSingleValueUnitDto du)

            orbDto.Dose.QuantityAdjust.Constraints.MinIncl <- dl.QuantityAdjust.Minimum.IsSome
            orbDto.Dose.QuantityAdjust.Constraints.Min <- dl.QuantityAdjust.Minimum |> Option.bind (createSingleValueUnitDto oda)
            orbDto.Dose.QuantityAdjust.Constraints.MaxIncl <- dl.QuantityAdjust.Maximum.IsSome
            orbDto.Dose.QuantityAdjust.Constraints.Max <- dl.QuantityAdjust.Maximum |> Option.bind (createSingleValueUnitDto oda)

            orbDto.Dose.PerTime.Constraints.MinIncl <- dl.PerTime.Minimum.IsSome
            orbDto.Dose.PerTime.Constraints.Min <- dl.PerTime.Minimum |> Option.bind (createSingleValueUnitDto opt)
            orbDto.Dose.PerTime.Constraints.MaxIncl <- dl.PerTime.Maximum.IsSome
            orbDto.Dose.PerTime.Constraints.Max <- dl.PerTime.Maximum |> Option.bind (createSingleValueUnitDto opt)

            orbDto.Dose.PerTimeAdjust.Constraints.MinIncl <- dl.PerTimeAdjust.Minimum.IsSome
            orbDto.Dose.PerTimeAdjust.Constraints.Min <- dl.PerTimeAdjust.Minimum |> Option.bind (createSingleValueUnitDto pta)
            orbDto.Dose.PerTimeAdjust.Constraints.MaxIncl <- dl.PerTimeAdjust.Maximum.IsSome
            orbDto.Dose.PerTimeAdjust.Constraints.Max <- dl.PerTimeAdjust.Maximum |> Option.bind (createSingleValueUnitDto pta)

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

                    cdto.ComponentQuantity.Constraints.Vals <- p.Quantities |> createValueUnitDto ou
                    if p.Divisible.IsSome then
                        cdto.OrderableQuantity.Constraints.Incr <- 1N / p.Divisible.Value |> createSingleValueUnitDto ou

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

                            itmDto.ComponentConcentration.Constraints.Vals <- s.Concentrations |> createValueUnitDto $"{su}/{ou}"

                            match s.Solution with
                            | Some sl ->
                                itmDto.OrderableQuantity.Constraints.MinIncl <- sl.Quantity.Minimum.IsSome
                                itmDto.OrderableQuantity.Constraints.Min <- sl.Quantity.Minimum |> Option.bind (createSingleValueUnitDto su)
                                itmDto.OrderableQuantity.Constraints.MaxIncl <- sl.Quantity.Maximum.IsSome
                                itmDto.OrderableQuantity.Constraints.Max <- sl.Quantity.Maximum |> Option.bind (createSingleValueUnitDto su)
                                itmDto.OrderableConcentration.Constraints.MinIncl <- sl.Concentration.Minimum.IsSome
                                itmDto.OrderableConcentration.Constraints.Min <- sl.Concentration.Minimum |> Option.bind (createSingleValueUnitDto $"{su}/{ou}")
                                itmDto.OrderableConcentration.Constraints.MaxIncl <- sl.Concentration.Maximum.IsSome
                                itmDto.OrderableConcentration.Constraints.Max <- sl.Concentration.Maximum |> Option.bind (createSingleValueUnitDto $"{su}/{ou}")
                            | None -> ()

                            let setDoseRate (dl : DoseLimit) =
                                let dru = $"{du}/{dl.RateUnit |> unitGroup}"
                                let dra = $"{du}/{au}/{dl.RateUnit |> unitGroup}"

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
                                        |> MinMax.setConstraints $"{du}/{s.TimeUnit |> unitGroup}" dl.NormPerTime dl.PerTime

                                    itmDto.Dose.PerTimeAdjust.Constraints <-
                                        itmDto.Dose.PerTimeAdjust.Constraints
                                        |> MinMax.setConstraints $"{du}/{au}/{s.TimeUnit |> unitGroup}" (dl.NormPerTimeAdjust |> toArr) dl.PerTimeAdjust


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

        dto.Prescription.Frequency.Constraints.Vals <- d.Frequencies |> createValueUnitDto ofu

        dto.Prescription.Time.Constraints.MinIncl <- d.Time.Minimum.IsSome
        dto.Prescription.Time.Constraints.Min <- d.Time.Minimum |> Option.bind (createSingleValueUnitDto tu)
        dto.Prescription.Time.Constraints.MaxIncl <- d.Time.Maximum.IsSome
        dto.Prescription.Time.Constraints.Max <- d.Time.Maximum |> Option.bind (createSingleValueUnitDto tu)

        if au |> String.contains "kg" then 
            dto.Adjust.Constraints.Min <- 
                (200N /1000N) |> createSingleValueUnitDto au

        if au |> String.contains "kg" then 
            dto.Adjust.Constraints.Max <- 150N |> createSingleValueUnitDto au
    
        dto.Adjust.Constraints.Vals <-
            d.Adjust
            |> Option.bind (createSingleValueUnitDto au)

        dto


