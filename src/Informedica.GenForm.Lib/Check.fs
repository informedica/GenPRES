namespace Informedica.GenForm.Lib


module Check =


    open MathNet.Numerics
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenForm.Lib


    module GStand = Informedica.ZForm.Lib.GStand
    module Dosage = Informedica.ZForm.Lib.DoseRule.Dosage
    module RuleFinder = Informedica.ZIndex.Lib.RuleFinder

    type Dosage = Informedica.ZForm.Lib.Types.Dosage
    type DoseRange = Informedica.ZForm.Lib.Types.DoseRange

    let unitToString = Units.toStringDutchShort >> String.removeBrackets


    let checkAdjustUnit (mm1: MinMax) (mm2 : MinMax) =
        let getAdj mm =
            match mm.Min |> Option.map Limit.getValueUnit,
                  mm.Max |> Option.map Limit.getValueUnit with
            | Some vu, _
            | _, Some vu ->
                match vu |> ValueUnit.getUnit |> ValueUnit.getUnits with
                | [_; adj ]
                | [_; adj; _ ] when
                    adj = Units.Weight.kiloGram ||
                    adj = Units.Weight.gram ||
                    adj = Units.BSA.m2 -> Some adj
                | _ -> None
            | _ -> None

        match mm1 |> getAdj with
        | Some adj ->
            if
                mm2
                |> getAdj
                |> Option.map (ValueUnit.Group.eqsGroup adj)
                |> Option.defaultValue false then Some adj
            else None
        | _ -> None


    let mapRoute s =
        Mapping.routeMapping
        |> Array.tryFind(fun r -> r.Short |> String.equalsCapInsens s)
        |> Option.map _.Long
        |> Option.defaultValue ""


    let createDoseRules (pat: Patient) gen shp rte =
        let a =
            pat.Age
            |> Option.bind (fun vu ->
                vu
                |> ValueUnit.convertTo Units.Time.month
                |> ValueUnit.getValue
                |> function
                    | [|v|] ->
                        v
                        |> BigRational.toDouble
                        |> Some
                    | _ -> None
            )

        let w =
            pat.Weight
            |> Option.bind (fun vu ->
                vu
                |> ValueUnit.convertTo Units.Mass.kiloGram
                |> ValueUnit.getValue
                |> function
                    | [|v|] ->
                        v
                        |> BigRational.toDouble
                        |> Some
                    | _ -> None
            )

        rte
        |> mapRoute
        |> GStand.createDoseRules
            GStand.config
            a w None None
            gen shp


    let setAdjustAndOrTimeUnit adjUn tu (mm : MinMax) =
        let setUnits u =
            match adjUn, tu with
            | None, None -> u
            | Some adj, None -> u |> Units.per adj
            | None, Some tu -> u |> Units.per tu
            | Some adj, Some tu -> u |> Units.per adj |> Units.per tu

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


    let checkInRangeOf sn (refRange : MinMax) (testRange : MinMax) =
        //let toStr = MinMax.toString "min incl " "min excl " "max incl " "max excl "
        //printfn $"refRange : {refRange |> toStr} testRange : {testRange |> toStr}"

        let getTimeUnit mm =
            match mm.Min |> Option.map Limit.getValueUnit,
                  mm.Max |> Option.map Limit.getValueUnit with
            | Some vu, _
            | _, Some vu ->
                match vu |> ValueUnit.getUnit |> ValueUnit.getUnits with
                | [_; tu ]
                | [_; _; tu ] when
                    tu |> ValueUnit.Group.eqsGroup Units.Time.day -> Some tu
                | _ -> None
            | _ -> None
            |> Option.map unitToString
            |> Option.defaultValue ""

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
                | _, Some l ->
                    l
                    |> Limit.getValueUnit |> ValueUnit.getUnit
                    |> Some
                | _ -> None
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
            b,
            $"""%s{sn} {testRange |> toStr} {if b then "" else "niet "}in bereik van {refRange |> toStr}"""
            |> String.replace "<TIMEUNIT>" (testRange |> getTimeUnit)


    let maximizeDosages (dosages : Dosage list) =
        let maximize = MinMax.foldMaximize true true

        let maxRange (first : DoseRange) (rest : DoseRange list) =
            rest
            |> List.fold (fun (acc : DoseRange) (dr : DoseRange) ->
                {
                    Norm = maximize [dr.Norm; acc.Norm]
                    NormWeight =
                        [
                            acc.NormWeight |> fst
                            dr.NormWeight |> fst
                        ] |> maximize,
                        if acc.NormWeight |> snd = NoUnit then
                            dr.NormWeight |> snd
                        else
                            acc.NormWeight |> snd
                    NormBSA =
                        [
                            acc.NormBSA |> fst
                            dr.NormBSA |> fst
                        ] |> maximize,
                        if acc.NormBSA |> snd = NoUnit then
                            dr.NormBSA |> snd
                        else
                            acc.NormBSA |> snd
                    Abs = maximize [dr.Norm; acc.Norm]
                    AbsWeight =
                        [
                            acc.AbsWeight |> fst
                            dr.AbsWeight |> fst
                        ] |> maximize,
                        if acc.AbsWeight |> snd = NoUnit then
                            dr.AbsWeight |> snd
                        else
                            acc.AbsWeight |> snd
                    AbsBSA =
                        [
                            acc.AbsBSA |> fst
                            dr.AbsBSA |> fst
                        ] |> maximize,
                        if acc.AbsBSA |> snd = NoUnit then
                            dr.AbsBSA |> snd
                        else
                            acc.AbsBSA |> snd
                }
            ) first

        dosages
        |> function
            | [] -> None
            | dosage::rest ->
                { dosage with
                    SingleDosage =
                        rest
                        |> List.map _.SingleDosage
                        |> maxRange dosage.SingleDosage
                    StartDosage =
                        rest
                        |> List.map _.StartDosage
                        |> maxRange dosage.StartDosage
                    RateDosage =
                        if dosage.RateDosage |> snd = NoUnit then dosage.RateDosage
                        else
                            rest
                            |> List.map _.RateDosage
                            |> List.filter (snd >> ((=) NoUnit) >> not)
                            |> List.map fst
                            |> maxRange (dosage.RateDosage |> fst)
                            , dosage.RateDosage |> snd
                    TotalDosage =
                        if (dosage.TotalDosage |> snd).TimeUnit = NoUnit then dosage.TotalDosage
                        else
                            let rest =
                                rest
                                |> List.map _.TotalDosage
                                |> List.filter (fun (_, fr) -> fr.TimeUnit = NoUnit |> not)
                            rest
                            |> List.map fst
                            |> maxRange (dosage.TotalDosage |> fst)
                            ,
                            { (dosage.TotalDosage |> snd) with
                                Frequencies =
                                    rest
                                    |> List.map snd
                                    |> List.collect _.Frequencies
                                    |> List.append
                                        (dosage.TotalDosage
                                        |> snd
                                        |> _.Frequencies)
                                    |> List.distinct
                            }
                    Rules =
                        rest
                        |> List.collect _.Rules
                        |> List.append dosage.Rules
                }
                |> Some


    let filterPatient (pat : PatientCategory) (pdsg : Informedica.ZForm.Lib.Types.PatientDosage) =
        let age =
            pat.Age = MinMax.empty && pdsg.Patient.Age = MinMax.empty ||
            (pdsg.Patient.Age |> MinMax.intersect pat.Age = MinMax.empty |> not)
        let weight =
            pat.Weight = MinMax.empty && pdsg.Patient.Weight = MinMax.empty ||
            (pdsg.Patient.Weight |> MinMax.intersect pat.Weight = MinMax.empty |> not)
        //printfn $"{pat |> PatientCategory.toString} intersects with {pdsg.Patient |> Informedica.ZForm.Lib.PatientCategory.toString}: {age && weight}"
        age && weight


    let matchWithZIndex (pat : Patient) (dr : DoseRule) =
        {|
            doseRule = dr
            zindex =
                {|
                    dosages =
                        createDoseRules pat dr.Generic dr.Shape dr.Route
                        |> Seq.toList
                        |> List.collect _.IndicationsDosages
                        |> List.collect _.RouteDosages
                        |> List.collect _.ShapeDosages
                        |> List.collect _.PatientDosages
                        |> List.filter (filterPatient dr.PatientCategory)
                        |> List.collect _.SubstanceDosages
                        |> List.groupBy _.Name
                        |> List.map (fun (n, dsgs) ->
                            {|
                                target = n
                                dosage =
                                    // TODO: hack avoid cmp err with diff dose units (filgrastim)
                                    try
                                        dsgs |> maximizeDosages
                                    with
                                    | _ -> None
                            |}
                        )
                |}
        |}


    let createMapping (r : {| doseRule: DoseRule; zindex: {| dosages: {| dosage: Dosage option; target: string |} list |} |}) =
        {| r with
            mapping =
                {|
                    frequencies =
                        {|
                            genform = r.doseRule.Frequencies
                            gstand =
                                r.zindex.dosages
                                |> List.map _.dosage
                                |> List.choose (fun ds ->
                                    ds
                                    |> Option.bind (fun ds ->
                                        let fr = ds.TotalDosage |> snd
                                        if fr.TimeUnit = NoUnit then None
                                        else
                                            Units.Count.times
                                            |> Units.per fr.TimeUnit
                                            |> ValueUnit.withValue (fr.Frequencies |> List.toArray)
                                            |> Some
                                    )
                                )
                                |> function
                                    | [] -> None
                                    | vu::rest ->
                                        let u = vu |> ValueUnit.getUnit
                                        let v =
                                            vu::rest
                                            |> List.toArray
                                            |> Array.collect ValueUnit.getValue
                                        ValueUnit.create u v
                                        |> Some
                        |}
                    doseLimits =
                        r.doseRule.DoseLimits
                        |> Array.filter DoseRule.DoseLimit.isSubstanceLimit
                        |> Array.map (fun dl ->
                            {|
                                genForm = dl
                                gstand =
                                    r.zindex.dosages
                                    |> List.tryFind (fun g ->
                                        dl.DoseLimitTarget
                                        |> DoseRule.DoseLimit.doseLimitTargetToString
                                        |> String.equalsCapInsens g.target
                                    )
                                    |> Option.bind _.dosage
                                    |> Option.map (fun x ->
                                        let convert adjUn =
                                            x.TotalDosage
                                            |> snd
                                            |> _.TimeUnit
                                            |> Some
                                            |> setAdjustAndOrTimeUnit adjUn

                                        {|
                                            doseLimitTarget =
                                                dl.DoseLimitTarget
                                                |> DoseRule.DoseLimit.doseLimitTargetToString
                                            quantityNorm =
                                                if x.SingleDosage.Norm =
                                                   MinMax.empty then x.StartDosage.Norm
                                                else x.SingleDosage.Norm
                                            quantityAbs =
                                                if x.SingleDosage.Abs =
                                                   MinMax.empty then x.StartDosage.Abs
                                                else x.SingleDosage.Abs
                                            quantityAdjustNorm =
                                                if x.SingleDosage.NormWeight |> fst = MinMax.empty then
                                                   if x.SingleDosage.NormBSA |> fst = MinMax.empty then MinMax.empty
                                                   else
                                                        x.SingleDosage.NormBSA
                                                       |> fst
                                                       |> setAdjustAndOrTimeUnit
                                                           (Some Units.BSA.m2)
                                                           None
                                                else
                                                    x.SingleDosage.NormWeight
                                                    |> fst
                                                    |> setAdjustAndOrTimeUnit
                                                        (Some Units.Weight.kiloGram)
                                                        None
                                            quantityAdjustAbs =
                                                if x.SingleDosage.AbsWeight |> fst = MinMax.empty then
                                                    if x.StartDosage.AbsBSA |> fst = MinMax.empty then MinMax.empty
                                                    else
                                                        x.StartDosage.AbsBSA
                                                        |> fst
                                                        |> setAdjustAndOrTimeUnit
                                                            (Some Units.BSA.m2)
                                                            None
                                                else
                                                    x.SingleDosage.AbsWeight
                                                    |> fst
                                                    |> setAdjustAndOrTimeUnit
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
                                                let normWeight =
                                                    x.TotalDosage
                                                    |> fst
                                                    |> _.NormWeight
                                                if normWeight |> fst = MinMax.empty then
                                                    let normBSA =
                                                        x.TotalDosage
                                                        |> fst
                                                        |> _.NormBSA
                                                    if normBSA |> fst = MinMax.empty then MinMax.empty
                                                    else
                                                        normBSA
                                                        |> fst
                                                        |> convert (Some Units.BSA.m2)
                                                else
                                                    normWeight
                                                    |> fst
                                                    |> convert (Some Units.Weight.kiloGram)
                                            perTimeAdjustAbs =
                                                let absWeight =
                                                    x.TotalDosage
                                                    |> fst
                                                    |> _.AbsWeight
                                                if absWeight |> fst = MinMax.empty then
                                                    let absBSA =
                                                        x.TotalDosage
                                                        |> fst
                                                        |> _.AbsBSA
                                                    if absBSA |> fst = MinMax.empty then MinMax.empty
                                                    else
                                                        absBSA
                                                        |> fst
                                                        |> convert (Some Units.BSA.m2)
                                                else
                                                    absWeight
                                                    |> fst
                                                    |> convert (Some Units.Weight.kiloGram)
                                        |}
                                    )
                            |}
                        )
                |}
        |}


    let checkDoseRule (pat : Patient) (dr : DoseRule) =
        let m =
            dr
            |> matchWithZIndex pat
            |> createMapping

        m.mapping.doseLimits
        |> Array.collect (fun dl ->
            match dl.gstand with
            | None -> [| true, "" |]
            | Some gstand ->
                let p = m.doseRule.PatientCategory |> PatientCategory.toString
                let r = m.doseRule.Route
                let inRangeOf m refRange testRange =
                    try
                        checkInRangeOf
                            $"{gstand.doseLimitTarget}\t{r}\t{p}\t{m}: "
                            refRange
                            testRange
                    with
                    | e ->
                        printfn $"{e}"
                        true, $"{gstand.doseLimitTarget}\t{r}\t{p}\t{m}: kan niet worden gechecked vanwege foutmelding"

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
                        //let u = m.mapping.frequencies.genform |> Option.map ValueUnit.getUnit
                        let s1 =
                            m.mapping.frequencies.genform
                            |> Option.map (ValueUnit.toStringDecimalDutchShortWithPrec -1)
                            |> Option.defaultValue ""
                        let s2 =
                            m.mapping.frequencies.gstand
                            //|> Option.map (fun vu -> if u |> Option.isNone then vu else vu |> ValueUnit.convertTo u.Value)
                            |> Option.map (ValueUnit.toStringDecimalDutchShortWithPrec -1)
                            |> Option.defaultValue ""
                        if not b then
                            b,
                            $"{m.doseRule.Generic}\t{r}\t{p}\tfrequenties {s1} niet gelijk aan {s2}"
                        else
                            b,
                            $"{m.doseRule.Generic}\t{r}\t{p}\tfrequenties {s1} is subset van {s2}"

                    dl.genForm.Quantity
                    |> inRangeOf "keer dosering" gstand.quantityNorm

                    dl.genForm.Quantity
                    |> inRangeOf "keer dosering" gstand.quantityAbs

                    match dl.genForm.QuantityAdjust |> checkAdjustUnit gstand.quantityAdjustNorm with
                    | None -> ()
                    | Some adj ->
                        let adj = adj |> unitToString
                        dl.genForm.QuantityAdjust
                        |> inRangeOf $"keer dosering per %s{adj}" gstand.quantityAdjustNorm

                    match dl.genForm.QuantityAdjust |> checkAdjustUnit gstand.quantityAdjustAbs with
                    | None -> ()
                    | Some adj ->
                        let adj = adj |> unitToString
                        dl.genForm.QuantityAdjust
                        |> inRangeOf $"keer dosering per %s{adj}" gstand.quantityAdjustAbs

                    let mm =
                        dl.genForm.NormQuantityAdjust
                        |> toMinMax

                    match mm |> checkAdjustUnit gstand.quantityAdjustNorm with
                    | None -> ()
                    | Some adj ->
                        let adj = adj |> unitToString
                        mm
                        |> inRangeOf $"keer dosering per %s{adj}" gstand.quantityAdjustNorm

                    match mm |> checkAdjustUnit gstand.quantityAdjustAbs with
                    | None -> ()
                    | Some adj ->
                        let adj = adj |> unitToString
                        mm
                        |> inRangeOf $"keer dosering per %s{adj}" gstand.quantityAdjustAbs

                    dl.genForm.PerTime
                    |> inRangeOf "dosering per <TIMEUNIT>" gstand.perTimeNorm

                    dl.genForm.PerTime
                    |> inRangeOf "dosering per <TIMEUNIT>" gstand.perTimeAbs

                    match dl.genForm.PerTimeAdjust |> checkAdjustUnit gstand.perTimeAdjustNorm with
                    | None -> ()
                    | Some adj ->
                        let adj = adj |> unitToString
                        dl.genForm.PerTimeAdjust
                        |> inRangeOf $"dosering per %s{adj} per <TIMEUNIT>" gstand.perTimeAdjustNorm

                    match dl.genForm.PerTimeAdjust |> checkAdjustUnit gstand.perTimeAdjustAbs with
                    | None -> ()
                    | Some adj ->
                        let adj = adj |> unitToString
                        dl.genForm.PerTimeAdjust
                        |> inRangeOf $"dosering per %s{adj} per <TIMEUNIT>"  gstand.perTimeAdjustAbs

                    let mm =
                        dl.genForm.NormPerTimeAdjust
                        |> toMinMax

                    match mm |> checkAdjustUnit gstand.perTimeAdjustNorm with
                    | None -> ()
                    | Some adj ->
                        let adj = adj |> unitToString
                        mm
                        |> inRangeOf $"dosering per %s{adj} per <TIMEUNIT>"  gstand.perTimeAdjustNorm

                    match mm |> checkAdjustUnit gstand.perTimeAdjustAbs with
                    | None -> ()
                    | Some adj ->
                        let adj = adj |> unitToString
                        mm
                        |> inRangeOf $"dosering per %s{adj} per <TIMEUNIT>"  gstand.perTimeAdjustAbs
                |]
        )
        |> Array.partition (fst >> not)
        |> fun (didNot, did) ->
            {| m with didNotPass = didNot |> Array.map snd; didPass = did |> Array.map snd |}


    let checkAll (pat : Patient) (drs : DoseRule[]) =
        drs
        |> Array.mapi (fun i dr ->
            printfn $"{i}. checking {dr.Generic}\t{dr.Shape}\t{dr.Route}"
            checkDoseRule pat dr
        )
        |> Array.filter (fun c ->
            c.didNotPass |> Array.isEmpty |> not
        )
        |> Array.collect _.didNotPass
        |> Array.filter String.notEmpty
        |> Array.distinct



