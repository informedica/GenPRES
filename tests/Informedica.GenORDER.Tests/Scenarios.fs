/// Medication order temmplate scenarios for
/// testing purposes
module Scenarios

open Informedica.Utils.Lib.BCL
open Informedica.Utils.Lib.BCL
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib


/// The start every order in the tests is built with, so that two orders built from the same
/// medication are equal. Building reads no clock: the start is the caller's.
let testStart = System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)


let pcmSuppText =
    """
Id: 047f9e19-4cfc-43cb-b7ee-f88f23d2eab6
Name: paracetamol
Quantity:
Quantities:
Route: RECTAAL
OrderType: DiscontinuousOrder
Adjust: 14 kg
Frequencies: 3;4 x/dag
Time:
Dose: 1 stuk/dosis
Div:
DoseCount: 1 x
Components:
	Name: paracetamol
	Form: zetpil
	Quantities: 1 stuk
	Divisible: 1
	Dose:
	Solution:
	Substances:

		Name: paracetamol
		Concentrations: 120;240;500;1 000;125;250;60;30;360;90;750;180 mg/stuk
		Dose: paracetamol, 10 - 20 mg/kg/dosis
		Solution:
"""

let pcmSupp =
    let au = Units.Weight.kiloGram
    let fu = Units.General.general "stuk"
    let su = Units.Mass.milliGram
    let cu = su |> ValueUnit.per fu
    let tu = Units.Time.day

    { Medication.template with
        Id = "047f9e19-4cfc-43cb-b7ee-f88f23d2eab6"
        Name = "paracetamol"
        Components =
            [
                { Medication.productComponent with
                    Name = "paracetamol"
                    Form = "zetpil"
                    Quantities = 1N |> ValueUnit.singleWithUnit fu |> Some
                    Divisible = Some 1N
                    Substances =
                        [
                            { Medication.substanceItem with
                                Name = "paracetamol"
                                Concentrations =
                                    [| 120; 240; 500; 1_000; 125; 250; 60; 30; 360; 90; 750; 180 |]
                                    |> Array.map BigRational.fromInt
                                    |> ValueUnit.withUnit cu
                                    |> Some
                                Dose =
                                    { DoseLimit.limit with
                                        DoseLimitTarget = "paracetamol" |> SubstanceLimitTarget
                                        AdjustUnit = su |> Some
                                        QuantityAdjust =
                                            MinMax.createInclIncl
                                                (10N |> ValueUnit.singleWithUnit (su |> ValueUnit.per au))
                                                (20N |> ValueUnit.singleWithUnit (su |> ValueUnit.per au))
                                    }
                                    |> Some
                            }
                        ]
                }
            ]
        Route = "RECTAAL"
        OrderType = DiscontinuousOrder
        Adjust = 14N |> ValueUnit.singleWithUnit au |> Some
        Frequencies =
            [| 3N; 4N |]
            |> ValueUnit.withUnit (Units.Count.times |> ValueUnit.per tu)
            |> Some
        DoseCount = 1N |> ValueUnit.singleWithUnit Units.Count.times |> MinMax.createExact
        Dose =
            { DoseLimit.limit with Quantity = 1N |> ValueUnit.singleWithUnit fu |> MinMax.createExact }
            |> Some
    }


let amfo =
    let au = Units.Weight.kiloGram
    let fu = Units.Volume.milliLiter
    let su = Units.Mass.milliGram
    let du = Units.Mass.milliGram |> ValueUnit.per au |> ValueUnit.per Units.Time.day
    let cu = su |> ValueUnit.per fu

    { Medication.template with
        Id = "1"
        Name = "amfotericine b liposomaal"
        Route = "INTRAVENEUS"
        Quantities = None //50N |> ValueUnit.singleWithUnit fu |> Some
        OrderType = DiscontinuousOrder
        Adjust = 14N |> ValueUnit.singleWithUnit au |> Some
        Frequencies =
            Units.Count.times
            |> ValueUnit.per Units.Time.day
            |> ValueUnit.singleWithValue 1N
            |> Some
        DoseCount = 1N |> ValueUnit.singleWithUnit Units.Count.times |> MinMax.createExact
        Components =
            [
                { Medication.productComponent with
                    Name = "amfotericine b liposomaal"
                    Form = "poeder voor oplossing voor infusie"
                    Quantities = 1N |> ValueUnit.singleWithUnit fu |> Some
                    Divisible = Some 10N
                    Substances =
                        [
                            { Medication.SubstanceItem.item with
                                Name = "saccharose"
                                Concentrations =
                                    Units.Mass.milliGram
                                    |> ValueUnit.per Units.Volume.milliLiter
                                    |> ValueUnit.singleWithValue 72N
                                    |> Some
                            }
                            { Medication.SubstanceItem.item with
                                Name = "amfotericine b liposomaal"
                                Concentrations =
                                    Units.Mass.milliGram
                                    |> ValueUnit.per Units.Volume.milliLiter
                                    |> ValueUnit.singleWithValue 4N
                                    |> Some
                                Dose =
                                    { DoseLimit.limit with
                                        DoseLimitTarget = "amfotericine b liposomaal" |> SubstanceLimitTarget
                                        AdjustUnit = au |> Some
                                        PerTimeAdjust =
                                            { MinMax.empty with
                                                Min = 3N |> ValueUnit.singleWithUnit du |> Limit.inclusive |> Some
                                                Max = 5N |> ValueUnit.singleWithUnit du |> Limit.inclusive |> Some
                                            }
                                    }
                                    |> Some
                                Solution =
                                    { SolutionLimit.limit with
                                        SolutionLimitTarget = "amfotericine b liposomaal" |> SubstanceLimitTarget
                                        Concentration =
                                            { MinMax.empty with
                                                Min =
                                                    su
                                                    |> ValueUnit.per Units.Volume.milliLiter
                                                    |> ValueUnit.singleWithValue (2N / 10N)
                                                    |> Limit.inclusive
                                                    |> Some
                                                Max =
                                                    su
                                                    |> ValueUnit.per Units.Volume.milliLiter
                                                    |> ValueUnit.singleWithValue 2N
                                                    |> Limit.inclusive
                                                    |> Some
                                            }
                                    }
                                    |> Some
                            }
                        ]
                }
                { Medication.productComponent with
                    Name = "gluc 10%"
                    Form = "vloeistof"
                    Quantities = 1N |> ValueUnit.singleWithUnit fu |> Some
                    Divisible = Some 10N
                    Substances =
                        [
                            { Medication.SubstanceItem.item with
                                Name = "energie"
                                Concentrations =
                                    Units.Energy.kiloCalorie
                                    |> ValueUnit.per Units.Volume.milliLiter
                                    |> ValueUnit.singleWithValue (4N / 10N)
                                    |> Some
                            }
                            { Medication.SubstanceItem.item with
                                Name = "koolhydraat"
                                Concentrations =
                                    Units.Mass.gram
                                    |> ValueUnit.per Units.Volume.milliLiter
                                    |> ValueUnit.singleWithValue (1N / 10N)
                                    |> Some
                            }

                        ]
                }

            ]
    }


