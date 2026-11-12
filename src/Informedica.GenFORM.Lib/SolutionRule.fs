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
                        Diluents = get "Diluents" |> String.split "|" |> List.map String.trim
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
                        MinQtyPerKg = get "MinQtyPerKg" |> toBrOpt
                        MaxQtyPerKg = get "MaxQtyPerKg" |> toBrOpt
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


    /// The Component of a SolutionRules row that limits the diluent of the rule, whichever diluent is
    /// chosen.
    let diluentComponent = "diluent"


    /// Whether a SolutionRules row is a diluent row: "diluent" in the Component column and no substance.
    let isDiluentRow (r: SolutionRuleData) =
        r.Substance |> String.isNullOrWhiteSpace
        && r.Component |> String.equalsCapInsens diluentComponent


    /// Each diluent row is replaced by one row per diluent, named after that diluent; the other rows
    /// stay.
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
                // in the order of the cell, so that the first diluent of the cell is the one an order
                // takes when none is chosen
                Diluents =
                    r.Diluents
                    |> List.toArray
                    |> Array.choose (fun s -> parenteral |> Array.tryFind (_.Generic >> String.equalsCapInsens s))
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
        |> Array.map (fun (solRule, rs) ->
            { solRule with
                SolutionLimits =
                    rs
                    |> expandDiluentRows (solRule.Diluents |> Array.map _.Generic)
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
                            QuantityAdj = (l.MinQtyPerKg, l.MaxQtyPerKg) |> fromTupleInclIncl au
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
                    // filter out solution limits that are empty
                    |> Array.filter (fun solutionLimit ->
                        (solutionLimit.Concentration |> MinMax.isEmpty
                         && solutionLimit.Quantities |> Option.isNone
                         && solutionLimit.QuantityAdj |> MinMax.isEmpty
                         && solutionLimit.Quantity |> MinMax.isEmpty)
                        |> not
                    )

            }
        )
        |> Ok


    /// The rule as an admin finds it in the sheet: generic, form, route, indication,
    /// patient, which includes the department, and dose type.
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


    /// Every range a solution rule carries, each with its name.
    let ranges (solRule: SolutionRule) =
        [
            yield! solRule.PatientCategory |> PatientCategory.ranges
            "dose", solRule.Dose
            "volume", solRule.Volume
            "volume adjust", solRule.VolumeAdjust
            "drip rate", solRule.DripRate
            "dose fraction", solRule.DoseFraction
            for solutionLimit in solRule.SolutionLimits do
                let target = solutionLimit.SolutionLimitTarget |> LimitTarget.toString
                $"%s{target} quantity", solutionLimit.Quantity
                $"%s{target} quantity adjust", solutionLimit.QuantityAdj
                $"%s{target} concentration", solutionLimit.Concentration
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


    /// One warning per component or substance that a solution rule gives more than one solution limit,
    /// names compared case-insensitively. An order takes only the first of those solution limits.
    let repeatedTargets (rules: SolutionRule[]) =
        rules
        |> Array.toList
        |> List.collect (fun solRule ->
            solRule.SolutionLimits
            |> Array.toList
            |> List.groupBy (fun dl ->
                match dl.SolutionLimitTarget with
                | ComponentLimitTarget s -> "component", s |> String.toLower
                | target -> "substance", target |> LimitTarget.toString |> String.toLower
            )
            |> List.filter (fun (_, dls) -> dls |> List.length > 1)
            |> List.map (fun ((kind, _), dls) ->
                let name = dls |> List.head |> _.SolutionLimitTarget |> LimitTarget.toString

                $"%s{solRule |> label}: %i{dls |> List.length} solution limits for the %s{kind} %s{name}, "
                + "an order takes only the first"
            )
        )
        |> List.distinct
        |> List.map Warning


    /// A limit, or nothing when the limit is 0, which counts as empty.
    let nonZeroLimit (lim: Limit option) =
        lim |> Option.filter (Limit.getValueUnit >> ValueUnit.isZero >> not)


    /// Whether a solution limit bounds the preparation: the maximum quantity of a substance, or
    /// the maximum quantity per kg once the weight is known, divided by the minimum concentration
    /// of that substance is a maximum volume.
    let boundsPreparation (solutionLimit: SolutionLimit) =
        (solutionLimit.Quantity.Max.IsSome || solutionLimit.QuantityAdj.Max.IsSome)
        && solutionLimit.Concentration.Min
           |> Option.exists (Limit.getValueUnit >> ValueUnit.gtZero)


    /// One warning per solution rule whose dose count has no maximum, because the rule gives a
    /// maximum dose fraction and no minimum, while the preparation has no maximum either: no
    /// maximum volume, no list of volumes, no maximum volume per kg, which bounds the preparation
    /// once the weight is known, and no solution limit that bounds it. The user then cannot set
    /// the preparation.
    let openDoseCounts (rules: SolutionRule[]) =
        rules
        |> Array.filter (fun solRule ->
            solRule.DoseFraction.Max |> nonZeroLimit |> Option.isSome
            && solRule.DoseFraction.Min |> nonZeroLimit |> Option.isNone
            && solRule.Volume.Max |> Option.isNone
            && solRule.Volumes |> Option.isNone
            && solRule.VolumeAdjust.Max |> Option.isNone
            && solRule.SolutionLimits |> Array.exists boundsPreparation |> not
        )
        |> Array.map (fun solRule ->
            $"%s{solRule |> label}: a maximum dose fraction without a minimum and no maximum volume, "
            + "so the preparation cannot be set"
        )
        |> Array.toList
        |> List.distinct
        |> List.map Warning


    /// One warning per solution rule whose rows name diluents none of which matches a
    /// parenteral product, so the rule loads with no diluents. Each row is built into a rule
    /// on its own and the rules are grouped without their limits, which is the grouping map
    /// makes, so a rule of several rows gives a single warning.
    let missingDiluents parenteral (data: SolutionRuleData[]) =
        data
        |> Array.filter (fun r -> r.Diluents |> List.exists String.notEmpty)
        |> Array.choose (fun r ->
            match [| r |] |> map [||] parenteral [||] with
            | Ok [| solRule |] when solRule.Diluents |> Array.isEmpty ->
                Some({ solRule with SolutionLimits = [||] }, r.Diluents)
            | _ -> None
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
        |> Array.choose (fun r ->
            match [| r |] |> map [||] parenteral [||] with
            | Ok [| solRule |] when solRule.Diluents |> Array.isEmpty -> Some { solRule with SolutionLimits = [||] }
            | _ -> None
        )
        |> Array.distinct
        |> Array.map (fun solRule ->
            Warning $"%s{solRule |> label}: a diluent row but no diluent, so the diluent row limits nothing"
        )
        |> Array.toList


    /// Whether a solution rule applies to a dose rule: the generic and the route, and the form,
    /// indication and dose type when the solution rule names them, matched as filter matches them
    /// for an order. A solution rule and a dose rule that name different departments or different
    /// locations apply to different patients, so they do not match.
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


    /// Whether a solution rule applies to a dose rule and can meet a patient of that dose rule.
    let canMeetDoseRule routeMapping (dr: DoseRule) (solRule: SolutionRule) =
        dr |> appliesToDoseRule routeMapping solRule
        && PatientCategory.canShare solRule.PatientCategory dr.PatientCategory


    /// The dose count range of the solution rule: the number of doses in the preparation, from the
    /// fraction of the preparation that makes up one dose. A higher fraction is fewer doses, so the
    /// minimum of the dose count comes from the maximum of the fraction and the maximum from the
    /// minimum. Without a maximum fraction, and with a minimum fraction of at most 1, the maximum
    /// fraction is 1, the whole preparation, so the dose count is at least 1. Without a minimum
    /// fraction the dose count has no maximum. A fraction of 0 counts as no fraction, and without
    /// either fraction the dose count is empty.
    let doseCountRange (solRule: SolutionRule) =
        let valueReciprocal = ValueUnit.applyToValue (Array.map (fun v -> 1N / v))
        let limitReciprocal = Limit.apply valueReciprocal valueReciprocal

        match solRule.DoseFraction.Min |> nonZeroLimit, solRule.DoseFraction.Max |> nonZeroLimit with
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


    /// Whether the dose count of the solution rule is at least 1, so that the dose is at most the
    /// preparation. Without a dose fraction the order builder sets the dose count to 1.
    let hasDoseCountOfAtLeastOne solRule =
        match solRule |> doseCountRange with
        | {
              Min = None
              Max = None
          } -> true
        | count -> count.Min |> Option.bind Limit.getValue |> Option.exists (fun c -> c >= 1N)


    /// The first solution limit of the component, the one the order takes.
    let componentLimit name (solRule: SolutionRule) =
        solRule.SolutionLimits
        |> Array.tryFind (fun dl ->
            match dl.SolutionLimitTarget with
            | ComponentLimitTarget c -> c |> String.equalsCapInsens name
            | _ -> false
        )


    /// Whether the solution rule can limit the component for some patient: its first solution limit of
    /// the component has a maximum quantity, a maximum quantity per kg or a list of quantities.
    let canLimitComponent (solRule: SolutionRule) name =
        solRule
        |> componentLimit name
        |> Option.exists (fun dl ->
            dl.Quantity.Max |> Option.isSome
            || dl.QuantityAdj.Max |> Option.isSome
            || dl.Quantities |> Option.isSome
        )


    /// Whether a solution rule that applies to the dose rule limits every component of it, with a dose
    /// count of at least 1.
    let anyLimitsDoseRule routeMapping (solutionRules: SolutionRule[]) (dr: DoseRule) =
        let names = dr.ComponentLimits |> Array.map _.Name

        solutionRules
        |> Array.exists (fun solRule ->
            solRule |> canMeetDoseRule routeMapping dr
            && solRule |> hasDoseCountOfAtLeastOne
            && names |> Array.forall (canLimitComponent solRule)
        )


    /// Whether no dose count in the range lets an order meet both the dose range and the range in the
    /// preparation: the dose minimum above the preparation maximum divided by the dose count minimum,
    /// or the dose maximum below the preparation minimum divided by the dose count maximum. An empty
    /// dose count range is a dose count of 1, as the order builder sets it. Ranges in different unit
    /// groups are not compared.
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


    /// The warnings for one solution rule and one dose rule: per component they both limit, whose
    /// dose range and range in the preparation no order can meet for any dose count of the solution
    /// rule. A quantity is compared with a quantity, a quantity per kg with a quantity per kg; the
    /// solution limit is the first of the component.
    let pairConflicts (solRule: SolutionRule) (dr: DoseRule) =
        let count = solRule |> doseCountRange
        let rangeText = MinMax.toString "min. " "min. " "max. " "max. "

        dr.ComponentLimits
        |> Array.toList
        |> List.choose (fun cl ->
            match cl.Limit, solRule |> componentLimit cl.Name with
            | Some doseLimit, Some solutionLimit -> Some(cl.Name, doseLimit, solutionLimit)
            | _ -> None
        )
        |> List.collect (fun (name, doseLimit, solutionLimit) ->
            [
                name, "quantity", doseLimit.Quantity, solutionLimit.Quantity
                name, "quantity per kg", doseLimit.QuantityAdjust, solutionLimit.QuantityAdj
            ]
        )
        |> List.filter (fun (_, _, dose, prep) -> noDoseCountMeets count dose prep)
        |> List.map (fun (name, kind, dose, prep) ->
            $"%s{solRule |> label} and %s{dr |> DoseRule.label}, component %s{name}: "
            + $"the dose %s{kind} %s{dose |> rangeText} and the %s{kind} in the preparation "
            + $"%s{prep |> rangeText} meet for no dose count"
        )


    /// One warning per dose rule and solution rule that apply to each other and can meet the same
    /// patient, and a component they both limit, whose dose range and range in the preparation no order
    /// can meet for any dose count of the solution rule.
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


    /// One warning per dose rule without limits that a solution rule applies to, while no solution
    /// rule limits every component of it, so loading leaves it out.
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


    /// One warning per pair of solution rules that apply to the same dose rule and can meet the
    /// same patient of that dose rule. Ranges that overlap pair by pair share a value, so comparing
    /// each pair of the three rules is enough. Each rule gives its own scenario, so the user has to choose between them, and
    /// a view that shows a single order shows none. The dose range of a solution rule does not
    /// take part in the matching, so rules that differ only in dose range also compete.
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


    /// The substances whose concentration limit allows no strength of the products. A strength
    /// in a unit group the limit cannot be compared with is skipped. The limit is inclusive: a limit
    /// with a minimum equal to the maximum allows that one strength.
    let substancesWithoutAllowedStrength (products: ProductComponent[]) (solRule: SolutionRule) =
        solRule.SolutionLimits
        |> Array.choose (fun solutionLimit ->
            match solutionLimit.SolutionLimitTarget with
            | SubstanceLimitTarget s when solutionLimit.Concentration |> MinMax.isEmpty |> not ->
                let conc = solutionLimit.Concentration

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


    /// A warning for each solution rule without diluents whose concentration limit allows no
    /// strength of the products of a dose rule it applies to. Only a dose rule with one component
    /// is checked, because a second component can dilute the first. The products are
    /// reconstituted for the location and the department of the solution rule. A solution rule
    /// and a dose rule without a department apply to every department, so the products are then
    /// reconstituted for each department their reconstitution rules name, and for none.
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
                    $"%s{solRule |> label}: no diluent, and the concentration limit of %s{names} allows no strength of its products"
                |> Some
        )
        |> Array.distinct
        |> Array.toList


    let get dataUrlId routeMapping (parenteral: ProductComponent[]) products : Result<_, Message list> =
        try
            dataUrlId |> getData |> Result.bind (map routeMapping parenteral products)
        with exn ->
            Result.createError "Error in SolutionRule.getResult: " exn


    /// The solution rule without the amounts the rule lookup works out from a per kg limit with the
    /// weight of the patient, so that one rule compares as equal at every weight.
    let withoutPatientAmounts (solRule: SolutionRule) =
        { solRule with
            Volume =
                if solRule.VolumeAdjust |> MinMax.isEmpty then
                    solRule.Volume
                else
                    MinMax.empty
            SolutionLimits =
                solRule.SolutionLimits
                |> Array.map (fun solutionLimit ->
                    if solutionLimit.QuantityAdj |> MinMax.isEmpty then
                        solutionLimit
                    else
                        { solutionLimit with Quantity = MinMax.empty }
                )
        }


    /// The solution rules that match the filter. A patient with no data besides the department is checked on
    /// the department alone, so that a lookup without a patient returns every rule.
    let filter mapping (filter: SolutionFilter) (solutionRules: SolutionRule[]) =
        let eqs a (b: string) =
            a |> Option.map (String.equalsCapInsens b) |> Option.defaultValue true

        [|
            fun (solRule: SolutionRule) -> solRule.Generic |> String.equalsCapInsens filter.Generic
            // a rule that names a department is for the patients of that department only
            if filter.Patient.Department |> Option.isSome then
                fun (solRule: SolutionRule) ->
                    filter.Patient.Department
                    |> PatientCategory.departmentMatches solRule.PatientCategory.Department
            // don't filter on the rest of the patient if it is not set; a department alone does not set it
            if filter.Patient |> Patient.hasDataBesidesDepartment then
                fun (solRule: SolutionRule) -> solRule.PatientCategory |> PatientCategory.filterPatient filter.Patient
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
        let printDoseFraction (solRule: SolutionRule) =
            let toPerc l =
                l
                |> Limit.getValueUnit
                |> ValueUnit.getValue
                |> Array.item 0
                |> BigRational.toDouble
                |> fun x -> $"{x * 100.}"

            let p =
                match solRule.DoseFraction.Min, solRule.DoseFraction.Max with
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
        let printSolutionLimit (solRule: SolutionRule) (includeSubstanceName: bool) (limit: SolutionLimit) =
            let mmToStr = MinMax.toString "min. " "min. " "max. " "max. "

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
                if solRule.Volume |> mmToStr |> String.isNullOrWhiteSpace then
                    ""
                else
                    solRule.Volume
                    |> mmToStr
                    |> fun s -> $""" in {s} {solRule.Diluents |> Array.map _.Generic |> String.concat "/"}"""
                |> fun s ->
                    if s |> String.notEmpty then
                        s
                    else
                        solRule.Volumes
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
                                        let rules_md =
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
                                                |> fun s -> $"{s}\n{rules_md}"
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
                |> Array.filter (fun solRule -> solRule.Generic = generic)
                |> Array.sortBy _.Generic
                |> toMarkdown ""
            )
