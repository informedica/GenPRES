namespace Informedica.GenForm.Lib


module DoseRule =

    open System
    open Informedica.Utils.Lib.BCL

    open Informedica.Utils.Lib
    open Informedica.GenCore.Lib.Ranges

    open Utils


    module Print =


        open Informedica.GenUnits.Lib

        let printFreqs (r: DoseRule) =
            r.Frequencies
            |> Option.map (Utils.ValueUnit.toString 0)
            |> Option.defaultValue ""


        let printInterval (dr: DoseRule) =
            if dr.IntervalTime = MinMax.empty then
                ""
            else
                dr.IntervalTime
                |> MinMax.toString "min. interval " "min. interval " "max. interval " "max. interval "


        let printTime (dr: DoseRule) =
            if dr.AdministrationTime = MinMax.empty then
                ""
            else
                dr.AdministrationTime |> MinMax.toString "min. " "min. " "max. " "max. "


        let printDuration (dr: DoseRule) =
            if dr.Duration = MinMax.empty then
                ""
            else
                dr.Duration
                |> MinMax.toString "min. duur " "min. duur " "max. duur " "max. duur "


        let printDose wrap (dr: DoseRule) =
            let substDls = dr.ComponentLimits |> Array.collect _.SubstanceLimits

            let formDls = dr.ComponentLimits |> Array.choose _.Limit

            let useSubstDl = substDls |> Array.length > 0
            // only use form dose limits if there are no substance dose limits
            if useSubstDl then substDls else formDls
            |> Array.map (fun dl ->
                let perDose = "/dosis"
                let emptyS = ""
                let printMinMaxDose = DoseLimit.printMinMaxDose ""

                [
                    $"%s{dl.Rate |> printMinMaxDose emptyS}"
                    $"%s{dl.RateAdjust |> printMinMaxDose emptyS}"
                    $"%s{dl.PerTimeAdjust |> printMinMaxDose emptyS}"

                    $"%s{dl.PerTime |> printMinMaxDose emptyS}"
                    $"%s{dl.QuantityAdjust |> printMinMaxDose perDose}"

                    $"%s{dl.Quantity |> printMinMaxDose perDose}"
                ]
                |> List.map String.trim
                |> List.filter (String.IsNullOrEmpty >> not)
                |> String.concat ", "
                |> fun s -> $"%s{dl.DoseLimitTarget |> LimitTarget.toString} %s{wrap}%s{s}{wrap}"
            )


        /// <summary>
        /// Render the dose rules of <b>one</b> generic as markdown: a generic heading, then
        /// per route the products and synonyms, per patient category and indication the
        /// dose type, schedule, formulary link and dose limits.
        /// </summary>
        /// <remarks>
        /// Per-generic by contract: the outer fold keeps the last generic it sees, so a
        /// multi-generic array yields only the last one. Use <c>printGenerics</c> for
        /// several. On the anonymous record in the fold, see
        /// https://github.com/dotnet/fsharp/issues/6699.
        /// </remarks>
        /// <param name="getLink">
        /// Resolves the external formulary link per dose rule. Passed in rather than
        /// looked up here: the Nederlands Kinder Formularium index it needs is a registered
        /// resource (<c>Resources.Keys.nkfLinkProvider</c>), so a failed fetch degrades to
        /// an empty index — rendered below as the existing "*Lokaal*" fallback — is
        /// reported as a resource <c>Warning</c>, and is refreshed by an admin
        /// ReloadResources. Keeping the fetch here made this module impure and its cache
        /// unreachable by that reload. See issue #529.
        /// </param>
        /// <param name="rules">The dose rules to render.</param>
        let toMarkdown (getLink: Source.LinkProvider) (rules: DoseRule array) =
            let generic_md generic = $"\n\n# %s{generic}\n\n---\n"

            let route_md route products synonyms =
                if synonyms |> String.isNullOrWhiteSpace then
                    $"\n\n### Route: %s{route}\n\n#### Producten\n%s{products}\n"
                else
                    $"\n\n### Route: %s{route}\n\n#### Producten\n%s{products}\n\n#### Synoniemen\n%s{synonyms}\n"

            let product_md product = $"* %s{product}"

            let synonyms_md names =
                if names |> Seq.isEmpty then
                    ""
                else
                    let names = names |> String.concat ", "
                    $"* %s{names}"

            let indication_md indication =
                $"\n\n## Indicatie: %s{indication}\n\n---\n"

            let doseCapt_md = "\n\n#### Doseringen\n\n"

            let dose_md dt dose freqs intv time dur =
                let dt = dt |> DoseType.toDescription

                let freqs =
                    if freqs |> String.isNullOrWhiteSpace then
                        ""
                    else
                        $" in %s{freqs}"

                let s =
                    [
                        if intv |> String.isNullOrWhiteSpace |> not then
                            $" %s{intv}"
                        if time |> String.isNullOrWhiteSpace |> not then
                            $" inloop tijd %s{time}"
                        if dur |> String.isNullOrWhiteSpace |> not then
                            $" %s{dur}"
                    ]
                    |> String.concat ", "
                    |> fun s ->
                        if s |> String.isNullOrWhiteSpace then
                            ""
                        else
                            $" (%s{s |> String.trim})"

                $"* *%s{dt}*: %s{dose}%s{freqs}%s{s}"

            let patient_md patient =
                let patient =
                    if patient |> String.notEmpty then
                        patient
                    else
                        "alle patienten"

                $"\n\n##### Patient: **%s{patient}**\n\n"

            let printDoses (rules: DoseRule array) =
                ("", rules |> Array.groupBy _.DoseType)
                ||> Array.fold (fun acc (dt, ds) ->
                    let pedForm =
                        let link =
                            ds
                            |> Array.tryHead
                            |> Option.bind (fun dr -> dr.Generic.Label |> getLink dr.Source)
                            |> Option.defaultValue "*Lokaal*"

                        ds
                        |> Array.map _.ScheduleText
                        |> Array.distinct
                        |> function
                            | [| s |] -> $"\n\n%s{link}: %s{s}"
                            | _ -> ""

                    ds
                    |> Array.fold
                        (fun acc r ->
                            let dose = r |> printDose "" |> Array.distinct |> String.concat ", "

                            let freqs = r |> printFreqs
                            let intv = r |> printInterval
                            let time = r |> printTime
                            let dur = r |> printDuration

                            let md = dose_md dt dose freqs intv time dur

                            if acc |> String.containsCapsInsens md then
                                acc // prevent duplicate doserule per form print
                            else
                                $"%s{acc}\n%s{md}%s{pedForm}"

                        )
                        acc
                )

            ({|
                md = ""
                rules = [||]
             |},
             rules |> Array.groupBy _.Generic)
            ||> Array.fold (fun acc (generic, rs) ->
                {| acc with
                    md = generic_md (generic |> Generic.toString)
                    rules = rs
                |}
                |> fun r ->
                    if r.rules = Array.empty then
                        r
                    else
                        (r, r.rules |> Array.groupBy _.Indication)
                        ||> Array.fold (fun acc (indication, rs) ->
                            {| acc with
                                md = acc.md + (indication_md indication)
                                rules = rs
                            |}
                            |> fun r ->
                                if r.rules = Array.empty then
                                    r
                                else
                                    (r, r.rules |> Array.groupBy _.Route)
                                    ||> Array.fold (fun acc (route, rs) ->
                                        let prods =
                                            rs
                                            |> Array.collect _.ComponentLimits
                                            |> Array.collect _.Products
                                            |> Array.sortBy (fun p ->
                                                p.Substances
                                                |> Array.sumBy (fun s ->
                                                    s.Concentration
                                                    |> Option.map ValueUnit.getValue
                                                    |> Option.bind Array.tryHead
                                                    |> Option.defaultValue 0N
                                                )
                                            )
                                            |> Array.map (fun p ->
                                                if p.GPK |> String.IsNullOrWhiteSpace then
                                                    p.Label
                                                else
                                                    $"{p.GPK} - {p.Label}"
                                                |> product_md
                                            )
                                            |> Array.distinct
                                            |> String.concat "\n"

                                        let synonyms =
                                            rs
                                            |> Array.collect _.ComponentLimits
                                            |> Array.collect _.Products
                                            |> Array.collect _.Synonyms
                                            |> Array.distinct
                                            |> synonyms_md

                                        {| acc with
                                            md = acc.md + (route_md route prods synonyms) + doseCapt_md
                                            rules = rs
                                        |}
                                        |> fun r ->
                                            if r.rules = Array.empty then
                                                r
                                            else
                                                (r,
                                                 r.rules
                                                 |> Array.sortBy (fun d ->
                                                     d.PatientCategory |> PatientCategory.sortBy
                                                 )
                                                 |> Array.groupBy _.PatientCategory)
                                                ||> Array.fold (fun acc (pat, rs) ->
                                                    let doses =
                                                        rs
                                                        |> Array.sortBy (fun r -> r.DoseType |> DoseType.sortBy)
                                                        |> printDoses

                                                    let pat = pat |> PatientCategory.toString

                                                    {| acc with
                                                        rules = rs
                                                        md = acc.md + (patient_md pat) + $"\n{doses}"
                                                    |}
                                                )
                                    )
                        )
            )
            |> _.md


        let printGenerics (getLink: Source.LinkProvider) generics (doseRules: DoseRule[]) =
            doseRules
            |> generics
            |> Array.sort
            |> Array.map (fun g -> doseRules |> Array.filter (fun dr -> dr.Generic = g) |> toMarkdown getLink)


    open Informedica.GenUnits.Lib

    module GenPresProduct = Informedica.ZIndex.Lib.GenPresProduct
    module GenericProduct = Informedica.ZIndex.Lib.GenericProduct


    /// <summary>
    /// Reconstitute the products in a DoseRule that require reconstitution.
    /// </summary>
    /// <param name="mapping"></param>
    /// <param name="dep">The Department to select the reconstitution</param>
    /// <param name="loc">The VenousAccess location to select the reconstitution</param>
    /// <param name="dr">The DoseRule</param>
    let reconstitute mapping loc dep (dr: DoseRule) =
        let warns = ResizeArray<string>()
        let reconstitute = Product.reconstitute mapping loc dep

        let dr =
            { dr with
                ComponentLimits =
                    dr.ComponentLimits
                    |> Array.map (fun dl ->
                        { dl with
                            Products =
                                dl.Products
                                |> Array.collect (fun prod ->
                                    let prods, newWarns = reconstitute dr.Route prod
                                    warns.AddRange(newWarns)
                                    prods
                                )
                        }
                    )
            }

        dr, warns |> Seq.distinct


    let fromTupleInclExcl = MinMax.fromTuple Inclusive Exclusive


    let fromTupleInclIncl = MinMax.fromTuple Inclusive Inclusive


    let addFormLimits routeMapping formRoutes (dr: DoseRule) =
        let prods = dr.ComponentLimits |> Array.collect _.Products

        let droplets =
            prods
            |> Array.filter (fun p -> p.Form |> String.containsCapsInsens "druppel")
            |> Array.choose _.Divisible
            |> Array.distinct
            |> Array.tryExactlyOne

        let setDroplet vu =
            let v, u = vu |> ValueUnit.get

            match droplets with
            | None -> vu
            | Some m -> u |> Units.Volume.dropletSetDropsPerMl m |> ValueUnit.withValue v

        if dr.Generic.Form |> PharmaceuticalForm.toString |> String.isNullOrWhiteSpace then
            dr
        else
            prods
            |> Array.map _.FormUnit
            |> Array.tryExactlyOne
            |> Option.defaultValue NoUnit
            |> Mapping.filterFormRoutes
                routeMapping
                formRoutes
                dr.Route
                (dr.Generic.Form |> PharmaceuticalForm.toString)
            |> Array.map (fun rsu ->
                { DoseLimit.limit with
                    DoseLimitTarget = OrderableLimitTarget
                    Quantity =
                        {
                            Min = rsu.MinDoseQty |> Option.map Limit.Inclusive
                            Max = rsu.MaxDoseQty |> Option.map Limit.Inclusive
                        }
                    QuantityAdjust =
                        match dr.AdjustUnit with
                        | Some unt when unt |> Units.eqsUnit Units.Weight.kiloGram ->
                            {
                                Min = rsu.MinDoseQtyPerKg |> Option.map Limit.Inclusive
                                Max = rsu.MaxDoseQtyPerKg |> Option.map Limit.Inclusive
                            }
                        | _ -> DoseLimit.limit.QuantityAdjust
                }
                |> fun dl ->
                    if droplets |> Option.isNone then
                        dl
                    else
                        { dl with
                            DoseUnit =
                                droplets
                                |> Option.map Units.Volume.dropletWithDropsPerMl
                                |> Option.defaultValue rsu.DoseUnit
                            Quantity =
                                {
                                    Min = dl.Quantity.Min |> Option.map (Limit.apply setDroplet setDroplet)
                                    Max = dl.Quantity.Max |> Option.map (Limit.apply setDroplet setDroplet)
                                }

                        }
            )
            |> Array.distinct
            |> Array.tryExactlyOne
            |> function
                | None -> dr
                | Some formLimit ->
                    if formLimit |> DoseLimit.hasNoLimits then
                        dr
                    else
                        { dr with FormLimit = Some formLimit }


    let getDoseLimits (rs: DoseRuleData[]) =
        rs
        |> Array.map (fun r ->
            // the adjust unit
            let adj = r.ScheduleData.AdjustUnit |> Utils.Units.adjustUnit
            // the dose unit
            let du = r.ScheduleData.DoseLimitData.DoseUnit |> UnitsParse.fromString
            // the adjusted dose unit
            let duAdj =
                match adj, du with
                | Some adj, Some du -> du |> ValueUnit.per adj |> Some
                | _ -> None
            // the time unit
            let tu = r.ScheduleData.FreqUnit |> Utils.Units.timeUnit
            // the dose unit per time unit
            let duTime =
                match du, tu with
                | Some du, Some tu -> du |> ValueUnit.per tu |> Some
                | _ -> None
            // the adjusted dose unit per time unit
            let duAdjTime =
                match duAdj, tu with
                | Some duAdj, Some tu -> duAdj |> ValueUnit.per tu |> Some
                | _ -> None
            // the rate unit
            let ru = r.ScheduleData.RateUnit |> UnitsParse.fromString
            // the dose unit per rate unit
            let duRate =
                match du, ru with
                | Some du, Some ru -> du |> ValueUnit.per ru |> Some
                | _ -> None
            // the adjusted dose unit per rate unit
            let duAdjRate =
                match duAdj, ru with
                | Some duAdj, Some ru -> duAdj |> ValueUnit.per ru |> Some
                | _ -> None

            {
                DoseLimitTarget =
                    if r.ScheduleData.DoseLimitData.Substance |> String.isNullOrWhiteSpace then
                        r.ScheduleData.DoseLimitData.Component |> ComponentLimitTarget
                    else
                        r.ScheduleData.DoseLimitData.Substance |> SubstanceLimitTarget
                AdjustUnit = adj
                DoseUnit = du |> Option.defaultValue NoUnit
                Quantity =
                    (r.ScheduleData.DoseLimitData.MinQty, r.ScheduleData.DoseLimitData.MaxQty)
                    |> fromTupleInclIncl du
                QuantityAdjust =
                    (r.ScheduleData.DoseLimitData.MinQtyAdj, r.ScheduleData.DoseLimitData.MaxQtyAdj)
                    |> fromTupleInclIncl duAdj
                PerTime =
                    (r.ScheduleData.DoseLimitData.MinPerTime, r.ScheduleData.DoseLimitData.MaxPerTime)
                    |> fromTupleInclIncl duTime
                PerTimeAdjust =
                    (r.ScheduleData.DoseLimitData.MinPerTimeAdj, r.ScheduleData.DoseLimitData.MaxPerTimeAdj)
                    |> fromTupleInclIncl duAdjTime
                Rate =
                    (r.ScheduleData.DoseLimitData.MinRate, r.ScheduleData.DoseLimitData.MaxRate)
                    |> fromTupleInclIncl duRate
                RateAdjust =
                    (r.ScheduleData.DoseLimitData.MinRateAdj, r.ScheduleData.DoseLimitData.MaxRateAdj)
                    |> fromTupleInclIncl duAdjRate
            }
        )


    let setDoseRuleHashId unitGroup (dr: DoseRule) =
        dr.Generic.Products
        |> List.map (fun id ->
            match id with
            | Gpk s
            | Hpk s -> s
        )
        |> List.append
            [
                unitGroup
                dr.Source |> Source.toString
                dr.Generic.Label |> GenericLabel.toString
                dr.Generic.Form |> PharmaceuticalForm.toString
                dr.Route
                dr.Indication
                dr.PatientCategory |> PatientCategory.toString
                dr.DoseType |> DoseType.toString
            ]
        |> String.sha1Short


    let mapToComponentLimits (prods: ProductComponent[]) dd =
        dd
        |> Array.map (fun r ->
            let cmp = r.ScheduleData.DoseLimitData.Component
            r, prods |> Array.filter (_.Generic >> (String.equalsCapInsens cmp))
        )
        |> Array.groupBy (fst >> _.ScheduleData.DoseLimitData.Component)
        |> Array.map (fun (cmp, rs) ->
            let prods = rs |> Array.collect snd
            let rs = rs |> Array.map fst

            let lim =
                rs
                // if no substance the dose limit is a component limit
                |> Array.filter (_.ScheduleData.DoseLimitData.Substance >> String.isNullOrWhiteSpace)
                |> getDoseLimits
                |> Array.filter (DoseLimit.hasNoLimits >> not)
                |> Array.tryExactlyOne

            {
                Name = cmp
                ProductIds =
                    let hpks =
                        rs
                        |> Array.collect (_.Generic.HPKs >> Array.toList >> ProductId.hpks >> List.toArray)

                    rs
                    |> Array.collect (_.Generic.GPKs >> Array.toList >> ProductId.gpks >> List.toArray)
                    |> Array.append hpks
                Limit = lim
                Products =
                    let dosis = "dosis" |> Units.General.general

                    prods
                    |> Array.filter (fun p ->
                        match lim with
                        | None -> true
                        | Some l ->
                            // special case where dosis is the dose unit
                            l.DoseUnit |> Units.eqsUnit dosis
                            ||
                            // special case where dosis is count unit
                            l.DoseUnit |> ValueUnit.Group.eqsGroup Units.Count.times
                            || l.DoseUnit |> ValueUnit.Group.eqsGroup p.FormUnit
                    )
                    |> Array.distinct
                SubstanceLimits =
                    rs
                    // if a substance the limit is a substance limit
                    |> Array.filter (_.ScheduleData.DoseLimitData.Substance >> String.isNullOrWhiteSpace >> not)
                    |> getDoseLimits
            }
        )


    let mapToDoseRule (grp: GroupedRuleData) =
        let identifySource s =
            match s with
            | _ when s = "FTK" -> s |> Source.identified
            | _ when s = "NKF" -> s |> Source.identified
            | _ -> s |> Source.other

        let toLabel = GenericLabel.toLabel

        let toForm = PharmaceuticalForm.fromString

        let r = grp.DoseRuleData[0]

        if grp.Forms |> Array.isEmpty then
            [| r.Generic.Form |]
        else
            grp.Forms
        // expand the DoseRuleData to all available product forms
        |> Array.collect (fun frm ->
            if grp.Products |> Array.isEmpty then // make a virtual product component
                grp.DoseRuleData
                |> Array.map _.ScheduleData.DoseLimitData.Component
                |> Array.distinct
                |> Array.map (fun cmp ->
                    grp.DoseRuleData
                    |> Array.filter (_.ScheduleData.DoseLimitData.Component >> String.equalsCapInsens cmp)
                    |> Array.map _.ScheduleData.DoseLimitData.Substance
                    |> Array.filter String.notEmpty
                    |> Array.distinct
                    |> Product.create cmp grp.DataGroupKey.Generic.Form grp.DataGroupKey.Route
                )
            else
                grp.Products
            |> Array.filter (_.Form >> String.equalsCapInsens frm)
            |> Array.groupBy (_.FormUnit >> ValueUnit.Group.unitToGroup)
            |> Array.map (fun (unitGroup, prods) ->
                unitGroup |> Group.toString,
                {
                    Id = ""
                    DataId = r.RuleId
                    GroupId = r.GrpId
                    SortNo = r.SortNo
                    Source = r.Source |> identifySource
                    SourceText = r.SourceText
                    Indication = r.Indication
                    Generic =
                        // Label reflects the ORIGINAL source restriction (form/brand/none).
                        let lbl = toLabel r.Generic.Name r.Generic.Form r.Generic.Brand

                        let frm = frm |> toForm

                        let ids =
                            match r.Generic.GPKs, r.Generic.HPKs with
                            | _ when r.Generic.GPKs.Length > 0 -> r.Generic.GPKs |> Array.toList |> ProductId.gpks
                            | _ when r.Generic.HPKs.Length > 0 -> r.Generic.HPKs |> Array.toList |> ProductId.hpks
                            | _ -> []

                        Generic.create lbl frm ids
                    Route = r.Route
                    ScheduleText = r.ScheduleText
                    PatientText = r.PatientText
                    PatientCategory =
                        let p = r.Patient

                        {
                            Location =
                                if p.Location |> String.isNullOrWhiteSpace then
                                    None
                                else
                                    p.Location |> Some

                            Department =
                                if p.Dep |> String.isNullOrWhiteSpace then
                                    None
                                else
                                    p.Dep |> Some
                            Gender = p.Gender
                            Age =
                                if p.IsAdult then
                                    IsAdult
                                else
                                    (p.MinAge, p.MaxAge) |> fromTupleInclExcl (Some Utils.Units.day) |> AbsoluteAge
                            Weight = (p.MinWeight, p.MaxWeight) |> fromTupleInclExcl (Some Utils.Units.weightGram)
                            BSA = (p.MinBSA, p.MaxBSA) |> fromTupleInclExcl (Some Utils.Units.bsaM2)
                            GestAge = (p.MinGestAge, p.MaxGestAge) |> fromTupleInclExcl (Some Utils.Units.day)
                            PMAge = (p.MinPMAge, p.MaxPMAge) |> fromTupleInclExcl (Some Utils.Units.day)
                            Access = AnyAccess
                        }
                    DoseType = r.ScheduleData.DoseText |> DoseType.fromString r.ScheduleData.DoseType
                    AdjustUnit = r.ScheduleData.AdjustUnit |> Units.adjustUnit
                    Frequencies =
                        match r.ScheduleData.FreqUnit |> Units.freqUnit with
                        | None -> None
                        | Some u ->
                            if r.ScheduleData.Freqs |> Array.isEmpty then
                                None
                            else
                                r.ScheduleData.Freqs |> ValueUnit.withUnit u |> Some
                    AdministrationTime =
                        (r.ScheduleData.MinTime, r.ScheduleData.MaxTime)
                        |> fromTupleInclIncl (r.ScheduleData.TimeUnit |> Utils.Units.timeUnit)
                    IntervalTime =
                        (r.ScheduleData.MinInt, r.ScheduleData.MaxInt)
                        |> fromTupleInclIncl (r.ScheduleData.IntUnit |> Utils.Units.timeUnit)
                    Duration =
                        (r.ScheduleData.MinDur, r.ScheduleData.MaxDur)
                        |> fromTupleInclIncl (r.ScheduleData.DurUnit |> Utils.Units.timeUnit)
                    FormLimit = None
                    ComponentLimits = grp.DoseRuleData |> mapToComponentLimits prods
                    RenalRuleSource = None
                    Validated = r.Validated
                    Check =
                        {
                            FreqCheck = r.FreqCheck
                            DoseCheck = r.DoseCheck
                        }
                }
            )
        )
        |> Array.map (fun (unitGroup, dr) -> { dr with Id = dr |> setDoseRuleHashId unitGroup })


    /// <summary>
    /// Drop empty (meaningless) limits and dose rules from the forward output.
    /// </summary>
    /// <remarks>
    /// The forward map keeps a row as long as it passes <c>validateData</c> (dose
    /// type / schedule completeness), which does NOT check for the presence of any
    /// dose limit. As a result a row with no dose values yields a dose rule whose
    /// limits are all empty — no dosing meaning. This pass removes them:
    /// <para>- substance limits carrying no values (mirrors the component branch in
    /// <c>mapToComponentLimits</c>, which already filters with <c>hasNoLimits</c>);</para>
    /// <para>- component limits left with no component limit AND no substance limits;</para>
    /// <para>- dose rules left with no component limits.</para>
    /// </remarks>
    let removeEmptyLimits (drs: DoseRule[]) =
        drs
        |> Array.map (fun dr ->
            { dr with
                ComponentLimits =
                    dr.ComponentLimits
                    |> Array.choose (fun cl ->
                        let substLims = cl.SubstanceLimits |> Array.filter (DoseLimit.hasNoLimits >> not)
                        let cmpLim = cl.Limit |> Option.filter (DoseLimit.hasNoLimits >> not)

                        if substLims |> Array.isEmpty && cmpLim |> Option.isNone then
                            None
                        else
                            Some
                                { cl with
                                    Limit = cmpLim
                                    SubstanceLimits = substLims
                                }
                    )
            }
        )
        |> Array.filter (_.ComponentLimits >> Array.isEmpty >> not)


    // -----------------------------------------------------------------------
    // Reverse mapping  DoseRule -> DoseRuleData[]
    //
    // Inverse of `mapToDoseRule`: pure and resource-free. It drops the form /
    // form-unit-group expansion and the resource-derived FormLimit (both are
    // re-derived by the forward path), but recovers form/brand/gpks/hpks
    // narrowing from the Generic label and products. Reversing every rule
    // yields duplicates; a caller-chosen `Array.distinct` collapses them back
    // to (roughly) the original DoseRuleData, modulo generated Id/GrpId/SortNo.
    // -----------------------------------------------------------------------


    /// Bare, group-less unit string; idempotent through UnitsParse.fromString.
    let unitStr (u: Unit) = u |> Units.toStringEngShortWithoutGroup


    /// "kg" / "m2" / "" from an adjust Unit option (inverse of Utils.Units.adjustUnit).
    let adjStr (u: Unit option) =
        match u with
        | Some uu when uu |> Units.eqsUnit Units.Weight.kiloGram -> "kg"
        | Some uu when uu |> Units.eqsUnit Units.BSA.m2 -> "m2"
        | _ -> ""


    /// Time-token of a frequency ValueUnit (the denominator of times/<time>).
    let freqUnitStr (vuOpt: ValueUnit option) =
        match vuOpt with
        | Some vu ->
            match vu |> ValueUnit.getUnit with
            | CombiUnit(_, OpPer, t) -> t |> unitStr
            | _ -> ""
        | None -> ""


    /// Denominator time-token of a per-rate / per-time unit (du/<x>).
    let perUnitStr (mm: MinMax) =
        match mm |> MinMax.getUnit with
        | Some(CombiUnit(_, OpPer, d)) -> d |> unitStr
        | _ -> ""


    /// Time-token of a plain time MinMax (administration/interval/duration).
    let timeUnitStr (mm: MinMax) =
        match mm |> MinMax.getUnit with
        | Some u -> u |> unitStr
        | None -> ""


    let emptyDoseLimitData: DoseLimitData =
        {
            CmpBased = false
            Component = ""
            Substance = ""
            DoseUnit = ""
            MinQty = None
            MaxQty = None
            MinQtyAdj = None
            MaxQtyAdj = None
            MinPerTime = None
            MaxPerTime = None
            MinPerTimeAdj = None
            MaxPerTimeAdj = None
            MinRate = None
            MaxRate = None
            MinRateAdj = None
            MaxRateAdj = None
        }


    /// Inverse of getDoseLimits for one DoseLimit, parented by component name.
    /// cmpBased = true marks a component-only limit (no substance limits), per the
    /// source convention CmpBased &lt;=&gt; blank Substance.
    let reverseDoseLimit (cmpBased: bool) (cmpName: string) (dl: DoseLimit) : DoseLimitData =
        let substance =
            match dl.DoseLimitTarget with
            | SubstanceLimitTarget s -> s
            | _ -> ""

        let mmTuple = MinMax.toValueTuple

        let q = mmTuple dl.Quantity
        let qa = mmTuple dl.QuantityAdjust
        let pt = mmTuple dl.PerTime
        let pta = mmTuple dl.PerTimeAdjust
        let rt = mmTuple dl.Rate
        let rta = mmTuple dl.RateAdjust

        { emptyDoseLimitData with
            CmpBased = cmpBased
            Component = cmpName
            Substance = substance
            DoseUnit = dl.DoseUnit |> unitStr
            MinQty = fst q
            MaxQty = snd q
            MinQtyAdj = fst qa
            MaxQtyAdj = snd qa
            MinPerTime = fst pt
            MaxPerTime = snd pt
            MinPerTimeAdj = fst pta
            MaxPerTimeAdj = snd pta
            MinRate = fst rt
            MaxRate = snd rt
            MinRateAdj = fst rta
            MaxRateAdj = snd rta
        }


    /// Inverse of the Generic part of mapToDoseRule. Narrowing is read from
    /// (Label, Products); the expansion form (Generic.Form) is ignored.
    let reverseGeneric (g: Generic) : GenericData =
        let name = g.Label |> GenericLabel.genericName

        let form =
            match g.Label with
            | GenericForm(_, f) -> f
            | _ -> ""

        let brand =
            match g.Label with
            | GenericBrand(_, b) -> b
            | _ -> ""

        let gpks =
            g.Products
            |> List.choose (
                function
                | Gpk s -> Some s
                | _ -> None
            )
            |> List.toArray

        let hpks =
            g.Products
            |> List.choose (
                function
                | Hpk s -> Some s
                | _ -> None
            )
            |> List.toArray

        {
            Name = name
            Form = form
            Brand = brand
            GPKs = gpks
            HPKs = hpks
        }


    /// Inverse of the PatientCategory part of mapToDoseRule.
    let reversePatient (pc: PatientCategory) : PatientCategoryData =
        let isAdult = (pc.Age = IsAdult)

        let mmTuple = MinMax.toValueTuple

        let minAge, maxAge =
            match pc.Age with
            | IsAdult -> None, None
            | AbsoluteAge mm -> mmTuple mm

        let minW, maxW = mmTuple pc.Weight
        let minB, maxB = mmTuple pc.BSA
        let minG, maxG = mmTuple pc.GestAge
        let minP, maxP = mmTuple pc.PMAge

        {
            Location = pc.Location |> Option.defaultValue ""
            Dep = pc.Department |> Option.defaultValue ""
            IsAdult = isAdult
            Gender = pc.Gender
            MinAge = minAge
            MaxAge = maxAge
            MinWeight = minW
            MaxWeight = maxW
            MinBSA = minB
            MaxBSA = maxB
            MinGestAge = minG
            MaxGestAge = maxG
            MinPMAge = minP
            MaxPMAge = maxP
        }


    /// Inverse of the ScheduleData part of mapToDoseRule.
    let reverseSchedule (dr: DoseRule) (dl: DoseLimit) (dld: DoseLimitData) : ScheduleData =
        let doseType = dr.DoseType |> DoseType.toCategory
        let doseText = dr.DoseType |> DoseType.getText

        let mmTuple = MinMax.toValueTuple

        let freqs =
            dr.Frequencies |> Option.map ValueUnit.getValue |> Option.defaultValue [||]

        {
            DoseType = doseType
            DoseText = doseText
            Freqs = freqs
            AdjustUnit = dr.AdjustUnit |> adjStr
            FreqUnit = freqUnitStr dr.Frequencies
            RateUnit =
                match perUnitStr dl.Rate with
                | "" -> perUnitStr dl.RateAdjust
                | s -> s
            MinTime = fst (mmTuple dr.AdministrationTime)
            MaxTime = snd (mmTuple dr.AdministrationTime)
            TimeUnit = timeUnitStr dr.AdministrationTime
            MinInt = fst (mmTuple dr.IntervalTime)
            MaxInt = snd (mmTuple dr.IntervalTime)
            IntUnit = timeUnitStr dr.IntervalTime
            MinDur = fst (mmTuple dr.Duration)
            MaxDur = snd (mmTuple dr.Duration)
            DurUnit = timeUnitStr dr.Duration
            DoseLimitData = dld
        }


    /// Explode a DoseRule into its source data rows (inverse of mapToDoseRule).
    /// FormLimit is NOT emitted (it is derived from the external formRoutes table).
    /// A component limit with no substance limits emits one CmpBased row; a
    /// component with substance limits emits one row per substance.
    let toData (dr: DoseRule) : DoseRuleData[] =
        let gen = reverseGeneric dr.Generic
        let pat = reversePatient dr.PatientCategory

        let mkRow (cmpBased: bool) (cmpName: string) (dl: DoseLimit) : DoseRuleData =
            let dld = reverseDoseLimit cmpBased cmpName dl

            {
                RowId = ""
                RuleId = ""
                GrpId = ""
                SortNo = 0
                Source = dr.Source |> Source.toString
                SourceText = dr.SourceText
                Generic = gen
                Indication = dr.Indication
                Route = dr.Route
                PatientText = dr.PatientText
                Patient = pat
                ScheduleText = dr.ScheduleText
                ScheduleData = reverseSchedule dr dl dld
                Validated = dr.Validated
                FreqCheck = dr.Check.FreqCheck
                DoseCheck = dr.Check.DoseCheck
            }

        dr.ComponentLimits
        |> Array.collect (fun cl ->
            // no substance limits -> use the component limit (CmpBased = true)
            // has substance limits -> use only the substance limits (CmpBased = false)
            if cl.SubstanceLimits |> Array.isEmpty then
                match cl.Limit with
                | Some l -> [| mkRow true cl.Name l |]
                | None -> [||]
            else
                cl.SubstanceLimits |> Array.map (mkRow false cl.Name)
        )


    /// <summary>
    /// Filter the DoseRules according to the Filter.
    /// </summary>
    /// <param name="routeMapping"></param>
    /// <param name="filter">The Filter</param>
    /// <param name="drs">The DoseRule array</param>
    let filter routeMapping (filter: DoseFilter) (drs: DoseRule array) =
        // if the filter is 'empty' just return all
        if filter = Filter.doseFilter then
            drs
        else
            let eqs a b =
                a |> Option.map (String.equalsCapInsens b) |> Option.defaultValue true

            [|
                fun (dr: DoseRule) -> dr.Indication |> eqs filter.Indication
                fun (dr: DoseRule) -> dr.Generic.Label |> GenericLabel.toString |> eqs filter.Generic
                fun (dr: DoseRule) -> dr.Generic.Form |> PharmaceuticalForm.toString |> eqs filter.Form
                fun (dr: DoseRule) ->
                    filter.Route |> Option.isNone
                    || dr.Route |> Mapping.eqsRoute routeMapping filter.Route
                // don't filter on patients if patient is not set
                if filter.Patient = Patient.patient |> not then
                    fun (dr: DoseRule) -> dr.PatientCategory |> PatientCategory.filter filter
                fun (dr: DoseRule) -> filter.DoseType |> Option.map ((=) dr.DoseType) |> Option.defaultValue true
            |]
            |> Array.fold (fun (acc: DoseRule[]) pred -> acc |> Array.filter pred) drs


    let private getMember getter (drs: DoseRule[]) =
        drs
        |> Array.map getter
        |> Array.map String.trim
        |> Array.distinctBy String.toLower
        |> Array.sortBy String.toLower


    /// Extract all indications from the DoseRules.
    let indications = getMember _.Indication


    /// Extract all the generics from the DoseRules.
    let generics = getMember (_.Generic.Label >> GenericLabel.toString)


    /// Extract all the pharmaceutical forms from the DoseRules.
    let forms = getMember (_.Generic.Form >> PharmaceuticalForm.toString)


    /// Extract all the routes from the DoseRules.
    let routes = getMember _.Route


    let doseTypes (dsrs: DoseRule[]) =
        dsrs |> Array.map _.DoseType |> Array.distinct


    /// Extract all the departments from the DoseRules.
    let departments = getMember (_.PatientCategory.Department >> Option.defaultValue "")


    /// Extract all genders from the DoseRules.
    let genders = getMember (_.PatientCategory.Gender >> Gender.toString)


    /// Extract all patient categories from the DoseRules as strings.
    let patientCategories (drs: DoseRule array) =
        drs
        |> Array.map _.PatientCategory
        |> Array.sortBy PatientCategory.sortBy
        |> Array.map PatientCategory.toString
        |> Array.distinct


    /// Extract all frequencies from the DoseRules as strings.
    let frequencies (drs: DoseRule array) =
        drs |> Array.map Print.printFreqs |> Array.distinct


    let useAdjust (dr: DoseRule) =
        let substUseAdj =
            dr.ComponentLimits
            |> Array.collect _.SubstanceLimits
            |> Array.exists DoseLimit.useAdjust

        let compUseAdj =
            dr.ComponentLimits |> Array.choose _.Limit |> Array.exists DoseLimit.useAdjust

        let formUseAdj = false
        // TODO figure out whether or not to use this
        //dr.FormLimit |> Option.map DoseLimit.useAdjust |> Option.defaultValue false

        substUseAdj || compUseAdj || formUseAdj


    let rec getNormDose (dr: DoseRule) =
        dr.ComponentLimits
        |> Array.collect _.SubstanceLimits
        |> Array.collect (fun dl ->
            [|
                match dl.PerTimeAdjust |> DoseLimit.getNormDose with
                | Some norm -> (dl.DoseLimitTarget, norm) |> NormPerTimeAdjust |> Some
                | _ -> None

                match dl.QuantityAdjust |> DoseLimit.getNormDose with
                | Some norm -> (dl.DoseLimitTarget, norm) |> NormQuantityAdjust |> Some
                | _ -> None
            |]
        )
        |> Array.choose id
        |> Array.tryHead
