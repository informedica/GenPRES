namespace Informedica.ZForm.Lib


/// Utility methods to extend the
/// `Informedica.GenUnits.Lib.ValueUnit` library
module ValueUnit =

    open MathNet.Numerics

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib

    open ValueUnit

    let unitFromString = Units.fromString

    let unitToString = Units.toString Units.Localization.English Units.Short


    let readableStringToWeightUnit s =
        $"%s{s}[Weight]"
        |> Units.fromString

    let readableStringToBSAUnit s =
        $"%s{s}[BSA]"
        |> Units.fromString

    let readableStringToTimeUnit s =
        $"%s{s}[Time]"
        |> Units.fromString


    //TODO: rewrite to new online mapping
    /// Create a unit from a GStand unit
    let unitFromZIndexString = Mapping.stringToUnit (Mapping.getUnitMapping ())


    /// Create a value unit using a specific mapping `m`
    /// with value `v` and unit `u`.
    let createValueUnit (d : decimal) u =
        let v = d |> float
        let u = u |> unitFromZIndexString

        match v |> BigRational.fromFloat with
        | None -> None
        | Some v  -> createSingle u v |> Some

    /// Create a `ValueUnit` using a float value
    /// `v` and a `Unit` `u`.
    let fromDecimal (v: decimal) u =
        v
        |> BigRational.fromDecimal
        |> createSingle u


    //TODO: rewrite to new online mapping
    /// Turn a `ValueUnit` to a float, string tuple.
    /// Where the unit string representation is a
    /// ZIndex string.
    let valueUnitToZIndexString vu =
        let v, u = get vu

        v |> Array.map BigRational.toDecimal,
        u
        |> Mapping.unitToString (Mapping.getUnitMapping ())


    let valueUnitFromZIndexString v u =
        let u = u |> unitFromZIndexString
        v
        |> BigRational.fromDecimal
        |> ValueUnit.singleWithUnit u


    let timeInMinute = (fun n -> fromDecimal n Units.Time.minute)


    let timeInHour =  (fun n -> fromDecimal n Units.Time.hour)


    let timeInDay =  (fun n -> fromDecimal n Units.Time.day)


    let timeInWeek =  (fun n -> fromDecimal n Units.Time.week)


    let ageInWk =  (fun n -> fromDecimal n Units.Time.week)


    let ageInMo =  (fun n -> fromDecimal n Units.Time.month)


    let ageInYr =  (fun n -> fromDecimal n Units.Time.year)


    let weightInKg =  (fun n -> fromDecimal n Units.Weight.kiloGram)


    let bsaInM2 =  (fun n -> fromDecimal n Units.BSA.m2)


    /// Create a frequency unit
    /// per `n` days
    let freqUnitPerNday n =
        1N
        |> Units.Count.nTimes
        |> per (Units.Time.nDay n)


    /// Create a frequency unit
    /// per `n` hours
    let freqUnitPerNHour n =
        1N
        |> Units.Count.nTimes
        |> per (Units.Time.nHour n)

    /// Freq unit per 1 hour.
    let freqPerOneHour = freqUnitPerNHour 1N

    /// Create an optional `ValueUnit` using
    /// an optional gestational age `gest` in
    /// weeks and days.
    let gestAgeInDaysAndWeeks gest =
        gest
        |> Option.bind (fun (w, d) ->
            let vu1 = fromDecimal w Units.Time.week
            let vu2 = fromDecimal d Units.Time.day
            vu1 + vu2 |> Some
        )

    /// Turn a frequency `ValueUnit` `freq`
    /// to a valueunit string representation.
    let freqToValueUnitString freq =
        freq |> toStringDutchLong


    /// Check whether a unit `u`
    /// is a time unit.
    let isTimeUnit u =
        (u |> Group.unitToGroup) = Group.TimeGroup


    /// Helper functions to quicly create
    /// combined units
    module Units =

        let (/.) = per

        let day  = Units.Time.day
        let day2  = Units.Time.nDay 2N
        let day3  = Units.Time.nDay 3N
        let dropl  = Units.General.general "druppel"
        let FIP  = Units.General.general "FIP"
        let g  = Units.Mass.gram
        let hour  = Units.Time.hour
        let hour36  = Units.Time.nHour 36N
        let IE  = Units.InterNatUnit.iu
        let kg  = Units.Weight.kiloGram
        let m2  = Units.BSA.m2
        let mcg  = Units.Mass.microGram
        let mg  = Units.Mass.milliGram
        let min  = Units.Time.minute
        let ml  = Units.Volume.milliLiter
        let mmol  = Units.Molar.milliMol
        let month  = Units.Time.month
        let ng  = Units.Mass.nanoGram
        let piece  = Units.General.general "stuk"
        let puff  = Units.General.general "puff"
        let times  = Units.Count.times
        let week  = Units.Time.week
        let week13  = Units.Time.nWeek 13N
        let week2  = Units.Time.nWeek 2N
        let week4  = Units.Time.nWeek 4N

        let dropl_day = dropl/.day
        let g_day = g/.day
        let IE_day = IE/.day
        let IE_kg = IE/.kg
        let IE_week = IE/.week
        let mcg_day = mcg/.day
        let mcg_kg = mcg/.kg
        let mcg_week = mcg/.week
        let mcg_week4 = mcg/.week4
        let mg_day = mg/.day
        let mg_day2 = mg/.day2
        let mg_kg = mg/.kg
        let mg_m2 = mg/.m2
        let mg_month = mg/.month
        let mg_week = mg/.week
        let mg_week13 = mg/.week13
        let mg_week2 = mg/.week2
        let mg_week4 = mg/.week4
        let ml_day = ml/.day
        let mmol_day = mmol/.day
        let ng_kg = ng/.kg
        let piece_day = piece/.day
        let puff_day = puff/.day
        let times_day = times/.day
        let times_week = times/.week

        let FIP_kg_day = FIP/.kg/.day
        let g_kg_day = g/.kg/.day
        let g_kg_week2 = g/.kg/.week2
        let g_kg_week4 = g/.kg/.week4
        let IE_kg_day = IE/.kg/.day
        let IE_kg_hour = IE/.kg/.hour
        let IE_kg_week = IE/.kg/.week
        let mcg_kg_day = mcg/.kg/.day
        let mcg_kg_hour = mcg/.kg/.hour
        let mcg_kg_min = mcg/.kg/.min
        let mcg_kg_week = mcg/.kg/.week
        let mcg_kg_week2 = mcg/.kg/.week2
        let mcg_m2_week = mcg/.m2/.week
        let mg_kg_day = mg/.kg/.day
        let mg_kg_day2 = mg/.kg/.day2
        let mg_kg_day3 = mg/.kg/.day3
        let mg_kg_hour = mg/.kg/.hour
        let mg_kg_hour36 = mg/.kg/.hour36
        let mg_kg_min = mg/.kg/.min
        let mg_kg_week = mg/.kg/.week
        let mg_kg_week2 = mg/.kg/.week2
        let mg_kg_week4 = mg/.kg/.week4
        let mg_m2_day = mg/.m2/.day
        let mg_m2_day2 = mg/.m2/.day2
        let mg_m2_week = mg/.m2/.week
        let mg_m2_week2 = mg/.m2/.week2
        let ml_kg_day = ml/.kg/.day
        let ml_m2_day = ml/.m2/.day
        let mmol_kg_day = mmol/.kg/.day
        let mmol_m2_day = mmol/.m2/.day
        let ng_kg_day = ng/.kg/.day
        let ng_kg_min = ng/.kg/.min


