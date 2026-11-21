namespace Informedica.GenForm.Lib


module SolutionRule =

    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib
    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges

    open Utils


    module DilutionTarget =


        /// The name of the component or the substance.
        let toString =
            function
            | DilutionTarget.Component s
            | DilutionTarget.Substance s -> s


    module DilutionLimit =


        /// A DilutionLimit on the target with no limits set.
        let create target =
            {
                DilutionLimitTarget = target
                Quantity = MinMax.empty
                QuantityAdjust = MinMax.empty
                Quantities = None
                Concentration = MinMax.empty
                Products = [||]
            }


    module SolutionLimit =


        /// A SolutionLimit with no limits set.
        let empty =
            {
                Div = None
                Volumes = None
                Volume = MinMax.empty
                VolumeAdjust = MinMax.empty
                DripRate = MinMax.empty
                DoseFraction = MinMax.empty
            }


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
                        Diluents = get "Diluents" |> String.split "|" |> List.map String.trim
                        Volumes = get "Volumes" |> BigRational.toBrs
                        Div = get "Div" |> toBrOpt
                        MinVol = get "MinVol" |> toBrOpt
                        MaxVol = get "MaxVol" |> toBrOpt
                        MinVolAdj = get "MinVolAdj" |> toBrOpt
                        MaxVolAdj = get "MaxVolAdj" |> toBrOpt
                        AdjustUnit = get "AdjustUnit"
                        MinPerc = get "MinPerc" |> toBrOpt
                        MaxPerc = get "MaxPerc" |> toBrOpt
                        // dilution limit section
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


    /// The Component value of a SolutionRules row that limits the diluent of the rule, whichever diluent
    /// is chosen.
    let diluentComponent = "diluent"


    /// Whether a SolutionRules row is a diluent row: "diluent" in the Component column and no substance.
    let isDiluentRow (r: SolutionRuleData) =
        r.Substance |> String.isNullOrWhiteSpace
        && r.Component |> String.equalsCapInsens diluentComponent


    /// Replaces each diluent row with one row per diluent, with the diluent name as the Component.
    /// Returns the other rows unchanged.
    let expandDiluentRows diluents rows =
        rows
        |> Array.collect (fun r ->
            if r |> isDiluentRow then
                diluents |> Array.map (fun dil -> { r with Component = dil })
            else
                [| r |]
        )


    let map routeMapping (parenteral: ProductComponent[]) products data : Result<_, Message list> =
        data
        |> Array.groupBy (fun r ->
            let du = r.Unit |> UnitsParse.fromString
            let au = r.AdjustUnit |> Units.adjustUnit

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
                AdjustUnit = au
                DoseType = DoseType.fromString r.DoseType r.DoseText
                // Keep the order of the cell: an order takes the first diluent when no diluent is chosen.
                Diluents =
                    r.Diluents
                    |> List.toArray
                    |> Array.choose (fun s -> parenteral |> Array.tryFind (_.Generic >> String.equalsCapInsens s))
                    |> Array.distinctBy _.Generic
                SolutionLimit =
                    {
                        Div = r.Div
                        Volumes =
                            if r.Volumes |> Array.isEmpty then
                                None
                            else
                                r.Volumes |> ValueUnit.withUnit Units.mL |> Some
                        Volume = (r.MinVol, r.MaxVol) |> fromTupleInclIncl (Some Units.mL)
                        VolumeAdjust =
                            (r.MinVolAdj, r.MaxVolAdj)
                            |> fromTupleInclIncl (au |> Option.map (fun adj -> Units.mL |> ValueUnit.per adj))
                        DripRate =
                            (r.MinDrip, r.MaxDrip)
                            |> fromTupleInclIncl (Some(Units.Volume.milliLiter |> ValueUnit.per Units.Time.hour))
                        DoseFraction = (r.MinPerc, r.MaxPerc) |> fromTupleInclIncl (Some Units.Count.times)
                    }
                DilutionLimits = [||]
            }
        )
        |> Array.map (fun (solRule, rs) ->
            { solRule with
                DilutionLimits =
                    rs
                    |> expandDiluentRows (solRule.Diluents |> Array.map _.Generic)
                    |> Array.map (fun l ->
                        let u = l.Unit |> UnitsParse.fromString
                        // a quantity per adjust needs the adjust unit of the rule
                        let uAdj = Option.map2 (fun u adj -> u |> ValueUnit.per adj) u solRule.AdjustUnit

                        {
                            DilutionLimitTarget =
                                match l.Substance, l.Component with
                                | s, _ when s |> String.notEmpty -> s |> DilutionTarget.Substance
                                | _, c when c |> String.notEmpty -> c |> DilutionTarget.Component
                                | _ -> invalidOp "Dilution limit should be either a substance or a component limit"
                            Quantity = (l.MinQty, l.MaxQty) |> fromTupleInclIncl u
                            QuantityAdjust = (l.MinQtyAdj, l.MaxQtyAdj) |> fromTupleInclIncl uAdj
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
                                    && solRule.Form
                                       |> Option.map (fun s -> s |> String.equalsCapInsens p.Form)
                                       |> Option.defaultValue true
                                    && p.Routes |> Array.exists (Mapping.eqsRoute routeMapping (Some solRule.Route))
                                )
                        }
                    )
                    // filter out dilution limits that are empty
                    |> Array.filter (fun dilutionLimit ->
                        (dilutionLimit.Concentration |> MinMax.isEmpty
                         && dilutionLimit.Quantities |> Option.isNone
                         && dilutionLimit.QuantityAdjust |> MinMax.isEmpty
                         && dilutionLimit.Quantity |> MinMax.isEmpty)
                        |> not
                    )

            }
        )
        |> Ok


    /// Returns a label for warnings with the generic, form, route, indication, patient category
    /// (with the department) and dose type of the rule, as they appear in the sheet.
    let label (solRule: SolutionRule) =
        [
            solRule.Generic
            solRule.Form |> Option.defaultValue ""
            solRule.Route
            solRule.Indication |> Option.defaultValue ""
            solRule.PatientCategory |> PatientCategory.toString
            solRule.DoseType |> DoseType.toString
        ]
        |> List.filter String.notEmpty
        |> String.concat ", "
        |> sprintf "solution rule %s"


    /// Returns every range of a solution rule with its name: the patient ranges, the dose,
    /// the solution limits and the dilution limits.
    let ranges (solRule: SolutionRule) =
        let sl = solRule.SolutionLimit

        [
            yield! solRule.PatientCategory |> PatientCategory.ranges
            "dose", solRule.Dose
            "volume", sl.Volume
            "volume adjust", sl.VolumeAdjust
            "drip rate", sl.DripRate
            "dose fraction", sl.DoseFraction
            for dilutionLimit in solRule.DilutionLimits do
                let target = dilutionLimit.DilutionLimitTarget |> DilutionTarget.toString
                $"%s{target} quantity", dilutionLimit.Quantity
                $"%s{target} quantity adjust", dilutionLimit.QuantityAdjust
                $"%s{target} concentration", dilutionLimit.Concentration
        ]


    /// One warning per inverted range in the solution rules.
    let invertedRanges (rules: SolutionRule[]) =
        rules
        |> Array.toList
        |> List.collect (fun solRule ->
            solRule
            |> ranges
            |> MinMax.invertedRanges
            |> List.map (fun s -> $"%s{solRule |> label}: minimum above maximum, %s{s}")
        )
        |> List.distinct
        |> List.map Warning


    /// One warning per component or substance with more than one dilution limit in a solution rule.
    /// Names are compared case-insensitively. An order takes only the first dilution limit.
    let repeatedTargets (rules: SolutionRule[]) =
        rules
        |> Array.toList
        |> List.collect (fun solRule ->
            solRule.DilutionLimits
            |> Array.toList
            |> List.groupBy (fun dl ->
                match dl.DilutionLimitTarget with
                | DilutionTarget.Component s -> "component", s |> String.toLower
                | DilutionTarget.Substance s -> "substance", s |> String.toLower
            )
            |> List.filter (fun (_, dls) -> dls |> List.length > 1)
            |> List.map (fun ((kind, _), dls) ->
                let name = dls |> List.head |> _.DilutionLimitTarget |> DilutionTarget.toString

                $"%s{solRule |> label}: %i{dls |> List.length} dilution limits for the %s{kind} %s{name}, "
                + "an order takes only the first"
            )
        )
        |> List.distinct
        |> List.map Warning


    /// Returns the limit, or None when the limit is 0, because a limit of 0 counts as empty.
    let nonZeroLimit (lim: Limit option) =
        lim |> Option.filter (Limit.getValueUnit >> ValueUnit.isZero >> not)


    /// Whether a dilution limit bounds the volume of the preparation: it has a maximum quantity or a
    /// maximum quantity per kg, and a minimum concentration above 0. The maximum quantity divided by
    /// the minimum concentration is then a maximum volume.
    let boundsPreparation (dilutionLimit: DilutionLimit) =
        (dilutionLimit.Quantity.Max.IsSome || dilutionLimit.QuantityAdjust.Max.IsSome)
        && dilutionLimit.Concentration.Min
           |> Option.exists (Limit.getValueUnit >> ValueUnit.gtZero)


    /// One warning per solution rule with a maximum dose fraction but no minimum, and no maximum
    /// volume, no list of volumes, no maximum volume per kg and no dilution limit that bounds the
    /// volume. The dose count then has no maximum and the preparation cannot be set.
    let openDoseCounts (rules: SolutionRule[]) =
        rules
        |> Array.filter (fun solRule ->
            let sl = solRule.SolutionLimit

            sl.DoseFraction.Max |> nonZeroLimit |> Option.isSome
            && sl.DoseFraction.Min |> nonZeroLimit |> Option.isNone
            && sl.Volume.Max |> Option.isNone
            && sl.Volumes |> Option.isNone
            && sl.VolumeAdjust.Max |> Option.isNone
            && solRule.DilutionLimits |> Array.exists boundsPreparation |> not
        )
        |> Array.map (fun solRule ->
            $"%s{solRule |> label}: a maximum dose fraction without a minimum and no maximum volume, "
            + "so the preparation cannot be set"
        )
        |> Array.toList
        |> List.distinct
        |> List.map Warning


    /// Maps one SolutionRules row on its own to its solution rule; None when the row does not map.
    let ruleOfRow parenteral r =
        match [| r |] |> map [||] parenteral [||] with
        | Ok [| solRule |] -> Some solRule
        | _ -> None


    /// One warning per solution rule whose Diluents cell names no parenteral product, so the rule
    /// loads without diluents. Each row is mapped on its own and grouped by the rule without its
    /// dilution limits, so a rule of several rows gives one warning.
    let missingDiluents parenteral (data: SolutionRuleData[]) =
        data
        |> Array.filter (fun r -> r.Diluents |> List.exists String.notEmpty)
        |> Array.choose (fun r ->
            r
            |> ruleOfRow parenteral
            |> Option.filter (_.Diluents >> Array.isEmpty)
            |> Option.map (fun solRule -> { solRule with DilutionLimits = [||] }, r.Diluents)
        )
        |> Array.groupBy fst
        |> Array.map (fun (solRule, xs) ->
            let diluents =
                xs
                |> Array.collect (snd >> List.toArray)
                |> Array.filter String.notEmpty
                |> Array.distinct
                |> String.concat "|"

            Warning $"%s{solRule |> label}: no parenteral product matches the diluents %s{diluents}"
        )
        |> Array.toList


    /// One warning per solution rule with a diluent row but no diluent, because its Diluents cell is
    /// empty or names no parenteral product. The diluent row then limits nothing.
    let diluentRowsWithoutDiluent parenteral (data: SolutionRuleData[]) =
        data
        |> Array.filter isDiluentRow
        |> Array.choose (
            ruleOfRow parenteral
            >> Option.filter (_.Diluents >> Array.isEmpty)
            >> Option.map (fun solRule -> { solRule with DilutionLimits = [||] })
        )
        |> Array.distinct
        |> Array.map (fun solRule ->
            Warning $"%s{solRule |> label}: a diluent row but no diluent, so the diluent row limits nothing"
        )
        |> Array.toList


    /// Whether a SolutionRules row has a volume or a quantity per adjust.
    let hasLimitPerAdjust (r: SolutionRuleData) =
        [ r.MinVolAdj; r.MaxVolAdj; r.MinQtyAdj; r.MaxQtyAdj ]
        |> List.exists Option.isSome


    /// One warning per solution rule with a row that has a volume or a quantity per adjust but no adjust unit,
    /// so the limit is left out. A rule of several such rows gives one warning.
    let missingAdjustUnits parenteral (data: SolutionRuleData[]) =
        data
        |> Array.filter (fun r -> r |> hasLimitPerAdjust && r.AdjustUnit |> Units.adjustUnit |> Option.isNone)
        |> Array.choose (
            ruleOfRow parenteral
            >> Option.map (fun solRule -> { solRule with DilutionLimits = [||] })
        )
        |> Array.distinct
        |> Array.map (fun solRule ->
            Warning(
                $"%s{solRule |> label}: a volume or a quantity per adjust without an adjust unit, "
                + "so the limit is left out"
            )
        )
        |> Array.toList


    /// One warning per solution rule whose rows give different adjust units, so the rows form separate rules.
    /// Each row is mapped on its own and grouped by the rule without its adjust unit and its limits per adjust.
    let mixedAdjustUnits parenteral (data: SolutionRuleData[]) =
        data
        |> Array.choose (
            ruleOfRow parenteral
            >> Option.map (fun solRule ->
                { solRule with
                    AdjustUnit = None
                    SolutionLimit = { solRule.SolutionLimit with VolumeAdjust = MinMax.empty }
                    DilutionLimits = [||]
                },
                solRule.AdjustUnit
            )
        )
        |> Array.groupBy fst
        |> Array.choose (fun (solRule, xs) ->
            let units = xs |> Array.map snd |> Array.distinct

            if units |> Array.length > 1 then
                let text =
                    units
                    |> Array.map (fun u ->
                        match DoseRule.adjStr u with
                        | "" -> "none"
                        | s -> s
                    )
                    |> String.concat ", "

                Warning $"%s{solRule |> label}: the rows give the adjust units %s{text}, so they form separate rules"
                |> Some
            else
                None
        )
        |> Array.toList


    /// Whether a solution rule applies to a dose rule: the same generic and route, and the same form,
    /// indication and dose type when the solution rule names them, matched as the function filter
    /// matches them for an order. Rules that name different departments or different locations do
    /// not match.
    let appliesToDoseRule routeMapping (solRule: SolutionRule) (dr: DoseRule) =
        solRule.Generic |> String.equalsCapInsens (dr.Generic |> Generic.genericName)
        && PatientCategory.canMeet solRule.PatientCategory.Department dr.PatientCategory.Department
        && PatientCategory.canMeet solRule.PatientCategory.Location dr.PatientCategory.Location
        && solRule.Form
           |> Option.forall (String.equalsCapInsens (dr.Generic.Form |> PharmaceuticalForm.toString))
        && solRule.Indication |> Option.forall (String.equalsCapInsens dr.Indication)
        && solRule.Route |> Mapping.eqsRoute routeMapping (Some dr.Route)
        && (solRule.DoseType = NoDoseType
            || if solRule.DoseType |> DoseType.getText |> String.isNullOrWhiteSpace then
                   DoseType.eqsType solRule.DoseType dr.DoseType
               else
                   DoseType.eqs solRule.DoseType dr.DoseType)


    /// Whether the adjust unit of the solution rule fits the adjust unit of the dose rule: the two are the same,
    /// or the dose rule or the solution rule has none.
    let adjustUnitsMatch (dr: DoseRule) (solRule: SolutionRule) =
        match dr.AdjustUnit, solRule.AdjustUnit with
        | Some du, Some su -> du |> Units.eqsUnit su
        | _ -> true


    /// Whether a solution rule applies to a dose rule and can meet a patient of that dose rule.
    let canShareDoseRule routeMapping (dr: DoseRule) (solRule: SolutionRule) =
        dr |> appliesToDoseRule routeMapping solRule
        && PatientCategory.canShare solRule.PatientCategory dr.PatientCategory


    /// Whether a solution rule applies to a dose rule, can meet a patient of that dose rule, and fits its adjust
    /// unit.
    let canMeetDoseRule routeMapping dr solRule =
        canShareDoseRule routeMapping dr solRule && adjustUnitsMatch dr solRule


    /// Leaves out the solution rules whose adjust unit differs from the adjust unit of the dose rule. A solution
    /// rule stays when it or the dose rule has no adjust unit.
    let forDoseRule dr solRules = solRules |> Array.filter (adjustUnitsMatch dr)


    /// One warning per dose rule and solution rule that apply to each other and can meet the same patient, but
    /// whose adjust units differ, so the solution rule does not apply to the dose rule.
    let adjustUnitMismatches routeMapping (doseRules: DoseRule[]) (rules: SolutionRule[]) =
        rules
        |> Array.toList
        |> List.collect (fun solRule ->
            doseRules
            |> Array.toList
            |> List.filter (fun dr -> canShareDoseRule routeMapping dr solRule && adjustUnitsMatch dr solRule |> not)
            |> List.map (fun dr ->
                $"%s{solRule |> label} and %s{dr |> DoseRule.label}: the adjust unit "
                + $"%s{solRule.AdjustUnit |> DoseRule.adjStr} differs from "
                + $"%s{dr.AdjustUnit |> DoseRule.adjStr}, so the solution rule does not apply"
            )
        )
        |> List.distinct
        |> List.map Warning


    /// Returns the range of the number of doses in the preparation: the reciprocal of the dose
    /// fraction, so the maximum fraction gives the minimum count and the minimum fraction the maximum
    /// count. Without a maximum fraction the maximum fraction is 1, unless the minimum fraction is
    /// above 1. A fraction of 0 counts as absent. Without either fraction the range is empty.
    let doseCountRange (solRule: SolutionRule) =
        let valueReciprocal = ValueUnit.applyToValue (Array.map (fun v -> 1N / v))
        let limitReciprocal = Limit.apply valueReciprocal valueReciprocal
        let sl = solRule.SolutionLimit

        match sl.DoseFraction.Min |> nonZeroLimit, sl.DoseFraction.Max |> nonZeroLimit with
        | None, None -> MinMax.empty
        | min, max ->
            let max =
                match min, max with
                // a minimum fraction above 1 with a maximum of 1 would be an empty range
                | Some lim, None when lim |> Limit.getValue |> Option.exists (fun v -> v > 1N) -> None
                | _, None -> Some Limit.one
                | _, Some _ -> max

            { MinMax.empty with
                Min = max |> Option.map limitReciprocal
                Max = min |> Option.map limitReciprocal
            }


    /// Whether the dose count of the solution rule is at least 1, so that a dose is at most the whole
    /// preparation. An empty dose count counts as 1, because the order builder then sets it to 1.
    let hasDoseCountOfAtLeastOne solRule =
        match solRule |> doseCountRange with
        | {
              Min = None
              Max = None
          } -> true
        | count -> count.Min |> Option.bind Limit.getValue |> Option.exists (fun c -> c >= 1N)


    /// Returns the first dilution limit of the component, the one an order takes. Names are compared
    /// case-insensitively.
    let componentLimit name (solRule: SolutionRule) =
        solRule.DilutionLimits
        |> Array.tryFind (fun dl ->
            match dl.DilutionLimitTarget with
            | DilutionTarget.Component c -> c |> String.equalsCapInsens name
            | DilutionTarget.Substance _ -> false
        )


    /// Whether the solution rule can limit the component for some patient: the first dilution limit of
    /// the component has a maximum quantity, a maximum quantity per adjust unit or a list of quantities.
    let canLimitComponent (solRule: SolutionRule) name =
        solRule
        |> componentLimit name
        |> Option.exists (fun dl ->
            dl.Quantity.Max |> Option.isSome
            || dl.QuantityAdjust.Max |> Option.isSome
            || dl.Quantities |> Option.isSome
        )


    /// Whether a solution rule that applies to the dose rule and can meet its patient limits every
    /// component of the dose rule, with a dose count of at least 1.
    let anyLimitsDoseRule routeMapping (solutionRules: SolutionRule[]) (dr: DoseRule) =
        let names = dr.ComponentLimits |> Array.map _.Name

        solutionRules
        |> Array.exists (fun solRule ->
            solRule |> canMeetDoseRule routeMapping dr
            && solRule |> hasDoseCountOfAtLeastOne
            && names |> Array.forall (canLimitComponent solRule)
        )


    /// Whether no dose count in the range meets both the dose range and the range in the preparation:
    /// the dose minimum is above the preparation maximum divided by the minimum dose count, or the dose
    /// maximum is below the preparation minimum divided by the maximum dose count. An empty dose count
    /// range counts as a dose count of 1. Ranges in different unit groups are not compared.
    let noDoseCountMeets (count: MinMax) (dose: MinMax) (prep: MinMax) =
        let countMin, countMax =
            match count.Min, count.Max with
            | None, None -> Some 1N, Some 1N
            | min, max -> min |> Option.bind Limit.getValue, max |> Option.bind Limit.getValue

        let outOfReach f doseLimit prepLimit count =
            match doseLimit |> Option.map Limit.getValueUnit, prepLimit |> Option.map Limit.getValueUnit, count with
            | Some d, Some p, Some c when ValueUnit.eqsGroup d p ->
                f d (p |> ValueUnit.applyToValue (Array.map (fun v -> v / c)))
            | _ -> false

        outOfReach ValueUnit.gt dose.Min prep.Max countMin
        || outOfReach ValueUnit.st dose.Max prep.Min countMax


    /// Returns a warning per component that both rules limit, when the dose range and the range in the
    /// preparation meet for no dose count of the solution rule. Quantities and quantities per adjust are
    /// compared separately, with the first dilution limit of the component.
    let pairConflicts (solRule: SolutionRule) (dr: DoseRule) =
        let count = solRule |> doseCountRange
        let rangeText = MinMax.toString "min. " "min. " "max. " "max. "

        dr.ComponentLimits
        |> Array.toList
        |> List.choose (fun cl ->
            match cl.Limit, solRule |> componentLimit cl.Name with
            | Some doseLimit, Some dilutionLimit -> Some(cl.Name, doseLimit, dilutionLimit)
            | _ -> None
        )
        |> List.collect (fun (name, doseLimit, dilutionLimit) ->
            [
                name, "quantity", doseLimit.Quantity, dilutionLimit.Quantity
                name,
                $"quantity per %s{solRule.AdjustUnit |> DoseRule.adjStr}",
                doseLimit.QuantityAdjust,
                dilutionLimit.QuantityAdjust
            ]
        )
        |> List.filter (fun (_, _, dose, prep) -> noDoseCountMeets count dose prep)
        |> List.map (fun (name, kind, dose, prep) ->
            $"%s{solRule |> label} and %s{dr |> DoseRule.label}, component %s{name}: "
            + $"the dose %s{kind} %s{dose |> rangeText} and the %s{kind} in the preparation "
            + $"%s{prep |> rangeText} meet for no dose count"
        )


    /// One warning per dose rule, solution rule and component, when the rules apply to each other and
    /// can meet the same patient, and the dose range and the range in the preparation of the component
    /// meet for no dose count of the solution rule.
    let conflictingLimits routeMapping (doseRules: DoseRule[]) (rules: SolutionRule[]) =
        rules
        |> Array.toList
        |> List.collect (fun solRule ->
            doseRules
            |> Array.toList
            |> List.filter (fun dr -> solRule |> canMeetDoseRule routeMapping dr)
            |> List.collect (pairConflicts solRule)
        )
        |> List.distinct
        |> List.map Warning


    /// One warning per dose rule without limits that a solution rule can meet, when no solution rule
    /// limits every component of it. Loading leaves such a dose rule out.
    let unlimitedDoseRules routeMapping (doseRules: DoseRule[]) (rules: SolutionRule[]) =
        doseRules
        |> Array.filter (fun dr ->
            dr |> DoseRule.hasNoLimits
            && rules |> Array.exists (canMeetDoseRule routeMapping dr)
            && dr |> anyLimitsDoseRule routeMapping rules |> not
        )
        |> Array.map (fun dr ->
            let names = dr.ComponentLimits |> Array.map _.Name |> String.concat ", "

            $"%s{dr |> DoseRule.label}: no limits, and no solution rule limits every component (%s{names}), "
            + "so the dose rule is left out"
        )
        |> Array.toList
        |> List.distinct
        |> List.map Warning


    /// One warning per pair of solution rules that apply to the same dose rule and can meet the same
    /// patient. Ranges that overlap two by two share a common value, so checking each pair of the two
    /// solution rules and the dose rule is enough. Each solution rule gives its own scenario, so the
    /// user must choose between them. The dose range of a solution rule is not part of the matching,
    /// so rules that differ only in dose range also compete.
    let competingRules routeMapping (doseRules: DoseRule[]) (rules: SolutionRule[]) =
        let appliesToBoth a b dr =
            a |> canMeetDoseRule routeMapping dr && b |> canMeetDoseRule routeMapping dr

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


    /// The names of the diluents of the solution rule that are also components of the dose rule. An
    /// order with such a pair would hold that component twice, so the pair is refused.
    let componentDiluents (dr: DoseRule) (solRule: SolutionRule) =
        solRule.Diluents
        |> Array.map _.Generic
        |> Array.filter (fun dil -> dr.ComponentLimits |> Array.exists (_.Name >> String.equalsCapInsens dil))


    /// One warning per solution rule and dose rule that apply to each other and can meet the same patient,
    /// while a diluent of the solution rule is also a component of the dose rule: the solution rule is not
    /// applied to that dose rule.
    let diluentsThatAreComponents routeMapping (doseRules: DoseRule[]) (rules: SolutionRule[]) =
        [
            for solRule in rules do
                for dr in doseRules do
                    if solRule |> canMeetDoseRule routeMapping dr then
                        match componentDiluents dr solRule with
                        | [||] -> ()
                        | dils ->
                            let names = dils |> String.concat ", "

                            $"%s{solRule |> label}: the diluent %s{names} is also a component of a dose rule of "
                            + $"%s{dr.Generic |> Generic.toString}, so the solution rule is not applied to that dose rule"
        ]
        |> List.distinct
        |> List.map Warning


    /// One warning per name in a Diluents cell whose parenteral Formulary product is not marked
    /// IsDiluent, listing the generics whose solution rules name it. A name without a parenteral
    /// product is reported by missingDiluents.
    let unmarkedDiluents (formulary: FormularyProduct[]) (data: SolutionRuleData[]) =
        let unmarked name =
            formulary
            |> Array.exists (fun fp ->
                fp.ProductType = ParenteralProduct
                && fp.Generic |> String.equalsCapInsens name
                && not fp.CanBeUsedAsDiluent
            )

        data
        |> Array.collect (fun r -> r.Diluents |> List.toArray |> Array.map (fun dil -> dil, r.Generic))
        |> Array.filter (fst >> unmarked)
        // the names match the Formulary in any case, so the spellings of one name give one warning
        |> Array.groupBy (fst >> String.toLower)
        |> Array.map (fun (_, xs) ->
            let dil = xs |> Array.head |> fst
            let generics = xs |> Array.map snd |> Array.distinct |> String.concat ", "
            Warning $"the diluent %s{dil} is not marked IsDiluent in the Formulary, named by %s{generics}"
        )
        |> Array.toList


    /// Returns the substances whose concentration limit allows no strength of the products. A strength
    /// in a unit group that the limit cannot be compared with is skipped, and a substance without a
    /// comparable strength is not returned. The limit is inclusive, so a minimum equal to the maximum
    /// allows that one strength.
    let substancesWithoutAllowedStrength (products: ProductComponent[]) (solRule: SolutionRule) =
        solRule.DilutionLimits
        |> Array.choose (fun dilutionLimit ->
            match dilutionLimit.DilutionLimitTarget with
            | DilutionTarget.Substance s when dilutionLimit.Concentration |> MinMax.isEmpty |> not ->
                let conc = dilutionLimit.Concentration

                let comparable vu =
                    [ conc.Min; conc.Max ]
                    |> List.choose id
                    |> List.forall (Limit.getValueUnit >> ValueUnit.eqsGroup vu)

                let strengths =
                    products
                    |> Array.collect _.Substances
                    |> Array.filter (fun sub -> sub.Name |> String.equalsCapInsens s)
                    |> Array.choose _.Concentration
                    |> Array.filter comparable

                if
                    strengths |> Array.isEmpty |> not
                    && strengths |> Array.forall (Some >> MinMax.inRange conc >> not)
                then
                    Some s
                else
                    None
            | _ -> None
        )


    /// One warning per solution rule without diluents whose concentration limit allows no strength of
    /// the products of a dose rule it applies to. Only a dose rule with one component is checked,
    /// because a second component can dilute the first. The products are reconstituted for the
    /// location of the solution rule and the department of the solution rule, else of the dose rule.
    /// When neither rule names a department, the products are reconstituted for each department their
    /// reconstitution rules name, and for no department.
    let undilutableRules routeMapping (doseRules: DoseRule[]) (rules: SolutionRule[]) =
        let substances (solRule: SolutionRule) =
            doseRules
            |> Array.filter (fun dr ->
                dr.ComponentLimits |> Array.length = 1
                && dr |> appliesToDoseRule routeMapping solRule
            )
            |> Array.collect (fun dr ->
                let departments =
                    match solRule.PatientCategory.Department, dr.PatientCategory.Department with
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
                    let dr, _ = dr |> DoseRule.reconstitute routeMapping solRule.PatientCategory.Location dep

                    solRule
                    |> substancesWithoutAllowedStrength (dr.ComponentLimits |> Array.collect _.Products)
                )
            )
            |> Array.distinct

        rules
        |> Array.filter (fun solRule -> solRule.Diluents |> Array.isEmpty)
        |> Array.choose (fun solRule ->
            match solRule |> substances with
            | [||] -> None
            | subs ->
                let names = subs |> String.concat ", "

                Warning
                    $"%s{solRule |> label}: given undiluted, the concentration limit of %s{names} allows no strength of its products"
                |> Some
        )
        |> Array.distinct
        |> Array.toList


    let get dataUrlId routeMapping (parenteral: ProductComponent[]) products : Result<_, Message list> =
        try
            dataUrlId |> getData |> Result.bind (map routeMapping parenteral products)
        with exn ->
            Result.createError "Error in SolutionRule.getResult: " exn


    /// Returns the solution rules that match the filter. The department is checked only when given, and
    /// the other patient data only when set, so a lookup without patient data returns the rules of every
    /// patient. When the filter names a diluent, each rule keeps only that diluent.
    let filter mapping (filter: SolutionFilter) (solutionRules: SolutionRule[]) =
        let eqs a (b: string) =
            a |> Option.map (String.equalsCapInsens b) |> Option.defaultValue true

        [|
            fun (solRule: SolutionRule) -> solRule.Generic |> String.equalsCapInsens filter.Generic
            // a rule that names a department is for the patients of that department only
            if filter.PatientData |> PatientData.department |> Option.isSome then
                fun (solRule: SolutionRule) ->
                    filter.PatientData
                    |> PatientData.department
                    |> PatientCategory.departmentMatches solRule.PatientCategory.Department
            // don't filter on the rest of the patient if it is not set; a department alone does not set it
            if filter.PatientData |> PatientData.hasDataBesidesDepartment then
                fun (solRule: SolutionRule) ->
                    solRule.PatientCategory
                    |> PatientData.matchesCategoryWithoutLocationAndGender filter.PatientData
            fun (solRule: SolutionRule) -> solRule.Form |> Option.map (eqs filter.Form) |> Option.defaultValue true
            fun (solRule: SolutionRule) ->
                solRule.Indication
                |> Option.map (eqs filter.Indication)
                |> Option.defaultValue true
            fun (solRule: SolutionRule) ->
                filter.Route |> Option.isNone
                || solRule.Route |> Mapping.eqsRoute mapping filter.Route
            fun (solRule: SolutionRule) ->
                solRule.DoseType = NoDoseType
                || filter.DoseType
                   |> Option.map (
                       if solRule.DoseType |> DoseType.getText |> String.isNullOrWhiteSpace then
                           DoseType.eqsType solRule.DoseType
                       else
                           DoseType.eqs solRule.DoseType
                   )
                   |> Option.defaultValue true
        |]
        |> Array.fold (fun (acc: SolutionRule[]) pred -> acc |> Array.filter pred) solutionRules
        |> Array.map (fun solRule ->
            { solRule with
                Diluents =
                    solRule.Diluents
                    |> Array.filter (fun dil ->
                        filter.Diluent |> Option.map ((=) dil.Generic) |> Option.defaultValue true
                    )
            }
        )


    /// Returns the sorted distinct values of a field of the solution rules.
    let private getMember getter (rules: SolutionRule[]) = rules |> Array.map getter |> Array.distinct |> Array.sort


    /// Returns the sorted distinct generics of the solution rules.
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


        /// Formats the dose fraction of the rule as the line "geef X% van de bereiding". Returns an
        /// empty string when the rule has no dose fraction.
        let printDoseFraction (solRule: SolutionRule) =
            let toPerc l =
                l
                |> Limit.getValueUnit
                |> ValueUnit.getValue
                |> Array.item 0
                |> BigRational.toDouble
                |> fun x -> $"{x * 100.}"

            let sl = solRule.SolutionLimit

            let p =
                match sl.DoseFraction.Min, sl.DoseFraction.Max with
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


        /// Returns the head line and the concentration line of a dilution limit, so a caller can remove
        /// repeated concentration lines. With includeSubstanceName false the head leaves out the
        /// substance name, for a substance that has its own heading.
        let printDilutionLimit (solRule: SolutionRule) (includeSubstanceName: bool) (limit: DilutionLimit) =
            let mmToStr = MinMax.toString "min. " "min. " "max. " "max. "
            let sl = solRule.SolutionLimit

            let loc =
                match solRule.PatientCategory.Access with
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
                if sl.Volume |> mmToStr |> String.isNullOrWhiteSpace then
                    ""
                else
                    sl.Volume
                    |> mmToStr
                    |> fun s -> $""" in {s} {solRule.Diluents |> Array.map _.Generic |> String.concat "/"}"""
                |> fun s ->
                    if s |> String.notEmpty then
                        s
                    else
                        sl.Volumes
                        |> Option.map (Utils.ValueUnit.toString -1)
                        |> Option.defaultValue ""
                        |> fun s ->
                            let diluents = solRule.Diluents |> Array.map _.Generic |> String.concat "/"

                            if s |> String.isNullOrWhiteSpace then
                                if diluents |> String.isNullOrWhiteSpace then
                                    " puur"
                                else
                                    $" in {diluents}"
                            else
                                $" in {s} {diluents}"

            let substancePrefix =
                if includeSubstanceName then
                    $"{limit.DilutionLimitTarget |> DilutionTarget.toString}: "
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


        /// Returns the Markdown text of the solution rules, grouped by generic, department, patient
        /// and dose, and access. The text is the heading; an empty text gives the generic.
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
                    |> Array.collect _.DilutionLimits
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
                                $"{p.Label} oplossen in %s{r.SolventVolume |> Utils.ValueUnit.toString -1} voor {r.Route}"
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
                                |> Array.collect _.DilutionLimits
                                |> Array.map _.DilutionLimitTarget
                                |> Array.distinct

                            let substanceHeading, includeSubstanceName =
                                if distinctSubstances.Length = 1 then
                                    distinctSubstances[0] |> DilutionTarget.toString |> substance_md, false
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
                                        let rules_md =
                                            rs
                                            |> Array.groupBy _.PatientCategory.Access
                                            |> Array.collect (fun (_, rs) ->
                                                match rs |> Array.tryHead with
                                                | None -> [||]
                                                | Some r ->
                                                    // Group the limits by target so the concentration lines
                                                    // stay next to the head lines they describe. Within one
                                                    // target a repeated concentration line is printed once.
                                                    let targetBlocks =
                                                        r.DilutionLimits
                                                        |> Array.groupBy _.DilutionLimitTarget
                                                        |> Array.collect (fun (_, limits) ->
                                                            let parts =
                                                                limits
                                                                |> Array.map (
                                                                    printDilutionLimit r includeSubstanceName
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
                                                |> fun s -> $"{s}\n{rules_md}"
                                        |}
                                    )
                        )


            )
            |> _.md


        /// Returns the Markdown text of the solution rules, one text per generic.
        let printGenerics (rules: SolutionRule[]) =
            rules
            |> generics
            |> Array.map (fun generic ->
                rules
                |> Array.filter (fun solRule -> solRule.Generic = generic)
                |> Array.sortBy _.Generic
                |> toMarkdown ""
            )
