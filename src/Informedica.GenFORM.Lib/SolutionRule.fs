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
                DosePerc = (r.MinPerc, r.MaxPerc) |> fromTupleInclIncl (Some Units.Count.times)
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
                                | s, _ when s |> String.isNullOrWhiteSpace |> not -> s |> SubstanceLimitTarget
                                | _, c when c |> String.isNullOrWhiteSpace |> not -> c |> ComponentLimitTarget
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
    let private getMember getter (rules: SolutionRule[]) =
        rules |> Array.map getter |> Array.distinct |> Array.sort


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
        /// DosePerc bounds on the rule. Returns an empty string when no bounds
        /// are set.
        let printDosePerc (sr: SolutionRule) =
            let toPerc l =
                l
                |> Limit.getValueUnit
                |> ValueUnit.getValue
                |> Array.item 0
                |> BigRational.toDouble
                |> fun x -> $"{x * 100.}"

            let p =
                match sr.DosePerc.Min, sr.DosePerc.Max with
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
        /// `includeSubstanceName = false` suppresses the inline substance prefix
        /// (used when the substance is rendered as a separate heading).
        let printSolutionLimit (sr: SolutionRule) (includeSubstanceName: bool) (limit: SolutionLimit) =
            let mmToStr = MinMax.toString "min. " "min. " "max. " "max. "

            let loc =
                match sr.PatientCategory.Access with
                | CVL -> "###### centraal: \n* "
                | PVL -> "###### perifeer: \n* "
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
                    if s |> String.isNullOrWhiteSpace |> not then
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
                                                                |> Array.filter (String.isNullOrWhiteSpace >> not)
                                                                |> Array.distinct
                                                                |> String.concat "\n"

                                                            [| heads; concBlock |]
                                                            |> Array.filter (String.isNullOrWhiteSpace >> not)
                                                        )

                                                    let dosePercBlock = printDosePerc r

                                                    [|
                                                        yield! targetBlocks
                                                        if dosePercBlock |> String.isNullOrWhiteSpace |> not then
                                                            yield dosePercBlock
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
                                            [ dt; pat; dose ]
                                            |> List.filter (String.isNullOrWhiteSpace >> not)
                                            |> String.concat ", "

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
