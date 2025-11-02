
#time

// load demo or product cache


System.Environment.SetEnvironmentVariable("GENPRES_DEBUG", "1")
System.Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
System.Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1s76xvQJXhfTpV15FuvTZfB-6pkkNTpSB30p51aAca8I")

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


let tpn = 
    { Medication.order with
        Id = "1"
        Name = "Samenstelling C"
        Route = "intraveneus"
        OrderType = TimedOrder
        Frequencies = 
            1N
            |> ValueUnit.singleWithUnit (Units.Count.times |> Units.per Units.Time.day)
            |> Some
        AdjustUnit = Units.Weight.kiloGram |> Some
        Adjust =
            10N 
            |> ValueUnit.singleWithUnit Units.Weight.kiloGram
            |> Some
        Dose = 
            { DoseLimit.limit with
                DoseLimitTarget = "vloeistof" |> LimitTarget.ShapeLimitTarget
                AdjustUnit = Units.Weight.kiloGram |> Some
            }
            |> Some
    }


tpn
|> Medication.toString
|> print
