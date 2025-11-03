
#time

// load demo or product cache


System.Environment.SetEnvironmentVariable("GENPRES_DEBUG", "1")
System.Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
System.Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1xhFPiF-e5rMkk7BRSfbOF-XGACeHInWobxRbjYU0_w4")

#load "load.fsx"

open MathNet.Numerics
open Informedica.Utils.Lib.BCL
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib


let print sl = sl |> List.iter (printfn "%s")


let cotrim =
    {
        Medication.order with
            Id = "1"
            Name = "cotrimoxazol"
            Components =
                [
                    {
                        Medication.productComponent with
                            Name = "cotrimoxazol"
                            Shape = "tablet"
                            Quantities =
                                1N
                                |> ValueUnit.singleWithUnit Units.Count.times
                                |> Some
                            Divisible = Some (1N)
                            Substances =
                                [
                                    {
                                        Medication.substanceItem with
                                            Name = "sulfamethoxazol"
                                            Concentrations =
                                                [| 100N; 400N; 800N |]
                                                |> ValueUnit.withUnit Units.Mass.milliGram
                                                |> Some
                                    }
                                    {
                                        Medication.substanceItem with
                                            Name = "trimethoprim"
                                            Concentrations =
                                                [| 20N; 80N; 160N |]
                                                |> ValueUnit.withUnit Units.Mass.milliGram
                                                |> Some
                                    }
                                ]
                    }
                ]
            Route = "or"
            OrderType = DiscontinuousOrder
            Frequencies =
                [|2N |]
                |> ValueUnit.withUnit (Units.Count.times |> Units.per Units.Time.day)
                |> Some
    }


cotrim
|> Medication.toString
|> print


cotrim
|> Medication.toOrderDto
|> Order.Dto.fromDto
|> Result.map Order.toString


