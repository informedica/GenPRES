namespace Informedica.GenForm.Lib


module Check =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenForm.Lib

    open Utils

    module GStand = Informedica.ZForm.Lib.GStand
    module Dosage = Informedica.ZForm.Lib.DoseRule.Dosage
    module RuleFinder = Informedica.ZIndex.Lib.RuleFinder

    type Dosage = Informedica.ZForm.Lib.Types.Dosage
    type DoseRange = Informedica.ZForm.Lib.Types.DoseRange


    let unitToString = Units.toStringDutchShort >> String.removeBrackets


    /// Graded outcome of a dose-check signal (IR Doseringscontrole 4.6.2).
    /// `Within` is a pass; the others are signals of decreasing/typed concern.
    type Severity =
        | Within
        | AdvisoryOverNorm // > norm max, <= absolute max (IR tekst 1)
        | OverAbsolute // > absolute max (IR tekst 3)
        | UnderNorm // < norm min (IR tekst 2)
        | UnitMismatch // kg vs m2 — cannot compare
        | IncomparableUnits // unit groups differ (e.g. Count/kg/day vs IU/kg/week, droplet vs mg) — cannot compare
        | NotComparable // missing dose type / both ranges empty
        | FrequencyMismatch // entered frequency not in the rule (IR 4.5.2)
        | NoMonitoring // no G-Standaard rule exists for the selection (info only)


    /// Scalar IR 4.6.2 flow. The absolute max is the hard ceiling and is checked
    /// first, so a breach is caught even when no norm max is present (norm <= abs
    /// in valid G-Standaard data, so this matches the IR "norm exceeded then abs"
    /// flow while also being safe when normMax = None).
    let classify (dose: BigRational) normMax absMax normMin : Severity =
        match absMax with
        | Some a when dose > a -> OverAbsolute
        | _ ->
            match normMax with
            | Some n when dose > n -> AdvisoryOverNorm
            | _ ->
                match normMin with
                | Some n when dose < n -> UnderNorm
                | _ -> Within


    /// IR 4.6.1 limit-selection priority: m2 (BSA) -> per kg -> absolute. Prefer
    /// the BSA range when present, else weight, else empty. `convWeight`/`convBSA`
    /// stamp the adjust (and optional time) unit.
    let pickAdjust
        (weightMM: MinMax)
        (bsaMM: MinMax)
        (convWeight: MinMax -> MinMax)
        (convBSA: MinMax -> MinMax)
        : MinMax
        =
        if bsaMM <> MinMax.empty then bsaMM |> convBSA
        elif weightMM <> MinMax.empty then weightMM |> convWeight
        else MinMax.empty


    /// Convert a months-based age MinMax to days at the IR 3.3 rate of
    /// 30 days/month, so a ZForm (month) age can be intersected with a GenFORM
    /// (day) age.
    let monthsMinMaxToDays (mm: MinMax) : MinMax =
        let toDays lim =
            lim
            |> Limit.getValueUnit
            |> ValueUnit.convertTo Units.Time.month
            |> ValueUnit.applyToValue (Array.map ((*) 30N))
            |> ValueUnit.setUnit Units.Time.day
            |> Limit.inclusive

        {
            Min = mm.Min |> Option.map toDays
            Max = mm.Max |> Option.map toDays
        }


    /// IR 3.4 interchangeable frequency time-unit groups. per-12-weken vs
    /// per-3-maanden are deliberately NOT interchangeable.
    let interchangeGroups =
        [
            set [ "per 2 dagen"; "om de dag" ]
            set [ "per 4 weken"; "per maand" ]
            set [ "per 8 weken"; "per 2 maanden" ]
            set [ "per half jaar"; "per 6 maanden" ]
        ]

    let canonTimeUnit (u: string) =
        interchangeGroups
        |> List.tryFind (Set.contains u)
        |> Option.map Set.minElement
        |> Option.defaultValue u

    let interchangeable a b = canonTimeUnit a = canonTimeUnit b


    /// IR 4.5.2 frequency message granularity. The G-Standaard IR text number
    /// (24 / 25 / 8) is intentionally NOT surfaced — only the human description.
    let freqMsg (aantalDiff: bool) (eenheidDiff: bool) =
        match aantalDiff, eenheidDiff with
        | true, false -> "aantal verschilt"
        | false, true -> "tijdseenheid verschilt"
        | _ -> "aantal en/of tijdseenheid verschilt"


    /// IR 4.5.2: the rule's frequency set <paramref name="genform"/> is acceptable
    /// when it is a SUBSET of the G-Standaard reference set <paramref name="gstand"/>
    /// (i.e. every prescribed frequency is allowed), or the time units are
    /// interchangeable and the counts are equal. Extracted so the subset direction
    /// (genform ⊆ gstand) is unit-testable — see CheckTests.
    let freqWithinReference unitsInterchangeable aantalDiff (genform: ValueUnit) (gstand: ValueUnit) =
        (ValueUnit.isSubset genform gstand) || (unitsInterchangeable && not aantalDiff)


    /// IR 1.3.2 (5,6): toedieningssnelheid/-duur are out of the dose-check scope.
    type RateCheckMode =
        | DropRateChecks // strict IR conformance
        | LabelRateChecksOutOfScope // keep the signal, but tag it (recommended)

    let rateScopeLabel (mode: RateCheckMode) (msg: string) =
        match mode with
        | DropRateChecks -> None
        | LabelRateChecksOutOfScope -> Some $"[buiten G-Standaard doseringscontrole] {msg}"


    /// IR 4.6.1.3/4.6.1.4 replacement for the old symmetric +-10% band.
    /// `isRisk = true` (GPRISC = "*") => exact, no margin. Otherwise the margin is
    /// one-sided on the max (`marginUpper`, e.g. 12/10 for 120%).
    let marginedTestRange (isRisk: bool) (marginUpper: BigRational) (vuOpt: ValueUnit option) : MinMax =
        match vuOpt with
        | None -> MinMax.empty
        | Some vu when isRisk ->
            {
                Min = Some(vu |> Limit.inclusive)
                Max = Some(vu |> Limit.inclusive)
            }
        | Some vu ->
            let factor = [| marginUpper |] |> ValueUnit.withUnit Units.Count.times

            {
                Min = Some(vu |> Limit.inclusive)
                Max = Some(vu * factor |> Limit.inclusive)
            }


    /// Configuration for `checkDoseRuleWith`: the (provider-configurable) upper
    /// margin and how infusion-rate checks are handled.
    type CheckConfig =
        {
            // Upper margin multiplier on the G-Standaard max for non-risk substances
            MarginUpper: BigRational
            // Whether infusion-rate rows are dropped or kept-but-labelled (HIGH-2)
            RateCheckMode: RateCheckMode
        }

    let checkConfigDef =
        {
            MarginUpper = 12N / 10N
            RateCheckMode = LabelRateChecksOutOfScope
        }


    let checkAdjustUnit (mm1: MinMax) (mm2: MinMax) =
        let getAdj mm =
            match mm.Min |> Option.map Limit.getValueUnit, mm.Max |> Option.map Limit.getValueUnit with
            | Some vu, _
            | _, Some vu ->
                match vu |> ValueUnit.getUnit |> ValueUnit.getUnits with
                | [ _; adj ]
                | [ _; adj; _ ]
                | [ _; _; adj ] when adj = Units.Weight.kiloGram || adj = Units.Weight.gram || adj = Units.BSA.m2 ->
                    Some adj
                | _ -> None
            | _ -> None

        match mm1 |> getAdj with
        | Some adj ->
            if
                mm2
                |> getAdj
                |> Option.map (ValueUnit.Group.eqsGroup adj)
                |> Option.defaultValue false
            then
                Some adj
            else
                None
        | _ -> None


    let mapRouteWithMapping routeMapping s =
        routeMapping
        |> Array.tryFind (fun r -> r.Short |> String.equalsCapInsens s)
        |> Option.map _.Long
        |> Option.defaultValue ""


    let createDoseRulesWithMapping routeMapping (pat: Patient) gen frm rte =
        let a =
            pat.Age
            |> Option.bind (fun vu ->
                vu
                |> ValueUnit.convertTo Units.Time.month
                |> ValueUnit.getValue
                |> function
                    | [| v |] -> v |> BigRational.toDouble |> Some
                    | _ -> None
            )

        let w =
            pat.Weight
            |> Option.bind (fun vu ->
                vu
                |> ValueUnit.convertTo Units.Mass.kiloGram
                |> ValueUnit.getValue
                |> function
                    | [| v |] -> v |> BigRational.toDouble |> Some
                    | _ -> None
            )

        rte
        |> mapRouteWithMapping routeMapping
        |> GStand.createDoseRules GStand.config a w None None gen frm


    type private gen = string
    type private frm = string
    type private rte = string

    /// Provider of raw G-Standaard (ZForm) dose rules for a generic/form/route and
    /// patient.
    type GStandProvider = Patient -> gen -> frm -> rte -> Informedica.ZForm.Lib.Types.DoseRule seq


    /// The live (side-effecting) `GStandProvider` backed by `GStand.createDoseRules`.
    let gStandProvider routeMapping : GStandProvider = createDoseRulesWithMapping routeMapping


    let setAdjustAndOrTimeUnit adjUn tu (mm: MinMax) =
        let setUnits u =
            match adjUn, tu with
            | None, None -> u
            | Some adj, None -> u |> ValueUnit.per adj
            | None, Some tu -> u |> ValueUnit.per tu
            | Some adj, Some tu -> u |> ValueUnit.per adj |> ValueUnit.per tu

        {
            Min =
                if mm.Min |> Option.isNone then
                    mm.Min
                else
                    let v, u = mm.Min.Value |> Limit.getValueUnit |> ValueUnit.get

                    u |> setUnits |> ValueUnit.withValue v |> Limit.inclusive |> Some
            Max =
                if mm.Max |> Option.isNone then
                    mm.Max
                else
                    let v, u = mm.Max.Value |> Limit.getValueUnit |> ValueUnit.get

                    u |> setUnits |> ValueUnit.withValue v |> Limit.inclusive |> Some
        }


    /// Full unit (incl. combi units) of a `MinMax`, taken from its min or max limit.
    let rangeUnit (mm: MinMax) =
        match mm.Min |> Option.map Limit.getValueUnit, mm.Max |> Option.map Limit.getValueUnit with
        | Some vu, _
        | _, Some vu -> vu |> ValueUnit.getUnit |> Some
        | _ -> None


    /// IR safety: two ranges are comparable only when their unit groups match
    /// (e.g. mg/kg/day vs mg/kg/day). Empty / unit-less ranges are treated as
    /// comparable so the existing empty-range short-circuits still apply.
    let rangesComparable (refRange: MinMax) (testRange: MinMax) =
        match rangeUnit refRange, rangeUnit testRange with
        | Some ru, Some tu -> ru |> ValueUnit.Group.eqsGroup tu
        | _ -> true


    let checkInRangeOf sn (refRange: MinMax) (testRange: MinMax) : bool option * string =
        if testRange |> MinMax.isEmpty || refRange |> MinMax.isEmpty then
            None, ""
        else
            let getTimeUnit mm =
                match mm.Min |> Option.map Limit.getValueUnit, mm.Max |> Option.map Limit.getValueUnit with
                | Some vu, _
                | _, Some vu ->
                    match vu |> ValueUnit.getUnit |> ValueUnit.getUnits with
                    | [ _; tu ]
                    | [ _; _; tu ] when tu |> ValueUnit.Group.eqsGroup Units.Time.day -> Some tu
                    | _ -> None
                | _ -> None
                |> Option.map unitToString
                |> Option.defaultValue ""

            ((testRange.Min |> Option.isNone
              || testRange.Min |> Option.map Limit.getValueUnit |> MinMax.inRange refRange)
             && (testRange.Max |> Option.isNone
                 || testRange.Max |> Option.map Limit.getValueUnit |> MinMax.inRange refRange))
            |> fun b ->
                let u =
                    match testRange.Min, testRange.Max with
                    | Some l, _
                    | _, Some l -> l |> Limit.getValueUnit |> ValueUnit.getUnit |> Some
                    | _ -> None

                let toStr mm =
                    if u |> Option.isNone then
                        mm
                    else
                        let convert = Option.map (Limit.getValueUnit >> ValueUnit.convertTo u.Value >> Limit.inclusive)

                        {
                            Min = mm.Min |> convert
                            Max = mm.Max |> convert
                        }
                    |> MinMax.toString "min " "min " "max " "max "

                Some b,
                $"""%s{sn} {testRange |> toStr} {if b then "" else "niet "}in bereik van %s{refRange |> toStr}"""
                |> String.replace "<TIMEUNIT>" (testRange |> getTimeUnit)


    let maximizeDosages (dosages: Dosage list) =
        let maximize = MinMax.foldMaximize true true

        let maxRange (first: DoseRange) (rest: DoseRange list) =
            rest
            |> List.fold
                (fun (acc: DoseRange) (dr: DoseRange) ->
                    {
                        Norm = maximize [ dr.Norm; acc.Norm ]
                        NormWeight =
                            [ acc.NormWeight |> fst; dr.NormWeight |> fst ] |> maximize,
                            if acc.NormWeight |> snd = NoUnit then
                                dr.NormWeight |> snd
                            else
                                acc.NormWeight |> snd
                        NormBSA =
                            [ acc.NormBSA |> fst; dr.NormBSA |> fst ] |> maximize,
                            if acc.NormBSA |> snd = NoUnit then
                                dr.NormBSA |> snd
                            else
                                acc.NormBSA |> snd
                        Abs = maximize [ dr.Abs; acc.Abs ]
                        AbsWeight =
                            [ acc.AbsWeight |> fst; dr.AbsWeight |> fst ] |> maximize,
                            if acc.AbsWeight |> snd = NoUnit then
                                dr.AbsWeight |> snd
                            else
                                acc.AbsWeight |> snd
                        AbsBSA =
                            [ acc.AbsBSA |> fst; dr.AbsBSA |> fst ] |> maximize,
                            if acc.AbsBSA |> snd = NoUnit then
                                dr.AbsBSA |> snd
                            else
                                acc.AbsBSA |> snd
                    }
                )
                first

        dosages
        |> function
            | [] -> None
            | dosage :: rest ->
                { dosage with
                    SingleDosage = rest |> List.map _.SingleDosage |> maxRange dosage.SingleDosage
                    StartDosage = rest |> List.map _.StartDosage |> maxRange dosage.StartDosage
                    RateDosage =
                        if dosage.RateDosage |> snd = NoUnit then
                            dosage.RateDosage
                        else
                            rest
                            |> List.map _.RateDosage
                            |> List.filter (snd >> ((=) NoUnit) >> not)
                            |> List.map fst
                            |> maxRange (dosage.RateDosage |> fst),
                            dosage.RateDosage |> snd
                    TotalDosage =
                        if (dosage.TotalDosage |> snd).TimeUnit = NoUnit then
                            dosage.TotalDosage
                        else
                            let rest =
                                rest
                                |> List.map _.TotalDosage
                                |> List.filter (fun (_, fr) -> fr.TimeUnit = NoUnit |> not)

                            rest |> List.map fst |> maxRange (dosage.TotalDosage |> fst),
                            { (dosage.TotalDosage |> snd) with
                                Frequencies =
                                    rest
                                    |> List.map snd
                                    |> List.collect _.Frequencies
                                    |> List.append (dosage.TotalDosage |> snd |> _.Frequencies)
                                    |> List.distinct
                            }
                    Rules = rest |> List.collect _.Rules |> List.append dosage.Rules
                    // OR the GPRISC high-risk flag across all merged dosages: if any
                    // overlapping patient-band dosage is narrow-TI, the merged result
                    // must stay high risk so marginedTestRange suppresses the margin.
                    HighRisk = dosage.HighRisk || (rest |> List.exists _.HighRisk)
                }
                |> Some


    let filterPatient (pat: PatientCategory) (pdsg: Informedica.ZForm.Lib.Types.PatientDosage) =
        let patAge = pat |> PatientCategory.getAge
        // IR 3.3: the ZForm/G-Standaard age is in months; the GenFORM patient
        // category age is in days. Normalise to days (30 days/month) before
        // intersecting so the two are comparable on the same unit.
        let zformAge = pdsg.Patient.Age |> monthsMinMaxToDays

        let age =
            patAge = MinMax.empty && zformAge = MinMax.empty
            || (zformAge |> MinMax.intersect patAge = MinMax.empty |> not)

        let weight =
            pat.Weight = MinMax.empty && pdsg.Patient.Weight = MinMax.empty
            || (pdsg.Patient.Weight |> MinMax.intersect pat.Weight = MinMax.empty |> not)

        age && weight


    let matchWithZIndex (getDosageRules: GStandProvider) (pat: Patient) (dr: DoseRule) =
        {|
            doseRule = dr
            zindex =
                {|
                    dosages =
                        getDosageRules
                            pat
                            // G-Standaard keys on the base substance name only;
                            // the brand/form label would never match.
                            (dr.Generic |> Generic.genericName)
                            (dr.Generic.Form |> PharmaceuticalForm.toString)
                            dr.Route
                        |> Seq.toList
                        |> List.collect _.IndicationsDosages
                        |> List.collect _.RouteDosages
                        |> List.collect _.FormDosages
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
                                    with _ ->
                                        None
                            |}
                        )
                |}
        |}


    let createMapping
        (r:
            {|
                doseRule: DoseRule
                zindex:
                    {|
                        dosages:
                            {|
                                dosage: Dosage option
                                target: string
                            |} list
                    |}
            |})
        =
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

                                        if fr.TimeUnit = NoUnit then
                                            None
                                        else
                                            Units.Count.times
                                            |> ValueUnit.per fr.TimeUnit
                                            |> ValueUnit.withValue (fr.Frequencies |> List.toArray)
                                            |> Some
                                    )
                                )
                                |> function
                                    | [] -> None
                                    | vu :: rest ->
                                        let u = vu |> ValueUnit.getUnit
                                        let v = vu :: rest |> List.toArray |> Array.collect ValueUnit.getValue
                                        ValueUnit.create u v |> Some
                        |}
                    doseLimits =
                        r.doseRule.ComponentLimits
                        |> Array.collect _.SubstanceLimits
                        |> Array.map (fun dl ->
                            {|
                                genForm =
                                    { dl with
                                        Types.DoseLimit.PerTimeAdjust =
                                            if
                                                dl.PerTimeAdjust |> MinMax.isEmpty
                                                && dl.QuantityAdjust |> MinMax.isEmpty |> not
                                            then
                                                match r.doseRule.Frequencies with
                                                | None -> dl.PerTimeAdjust
                                                | Some fr ->
                                                    match fr |> ValueUnit.minValue, fr |> ValueUnit.maxValue with
                                                    | Some minFreq, Some maxFreq ->
                                                        let maxPerTimeAdj =
                                                            dl.QuantityAdjust.Max
                                                            |> Option.map Limit.getValueUnit
                                                            |> Option.map (fun vu -> vu * maxFreq)

                                                        let minPerTimeAdj =
                                                            dl.QuantityAdjust.Min
                                                            |> Option.map Limit.getValueUnit
                                                            |> Option.map (fun vu -> vu * minFreq)

                                                        match minPerTimeAdj, maxPerTimeAdj with
                                                        | Some min, Some max -> MinMax.createInclIncl min max
                                                        | _ -> dl.PerTimeAdjust
                                                    | _ -> dl.PerTimeAdjust
                                            else
                                                dl.PerTimeAdjust

                                    }
                                gstand =
                                    r.zindex.dosages
                                    |> List.tryFind (fun g ->
                                        dl.DoseLimitTarget |> LimitTarget.toString |> String.equalsCapInsens g.target
                                    )
                                    |> Option.bind _.dosage
                                    |> Option.map (fun x ->
                                        let convert adjUn =
                                            x.TotalDosage |> snd |> _.TimeUnit |> Some |> setAdjustAndOrTimeUnit adjUn

                                        {|
                                            doseLimitTarget = dl.DoseLimitTarget |> LimitTarget.toString
                                            quantityNorm =
                                                if x.SingleDosage.Norm = MinMax.empty then
                                                    x.StartDosage.Norm
                                                else
                                                    x.SingleDosage.Norm
                                            quantityAbs =
                                                if x.SingleDosage.Abs = MinMax.empty then
                                                    x.StartDosage.Abs
                                                else
                                                    x.SingleDosage.Abs
                                            // IR 4.6.1 priority: BSA (m2) before kg.
                                            quantityAdjustNorm =
                                                pickAdjust
                                                    (x.SingleDosage.NormWeight |> fst)
                                                    (x.SingleDosage.NormBSA |> fst)
                                                    (setAdjustAndOrTimeUnit (Some Units.Weight.kiloGram) None)
                                                    (setAdjustAndOrTimeUnit (Some Units.BSA.m2) None)
                                            // BUG-A fix: both ranges read from SingleDosage
                                            // (the m2 branch previously read StartDosage.AbsBSA).
                                            quantityAdjustAbs =
                                                pickAdjust
                                                    (x.SingleDosage.AbsWeight |> fst)
                                                    (x.SingleDosage.AbsBSA |> fst)
                                                    (setAdjustAndOrTimeUnit (Some Units.Weight.kiloGram) None)
                                                    (setAdjustAndOrTimeUnit (Some Units.BSA.m2) None)
                                            perTimeNorm = x.TotalDosage |> fst |> _.Norm |> convert None
                                            perTimeAbs = x.TotalDosage |> fst |> _.Abs |> convert None
                                            perTimeAdjustNorm =
                                                pickAdjust
                                                    (x.TotalDosage |> fst |> _.NormWeight |> fst)
                                                    (x.TotalDosage |> fst |> _.NormBSA |> fst)
                                                    (convert (Some Units.Weight.kiloGram))
                                                    (convert (Some Units.BSA.m2))
                                            perTimeAdjustAbs =
                                                pickAdjust
                                                    (x.TotalDosage |> fst |> _.AbsWeight |> fst)
                                                    (x.TotalDosage |> fst |> _.AbsBSA |> fst)
                                                    (convert (Some Units.Weight.kiloGram))
                                                    (convert (Some Units.BSA.m2))
                                            highRisk = x.HighRisk
                                        |}
                                    )
                            |}
                        )
                |}
        |}


    let checkDoseRuleWith (cfg: CheckConfig) (getDosageRules: GStandProvider) (pat: Patient) (dr: DoseRule) =
        let m = dr |> matchWithZIndex getDosageRules pat |> createMapping

        let eqsAny (candidates: DoseType list) (dt: DoseType) = candidates |> List.exists (DoseType.eqsType dt)

        // Derive rate fields for a given dose-limit target from m.zindex.dosages,
        // mirroring the perTimeAdjust* pattern in createMapping.
        let rateFieldsFor (target: string) =
            let empty =
                {|
                    rateNorm = MinMax.empty
                    rateAbs = MinMax.empty
                    rateAdjustNorm = MinMax.empty
                    rateAdjustAbs = MinMax.empty
                |}

            m.zindex.dosages
            |> List.tryFind (fun g -> target |> String.equalsCapInsens g.target)
            |> Option.bind _.dosage
            |> Option.map (fun x ->
                let dr, rateUnit = x.RateDosage

                if rateUnit = NoUnit then
                    empty
                else
                    {|
                        rateNorm = dr.Norm |> setAdjustAndOrTimeUnit None (Some rateUnit)
                        rateAbs = dr.Abs |> setAdjustAndOrTimeUnit None (Some rateUnit)
                        // IR 4.6.1 priority: BSA (m2) before kg.
                        rateAdjustNorm =
                            pickAdjust
                                (dr.NormWeight |> fst)
                                (dr.NormBSA |> fst)
                                (setAdjustAndOrTimeUnit (Some Units.Weight.kiloGram) (Some rateUnit))
                                (setAdjustAndOrTimeUnit (Some Units.BSA.m2) (Some rateUnit))
                        rateAdjustAbs =
                            pickAdjust
                                (dr.AbsWeight |> fst)
                                (dr.AbsBSA |> fst)
                                (setAdjustAndOrTimeUnit (Some Units.Weight.kiloGram) (Some rateUnit))
                                (setAdjustAndOrTimeUnit (Some Units.BSA.m2) (Some rateUnit))
                    |}
            )
            |> Option.defaultValue empty

        // Extract adjust unit from a MinMax (mirrors getAdj inside checkAdjustUnit).
        let getAdjUnit (mm: MinMax) =
            match mm.Min |> Option.map Limit.getValueUnit, mm.Max |> Option.map Limit.getValueUnit with
            | Some vu, _
            | _, Some vu ->
                match vu |> ValueUnit.getUnit |> ValueUnit.getUnits with
                | [ _; adj ]
                | [ _; adj; _ ]
                | [ _; _; adj ] when adj = Units.Weight.kiloGram || adj = Units.Weight.gram || adj = Units.BSA.m2 ->
                    Some adj
                | _ -> None
            | _ -> None

        m.mapping.doseLimits
        |> Array.collect (fun dl ->
            match dl.gstand with
            | None -> [| (None: Severity option), "" |]
            | Some gstand ->
                let dt = m.doseRule.DoseType
                let p = m.doseRule.PatientCategory |> PatientCategory.toString
                let r = m.doseRule.Route

                let inRangeOf msg refRange testRange =
                    try
                        checkInRangeOf $"{gstand.doseLimitTarget}\t{r}\t{p}\t{msg}: " refRange testRange
                    with _ ->
                        // Defensive backstop: the rangesComparable guard in `grade`
                        // already prevents incomparable-unit cmp throws, so reaching
                        // here is exceptional. Drop the row (never report a crash as a
                        // PASS); kept pure — no console IO in the check pipeline.
                        None, ""

                // HIGH-1: one-sided, risk-aware margin on the norm dose. Risk
                // substances (GPRISC = "*") get no margin.
                let toMinMax vuOpt = vuOpt |> marginedTestRange gstand.highRisk cfg.MarginUpper

                // MEDIUM-2: grade a test range against the advisory (norm) and the
                // absolute reference ranges (IR 4.6.2): a norm breach is advisory,
                // an absolute breach is serious, and absolute is only decisive once
                // norm is exceeded.
                let grade msg (normRef: MinMax) (absRef: MinMax) (test: MinMax) : Severity option * string =
                    // IR safety guard: never feed incomparable unit groups to cmp
                    // (e.g. Count/kg/day vs IU/kg/week, droplet vs mg) — that throws.
                    // Emit a typed warning instead of crashing or (previously) a fake pass.
                    let incomparable ref = ref |> MinMax.isEmpty |> not && not (rangesComparable ref test)

                    if (test |> MinMax.isEmpty |> not) && (incomparable normRef || incomparable absRef) then
                        Some IncomparableUnits,
                        $"{gstand.doseLimitTarget}\t{r}\t{p}\t{msg}: eenheden niet vergelijkbaar (kan niet worden gecontroleerd)"
                    else

                        let nb, nmsg = inRangeOf msg normRef test
                        let ab, amsg = inRangeOf msg absRef test

                        // The absolute ceiling breach is checked first so that even
                        // inconsistent G-Standaard data (normRef.Max > absRef.Max) can
                        // never silently downgrade an over-absolute dose to Within.
                        // For valid data (norm ⊆ abs) this is equivalent to norm-first.
                        match nb, ab with
                        | None, None -> None, ""
                        | _, Some false -> Some OverAbsolute, amsg
                        | Some true, _ -> Some Within, nmsg
                        | None, Some true -> Some Within, amsg
                        | Some false, _ -> Some AdvisoryOverNorm, nmsg

                // Adjust-unit-gated grade: only compares ranges whose adjust unit
                // matches the test's, and substitutes the unit into the message.
                let gradeAdjust (msgFor: string -> string) normRef absRef test : Severity option * string =
                    let na = test |> checkAdjustUnit normRef
                    let aa = test |> checkAdjustUnit absRef

                    match na, aa with
                    | None, None -> None, ""
                    | _ ->
                        let adj = (na |> Option.orElse aa |> Option.get) |> unitToString
                        let normRef = if na |> Option.isSome then normRef else MinMax.empty
                        let absRef = if aa |> Option.isSome then absRef else MinMax.empty
                        grade (msgFor adj) normRef absRef test

                match dt with
                | NoDoseType ->
                    [|
                        Some NotComparable,
                        $"{m.doseRule.Generic |> Generic.toString}\t{r}\t{p}\tdoseer type mist — kan niet vergelijken"
                    |]
                | _ ->
                    let rates = rateFieldsFor gstand.doseLimitTarget

                    let runQuantity = dt |> eqsAny [ Once ""; Discontinuous ""; Timed ""; OnceTimed "" ]

                    let runPerTime = dt |> eqsAny [ Discontinuous ""; Timed "" ]
                    let runRate = dt |> eqsAny [ Continuous ""; Timed ""; OnceTimed "" ]
                    let runFrequencies = dt |> eqsAny [ Discontinuous ""; Timed "" ]

                    let freqRow () : Severity option * string =
                        match m.mapping.frequencies.genform, m.mapping.frequencies.gstand with
                        | None, _
                        | _, None -> None, ""
                        | Some vuG, Some vuS ->
                            let s1 = vuG |> ValueUnit.toStringDecimalDutchShortWithPrec -1
                            let s2 = vuS |> ValueUnit.toStringDecimalDutchShortWithPrec -1

                            // LOW-3 (IR 3.4): treat interchangeable time units as equal.
                            let tokenOf vu =
                                match vu |> ValueUnit.getUnit |> ValueUnit.getUnits with
                                | [ _; tu ]
                                | [ _; tu; _ ] -> "per " + (tu |> unitToString)
                                | _ -> ""

                            let unitsInterchangeable = interchangeable (tokenOf vuG) (tokenOf vuS)
                            let aantalDiff = (vuG |> ValueUnit.getValue) <> (vuS |> ValueUnit.getValue)

                            // genform ⊆ gstand: the rule's frequencies must all be
                            // allowed by the G-Standaard reference (NOT the reverse).
                            let b = freqWithinReference unitsInterchangeable aantalDiff vuG vuS

                            if b then
                                Some Within,
                                $"{m.doseRule.Generic |> Generic.toString}\t{r}\t{p}\tfrequenties {s1} is subset van {s2}"
                            else
                                // LOW-4 (IR 4.5.2): granular description (no IR text code).
                                let eenheidDiff = tokenOf vuG <> tokenOf vuS && not unitsInterchangeable

                                Some FrequencyMismatch,
                                $"{m.doseRule.Generic |> Generic.toString}\t{r}\t{p}\tfrequenties {s1} t.o.v. {s2}: %s{freqMsg aantalDiff eenheidDiff}"

                    // Each gradeable quantity yields ONE graded row (norm+abs
                    // combined). The margined row applies the HIGH-1 risk-aware band.
                    let quantityChecks () =
                        let adjMsg adj = $"keer dosering per %s{adj}"

                        [|
                            grade "keer dosering" gstand.quantityNorm gstand.quantityAbs dl.genForm.Quantity

                            gradeAdjust
                                adjMsg
                                gstand.quantityAdjustNorm
                                gstand.quantityAdjustAbs
                                dl.genForm.QuantityAdjust

                            dl.genForm.QuantityAdjust
                            |> DoseLimit.getNormDose
                            |> toMinMax
                            |> gradeAdjust adjMsg gstand.quantityAdjustNorm gstand.quantityAdjustAbs
                        |]

                    let perTimeChecks () =
                        let adjMsg adj = $"dosering per %s{adj} per <TIMEUNIT>"

                        [|
                            grade "dosering per <TIMEUNIT>" gstand.perTimeNorm gstand.perTimeAbs dl.genForm.PerTime

                            gradeAdjust
                                adjMsg
                                gstand.perTimeAdjustNorm
                                gstand.perTimeAdjustAbs
                                dl.genForm.PerTimeAdjust

                            dl.genForm.PerTimeAdjust
                            |> DoseLimit.getNormDose
                            |> toMinMax
                            |> gradeAdjust adjMsg gstand.perTimeAdjustNorm gstand.perTimeAdjustAbs
                        |]

                    // HIGH-2: rate is out of IR scope. Drop the rows, or keep the
                    // signal but tag the message as out-of-scope (default).
                    let rateChecks () =
                        let adjMsg adj = $"dosering per %s{adj} per <TIMEUNIT>"

                        let rows =
                            [|
                                grade "infusiesnelheid per <TIMEUNIT>" rates.rateNorm rates.rateAbs dl.genForm.Rate

                                gradeAdjust adjMsg rates.rateAdjustNorm rates.rateAdjustAbs dl.genForm.RateAdjust

                                dl.genForm.RateAdjust
                                |> DoseLimit.getNormDose
                                |> toMinMax
                                |> gradeAdjust adjMsg rates.rateAdjustNorm rates.rateAdjustAbs
                            |]

                        match cfg.RateCheckMode with
                        | DropRateChecks -> [||]
                        | LabelRateChecksOutOfScope ->
                            rows
                            |> Array.map (fun (sev, msg) ->
                                match sev with
                                | Some s when s <> Within ->
                                    match rateScopeLabel cfg.RateCheckMode msg with
                                    | Some tagged -> sev, tagged
                                    | None -> sev, msg
                                | _ -> sev, msg
                            )

                    // "non matching adjust units" detection. Only fires when both
                    // sides of the (DoseType-applicable) adjust ranges carry a
                    // detectable unit AND no pair of them is in the same unit
                    // group. Emits at most one row per DoseLimit.
                    let mismatchRow () =
                        let gstandSides =
                            [
                                if runQuantity then
                                    yield gstand.quantityAdjustNorm
                                    yield gstand.quantityAdjustAbs
                                if runPerTime then
                                    yield gstand.perTimeAdjustNorm
                                    yield gstand.perTimeAdjustAbs
                                if runRate then
                                    yield rates.rateAdjustNorm
                                    yield rates.rateAdjustAbs
                            ]
                            |> List.choose (fun mm -> getAdjUnit mm |> Option.map (fun u -> mm, u))

                        let genFormSides =
                            [
                                if runQuantity then
                                    yield dl.genForm.QuantityAdjust
                                if runPerTime then
                                    yield dl.genForm.PerTimeAdjust
                                if runRate then
                                    yield dl.genForm.RateAdjust
                            ]
                            |> List.choose (fun mm -> getAdjUnit mm |> Option.map (fun u -> mm, u))

                        match gstandSides, genFormSides with
                        | [], _
                        | _, [] -> None
                        | gs, gf ->
                            let anyMatch =
                                gf
                                |> List.exists (fun (gfMm, _) ->
                                    gs |> List.exists (fun (gsMm, _) -> checkAdjustUnit gfMm gsMm |> Option.isSome)
                                )

                            if anyMatch then
                                None
                            else
                                let gfU = gf |> List.head |> snd |> unitToString
                                let gsU = gs |> List.head |> snd |> unitToString

                                Some(
                                    Some UnitMismatch,
                                    $"{gstand.doseLimitTarget}\t{r}\t{p}\teenheden verschillen (kg vs m2) (doseer regel: %s{gfU}, G-Standaard controle: %s{gsU})"
                                )

                    [|
                        if runFrequencies then
                            yield freqRow ()
                        if runQuantity then
                            yield! quantityChecks ()
                        if runPerTime then
                            yield! perTimeChecks ()
                        if runRate then
                            yield! rateChecks ()
                        match mismatchRow () with
                        | Some entry -> yield entry
                        | None -> ()
                    |]
        )
        |> fun xs ->
            // Graded signals (skip the non-applicable None rows).
            let signals =
                xs
                |> Array.choose (
                    function
                    | Some sev, s -> Some(sev, s)
                    | None, _ -> None
                )

            // didPass/didNotPass kept as string[] projections for back-compat:
            // Within -> pass, every other graded signal -> did-not-pass.
            let didPass =
                signals
                |> Array.choose (fun (sev, s) -> if sev = Within then Some s else None)
                |> Array.filter String.notEmpty

            let didNotPass =
                signals
                |> Array.choose (fun (sev, s) -> if sev = Within then None else Some s)
                |> Array.filter String.notEmpty

            {| m with
                didNotPass = didNotPass
                didPass = didPass
                signals = signals
            |}


    /// Pure dose-check over a single rule given an injected data provider.
    /// In production the provider comes from the Resources layer
    /// (`Api.getGStandProvider`), not built ad hoc from a route mapping.
    let checkDoseRuleWithProvider (getDosageRules: GStandProvider) (pat: Patient) (dr: DoseRule) =
        checkDoseRuleWith checkConfigDef getDosageRules pat dr


    /// Dose-check over many rules. `log` and the data provider are injected so the
    /// orchestration is pure given its dependencies; callers wire the live ones
    /// (e.g. the server passes `Api.getGStandProvider provider`).
    let checkAllWith (log: int -> DoseRule -> unit) (getDosageRules: GStandProvider) (pat: Patient) (drs: DoseRule[]) =
        drs
        |> Array.mapi (fun i dr ->
            log i dr
            checkDoseRuleWithProvider getDosageRules pat dr
        )
        |> Array.filter (fun c -> c.didNotPass |> Array.isEmpty |> not)
        |> Array.collect _.didNotPass
        |> Array.filter String.notEmpty
        |> Array.distinct
