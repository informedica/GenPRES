namespace Informedica.GenCore.Lib.ValueUnits


module ValueUnit =

    open System
    open MathNet.Numerics
    open FsToolkit.ErrorHandling

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenCore.Lib
    open Informedica.GenUnits.Lib
    open Informedica.GenUnits.Lib.ValueUnit
    open Informedica.GenUnits.Lib.ValueUnit.Operators


    module Measures =


        let inline toMeasure (group : Group.Group) brTo toMeasure u s vu =
            if vu |> getGroup = group then
                vu ==> u
                |> getValue
                |> Array.map (brTo >> toMeasure)
                |> Ok
            else
                $"cannot convert valueunit: {vu |> toStringEngShort} to {s}"
                |> Error


        let toDecimalCm =
            toMeasure
                Group.HeightGroup
                BigRational.toDecimal
                Conversions.cmFromDecimal
                Units.Height.centiMeter
                "cm"

        let toIntCm =
            toMeasure
                Group.HeightGroup
                BigRational.ToInt32
                Conversions.cmFromInt
                Units.Height.centiMeter
                "cm"

        let toDecimalMeter =
            toMeasure
                Group.HeightGroup
                BigRational.toDecimal
                Conversions.meterFromDecimal
                Units.Height.meter
                "m"

        let toIntGram =
            toMeasure
                Group.WeightGroup
                BigRational.ToInt32
                Conversions.gramFromInt
                Units.Weight.gram
                "gram"

        let toDecimalKg =
            toMeasure
                Group.WeightGroup
                BigRational.toDecimal
                Conversions.kgFromDecimal
                Units.Weight.kiloGram
                "kg"

        let toIntDay =
            toMeasure
                Group.TimeGroup
                BigRational.ToInt32
                Conversions.dayFromInt
                Units.Time.day
                "day"

        let toIntWeek =
            toMeasure
                Group.TimeGroup
                BigRational.ToInt32
                Conversions.weekFromInt
                Units.Time.week
                "week"

        let toIntMonth =
            toMeasure
                Group.TimeGroup
                BigRational.ToInt32
                Conversions.monthFromInt
                Units.Time.month
                "month"

        let toIntYear =
            toMeasure
                Group.TimeGroup
                BigRational.ToInt32
                Conversions.yearFromInt
                Units.Time.year
                "year"


        let inline fromMeasure u conv v =
            v
            |> conv
            |> BigRational.fromDecimal
            |> singleWithUnit u


        let fromIntGram (v : int<gram>) = v |> fromMeasure Units.Weight.gram (int >> decimal)

        let fromDecimalM2 (v : decimal<bsa>) = v |> fromMeasure Units.BSA.m2 decimal

        let fromIntDay (v : int<day>) = v |> fromMeasure Units.Time.day (int >> decimal)

        let fromIntWeek (v : int<week>) = v |> fromMeasure Units.Time.week (int >> decimal)

        let fromIntMonth (v : int<month>) = v |> fromMeasure Units.Time.month (int >> decimal)

        let fromIntYear (v : int<year>) = v |> fromMeasure Units.Time.year (int >> decimal)



    module Calculations =


        let inline calc2 vu1To vu2To resultTo calculation vu1 vu2 =
            result {
            let! v1 = vu1 |> vu1To
            let! v2 = vu2 |> vu2To

            return
                Array.allPairs v1 v2
                |> Array.map (fun (v1, v2) ->
                    calculation (Some 2) v1 v2
                    |> resultTo
                )
            }


        let inline calc3 vu1To vu2To vu3To resultTo calculation vu1 vu2 vu3 =
            result {
            if [vu1; vu2; vu3] |> List.forall isSingleValue |> not then
                return! Error "value unit arugments must be all single value"
            else
                let! v1 = vu1 |> vu1To
                let! v2 = vu2 |> vu2To
                let! v3 = vu3 |> vu3To

                let v1 = v1 |>  Array.item 0
                let v2 = v2 |>  Array.item 0
                let v3 = v3 |>  Array.item 0

                return!
                    calculation v1 v2 v3
                    |> resultTo
                    |> Ok
            }


        let inline calc4 vu1To vu2To vu3To vu4To resultTo calculation vu1 vu2 vu3 vu4 =
            result {
            if [vu1; vu2; vu3; vu4] |> List.forall isSingleValue |> not then
                return! Error "value unit arugments must be all single value"

            else
                let! v1 = vu1 |> vu1To
                let! v2 = vu2 |> vu2To
                let! v3 = vu3 |> vu3To
                let! v4 = vu4 |> vu4To

                let v1 = v1 |>  Array.item 0
                let v2 = v2 |>  Array.item 0
                let v3 = v3 |>  Array.item 0
                let v4 = v4 |>  Array.item 0


                return!
                    calculation v1 v2 v3 v4
                    |> resultTo
                    |> Ok
            }


        let inline calc4Opt vu1To vu2To vu3To vu4To resultTo calculation vu1 vu2 vu3 vu4 =
            result {
            if [vu1; vu2; vu3; vu4] |> List.forall (Option.map isSingleValue >> (Option.defaultValue true)) |> not then
                return! Error "value unit arugments must be all single value"

            else
                let! v1 = vu1 |> vu1To
                let! v2 = vu2 |> vu2To
                let! v3 = vu3 |> vu3To
                let! v4 = vu4 |> vu4To

                let v1 = v1 |>  Array.item 0
                let v2 = v2 |>  Array.item 0
                let v3 = v3 |>  Array.item 0
                let v4 = v4 |>  Array.item 0


                return!
                    calculation v1 v2 v3 v4
                    |> resultTo
                    |> Ok
            }


        module Age =


            let adjusted bd dt =
                calc2
                    Measures.toIntDay
                    Measures.toIntWeek
                    Measures.fromIntDay
                    (fun _ d w -> Calculations.Age.adjustedAge d w bd dt)


            let yearsMonthsWeeksDaysToDays =
                calc4
                    Measures.toIntYear
                    Measures.toIntMonth
                    Measures.toIntWeek
                    Measures.toIntDay
                    Measures.fromIntDay
                    Calculations.Age.yearsMonthsWeeksDaysToDays


            let fromBirthDate bd dt =
                let y, m, w, d = Calculations.Age.fromBirthDate bd dt
                y |> Measures.fromIntYear,
                m |> Measures.fromIntMonth,
                w |> Measures.fromIntWeek,
                d |> Measures.fromIntDay


            let toBirthDate dt =
                calc4
                    Measures.toIntYear
                    Measures.toIntMonth
                    Measures.toIntWeek
                    Measures.toIntDay
                    id
                    (Calculations.Age.toBirthDate dt)


            let postMenstrualAge actD gestW gestD =
                calc3
                    Measures.toIntDay
                    Measures.toIntWeek
                    Measures.toIntDay
                    (fun (v1, v2) -> v1 |> Measures.fromIntWeek, v2 |> Measures.fromIntDay)
                    Calculations.Age.postMenstrualAge


            let ageToString =

                let fYear = Option.map (Measures.toIntYear >> Result.map (Array.map Some)) >> Option.defaultValue (Ok [|None|])
                let fMonth = Option.map (Measures.toIntMonth >> Result.map (Array.map Some)) >> Option.defaultValue (Ok [|None|])
                let fWeek = Option.map (Measures.toIntWeek >> Result.map (Array.map Some)) >> Option.defaultValue (Ok [|None|])
                let fDay = Option.map (Measures.toIntDay >> Result.map (Array.map Some)) >> Option.defaultValue (Ok [|None|])

                calc4Opt fYear fMonth fWeek fDay id
                    Calculations.Age.ageToString


            let ageToStringNL =

                let fYear = Option.map (Measures.toIntYear >> Result.map (Array.map Some)) >> Option.defaultValue (Ok [|None|])
                let fMonth = Option.map (Measures.toIntMonth >> Result.map (Array.map Some)) >> Option.defaultValue (Ok [|None|])
                let fWeek = Option.map (Measures.toIntWeek >> Result.map (Array.map Some)) >> Option.defaultValue (Ok [|None|])
                let fDay = Option.map (Measures.toIntDay >> Result.map (Array.map Some)) >> Option.defaultValue (Ok [|None|])

                calc4Opt fYear fMonth fWeek fDay id
                    Calculations.Age.ageToStringNL


            let ageToStringNLShort =

                let fYear = Option.map (Measures.toIntYear >> Result.map (Array.map Some)) >> Option.defaultValue (Ok [|None|])
                let fMonth = Option.map (Measures.toIntMonth >> Result.map (Array.map Some)) >> Option.defaultValue (Ok [|None|])
                let fWeek = Option.map (Measures.toIntWeek >> Result.map (Array.map Some)) >> Option.defaultValue (Ok [|None|])
                let fDay = Option.map (Measures.toIntDay >> Result.map (Array.map Some)) >> Option.defaultValue (Ok [|None|])

                calc4Opt fYear fMonth fWeek fDay id
                    Calculations.Age.ageToStringNlShort



        module BSA =


            let calcBSA method =
                calc2
                    Measures.toDecimalKg
                    Measures.toDecimalCm
                    Measures.fromDecimalM2
                    method


            let calcDuBois = calcBSA Calculations.BSA.calcDuBois

            let calcMosteller = calcBSA Calculations.BSA.calcMosteller

            let calcDuFujimoto = calcBSA Calculations.BSA.calcFujimoto

            let calcHaycock = calcBSA Calculations.BSA.calcHaycock

            let calcGehanAndGeorge = calcBSA Calculations.BSA.calcGehanAndGeorge


