namespace Informedica.GenForm.Lib

module PrescriptionRule =


    open Informedica.GenCore.Lib.Ranges
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib

    /// The measurements the per kg and per m2 bounds of a dose limit need: a weight unit for a per kg
    /// bound, a BSA unit for a per m2 bound. A limit with an adjust unit and only absolute bounds needs
    /// none.
    let doseLimitMeasures (dl: DoseLimit) =
        [ dl.QuantityAdjust; dl.PerTimeAdjust; dl.RateAdjust ]
        |> List.choose RenalRule.measureOf


    /// Whether a prescription rule can be prescribed for the patient: the patient has every
    /// measurement, the weight or the BSA, that the per kg and per m2 bounds of its dose limits, its
    /// solution rules and its applied renal rule need.
    let isPrescribable pat (pr: PrescriptionRule) =
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
        |> List.forall (Some >> Patient.adjustValue pat >> Option.isSome)


    /// The dose limit adjusted to the patient, when the patient has the measurement its per kg or per m2
    /// bounds need; otherwise the dose limit as it is.
    let adjustDoseLimitToPatient freqs pat dl =
        match dl |> doseLimitMeasures |> List.tryHead |> Patient.adjustValue pat with
        | None -> dl
        | Some adj ->
            // The frequency that minimizes the per administration dose (highest
            // frequency), used to test whether an adjusted target is reachable
            // at all before pinning the absolute quantity.
            let maxFreq = freqs |> Option.bind ValueUnit.maxValue
            // The frequency that maximizes the per administration dose (lowest
            // frequency), used to test the absolute quantity floor.
            let minFreq = freqs |> Option.bind ValueUnit.minValue
            // recalculate the max dose per administration
            // if min adjust * adj >= max absolute, pin to max
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
            // if max adjust * adj <= min absolute, pin to min
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
            // recalculate the max dose per administration with the freq
            // if the adjusted per time target is unreachable even at the
            // highest frequency (min adjust * adj / maxFreq >= max absolute),
            // pin the quantity to its max and drop the now-unreachable
            // PerTimeAdjust target so the frequency is not over-constrained
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
            // if the adjusted per time max, even at the lowest frequency, stays
            // at or below the absolute min quantity (max adjust * adj / minFreq
            // <= min absolute), pin to min
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
            // recalculate the max dose per time
            // if min adjust * adj >= max absolute, pin to max
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
            // if max adjust * adj <= min absolute, pin to min
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
            // recalculate the max dose rate
            // if min adjust * adj >= max absolute, pin to max
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
            // if max adjust * adj <= min absolute, pin to min
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


    let adjustSolutionRuleToPatient (pat: Patient) (solRule: SolutionRule) =
        match pat.Weight with
        | None -> solRule
        | Some w ->
            let sl = solRule.SolutionLimit

            { solRule with
                SolutionLimit =
                    { sl with
                        Volume =
                            if sl.VolumeAdjust |> MinMax.isEmpty then
                                sl.Volume
                            else
                                [ sl.VolumeAdjust |> MinMax.apply ((*) w); sl.Volume ]
                                |> MinMax.foldMinimize true true
                    }
                DilutionLimits =
                    solRule.DilutionLimits
                    |> Array.map (fun dilutionLimit ->
                        if dilutionLimit.QuantityAdjust |> MinMax.isEmpty then
                            dilutionLimit
                        else
                            { dilutionLimit with
                                Quantity =
                                    [ dilutionLimit.QuantityAdjust |> MinMax.apply ((*) w); dilutionLimit.Quantity ]
                                    |> MinMax.foldMinimize true true
                            }
                    )
            }


    /// What is compared of a rule: the rule row, the renal rules and, when they count, the solution
    /// rules. A rule rewritten for the patient, to end a conflict between its limits, is the same
    /// rule, so the rewritten rule itself counts only when a renal rule applies: a renal band is a
    /// limit of its own. The patient of the rule differs between any two patients, so it is left out.
    let comparedRule solutionRules (pr: PrescriptionRule) =
        pr.DoseRule.Id,
        (if pr.RenalRules |> Array.isEmpty then
             None
         else
             Some pr.DoseRule),
        pr.RenalRules,
        match solutionRules with
        | SolutionRuleComparison.Counted -> pr.SolutionRules |> Array.map SolutionRule.withoutPatientAmounts
        | SolutionRuleComparison.Ignored -> [||]


    /// Whether the rules of the current patient are the rules of the patient at the context. No rule
    /// for the current patient means they are not.
    let sameRules solutionRules atContext current =
        (current |> Array.isEmpty |> not)
        && (atContext |> Array.map (comparedRule solutionRules)) = (current |> Array.map (comparedRule solutionRules))


    /// Whether a component is an additive: a product of the component is marked IsAdditive, by the
    /// lookup isAdditive on the GPK of the product.
    let isAdditiveComponent isAdditive (cl: ComponentLimit) = cl.Products |> Array.exists (_.GPK >> isAdditive)


    /// The components a prescriber can switch on and off: the additives after the first component.
    let optionalComponents isAdditive (cls: ComponentLimit[]) =
        match cls |> Array.toList with
        | [] -> [||]
        | _ :: rest ->
            rest
            |> List.filter (isAdditiveComponent isAdditive)
            |> List.map _.Name
            |> List.toArray


    /// The component limits an order keeps: all of them when no selection is made; otherwise every
    /// component that is not optional, and the selected optional components.
    let selectComponents isAdditive selection (cls: ComponentLimit[]) =
        match selection with
        | ComponentSelection.AllComponents -> cls
        | ComponentSelection.Selected selected ->
            let optional = cls |> optionalComponents isAdditive

            cls
            |> Array.filter (fun cl -> optional |> Array.contains cl.Name |> not || selected |> List.contains cl.Name)


    /// The names of the components of the dose rule without a limit or a substance limit that holds a
    /// value.
    let componentNamesWithoutLimits (dr: DoseRule) =
        dr.ComponentLimits
        |> Array.filter DoseRule.componentHasNoLimits
        |> Array.map _.Name


    /// Whether the solution rule, adjusted to the patient, limits the component: a dilution limit of
    /// the component with a maximum quantity or a list of quantities. A quantity per kg is in the
    /// quantity only when the patient has a weight. A minimum alone leaves the component unbounded.
    /// Only the first dilution limit of the component counts, since that is the one the order takes.
    let limitsComponent (solRule: SolutionRule) name =
        solRule
        |> SolutionRule.componentLimit name
        |> Option.exists (fun dl -> dl.Quantity.Max |> Option.isSome || dl.Quantities |> Option.isSome)


    /// The prescription rule with only the solution rules that limit every component without a limit,
    /// with a dose count of at least 1; none when no solution rule is left. Each solution rule gives
    /// its own scenario, so a solution rule that does not limit those components would give an order
    /// with a component without any limit. A rule whose components all have a limit is kept as it is.
    let withLimitingSolutionRules (pr: PrescriptionRule) =
        match pr.DoseRule |> componentNamesWithoutLimits with
        | [||] -> Some pr
        | names ->
            let limiting =
                pr.SolutionRules
                |> Array.filter (fun solRule ->
                    solRule |> SolutionRule.hasDoseCountOfAtLeastOne
                    && names |> Array.forall (limitsComponent solRule)
                )

            match limiting with
            | [||] -> None
            | solRules -> Some { pr with SolutionRules = solRules }


    /// The prescription rules for the filter; isAdditive decides the components a selection can leave
    /// out, see isAdditiveComponent.
    let filter
        isAdditive
        doseRules
        solutionRules
        renalRules
        routeMapping
        (filter: DoseFilter)
        : Result<PrescriptionRule array, Message list>
        =

        let warns = ResizeArray<string>()
        let pat = filter.Patient

        doseRules
        |> DoseRule.filter routeMapping filter
        |> Array.map (fun dr ->
            let dr, newWarns = dr |> DoseRule.reconstitute routeMapping pat.Location pat.Department

            warns.AddRange(newWarns)

            let filter =
                { filter with
                    Indication = dr.Indication |> Some
                    // RenalRule / SolutionRule sheets key on the base substance
                    // name, not the brand/form label, so match on genericName.
                    Generic = dr.Generic |> Generic.genericName |> Some
                    Form = dr.Generic.Form |> PharmaceuticalForm.toString |> Some
                    Route = dr.Route |> Some
                    DoseType = dr.DoseType |> Some
                }

            {
                Patient = pat
                DoseRule = dr
                SolutionRules =
                    let solutionFilter =
                        { Filter.solutionFilter (dr.Generic |> Generic.genericName) with
                            Patient = pat
                            Form = dr.Generic.Form |> PharmaceuticalForm.toString |> Some
                            Route = dr.Route |> Some
                            Indication = dr.Indication |> Some
                            Diluent = filter.Diluent
                            DoseType = dr.DoseType |> Some
                            Dose = None
                        }

                    solutionRules
                    // a diluent that is also a component of the dose rule would be in the order twice;
                    // checked before the filter narrows the diluents to the chosen one, so that choosing
                    // another diluent of the rule does not let the rule through
                    |> Array.filter (SolutionRule.componentDiluents dr >> Array.isEmpty)
                    |> SolutionRule.filter routeMapping solutionFilter
                    |> Array.map (fun solRule ->
                        { solRule with
                            DilutionLimits =
                                solRule.DilutionLimits
                                |> Array.map (fun dilutionLimit ->
                                    { dilutionLimit with
                                        Products =
                                            dilutionLimit.Products
                                            |> Array.filter (fun p ->
                                                dr.ComponentLimits
                                                |> Array.collect _.Products
                                                |> Array.exists (_.GPK >> (=) p.GPK)
                                            )

                                    }
                                )
                        }
                    )
                RenalRules = renalRules |> RenalRule.filter routeMapping filter
            }
        )
        |> Array.filter (fun pr ->
            // filter out the dose rules that do not have a dose type
            pr.DoseRule.DoseType <> DoseType.NoDoseType
            &&
            // also do filter out prescription rules for which
            // there are no products
            pr.DoseRule.ComponentLimits |> Array.collect _.Products |> Array.length > 0
        )
        // recalculate adjusted dose limits
        |> Array.map (fun pr ->
            // pass the full frequency set: adjustDoseLimitToPatient derives
            // both the highest frequency (reachability) and the lowest
            // frequency (floor) from it
            let freq = pr.DoseRule.Frequencies

            { pr with
                DoseRule =
                    { pr.DoseRule with
                        ComponentLimits =
                            pr.DoseRule.ComponentLimits
                            |> selectComponents isAdditive filter.Components

                            // applies to all targets?
                            // |> Array.filter DoseRule.DoseLimit.isSubstanceLimit
                            |> Array.map (fun dl ->
                                { dl with
                                    Limit = dl.Limit |> Option.map (adjustDoseLimitToPatient freq filter.Patient)
                                    SubstanceLimits =
                                        dl.SubstanceLimits |> Array.map (adjustDoseLimitToPatient freq filter.Patient)
                                }
                            )
                    }
                SolutionRules = pr.SolutionRules |> Array.map (adjustSolutionRuleToPatient filter.Patient)
            }
        )
        // Recalculate the dose rule according to a renal rules
        |> Array.collect (fun pr ->
            if pr.RenalRules |> Array.isEmpty then
                [| pr |]
            else
                pr.RenalRules
                |> Array.map (fun rr ->
                    { pr with
                        DoseRule = pr.DoseRule |> RenalRule.adjustDoseRule filter.Patient rr
                        RenalRules = [| rr |]
                    }
                )
        )
        // This step comes last, after the solution rules are adjusted to the patient. The renal rules
        // change only substance limits, so a component without a limit still has none after them.
        |> Array.choose withLimitingSolutionRules
        |> Ok


    /// Get all matching PrescriptionRules for a given Patient.
    let getForPatient isAdditive doseRules solutionRules renalRules routeMapping (pat: Patient) =
        Filter.doseFilter
        |> Filter.setPatient pat
        |> filter isAdditive doseRules solutionRules renalRules routeMapping


    /// Get the string representation of an array of PrescriptionRules. getLink is
    /// forwarded to DoseRule.Print.toMarkdown; pass Api.getNKFLinkProvider provider,
    /// or Source.noLinks when no external links are wanted.
    let toMarkdown (getLink: Source.LinkProvider) (prs: PrescriptionRule[]) =
        [
            yield!
                prs
                |> Array.collect (fun x ->
                    [|
                        [| x.DoseRule |] |> DoseRule.Print.toMarkdown getLink
                        x.SolutionRules |> SolutionRule.Print.toMarkdown "verdunnen"
                    |]
                )
        ]
        |> List.append [ prs[0].Patient |> Patient.toString ]
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
