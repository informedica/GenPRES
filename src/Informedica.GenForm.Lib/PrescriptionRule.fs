namespace Informedica.GenForm.Lib


module PrescriptionRule =

    open MathNet.Numerics
    open Informedica.GenUnits.Lib


    /// Use a Filter to get matching PrescriptionRules.
    let filter (filter : Filter) =
        let pat = filter.Patient

        DoseRule.get ()
        |> DoseRule.filter filter
        |> Array.map (fun dr ->
            let dr = dr |> DoseRule.reconstitute pat.Department pat.VenousAccess
            {
                Patient = pat
                DoseRule = dr
                SolutionRules =
                    SolutionRule.get ()
                    |> SolutionRule.filter
                        { filter with
                            Generic = dr.Generic |> Some
                            Shape = dr.Shape |> Some
                            Route = dr.Route |> Some
                            DoseType = dr.DoseType
                        }
            }
        )
        |> Array.filter (fun pr ->
            pr.DoseRule.Products |> Array.isEmpty |> not  &&
            pr.DoseRule.DoseType <> DoseType.Contraindicated
        )


    /// Get all matching PrescriptionRules for a given Patient.
    let get (pat : Patient) =
        Filter.filter
        |> Filter.setPatient pat
        |> filter


    /// Filter the Products in a PrescriptionRule to match the
    /// the given ShapeQuantities and Substances.
    let filterProducts
        shapeQuantities
        (substs : (string * BigRational option) array)
        (pr : PrescriptionRule) =
        { pr with
            DoseRule =
                { pr.DoseRule with
                    Products =
                        pr.DoseRule.Products
                        |> Array.filter (fun p ->
                            // TODO rewrite to compare valueunits
                            p.ShapeQuantities
                            |> ValueUnit.getValue
                            |> Array.exists (fun sq ->
                                shapeQuantities
                                |> Array.exists ((=) sq)
                            ) &&
                            p.Substances
                            // TODO rewrite to compare valueunits
                            |> Array.map (fun s ->
                                s.Name.ToLower(),
                                s.Quantity
                                |> Option.map ValueUnit.getValue
                                |> Option.bind Array.tryHead
                            )
                            |> Array.forall (fun sq ->
                                substs
                                |> Array.exists((=) sq)
                            )
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


    /// Get all DoseRules of an array of PrescriptionRules.
    let getDoseRules = Array.map getDoseRule


    /// Get all indications of an array of PrescriptionRules.
    let indications = getDoseRules >> DoseRule.indications


    /// Get all generics of an array of PrescriptionRules.
    let generics = getDoseRules >> DoseRule.generics


    /// Get all shapes of an array of PrescriptionRules.
    let shapes = getDoseRules >> DoseRule.shapes


    /// Get all routes of an array of PrescriptionRules.
    let routes = getDoseRules >> DoseRule.routes


    /// Get all departments of an array of PrescriptionRules.
    let departments = getDoseRules >> DoseRule.departments


    /// Get all diagnoses of an array of PrescriptionRules.
    let diagnoses= getDoseRules >> DoseRule.diagnoses


    /// Get all genders of an array of PrescriptionRules.
    let genders = getDoseRules >> DoseRule.genders


    /// Get all patients of an array of PrescriptionRules.
    let patients = getDoseRules >> DoseRule.patients


    /// Get all frequencies of an array of PrescriptionRules.
    let frequencies = getDoseRules >> DoseRule.frequencies




