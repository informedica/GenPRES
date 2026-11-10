namespace Informedica.GenForm.Lib


module DilutionRule =

    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib
    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges

    open Utils


    let fromTupleInclExcl = MinMax.fromTuple Inclusive Exclusive


    let fromTupleInclIncl = MinMax.fromTuple Inclusive Inclusive


    let parseDilutionRuleData (data: string[][]) : Result<_, Message list> =
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
                        // dilution rule section
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
                        // dilution limit section
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
            Result.createError "Error in DilutionRule.getResult: " exn


    let getData dataUrlId =
        Web.getDataFromSheet dataUrlId "DilutionRules" |> parseDilutionRuleData


    /// The Component of a DilutionRules row that limits the diluent of the rule, whichever diluent is
    /// chosen.
    let diluentComponent = "diluent"


    /// Whether a DilutionRules row is a diluent row: "diluent" in the Component column and no substance.
    let isDiluentRow (r: DilutionRuleData) =
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
                DilutionLimits = [||]
            }
        )
        |> Array.map (fun (dilRule, rs) ->
            { dilRule with
                DilutionLimits =
                    rs
                    |> expandDiluentRows (dilRule.Diluents |> Array.map _.Generic)
                    |> Array.map (fun l ->
                        let u = l.Unit |> UnitsParse.fromString
                        let au = u |> Option.map (ValueUnit.per Units.Weight.kiloGram)

                        {
                            DilutionLimitTarget =
                                match l.Substance, l.Component with
                                | s, _ when s |> String.notEmpty -> s |> SubstanceLimitTarget
                                | _, c when c |> String.notEmpty -> c |> ComponentLimitTarget
                                | _ -> invalidOp "Dilution limit should be either a substance or a component limit"
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
                                    && dilRule.Form
                                       |> Option.map (fun s -> s |> String.equalsCapInsens p.Form)
                                       |> Option.defaultValue true
                                    && p.Routes |> Array.exists (Mapping.eqsRoute routeMapping (Some dilRule.Route))
                                )
                        }
                    )
                    // filter out dilution limits that are empty
                    |> Array.filter (fun dilLimit ->
                        (dilLimit.Concentration |> MinMax.isEmpty
                         && dilLimit.Quantities |> Option.isNone
                         && dilLimit.QuantityAdj |> MinMax.isEmpty
                         && dilLimit.Quantity |> MinMax.isEmpty)
                        |> not
                    )

            }
        )
        |> Ok


    /// The rule as an admin finds it in the sheet: generic, form, route, indication,
    /// patient, which includes the department, and dose type.
    let label (dilRule: DilutionRule) =
        [
            dilRule.Generic
            dilRule.Form |> Option.defaultValue ""
            dilRule.Route
            dilRule.Indication |> Option.defaultValue ""
            dilRule.PatientCategory |> PatientCategory.toString
            dilRule.DoseType |> DoseType.toString
        ]
        |> List.filter String.notEmpty
        |> String.concat ", "
        |> sprintf "dilution rule %s"


    /// Every range a dilution rule carries, each with its name.
    let ranges (dilRule: DilutionRule) =
        [
            yield! dilRule.PatientCategory |> PatientCategory.ranges
            "dose", dilRule.Dose
            "volume", dilRule.Volume
            "volume adjust", dilRule.VolumeAdjust
            "drip rate", dilRule.DripRate
            "dose fraction", dilRule.DoseFraction
            for dilLimit in dilRule.DilutionLimits do
                let target = dilLimit.DilutionLimitTarget |> LimitTarget.toString
                $"%s{target} quantity", dilLimit.Quantity
                $"%s{target} quantity adjust", dilLimit.QuantityAdj
                $"%s{target} concentration", dilLimit.Concentration
        ]


    /// One warning per inverted range in the dilution rules.
    let invertedRanges (rules: DilutionRule[]) =
        rules
        |> Array.toList
        |> List.collect (fun dilRule ->
            dilRule
            |> ranges
            |> MinMax.invertedRanges
            |> List.map (fun s -> $"%s{dilRule |> label}: minimum above maximum, %s{s}")
        )
        |> List.distinct
        |> List.map Warning


    /// A limit, or nothing when the limit is 0, which counts as empty.
    let nonZeroLimit (lim: Limit option) =
        lim |> Option.filter (Limit.getValueUnit >> ValueUnit.isZero >> not)


    /// Whether a dilution limit bounds the preparation: the maximum quantity of a substance, or
    /// the maximum quantity per kg once the weight is known, divided by the minimum concentration
    /// of that substance is a maximum volume.
    let boundsPreparation (dilLimit: DilutionLimit) =
        (dilLimit.Quantity.Max.IsSome || dilLimit.QuantityAdj.Max.IsSome)
        && dilLimit.Concentration.Min
           |> Option.exists (Limit.getValueUnit >> ValueUnit.gtZero)


    /// One warning per dilution rule whose dose count has no maximum, because the rule gives a
    /// maximum dose fraction and no minimum, while the preparation has no maximum either: no
    /// maximum volume, no list of volumes, no maximum volume per kg, which bounds the preparation
    /// once the weight is known, and no dilution limit that bounds it. The user then cannot set
    /// the preparation.
    let openDoseCounts (rules: DilutionRule[]) =
        rules
        |> Array.filter (fun dilRule ->
            dilRule.DoseFraction.Max |> nonZeroLimit |> Option.isSome
            && dilRule.DoseFraction.Min |> nonZeroLimit |> Option.isNone
            && dilRule.Volume.Max |> Option.isNone
            && dilRule.Volumes |> Option.isNone
            && dilRule.VolumeAdjust.Max |> Option.isNone
            && dilRule.DilutionLimits |> Array.exists boundsPreparation |> not
        )
        |> Array.map (fun dilRule ->
            $"%s{dilRule |> label}: a maximum dose fraction without a minimum and no maximum volume, "
            + "so the preparation cannot be set"
        )
        |> Array.toList
        |> List.distinct
        |> List.map Warning


    /// One warning per dilution rule whose rows name solutions none of which matches a
    /// parenteral product, so the rule loads with no diluents. Each row is built into a rule
    /// on its own and the rules are grouped without their limits, which is the grouping map
    /// makes, so a rule of several rows gives a single warning.
    let missingDiluents parenteral (data: DilutionRuleData[]) =
        data
        |> Array.filter (fun r -> r.Diluents |> List.exists String.notEmpty)
        |> Array.choose (fun r ->
            match [| r |] |> map [||] parenteral [||] with
            | Ok [| dilRule |] when dilRule.Diluents |> Array.isEmpty ->
                Some({ dilRule with DilutionLimits = [||] }, r.Diluents)
            | _ -> None
        )
        |> Array.groupBy fst
        |> Array.map (fun (dilRule, xs) ->
            let solutions =
                xs
                |> Array.collect (snd >> List.toArray)
                |> Array.filter String.notEmpty
                |> Array.distinct
                |> String.concat "|"

            Warning $"%s{dilRule |> label}: no parenteral product matches the solutions %s{solutions}"
        )
        |> Array.toList


    /// One warning per dilution rule with a diluent row but no diluent, because its Diluents cell is
    /// empty or names no parenteral product. The diluent row then limits nothing.
    let diluentRowsWithoutDiluent parenteral (data: DilutionRuleData[]) =
        data
        |> Array.filter isDiluentRow
        |> Array.choose (fun r ->
            match [| r |] |> map [||] parenteral [||] with
            | Ok [| dilRule |] when dilRule.Diluents |> Array.isEmpty -> Some { dilRule with DilutionLimits = [||] }
            | _ -> None
        )
        |> Array.distinct
        |> Array.map (fun dilRule ->
            Warning $"%s{dilRule |> label}: a diluent row but no diluent, so the diluent row limits nothing"
        )
        |> Array.toList


    /// Whether a dilution rule applies to a dose rule: the generic and the route, and the form,
    /// indication and dose type when the dilution rule names them, matched as filter matches them
    /// for an order. A dilution rule and a dose rule that name different departments or different
    /// locations apply to different patients, so they do not match.
    let appliesToDoseRule routeMapping (dilRule: DilutionRule) (dr: DoseRule) =
        dilRule.Generic |> String.equalsCapInsens (dr.Generic |> Generic.genericName)
        && PatientCategory.canMeet dilRule.PatientCategory.Department dr.PatientCategory.Department
        && PatientCategory.canMeet dilRule.PatientCategory.Location dr.PatientCategory.Location
        && dilRule.Form
           |> Option.forall (String.equalsCapInsens (dr.Generic.Form |> PharmaceuticalForm.toString))
        && dilRule.Indication |> Option.forall (String.equalsCapInsens dr.Indication)
        && dilRule.Route |> Mapping.eqsRoute routeMapping (Some dr.Route)
        && (dilRule.DoseType = NoDoseType
            || if dilRule.DoseType |> DoseType.getText |> String.isNullOrWhiteSpace then
                   DoseType.eqsType dilRule.DoseType dr.DoseType
               else
                   DoseType.eqs dilRule.DoseType dr.DoseType)


    /// Whether a dilution rule applies to a dose rule and can meet a patient of that dose rule.
    let canMeetDoseRule routeMapping (dr: DoseRule) (dilRule: DilutionRule) =
        dr |> appliesToDoseRule routeMapping dilRule
        && PatientCategory.canShare dilRule.PatientCategory dr.PatientCategory


    /// One warning per pair of dilution rules that apply to the same dose rule and can meet the
    /// same patient of that dose rule. Ranges that overlap pair by pair share a value, so comparing
    /// each pair of the three rules is enough. Each rule gives its own scenario, so the user has to choose between them, and
    /// a view that shows a single order shows none. The dose range of a dilution rule does not
    /// take part in the matching, so rules that differ only in dose range also compete.
    let competingRules routeMapping (doseRules: DoseRule[]) (rules: DilutionRule[]) =
        let appliesToBoth a b dr =
            a |> canMeetDoseRule routeMapping dr && b |> canMeetDoseRule routeMapping dr

        let pairs (rules: DilutionRule[]) =
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


    /// The names of the diluents of the dilution rule that are also components of the dose rule. An
    /// order with such a pair would hold that component twice, so the pair is refused.
    let componentDiluents (dr: DoseRule) (dilRule: DilutionRule) =
        dilRule.Diluents
        |> Array.map _.Generic
        |> Array.filter (fun dil -> dr.ComponentLimits |> Array.exists (_.Name >> String.equalsCapInsens dil))


    /// One warning per dilution rule and dose rule that apply to each other and can meet the same patient,
    /// while a diluent of the dilution rule is also a component of the dose rule: the dilution rule is not
    /// applied to that dose rule.
    let diluentsThatAreComponents routeMapping (doseRules: DoseRule[]) (rules: DilutionRule[]) =
        [
            for dilRule in rules do
                for dr in doseRules do
                    if dilRule |> canMeetDoseRule routeMapping dr then
                        match componentDiluents dr dilRule with
                        | [||] -> ()
                        | dils ->
                            let names = dils |> String.concat ", "

                            $"%s{dilRule |> label}: the diluent %s{names} is also a component of a dose rule of "
                            + $"%s{dr.Generic |> Generic.toString}, so the dilution rule is not applied to that dose rule"
        ]
        |> List.distinct
        |> List.map Warning


    /// One warning per name in a Diluents cell whose parenteral Formulary product is not marked
    /// IsDiluent, listing the generics whose dilution rules name it. A name without a parenteral
    /// product is reported by missingDiluents.
    let unmarkedDiluents (formulary: FormularyProduct[]) (data: DilutionRuleData[]) =
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
    let substancesWithoutAllowedStrength (products: ProductComponent[]) (dilRule: DilutionRule) =
        dilRule.DilutionLimits
        |> Array.choose (fun dilLimit ->
            match dilLimit.DilutionLimitTarget with
            | SubstanceLimitTarget s when dilLimit.Concentration |> MinMax.isEmpty |> not ->
                let conc = dilLimit.Concentration

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


    /// A warning for each dilution rule without diluents whose concentration limit allows no
    /// strength of the products of a dose rule it applies to. Only a dose rule with one component
    /// is checked, because a second component can dilute the first. The products are
    /// reconstituted for the location and the department of the dilution rule. A dilution rule
    /// and a dose rule without a department apply to every department, so the products are then
    /// reconstituted for each department their reconstitution rules name, and for none.
    let undilutableRules routeMapping (doseRules: DoseRule[]) (rules: DilutionRule[]) =
        let substances (dilRule: DilutionRule) =
            doseRules
            |> Array.filter (fun dr ->
                dr.ComponentLimits |> Array.length = 1
                && dr |> appliesToDoseRule routeMapping dilRule
            )
            |> Array.collect (fun dr ->
                let departments =
                    match dilRule.PatientCategory.Department, dr.PatientCategory.Department with
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
                    let dr, _ = dr |> DoseRule.reconstitute routeMapping dilRule.PatientCategory.Location dep

                    dilRule
                    |> substancesWithoutAllowedStrength (dr.ComponentLimits |> Array.collect _.Products)
                )
            )
            |> Array.distinct

        rules
        |> Array.filter (fun dilRule -> dilRule.Diluents |> Array.isEmpty)
        |> Array.choose (fun dilRule ->
            match dilRule |> substances with
            | [||] -> None
            | subs ->
                let names = subs |> String.concat ", "

                Warning
                    $"%s{dilRule |> label}: no diluent, and the concentration limit of %s{names} allows no strength of its products"
                |> Some
        )
        |> Array.distinct
        |> Array.toList


    let get dataUrlId routeMapping (parenteral: ProductComponent[]) products : Result<_, Message list> =
        try
            dataUrlId |> getData |> Result.bind (map routeMapping parenteral products)
        with exn ->
            Result.createError "Error in DilutionRule.getResult: " exn


    /// The dilution rule without the amounts the rule lookup works out from a per kg limit with the
    /// weight of the patient, so that one rule compares as equal at every weight.
    let withoutPatientAmounts (dilRule: DilutionRule) =
        { dilRule with
            Volume =
                if dilRule.VolumeAdjust |> MinMax.isEmpty then
                    dilRule.Volume
                else
                    MinMax.empty
            DilutionLimits =
                dilRule.DilutionLimits
                |> Array.map (fun dilLimit ->
                    if dilLimit.QuantityAdj |> MinMax.isEmpty then
                        dilLimit
                    else
                        { dilLimit with Quantity = MinMax.empty }
                )
        }


    /// The dilution rules that match the filter. A patient with no data besides the department is checked on
    /// the department alone, so that a lookup without a patient returns every rule.
    let filter mapping (filter: DilutionFilter) (dilutionRules: DilutionRule[]) =
        let eqs a (b: string) =
            a |> Option.map (String.equalsCapInsens b) |> Option.defaultValue true

        [|
            fun (dilRule: DilutionRule) -> dilRule.Generic |> String.equalsCapInsens filter.Generic
            // a rule that names a department is for the patients of that department only
            if filter.Patient.Department |> Option.isSome then
                fun (dilRule: DilutionRule) ->
                    filter.Patient.Department
                    |> PatientCategory.departmentMatches dilRule.PatientCategory.Department
            // don't filter on the rest of the patient if it is not set; a department alone does not set it
            if filter.Patient |> Patient.hasDataBesidesDepartment then
                fun (dilRule: DilutionRule) -> dilRule.PatientCategory |> PatientCategory.filterPatient filter.Patient
            fun (dilRule: DilutionRule) -> dilRule.Form |> Option.map (eqs filter.Form) |> Option.defaultValue true
            fun (dilRule: DilutionRule) ->
                dilRule.Indication
                |> Option.map (eqs filter.Indication)
                |> Option.defaultValue true
            fun (dilRule: DilutionRule) ->
                filter.Route |> Option.isNone
                || dilRule.Route |> Mapping.eqsRoute mapping filter.Route
            fun (dilRule: DilutionRule) ->
                dilRule.DoseType = NoDoseType
                || filter.DoseType
                   |> Option.map (
                       if dilRule.DoseType |> DoseType.getText |> String.isNullOrWhiteSpace then
                           DoseType.eqsType dilRule.DoseType
                       else
                           DoseType.eqs dilRule.DoseType
                   )
                   |> Option.defaultValue true
        |]
        |> Array.fold (fun (acc: DilutionRule[]) pred -> acc |> Array.filter pred) dilutionRules
        |> Array.map (fun dilRule ->
            { dilRule with
                Diluents =
                    dilRule.Diluents
                    |> Array.filter (fun dil ->
                        filter.Diluent |> Option.map ((=) dil.Generic) |> Option.defaultValue true
                    )
            }
        )


    /// Helper function to get the distinct values of a member of DilutionRule.
    let private getMember getter (rules: DilutionRule[]) = rules |> Array.map getter |> Array.distinct |> Array.sort


    /// Get all the distinct Generics from the given DilutionRules.
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
        let printDoseFraction (dilRule: DilutionRule) =
            let toPerc l =
                l
                |> Limit.getValueUnit
                |> ValueUnit.getValue
                |> Array.item 0
                |> BigRational.toDouble
                |> fun x -> $"{x * 100.}"

            let p =
                match dilRule.DoseFraction.Min, dilRule.DoseFraction.Max with
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


        /// Split a DilutionLimit into its display parts so callers can de-duplicate
        /// trailing lines that are identical across multiple limits of the same rule.
        /// includeSubstanceName = false suppresses the inline substance prefix
        /// (used when the substance is rendered as a separate heading).
        let printDilutionLimit (dilRule: DilutionRule) (includeSubstanceName: bool) (limit: DilutionLimit) =
            let mmToStr = MinMax.toString "min. " "min. " "max. " "max. "

            let loc =
                match dilRule.PatientCategory.Access with
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
                if dilRule.Volume |> mmToStr |> String.isNullOrWhiteSpace then
                    ""
                else
                    dilRule.Volume
                    |> mmToStr
                    |> fun s -> $""" in {s} {dilRule.Diluents |> Array.map _.Generic |> String.concat "/"}"""
                |> fun s ->
                    if s |> String.notEmpty then
                        s
                    else
                        dilRule.Volumes
                        |> Option.map (Utils.ValueUnit.toString -1)
                        |> Option.defaultValue ""
                        |> fun s ->
                            let sols = dilRule.Diluents |> Array.map _.Generic |> String.concat "/"

                            if s |> String.isNullOrWhiteSpace then
                                if sols |> String.isNullOrWhiteSpace then
                                    " puur"
                                else
                                    $" in {sols}"
                            else
                                $" in {s} {sols}"

            let substancePrefix =
                if includeSubstanceName then
                    $"{limit.DilutionLimitTarget |> LimitTarget.toString}: "
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


        /// Get the markdown representation of the given DilutionRules.
        let toMarkdown text (rules: DilutionRule[]) =
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
                                                |> fun s -> $"{s}\n{sol}"
                                        |}
                                    )
                        )


            )
            |> _.md


        /// Get the markdown representation of the given DilutionRules.
        let printGenerics (rules: DilutionRule[]) =
            rules
            |> generics
            |> Array.map (fun generic ->
                rules
                |> Array.filter (fun dilRule -> dilRule.Generic = generic)
                |> Array.sortBy _.Generic
                |> toMarkdown ""
            )