let morfCont =
    let au = Units.Weight.kiloGram
    let fu = Units.Volume.milliLiter
    let su = Units.Mass.milliGram
    let du = Units.Mass.microGram |> ValueUnit.per au |> ValueUnit.per Units.Time.hour
    let cu = su |> ValueUnit.per fu
    let ru = fu |> ValueUnit.per Units.Time.hour

    { Medication.template with
        Id = "1"
        Name = "morfine"
        Route = "INTRAVENEUS"
        Quantities = 50N |> ValueUnit.singleWithUnit fu |> Some
        Components =
            [
                { Medication.productComponent with
                    Name = "morfine"
                    Form = "injectievloeistof"
                    Quantities = 1N |> ValueUnit.singleWithUnit fu |> Some
                    Divisible = Some 10N
                    Substances =
                        [
                            { Medication.substanceItem with
                                Name = "morfin"
                                Concentrations = [| 1N; 10N |] |> ValueUnit.withUnit cu |> Some
                                Dose =
                                    { DoseLimit.limit with
                                        DoseLimitTarget = "morfine" |> SubstanceLimitTarget
                                        AdjustUnit = au |> Some
                                        RateAdjust =
                                            { MinMax.empty with
                                                Min = 10N |> ValueUnit.singleWithUnit du |> Limit.inclusive |> Some
                                                Max = 40N |> ValueUnit.singleWithUnit du |> Limit.inclusive |> Some
                                            }
                                    }
                                    |> Some
                                Solution =
                                    { SolutionLimit.limit with
                                        SolutionLimitTarget = "morfine" |> SubstanceLimitTarget
                                        Quantity = 10N |> ValueUnit.singleWithUnit su |> MinMax.createExact
                                        Concentration =
                                            { MinMax.empty with
                                                Max =
                                                    su
                                                    |> ValueUnit.per Units.Volume.milliLiter
                                                    |> ValueUnit.singleWithValue 1N
                                                    |> Limit.inclusive
                                                    |> Some
                                            }
                                    }
                                    |> Some
                            }
                        ]
                }
                { Medication.productComponent with
                    Name = "gluc 10%"
                    Form = "iv fluid"
                    Quantities = 1N |> ValueUnit.singleWithUnit fu |> Some
                    Divisible = Some 10N
                    Substances =
                        [
                            { Medication.SubstanceItem.item with
                                Name = "energie"
                                Concentrations =
                                    Units.Energy.kiloCalorie
                                    |> ValueUnit.per Units.Volume.milliLiter
                                    |> ValueUnit.singleWithValue (4N / 10N)
                                    |> Some
                            }
                            { Medication.SubstanceItem.item with
                                Name = "koolhydraat"
                                Concentrations =
                                    Units.Mass.gram
                                    |> ValueUnit.per Units.Volume.milliLiter
                                    |> ValueUnit.singleWithValue (1N / 10N)
                                    |> Some
                            }

                        ]
                }
            ]
        OrderType = ContinuousOrder
        Adjust = 14N |> ValueUnit.singleWithUnit au |> Some
        Dose =
            { DoseLimit.limit with
                DoseLimitTarget = OrderableLimitTarget
                AdjustUnit = None
            }
            |> Some
        DoseCount = 1N |> ValueUnit.singleWithUnit Units.Count.times |> MinMax.createExact
    }


let pcmDrink =
    let au = Units.Weight.kiloGram
    let fu = Units.Volume.milliLiter
    let su = Units.Mass.milliGram
    let cu = su |> ValueUnit.per fu
    let tu = Units.Time.day

    { Medication.template with
        Id = "pcm-drank"
        Name = "paracetamol drank"
        Components =
            [
                { Medication.productComponent with
                    Name = "paracetamol"
                    Form = "drank"
                    Quantities = 5N |> ValueUnit.singleWithUnit fu |> Some
                    Divisible = Some 1N
                    Substances =
                        [
                            { Medication.substanceItem with
                                Name = "paracetamol"
                                Concentrations = 24N |> ValueUnit.singleWithUnit cu |> Some
                                Dose =
                                    { DoseLimit.limit with
                                        DoseLimitTarget = "paracetamol" |> SubstanceLimitTarget
                                        AdjustUnit = su |> Some
                                        PerTimeAdjust =
                                            MinMax.createInclIncl
                                                (60N
                                                 |> ValueUnit.singleWithUnit (
                                                     su |> ValueUnit.per au |> ValueUnit.per tu
                                                 ))
                                                (90N
                                                 |> ValueUnit.singleWithUnit (
                                                     su |> ValueUnit.per au |> ValueUnit.per tu
                                                 ))
                                    }
                                    |> Some
                            }
                        ]
                }
            ]
        Route = "or"
        OrderType = DiscontinuousOrder
        Adjust = 10N |> ValueUnit.singleWithUnit au |> Some
        Frequencies =
            [| 3N; 4N; 6N |]
            |> ValueUnit.withUnit (Units.Count.times |> ValueUnit.per tu)
            |> Some
        DoseCount = 1N |> ValueUnit.singleWithUnit Units.Count.times |> MinMax.createExact
    }


let cotrim =
    let au = Units.Weight.kiloGram
    let fu = Units.Volume.milliLiter
    let su = Units.Mass.milliGram
    let cu = su |> ValueUnit.per fu
    let tu = Units.Time.day

    { Medication.template with
        Id = "1"
        Name = "cotrimoxazol"
        Components =
            [
                { Medication.productComponent with
                    Name = "cotrimoxazol"
                    Form = "drank"
                    Quantities = 1N |> ValueUnit.singleWithUnit fu |> Some
                    Divisible = Some 1N
                    Substances =
                        [
                            { Medication.substanceItem with
                                Name = "sulfamethoxazol"
                                Concentrations = [| 40N; 400N; 800N |] |> ValueUnit.withUnit cu |> Some
                                Dose =
                                    { DoseLimit.limit with
                                        DoseLimitTarget = "sulfamethoxazol" |> SubstanceLimitTarget
                                        AdjustUnit = su |> Some
                                        QuantityAdjust =
                                            MinMax.createInclIncl
                                                (27N |> ValueUnit.singleWithUnit (su |> ValueUnit.per au))
                                                (30N |> ValueUnit.singleWithUnit (su |> ValueUnit.per au))
                                    }
                                    |> Some
                            }
                            { Medication.substanceItem with
                                Name = "trimethoprim"
                                Concentrations = [| 8N; 80N; 160N |] |> ValueUnit.withUnit cu |> Some
                                Dose =
                                    { DoseLimit.limit with
                                        DoseLimitTarget = "trimethoprim" |> SubstanceLimitTarget
                                        AdjustUnit = su |> Some
                                        QuantityAdjust =
                                            MinMax.createInclIncl
                                                (6N - 6N / 10N |> ValueUnit.singleWithUnit (su |> ValueUnit.per au))
                                                (6N |> ValueUnit.singleWithUnit (su |> ValueUnit.per au))
                                    }
                                    |> Some
                            }
                        ]
                }
            ]
        Route = "or"
        OrderType = DiscontinuousOrder
        Frequencies = [| 2N |] |> ValueUnit.withUnit (Units.Count.times |> ValueUnit.per tu) |> Some
        Adjust = 10N |> ValueUnit.singleWithUnit au |> Some
        DoseCount = 1N |> ValueUnit.singleWithUnit Units.Count.times |> MinMax.createExact
        Dose =
            { DoseLimit.limit with
                DoseLimitTarget = OrderableLimitTarget
                AdjustUnit = au |> Some
                QuantityAdjust =
                    { MinMax.empty with
                        Max =
                            10N
                            |> ValueUnit.singleWithUnit (fu |> ValueUnit.per au)
                            |> Limit.inclusive
                            |> Some
                    }
            }
            |> Some
    }


