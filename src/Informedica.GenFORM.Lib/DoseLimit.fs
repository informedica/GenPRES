namespace Informedica.GenForm.Lib


module DoseLimit =

    open System
    open Informedica.Utils.Lib.BCL
    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenUnits.Lib


    /// Field labels for deterministic parsing
    module FieldLabels =
        [<Literal>]
        let DoseUnit = "[dun]"

        [<Literal>]
        let Quantity = "[qty]"

        [<Literal>]
        let QuantityAdjust = "[qty-adj]"

        [<Literal>]
        let PerTime = "[per-time]"

        [<Literal>]
        let PerTimeAdjust = "[per-time-adj]"

        [<Literal>]
        let Rate = "[rate]"

        [<Literal>]
        let RateAdjust = "[rate-adj]"


    let create tar aun dun qty qta ptm pta rte rta : DoseLimit =

        {
            DoseLimitTarget = tar
            AdjustUnit = aun
            DoseUnit = dun
            Quantity = qty
            QuantityAdjust = qta
            PerTime = ptm
            PerTimeAdjust = pta
            Rate = rte
            RateAdjust = rta
        }


    /// An empty DoseLimit.
    let limit =
        {
            DoseLimitTarget = NoLimitTarget
            AdjustUnit = None
            DoseUnit = NoUnit
            Quantity = MinMax.empty
            QuantityAdjust = MinMax.empty
            PerTime = MinMax.empty
            PerTimeAdjust = MinMax.empty
            Rate = MinMax.empty
            RateAdjust = MinMax.empty
        }


    /// <summary>
    /// Check whether an adjust is used in
    /// the DoseLimit.
    /// </summary>
    /// <remarks>
    /// If any of the adjust values is not None
    /// then an adjust is used.
    /// </remarks>
    let useAdjust (dl: DoseLimit) =
        let adj =
            [
                dl.QuantityAdjust = MinMax.empty
                dl.PerTimeAdjust = MinMax.empty
                dl.RateAdjust = MinMax.empty
            ]
            |> List.forall id
            |> not

        let abs =
            [
                dl.Quantity.Min.IsSome && dl.Quantity.Max.IsSome
                dl.PerTime.Min.IsSome && dl.PerTime.Max.IsSome
                dl.Rate.Min.IsSome && dl.Rate.Max.IsSome
            ]
            |> List.exists id

        adj && not abs


    let hasNoLimits (dl: DoseLimit) =
        { limit with
            DoseLimitTarget = dl.DoseLimitTarget
            AdjustUnit = dl.AdjustUnit
            DoseUnit = dl.DoseUnit
        }
            =
            dl


    let isSubstanceLimit (dl: DoseLimit) = dl.DoseLimitTarget |> LimitTarget.isSubstanceTarget


    let isComponentLimit (dl: DoseLimit) = dl.DoseLimitTarget |> LimitTarget.isComponentTarget


    let isShapeLimit (dl: DoseLimit) = dl.DoseLimitTarget |> LimitTarget.isOrderableTarget


    let getNormDose minMax =
        match minMax.Min, minMax.Max with
        | Some minLimit, Some maxLimit ->
            if minLimit |> Limit.eq maxLimit then
                minLimit |> Limit.getValueUnit |> Some
            else
                None
        | _ -> None


    let isNormDose = getNormDose >> Option.isSome


    /// <summary>
    /// Formats a MinMax as a string with label and per-dose suffix.
    /// </summary>
    /// <param name="label">Field label to prepend (e.g., "[qty]"). When empty, uses verbose format with decimal precision.</param>
    /// <param name="perDose">Suffix to append (e.g., "/dosis"). Applied to each value in the MinMax.</param>
    /// <param name="minMax">The MinMax to format.</param>
    /// <returns>
    /// A formatted string. Returns empty string if MinMax is empty.
    /// - When label is null/whitespace: uses decimal string format with 3 decimals
    /// - When label is provided: uses engineering short notation
    /// - When min equals max (norm dose): returns single value instead of min/max pair
    /// - Final format: "{label} {formatted-value}{perDose}"
    /// </returns>
    let printMinMaxDose label perDose (minMax: MinMax) =
        let vuToStr, mmToStr =
            if label |> String.isNullOrWhiteSpace then
                Utils.ValueUnit.toString 3, Utils.MinMax.toString "min " "min " "max " "max "
            else
                ValueUnit.toStringDecimalEngShortWithoutGroup,
                MinMax.toString
                    ValueUnit.toStringDecimalEngShortWithoutGroup
                    ValueUnit.toStringDecimalEngShortWithoutGroup
                    "min "
                    "min "
                    "max "
                    "max "

        let toStr mm =
            if mm = MinMax.empty then
                ""
            else
                mm |> mmToStr |> (fun s -> $"{s}{perDose}")

        if minMax |> isNormDose then
            minMax.Min
            |> Option.map (fun minLim -> minLim |> Limit.getValueUnit |> (fun vu -> $"{vu |> vuToStr}{perDose}"))
            |> Option.defaultValue ""

        else
            minMax |> toStr
        |> fun s ->
            if s |> String.isNullOrWhiteSpace then
                ""
            else
                $"{label} {s}" |> String.trim


    let toString (dl: DoseLimit) =
        [
            let perDose = "/dosis"
            let emptyS = ""
            let dun = dl.DoseUnit |> Units.toStringEngShortWithoutGroup

            [
                $"%s{dl.DoseLimitTarget |> LimitTarget.toString}"
                if dun |> String.notEmpty then
                    $"{FieldLabels.DoseUnit} %s{dun}"

                $"%s{dl.Rate |> printMinMaxDose FieldLabels.Rate emptyS}"
                $"%s{dl.RateAdjust |> printMinMaxDose FieldLabels.RateAdjust emptyS}"

                $"%s{dl.PerTimeAdjust |> printMinMaxDose FieldLabels.PerTimeAdjust emptyS}"
                $"%s{dl.PerTime |> printMinMaxDose FieldLabels.PerTime emptyS}"

                $"%s{dl.QuantityAdjust |> printMinMaxDose FieldLabels.QuantityAdjust perDose}"
                $"%s{dl.Quantity |> printMinMaxDose FieldLabels.Quantity perDose}"
            ]
            |> List.map String.trim
            |> List.filter String.notNullOrEmpty
            |> String.concat ", "
        ]
