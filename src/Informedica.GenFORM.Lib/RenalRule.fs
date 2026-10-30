namespace Informedica.GenForm.Lib


module RenalRule =

    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib
    open Informedica.Logging.Lib

    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges

    open Utils


    module DoseReduction =

        [<Literal>]
        let REL = "rel"

        [<Literal>]
        let REPL = "repl"

        /// The old sheet value of the replacing reduction, still read until the sheet is updated.
        [<Literal>]
        let ABS = "abs"


        /// Whether a sheet value is the replacing reduction: "repl", or the old "abs", in any case.
        let isReplace s =
            s |> String.equalsCapInsens REPL || s |> String.equalsCapInsens ABS


        let fromString logger s =
            match s with
            | _ when s |> isReplace -> Replace
            | _ when s |> String.equalsCapInsens REL -> Relative
            | _ ->
                Warning $"{s} is not a valid dosereduction" |> Logging.logWarning logger
                NoReduction


        let toString =
            function
            | Replace -> REPL
            | Relative -> REL
            | NoReduction -> ""


    module Limit =

        /// <summary>
        /// Create a RenalLimit
        /// </summary>
        /// <returns>
        /// RenalLimit
        /// </returns>
        let create doseLimitTarget doseReduction quantity quantityAdjust perTime perTimeAdjust rate rateAdjust =
            {
                DoseLimitTarget = doseLimitTarget
                DoseReduction = doseReduction
                Quantity = quantity
                QuantityAdjust = quantityAdjust
                PerTime = perTime
                PerTimeAdjust = perTimeAdjust
                Rate = rate
                RateAdjust = rateAdjust
            }


    let create gen rte ind src age dt fr it rf limits : RenalRule =
        {
            Generic = gen
            Route = rte
            Indication = ind
            Source = src
            Age = age
            RenalFunction = rf
            DoseType = dt
            Frequencies = fr
            IntervalTime = it
            RenalLimits = limits
        }


    let parseRenalRuleData (data: string[][]) : Result<_, Message list> =
        try
            data
            |> fun data ->
                let getColumn = data |> Array.head |> Csv.getStringColumn

                data
                |> Array.tail
                |> Array.map (fun r ->
                    let get = getColumn r >> String.trim
                    let toBrOpt = BigRational.toBrs >> Array.tryHead

                    {
                        Generic = get "Generic"
                        Route = get "Route"
                        Indication = get "Indication"
                        Source = get "Source"
                        MinAge = get "MinAge" |> toBrOpt
                        MaxAge = get "MaxAge" |> toBrOpt
                        IntDial = get "IntDial"
                        ContDial = get "ContDial"
                        PerDial = get "PerDial"
                        MinGFR = get "MinGFR" |> toBrOpt
                        MaxGFR = get "MaxGFR" |> toBrOpt
                        DoseType = get "DoseType"
                        DoseText = get "DoseText"
                        Frequencies = get "Freqs" |> BigRational.toBrs
                        DoseRed = get "DoseRed"
                        DoseUnit = get "DoseUnit"
                        AdjustUnit = get "AdjustUnit"
                        FreqUnit = get "FreqUnit"
                        RateUnit = get "RateUnit"
                        MinInterval = get "MinInt" |> toBrOpt
                        MaxInterval = get "MaxInt" |> toBrOpt
                        IntervalUnit = get "IntUnit"
                        Substance = get "Substance"
                        MinQty = get "MinQty" |> toBrOpt
                        MaxQty = get "MaxQty" |> toBrOpt
                        MinQtyAdj = get "MinQtyAdj" |> toBrOpt
                        MaxQtyAdj = get "MaxQtyAdj" |> toBrOpt
                        MinPerTime = get "MinPerTime" |> toBrOpt
                        MaxPerTime = get "MaxPerTime" |> toBrOpt
                        MinPerTimeAdj = get "MinPerTimeAdj" |> toBrOpt
                        MaxPerTimeAdj = get "MaxPerTimeAdj" |> toBrOpt
                        MinRate = get "MinRate" |> toBrOpt
                        MaxRate = get "MaxRate" |> toBrOpt
                        MinRateAdj = get "MinRateAdj" |> toBrOpt
                        MaxRateAdj = get "MaxRateAdj" |> toBrOpt
                    }
                )
            |> Ok
        with exn ->
            Result.createError "Error in RenalRule.getDetails: " exn


    let getData dataUrlId : Result<_, Message list> =
        Web.getDataFromSheet dataUrlId "RenalRules" |> parseRenalRuleData


    let fromTupleInclExcl = MinMax.fromTuple Inclusive Exclusive


    let fromTupleInclIncl = MinMax.fromTuple Inclusive Inclusive


    let createRenalFunction contDial intDial perDial minGFR maxGFR =
        match minGFR, maxGFR with
        | None, None ->
            match contDial, intDial, perDial with
            | "x", _, _ -> ContinuousHemodialysis |> Some
            | _, "x", _ -> IntermittentHemodialysis |> Some
            | _, _, "x" -> PeritonealDialysis |> Some
            | _, _, _ -> None
        | None, Some max -> (None, max |> BigRational.ToInt32 |> Some) |> EGFR |> Some
        | Some min, None -> (min |> BigRational.ToInt32 |> Some, None) |> EGFR |> Some
        | Some min, Some max ->
            (min |> BigRational.ToInt32 |> Some, max |> BigRational.ToInt32 |> Some)
            |> EGFR
            |> Some


    /// The key that the rows of one renal rule share.
    let groupKey (r: RenalRuleData) =
        r.Generic,
        r.Route,
        r.Indication,
        r.Source,
        r.MinAge,
        r.MaxAge,
        DoseType.fromString r.DoseType r.DoseText,
        (match r.FreqUnit |> Units.freqUnit with
         | None -> None
         | Some u ->
             if r.Frequencies |> Array.isEmpty then
                 None
             else
                 r.Frequencies |> ValueUnit.withUnit u |> Some),
        (r.MinInterval, r.MaxInterval)
        |> fromTupleInclIncl (r.IntervalUnit |> Units.timeUnit),
        createRenalFunction r.ContDial r.IntDial r.PerDial r.MinGFR r.MaxGFR


    /// The rows of the sheet grouped per renal rule.
    let groups (data: RenalRuleData[]) =
        data
        |> Array.filter (fun r -> r.Generic <> "" && r.Source <> "")
        |> Array.groupBy groupKey


    /// The renal rule of the rows of one group; none for rows without a renal function.
    let ofRows logger ((gen, rte, ind, src, minAge, maxAge, dt, fr, it, rf), rules: RenalRuleData[]) =
        let limits =
            rules
            |> Array.filter (fun r -> r.DoseRed |> String.notEmpty)
            |> Array.map (fun r ->
                let times = Units.Count.times |> Some

                // the adjust unit
                let adj =
                    if r.DoseRed = DoseReduction.REL then
                        None
                    else
                        r.AdjustUnit |> Units.adjustUnit
                // the dose unit
                let du =
                    if r.DoseRed = DoseReduction.REL then
                        times
                    else
                        r.DoseUnit |> UnitsParse.fromString
                // the adjusted dose unit
                let duAdj =
                    if r.DoseRed = DoseReduction.REL then
                        times
                    else
                        match adj, du with
                        | Some adj, Some du -> du |> ValueUnit.per adj |> Some
                        | _ -> None
                // the time unit
                let tu = r.FreqUnit |> Units.timeUnit
                // the dose unit per time unit
                let duTime =
                    if r.DoseRed = DoseReduction.REL then
                        times
                    else
                        match du, tu with
                        | Some du, Some tu -> du |> ValueUnit.per tu |> Some
                        | _ -> None
                // the adjusted dose unit per time unit
                let duAdjTime =
                    if r.DoseRed = DoseReduction.REL then
                        times
                    else
                        match duAdj, tu with
                        | Some duAdj, Some tu -> duAdj |> ValueUnit.per tu |> Some
                        | _ -> None
                // the rate unit
                let ru =
                    if r.DoseRed = DoseReduction.REL then
                        times
                    else
                        r.RateUnit |> UnitsParse.fromString
                // the dose unit per rate unit
                let duRate =
                    if r.DoseRed = DoseReduction.REL then
                        times
                    else
                        match du, ru with
                        | Some du, Some ru -> du |> ValueUnit.per ru |> Some
                        | _ -> None
                // the adjusted dose unit per rate unit
                let duAdjRate =
                    if r.DoseRed = DoseReduction.REL then
                        times
                    else
                        match duAdj, ru with
                        | Some duAdj, Some ru -> duAdj |> ValueUnit.per ru |> Some
                        | _ -> None

                Limit.create
                    (r.Substance |> LimitTarget.SubstanceLimitTarget)
                    (r.DoseRed |> DoseReduction.fromString logger)
                    ((r.MinQty, r.MaxQty) |> fromTupleInclIncl du)
                    ((r.MinQtyAdj, r.MaxQtyAdj) |> fromTupleInclIncl duAdj)
                    ((r.MinPerTime, r.MaxPerTime) |> fromTupleInclIncl duTime)
                    ((r.MinPerTimeAdj, r.MaxPerTimeAdj) |> fromTupleInclIncl duAdjTime)
                    ((r.MinRate, r.MaxRate) |> fromTupleInclIncl duRate)
                    ((r.MinRateAdj, r.MaxRateAdj) |> fromTupleInclIncl duAdjRate)
            )

        let age = (minAge, maxAge) |> fromTupleInclIncl (Some Units.Time.day)

        rf |> Option.map (fun rf -> create gen rte ind src age dt fr it rf limits)


    /// Whether a row has a value in a per kg or per m2 column.
    let hasAdjustedValue (r: RenalRuleData) =
        [
            r.MinQtyAdj
            r.MaxQtyAdj
            r.MinPerTimeAdj
            r.MaxPerTimeAdj
            r.MinRateAdj
            r.MaxRateAdj
        ]
        |> List.exists Option.isSome


    /// The reasons a renal rule, given as its rows, is refused at load: both per kg and per m2
    /// bounds, which the single adjust quantity of an order cannot hold, and a per kg or per m2
    /// value without an adjust unit, which is lost. Only the rows with a replacing reduction are
    /// checked: the relative rows hold reduction factors without a unit in the same columns.
    let unitRefusals (rows: RenalRuleData[]) =
        let rows =
            rows
            |> Array.filter (fun r -> r.DoseRed |> DoseReduction.isReplace && r |> hasAdjustedValue)

        [
            let units = rows |> Array.choose (_.AdjustUnit >> Units.adjustUnit) |> Array.distinct

            if units |> Array.length > 1 then
                "it has both per kg and per m2 bounds"

            for r in rows |> Array.filter (_.AdjustUnit >> Units.adjustUnit >> Option.isNone) do
                $"the row for %s{r.Substance} has a per kg or per m2 value without an adjust unit"
        ]


    /// The groups of the sheet, each with the reasons the unit checks refuse it; no reasons for a group
    /// that passes.
    let checkedGroups data =
        data |> groups |> Array.map (fun grp -> grp, grp |> snd |> unitRefusals)


    /// The renal rules of the sheet, without those refused by the unit checks.
    let map logger (data: RenalRuleData[]) : Result<_, Message list> =
        data
        |> checkedGroups
        |> Array.filter (snd >> List.isEmpty)
        |> Array.choose (fst >> ofRows logger)
        |> Ok


    /// The renal rules refused by the unit checks, each with the reasons.
    let refused logger (data: RenalRuleData[]) =
        data
        |> checkedGroups
        |> Array.choose (fun (grp, reasons) ->
            match reasons with
            | [] -> None
            | _ -> grp |> ofRows logger |> Option.map (fun rr -> rr, reasons)
        )


    let get logger dataUrlId : Result<_, Message list> = getData dataUrlId |> Result.bind (map logger)


    /// Whether a renal rule applies to a generic, a route, an indication and a dose type. A value
    /// that is not given, and a field the renal rule leaves blank, match every value.
    let appliesTo mapping generic route indication doseType (rr: RenalRule) =
        let eqs a (b: string) = a |> Option.forall (String.equalsCapInsens b)

        rr.Generic |> eqs generic
        && (rr.Route |> String.isNullOrWhiteSpace
            || rr.Route |> Mapping.eqsRoute mapping route)
        && (rr.Indication |> String.isNullOrWhiteSpace || rr.Indication |> eqs indication)
        && (rr.DoseType = NoDoseType
            || doseType |> Option.forall (DoseType.eqsType rr.DoseType))


    /// Whether a renal rule applies to a dose rule: the generic, and the route, indication and
    /// dose type when the renal rule names them.
    let appliesToDoseRule routeMapping (rr: RenalRule) (dr: DoseRule) =
        rr
        |> appliesTo
            routeMapping
            (dr.Generic |> Generic.genericName |> Some)
            (Some dr.Route)
            (Some dr.Indication)
            (Some dr.DoseType)


    let filter mapping (filter: DoseFilter) (renalRules: RenalRule[]) =
        let pat, ageHolds = filter.Patient |> PatientCategory.forAdultMatch

        [|
            fun (rr: RenalRule) ->
                ageHolds rr.Age
                && pat.Age |> MinMax.inRange rr.Age
                // renal rules only apply to patients of at least 28 days of age
                && pat.Age
                   |> Option.forall (fun a -> Units.Time.day |> ValueUnit.singleWithValue 28N <=? a)
            appliesTo mapping filter.Generic filter.Route filter.Indication filter.DoseType
            fun (rr: RenalRule) ->
                filter.Patient.RenalFunction
                |> Option.map (fun rf ->
                    match rr.RenalFunction with
                    | ContinuousHemodialysis
                    | IntermittentHemodialysis
                    | PeritonealDialysis -> rr.RenalFunction = rf
                    | EGFR(rmin, rmax) ->
                        match rf with
                        | EGFR(pmin, pmax) ->
                            if pmin = pmax then
                                match pmin with
                                | None -> false
                                | Some v ->
                                    (rmin |> Option.map (fun rmin -> v >= rmin) |> Option.defaultValue true)
                                    && (rmax |> Option.map (fun rmax -> v <= rmax) |> Option.defaultValue true)
                            else
                                pmin = rmin && pmax = rmax
                        | _ -> false
                )
                |> Option.defaultValue false
        |]
        |> Array.fold (fun (acc: RenalRule[]) pred -> acc |> Array.filter pred) renalRules


    /// The unit a per kg or per m2 range is adjusted with: the weight or the BSA unit in its bounds.
    let measureOf (mm: MinMax) =
        [ mm.Min; mm.Max ]
        |> List.choose id
        |> List.collect (Limit.getValueUnit >> ValueUnit.getUnit >> ValueUnit.getUnits)
        |> List.tryFind (
            function
            | Weight _
            | BSA _ -> true
            | _ -> false
        )


    /// The measurements the per kg and per m2 bounds of a renal limit need.
    let limitMeasures (rl: RenalLimit) =
        [ rl.QuantityAdjust; rl.PerTimeAdjust; rl.RateAdjust ] |> List.choose measureOf


    /// Whether two adjust units measure the same: both a weight or both a BSA.
    let sameMeasure u1 u2 =
        match u1, u2 with
        | Weight _, Weight _
        | BSA _, BSA _ -> true
        | _ -> false


    /// The bounds of one dose under a replacing renal reduction. A dose has an absolute and an
    /// adjusted form, per kg or per m2, and the dose rule and the renal rule each give both. The
    /// adjust value is the patient's weight or BSA, when known, and converts a bound from one form
    /// into the other. When the renal rule gives no bound for the dose, the dose rule's maximum
    /// stays and its minimum is dropped, since no renal bound can make that minimum fit. On a side
    /// where the renal rule gives a bound, in either form, the renal bounds replace the dose rule's
    /// bounds in both forms. On a side where the renal rule gives no bound, the dose rule's bounds
    /// stay, each moved to a renal bound on the other side that it conflicts with, compared at the
    /// adjust value when the two are in different forms. Without an adjust value no per kg or per m2
    /// bound of the dose rule is dropped: a bound the renal rule does not give in that form stays.
    let replaceDose adj (renal: MinMax, renalAdj: MinMax) (dose: MinMax, doseAdj: MinMax) =
        let convert (f: ValueUnit -> ValueUnit -> ValueUnit) lim =
            adj
            |> Option.map (fun adj -> lim |> Limit.apply (fun vu -> f vu adj) (fun vu -> f vu adj))

        // the renal bounds of a side in one form: the renal rule's own, and the other form converted
        let renalBounds same other f bound =
            [ same |> bound; other |> bound |> Option.bind (convert f) ] |> List.choose id

        let gives bound =
            renal |> bound |> Option.isSome || renalAdj |> bound |> Option.isSome

        // a kept bound moves to every renal bound it conflicts with, so it ends at the tightest
        let move conflicts renalBounds kept =
            kept
            |> Option.map (fun k -> renalBounds |> List.fold (fun k r -> if conflicts k r then r else k) k)

        let bounds (mm: MinMax) (renalMm: MinMax) renalBounds =
            {
                Min =
                    if gives _.Min then
                        renalMm.Min
                    else
                        mm.Min |> move (Limit.gt true false) (renalBounds _.Max)
                Max =
                    if gives _.Max then
                        renalMm.Max
                    else
                        mm.Max |> move (Limit.st false true) (renalBounds _.Min)
            }

        let reduced, reducedAdj =
            if renal = MinMax.empty && renalAdj = MinMax.empty then
                { dose with Min = None }, { doseAdj with Min = None }
            else
                bounds dose renal (renalBounds renal renalAdj (*)),
                bounds doseAdj renalAdj (renalBounds renalAdj renal (/))

        let reducedAdj =
            match adj with
            | Some _ -> reducedAdj
            // without the patient's weight or BSA no per kg or per m2 bound of the dose rule is dropped,
            // so the rule still shows the measurement it needs
            | None ->
                {
                    Min = reducedAdj.Min |> Option.orElse doseAdj.Min
                    Max = reducedAdj.Max |> Option.orElse doseAdj.Max
                }

        reduced, reducedAdj


    let adjustDoseLimit (pat: Patient) (renalRule: RenalRule) (dl: DoseLimit) =
        let relative mmNew mmOrig =
            if mmOrig = MinMax.empty || mmNew = MinMax.empty then
                mmOrig
            else
                {
                    Min =
                        match mmOrig.Min, mmNew.Min with
                        | Some lim1, Some lim2 -> MinMax.calcLimit (*) lim1 lim2 |> Some
                        | _ -> mmOrig.Min
                    Max =
                        match mmOrig.Max, mmNew.Max with
                        | Some lim1, Some lim2 -> MinMax.calcLimit (*) lim1 lim2 |> Some
                        | _ -> mmOrig.Max
                }

        // the adjust unit of the dose limit, or, for a dose limit without one, the unit of the
        // renal rule's per kg or per m2 bounds, which the order applies at the weight or the BSA
        let adjustUnit (rl: RenalLimit) =
            dl.AdjustUnit |> Option.orElse (rl |> limitMeasures |> List.tryHead)

        // a renal per kg or per m2 range in another measurement than the dose limit's adjust unit
        // moves to the absolute form at its own measurement, and is left out without it
        let ownMeasure (renal: MinMax, renalAdj: MinMax) =
            match dl.AdjustUnit, renalAdj |> measureOf with
            | Some du, Some ru when not (sameMeasure du ru) ->
                match Some ru |> Patient.adjustValue pat with
                | Some v ->
                    let abs = renalAdj |> MinMax.apply (fun vu -> vu * v)

                    // with an absolute renal bound on the same side, the tighter of the two stays
                    let tighter isTighter lim1 lim2 =
                        match lim1, lim2 with
                        | Some l1, Some l2 -> Some(if isTighter l1 l2 then l1 else l2)
                        | _ -> lim1 |> Option.orElse lim2

                    {
                        Min = tighter (fun l1 l2 -> l1 |> Limit.gt true true <| l2) renal.Min abs.Min
                        Max = tighter (fun l1 l2 -> l1 |> Limit.st false false <| l2) renal.Max abs.Max
                    },
                    MinMax.empty
                | None -> renal, MinMax.empty
            | _ -> renal, renalAdj

        renalRule.RenalLimits
        |> Array.tryFind (fun rl -> rl.DoseLimitTarget = dl.DoseLimitTarget)
        |> function
            | None -> dl
            | Some rl ->
                match rl.DoseReduction with
                | NoReduction -> dl
                | Replace ->
                    let adjUnit = rl |> adjustUnit
                    let adj = adjUnit |> Patient.adjustValue pat

                    let dose renal = replaceDose adj (ownMeasure renal)

                    let qty, qtyAdj = (dl.Quantity, dl.QuantityAdjust) |> dose (rl.Quantity, rl.QuantityAdjust)
                    let perTime, perTimeAdj = (dl.PerTime, dl.PerTimeAdjust) |> dose (rl.PerTime, rl.PerTimeAdjust)
                    let rate, rateAdj = (dl.Rate, dl.RateAdjust) |> dose (rl.Rate, rl.RateAdjust)

                    { dl with
                        // a dose limit without an adjust unit takes the unit of the renal per kg or
                        // per m2 bounds, so the order applies them at that measurement
                        AdjustUnit = adjUnit
                        Quantity = qty
                        QuantityAdjust = qtyAdj
                        PerTime = perTime
                        PerTimeAdjust = perTimeAdj
                        Rate = rate
                        RateAdjust = rateAdj
                    }
                | Relative ->
                    { dl with
                        Quantity = dl.Quantity |> relative rl.Quantity
                        QuantityAdjust = dl.QuantityAdjust |> relative rl.QuantityAdjust
                        PerTime = dl.PerTime |> relative rl.PerTime
                        PerTimeAdjust = dl.PerTimeAdjust |> relative rl.PerTimeAdjust
                        Rate = dl.Rate |> relative rl.Rate
                        RateAdjust = dl.RateAdjust |> relative rl.RateAdjust
                    }


    /// The dose rule under a renal rule. A dose rule without an adjust unit takes the adjust unit
    /// that a dose limit took from the renal rule.
    let adjustDoseRule pat (renalRule: RenalRule) (doseRule: DoseRule) =

        { doseRule with
            RenalRuleSource = Some renalRule.Source
            Frequencies =
                if renalRule.Frequencies |> Option.isNone then
                    doseRule.Frequencies
                else
                    renalRule.Frequencies
            ComponentLimits =
                doseRule.ComponentLimits
                |> Array.map (fun dl ->
                    { dl with SubstanceLimits = dl.SubstanceLimits |> Array.map (adjustDoseLimit pat renalRule) }
                )
        }
        |> fun dr ->
            { dr with
                AdjustUnit =
                    dr.AdjustUnit
                    |> Option.orElse (
                        dr.ComponentLimits
                        |> Array.collect _.SubstanceLimits
                        |> Array.tryPick _.AdjustUnit
                    )
            }


    /// Whether a renal function is reduced: an eGFR range that stays at or below 90 mL/min, or
    /// a form of dialysis.
    let isReduced =
        function
        | EGFR(_, Some max) -> max <= 90
        | EGFR(_, None) -> false
        | IntermittentHemodialysis
        | ContinuousHemodialysis
        | PeritonealDialysis -> true


    /// The warnings for the renal rules checked against the dose rules they apply to. A renal rule
    /// applies to the dose rules of its generic, and of its route, indication and dose type when
    /// it names them. Only a replacing reduction is checked. A renal rule whose per kg or per m2
    /// bound has another measurement than the dose rule's adjust unit is reported for every
    /// renal function. A renal rule for a reduced kidney function with a higher maximum than the
    /// dose rule is reported, comparing a maximum only with one of the same kind and form, since
    /// there is no patient to convert a per kg bound with. The rules are still used as the sheet
    /// gives them.
    let doseRuleWarnings routeMapping (doseRules: DoseRule[]) (renalRules: RenalRule[]) =
        let raises (dose: MinMax) (renal: MinMax) =
            match dose.Max, renal.Max with
            | Some d, Some r when ValueUnit.eqsGroup (d |> Limit.getValueUnit) (r |> Limit.getValueUnit) ->
                r |> Limit.gt false false <| d
            | _ -> false

        [
            for rr in renalRules do
                for dr in doseRules |> Array.filter (appliesToDoseRule routeMapping rr) do
                    for rl in rr.RenalLimits |> Array.filter (fun rl -> rl.DoseReduction = Replace) do
                        for dl in
                            dr.ComponentLimits
                            |> Array.collect _.SubstanceLimits
                            |> Array.filter (fun dl -> dl.DoseLimitTarget = rl.DoseLimitTarget) do
                            let target = dl.DoseLimitTarget |> LimitTarget.toString
                            let rf = rr.RenalFunction |> RenalFunction.toString

                            let renal =
                                $"the renal rule %s{rr.Generic} %s{rr.Route} %s{rr.Indication} (%s{rr.Source}, %s{rf})"

                            let dose =
                                $"the dose rule %s{dr.Generic |> Generic.genericName} %s{dr.Route} %s{dr.Indication} (%s{dr.Source |> Source.toString}, %s{dr.PatientText})"

                            let otherMeasure =
                                rl
                                |> limitMeasures
                                |> List.exists (fun ru -> dl.AdjustUnit |> Option.exists (sameMeasure ru >> not))

                            if otherMeasure then
                                Warning
                                    $"%s{renal} has a per kg or per m2 bound for %s{target} in another measurement than %s{dose}"

                            if rr.RenalFunction |> isReduced then
                                let kinds =
                                    [
                                        "dose", dl.Quantity, rl.Quantity
                                        "dose per kg or m2", dl.QuantityAdjust, rl.QuantityAdjust
                                        "dose per time", dl.PerTime, rl.PerTime
                                        "dose per time per kg or m2", dl.PerTimeAdjust, rl.PerTimeAdjust
                                        "rate", dl.Rate, rl.Rate
                                        "rate per kg or m2", dl.RateAdjust, rl.RateAdjust
                                    ]
                                    |> List.filter (fun (_, dose, renal) -> raises dose renal)
                                    |> List.map (fun (kind, _, _) -> kind)

                                if kinds |> List.isEmpty |> not then
                                    let kinds = kinds |> String.concat ", "
                                    Warning $"%s{renal} has a higher maximum %s{kinds} for %s{target} than %s{dose}"
        ]


    /// The renal rule as a sheet author finds it.
    let describe (rr: RenalRule) =
        let rf = rr.RenalFunction |> RenalFunction.toString
        $"the renal rule %s{rr.Generic} %s{rr.Route} %s{rr.Indication} (%s{rr.Source}, %s{rf})"


    /// The dose rules without those a refused renal rule applies to, and one message per refused
    /// renal rule. A refused renal rule matches no patient, so without this a patient in its eGFR
    /// range would get the dose rule's full dose.
    let withoutRefused routeMapping (refused: (RenalRule * string list)[]) (doseRules: DoseRule[]) =
        let applies dr =
            refused |> Array.exists (fun (rr, _) -> appliesToDoseRule routeMapping rr dr)

        doseRules |> Array.filter (applies >> not),
        [
            for rr, reasons in refused do
                let n = doseRules |> Array.filter (appliesToDoseRule routeMapping rr) |> Array.length
                Warning
                    $"""%s{describe rr} is left out: %s{reasons |> String.concat "; "}; %i{n} dose rules of %s{rr.Generic} are left out with it, until the sheet is fixed"""
        ]