let tpnComplete =
    { Medication.template with
        Id = "f1adf475-919b-4b7d-9e26-6cc502b88e42"
        Name = "samenstelling c"
        Route = "INTRAVENEUS"
        OrderType = TimedOrder
        Adjust = 11N |> ValueUnit.singleWithUnit Units.Weight.kiloGram |> Some
        Frequencies =
            1N
            |> ValueUnit.singleWithUnit (Units.Count.times |> ValueUnit.per Units.Time.day)
            |> Some
        Time =
            { MinMax.empty with
                Min = 20N |> ValueUnit.singleWithUnit Units.Time.hour |> Limit.inclusive |> Some
                Max = 24N |> ValueUnit.singleWithUnit Units.Time.hour |> Limit.inclusive |> Some
            }
        Dose =
            { DoseLimit.limit with
                DoseLimitTarget = OrderableLimitTarget
                AdjustUnit = Units.Weight.kiloGram |> Some
                QuantityAdjust =
                    { MinMax.empty with
                        Max =
                            (755N / 10N)
                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> ValueUnit.per Units.Weight.kiloGram)
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
                { Medication.productComponent with
                    Name = "Samenstelling C"
                    Form = "vloeistof"
                    Quantities = 1N |> ValueUnit.singleWithUnit Units.Volume.milliLiter |> Some
                    Divisible = Some(1N)
                    Dose =
                        { DoseLimit.limit with
                            DoseLimitTarget = "Samenstelling C" |> ComponentLimitTarget
                            AdjustUnit = Units.Weight.kiloGram |> Some
                            QuantityAdjust =
                                { MinMax.empty with
                                    Min =
                                        10N
                                        |> ValueUnit.singleWithUnit (
                                            Units.Volume.milliLiter |> ValueUnit.per Units.Weight.kiloGram
                                        )
                                        |> Limit.inclusive
                                        |> Some
                                    Max =
                                        25N
                                        |> ValueUnit.singleWithUnit (
                                            Units.Volume.milliLiter |> ValueUnit.per Units.Weight.kiloGram
                                        )
                                        |> Limit.inclusive
                                        |> Some
                                }
                        }
                        |> Some
                    Substances =
                        [
                            { Medication.substanceItem with
                                Name = "energie"
                                Concentrations =
                                    (32N / 100N)
                                    |> ValueUnit.singleWithUnit (
                                        Units.Energy.kiloCalorie |> ValueUnit.per Units.Volume.milliLiter
                                    )
                                    |> Some
                            }
                            { Medication.substanceItem with
                                Name = "eiwit"
                                Concentrations =
                                    (8N / 100N)
                                    |> ValueUnit.singleWithUnit (
                                        Units.Mass.gram |> ValueUnit.per Units.Volume.milliLiter
                                    )
                                    |> Some
                                Solution =
                                    { SolutionLimit.limit with
                                        SolutionLimitTarget = "eiwit" |> SubstanceLimitTarget
                                        Concentration =
                                            { MinMax.empty with
                                                Max =
                                                    5N / 100N
                                                    |> ValueUnit.singleWithUnit (
                                                        Units.Mass.gram |> ValueUnit.per Units.Volume.milliLiter
                                                    )
                                                    |> Limit.inclusive
                                                    |> Some
                                            }
                                    }
                                    |> Some
                            }
                            { Medication.substanceItem with
                                Name = "natrium"
                                Concentrations =
                                    (1N / 100N)
                                    |> ValueUnit.singleWithUnit (
                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                    )
                                    |> Some
                                Solution =
                                    { SolutionLimit.limit with
                                        SolutionLimitTarget = "natrium" |> LimitTarget.SubstanceLimitTarget
                                        Concentration =
                                            { MinMax.empty with
                                                Max =
                                                    (5N / 10N)
                                                    |> ValueUnit.singleWithUnit (
                                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                                    )
                                                    |> Limit.inclusive
                                                    |> Some
                                            }
                                    }
                                    |> Some
                            }
                            { Medication.substanceItem with
                                Name = "kalium"
                                Concentrations =
                                    (2N / 100N)
                                    |> ValueUnit.singleWithUnit (
                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                    )
                                    |> Some
                                Solution =
                                    { SolutionLimit.limit with
                                        SolutionLimitTarget = "kalium" |> LimitTarget.SubstanceLimitTarget
                                        Concentration =
                                            { MinMax.empty with
                                                Max =
                                                    (5N / 10N)
                                                    |> ValueUnit.singleWithUnit (
                                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                                    )
                                                    |> Limit.inclusive
                                                    |> Some
                                            }
                                    }
                                    |> Some
                            }
                            { Medication.substanceItem with
                                Name = "calcium"
                                Concentrations =
                                    (3N / 100N)
                                    |> ValueUnit.singleWithUnit (
                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                    )
                                    |> Some
                            }
                            { Medication.substanceItem with
                                Name = "fosfaat"
                                Concentrations =
                                    (2N / 100N)
                                    |> ValueUnit.singleWithUnit (
                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                    )
                                    |> Some
                            }
                            { Medication.substanceItem with
                                Name = "magnesium"
                                Concentrations =
                                    (1N / 100N)
                                    |> ValueUnit.singleWithUnit (
                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                    )
                                    |> Some
                            }
                            { Medication.substanceItem with
                                Name = "chloor"
                                Concentrations =
                                    (7N / 100N)
                                    |> ValueUnit.singleWithUnit (
                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                    )
                                    |> Some
                            }
                        ]
                }
                // NaCl 3% component
                { Medication.productComponent with
                    Name = "NaCl 3%"
                    Form = "vloeistof"
                    Quantities = 1N |> ValueUnit.singleWithUnit Units.Volume.milliLiter |> Some
                    Divisible = Some(1N)
                    Dose =
                        { DoseLimit.limit with
                            DoseLimitTarget = "NaCl 3%" |> LimitTarget.ComponentLimitTarget
                            AdjustUnit = Units.Weight.kiloGram |> Some
                            QuantityAdjust =
                                { MinMax.empty with
                                    Min =
                                        6N
                                        |> ValueUnit.singleWithUnit (
                                            Units.Volume.milliLiter |> ValueUnit.per Units.Weight.kiloGram
                                        )
                                        |> Limit.inclusive
                                        |> Some
                                    Max =
                                        6N
                                        |> ValueUnit.singleWithUnit (
                                            Units.Volume.milliLiter |> ValueUnit.per Units.Weight.kiloGram
                                        )
                                        |> Limit.inclusive
                                        |> Some
                                }
                        }
                        |> Some
                    Substances =
                        [
                            { Medication.substanceItem with
                                Name = "natrium"
                                Concentrations =
                                    (5N / 10N)
                                    |> ValueUnit.singleWithUnit (
                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                    )
                                    |> Some
                                Solution =
                                    { SolutionLimit.limit with
                                        SolutionLimitTarget = "natrium" |> LimitTarget.SubstanceLimitTarget
                                        Concentration =
                                            { MinMax.empty with
                                                Max =
                                                    (5N / 10N)
                                                    |> ValueUnit.singleWithUnit (
                                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                                    )
                                                    |> Limit.inclusive
                                                    |> Some
                                            }
                                    }
                                    |> Some
                            }
                            { Medication.substanceItem with
                                Name = "chloor"
                                Concentrations =
                                    (5N / 10N)
                                    |> ValueUnit.singleWithUnit (
                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                    )
                                    |> Some
                            }
                        ]
                }
                // KCl 7,4% component
                { Medication.productComponent with
                    Name = "KCl 7,4%"
                    Form = "vloeistof"
                    Quantities = 1N |> ValueUnit.singleWithUnit Units.Volume.milliLiter |> Some
                    Divisible = Some(1N)
                    Dose =
                        { DoseLimit.limit with
                            DoseLimitTarget = "KCl 7,4%" |> LimitTarget.ComponentLimitTarget
                            AdjustUnit = Units.Weight.kiloGram |> Some
                            QuantityAdjust =
                                { MinMax.empty with
                                    Min =
                                        2N
                                        |> ValueUnit.singleWithUnit (
                                            Units.Volume.milliLiter |> ValueUnit.per Units.Weight.kiloGram
                                        )
                                        |> Limit.inclusive
                                        |> Some
                                    Max =
                                        2N
                                        |> ValueUnit.singleWithUnit (
                                            Units.Volume.milliLiter |> ValueUnit.per Units.Weight.kiloGram
                                        )
                                        |> Limit.inclusive
                                        |> Some
                                }
                        }
                        |> Some
                    Substances =
                        [
                            { Medication.substanceItem with
                                Name = "kalium"
                                Concentrations =
                                    1N
                                    |> ValueUnit.singleWithUnit (
                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                    )
                                    |> Some
                                Solution =
                                    { SolutionLimit.limit with
                                        SolutionLimitTarget = "kalium" |> LimitTarget.SubstanceLimitTarget
                                        Concentration =
                                            { MinMax.empty with
                                                Max =
                                                    (5N / 10N)
                                                    |> ValueUnit.singleWithUnit (
                                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                                    )
                                                    |> Limit.inclusive
                                                    |> Some
                                            }
                                    }
                                    |> Some
                            }
                            { Medication.substanceItem with
                                Name = "chloor"
                                Concentrations =
                                    1N
                                    |> ValueUnit.singleWithUnit (
                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                    )
                                    |> Some
                            }
                        ]
                }
                // gluc 10% component
                { Medication.productComponent with
                    Name = "gluc 10%"
                    Form = "vloeistof"
                    Quantities = 1N |> ValueUnit.singleWithUnit Units.Volume.milliLiter |> Some
                    Divisible = Some(1N)
                    Substances =
                        [
                            { Medication.substanceItem with
                                Name = "energie"
                                Concentrations =
                                    (4N / 10N)
                                    |> ValueUnit.singleWithUnit (
                                        Units.Energy.kiloCalorie |> ValueUnit.per Units.Volume.milliLiter
                                    )
                                    |> Some
                            }
                            { Medication.substanceItem with
                                Name = "koolhydraat"
                                Concentrations =
                                    (1N / 10N)
                                    |> ValueUnit.singleWithUnit (
                                        Units.Mass.gram |> ValueUnit.per Units.Volume.milliLiter
                                    )
                                    |> Some
                            }
                        ]
                }
            ]
    }


