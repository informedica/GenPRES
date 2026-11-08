namespace Informedica.GenCore.Lib.ValueUnits


module ValueUnit =

    open Informedica.Utils.Lib.BCL
    open FsToolkit.ErrorHandling

    open Informedica.GenCore.Lib
    open Informedica.GenUnits.Lib
    open Informedica.GenUnits.Lib.ValueUnit
    open Informedica.GenUnits.Lib.ValueUnit.Operators


    module Measures =


        let inline toMeasure (group: Group.Group) brTo toMeasure u s vu =
            if vu |> getGroup = group then
                vu ==> u |> getValue |> Array.map (brTo >> toMeasure) |> Ok
            else
                $"cannot convert valueunit: {vu |> toStringEngShort} to {s}" |> Error


        let toDecimalCm =
            toMeasure Group.HeightGroup BigRational.toDecimal Conversions.cmFromDecimal Units.Height.centiMeter "cm"

        let toIntCm =
            toMeasure Group.HeightGroup BigRational.ToInt32 Conversions.cmFromInt Units.Height.centiMeter "cm"

        let toDecimalMeter =
            toMeasure Group.HeightGroup BigRational.toDecimal Conversions.meterFromDecimal Units.Height.meter "m"

        let toIntGram =
            toMeasure Group.WeightGroup BigRational.ToInt32 Conversions.gramFromInt Units.Weight.gram "gram"

        let toDecimalKg =
            toMeasure Group.WeightGroup BigRational.toDecimal Conversions.kgFromDecimal Units.Weight.kiloGram "kg"

        let toIntDay = toMeasure Group.TimeGroup BigRational.ToInt32 Conversions.dayFromInt Units.Time.day "day"

        /// The single value of a time value in whole days, rounded down. Raises an
        /// ArgumentException for a value that is not a time.
        let toSingleIntDay vu =
            vu
            |> toIntDay
            |> Result.map Array.head
            |> Result.defaultWith (invalidArg (nameof vu))

        let toIntWeek =
            toMeasure Group.TimeGroup BigRational.ToInt32 Conversions.weekFromInt Units.Time.week "week"

        let toIntMonth =
            toMeasure Group.TimeGroup BigRational.ToInt32 Conversions.monthFromInt Units.Time.month "month"

        let toIntYear =
            toMeasure Group.TimeGroup BigRational.ToInt32 Conversions.yearFromInt Units.Time.year "year"


        let inline fromMeasure u conv v = v |> conv |> BigRational.fromDecimal |> singleWithUnit u


        let fromIntGram (v: int<gram>) = v |> fromMeasure Units.Weight.gram (int >> decimal)

        let fromDecimalM2 (v: decimal<bsa>) = v |> fromMeasure Units.BSA.m2 decimal

        let fromIntDay (v: int<day>) = v |> fromMeasure Units.Time.day (int >> decimal)

        let fromIntWeek (v: int<week>) = v |> fromMeasure Units.Time.week (int >> decimal)

        let fromIntMonth (v: int<month>) = v |> fromMeasure Units.Time.month (int >> decimal)

        let fromIntYear (v: int<year>) = v |> fromMeasure Units.Time.year (int >> decimal)


    module Calculations =


        let inline calc2 vu1To vu2To resultTo calculation vu1 vu2 =
            result {
                let! v1 = vu1 |> vu1To
                let! v2 = vu2 |> vu2To

                return
                    Array.allPairs v1 v2
                    |> Array.map (fun (v1, v2) -> calculation (Some 2) v1 v2 |> resultTo)
            }


        module BSA =


            let calcBSA method =
                calc2 Measures.toDecimalKg Measures.toDecimalCm Measures.fromDecimalM2 method


            let calcDuBois = calcBSA Calculations.BSA.calcDuBois

            let calcMosteller = calcBSA Calculations.BSA.calcMosteller

            let calcDuFujimoto = calcBSA Calculations.BSA.calcFujimoto

            let calcHaycock = calcBSA Calculations.BSA.calcHaycock

            let calcGehanAndGeorge = calcBSA Calculations.BSA.calcGehanAndGeorge
