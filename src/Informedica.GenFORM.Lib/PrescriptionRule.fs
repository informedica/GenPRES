namespace Informedica.GenForm.Lib

module PrescriptionRule =


    open Informedica.GenCore.Lib.Ranges
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib

    let adjustDoseLimitToPatient (freqs: ValueUnit option) (pat: Patient) (dl: DoseLimit) =
        if dl.AdjustUnit |> Option.isNone then
            dl
        else
            let adj =
                if dl.AdjustUnit.Value |> Units.eqsUnit Units.Weight.kiloGram then
                    pat.Weight
                else
                    pat |> Patient.calcBSA
                |> Option.get
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


    let adjustSolutionRuleToPatient (pat: Patient) (sr: SolutionRule) =
        match pat.Weight with
        | None -> sr
        | Some w ->
            { sr with
                Volume =
                    if sr.VolumeAdjust |> MinMax.isEmpty then
                        sr.Volume
                    else
                        [ sr.VolumeAdjust |> MinMax.apply ((*) w); sr.Volume ]
                        |> MinMax.foldMinimize true true
                SolutionLimits =
                    sr.SolutionLimits
                    |> Array.map (fun sl ->
                        if sl.QuantityAdj |> MinMax.isEmpty then
                            sl
                        else
                            { sl with
                                Quantity =
                                    [ sl.QuantityAdj |> MinMax.apply ((*) w); sl.Quantity ]
                                    |> MinMax.foldMinimize true true
                            }
                    )
            }


    /// Use a Filter to get matching PrescriptionRules.
    let filter
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
            let dr, newWarns =
                dr |> DoseRule.reconstitute routeMapping pat.Location pat.Department

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
                    let solFilter =
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
                    |> SolutionRule.filter routeMapping solFilter
                    |> Array.map (fun sr ->
                        { sr with
                            SolutionLimits =
                                sr.SolutionLimits
                                |> Array.map (fun sl ->
                                    { sl with
                                        Products =
                                            sl.Products
                                            |> Array.filter (fun sr_p ->
                                                dr.ComponentLimits
                                                |> Array.collect _.Products
                                                |> Array.exists (fun dr_p -> sr_p.GPK = dr_p.GPK)
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
            if filter.Patient.Weight |> Option.isNone || filter.Patient.Height |> Option.isNone then
                pr
            else
                // pass the full frequency set: adjustDoseLimitToPatient derives
                // both the highest frequency (reachability) and the lowest
                // frequency (floor) from it
                let freq = pr.DoseRule.Frequencies

                { pr with
                    DoseRule =
                        { pr.DoseRule with
                            ComponentLimits =
                                // component selection mechanism
                                if filter.Components |> List.isEmpty then
                                    pr.DoseRule.ComponentLimits
                                else
                                    match pr.DoseRule.ComponentLimits |> Array.tryHead with
                                    | None -> [||]
                                    | Some dl ->
                                        pr.DoseRule.ComponentLimits
                                        |> Array.tail
                                        |> Array.filter (fun dl -> filter.Components |> List.exists ((=) dl.Name))
                                        |> Array.append [| dl |]

                                // applies to all targets?
                                // |> Array.filter DoseRule.DoseLimit.isSubstanceLimit
                                |> Array.map (fun dl ->
                                    { dl with
                                        Limit = dl.Limit |> Option.map (adjustDoseLimitToPatient freq filter.Patient)
                                        SubstanceLimits =
                                            dl.SubstanceLimits
                                            |> Array.map (adjustDoseLimitToPatient freq filter.Patient)
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
                |> Array.map (fun rr -> { pr with DoseRule = pr.DoseRule |> RenalRule.adjustDoseRule rr })
        )
        |> Ok


    /// Get all matching PrescriptionRules for a given Patient.
    let getForPatient doseRules solutionRules renalRules routeMapping (pat: Patient) =
        Filter.doseFilter
        |> Filter.setPatient pat
        |> filter doseRules solutionRules renalRules routeMapping


    /// Filter the Products in a PrescriptionRule to match
    /// the given FormQuantities and Substances.
    let filterProducts (cmpItems: ComponentItem list) (pr: PrescriptionRule) =
        let eqs vu1 vu2 =
            if vu1 |> ValueUnit.eqsGroup vu2 |> not then
                false
            else
                vu1 |> ValueUnit.eqs vu2

        { pr with
            DoseRule =
                { pr.DoseRule with
                    ComponentLimits =
                        pr.DoseRule.ComponentLimits
                        |> Array.map (fun dl ->
                            { dl with
                                Products =
                                    dl.Products
                                    |> Array.filter (fun p ->
                                        let cmpItems =
                                            cmpItems |> List.filter (fun itm -> itm.ComponentName = p.Generic)

                                        cmpItems
                                        |> List.map _.ComponentQuantity
                                        |> List.exists (ValueUnit.eqs p.FormQuantities)
                                        && p.Substances
                                           |> Array.forall (fun subst ->
                                               cmpItems
                                               |> List.exists (fun itm ->
                                                   if itm.ItemName |> String.equalsCapInsens subst.Name |> not then
                                                       false
                                                   else
                                                       (subst.Concentration
                                                        |> Option.map (eqs itm.ItemConcentration)
                                                        |> Option.defaultValue false
                                                        || subst.MolarConcentration
                                                           |> Option.map (eqs itm.ItemConcentration)
                                                           |> Option.defaultValue false)
                                               )
                                           )
                                    )
                            }
                        )
                }
        }


    /// Get the string representation of an array of PrescriptionRules. `getLink` is
    /// forwarded to `DoseRule.Print.toMarkdown`; pass `Api.getNKFLinkProvider provider`,
    /// or `Source.noLinks` when no external links are wanted.
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