let tpn =
    { Medication.template with
        Id = "f1adf475-919b-4b7d-9e26-6cc502b88e42"
        Name = "samenstelling c"
        Route = "INTRAVENEUS"
        OrderType = TimedOrder
        Adjust = 11N |> ValueUnit.singleWithUnit Units.Weight.kiloGram |> Some
        Frequencies =
            1N
            |> ValueUnit.singleWithUnit (Units.Count.times |> ValueUnit.per Units.Time.day)
            |> Some
        Time =
            { MinMax.empty with
                Min = 20N |> ValueUnit.singleWithUnit Units.Time.hour |> Limit.inclusive |> Some
                Max = 24N |> ValueUnit.singleWithUnit Units.Time.hour |> Limit.inclusive |> Some
            }
        Dose =
            { DoseLimit.limit with
                DoseLimitTarget = OrderableLimitTarget
                AdjustUnit = Units.Weight.kiloGram |> Some
                QuantityAdjust =
                    { MinMax.empty with
                        Max =
                            (755N / 10N)
                            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> ValueUnit.per Units.Weight.kiloGram)
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
                { Medication.productComponent with
                    Name = "Samenstelling C"
                    Form = "vloeistof"
                    Quantities = 1N |> ValueUnit.singleWithUnit Units.Volume.milliLiter |> Some
                    Divisible = Some(1N)
                    Dose =
                        { DoseLimit.limit with
                            DoseLimitTarget = "Samenstelling C" |> LimitTarget.ComponentLimitTarget
                            AdjustUnit = Units.Weight.kiloGram |> Some
                            QuantityAdjust =
                                { MinMax.empty with
                                    Min =
                                        10N
                                        |> ValueUnit.singleWithUnit (
                                            Units.Volume.milliLiter |> ValueUnit.per Units.Weight.kiloGram
                                        )
                                        |> Limit.inclusive
                                        |> Some
                                    Max =
                                        25N
                                        |> ValueUnit.singleWithUnit (
                                            Units.Volume.milliLiter |> ValueUnit.per Units.Weight.kiloGram
                                        )
                                        |> Limit.inclusive
                                        |> Some
                                }
                        }
                        |> Some
                    Substances =
                        [
                            { Medication.substanceItem with
                                Name = "eiwit"
                                Concentrations =
                                    (8N / 100N)
                                    |> ValueUnit.singleWithUnit (
                                        Units.Mass.gram |> ValueUnit.per Units.Volume.milliLiter
                                    )
                                    |> Some
                                Solution =
                                    { SolutionLimit.limit with
                                        SolutionLimitTarget = "eiwit" |> LimitTarget.SubstanceLimitTarget
                                        Concentration =
                                            { MinMax.empty with
                                                Max =
                                                    (5N / 100N)
                                                    |> ValueUnit.singleWithUnit (
                                                        Units.Mass.gram |> ValueUnit.per Units.Volume.milliLiter
                                                    )
                                                    |> Limit.inclusive
                                                    |> Some
                                            }
                                    }
                                    |> Some
                            }
                            { Medication.substanceItem with
                                Name = "natrium"
                                Concentrations =
                                    (1N / 100N)
                                    |> ValueUnit.singleWithUnit (
                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                    )
                                    |> Some
                                Solution =
                                    { SolutionLimit.limit with
                                        SolutionLimitTarget = "natrium" |> LimitTarget.SubstanceLimitTarget
                                        Concentration =
                                            { MinMax.empty with
                                                Max =
                                                    (5N / 10N)
                                                    |> ValueUnit.singleWithUnit (
                                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                                    )
                                                    |> Limit.inclusive
                                                    |> Some
                                            }
                                    }
                                    |> Some
                            }
                            { Medication.substanceItem with
                                Name = "kalium"
                                Concentrations =
                                    (2N / 100N)
                                    |> ValueUnit.singleWithUnit (
                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                    )
                                    |> Some
                                Solution =
                                    { SolutionLimit.limit with
                                        SolutionLimitTarget = "kalium" |> LimitTarget.SubstanceLimitTarget
                                        Concentration =
                                            { MinMax.empty with
                                                Max =
                                                    (5N / 10N)
                                                    |> ValueUnit.singleWithUnit (
                                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                                    )
                                                    |> Limit.inclusive
                                                    |> Some
                                            }
                                    }
                                    |> Some
                            }
                        ]
                }
                // NaCl 3% component
                { Medication.productComponent with
                    Name = "NaCl 3%"
                    Form = "vloeistof"
                    Quantities = 1N |> ValueUnit.singleWithUnit Units.Volume.milliLiter |> Some
                    Divisible = Some(1N)
                    Dose =
                        { DoseLimit.limit with
                            DoseLimitTarget = "NaCl 3%" |> LimitTarget.ComponentLimitTarget
                            AdjustUnit = Units.Weight.kiloGram |> Some
                            QuantityAdjust =
                                { MinMax.empty with
                                    Min =
                                        6N
                                        |> ValueUnit.singleWithUnit (
                                            Units.Volume.milliLiter |> ValueUnit.per Units.Weight.kiloGram
                                        )
                                        |> Limit.inclusive
                                        |> Some
                                    Max =
                                        6N
                                        |> ValueUnit.singleWithUnit (
                                            Units.Volume.milliLiter |> ValueUnit.per Units.Weight.kiloGram
                                        )
                                        |> Limit.inclusive
                                        |> Some
                                }
                        }
                        |> Some
                    Substances =
                        [
                            { Medication.substanceItem with
                                Name = "natrium"
                                Concentrations =
                                    (5N / 10N)
                                    |> ValueUnit.singleWithUnit (
                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                    )
                                    |> Some
                                Solution =
                                    { SolutionLimit.limit with
                                        SolutionLimitTarget = "natrium" |> LimitTarget.SubstanceLimitTarget
                                        Concentration =
                                            { MinMax.empty with
                                                Max =
                                                    (5N / 10N)
                                                    |> ValueUnit.singleWithUnit (
                                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                                    )
                                                    |> Limit.inclusive
                                                    |> Some
                                            }
                                    }
                                    |> Some
                            }
                        ]
                }
                // KCl 7,4% component
                { Medication.productComponent with
                    Name = "KCl 7,4%"
                    Form = "vloeistof"
                    Quantities = 1N |> ValueUnit.singleWithUnit Units.Volume.milliLiter |> Some
                    Divisible = Some(1N)
                    Dose =
                        { DoseLimit.limit with
                            DoseLimitTarget = "KCl 7,4%" |> LimitTarget.ComponentLimitTarget
                            AdjustUnit = Units.Weight.kiloGram |> Some
                            QuantityAdjust =
                                { MinMax.empty with
                                    Min =
                                        2N
                                        |> ValueUnit.singleWithUnit (
                                            Units.Volume.milliLiter |> ValueUnit.per Units.Weight.kiloGram
                                        )
                                        |> Limit.inclusive
                                        |> Some
                                    Max =
                                        2N
                                        |> ValueUnit.singleWithUnit (
                                            Units.Volume.milliLiter |> ValueUnit.per Units.Weight.kiloGram
                                        )
                                        |> Limit.inclusive
                                        |> Some
                                }
                        }
                        |> Some
                    Substances =
                        [
                            { Medication.substanceItem with
                                Name = "kalium"
                                Concentrations =
                                    1N
                                    |> ValueUnit.singleWithUnit (
                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                    )
                                    |> Some
                                Solution =
                                    { SolutionLimit.limit with
                                        SolutionLimitTarget = "kalium" |> LimitTarget.SubstanceLimitTarget
                                        Concentration =
                                            { MinMax.empty with
                                                Max =
                                                    (5N / 10N)
                                                    |> ValueUnit.singleWithUnit (
                                                        Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                                    )
                                                    |> Limit.inclusive
                                                    |> Some
                                            }
                                    }
                                    |> Some
                            }
                        ]
                }
                // gluc 10% component
                { Medication.productComponent with
                    Name = "gluc 10%"
                    Form = "vloeistof"
                    Quantities = 1N |> ValueUnit.singleWithUnit Units.Volume.milliLiter |> Some
                    Divisible = Some(1N)
                    Substances =
                        [
                            { Medication.substanceItem with
                                Name = "koolhydraat"
                                Concentrations =
                                    (1N / 10N)
                                    |> ValueUnit.singleWithUnit (
                                        Units.Mass.gram |> ValueUnit.per Units.Volume.milliLiter
                                    )
                                    |> Some
                            }
                        ]
                }
            ]
        Quantities = None
    }


