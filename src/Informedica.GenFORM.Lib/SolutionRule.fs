namespace Informedica.GenForm.Lib


module SolutionRule =

    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib
    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges

    open Utils


    let fromTupleInclExcl = MinMax.fromTuple Inclusive Exclusive


    let fromTupleInclIncl = MinMax.fromTuple Inclusive Inclusive


    let parseSolutionRuleData (data: string[][]) : Result<_, Message list> =
        try
            data
            |> fun data ->
                let getColumn = data |> Array.head |> Csv.getStringColumn

                data
                |> Array.tail
                |> Array.map (fun r ->
                    let get = getColumn r >> String.trim

                    let getOpt =
                        getColumn r
                        >> String.trim
                        >> fun s -> if s |> String.isNullOrWhiteSpace then None else Some s

                    let toBrOpt = BigRational.toBrs >> Array.tryHead

                    {
                        // solution rule section
                        Generic = get "Generic"
                        Form = get "Form"
                        Route = get "Route"
                        Indication = get "Indication"
                        Location = getOpt "Loc"
                        Department = getOpt "Dep"
                        CVL = get "CVL"
                        PVL = get "PVL"
                        MinAge = get "MinAge" |> toBrOpt
                        MaxAge = get "MaxAge" |> toBrOpt
                        MinWeight = get "MinWeight" |> toBrOpt
                        MaxWeight = get "MaxWeight" |> toBrOpt
                        MinGestAge = get "MinGestAge" |> toBrOpt
                        MaxGestAge = get "MaxGestAge" |> toBrOpt
                        MinDose = get "MinDose" |> toBrOpt
                        MaxDose = get "MaxDose" |> toBrOpt
                        DoseType = get "DoseType"
                        DoseText = get "DoseText"
                        Solutions = get "Solutions" |> String.split "|" |> List.map String.trim
                        Volumes = get "Volumes" |> BigRational.toBrs
                        Div = get "Div" |> toBrOpt
                        MinVol = get "MinVol" |> toBrOpt
                        MaxVol = get "MaxVol" |> toBrOpt
                        MinVolAdj = get "MinVolAdj" |> toBrOpt
                        MaxVolAdj = get "MaxVolAdj" |> toBrOpt
                        MinPerc = get "MinPerc" |> toBrOpt
                        MaxPerc = get "MaxPerc" |> toBrOpt
                        // solution limit section
                        Component = get "Component"
                        Substance = get "Substance"
                        Unit = get "Unit"
                        Quantities = get "Quantities" |> BigRational.toBrs
                        MinQty = get "MinQty" |> toBrOpt
                        MaxQty = get "MaxQty" |> toBrOpt
                        MinQtyAdj = get "MinQtyAdj" |> toBrOpt
                        MaxQtyAdj = get "MaxQtyAdj" |> toBrOpt
                        MinDrip = get "MinDrip" |> toBrOpt
                        MaxDrip = get "MaxDrip" |> toBrOpt
                        MinConc = get "MinConc" |> toBrOpt
                        MaxConc = get "MaxConc" |> toBrOpt
                    }
                )
            |> Ok
        with exn ->
            Result.createError "Error in SolutionRule.getResult: " exn


    let getData dataUrlId =
        Web.getDataFromSheet dataUrlId "SolutionRules" |> parseSolutionRuleData


    let map routeMapping (parenteral: ProductComponent[]) products data : Result<_, Message list> =
        data
        |> Array.groupBy (fun r ->
            let du = r.Unit |> UnitsParse.fromString

            {
                Generic = r.Generic
                Form =
                    if r.Form |> String.isNullOrWhiteSpace then
                        None
                    else
                        r.Form |> Some
                Route = r.Route
                Indication =
                    if r.Indication |> String.isNullOrWhiteSpace then
                        None
                    else
                        r.Indication |> Some
                PatientCategory =
                    { PatientCategory.empty with
                        Location = r.Location
                        Department = r.Department
                        Access =
                            if r.CVL = "x" then CVL
                            else if r.PVL = "x" then PVL
                            else AnyAccess
                        Age = (r.MinAge, r.MaxAge) |> fromTupleInclExcl (Some Units.day) |> AbsoluteAge
                        Weight = (r.MinWeight, r.MaxWeight) |> fromTupleInclExcl (Some Units.weightGram)
                        GestAge = (r.MinGestAge, r.MaxGestAge) |> fromTupleInclExcl (Some Units.day)
                    }
                Dose = (r.MinDose, r.MaxDose) |> fromTupleInclIncl du
                DoseType = DoseType.fromString r.DoseType r.DoseText
                Diluents =
                    parenteral
                    |> Array.filter (fun p ->
                        r.Solutions |> List.exists (fun s -> p.Generic |> String.equalsCapInsens s)
                    )
                    |> Array.distinctBy _.Generic
                Div = r.Div
                Volumes =
                    if r.Volumes |> Array.isEmpty then
                        None
                    else
                        r.Volumes |> ValueUnit.withUnit Units.mL |> Some
                Volume = (r.MinVol, r.MaxVol) |> fromTupleInclIncl (Some Units.mL)
                VolumeAdjust =
                    (r.MinVolAdj, r.MaxVolAdj)
                    |> fromTupleInclIncl (Units.mL |> ValueUnit.per Units.Weight.kiloGram |> Some)
                DripRate =
                    (r.MinDrip, r.MaxDrip)
                    |> fromTupleInclIncl (Some(Units.Volume.milliLiter |> ValueUnit.per Units.Time.hour))
                DoseFraction = (r.MinPerc, r.MaxPerc) |> fromTupleInclIncl (Some Units.Count.times)
                SolutionLimits = [||]
            }
        )
        |> Array.map (fun (sr, rs) ->
            { sr with
                SolutionLimits =
                    rs
                    |> Array.map (fun l ->
                        let u = l.Unit |> UnitsParse.fromString
                        let au = u |> Option.map (ValueUnit.per Units.Weight.kiloGram)

                        {
                            SolutionLimitTarget =
                                match l.Substance, l.Component with
                                | s, _ when s |> String.notEmpty -> s |> SubstanceLimitTarget
                                | _, c when c |> String.notEmpty -> c |> ComponentLimitTarget
                                | _ -> invalidOp "Solution limit should be either a substance or a component limit"
                            Quantity = (l.MinQty, l.MaxQty) |> fromTupleInclIncl u
                            QuantityAdj = (l.MinQtyAdj, l.MaxQtyAdj) |> fromTupleInclIncl au
                            Quantities =
                                if l.Quantities |> Array.isEmpty then
                                    None
                                else
                                    match u with
                                    | None -> None
                                    | Some u -> l.Quantities |> ValueUnit.withUnit u |> Some
                            Concentration =
                                let u = u |> Option.map (ValueUnit.per Units.Volume.milliLiter)
                                (l.MinConc, l.MaxConc) |> fromTupleInclIncl u
                            Products =
                                products
                                |> Array.filter (fun p ->
                                    p.Generic = l.Component
                                    && sr.Form
                                       |> Option.map (fun s -> s |> String.equalsCapInsens p.Form)
                                       |> Option.defaultValue true
                                    && p.Routes |> Array.exists (Mapping.eqsRoute routeMapping (Some sr.Route))
                                )
                        }
                    )
                    // filter out solution limits that are empty
                    |> Array.filter (fun sl ->
                        (sl.Concentration |> MinMax.isEmpty
                         && sl.Quantities |> Option.isNone
                         && sl.QuantityAdj |> MinMax.isEmpty
                         && sl.Quantity |> MinMax.isEmpty)
                        |> not
                    )

            }
        )
        |> Ok


    /// The rule as an admin finds it in the sheet: generic, form, route, indication,
    /// patient, which includes the department, and dose type.
    let label (sr: SolutionRule) =
        [
            sr.Generic
            sr.Form |> Option.defaultValue ""
            sr.Route
            sr.Indication |> Option.defaultValue ""
            sr.PatientCategory |> PatientCategory.toString
            sr.DoseType |> DoseType.toString
        ]
        |> List.filter String.notEmpty
        |> String.concat ", "
        |> sprintf "solution rule %s"


    /// Every range a solution rule carries, each with its name.
    let ranges (sr: SolutionRule) =
        [
            yield! sr.PatientCategory |> PatientCategory.ranges
            "dose", sr.Dose
            "volume", sr.Volume
            "volume adjust", sr.VolumeAdjust
            "drip rate", sr.DripRate
            "dose fraction", sr.DoseFraction
            for sl in sr.SolutionLimits do
                let target = sl.SolutionLimitTarget |> LimitTarget.toString
                $"%s{target} quantity", sl.Quantity
                $"%s{target} quantity adjust", sl.QuantityAdj
                $"%s{target} concentration", sl.Concentration
        ]


    /// One warning per inverted range in the solution rules.
    let invertedRanges (rules: SolutionRule[]) =
        rules
        |> Array.toList
        |> List.collect (fun sr ->
            sr
            |> ranges
            |> MinMax.invertedRanges
            |> List.map (fun s -> $"%s{sr |> label}: minimum above maximum, %s{s}")
        )
        |> List.distinct
        |> List.map Warning


    /// A limit, or nothing when the limit is 0, which counts as empty.
    let nonZeroLimit (lim: Limit option) =
        lim |> Option.filter (Limit.getValueUnit >> ValueUnit.isZero >> not)


    /// Whether a solution limit bounds the preparation: the maximum quantity of a substance, or
    /// the maximum quantity per kg once the weight is known, divided by the minimum concentration
    /// of that substance is a maximum volume.
    let boundsPreparation (sl: SolutionLimit) =
        (sl.Quantity.Max.IsSome || sl.QuantityAdj.Max.IsSome)
        && sl.Concentration.Min |> Option.exists (Limit.getValueUnit >> ValueUnit.gtZero)


    /// One warning per solution rule whose dose count has no maximum, because the rule gives a
    /// maximum dose fraction and no minimum, while the preparation has no maximum either: no
    /// maximum volume, no list of volumes, no maximum volume per kg, which bounds the preparation
    /// once the weight is known, and no solution limit that bounds it. The user then cannot set
    /// the preparation.
    let openDoseCounts (rules: SolutionRule[]) =
        rules
        |> Array.filter (fun sr ->
            sr.DoseFraction.Max |> nonZeroLimit |> Option.isSome
            && sr.DoseFraction.Min |> nonZeroLimit |> Option.isNone
            && sr.Volume.Max |> Option.isNone
            && sr.Volumes |> Option.isNone
            && sr.VolumeAdjust.Max |> Option.isNone
            && sr.SolutionLimits |> Array.exists boundsPreparation |> not
        )
        |> Array.map (fun sr ->
            $"%s{sr |> label}: a maximum dose fraction without a minimum and no maximum volume, "
            + "so the preparation cannot be set"
        )
        |> Array.toList
        |> List.distinct
        |> List.map Warning


    /// One warning per solution rule whose rows name solutions none of which matches a
    /// parenteral product, so the rule loads with no diluents. Each row is built into a rule
    /// on its own and the rules are grouped without their limits, which is the grouping map
    /// makes, so a rule of several rows gives a single warning.
    let missingDiluents parenteral (data: SolutionRuleData[]) =
        data
        |> Array.filter (fun r -> r.Solutions |> List.exists String.notEmpty)
        |> Array.choose (fun r ->
            match [| r |] |> map [||] parenteral [||] with
            | Ok [| sr |] when sr.Diluents |> Array.isEmpty -> Some({ sr with SolutionLimits = [||] }, r.Solutions)
            | _ -> None
        )
        |> Array.groupBy fst
        |> Array.map (fun (sr, xs) ->
            let solutions =
                xs
                |> Array.collect (snd >> List.toArray)
                |> Array.filter String.notEmpty
                |> Array.distinct
                |> String.concat "|"

            Warning $"%s{sr |> label}: no parenteral product matches the solutions %s{solutions}"
        )
        |> Array.toList


    /// Whether a solution rule applies to a dose rule: the generic and the route, and the form,
    /// indication and dose type when the solution rule names them, matched as filter matches them
    /// for an order. A solution rule and a dose rule that name different departments or different
    /// locations apply to different patients, so they do not match.
    let appliesToDoseRule routeMapping (sr: SolutionRule) (dr: DoseRule) =
        sr.Generic |> String.equalsCapInsens (dr.Generic |> Generic.genericName)
        && PatientCategory.canMeet sr.PatientCategory.Department dr.PatientCategory.Department
        && PatientCategory.canMeet sr.PatientCategory.Location dr.PatientCategory.Location
        && sr.Form
           |> Option.forall (String.equalsCapInsens (dr.Generic.Form |> PharmaceuticalForm.toString))
        && sr.Indication |> Option.forall (String.equalsCapInsens dr.Indication)
        && sr.Route |> Mapping.eqsRoute routeMapping (Some dr.Route)
        && (sr.DoseType = NoDoseType
            || if sr.DoseType |> DoseType.getText |> String.isNullOrWhiteSpace then
                   DoseType.eqsType sr.DoseType dr.DoseType
               else
                   DoseType.eqs sr.DoseType dr.DoseType)


    /// One warning per pair of solution rules that apply to the same dose rule and can meet the
    /// same patient of that dose rule. Ranges that overlap pair by pair share a value, so comparing
    /// each pair of the three rules is enough. Each rule gives its own scenario, so the user has to choose between them, and
    /// a view that shows a single order shows none. The dose range of a solution rule does not
    /// take part in the matching, so rules that differ only in dose range also compete.
    let competingRules routeMapping (doseRules: DoseRule[]) (rules: SolutionRule[]) =
        let reaches (dr: DoseRule) (sr: SolutionRule) =
            dr |> appliesToDoseRule routeMapping sr
            && PatientCategory.canShare sr.PatientCategory dr.PatientCategory

        let appliesToBoth a b dr = a |> reaches dr && b |> reaches dr

        let pairs (rules: SolutionRule[]) =
            rules
            |> Array.indexed
            |> Array.collect (fun (i, a) -> rules[i + 1 ..] |> Array.map (fun b -> a, b))

        // a dose rule has one generic, and the matching ignores the case of its name
        rules
        |> Array.groupBy (_.Generic >> String.toLower)
        |> Array.collect (snd >> pairs)
        |> Array.filter (fun (a, b) ->
            PatientCategory.canShare a.PatientCategory b.PatientCategory
            && doseRules |> Array.exists (appliesToBoth a b)
        )
        |> Array.map (fun (a, b) ->
            $"%s{a |> label} and %s{b |> label}: both apply to one dose rule and one patient, "
            + "so each gives its own scenario"
        )
        |> Array.toList
        |> List.distinct
        |> List.map Warning


    /// The substances whose concentration limit allows no strength of the products. A strength
    /// in a unit group the limit cannot be compared with is skipped. The limit is inclusive, and
    /// a limit with a minimum equal to the maximum is widened to 90% and 110%, as an order does
    /// until #1579 decides whether a concentration limit is widened.
    let substancesWithoutAllowedStrength (products: ProductComponent[]) (sr: SolutionRule) =
        sr.SolutionLimits
        |> Array.choose (fun sl ->
            match sl.SolutionLimitTarget with
            | SubstanceLimitTarget s when sl.Concentration |> MinMax.isEmpty |> not ->
                let conc = sl.Concentration

                let comparable vu =
                    [ conc.Min; conc.Max ]
                    |> List.choose id
                    |> List.forall (Limit.getValueUnit >> ValueUnit.eqsGroup vu)

                let isNorm =
                    match conc.Min, conc.Max with
                    | Some min, Some max -> min |> Limit.eq max
                    | _ -> false

                let bound factor =
                    Option.map (fun lim ->
                        let vu = lim |> Limit.getValueUnit

                        if isNorm then
                            vu * (factor |> ValueUnit.singleWithUnit Units.Count.times)
                        else
                            vu
                        |> Limit.inclusive
                    )

                let allowed =
                    {
                        Min = conc.Min |> bound (9N / 10N)
                        Max = conc.Max |> bound (11N / 10N)
                    }

                let strengths =
                    products
                    |> Array.collect _.Substances
                    |> Array.filter (fun sub -> sub.Name |> String.equalsCapInsens s)
                    |> Array.choose _.Concentration
                    |> Array.filter comparable

                if
                    strengths |> Array.isEmpty |> not
                    && strengths |> Array.forall (Some >> MinMax.inRange allowed >> not)
                then
                    Some s
                else
                    None
            | _ -> None
        )


    /// A warning for each solution rule without diluents whose concentration limit allows no
    /// strength of the products of a dose rule it applies to. Only a dose rule with one component
    /// is checked, because a second component can dilute the first. The products are
    /// reconstituted for the location and the department of the solution rule. A solution rule
    /// and a dose rule without a department apply to every department, so the products are then
    /// reconstituted for each department their reconstitution rules name, and for none.
    let undilutableRules routeMapping (doseRules: DoseRule[]) (rules: SolutionRule[]) =
        let substances (sr: SolutionRule) =
            doseRules
            |> Array.filter (fun dr ->
                dr.ComponentLimits |> Array.length = 1
                && dr |> appliesToDoseRule routeMapping sr
            )
            |> Array.collect (fun dr ->
                let departments =
                    match sr.PatientCategory.Department, dr.PatientCategory.Department with
                    | Some dep, _
                    | None, Some dep -> [| Some dep |]
                    | None, None ->
                        dr.ComponentLimits
                        |> Array.collect _.Products
                        |> Array.collect _.Reconstitution
                        |> Array.map _.Department
                        |> Array.append [| None |]
                        |> Array.distinct

                departments
                |> Array.collect (fun dep ->
                    let dr, _ = dr |> DoseRule.reconstitute routeMapping sr.PatientCategory.Location dep

                    sr
                    |> substancesWithoutAllowedStrength (dr.ComponentLimits |> Array.collect _.Products)
                )
            )
            |> Array.distinct

        rules
        |> Array.filter (fun sr -> sr.Diluents |> Array.isEmpty)
        |> Array.choose (fun sr ->
            match sr |> substances with
            | [||] -> None
            | subs ->
                let names = subs |> String.concat ", "

                Warning
                    $"%s{sr |> label}: no diluent, and the concentration limit of %s{names} allows no strength of its products"
                |> Some
        )
        |> Array.distinct
        |> Array.toList


    let get dataUrlId routeMapping (parenteral: ProductComponent[]) products : Result<_, Message list> =
        try
            dataUrlId |> getData |> Result.bind (map routeMapping parenteral products)
        with exn ->
            Result.createError "Error in SolutionRule.getResult: " exn


    /// <summary>
    /// Get all the SolutionRules that match the given Filter.
    /// </summary>
    /// <param name="mapping"></param>
    /// <param name="filter">The Filter</param>
    /// <param name="solutionRules">The SolutionRules</param>
    /// <returns>The matching SolutionRules</returns>
    let filter mapping (filter: SolutionFilter) (solutionRules: SolutionRule[]) =
        let eqs a (b: string) =
            a |> Option.map (String.equalsCapInsens b) |> Option.defaultValue true

        [|
            fun (sr: SolutionRule) -> sr.Generic |> String.equalsCapInsens filter.Generic
            fun (sr: SolutionRule) -> sr.PatientCategory |> PatientCategory.filterPatient filter.Patient
            fun (sr: SolutionRule) -> sr.Form |> Option.map (eqs filter.Form) |> Option.defaultValue true
            fun (sr: SolutionRule) -> sr.Indication |> Option.map (eqs filter.Indication) |> Option.defaultValue true
            fun (sr: SolutionRule) ->
                filter.Route |> Option.isNone
                || sr.Route |> Mapping.eqsRoute mapping filter.Route
            fun (sr: SolutionRule) ->
                sr.DoseType = NoDoseType
                || filter.DoseType
                   |> Option.map (
                       if sr.DoseType |> DoseType.getText |> String.isNullOrWhiteSpace then
                           DoseType.eqsType sr.DoseType
                       else
                           DoseType.eqs sr.DoseType
                   )
                   |> Option.defaultValue true
        |]
        |> Array.fold (fun (acc: SolutionRule[]) pred -> acc |> Array.filter pred) solutionRules
        |> Array.map (fun sr ->
            { sr with
                Diluents =
                    sr.Diluents
                    |> Array.filter (fun dil ->
                        filter.Diluent |> Option.map ((=) dil.Generic) |> Option.defaultValue true
                    )
            }
        )


    /// Helper function to get the distinct values of a member of SolutionRule.
    let private getMember getter (rules: SolutionRule[]) = rules |> Array.map getter |> Array.distinct |> Array.sort


    /// Get all the distinct Generics from the given SolutionRules.
    let generics = getMember _.Generic

    let forms = getMember _.Form >> Array.choose id


    let routes = getMember _.Route


    module Print =


        module Limit = Limit


        let private vuToStr vu =
            let milliGram = Units.Mass.milliGram

            let gram = Units.Mass.gram
            let day = Units.Time.day

            let per = ValueUnit.per
            let convertTo = ValueUnit.convertTo

            let milliGramPerDay = milliGram |> per day
            let gramPerDay = gram |> per day

            vu
            |> (fun vu ->
                match vu |> ValueUnit.get with
                | v, u when v >= [| 1000N |] && u = milliGram -> vu |> convertTo gram
                | v, u when v >= [| 1000N |] && u = milliGramPerDay -> vu |> convertTo gramPerDay
                | _ -> vu
            )
            |> ValueUnit.toStringDecimalDutchShortWithPrec 2


        /// Format the rule-wide "geef X% van de bereiding" line from the
        /// DoseFraction bounds on the rule. Returns an empty string when no bounds
        /// are set.
        let printDoseFraction (sr: SolutionRule) =
            let toPerc l =
                l
                |> Limit.getValueUnit
                |> ValueUnit.getValue
                |> Array.item 0
                |> BigRational.toDouble
                |> fun x -> $"{x * 100.}"

            let p =
                match sr.DoseFraction.Min, sr.DoseFraction.Max with
                | None, None -> ""
                | Some l, None -> $"min. {l |> toPerc}"
                | None, Some l -> $"max. {l |> toPerc}"
                | Some min, Some max ->
                    if min = max then
                        $"{min |> toPerc}"
                    else
                        $"{min |> toPerc} - {max |> toPerc}"

            if p |> String.isNullOrWhiteSpace then
                ""
            else
                $"* geef %s{p}%% van de bereiding"


        /// Split a SolutionLimit into its display parts so callers can de-duplicate
        /// trailing lines that are identical across multiple limits of the same rule.
        /// includeSubstanceName = false suppresses the inline substance prefix
        /// (used when the substance is rendered as a separate heading).
        let printSolutionLimit (sr: SolutionRule) (includeSubstanceName: bool) (limit: SolutionLimit) =
            let mmToStr = MinMax.toString "min. " "min. " "max. " "max. "

            let loc =
                match sr.PatientCategory.Access with
                | CVL -> "###### centraal: \n* "
                | PVL -> "###### perifeer: \n* "
                | EnteralTube -> "###### sonde: \n* "
                | AnyAccess -> "* "

            let qs =
                limit.Quantities
                |> Option.map (Utils.ValueUnit.toString -1)
                |> Option.defaultValue ""

            let q = limit.Quantity |> mmToStr

            let vol =
                if sr.Volume |> mmToStr |> String.isNullOrWhiteSpace then
                    ""
                else
                    sr.Volume
                    |> mmToStr
                    |> fun s -> $""" in {s} {sr.Diluents |> Array.map _.Generic |> String.concat "/"}"""
                |> fun s ->
                    if s |> String.notEmpty then
                        s
                    else
                        sr.Volumes
                        |> Option.map (Utils.ValueUnit.toString -1)
                        |> Option.defaultValue ""
                        |> fun s ->
                            let sols = sr.Diluents |> Array.map _.Generic |> String.concat "/"

                            if s |> String.isNullOrWhiteSpace then
                                if sols |> String.isNullOrWhiteSpace then
                                    " puur"
                                else
                                    $" in {sols}"
                            else
                                $" in {s} {sols}"

            let substancePrefix =
                if includeSubstanceName then
                    $"{limit.SolutionLimitTarget |> LimitTarget.toString}: "
                else
                    ""

            let conc =
                if limit.Concentration |> mmToStr |> String.isNullOrWhiteSpace then
                    ""
                else
                    $"* concentratie: {limit.Concentration |> mmToStr}"

            {|
                head = $"\n{loc}{substancePrefix}{q}{qs}{vol}"
                conc = conc
            |}


        /// Get the markdown representation of the given SolutionRules.
        let toMarkdown text (rules: SolutionRule[]) =
            let generic_md generic products =
                let text = if text |> String.isNullOrWhiteSpace then generic else text
                $"\n# %s{text}\n---\n#### Producten\n%s{products}\n"

            let department_md dep =
                let dep =
                    match dep with
                    | _ when dep = "AICU" -> "ICC"
                    | _ -> dep

                $"\n### Afdeling: %s{dep}\n"

            let substance_md substance = $"\n## %s{substance}\n"

            let pat_md pat = $"\n##### %s{pat}\n"

            let product_md product = $"\n* %s{product}\n"


            ({|
                md = ""
                rules = [||]
             |},
             rules |> Array.groupBy _.Generic)
            ||> Array.fold (fun acc (generic, rs) ->
                let prods =
                    rs
                    |> Array.collect _.SolutionLimits
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
                    |> Array.collect (fun p ->
                        if p.Reconstitution |> Array.isEmpty then
                            if p.RequiresReconstitution then
                                [| $"{product_md p.Label} oplossen in ... " |]
                            else
                                [| product_md p.Label |]
                        else
                            p.Reconstitution
                            |> Array.map (fun r ->
                                $"{p.Label} oplossen in %s{r.DiluentVolume |> Utils.ValueUnit.toString -1} voor {r.Route}"
                                |> product_md
                            )
                    )
                    |> Array.distinct
                    |> String.concat "\n"

                {| acc with
                    md = generic_md generic prods
                    rules = rs
                |}
                |> fun r ->
                    if r.rules = Array.empty then
                        r
                    else
                        (r, r.rules |> Array.groupBy _.PatientCategory.Department)
                        ||> Array.fold (fun acc (dep, rs) ->
                            let dep = dep |> Option.defaultValue ""

                            let distinctSubstances =
                                rs
                                |> Array.collect _.SolutionLimits
                                |> Array.map _.SolutionLimitTarget
                                |> Array.distinct

                            let substanceHeading, includeSubstanceName =
                                if distinctSubstances.Length = 1 then
                                    let name = distinctSubstances[0] |> LimitTarget.toString

                                    if name |> String.isNullOrWhiteSpace then
                                        "", true
                                    else
                                        substance_md name, false
                                else
                                    "", true

                            {| acc with
                                md = acc.md + department_md dep + substanceHeading
                                rules = rs
                            |}
                            |> fun r ->
                                if r.rules |> Array.isEmpty then
                                    r
                                else
                                    (r,
                                     r.rules
                                     |> Array.groupBy (fun r ->
                                         {|
                                             Age = r.PatientCategory |> PatientCategory.getAge
                                             Weight = r.PatientCategory.Weight
                                             Dose = r.Dose
                                             DoseType = r.DoseType
                                         |}
                                     ))
                                    ||> Array.fold (fun acc (sel, rs) ->
                                        let sol =
                                            rs
                                            |> Array.groupBy _.PatientCategory.Access
                                            |> Array.collect (fun (_, rs) ->
                                                match rs |> Array.tryHead with
                                                | None -> [||]
                                                | Some r ->
                                                    // Group limits by their substance/component target so that
                                                    // concentratie lines stay adjacent to the heads they describe.
                                                    // Within a single-target group the concentratie is de-duplicated
                                                    // (same substance + same conc across different qty ranges).
                                                    let targetBlocks =
                                                        r.SolutionLimits
                                                        |> Array.groupBy _.SolutionLimitTarget
                                                        |> Array.collect (fun (_, limits) ->
                                                            let parts =
                                                                limits
                                                                |> Array.map (
                                                                    printSolutionLimit r includeSubstanceName
                                                                )

                                                            let heads =
                                                                parts |> Array.map _.head |> String.concat ""

                                                            let concBlock =
                                                                parts
                                                                |> Array.map _.conc
                                                                |> Array.filter String.notEmpty
                                                                |> Array.distinct
                                                                |> String.concat "\n"

                                                            [| heads; concBlock |] |> Array.filter String.notEmpty
                                                        )

                                                    let doseFractionBlock = printDoseFraction r

                                                    [|
                                                        yield! targetBlocks
                                                        if doseFractionBlock |> String.notEmpty then
                                                            yield doseFractionBlock
                                                    |]
                                            )
                                            |> String.concat "\n"

                                        let pat =
                                            let a = sel.Age |> PatientCategory.printAgeMinMax

                                            let w =
                                                let s =
                                                    sel.Weight
                                                    |> MinMax.convertTo Units.Weight.kiloGram
                                                    |> MinMax.toString "van " "van " "tot " "tot "

                                                if s |> String.isNullOrWhiteSpace then
                                                    ""
                                                else
                                                    $"gewicht %s{s}"

                                            if a |> String.isNullOrWhiteSpace && w |> String.isNullOrWhiteSpace then
                                                ""
                                            else
                                                $"patient: %s{a} %s{w}" |> String.trim

                                        let dose = sel.Dose |> MinMax.toString "van " "van " "tot " "tot "

                                        let dt =
                                            let s = sel.DoseType |> DoseType.toDescription
                                            if s |> String.isNullOrWhiteSpace then "" else $"{s}"

                                        let header =
                                            [ dt; pat; dose ] |> List.filter String.notEmpty |> String.concat ", "

                                        {| acc with
                                            rules = rs
                                            md =
                                                (if header |> String.isNullOrWhiteSpace then
                                                     acc.md
                                                 else
                                                     acc.md + pat_md header)
                                                |> fun s -> $"{s}\n{sol}"
                                        |}
                                    )
                        )


            )
            |> _.md


        /// Get the markdown representation of the given SolutionRules.
        let printGenerics (rules: SolutionRule[]) =
            rules
            |> generics
            |> Array.map (fun generic ->
                rules
                |> Array.filter (fun sr -> sr.Generic = generic)
                |> Array.sortBy _.Generic
                |> toMarkdown ""
            )
