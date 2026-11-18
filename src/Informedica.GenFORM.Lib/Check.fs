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
    module Age = Informedica.GenCore.Lib.Calculations.Age
    module CorePatientData = Informedica.GenCore.Lib.Patients.PatientData

    type Dosage = Informedica.ZForm.Lib.Types.Dosage
    type DoseRange = Informedica.ZForm.Lib.Types.DoseRange


    let unitToString = Units.toStringDutchShort >> String.removeBrackets


    /// The grade of a dose-check signal, after the G-Standaard dose-check guideline.
    /// Within is a pass; every other case is a signal.
    type Severity =
        /// The dose passes the check.
        | Within
        /// The dose is above the norm maximum but not above the absolute maximum.
        | AdvisoryOverNorm
        /// The dose is above the absolute maximum.
        | OverAbsolute
        /// The dose is below the norm minimum.
        | UnderNorm
        /// One rule doses per kg and the other per m2, so the doses cannot be compared.
        | UnitMismatch
        /// The unit groups differ, such as IU and mg, so the dose cannot be checked.
        | IncomparableUnits
        /// The dose rule has no dose type, so it cannot be compared.
        | NotComparable
        /// A frequency of the dose rule is not in the G-Standaard rule.
        | FrequencyMismatch
        /// No G-Standaard rule exists for the dose rule; information only.
        | NoMonitoring


    /// Grades a dose against the norm maximum, the absolute maximum and the norm minimum. The absolute maximum
    /// is checked first, so a dose above it is OverAbsolute also when there is no norm maximum.
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


    /// Picks the per m2 range when present, else the per kg range, else an empty range. The picked range
    /// gets its units from convBSA or convWeight.
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


    /// Converts an age MinMax in months to days with fractionalMonthsToDays, so a ZForm age in months
    /// can be intersected with a GenFORM age in days. Each limit stays inclusive or exclusive.
    let monthsMinMaxToDays =
        MinMax.apply (
            ValueUnit.convertTo Units.Time.month
            >> ValueUnit.applyToValue (Array.map Age.fractionalMonthsToDays)
            >> ValueUnit.setUnit Units.Time.day
        )


    /// The groups of frequency time units that count as equal, such as per 4 weken and per maand.
    /// Per 12 weken and per 3 maanden are not equal on purpose.
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


    /// Describes how a frequency differs from the G-Standaard frequency: the count, the time unit, or both.
    /// The G-Standaard text number is left out on purpose.
    let freqMsg (aantalDiff: bool) (eenheidDiff: bool) =
        match aantalDiff, eenheidDiff with
        | true, false -> "aantal verschilt"
        | false, true -> "tijdseenheid verschilt"
        | _ -> "aantal en/of tijdseenheid verschilt"


    /// Returns true when every frequency in genform is in the G-Standaard frequencies gstand, or when the time
    /// units are interchangeable and the counts are equal. Only this direction is checked, not gstand in genform.
    let freqWithinReference unitsInterchangeable aantalDiff (genform: ValueUnit) (gstand: ValueUnit) =
        (ValueUnit.isSubset genform gstand) || (unitsInterchangeable && not aantalDiff)


    /// How the rate checks are handled. The G-Standaard dose check does not cover the administration rate.
    type RateCheckMode =
        /// Drops the rate checks.
        | DropRateChecks
        /// Keeps the rate signals and labels each one that is not Within as outside the G-Standaard dose check.
        | LabelRateChecksOutOfScope

    let rateScopeLabel (mode: RateCheckMode) (msg: string) =
        match mode with
        | DropRateChecks -> None
        | LabelRateChecksOutOfScope -> Some $"[buiten G-Standaard doseringscontrole] {msg}"


    /// Makes the range a dose is tested with: from the dose to the dose times marginUpper, or the dose alone
    /// when isRisk is true for a high-risk substance (GPRISC "*"). No dose gives an empty range.
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


    /// The settings of checkDoseRuleWith: the upper margin and the handling of the rate checks.
    type CheckConfig =
        {
            /// The factor the dose is multiplied by for the top of the tested range, for a substance that is not
            /// high risk.
            MarginUpper: BigRational
            /// Whether the rate checks are dropped or labelled as outside the G-Standaard dose check.
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


    /// The age of the patient in months that the G-Standaard dose rules are looked up with. Returns None for
    /// patient data without an age.
    let ageInMonths (patientData: PatientData) =
        patientData
        |> CorePatientData.ageInDays
        |> Option.bind (fun vu ->
            vu
            |> ValueUnit.convertTo Units.Time.day
            |> ValueUnit.getValue
            |> function
                | [| v |] -> v |> Age.daysToFractionalMonths |> BigRational.toDouble |> Some
                | _ -> None
        )


    let createDoseRulesWithMapping routeMapping (patientData: PatientData) gen frm rte =
        let a = patientData |> ageInMonths

        let w =
            patientData
            |> CorePatientData.weightInKg
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

    /// Returns the G-Standaard dose rules for the patient data, the generic, the form and the route.
    type GStandProvider = PatientData -> gen -> frm -> rte -> Informedica.ZForm.Lib.Types.DoseRule seq


    /// Makes a GStandProvider that maps the route with routeMapping and reads the G-Standaard dose rules.
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


    /// Returns the full unit, a combined unit included, of the minimum or else the maximum of a range.
    /// Returns None for an empty range.
    let rangeUnit (mm: MinMax) =
        match mm.Min |> Option.map Limit.getValueUnit, mm.Max |> Option.map Limit.getValueUnit with
        | Some vu, _
        | _, Some vu -> vu |> ValueUnit.getUnit |> Some
        | _ -> None


    /// Returns true when the unit groups of the two ranges match, such as mg/kg/day and mg/kg/day. An empty
    /// range counts as comparable, so the checks for an empty range still apply.
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


    /// The outcome of comparing a test range with a reference range.
    [<RequireQualifiedAccess>]
    type Comparison =
        /// The comparison does not apply, since one of the ranges is empty.
        | NotApplicable
        /// The test range is in the reference range, with the message that says so.
        | InRange of string
        /// The test range is not in the reference range, with the message that says so.
        | OutOfRange of string
        /// The units of the two ranges cannot be compared, so the test range cannot be checked.
        | CannotCheck


    /// Compares a test range with a reference range. Ranges whose units cannot be compared give
    /// CannotCheck; any other exception is a defect and is not caught.
    let compareRanges sn refRange testRange =
        // Both the guard and the catch are needed: ValueUnit compares a value without a unit by its number
        // alone, and the guard reads only one limit of each range.
        if rangesComparable refRange testRange |> not then
            Comparison.CannotCheck
        else
            try
                match checkInRangeOf sn refRange testRange with
                | None, _ -> Comparison.NotApplicable
                | Some true, msg -> Comparison.InRange msg
                | Some false, msg -> Comparison.OutOfRange msg
            // ValueUnit raises an ArgumentException for values whose units cannot be compared
            with :? System.ArgumentException ->
                Comparison.CannotCheck


    /// Grades a test range from its comparison with the norm range and with the absolute range. A
    /// breach of the absolute range is OverAbsolute even when the norm range cannot be checked;
    /// otherwise a comparison that cannot be checked gives IncomparableUnits with the message
    /// cannotCheck, so that a check that was not done never shows as Within.
    let gradeComparisons cannotCheck norm abs =
        match norm, abs with
        | _, Comparison.OutOfRange amsg -> Some OverAbsolute, amsg
        | Comparison.CannotCheck, _
        | _, Comparison.CannotCheck -> Some IncomparableUnits, cannotCheck
        | Comparison.NotApplicable, Comparison.NotApplicable -> None, ""
        | Comparison.InRange nmsg, _ -> Some Within, nmsg
        | Comparison.NotApplicable, Comparison.InRange amsg -> Some Within, amsg
        | Comparison.OutOfRange nmsg, _ -> Some AdvisoryOverNorm, nmsg


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
                    // The merged dosage is high risk when any of the dosages is, so marginedTestRange
                    // adds no margin.
                    HighRisk = dosage.HighRisk || (rest |> List.exists _.HighRisk)
                }
                |> Some


    let filterPatient (pat: PatientCategory) (pdsg: Informedica.ZForm.Lib.Types.PatientDosage) =
        let patAge = pat |> PatientCategory.getAge
        // The ZForm age is in months and the GenFORM patient category age in days; both are
        // brought to days before they are intersected.
        let zformAge = pdsg.Patient.Age |> monthsMinMaxToDays

        let age =
            patAge = MinMax.empty && zformAge = MinMax.empty
            || (zformAge |> MinMax.intersect patAge = MinMax.empty |> not)

        let weight =
            pat.Weight = MinMax.empty && pdsg.Patient.Weight = MinMax.empty
            || (pdsg.Patient.Weight |> MinMax.intersect pat.Weight = MinMax.empty |> not)

        age && weight


    let matchWithZIndex (getDosageRules: GStandProvider) (patientData: PatientData) (dr: DoseRule) =
        {|
            doseRule = dr
            zindex =
                {|
                    dosages =
                        getDosageRules
                            patientData
                            // The G-Standaard is searched by the generic name only; a name with a brand or a
                            // form never matches.
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
                                    // G-Standaard dosages in units that cannot be compared, such as
                                    // filgrastim in microgram and in units, cannot be merged. A group
                                    // is never empty, so no dosage means the limit cannot be checked.
                                    try
                                        dsgs |> maximizeDosages
                                    with :? System.ArgumentException ->
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
                                            // A per m2 range comes before a per kg range.
                                            quantityAdjustNorm =
                                                pickAdjust
                                                    (x.SingleDosage.NormWeight |> fst)
                                                    (x.SingleDosage.NormBSA |> fst)
                                                    (setAdjustAndOrTimeUnit (Some Units.Weight.kiloGram) None)
                                                    (setAdjustAndOrTimeUnit (Some Units.BSA.m2) None)
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


    let checkDoseRuleWith
        (cfg: CheckConfig)
        (getDosageRules: GStandProvider)
        (patientData: PatientData)
        (dr: DoseRule)
        =
        let m = dr |> matchWithZIndex getDosageRules patientData |> createMapping

        let eqsAny (candidates: DoseType list) (dt: DoseType) = candidates |> List.exists (DoseType.eqsType dt)

        // Returns the rate ranges of the G-Standaard dosage for a dose-limit target, or empty ranges when the
        // dosage has no rate.
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
                        // A per m2 range comes before a per kg range.
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

        // Returns the adjust unit of a range: kg, g or m2.
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
            | None ->
                let target = dl.genForm.DoseLimitTarget |> LimitTarget.toString

                let notMerged =
                    m.zindex.dosages
                    |> List.exists (fun g -> target |> String.equalsCapInsens g.target && g.dosage |> Option.isNone)

                if notMerged then
                    [|
                        Some IncomparableUnits,
                        $"{target}\t{m.doseRule.Route}\t{m.doseRule.PatientCategory |> PatientCategory.toString}\tG-Standaard eenheden niet vergelijkbaar (kan niet worden gecontroleerd)"
                    |]
                else
                    [| (None: Severity option), "" |]
            | Some gstand ->
                let dt = m.doseRule.DoseType
                let p = m.doseRule.PatientCategory |> PatientCategory.toString
                let r = m.doseRule.Route

                let inRangeOf msg refRange testRange =
                    compareRanges $"{gstand.doseLimitTarget}\t{r}\t{p}\t{msg}: " refRange testRange

                // A high-risk substance (GPRISC "*") gets no margin on the norm dose.
                let toMinMax vuOpt = vuOpt |> marginedTestRange gstand.highRisk cfg.MarginUpper

                // Grades a test range against the norm range and the absolute range. Above the norm range is
                // advisory; above the absolute range is OverAbsolute, whatever the norm result.
                let grade msg (normRef: MinMax) (absRef: MinMax) (test: MinMax) : Severity option * string =
                    let cannotCheck =
                        $"{gstand.doseLimitTarget}\t{r}\t{p}\t{msg}: eenheden niet vergelijkbaar (kan niet worden gecontroleerd)"

                    gradeComparisons cannotCheck (inRangeOf msg normRef test) (inRangeOf msg absRef test)

                // Grades only against the ranges with the adjust unit of the test range, and puts that unit in
                // the message.
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

                            // Interchangeable time units count as equal.
                            let tokenOf vu =
                                match vu |> ValueUnit.getUnit |> ValueUnit.getUnits with
                                | [ _; tu ]
                                | [ _; tu; _ ] -> "per " + (tu |> unitToString)
                                | _ -> ""

                            let unitsInterchangeable = interchangeable (tokenOf vuG) (tokenOf vuS)
                            let aantalDiff = (vuG |> ValueUnit.getValue) <> (vuS |> ValueUnit.getValue)

                            // Every frequency of the dose rule must be in the G-Standaard frequencies, not the
                            // reverse.
                            let b = freqWithinReference unitsInterchangeable aantalDiff vuG vuS

                            if b then
                                Some Within,
                                $"{m.doseRule.Generic |> Generic.toString}\t{r}\t{p}\tfrequenties {s1} is subset van {s2}"
                            else
                                let eenheidDiff = tokenOf vuG <> tokenOf vuS && not unitsInterchangeable

                                Some FrequencyMismatch,
                                $"{m.doseRule.Generic |> Generic.toString}\t{r}\t{p}\tfrequenties {s1} t.o.v. {s2}: %s{freqMsg aantalDiff eenheidDiff}"

                    // Each row combines the norm and the absolute check. The last row tests the norm dose
                    // with the margin.
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

                    // The G-Standaard dose check does not cover the rate, so cfg.RateCheckMode drops the rows
                    // or labels them.
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

                    // Gives one UnitMismatch row when the G-Standaard and the dose rule both have adjust units
                    // for the dose type, and no pair of them is in the same unit group.
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
            // The graded signals, without the rows that do not apply.
            let signals =
                xs
                |> Array.choose (
                    function
                    | Some sev, s -> Some(sev, s)
                    | None, _ -> None
                )

            // didPass has the messages of the Within signals, didNotPass those of all other signals.
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


    /// Checks a dose rule against the G-Standaard dose rules from getDosageRules, with the default settings.
    let checkDoseRuleWithProvider (getDosageRules: GStandProvider) (patientData: PatientData) (dr: DoseRule) =
        checkDoseRuleWith checkConfigDef getDosageRules patientData dr


    /// Checks all dose rules and returns the distinct messages of the failed checks. Calls log with the
    /// index and the dose rule before each check.
    let checkAllWith
        (log: int -> DoseRule -> unit)
        (getDosageRules: GStandProvider)
        (pat: PatientData)
        (drs: DoseRule[])
        =
        drs
        |> Array.mapi (fun i dr ->
            log i dr
            checkDoseRuleWithProvider getDosageRules pat dr
        )
        |> Array.filter (fun c -> c.didNotPass |> Array.isEmpty |> not)
        |> Array.collect _.didNotPass
        |> Array.filter String.notEmpty
        |> Array.distinct