/// Expected text output for the fully populated medication scenario
let fullMedicationText =
    """
Id: full-med-test-001
Name: Test Medication Complete
Quantity: 10 - 100 mL
Quantities: 50 mL
Route: INTRAVENEUS
OrderType: TimedOrder
Adjust: 15 kg
Frequencies: 1;2 x/dag
Time: 30 - 120 min
Dose: 5 - 10 mL/dosis
Div: 2
DoseCount: 1 - 2 x
Components:
	Name: Main Component
	Form: infuusvloeistof
	Quantities: 1 mL
	Divisible: 10
	Dose: Main Component, 1 - 5 mL/kg/dosis
	Solution: 10 - 50 mL
	Substances:

		Name: Active Substance
		Concentrations: 10 mg/mL
		Dose: Active Substance, 1 - 10 mg/kg/dag
		Solution: 0,5 - 2 mg/mL
	Name: Diluent Component
	Form: vloeistof
	Quantities: 1 mL
	Divisible: 1
	Dose:
	Solution:
	Substances:

		Name: sodium
		Concentrations: 0,15 mmol/mL
		Dose:
		Solution:
"""


// Kaliumchloride OnceTimed scenario that previously caused
// ValueSetOverflow (746,839 values) without staged expansion
let kaliumchlorideOnceTimedText =
    """
Id: c829b3ef-0dbe-4ed4-ac36-1260414d39b5
Name: kaliumchloride
Quantity:
Quantities:
Route: INTRAVENEUS
OrderType: OnceTimedOrder
Adjust: 31 kg
Frequencies:
Time: 60 min - 120 min
Dose: [dun] ml
Div:
DoseCount: 1 x
Components:

	Name: kaliumchloride
	Form: concentraat voor oplossing voor infusie
	Quantities: 1 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: kalium
		Quantities:
		Concentrations: 1 mmol/ml
		Dose:
		Solution:

		Name: chloor
		Quantities:
		Concentrations: 1 mmol/ml
		Dose:
		Solution:

		Name: kaliumchloride
		Quantities:
		Concentrations: 1 mmol/ml
		Dose: kaliumchloride, [dun] mmol, [qty-adj] 0.5 mmol/kg/dosis
		Solution:  [conc] 0.5 mmol/ml

	Name: gluc 5%
	Form: vloeistof
	Quantities: 1 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: glucose
		Quantities:
		Concentrations: 0.05 g/ml
		Dose:
		Solution:

		Name: energie
		Quantities:
		Concentrations: 0.2 kCal/ml
		Dose:
		Solution:

		Name: koolhydraat
		Quantities:
		Concentrations: 0.05 g/ml
		Dose:
		Solution:
"""


