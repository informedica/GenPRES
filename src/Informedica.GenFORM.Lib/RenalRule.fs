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
        let ABS = "abs"

        let fromString logger s =
            match s with
            | _ when s |> String.equalsCapInsens ABS -> Absolute
            | _ when s |> String.equalsCapInsens REL -> Relative
            | _ ->
                Warning $"{s} is not a valid dosereduction" |> Logging.logWarning logger
                NoReduction


        let toString =
            function
            | Absolute -> ABS
            | Relative -> REL
            | NoReduction -> ""


    module Limit =

        /// <summary>
        /// Create a RenalLimit
        /// </summary>
        /// <returns>
        /// RenalLimit
        /// </returns>
        let create
            doseLimitTarget
            doseReduction
            quantity
            normQuantityAdjust
            quantityAdjust
            perTime
            normPerTimeAdjust
            perTimeAdjust
            rate
            rateAdjust
            =
            {
                DoseLimitTarget = doseLimitTarget
                DoseReduction = doseReduction
                Quantity = quantity
                NormQuantityAdjust = normQuantityAdjust
                QuantityAdjust = quantityAdjust
                PerTime = perTime
                NormPerTimeAdjust = normPerTimeAdjust
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
                        NormQtyAdj = get "NormQtyAdj" |> String.replace " - " ";" |> BigRational.toBrs
                        MinQtyAdj = get "MinQtyAdj" |> toBrOpt
                        MaxQtyAdj = get "MaxQtyAdj" |> toBrOpt
                        MinPerTime = get "MinPerTime" |> toBrOpt
                        MaxPerTime = get "MaxPerTime" |> toBrOpt
                        NormPerTimeAdj = get "NormPerTimeAdj" |> String.replace " - " ";" |> BigRational.toBrs
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


    let map logger (data: RenalRuleData[]) : Result<_, Message list> =
        data
        |> Array.filter (fun r -> r.Generic <> "" && r.Source <> "")
        |> Array.choose (fun r ->
            createRenalFunction r.ContDial r.IntDial r.PerDial r.MinGFR r.MaxGFR
            |> Option.map (fun rf -> {| r with RenalFunction = rf |})
        )
        |> Array.groupBy (fun r ->
            r.Generic
            , r.Route
            , r.Indication
            , r.Source
            , r.MinAge
            , r.MaxAge
            , DoseType.fromString r.DoseType r.DoseText
            , match r.FreqUnit |> Units.freqUnit with
              | None -> None
              | Some u ->
                  if r.Frequencies |> Array.isEmpty then
                      None
                  else
                      r.Frequencies |> ValueUnit.withUnit u |> Some
            , (r.MinInterval, r.MaxInterval)
              |> fromTupleInclIncl (r.IntervalUnit |> Units.timeUnit)
            , r.RenalFunction
        )
        |> Array.map (fun ((gen, rte, ind, src, minAge, maxAge, dt, fr, it, rf), rules) ->
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
                        (r.NormQtyAdj |> ValueUnit.withArrayAndOptUnit duAdj)
                        ((r.MinQtyAdj, r.MaxQtyAdj) |> fromTupleInclIncl duAdj)
                        ((r.MinPerTime, r.MaxPerTime) |> fromTupleInclIncl duTime)
                        (r.NormPerTimeAdj |> ValueUnit.withArrayAndOptUnit duAdjTime)
                        ((r.MinPerTimeAdj, r.MaxPerTimeAdj) |> fromTupleInclIncl duAdjTime)
                        ((r.MinRate, r.MaxRate) |> fromTupleInclIncl duRate)
                        ((r.MinRateAdj, r.MaxRateAdj) |> fromTupleInclIncl duAdjRate)
                )

            let age = (minAge, maxAge) |> fromTupleInclIncl (Some Units.Time.day)

            create gen rte ind src age dt fr it rf limits
        )
        |> Ok


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


    /// Whether two adjust units measure the same: both a weight or both a BSA.
    let sameMeasure u1 u2 =
        match u1, u2 with
        | Weight _, Weight _
        | BSA _, BSA _ -> true
        | _ -> false


    /// The bounds of one dose under an absolute renal reduction. A dose has an absolute and an
    /// adjusted form, per kg or per m2, and the dose rule and the renal rule each give both. The
    /// adjust value is the patient's weight or BSA, when known, and converts a bound from one form
    /// into the other. When the renal rule gives no bound for the dose, the dose rule's maximum
    /// stays and its minimum is dropped, since no renal bound can make that minimum fit. On a side
    /// where the renal rule gives a bound, in either form, the renal bounds replace the dose rule's
    /// bounds in both forms. On a side where the renal rule gives no bound, the dose rule's bounds
    /// stay, each moved to a renal bound on the other side that it conflicts with, compared at the
    /// adjust value when the two are in different forms.
    let absoluteDose adj (renal: MinMax, renalAdj: MinMax) (dose: MinMax, doseAdj: MinMax) =
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

        if renal = MinMax.empty && renalAdj = MinMax.empty then
            { dose with Min = None }, { doseAdj with Min = None }
        else
            bounds dose renal (renalBounds renal renalAdj (*)), bounds doseAdj renalAdj (renalBounds renalAdj renal (/))


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
            dl.AdjustUnit
            |> Option.orElse ([ rl.QuantityAdjust; rl.PerTimeAdjust; rl.RateAdjust ] |> List.tryPick measureOf)

        // a renal per kg or per m2 range in another measurement than the dose limit's adjust unit
        // moves to the absolute form at its own measurement, and is left out without it
        let ownMeasure (renal: MinMax, renalAdj: MinMax) =
            match dl.AdjustUnit, renalAdj |> measureOf with
            | Some du, Some ru when not (sameMeasure du ru) ->
                match Some ru |> Patient.adjustValue pat with
                | Some v ->
                    let abs = renalAdj |> MinMax.apply (fun vu -> vu * v)

                    {
                        Min = renal.Min |> Option.orElse abs.Min
                        Max = renal.Max |> Option.orElse abs.Max
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
                | Absolute ->
                    let adj = rl |> adjustUnit |> Patient.adjustValue pat

                    let dose renal = absoluteDose adj (ownMeasure renal)

                    let qty, qtyAdj = (dl.Quantity, dl.QuantityAdjust) |> dose (rl.Quantity, rl.QuantityAdjust)
                    let perTime, perTimeAdj = (dl.PerTime, dl.PerTimeAdjust) |> dose (rl.PerTime, rl.PerTimeAdjust)
                    let rate, rateAdj = (dl.Rate, dl.RateAdjust) |> dose (rl.Rate, rl.RateAdjust)

                    { dl with
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
    /// it names them. Only an absolute reduction is checked. A renal rule whose per kg or per m2
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

        let applies rr (dr: DoseRule) =
            rr
            |> appliesTo
                routeMapping
                (dr.Generic |> Generic.genericName |> Some)
                (Some dr.Route)
                (Some dr.Indication)
                (Some dr.DoseType)

        [
            for rr in renalRules do
                for dr in doseRules |> Array.filter (applies rr) do
                    for rl in rr.RenalLimits |> Array.filter (fun rl -> rl.DoseReduction = Absolute) do
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
                                [ rl.QuantityAdjust; rl.PerTimeAdjust; rl.RateAdjust ]
                                |> List.choose measureOf
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
