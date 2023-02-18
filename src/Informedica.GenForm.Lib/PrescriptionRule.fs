namespace Informedica.GenForm.Lib


module PrescriptionRule =

    open MathNet.Numerics


    let filter (filter : Filter) =
        let pat = filter |> Filter.getPatient

        DoseRule.get ()
        |> DoseRule.filter filter
        |> Array.map (fun dr ->
            let dr = dr |> DoseRule.reconstitute pat.Department pat.Location
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
                            Weight = pat.Weight
                            DoseType = dr.DoseType
                        }
            }
        )


    let get (pat : Patient) = 
        Filter.filter
        |> Filter.setPatient pat
        |> filter 


    let filterProducts shapeQuantities
                       (substs : (string * BigRational option) array)
                       (pr : PrescriptionRule) =
        { pr with
            DoseRule =
                { pr.DoseRule with
                    Products =
                        pr.DoseRule.Products
                        |> Array.filter (fun p ->
                            p.ShapeQuantities
                            |> Array.exists (fun sq ->
                                shapeQuantities
                                |> Array.exists ((=) sq)
                            ) &&
                            p.Substances
                            |> Array.map (fun s -> s.Name.ToLower(), s.Quantity)
                            |> Array.exists (fun sq ->
                                substs
                                |> Array.exists((=) sq)
                            )
                        )
                }
        }


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


    let getDoseRule (pr : PrescriptionRule) = pr.DoseRule


    let getDoseRules = Array.map getDoseRule


    let indications = getDoseRules >> DoseRule.indications


    let generics = getDoseRules >> DoseRule.generics


    let shapes = getDoseRules >> DoseRule.shapes


    let routes = getDoseRules >> DoseRule.routes


    let departments = getDoseRules >> DoseRule.departments


    let diagnoses= getDoseRules >> DoseRule.diagnoses


    let genders = getDoseRules >> DoseRule.genders


    let patients = getDoseRules >> DoseRule.patients


    let frequencies = getDoseRules >> DoseRule.frequencies

