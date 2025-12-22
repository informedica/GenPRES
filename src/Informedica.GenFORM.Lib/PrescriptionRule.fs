namespace Informedica.GenForm.Lib



module PrescriptionRule =


    open Informedica.GenCore.Lib.Ranges
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib

    open Utils


    let adjustDoseLimitToPatient (freq : ValueUnit option) (pat : Patient) (dl : DoseLimit) =
        if dl.AdjustUnit |> Option.isNone then dl
        else
            let adj =
                if dl.AdjustUnit.Value |> Units.eqsUnit Units.Weight.kiloGram then
                    pat.Weight
                else
                    pat |> Patient.calcBSA
                |> Option.get
            // recalculate the max dose per administration
            match dl.Quantity.Max |> Option.map Limit.getValueUnit,
                  dl.NormQuantityAdjust,
                  dl.QuantityAdjust.Min |> Option.map Limit.getValueUnit with
            | Some max, Some norm, _ ->
                let norm = norm * adj
                if norm <? max then dl
                else
                    { dl with
                        NormQuantityAdjust = None
                        Quantity.Min = dl.Quantity.Max
                    }
            | Some max, _, Some min ->
                let min = min * adj
                if min <? max then dl
                else
                    { dl with
                        QuantityAdjust = MinMax.empty
                        Quantity.Min = dl.Quantity.Max
                    }
            | _ -> dl
            // recalculate the max dose per administration with the freq
            |> fun dl ->
                match dl.Quantity.Max |> Option.map Limit.getValueUnit,
                      freq,
                      dl.NormPerTimeAdjust with
                | Some max, Some freq, Some norm ->
                    let norm = adj * norm / freq
                    if norm <? max then dl
                    else
                        { dl with
                            NormPerTimeAdjust = None
                            Quantity.Min = dl.Quantity.Max
                        }
                | _ -> dl
            // recalculate the max dose per time
            |> fun dl ->

                match dl.PerTime.Max |> Option.map Limit.getValueUnit,
                      dl.NormPerTimeAdjust,
                      dl.PerTimeAdjust.Min |> Option.map Limit.getValueUnit with
                | Some max, Some norm, _ ->
                    let norm = norm * adj
                    if norm <? max then dl
                    else
                        { dl with
                            NormPerTimeAdjust = None
                            PerTime.Min = dl.PerTime.Max
                        }
                | Some max, _, Some min ->
                    let min = min * adj
                    if min <? max then dl
                    else
                        { dl with
                            PerTimeAdjust = MinMax.empty
                            PerTime.Min = dl.PerTime.Max
                        }
                | _ -> dl
            // recalculate the max dose rate
            |> fun dl ->
                match dl.Rate.Max |> Option.map Limit.getValueUnit,
                      dl.RateAdjust.Min |> Option.map Limit.getValueUnit with
                | Some max, Some min ->
                    let min = min * adj
                    if min <? max then dl
                    else
                        { dl with
                            RateAdjust = MinMax.empty
                            Rate.Min = dl.Rate.Max
                        }
                | _ -> dl


    /// Use a Filter to get matching PrescriptionRules.
    let filter
        doseRules
        solutionRules
        renalRules
        routeMapping
        (filter : DoseFilter) : GenFormResult<_> =

        let warns = ResizeArray<string>()
        let pat = filter.Patient

        doseRules
        |> DoseRule.filter routeMapping filter
        |> Array.map (fun dr ->
            let dr, newWarns =
                dr
                |> DoseRule.reconstitute
                       routeMapping
                       pat.Department pat.Access

            warns.AddRange(newWarns)

            let filter =
                { filter with
                    Indication = dr.Indication |> Some
                    Generic = dr.Generic |> Some
                    Form = dr.Form |> Some
                    Route = dr.Route |> Some
                    DoseType = dr.DoseType |> Some
                }

            {
                Patient = pat
                DoseRule = dr
                SolutionRules =
                    let solFilter =
                        { Filter.solutionFilter dr.Generic with
                            Patient = pat
                            Form = dr.Form |> Some
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
                            Products =
                                sr.Products
                                |> Array.filter (fun sr_p ->
                                    dr.ComponentLimits
                                    |> Array.collect _.Products
                                    |> Array.exists (fun dr_p ->
                                        sr_p.GPK = dr_p.GPK
                                    )
                                )
                        }
                    )
                RenalRules =
                    renalRules
                    |> RenalRule.filter routeMapping filter
            }
        )
        |> Array.filter (fun pr ->
            // filter out the dose rules that do not have a dose type
            pr.DoseRule.DoseType <> DoseType.NoDoseType &&
            // also do filter out prescription rules for which
            // there are no products
            pr.DoseRule.ComponentLimits
            |> Array.collect _.Products
            |> Array.length > 0
        )
        // recalculate adjusted dose limits
        |> Array.map (fun pr ->
            if filter.Patient.Weight |> Option.isNone ||
               filter.Patient.Height |> Option.isNone then pr
            else
                let freq =
                    pr.DoseRule.Frequencies
                    |> Option.map (fun vu ->
                        let u = vu |> ValueUnit.getUnit
                        vu
                        |> ValueUnit.getValue
                        |> Array.min
                        |> ValueUnit.singleWithUnit u
                    )
                { pr with
                    DoseRule =
                        { pr.DoseRule with
                            ComponentLimits =
                                // component selection mechanism
                                if filter.Components |> List.isEmpty then pr.DoseRule.ComponentLimits
                                else
                                    match pr.DoseRule.ComponentLimits |> Array.tryHead with
                                    | None -> [||]
                                    | Some dl ->
                                        pr.DoseRule.ComponentLimits
                                        |> Array.tail
                                        |> Array.filter (fun dl ->
                                            filter.Components
                                            |> List.exists ((=) dl.Name)
                                        )
                                        |> Array.append [| dl |]

                                // applies to all targets?
                                // |> Array.filter DoseRule.DoseLimit.isSubstanceLimit
                                |> Array.map(fun dl ->
                                    { dl with
                                        Limit = dl.Limit |> Option.map (adjustDoseLimitToPatient freq filter.Patient)
                                        SubstanceLimits =
                                            dl.SubstanceLimits
                                            |> Array.map (adjustDoseLimitToPatient freq filter.Patient)
                                    }
                                )
                    }
                }
        )
        // Recalculate the dose rule according to a renal rules
        |> Array.collect (fun pr ->
            if pr.RenalRules |> Array.isEmpty then [| pr |]
            else
                pr.RenalRules
                |> Array.map (fun rr ->
                    { pr with
                        DoseRule =
                            pr.DoseRule
                            |> RenalRule.adjustDoseRule rr
                    }
                )
        )
        |> fun prs ->
            warns
            |> Seq.distinct
            |> Seq.sort
            |> Seq.toList
            |> List.map Warning
            |> GenFormResult.createOk prs


    /// Get all matching PrescriptionRules for a given Patient.
    let getForPatient dataUrlId doseRules solutionRules routeMapping (pat : Patient) =
        Filter.doseFilter
        |> Filter.setPatient pat
        |> filter dataUrlId doseRules solutionRules routeMapping


    /// Filter the Products in a PrescriptionRule to match
    /// the given FormQuantities and Substances.
    let filterProducts
        (cmpItems: ComponentItem list)
        (pr : PrescriptionRule) =
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
                                            cmpItems
                                            |> List.filter (fun itm -> itm.ComponentName = p.Generic)

                                        cmpItems
                                        |> List.map _.ComponentQuantity
                                        |> List.exists (ValueUnit.eqs p.FormQuantities)
                                        &&
                                        p.Substances
                                        |> Array.forall (fun subst ->
                                            cmpItems
                                            |> List.exists(fun itm ->
                                                itm.ItemName |> String.equalsCapInsens subst.Name &&
                                                (subst.Concentration
                                                |> Option.map (ValueUnit.eqs itm.ItemConcentration)
                                                |> Option.defaultValue false ||
                                                subst.MolarConcentration
                                                |> Option.map (ValueUnit.eqs itm.ItemConcentration)
                                                |> Option.defaultValue false)

                                            )
                                        )
                                    )
                            }
                        )
                }
        }


    /// Get the string representation of an array of PrescriptionRules.
    let toMarkdown (prs : PrescriptionRule []) =
        [
            yield!
                prs
                |> Array.collect (fun x ->
                    [|
                        [| x.DoseRule |] |> DoseRule.Print.toMarkdown
                        x.SolutionRules |> SolutionRule.Print.toMarkdown "verdunnen"
                    |]
                )
        ]
        |> List.append [ prs[0].Patient |> Patient.toString ]
        |> String.concat "\n"


    /// Get the DoseRule of a PrescriptionRule.
    let getDoseRule (pr : PrescriptionRule) = pr.DoseRule


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


    let diluents (prs : PrescriptionRule []) =
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