let tpnComplete =
    { Medication.order with
        Id = "f1adf475-919b-4b7d-9e26-6cc502b88e42"
        Name = "samenstelling c"
        Route = "INTRAVENEUS"
        OrderType = TimedOrder
        Adjust =
            11N
            |> ValueUnit.singleWithUnit Units.Weight.kiloGram
            |> Some
        AdjustUnit = Units.Weight.kiloGram |> Some
        Frequencies =
            1N
            |> ValueUnit.singleWithUnit (Units.Count.times |> Units.per Units.Time.day)
            |> Some
        Time =
            { MinMax.empty with
                Min =
                    20N
                    |> ValueUnit.singleWithUnit Units.Time.hour
                    |> Limit.inclusive
                    |> Some
                Max =
                    24N
                    |> ValueUnit.singleWithUnit Units.Time.hour
                    |> Limit.inclusive
                    |> Some
            }
        Dose =
            { DoseLimit.limit with
                DoseLimitTarget = "vloeistof" |> LimitTarget.ShapeLimitTarget
                AdjustUnit = Units.Weight.kiloGram |> Some
                QuantityAdjust =
                    { MinMax.empty with
                        Max =
                            (755N / 10N)
                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                            |> Limit.inclusive
                            |> Some
                    }
            }
            |> Some
        DoseCount =
            { MinMax.empty with
                Min = 1N |> ValueUnit.singleWithUnit Units.Count.times |> Limit.inclusive |> Some
                Max = 1N |> ValueUnit.singleWithUnit Units.Count.times |> Limit.inclusive |> Some
            }
        Components =
            [
                // Samenstelling C component
                {
                    Medication.productComponent with
                        Name = "Samenstelling C"
                        Shape = "vloeistof"
                        Quantities =
                            1N
                            |> ValueUnit.singleWithUnit Units.Volume.milliLiter
                            |> Some
                        Divisible = Some (1N)
                        Dose =
                            { DoseLimit.limit with
                                DoseLimitTarget = "Samenstelling C" |> LimitTarget.ComponentLimitTarget
                                AdjustUnit = Units.Weight.kiloGram |> Some
                                QuantityAdjust =
                                    { MinMax.empty with
                                        Min =
                                            10N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                        Max =
                                            25N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                    }
                            }
                            |> Some
                        Substances =
                            [
                                {
                                    Medication.substanceItem with
                                        Name = "energie"
                                        Concentrations =
                                            (32N / 100N)
                                            |> ValueUnit.singleWithUnit (Units.Energy.kiloCalorie |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "eiwit"
                                        Concentrations =
                                            (8N / 100N)
                                            |> ValueUnit.singleWithUnit (Units.Mass.gram |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                        Solution =
                                            { SolutionLimit.limit with
                                                SolutionLimitTarget = "eiwit" |> LimitTarget.SubstanceLimitTarget
                                                Concentration =
                                                    { MinMax.empty with
                                                        Max =
                                                            (5N / 100N)
                                                            |> ValueUnit.singleWithUnit (Units.Mass.gram |> Units.per Units.Volume.milliLiter)
                                                            |> Limit.inclusive
                                                            |> Some
                                                    }
                                            }
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "natrium"
                                        Concentrations =
                                            (1N / 100N)
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                        Solution =
                                            { SolutionLimit.limit with
                                                SolutionLimitTarget = "natrium" |> LimitTarget.SubstanceLimitTarget
                                                Concentration =
                                                    { MinMax.empty with
                                                        Max =
                                                            (5N / 10N)
                                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                                            |> Limit.inclusive
                                                            |> Some
                                                    }
                                            }
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "kalium"
                                        Concentrations =
                                            (2N / 100N)
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                        Solution =
                                            { SolutionLimit.limit with
                                                SolutionLimitTarget = "kalium" |> LimitTarget.SubstanceLimitTarget
                                                Concentration =
                                                    { MinMax.empty with
                                                        Max =
                                                            (5N / 10N)
                                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                                            |> Limit.inclusive
                                                            |> Some
                                                    }
                                            }
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "calcium"
                                        Concentrations =
                                            (3N / 100N)
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "fosfaat"
                                        Concentrations =
                                            (2N / 100N)
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "magnesium"
                                        Concentrations =
                                            (1N / 100N)
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "chloor"
                                        Concentrations =
                                            (7N / 100N)
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                }
                            ]
                }
                // NaCl 3% component
                {
                    Medication.productComponent with
                        Name = "NaCl 3%"
                        Shape = "vloeistof"
                        Quantities =
                            1N
                            |> ValueUnit.singleWithUnit Units.Volume.milliLiter
                            |> Some
                        Divisible = Some (1N)
                        Dose =
                            { DoseLimit.limit with
                                DoseLimitTarget = "NaCl 3%" |> LimitTarget.ComponentLimitTarget
                                AdjustUnit = Units.Weight.kiloGram |> Some
                                QuantityAdjust =
                                    { MinMax.empty with
                                        Min =
                                            6N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                        Max =
                                            6N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                    }
                            }
                            |> Some
                        Substances =
                            [
                                {
                                    Medication.substanceItem with
                                        Name = "natrium"
                                        Concentrations =
                                            (5N / 10N)
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                        Solution =
                                            { SolutionLimit.limit with
                                                SolutionLimitTarget = "natrium" |> LimitTarget.SubstanceLimitTarget
                                                Concentration =
                                                    { MinMax.empty with
                                                        Max =
                                                            (5N / 10N)
                                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                                            |> Limit.inclusive
                                                            |> Some
                                                    }
                                            }
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "chloor"
                                        Concentrations =
                                            (5N / 10N)
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                }
                            ]
                }
                // KCl 7,4% component
                {
                    Medication.productComponent with
                        Name = "KCl 7,4%"
                        Shape = "vloeistof"
                        Quantities =
                            1N
                            |> ValueUnit.singleWithUnit Units.Volume.milliLiter
                            |> Some
                        Divisible = Some (1N)
                        Dose =
                            { DoseLimit.limit with
                                DoseLimitTarget = "KCl 7,4%" |> LimitTarget.ComponentLimitTarget
                                AdjustUnit = Units.Weight.kiloGram |> Some
                                QuantityAdjust =
                                    { MinMax.empty with
                                        Min =
                                            2N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                        Max =
                                            2N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                    }
                            }
                            |> Some
                        Substances =
                            [
                                {
                                    Medication.substanceItem with
                                        Name = "kalium"
                                        Concentrations =
                                            1N
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                        Solution =
                                            { SolutionLimit.limit with
                                                SolutionLimitTarget = "kalium" |> LimitTarget.SubstanceLimitTarget
                                                Concentration =
                                                    { MinMax.empty with
                                                        Max =
                                                            (5N / 10N)
                                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                                            |> Limit.inclusive
                                                            |> Some
                                                    }
                                            }
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "chloor"
                                        Concentrations =
                                            1N
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                }
                            ]
                }
                // gluc 10% component
                {
                    Medication.productComponent with
                        Name = "gluc 10%"
                        Shape = "vloeistof"
                        Quantities =
                            1N
                            |> ValueUnit.singleWithUnit Units.Volume.milliLiter
                            |> Some
                        Divisible = Some (1N)
                        Substances =
                            [
                                {
                                    Medication.substanceItem with
                                        Name = "energie"
                                        Concentrations =
                                            (4N / 10N)
                                            |> ValueUnit.singleWithUnit (Units.Energy.kiloCalorie |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "koolhydraat"
                                        Concentrations =
                                            (1N / 10N)
                                            |> ValueUnit.singleWithUnit (Units.Mass.gram |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                }
                            ]
                }
            ]
    }



let tpn =
    { Medication.order with
        Id = "f1adf475-919b-4b7d-9e26-6cc502b88e42"
        Name = "samenstelling c"
        Route = "INTRAVENEUS"
        OrderType = TimedOrder
        Adjust =
            11N
            |> ValueUnit.singleWithUnit Units.Weight.kiloGram
            |> Some
        AdjustUnit = Units.Weight.kiloGram |> Some
        Frequencies =
            1N
            |> ValueUnit.singleWithUnit (Units.Count.times |> Units.per Units.Time.day)
            |> Some
        Time =
            { MinMax.empty with
                Min =
                    20N
                    |> ValueUnit.singleWithUnit Units.Time.hour
                    |> Limit.inclusive
                    |> Some
                Max =
                    24N
                    |> ValueUnit.singleWithUnit Units.Time.hour
                    |> Limit.inclusive
                    |> Some
            }
        Dose =
            { DoseLimit.limit with
                DoseLimitTarget = "vloeistof" |> LimitTarget.ShapeLimitTarget
                AdjustUnit = Units.Weight.kiloGram |> Some
                QuantityAdjust =
                    { MinMax.empty with
                        Max =
                            (755N / 10N)
                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                            |> Limit.inclusive
                            |> Some
                    }
            }
            |> Some
        DoseCount =
            { MinMax.empty with
                Min = 1N |> ValueUnit.singleWithUnit Units.Count.times |> Limit.inclusive |> Some
                Max = 1N |> ValueUnit.singleWithUnit Units.Count.times |> Limit.inclusive |> Some
            }
        Components =
            [
                // Samenstelling C component
                {
                    Medication.productComponent with
                        Name = "Samenstelling C"
                        Shape = "vloeistof"
                        Quantities =
                            1N
                            |> ValueUnit.singleWithUnit Units.Volume.milliLiter
                            |> Some
                        Divisible = Some (1N)
                        Dose =
                            { DoseLimit.limit with
                                DoseLimitTarget = "Samenstelling C" |> LimitTarget.ComponentLimitTarget
                                AdjustUnit = Units.Weight.kiloGram |> Some
                                QuantityAdjust =
                                    { MinMax.empty with
                                        Min =
                                            10N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                        Max =
                                            25N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                    }
                            }
                            |> Some
                        Substances =
                            [
                                {
                                    Medication.substanceItem with
                                        Name = "eiwit"
                                        Concentrations =
                                            (8N / 100N)
                                            |> ValueUnit.singleWithUnit (Units.Mass.gram |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                        Solution =
                                            { SolutionLimit.limit with
                                                SolutionLimitTarget = "eiwit" |> LimitTarget.SubstanceLimitTarget
                                                Concentration =
                                                    { MinMax.empty with
                                                        Max =
                                                            (5N / 100N)
                                                            |> ValueUnit.singleWithUnit (Units.Mass.gram |> Units.per Units.Volume.milliLiter)
                                                            |> Limit.inclusive
                                                            |> Some
                                                    }
                                            }
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "natrium"
                                        Concentrations =
                                            (1N / 100N)
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                        Solution =
                                            { SolutionLimit.limit with
                                                SolutionLimitTarget = "natrium" |> LimitTarget.SubstanceLimitTarget
                                                Concentration =
                                                    { MinMax.empty with
                                                        Max =
                                                            (5N / 10N)
                                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                                            |> Limit.inclusive
                                                            |> Some
                                                    }
                                            }
                                            |> Some
                                }
                                {
                                    Medication.substanceItem with
                                        Name = "kalium"
                                        Concentrations =
                                            (2N / 100N)
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                        Solution =
                                            { SolutionLimit.limit with
                                                SolutionLimitTarget = "kalium" |> LimitTarget.SubstanceLimitTarget
                                                Concentration =
                                                    { MinMax.empty with
                                                        Max =
                                                            (5N / 10N)
                                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                                            |> Limit.inclusive
                                                            |> Some
                                                    }
                                            }
                                            |> Some
                                }
                            ]
                }
                // NaCl 3% component
                {
                    Medication.productComponent with
                        Name = "NaCl 3%"
                        Shape = "vloeistof"
                        Quantities =
                            1N
                            |> ValueUnit.singleWithUnit Units.Volume.milliLiter
                            |> Some
                        Divisible = Some (1N)
                        Dose =
                            { DoseLimit.limit with
                                DoseLimitTarget = "NaCl 3%" |> LimitTarget.ComponentLimitTarget
                                AdjustUnit = Units.Weight.kiloGram |> Some
                                QuantityAdjust =
                                    { MinMax.empty with
                                        Min =
                                            6N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                        Max =
                                            6N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                    }
                            }
                            |> Some
                        Substances =
                            [
                                {
                                    Medication.substanceItem with
                                        Name = "natrium"
                                        Concentrations =
                                            (5N / 10N)
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                        Solution =
                                            { SolutionLimit.limit with
                                                SolutionLimitTarget = "natrium" |> LimitTarget.SubstanceLimitTarget
                                                Concentration =
                                                    { MinMax.empty with
                                                        Max =
                                                            (5N / 10N)
                                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                                            |> Limit.inclusive
                                                            |> Some
                                                    }
                                            }
                                            |> Some
                                }
                            ]
                }
                // KCl 7,4% component
                {
                    Medication.productComponent with
                        Name = "KCl 7,4%"
                        Shape = "vloeistof"
                        Quantities =
                            1N
                            |> ValueUnit.singleWithUnit Units.Volume.milliLiter
                            |> Some
                        Divisible = Some (1N)
                        Dose =
                            { DoseLimit.limit with
                                DoseLimitTarget = "KCl 7,4%" |> LimitTarget.ComponentLimitTarget
                                AdjustUnit = Units.Weight.kiloGram |> Some
                                QuantityAdjust =
                                    { MinMax.empty with
                                        Min =
                                            2N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                        Max =
                                            2N
                                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> Units.per Units.Weight.kiloGram)
                                            |> Limit.inclusive
                                            |> Some
                                    }
                            }
                            |> Some
                        Substances =
                            [
                                {
                                    Medication.substanceItem with
                                        Name = "kalium"
                                        Concentrations =
                                            1N
                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                        Solution =
                                            { SolutionLimit.limit with
                                                SolutionLimitTarget = "kalium" |> LimitTarget.SubstanceLimitTarget
                                                Concentration =
                                                    { MinMax.empty with
                                                        Max =
                                                            (5N / 10N)
                                                            |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> Units.per Units.Volume.milliLiter)
                                                            |> Limit.inclusive
                                                            |> Some
                                                    }
                                            }
                                            |> Some
                                }
                            ]
                }
                // gluc 10% component
                {
                    Medication.productComponent with
                        Name = "gluc 10%"
                        Shape = "vloeistof"
                        Quantities =
                            1N
                            |> ValueUnit.singleWithUnit Units.Volume.milliLiter
                            |> Some
                        Divisible = Some (1N)
                        Substances =
                            [
                                {
                                    Medication.substanceItem with
                                        Name = "koolhydraat"
                                        Concentrations =
                                            (1N / 10N)
                                            |> ValueUnit.singleWithUnit (Units.Mass.gram |> Units.per Units.Volume.milliLiter)
                                            |> Some
                                }
                            ]
                }
            ]
    }


tpn
|> Medication.toString
|> print


let tpnConstraints =
    [
        OrderAdjust OrderVariable.Quantity.applyConstraints

        ScheduleFrequency OrderVariable.Frequency.applyConstraints
        ScheduleTime OrderVariable.Time.applyConstraints

        OrderableQuantity OrderVariable.Quantity.applyConstraints
        OrderableDoseCount OrderVariable.Count.applyConstraints
        OrderableDose Order.Orderable.Dose.applyConstraints

        ComponentOrderableQuantity ("", OrderVariable.Quantity.applyConstraints)

        ItemComponentConcentration ("", "", OrderVariable.Concentration.applyConstraints)
        ItemOrderableConcentration ("", "", OrderVariable.Concentration.applyConstraints)
    ]


let tpnSettings =
    [
        OrderableDose (Order.Orderable.Dose.applyConstraints)
    ]


let logger = OrderLogging.createConsoleLogger ()


let applyPropChange msg propChange ord =
    printfn $"=== Apply PropChange {msg} ==="
    let ord =
        ord
        |> Order.OrderPropertyChange.proc propChange
    ord
    |> Order.solveMinMax true Logging.noOp
    |> function
        | Ok ord -> ord
        | _ ->
            printfn $"=== ERROR {msg} ==="
            ord
    |> fun ord ->
        ord
        |> Order.printTable ConsoleTables.Format.Minimal

        ord


let run tpn =
    tpn
    |> Medication.toOrderDto
    |> Order.Dto.fromDto
    |> Result.map (fun ord ->
        let ord =
            ord
            |> Order.OrderPropertyChange.proc tpnConstraints
    //        |> Order.applyConstraints

        ord
        |> Order.printTable ConsoleTables.Format.Minimal

        let ord =
            ord
            |> Order.solveMinMax true Logging.noOp //logger
            //|> Result.bind (Order.solveMinMax true logger)

        ord
        |> Result.iter (Order.printTable ConsoleTables.Format.Minimal)

        let ord =
            ord
            |> Result.map (fun ord ->
                ord
                |> applyPropChange
                    "Samenstelling C"
                    [
                        ComponentOrderableQuantity ("Samenstelling C", OrderVariable.Quantity.setNthValue 519)
                    ]
            )

        let ord =
            ord
            |> Result.map (fun ord ->
                ord
                |> applyPropChange
                    "KCl 7,4%"
                    [
                        ComponentOrderableQuantity ("KCl 7,4%", OrderVariable.Quantity.setNthValue 30)
                    ]
            )

        let ord =
            ord
            |> Result.map (fun ord ->
                ord
                |> applyPropChange
                    "NaCl 3%"
                    [
                        ComponentOrderableQuantity ("NaCl 3%", OrderVariable.Quantity.setNthValue 30)
                    ]
            )

        let ord =
            ord
            |> Result.map (fun ord ->
                ord
                |> applyPropChange
                    "gluc 10%"
                    [
                        ComponentOrderableQuantity ("gluc 10%", OrderVariable.Quantity.setNthValue 1)
                    ]
            )

        ord
    )


run tpnComplete
|> ignore