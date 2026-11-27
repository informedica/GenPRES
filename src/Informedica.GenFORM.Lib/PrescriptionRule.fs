namespace Informedica.GenForm.Lib

module PrescriptionRule =


    open Informedica.GenCore.Lib.Ranges
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib

    /// Returns the measurement units that the per kg and per m2 bounds of a dose limit need: a weight unit
    /// for a per kg bound and a BSA unit for a per m2 bound. A limit with only absolute bounds needs none.
    let doseLimitMeasures (dl: DoseLimit) =
        [ dl.QuantityAdjust; dl.PerTimeAdjust; dl.RateAdjust ]
        |> List.choose RenalRule.measureOf


    /// Returns true when the patient has every measurement, the weight or the BSA, that the per kg and per m2
    /// bounds of the dose limits, the solution rules and the renal rules of the prescription rule need.
    let isPrescribable patientData (pr: PrescriptionRule) =
        [
            for dl in pr.DoseRule |> DoseRule.allLimits do
                yield! dl |> doseLimitMeasures
            for solRule in pr.SolutionRules do
                yield!
                    solRule.SolutionLimit.VolumeAdjust
                    :: (solRule.DilutionLimits |> Array.toList |> List.map _.QuantityAdjust)
                    |> List.choose RenalRule.measureOf
            for rl in pr.RenalRules |> Array.collect _.RenalLimits do
                yield! rl |> RenalRule.limitMeasures
        ]
        |> List.forall (Some >> PatientData.adjustValue patientData >> Option.isSome)


    /// Resolves a conflict between the adjusted bounds of a dose limit and its absolute bounds. An adjusted
    /// bound is a per kg or per m2 bound times the weight or the BSA of the patient. When an adjusted bound
    /// cannot be met within an absolute bound, the dose is fixed at that absolute bound and the per kg or
    /// per m2 limit is dropped. Returns the dose limit unchanged when the patient lacks the measurement.
    let adjustDoseLimitToPatient freqs patientData dl =
        match dl |> doseLimitMeasures |> List.tryHead |> PatientData.adjustValue patientData with
        | None -> dl
        | Some adj ->
            // The highest frequency gives the lowest dose per administration for a per time limit.
            let maxFreq = freqs |> Option.bind ValueUnit.maxValue
            // The lowest frequency gives the highest dose per administration for a per time limit.
            let minFreq = freqs |> Option.bind ValueUnit.minValue
            // Fixes the quantity at its maximum when the adjusted minimum quantity is at or above it.
            match
                dl.Quantity.Max |> Option.map Limit.getValueUnit, dl.QuantityAdjust.Min |> Option.map Limit.getValueUnit
            with
            | Some max, Some min ->
                let min = min * adj

                if min <? max then
                    dl
                else
                    { dl with
                        QuantityAdjust = MinMax.empty
                        Quantity.Min = dl.Quantity.Max
                    }
            | _ -> dl
            // Fixes the quantity at its minimum when the adjusted maximum quantity is at or below it.
            |> fun dl ->
                match
                    dl.Quantity.Min |> Option.map Limit.getValueUnit,
                    dl.QuantityAdjust.Max |> Option.map Limit.getValueUnit
                with
                | Some min, Some max ->
                    let max = max * adj

                    if max >? min then
                        dl
                    else
                        { dl with
                            QuantityAdjust = MinMax.empty
                            Quantity.Max = dl.Quantity.Min
                        }
                | _ -> dl
            // Fixes the quantity at its maximum when the adjusted minimum per time, divided by the highest
            // frequency, is at or above it. The per time limit is dropped, so the frequency stays free.
            |> fun dl ->
                match
                    dl.Quantity.Max |> Option.map Limit.getValueUnit,
                    maxFreq,
                    dl.PerTimeAdjust.Min |> Option.map Limit.getValueUnit
                with
                | Some max, Some freq, Some min ->
                    let norm = adj * min / freq

                    if norm <? max then
                        dl
                    else
                        { dl with
                            PerTimeAdjust = MinMax.empty
                            Quantity.Min = dl.Quantity.Max
                        }
                | _ -> dl
            // Fixes the quantity at its minimum when the adjusted maximum per time, divided by the lowest
            // frequency, is at or below it.
            |> fun dl ->
                match
                    dl.Quantity.Min |> Option.map Limit.getValueUnit,
                    minFreq,
                    dl.PerTimeAdjust.Max |> Option.map Limit.getValueUnit
                with
                | Some min, Some freq, Some max ->
                    let norm = adj * max / freq

                    if norm >? min then
                        dl
                    else
                        { dl with
                            PerTimeAdjust = MinMax.empty
                            Quantity.Max = dl.Quantity.Min
                        }
                | _ -> dl
            // Fixes the dose per time at its maximum when the adjusted minimum per time is at or above it.
            |> fun dl ->
                match
                    dl.PerTime.Max |> Option.map Limit.getValueUnit,
                    dl.PerTimeAdjust.Min |> Option.map Limit.getValueUnit
                with
                | Some max, Some min ->
                    let min = min * adj

                    if min <? max then
                        dl
                    else
                        { dl with
                            PerTimeAdjust = MinMax.empty
                            PerTime.Min = dl.PerTime.Max
                        }
                | _ -> dl
            // Fixes the dose per time at its minimum when the adjusted maximum per time is at or below it.
            |> fun dl ->
                match
                    dl.PerTime.Min |> Option.map Limit.getValueUnit,
                    dl.PerTimeAdjust.Max |> Option.map Limit.getValueUnit
                with
                | Some min, Some max ->
                    let max = max * adj

                    if max >? min then
                        dl
                    else
                        { dl with
                            PerTimeAdjust = MinMax.empty
                            PerTime.Max = dl.PerTime.Min
                        }
                | _ -> dl
            // Fixes the rate at its maximum when the adjusted minimum rate is at or above it.
            |> fun dl ->
                match
                    dl.Rate.Max |> Option.map Limit.getValueUnit, dl.RateAdjust.Min |> Option.map Limit.getValueUnit
                with
                | Some max, Some min ->
                    let min = min * adj

                    if min <? max then
                        dl
                    else
                        { dl with
                            RateAdjust = MinMax.empty
                            Rate.Min = dl.Rate.Max
                        }
                | _ -> dl
            // Fixes the rate at its minimum when the adjusted maximum rate is at or below it.
            |> fun dl ->
                match
                    dl.Rate.Min |> Option.map Limit.getValueUnit, dl.RateAdjust.Max |> Option.map Limit.getValueUnit
                with
                | Some min, Some max ->
                    let max = max * adj

                    if max >? min then
                        dl
                    else
                        { dl with
                            RateAdjust = MinMax.empty
                            Rate.Max = dl.Rate.Min
                        }
                | _ -> dl


    /// Returns the parts of a prescription rule that are compared: the dose rule id, the products of each
    /// component, the renal rules and, when solutionRules is Counted, the solution rules. The products are
    /// compared because the patient selects the reconstitution of a product. The dose rule itself is
    /// compared only when a renal rule applies, because a renal rule changes the limits. A dose rule changed
    /// for a conflict between its limits is still the same rule. The patient is left out, since it differs
    /// between any two patients.
    let comparedRule solutionRules (pr: PrescriptionRule) =
        pr.DoseRule.Id,
        pr.DoseRule.ComponentLimits |> Array.map _.Products,
        (if pr.RenalRules |> Array.isEmpty then
             None
         else
             Some pr.DoseRule),
        pr.RenalRules,
        match solutionRules with
        | SolutionRuleComparison.Counted -> pr.SolutionRules
        | SolutionRuleComparison.Ignored -> [||]


    /// Returns true when the rules for the current patient equal the rules at the context. Returns false
    /// when there are no rules for the current patient.
    let sameRules solutionRules atContext current =
        (current |> Array.isEmpty |> not)
        && (atContext |> Array.map (comparedRule solutionRules)) = (current |> Array.map (comparedRule solutionRules))


    /// Returns true when isAdditive is true for the GPK of a product of the component.
    let isAdditiveComponent isAdditive (cl: ComponentLimit) = cl.Products |> Array.exists (_.GPK >> isAdditive)


    /// Returns the names of the optional components: the additives after the first component.
    let optionalComponents isAdditive (cls: ComponentLimit[]) =
        match cls |> Array.toList with
        | [] -> [||]
        | _ :: rest ->
            rest
            |> List.filter (isAdditiveComponent isAdditive)
            |> List.map _.Name
            |> List.toArray


    /// Returns all component limits for AllComponents. For a selection, returns the components that are not
    /// optional and the selected optional components.
    let selectComponents isAdditive selection (cls: ComponentLimit[]) =
        match selection with
        | ComponentSelection.AllComponents -> cls
        | ComponentSelection.Selected selected ->
            let optional = cls |> optionalComponents isAdditive

            cls
            |> Array.filter (fun cl -> optional |> Array.contains cl.Name |> not || selected |> List.contains cl.Name)


    /// Returns the names of the components of the dose rule where neither the component limit nor any
    /// substance limit has a value.
    let componentNamesWithoutLimits (dr: DoseRule) =
        dr.ComponentLimits
        |> Array.filter DoseRule.componentHasNoLimits
        |> Array.map _.Name


    /// Returns true when the first dilution limit of the component has a maximum quantity or a list of
    /// quantities. When the patient has the value for the adjust unit of the solution rule, a maximum quantity
    /// per adjust unit also counts. The order uses only that dilution limit. A minimum alone does not limit the
    /// component.
    let limitsComponent patientData (solRule: SolutionRule) name =
        match solRule.AdjustUnit |> PatientData.adjustValue patientData with
        | Some _ -> SolutionRule.canLimitComponent solRule name
        | None ->
            solRule
            |> SolutionRule.componentLimit name
            |> Option.exists (fun dl -> dl.Quantity.Max |> Option.isSome || dl.Quantities |> Option.isSome)


    /// Keeps the solution rules with a dose count of at least 1 that limit every component without a limit,
    /// for the patient of the prescription rule. Returns None when no solution rule is left, and the rule
    /// unchanged when all components have a limit. Each solution rule gives its own scenario, so any other
    /// solution rule would give an order with a component without any limit.
    let withLimitingSolutionRules (pr: PrescriptionRule) =
        match pr.DoseRule |> componentNamesWithoutLimits with
        | [||] -> Some pr
        | names ->
            let limiting =
                pr.SolutionRules
                |> Array.filter (fun solRule ->
                    solRule |> SolutionRule.hasDoseCountOfAtLeastOne
                    && names |> Array.forall (limitsComponent pr.PatientData solRule)
                )

            match limiting with
            | [||] -> None
            | solRules -> Some { pr with SolutionRules = solRules }


    /// Returns a lookup of the solution rules by generic. The generic names are compared as
    /// String.equalsCapInsens compares them, in lower case and trimmed; a generic without solution rules gets
    /// an empty array.
    let solutionRulesByGeneric (solutionRules: SolutionRule[]) =
        let normalise = String.toLower >> String.trim
        // a dict, not a Map: this lookup runs once per dose rule, and a Map lookup measured about 5% slower
        // per request
        let byGeneric = solutionRules |> Array.groupBy (_.Generic >> normalise) |> dict

        fun generic ->
            match byGeneric.TryGetValue(generic |> normalise) with
            | true, srs -> srs
            | false, _ -> [||]


    /// Returns the prescription rules for the filter. isAdditive finds the additives, the components that a
    /// component selection can leave out. The products of a dose rule are reconstituted and then pre-diluted
    /// with the predilutions.
    let filter
        isAdditive
        doseRules
        solutionRules
        renalRules
        predilutions
        routeMapping
        (filter: DoseFilter)
        : Result<PrescriptionRule array, Message list>
        =

        let warns = ResizeArray<string>()
        let pat = filter.PatientData

        // Each dose rule visits only the solution rules of its own generic; SolutionRule.filter checks the
        // generic again, which these rules all pass.
        let solutionRulesFor = solutionRules |> solutionRulesByGeneric
        let renalRules = renalRules |> RenalRule.forPatient pat

        doseRules
        |> DoseRule.filter routeMapping filter
        |> Array.map (fun dr ->
            let dep = pat |> PatientData.department

            let dr, newWarns =
                dr
                |> DoseRule.reconstitute routeMapping pat.Location dep (Product.Reconstitution.matchesPatient pat)

            warns.AddRange(newWarns)

            // a reconstituted product is a liquid, so it can be pre-diluted after the reconstitution
            let dr =
                dr
                |> DoseRule.predilute
                    routeMapping
                    pat.Location
                    dep
                    (Product.Predilution.matchesPatient pat)
                    predilutions

            // The solution rules are matched on the generic name, without a brand or a form.
            let generic = dr.Generic |> Generic.genericName
            let products = dr.ComponentLimits |> Array.collect _.Products

            {
                PatientData = pat
                DoseRule = dr
                SolutionRules =
                    let solutionFilter =
                        { Filter.solutionFilter with
                            Generic = Some generic
                            PatientData = pat
                            Form = dr.Generic.Form |> PharmaceuticalForm.toString |> Some
                            Route = dr.Route |> Some
                            Indication = dr.Indication |> Some
                            Diluent = filter.Diluent
                            DoseType = dr.DoseType |> Some
                            Dose = None
                        }

                    generic
                    |> solutionRulesFor
                    // A diluent that is also a component of the dose rule would be in the order twice. This
                    // check runs before the diluents are narrowed to the chosen one, so that choosing another
                    // diluent of the rule does not let the rule through.
                    |> Array.filter (SolutionRule.componentDiluents dr >> Array.isEmpty)
                    // A dose rule is made for each product form, so a solution rule for the products of one
                    // form does not apply to the dose rule of another form.
                    |> Array.filter (SolutionRule.appliesToProducts products)
                    |> SolutionRule.filter routeMapping solutionFilter
                    |> Array.map (fun solRule ->
                        { solRule with
                            DilutionLimits =
                                solRule.DilutionLimits
                                |> Array.map (fun dilutionLimit ->
                                    { dilutionLimit with
                                        Products =
                                            dilutionLimit.Products
                                            |> Array.filter (fun p -> products |> Array.exists (_.GPK >> (=) p.GPK))

                                    }
                                )
                        }
                    )
                RenalRules =
                    renalRules
                    |> Array.filter (fun rr -> RenalRule.appliesToDoseRule routeMapping rr dr)
            }
        )
        |> Array.filter (fun pr ->
            // filter out the dose rules that do not have a dose type
            pr.DoseRule.DoseType <> DoseType.NoDoseType
            &&
            // and the prescription rules without products
            pr.DoseRule.ComponentLimits |> Array.collect _.Products |> Array.length > 0
        )
        // Adjusts the dose limits to the patient.
        |> Array.map (fun pr ->
            // adjustDoseLimitToPatient takes the highest and the lowest frequency from the full set.
            let freq = pr.DoseRule.Frequencies

            { pr with
                DoseRule =
                    { pr.DoseRule with
                        ComponentLimits =
                            pr.DoseRule.ComponentLimits
                            |> selectComponents isAdditive filter.Components
                            |> Array.map (fun dl ->
                                { dl with
                                    Limit = dl.Limit |> Option.map (adjustDoseLimitToPatient freq filter.PatientData)
                                    SubstanceLimits =
                                        dl.SubstanceLimits
                                        |> Array.map (adjustDoseLimitToPatient freq filter.PatientData)
                                }
                            )
                    }
            }
        )
        // Gives one prescription rule per renal rule, with the dose rule adjusted by that renal rule.
        |> Array.collect (fun pr ->
            if pr.RenalRules |> Array.isEmpty then
                [| pr |]
            else
                pr.RenalRules
                |> Array.map (fun rr ->
                    { pr with
                        DoseRule = pr.DoseRule |> RenalRule.adjustDoseRule filter.PatientData rr
                        RenalRules = [| rr |]
                    }
                )
        )
        // A renal rule can give a dose rule without an adjust unit one, so the solution rules are matched to the
        // adjust unit of the dose rule after the renal rules.
        |> Array.map (fun pr -> { pr with SolutionRules = pr.SolutionRules |> SolutionRule.forDoseRule pr.DoseRule })
        // This step comes last, after the renal rules. The renal rules change only substance limits, so a
        // component without a limit still has none after them.
        |> Array.choose withLimitingSolutionRules
        |> Ok


    /// Get all matching PrescriptionRules for a given Patient.
    let getForPatient
        isAdditive
        doseRules
        solutionRules
        renalRules
        predilutions
        routeMapping
        (patientData: PatientData)
        =
        Filter.doseFilter
        |> Filter.setPatientData patientData
        |> filter isAdditive doseRules solutionRules renalRules predilutions routeMapping


    /// Returns the prescription rules as markdown, after the patient data of the first rule. getLink makes
    /// the source links; Source.noLinks makes none. An empty array raises an exception.
    let toMarkdown (getLink: Source.LinkProvider) (prs: PrescriptionRule[]) =
        [
            yield!
                prs
                |> Array.collect (fun x ->
                    [|
                        [| x.DoseRule |] |> DoseRule.Print.toMarkdown getLink
                        x.SolutionRules |> SolutionRule.Print.toMarkdown "verdunnen" [||]
                    |]
                )
        ]
        |> List.append [ prs[0].PatientData |> PatientData.toString ]
        |> String.concat "\n"


    /// Get the DoseRule of a PrescriptionRule.
    let getDoseRule (pr: PrescriptionRule) = pr.DoseRule


    let getSolutionRules (pr: PrescriptionRule) = pr.SolutionRules


    /// Get all DoseRules of an array of PrescriptionRules.
    let getDoseRules = Array.map getDoseRule


    let collectSolutionRules = Array.collect getSolutionRules


    /// Get all indications of an array of PrescriptionRules.
    let indications = getDoseRules >> DoseRule.indications


    /// Get all generics of an array of PrescriptionRules.
    let generics = getDoseRules >> DoseRule.generics


    /// Get all pharmaceutical forms of an array of PrescriptionRules.
    let forms = getDoseRules >> DoseRule.forms


    /// Get all routes of an array of PrescriptionRules.
    let routes = getDoseRules >> DoseRule.routes


    let doseTypes = getDoseRules >> DoseRule.doseTypes


    let diluents (prs: PrescriptionRule[]) =
        prs
        |> Array.collect _.SolutionRules
        |> Array.collect _.Diluents
        |> Array.distinct


    /// Get all departments of an array of PrescriptionRules.
    let departments = getDoseRules >> DoseRule.departments


    /// Get all genders of an array of PrescriptionRules.
    let genders = getDoseRules >> DoseRule.genders


    /// Get all patients of an array of PrescriptionRules.
    let patients = getDoseRules >> DoseRule.patientCategories


    /// Get all frequencies of an array of PrescriptionRules.
    let frequencies = getDoseRules >> DoseRule.frequencies


    /// The filter options with no values.
    let emptyFilterOptions =
        {
            Indications = [||]
            Generics = [||]
            Routes = [||]
            Forms = [||]
            DoseTypes = [||]
        }


    /// The filter options of an array of PrescriptionRules.
    let filterOptions prs =
        {
            Indications = prs |> indications
            Generics = prs |> generics
            Routes = prs |> routes
            Forms = prs |> forms
            DoseTypes = prs |> doseTypes
        }
