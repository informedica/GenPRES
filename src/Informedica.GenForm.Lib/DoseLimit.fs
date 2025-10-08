namespace Informedica.GenForm.Lib


module DoseLimit =

    open System
    open Informedica.Utils.Lib.BCL
    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenUnits.Lib

    open Utils

    let create
        tar
        aun
        dun
        qty
        nqt
        qta
        ptm
        npt
        pta
        rte
        rta : DoseLimit =

        {
            DoseLimitTarget = tar
            AdjustUnit = aun
            DoseUnit = dun
            Quantity = qty
            NormQuantityAdjust = nqt
            QuantityAdjust = qta
            PerTime = ptm
            NormPerTimeAdjust = npt
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
            NormQuantityAdjust = None
            QuantityAdjust = MinMax.empty
            PerTime = MinMax.empty
            NormPerTimeAdjust = None
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
    let useAdjust (dl : DoseLimit) =
        [
            dl.NormQuantityAdjust = None
            dl.QuantityAdjust = MinMax.empty
            dl.NormPerTimeAdjust = None
            dl.PerTimeAdjust = MinMax.empty
            dl.RateAdjust = MinMax.empty
        ]
        |> List.forall id
        |> not


    let hasNoLimits (dl : DoseLimit) =
        { limit with
            DoseLimitTarget = dl.DoseLimitTarget
            AdjustUnit = dl.AdjustUnit
            DoseUnit = dl.DoseUnit
        } = dl


    let isSubstanceLimit (dl : DoseLimit) = dl.DoseLimitTarget |> LimitTarget.isSubstanceTarget


    let isComponentLimit (dl : DoseLimit) = dl.DoseLimitTarget |> LimitTarget.isComponentTarget


    let isShapeLimit (dl : DoseLimit) = dl.DoseLimitTarget |> LimitTarget.isShapeTarget


    let printMinMaxDose perDose (minMax : MinMax) =
        if minMax = MinMax.empty then ""
        else
            minMax
            |> MinMax.toString
                "min "
                "min "
                "max "
                "max "
            |> fun s ->
                $"{s}{perDose}"

    let printNormDose perDose vu =
        match vu with
        | None    -> ""
        | Some vu ->
            $"{vu |> Utils.ValueUnit.toString 3}{perDose}"


    let toString (dl: DoseLimit) =
        [
            let perDose = "/dosis"
            let emptyS = ""
            [
                $"{dl.Rate |> printMinMaxDose emptyS}"
                $"{dl.RateAdjust |> printMinMaxDose emptyS}"

                $"{dl.NormPerTimeAdjust |> printNormDose emptyS} " +
                $"{dl.PerTimeAdjust |> printMinMaxDose emptyS}"

                $"{dl.PerTime |> printMinMaxDose emptyS}"

                $"{dl.NormQuantityAdjust |> printNormDose perDose} " +
                $"{dl.QuantityAdjust |> printMinMaxDose perDose}"

                $"{dl.Quantity |> printMinMaxDose perDose}"
            ]
            |> List.map String.trim
            |> List.filter (String.IsNullOrEmpty >> not)
            |> String.concat ", "
        ]

