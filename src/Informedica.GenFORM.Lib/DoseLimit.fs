namespace Informedica.GenForm.Lib


module DoseLimit =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenUnits.Lib


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


    /// <summary>Formats the MinMax of a dose in Dutch decimals, with a per-dose suffix.</summary>
    /// <param name="perDose">The suffix after the formatted MinMax, as "/dosis"; may be empty.</param>
    /// <param name="minMax">The MinMax to format.</param>
    /// <returns>
    /// An empty string for an empty MinMax; the single value when min equals max, a norm dose;
    /// else the range, as "10 - 20 mg/dosis", or the one limit, as "max 20 mg/dosis".
    /// </returns>
    let printMinMaxDose perDose (minMax: MinMax) =
        let vuToStr = Utils.ValueUnit.toString 3
        let mmToStr = Utils.MinMax.toString "min " "min " "max " "max "

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
        |> String.trim