/// Amoxicilline/clavulaanzuur powder diluted with NaCl 0,9%: two components, and two items in the
/// first whose concentrations stand in a fixed ratio of ten to one.
let amoxClavText =
    """
Id: 303000f0-98b7-4db5-a0f2-a8a7642df9fc
Name: amoxicilline/clavulaanzuur
Quantity:
Quantities:
Route: INTRAVENEUS
OrderType: DiscontinuousOrder
Adjust: 14.5 kg
Frequencies: 3 x/day
Time:
Dose: [dun] ml
Div:
DoseCount: 1 x
Components:

	Name: amoxicilline/clavulaanzuur
	Form: poeder voor injectievloeistof
	Quantities: 10;20 ml
	Divisible: 10
	Dose:
	Solution:
	Product: [id] amox-clav-500-50 [qts] 10 ml [amoxicilline] 50 mg/ml [clavulaanzuur] 5 mg/ml
	Product: [id] amox-clav-1000-200 [qts] 20 ml [amoxicilline] 50 mg/ml [clavulaanzuur] 10 mg/ml
	Substances:

		Name: amoxicilline
		Quantities: 500;1000 mg
		Concentrations: 50 mg/ml
		Dose: amoxicilline, [dun] mg, [per-time-adj] 100 mg/kg/day, [per-time] max 6000 mg/day
		Solution:  [conc] 20 mg/ml

		Name: clavulaanzuur
		Quantities: 50;100;200 mg
		Concentrations: 5;10 mg/ml
		Dose: clavulaanzuur, [dun] mg, [per-time-adj] 10 mg/kg/day, [per-time] max 600 mg/day
		Solution:

	Name: NaCl 0,9%
	Form: vloeistof
	Quantities: 1 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: natrium
		Quantities:
		Concentrations: 0.155 mmol/ml
		Dose:
		Solution:

		Name: chloor
		Quantities:
		Concentrations: 0.155 mmol/ml
		Dose:
		Solution:
"""


/// Amoxicilline/clavulaanzuur without the dose limit of the clavulaanzuur. With it, the ratio of the
/// clavulaanzuur dose to the amoxicilline dose leaves 5 mg/mL alone; without it both strengths, 5
/// and 10 mg/mL, survive the solve.
let amoxClavWithoutClavDoseText =
    """
Id: 303000f0-98b7-4db5-a0f2-a8a7642df9fc
Name: amoxicilline/clavulaanzuur
Quantity:
Quantities:
Route: INTRAVENEUS
OrderType: DiscontinuousOrder
Adjust: 14.5 kg
Frequencies: 3 x/day
Time:
Dose: [dun] ml
Div:
DoseCount: 1 x
Components:

	Name: amoxicilline/clavulaanzuur
	Form: poeder voor injectievloeistof
	Quantities: 10;20 ml
	Divisible: 10
	Dose:
	Solution:
	Product: [id] amox-clav-500-50 [qts] 10 ml [amoxicilline] 50 mg/ml [clavulaanzuur] 5 mg/ml
	Product: [id] amox-clav-1000-200 [qts] 20 ml [amoxicilline] 50 mg/ml [clavulaanzuur] 10 mg/ml
	Substances:

		Name: amoxicilline
		Quantities: 500;1000 mg
		Concentrations: 50 mg/ml
		Dose: amoxicilline, [dun] mg, [per-time-adj] 100 mg/kg/day, [per-time] max 6000 mg/day
		Solution:  [conc] 20 mg/ml

		Name: clavulaanzuur
		Quantities: 50;100;200 mg
		Concentrations: 5;10 mg/ml
		Dose:
		Solution:

	Name: NaCl 0,9%
	Form: vloeistof
	Quantities: 1 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: natrium
		Quantities:
		Concentrations: 0.155 mmol/ml
		Dose:
		Solution:

		Name: chloor
		Quantities:
		Concentrations: 0.155 mmol/ml
		Dose:
		Solution:
"""


/// Amoxicilline/clavulaanzuur without the dose limit of the clavulaanzuur in four products, one for
/// every combination of 25 or 50 mg/mL amoxicilline with 5 or 10 mg/mL clavulaanzuur.
let amoxClavEveryCombinationText =
    """
Id: 303000f0-98b7-4db5-a0f2-a8a7642df9fc
Name: amoxicilline/clavulaanzuur
Quantity:
Quantities:
Route: INTRAVENEUS
OrderType: DiscontinuousOrder
Adjust: 14.5 kg
Frequencies: 3 x/day
Time:
Dose: [dun] ml
Div:
DoseCount: 1 x
Components:

	Name: amoxicilline/clavulaanzuur
	Form: poeder voor injectievloeistof
	Quantities: 10;20 ml
	Divisible: 10
	Dose:
	Solution:
	Product: [id] amox-clav-500-50 [qts] 10 ml [amoxicilline] 50 mg/ml [clavulaanzuur] 5 mg/ml
	Product: [id] amox-clav-250-50 [qts] 10 ml [amoxicilline] 25 mg/ml [clavulaanzuur] 5 mg/ml
	Product: [id] amox-clav-500-200 [qts] 20 ml [amoxicilline] 25 mg/ml [clavulaanzuur] 10 mg/ml
	Product: [id] amox-clav-1000-200 [qts] 20 ml [amoxicilline] 50 mg/ml [clavulaanzuur] 10 mg/ml
	Substances:

		Name: amoxicilline
		Quantities: 250;500;1000 mg
		Concentrations: 25;50 mg/ml
		Dose: amoxicilline, [dun] mg, [per-time-adj] 100 mg/kg/day, [per-time] max 6000 mg/day
		Solution:  [conc] 20 mg/ml

		Name: clavulaanzuur
		Quantities: 50;100;200 mg
		Concentrations: 5;10 mg/ml
		Dose:
		Solution:

	Name: NaCl 0,9%
	Form: vloeistof
	Quantities: 1 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: natrium
		Quantities:
		Concentrations: 0.155 mmol/ml
		Dose:
		Solution:

		Name: chloor
		Quantities:
		Concentrations: 0.155 mmol/ml
		Dose:
		Solution:
"""


