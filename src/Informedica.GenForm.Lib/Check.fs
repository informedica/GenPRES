namespace Informedica.GenForm.Lib


module Check =


    open MathNet.Numerics
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenForm.Lib


    module GStand = Informedica.ZForm.Lib.GStand
    module RuleFinder = Informedica.ZIndex.Lib.RuleFinder


    let mapRoute s =
        Mapping.routeMapping
        |> Array.tryFind(fun r -> r.Short |> String.equalsCapInsens s)
        |> Option.map _.Long
        |> Option.defaultValue ""


    let createDoseRules a w gpk =
        GStand.createDoseRules
            GStand.config
            a w None gpk
            "" "" ""


    let setWeightAndOrTimeUnit wu tu (mm : MinMax) =
        let setUnits u =
            match wu, tu with
            | None, None -> u
            | Some wu, None -> u |> Units.per wu
            | None, Some tu -> u |> Units.per tu
            | Some wu, Some tu -> u |> Units.per wu |> Units.per tu

        {
            Min =
                if mm.Min |> Option.isNone then mm.Min
                else
                    let v, u =
                        mm.Min.Value
                        |> Limit.getValueUnit
                        |> ValueUnit.get

                    u
                    |> setUnits
                    |> ValueUnit.withValue v
                    |> Limit.inclusive
                    |> Some
            Max =
                if mm.Max |> Option.isNone then mm.Max
                else
                    let v, u =
                        mm.Max.Value
                        |> Limit.getValueUnit
                        |> ValueUnit.get

                    u
                    |> setUnits
                    |> ValueUnit.withValue v
                    |> Limit.inclusive
                    |> Some
        }


    let inRangeOf sn (refRange : MinMax) (testRange : MinMax) =
        ((testRange.Min |> Option.isNone ||
        testRange.Min
        |> Option.map Limit.getValueUnit
        |> MinMax.inRange refRange) &&
        (testRange.Max |> Option.isNone ||
        testRange.Max
        |> Option.map Limit.getValueUnit
        |> MinMax.inRange refRange))
        |> fun b ->
            let u =
                match testRange.Min, testRange.Max with
                | Some l, _
                | _, Some l -> l |> Limit.getValueUnit |> ValueUnit.getUnit
                | _ -> NoUnit
                |> Some
            let toStr mm =
                if u |> Option.isNone then mm
                else
                    let convert =
                        Option.map (
                            Limit.getValueUnit
                            >> ValueUnit.convertTo u.Value
                            >> Limit.inclusive
                        )
                    {
                        Min = mm.Min |> convert
                        Max = mm.Max |> convert
                    }
                |> MinMax.toString "min " "min " "max " "max "
            if not b then
                b, $"{sn} {testRange |> toStr} niet in bereik van {refRange |> toStr}"
            else b, ""


    let mapDoseRule (dr : DoseRule) =
        {|
            doseRule = dr
            gstand =
                dr.Products
                |> Array.map _.GPK
                |> Array.collect (fun gpk ->
                    let gpk = Int32.tryParse gpk
                    [|
                        dr.PatientCategory.Age.Min, dr.PatientCategory.Weight.Min
                        dr.PatientCategory.Age.Max, dr.PatientCategory.Weight.Max
                        dr.PatientCategory.Age.Min, dr.PatientCategory.Weight.Max
                        dr.PatientCategory.Age.Max, dr.PatientCategory.Weight.Min
                    |]
                    |> Array.filter (fun (a, w) -> a.IsSome || w.IsSome)
                    |> Array.map (fun (a, w) ->
                        a
                        |> Option.map (
                            Limit.getValueUnit
                            >> ValueUnit.convertTo Units.Time.day
                            >> ValueUnit.getValue
                            >> Array.item 0
                            >> BigRational.toDouble),
                        w
                        |> Option.map (
                            Limit.getValueUnit
                            >> ValueUnit.convertTo Units.Weight.kiloGram
                            >> ValueUnit.getValue
                            >> Array.item 0
                            >> BigRational.toDouble)
                    )
                    |> Array.distinct
                    |> Array.collect (fun (a, w) ->
                        let a = a |> Option.map (fun x -> x / 28.)

                        createDoseRules a w gpk
                        |> Seq.toList
                        |> List.collect _.IndicationsDosages
                        |> List.collect _.RouteDosages
                        |> List.collect _.ShapeDosages
                        |> List.collect _.PatientDosages
                        |> List.collect _.SubstanceDosages
                        |> List.toArray
                    )
                )
        |}
        |> fun r ->
            {| r with
                mapping =
                    {|
                        frequencies =
                            {|
                                genform = r.doseRule.Frequencies
                                gstand =
                                    match r.doseRule.Frequencies
                                          |> Option.map ValueUnit.getUnit with
                                    | None -> None
                                    | Some u ->
                                        r.gstand
                                        |> Array.map _.TotalDosage
                                        |> Array.map snd
                                        |> Array.map (fun fr ->
                                            fr.Frequencies
                                            |> List.toArray
                                            |> ValueUnit.withUnit
                                                   (Units.Count.times |> Units.per fr.TimeUnit)
                                        )
                                        |> Array.collect ValueUnit.getBaseValue
                                        |> ValueUnit.withUnit u
                                        |> ValueUnit.toUnit
                                        |> Some
                            |}
                        doseLimits =
                            r.doseRule.DoseLimits
                            |> Array.filter DoseRule.DoseLimit.isSubstanceLimit
                            |> Array.map (fun dl ->
                                {|
                                    genForm = dl
                                    gstand =
                                        r.gstand
                                        |> Array.tryFind (fun g ->
                                            dl.DoseLimitTarget
                                            |> DoseRule.DoseLimit.doseLimitTargetToString
                                            |> String.equalsCapInsens g.Name
                                        )
                                        |> Option.map (fun x ->
                                            let convert wu =
                                                x.TotalDosage
                                                |> snd
                                                |> _.TimeUnit
                                                |> Some
                                                |> setWeightAndOrTimeUnit wu

                                            {|
                                                doseLimitTarget = x.Name |> String.toLower
                                                quantityNorm =
                                                    if x.SingleDosage.Norm =
                                                       MinMax.empty then x.StartDosage.Norm
                                                    else x.SingleDosage.Norm
                                                quantityAbs =
                                                    if x.SingleDosage.Abs =
                                                       MinMax.empty then x.StartDosage.Abs
                                                    else x.SingleDosage.Abs
                                                quantityAdjustNorm =
                                                    if x.SingleDosage.NormWeight |> fst =
                                                       MinMax.empty then x.StartDosage.NormWeight
                                                    else x.SingleDosage.NormWeight
                                                    |> fst
                                                    |> setWeightAndOrTimeUnit
                                                        (Some Units.Weight.kiloGram)
                                                        None
                                                quantityAdjustAbs =
                                                    if x.SingleDosage.AbsWeight |> fst =
                                                       MinMax.empty then x.StartDosage.AbsWeight
                                                    else x.SingleDosage.AbsWeight
                                                    |> fst
                                                    |> setWeightAndOrTimeUnit
                                                        (Some Units.Weight.kiloGram)
                                                        None
                                                perTimeNorm =
                                                    x.TotalDosage
                                                    |> fst
                                                    |> _.Norm
                                                    |> convert None
                                                perTimeAbs =
                                                    x.TotalDosage
                                                    |> fst
                                                    |> _.Abs
                                                    |> convert None
                                                perTimeAdjustNorm =
                                                    x.TotalDosage
                                                    |> fst
                                                    |> _.NormWeight
                                                    |> fst
                                                    |> convert (Some Units.Weight.kiloGram)
                                                perTimeAdjustAbs =
                                                    x.TotalDosage
                                                    |> fst
                                                    |> _.AbsWeight
                                                    |> fst
                                                    |> convert (Some Units.Weight.kiloGram)
                                            |}
                                        )
                                |}
                            )
                    |}
            |}
        |> fun m ->
            m.mapping.doseLimits
            |> Array.collect (fun dl ->
                match dl.gstand with
                | None -> [| true, "" |]
                | Some gstand ->
                    let s = m.doseRule.PatientCategory |> PatientCategory.toString
                    let r = m.doseRule.Route
                    let inRangeOf m = inRangeOf $"{gstand.doseLimitTarget}\t{r}\t{s}\t{m}: "

                    let toMinMax vuOpt =
                        {
                            Min =
                                vuOpt
                                |> Option.map ((*) ([|90N / 100N|] |> ValueUnit.withUnit Units.Count.times))
                                |> Option.map Limit.inclusive
                            Max =
                                vuOpt
                                |> Option.map ((*) ([|110N / 100N|] |> ValueUnit.withUnit Units.Count.times))
                                |> Option.map Limit.inclusive
                        }

                    [|
                        m.mapping.frequencies.genform
                        |> Option.map (fun vu ->
                            m.mapping.frequencies.gstand
                            |> Option.map (ValueUnit.isSubset vu)
                            |> Option.defaultValue true
                        )
                        |> Option.defaultValue true
                        |> fun b ->
                            if not b then
                                let s1 =
                                    m.mapping.frequencies.genform
                                    |> Option.map (ValueUnit.toStringDecimalDutchShortWithPrec 0)
                                    |> Option.defaultValue ""
                                let s2 =
                                    m.mapping.frequencies.gstand
                                    |> Option.map (ValueUnit.toStringDecimalDutchShortWithPrec 0)
                                    |> Option.defaultValue ""
                                b,
                                $"{gstand.doseLimitTarget}\t{r}\t{s}\tfreqenties niet gelijk {s1} aan {s2}"
                            else b, ""


                        dl.genForm.Quantity
                        |> inRangeOf "keer dosering" gstand.quantityNorm
                        dl.genForm.Quantity
                        |> inRangeOf "keer dosering" gstand.quantityAbs

                        dl.genForm.QuantityAdjust
                        |> inRangeOf "keer dosering per kg" gstand.quantityAdjustNorm
                        dl.genForm.QuantityAdjust
                        |> inRangeOf "keer dosering per kg" gstand.quantityAdjustAbs

                        dl.genForm.NormQuantityAdjust
                        |> toMinMax
                        |> inRangeOf "keer dosering per kg" gstand.quantityAdjustNorm
                        dl.genForm.NormQuantityAdjust
                        |> toMinMax
                        |> inRangeOf "keer dosering per kg" gstand.quantityAdjustAbs

                        dl.genForm.PerTime
                        |> inRangeOf "dosering per tijdseenheid" gstand.perTimeNorm
                        dl.genForm.PerTime
                        |> inRangeOf "dosering per tijdseenheid" gstand.perTimeAbs

                        dl.genForm.PerTimeAdjust
                        |> inRangeOf "dosering per kg per tijdseenheid" gstand.perTimeAdjustNorm
                        dl.genForm.PerTimeAdjust
                        |> inRangeOf "dosering per kg per tijdseenheid"  gstand.perTimeAdjustAbs

                        dl.genForm.NormPerTimeAdjust
                        |> toMinMax
                        |> inRangeOf "dosering per kg per tijdseenheid"  gstand.perTimeAdjustNorm
                        dl.genForm.NormPerTimeAdjust
                        |> toMinMax
                        |> inRangeOf "dosering per kg per tijdseenheid"  gstand.perTimeAdjustAbs
                    |]
            )
            |> Array.filter (fst >> not)
            |> fun xs ->
                {| m with didNotPass = xs |> Array.map snd |}


    let checkAll (drs : DoseRule[]) =
        drs
        //|> Array.take 500
        |> Array.mapi (fun i dr ->
            printfn $"processing {i}: {dr.Generic}, {dr.Route}, {dr.Indication}"
            mapDoseRule dr
        )
        |> Array.filter (_.didNotPass >> Array.isEmpty >> not)
        |> Array.collect _.didNotPass
        |> Array.filter String.notEmpty
        |> Array.distinct


