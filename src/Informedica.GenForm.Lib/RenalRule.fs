namespace Informedica.GenForm.Lib


module RenalRule =

    open MathNet.Numerics
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open ConsoleWriter.NewLineNoTime

    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges

    open Utils


    module DoseReduction =

        let [<Literal>] REL = "rel"
        let [<Literal>] ABS = "abs"

        let fromString s =
            match s with
            | _ when s |> String.equalsCapInsens ABS -> Absolute
            | _ when s |> String.equalsCapInsens REL -> Relative
            | _ ->
                writeWarningMessage $"{s} is not a valid dosereduction"
                NoReduction


        let toString = function
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


    let getData dataUrlId : GenFormResult<_> =
        try
            Web.getDataFromSheet dataUrlId "RenalRules"
            |> fun data ->
                let getColumn =
                    data
                    |> Array.head
                    |> Csv.getStringColumn

                data
                |> Array.tail
                |> Array.map (fun r ->
                    let get =
                        getColumn r >> String.trim
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
            |> GenFormResult.createOkNoMsgs
        with
        | exn ->
            GenFormResult.createError "Error in RenalRule.getDetails: " exn


    let fromTupleInclExcl = MinMax.fromTuple Inclusive Exclusive


    let fromTupleInclIncl = MinMax.fromTuple Inclusive Inclusive


    let createRenalFunction
        contDial
        intDial
        perDial
        minGFR
        maxGFR
        =
        match minGFR, maxGFR with
        | None, None ->
            match contDial, intDial, perDial with
            | "x", _, _ -> ContinuousHemodialysis |> Some
            | _, "x", _ -> IntermittentHemodialysis |> Some
            | _, _, "x" -> PeritonealDialysis |> Some
            | _, _, _ -> None
        | None, Some max ->
            (None, max |> BigRational.ToInt32 |>Some)
            |> EGFR |> Some
        | Some min, None ->
            (min |> BigRational.ToInt32 |> Some, None)
            |> EGFR |> Some
        | Some min, Some max ->
            (min |> BigRational.ToInt32 |> Some, max |> BigRational.ToInt32 |> Some)
            |> EGFR |> Some


    let map (data: RenalRuleData[], _) : GenFormResult<_> =
        data
        |> Array.filter (fun r ->
            r.Generic <> "" &&
            r.Source <> ""
        )
        |> Array.choose (fun r ->
            createRenalFunction
                r.ContDial
                r.IntDial
                r.PerDial
                r.MinGFR
                r.MaxGFR
            |> Option.map (fun rf ->
                {| r with RenalFunction = rf |}
            )
        )
        |> Array.groupBy (fun r ->
            r.Generic,
            r.Route,
            r.Indication,
            r.Source,
            r.MinAge,
            r.MaxAge,
            DoseType.fromString r.DoseType r.DoseText,
            match r.FreqUnit |> Units.freqUnit with
            | None -> None
            | Some u ->
                if r.Frequencies |> Array.isEmpty then None
                else
                    r.Frequencies
                    |> ValueUnit.withUnit u
                    |> Some
            ,
            (r.MinInterval, r.MaxInterval)
            |> fromTupleInclIncl (r.IntervalUnit |> Utils.Units.timeUnit)
            ,
            r.RenalFunction
        )
        |> Array.map (fun ((gen, rte, ind, src, minAge, maxAge, dt, fr, it, rf), rules) ->
            let limits =
                rules
                |> Array.filter (fun r -> r.DoseRed |> String.notEmpty)
                |> Array.map (fun r ->
                    let times =
                        Units.Count.times
                        |> Some

                    // the adjust unit
                    let adj =
                        if r.DoseRed = DoseReduction.REL then None
                        else
                            r.AdjustUnit |> Utils.Units.adjustUnit
                    // the dose unit
                    let du =
                        if r.DoseRed = DoseReduction.REL then times
                        else
                            r.DoseUnit |> Units.fromString
                    // the adjusted dose unit
                    let duAdj =
                        if r.DoseRed = DoseReduction.REL then times
                        else
                            match adj, du with
                            | Some adj, Some du ->
                                du
                                |> Units.per adj
                                |> Some
                            | _ -> None
                    // the time unit
                    let tu = r.FreqUnit |> Utils.Units.timeUnit
                    // the dose unit per time unit
                    let duTime =
                        if r.DoseRed = DoseReduction.REL then times
                        else
                            match du, tu with
                            | Some du, Some tu ->
                                du
                                |> Units.per tu
                                |> Some
                            | _ -> None
                    // the adjusted dose unit per time unit
                    let duAdjTime =
                        if r.DoseRed = DoseReduction.REL then times
                        else
                            match duAdj, tu with
                            | Some duAdj, Some tu ->
                                duAdj
                                |> Units.per tu
                                |> Some
                            | _ -> None
                    // the rate unit
                    let ru =
                        if r.DoseRed = DoseReduction.REL then times
                        else
                            r.RateUnit |> Units.fromString
                    // the dose unit per rate unit
                    let duRate =
                        if r.DoseRed = DoseReduction.REL then times
                        else
                            match du, ru with
                            | Some du, Some ru ->
                                du
                                |> Units.per ru
                                |> Some
                            | _ -> None
                    // the adjusted dose unit per rate unit
                    let duAdjRate =
                        if r.DoseRed = DoseReduction.REL then times
                        else
                            match duAdj, ru with
                            | Some duAdj, Some ru ->
                                duAdj
                                |> Units.per ru
                                |> Some
                            | _ -> None

                    Limit.create
                        (r.Substance |> LimitTarget.SubstanceLimitTarget)
                        (r.DoseRed |> DoseReduction.fromString)
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

            create
                gen
                rte
                ind
                src
                age
                dt
                fr
                it
                rf
                limits
        )
        |> GenFormResult.createOkNoMsgs


    let get dataUrlId : GenFormResult<_> =
        getData dataUrlId
        |> Result.bind map


    let filter mapping (filter : DoseFilter) (renalRules : RenalRule []) =
        let eqs a (b : string) =
            a
            |> Option.map (fun x ->
                x |> String.equalsCapInsens b
            )
            |> Option.defaultValue true

        [|
            fun (rr : RenalRule) ->
                filter.Patient.Age
                |> Option.map (fun a ->
                    (rr.Age |> MinMax.isEmpty ||
                    Some a |> Utils.MinMax.inRange rr.Age) &&
                    // renal rules only applies to patients at least 28 days of age
                    Units.Time.day |> ValueUnit.singleWithValue 28N <=? a
                ) |> Option.defaultValue false
            fun (rr : RenalRule) -> rr.Generic |> eqs filter.Generic
            fun (rr : RenalRule) ->
                rr.Route |> String.isNullOrWhiteSpace ||
                (filter.Route |> Option.isNone || rr.Route |> Mapping.eqsRoute mapping filter.Route)
            fun (rr : RenalRule) ->
                rr.Indication |> String.isNullOrWhiteSpace ||
                (filter.Indication |> Option.isNone || rr.Indication |> eqs filter.Indication)
            fun (rr : RenalRule) ->
                rr.DoseType = NoDoseType ||
                filter.DoseType
                |> Option.map (DoseType.eqsType rr.DoseType)
                |> Option.defaultValue true
            fun (rr : RenalRule) ->
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
                                    (rmin |> Option.map (fun rmin -> v >= rmin) |> Option.defaultValue true) &&
                                    (rmax |> Option.map (fun rmax -> v <= rmax) |> Option.defaultValue true)
                            else
                                pmin = rmin && pmax = rmax
                        | _ -> false
                )
                |> Option.defaultValue false
        |]
        |> Array.fold (fun (acc : RenalRule[]) pred ->
            acc |> Array.filter pred
        ) renalRules


    let adjustDoseLimit (renalRule : RenalRule) (doseRule : DoseRule) (dl : DoseLimit) =
        let adjustVU dr vuNew vuOrig =
            match dr with
            | Relative ->
                vuOrig
                |> Option.map (fun vu1 ->
                    vuNew
                    |> Option.map (fun vu2 ->
                        vu1 * vu2
                    )
                    |> Option.defaultValue vu1
                )
            | Absolute ->
                match vuOrig, vuNew with
                | Some v1, Some v2 ->
                    if v1 <? v2 then v1 else v2
                    |> Some
                | _ -> vuNew
            | NoReduction -> vuOrig

        let adjustMinMax dr mmNew mmOrig =
            match dr with
            | Absolute ->
                if mmNew = MinMax.empty then mmNew
                else
                    {
                        Min =
                            match mmOrig.Min, mmNew.Min with
                            | Some lim1, Some lim2 ->
                                // if lim1 < lim2 then lim1 else lim2
                                if lim1 |> (Limit.st true true) <| lim2 then lim1 |> Some
                                else
                                    lim2 |> Some
                            | _ -> mmNew.Min
                        Max =
                            match mmOrig.Max, mmNew.Max with
                            | Some lim1, Some lim2 ->
                                // if lim1 < lim2 then lim1 else lim2
                                if lim1 |> (Limit.st false false) <| lim2 then lim1 |> Some
                                else
                                    lim2 |> Some
                            | _ -> mmNew.Max
                    }
            | Relative ->
                if mmOrig = MinMax.empty || mmNew = MinMax.empty then mmOrig
                else
                    {
                        Min =
                            match mmOrig.Min, mmNew.Min with
                            | Some lim1, Some lim2 ->
                                MinMax.calcLimit (*) lim1 lim2
                                |> Some
                            | _ -> mmOrig.Min
                        Max =
                            match mmOrig.Max, mmNew.Max with
                            | Some lim1, Some lim2 ->
                                MinMax.calcLimit (*) lim1 lim2
                                |> Some
                            | _ -> mmOrig.Max
                    }
            | NoReduction -> mmOrig


        renalRule.RenalLimits
        |> Array.tryFind (fun rl -> rl.DoseLimitTarget = dl.DoseLimitTarget)
        |> function
            | None -> dl
            | Some rl when rl.DoseReduction = NoReduction -> dl
            | Some rl ->
                let normQtyAdj =
                    if dl.NormQuantityAdjust |> Option.isSome then dl.NormQuantityAdjust
                    else
                        dl.NormPerTimeAdjust
                        |> Option.bind (fun vu ->
                            doseRule.Frequencies
                            |> Option.map (fun f -> vu / f)
                        )
                { dl with
                    Quantity =
                        dl.Quantity
                        |> adjustMinMax
                            rl.DoseReduction
                            rl.Quantity
                    NormQuantityAdjust =
                        normQtyAdj
                        |> adjustVU
                            rl.DoseReduction
                            rl.NormQuantityAdjust
                    QuantityAdjust =
                        dl.QuantityAdjust
                        |> adjustMinMax
                            rl.DoseReduction
                            rl.QuantityAdjust
                    PerTime =
                        dl.PerTime
                        |> adjustMinMax
                            rl.DoseReduction
                            rl.PerTime
                    NormPerTimeAdjust =
                        if rl.NormQuantityAdjust |> Option.isSome ||
                           rl.QuantityAdjust <> MinMax.empty then None
                        else
                            dl.NormPerTimeAdjust
                            |> adjustVU
                                rl.DoseReduction
                                rl.NormPerTimeAdjust
                    PerTimeAdjust =
                        dl.PerTimeAdjust
                        |> adjustMinMax
                            rl.DoseReduction
                            rl.PerTimeAdjust
                    Rate =
                        dl.Rate
                        |> adjustMinMax
                            rl.DoseReduction
                            rl.Rate
                    RateAdjust =
                        dl.RateAdjust
                        |> adjustMinMax
                            rl.DoseReduction
                            rl.RateAdjust

                }


    let adjustDoseRule (renalRule : RenalRule) (doseRule : DoseRule) =

        { doseRule with
            RenalRule = Some renalRule.Source
            Frequencies =
                if renalRule.Frequencies |> Option.isNone then doseRule.Frequencies
                else
                    renalRule.Frequencies
            ComponentLimits =
                doseRule.ComponentLimits
                |> Array.map (fun dl ->
                    { dl with
                        SubstanceLimits =
                            dl.SubstanceLimits
                            |> Array.map (fun dl ->
                                dl |> adjustDoseLimit renalRule doseRule
                            )
                    }
                )
        }