/// A 5 mg prednisolon tablet dividable in quarters, 1 to 2 mg/kg for 30 kg: 6 to 12 tablets on a
/// quarter grid, 25 values.
let prednisolonText =
    """
Id: 2b1e1c1a-0000-4000-8000-000000001302
Name: prednisolon
Quantity:
Quantities:
Route: ORAAL
OrderType: DiscontinuousOrder
Adjust: 30 kg
Frequencies: 1 x/dag
Time:
Dose:
Div:
DoseCount: 1 x
Components:
	Name: prednisolon
	Form: tablet
	Quantities: 1 stuk
	Divisible: 4
	Dose:
	Solution:
	Substances:

		Name: prednisolon
		Concentrations: 5 mg/stuk
		Dose: prednisolon, [qty-adj] 1 - 2 mg/kg/dosis
		Solution:
"""


/// Fully populated medication for testing all Medication fields
let fullMedication =
    let volUnit = Units.Volume.milliLiter
    let massUnit = Units.Mass.milliGram
    let weightUnit = Units.Weight.kiloGram
    let timeUnit = Units.Time.day
    let concUnit = massUnit |> ValueUnit.per volUnit
    let molarConcUnit = Units.Molar.milliMole |> ValueUnit.per volUnit

    { Medication.template with
        Id = "full-med-test-001"
        Name = "Test Medication Complete"
        Quantity =
            { MinMax.empty with
                Min = 10N |> ValueUnit.singleWithUnit volUnit |> Limit.inclusive |> Some
                Max = 100N |> ValueUnit.singleWithUnit volUnit |> Limit.inclusive |> Some
            }
        Quantities = 50N |> ValueUnit.singleWithUnit volUnit |> Some
        Route = "INTRAVENEUS"
        OrderType = TimedOrder
        Frequencies =
            [| 1N; 2N |]
            |> ValueUnit.withUnit (Units.Count.times |> ValueUnit.per timeUnit)
            |> Some
        Time =
            { MinMax.empty with
                Min = 30N |> ValueUnit.singleWithUnit Units.Time.minute |> Limit.inclusive |> Some
                Max = 120N |> ValueUnit.singleWithUnit Units.Time.minute |> Limit.inclusive |> Some
            }
        Dose =
            { DoseLimit.limit with
                DoseLimitTarget = OrderableLimitTarget
                Quantity =
                    { MinMax.empty with
                        Min = 5N |> ValueUnit.singleWithUnit volUnit |> Limit.inclusive |> Some
                        Max = 10N |> ValueUnit.singleWithUnit volUnit |> Limit.inclusive |> Some
                    }
            }
            |> Some
        Div = Some 2N
        DoseCount =
            { MinMax.empty with
                Min = 1N |> ValueUnit.singleWithUnit Units.Count.times |> Limit.inclusive |> Some
                Max = 2N |> ValueUnit.singleWithUnit Units.Count.times |> Limit.inclusive |> Some
            }
        Adjust = 15N |> ValueUnit.singleWithUnit weightUnit |> Some
        Components =
            [
                // Main component with all fields populated
                { Medication.productComponent with
                    Name = "Main Component"
                    Form = "infuusvloeistof"
                    Quantities = 1N |> ValueUnit.singleWithUnit volUnit |> Some
                    Divisible = Some 10N
                    Dose =
                        { DoseLimit.limit with
                            DoseLimitTarget = "Main Component" |> ComponentLimitTarget
                            AdjustUnit = weightUnit |> Some
                            QuantityAdjust =
                                { MinMax.empty with
                                    Min =
                                        1N
                                        |> ValueUnit.singleWithUnit (volUnit |> ValueUnit.per weightUnit)
                                        |> Limit.inclusive
                                        |> Some
                                    Max =
                                        5N
                                        |> ValueUnit.singleWithUnit (volUnit |> ValueUnit.per weightUnit)
                                        |> Limit.inclusive
                                        |> Some
                                }
                        }
                        |> Some
                    Solution =
                        { SolutionLimit.limit with
                            SolutionLimitTarget = "Main Component" |> ComponentLimitTarget
                            Quantity =
                                { MinMax.empty with
                                    Min = 10N |> ValueUnit.singleWithUnit volUnit |> Limit.inclusive |> Some
                                    Max = 50N |> ValueUnit.singleWithUnit volUnit |> Limit.inclusive |> Some
                                }
                        }
                        |> Some
                    Substances =
                        [
                            { Medication.substanceItem with
                                Name = "Active Substance"
                                Concentrations = 10N |> ValueUnit.singleWithUnit concUnit |> Some
                                Dose =
                                    { DoseLimit.limit with
                                        DoseLimitTarget = "Active Substance" |> SubstanceLimitTarget
                                        AdjustUnit = massUnit |> Some
                                        PerTimeAdjust =
                                            { MinMax.empty with
                                                Min =
                                                    1N
                                                    |> ValueUnit.singleWithUnit (
                                                        massUnit |> ValueUnit.per weightUnit |> ValueUnit.per timeUnit
                                                    )
                                                    |> Limit.inclusive
                                                    |> Some
                                                Max =
                                                    10N
                                                    |> ValueUnit.singleWithUnit (
                                                        massUnit |> ValueUnit.per weightUnit |> ValueUnit.per timeUnit
                                                    )
                                                    |> Limit.inclusive
                                                    |> Some
                                            }
                                    }
                                    |> Some
                                Solution =
                                    { SolutionLimit.limit with
                                        SolutionLimitTarget = "Active Substance" |> SubstanceLimitTarget
                                        Concentration =
                                            { MinMax.empty with
                                                Min =
                                                    (5N / 10N)
                                                    |> ValueUnit.singleWithUnit concUnit
                                                    |> Limit.inclusive
                                                    |> Some
                                                Max = 2N |> ValueUnit.singleWithUnit concUnit |> Limit.inclusive |> Some
                                            }
                                    }
                                    |> Some
                            }
                        ]
                }
                // Second component (diluent) for completeness
                { Medication.productComponent with
                    Name = "Diluent Component"
                    Form = "vloeistof"
                    Quantities = 1N |> ValueUnit.singleWithUnit volUnit |> Some
                    Divisible = Some 1N
                    Substances =
                        [
                            { Medication.substanceItem with
                                Name = "sodium"
                                Concentrations = (154N / 1000N) |> ValueUnit.singleWithUnit molarConcUnit |> Some
                            }
                        ]
                }
            ]
    }


/// The medication with the substances of the named component changed by f.
let mapSubstances cmpName f (med: Types.Medication) =
    { med with
        Components =
            med.Components
            |> List.map (fun c ->
                if c.Name = cmpName then
                    { c with Substances = c.Substances |> List.map f }
                else
                    c
            )
    }


/// The medication with a third component, NaCl 0,9%, a copy of its last component with only natrium
/// 0,154 mmol/mL and no limits of its own.
let withNaCl (med: Types.Medication) =
    let last = med.Components |> List.last

    let natrium =
        { Medication.substanceItem with
            Name = "natrium"
            Concentrations =
                (154N / 1000N)
                |> ValueUnit.singleWithUnit (Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter)
                |> Some
        }

    { med with
        Components =
            med.Components
            @ [
                { last with
                    Name = "NaCl 0,9%"
                    Dose = None
                    Substances = [ natrium ]
                }
            ]
    }


/// The tpn with the kalium of KCl 7,4% at most 0,04 mmol/mL, below the 0,02 to 0,06 mmol/mL the
/// component allows: a concentration limit that binds.
let tpnKaliumConcentrationBinding =
    tpn
    |> mapSubstances
        "KCl 7,4%"
        (fun sub ->
            match sub.Solution with
            | Some sl when sub.Name = "kalium" ->
                { sub with
                    Solution =
                        { sl with
                            Concentration =
                                { sl.Concentration with
                                    Max =
                                        (4N / 100N)
                                        |> ValueUnit.singleWithUnit (
                                            Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter
                                        )
                                        |> Limit.inclusive
                                        |> Some
                                }
                        }
                        |> Some
                }
            | _ -> sub
        )


/// The tpn with the kalium of KCl 7,4% at most 0,09 mmol/kg/uur, where its 2 mL/kg over 20 to 24 uur
/// runs at 0,083 to 0,1 mmol/kg/uur: an item rate limit that binds.
let tpnKaliumRateBinding =
    let du =
        Units.Molar.milliMole
        |> ValueUnit.per Units.Weight.kiloGram
        |> ValueUnit.per Units.Time.hour

    tpn
    |> mapSubstances
        "KCl 7,4%"
        (fun sub ->
            if sub.Name = "kalium" then
                { sub with
                    Dose =
                        { DoseLimit.limit with
                            DoseLimitTarget = "kalium" |> LimitTarget.SubstanceLimitTarget
                            AdjustUnit = Units.Weight.kiloGram |> Some
                            RateAdjust =
                                { MinMax.empty with
                                    Max = (9N / 100N) |> ValueUnit.singleWithUnit du |> Limit.inclusive |> Some
                                }
                        }
                        |> Some
                }
            else
                sub
        )


/// The morfCont with NaCl 0,9%: continuous with three components, the morfine in two strengths. The
/// natrium is fixed at 1,54 mmol, 10 mL, so the two diluents do not offer each other every volume.
let morfContWithNaCl =
    morfCont
    |> withNaCl
    |> mapSubstances
        "NaCl 0,9%"
        (fun sub ->
            { sub with
                Solution =
                    { SolutionLimit.limit with
                        SolutionLimitTarget = "natrium" |> SubstanceLimitTarget
                        Quantity =
                            (154N / 100N)
                            |> ValueUnit.singleWithUnit Units.Molar.milliMole
                            |> MinMax.createExact
                    }
                    |> Some
            }
        )


/// The morfCont with NaCl 0,9% and the morfine in one strength, 1 mg/mL.
let morfContWithNaClOneStrength =
    morfContWithNaCl
    |> mapSubstances
        "morfine"
        (fun sub ->
            if sub.Name = "morfin" then
                { sub with
                    Concentrations =
                        1N
                        |> ValueUnit.singleWithUnit (Units.Mass.milliGram |> ValueUnit.per Units.Volume.milliLiter)
                        |> Some
                }
            else
                sub
        )


/// The amfo with NaCl 0,9%, the amfotericine b liposomaal in two strengths, 2 and 4 mg/mL, as a timed
/// order over one to two hours.
let amfoWithNaClTwoStrengthsTimed =
    let med =
        amfo
        |> withNaCl
        |> mapSubstances
            "amfotericine b liposomaal"
            (fun sub ->
                if sub.Name = "amfotericine b liposomaal" then
                    { sub with
                        Concentrations =
                            [| 2N; 4N |]
                            |> ValueUnit.withUnit (Units.Mass.milliGram |> ValueUnit.per Units.Volume.milliLiter)
                            |> Some
                    }
                else
                    sub
            )

    { med with
        OrderType = TimedOrder
        Time =
            { MinMax.empty with
                Min = 1N |> ValueUnit.singleWithUnit Units.Time.hour |> Limit.inclusive |> Some
                Max = 2N |> ValueUnit.singleWithUnit Units.Time.hour |> Limit.inclusive |> Some
            }
    }


/// Amfo as a timed order over one to two hours: its dose quantity is a list until it is picked,
/// so its rate stays a range until then.
let amfoTimed =
    { amfo with
        OrderType = TimedOrder
        Time =
            { MinMax.empty with
                Min = 1N |> ValueUnit.singleWithUnit Units.Time.hour |> Limit.inclusive |> Some
                Max = 2N |> ValueUnit.singleWithUnit Units.Time.hour |> Limit.inclusive |> Some
            }
    }


/// morfCont with NaCl 0,9% as a timed order, once a day over 20 to 24 uur: its dose quantity has
/// one value, 50 mL, from the start, so its rate gets its list at once.
let morfTimedWithNaCl =
    { (morfCont |> withNaCl) with
        OrderType = TimedOrder
        Frequencies =
            Units.Count.times
            |> ValueUnit.per Units.Time.day
            |> ValueUnit.singleWithValue 1N
            |> Some
        Time =
            { MinMax.empty with
                Min = 20N |> ValueUnit.singleWithUnit Units.Time.hour |> Limit.inclusive |> Some
                Max = 24N |> ValueUnit.singleWithUnit Units.Time.hour |> Limit.inclusive |> Some
            }
    }


/// NaCl 0,9% for an adult: 1000 mL over 2 to 20 uur, a timed order whose rate runs from 50 to
/// 500 mL/uur, more values than a cap keeps.
let naclLargeRateText =
    """
Id: nacl-large-rate
Name: NaCl 0,9%
Quantity:
Quantities:
Route: INTRAVENEUS
OrderType: TimedOrder
Adjust: 70 kg
Frequencies: 1 x/dag
Time: 120 min - 1200 min
Dose: [dun] ml, [qty] 1000 ml/dosis
Div:
DoseCount: 1 x
Components:

	Name: NaCl 0,9%
	Form: infuusvloeistof
	Quantities: 1 ml
	Divisible: 1
	Dose:
	Solution:
	Substances:

		Name: natrium
		Quantities:
		Concentrations: 0.154 mmol/ml
		Dose:
		Solution:
"""
