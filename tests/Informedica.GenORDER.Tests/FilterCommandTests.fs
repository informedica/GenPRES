/// The filter of the order context: a choice is written, an emptied field lets go of the choices
/// below it, and the scenarios go; a command picks a field's option by its index.
module FilterCommandTests

open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip

open FilterFixtures


[<Tests>]
let tests =
    testList
        "OrderContext filter"
        [
            test "a choice keeps the options" {
                context options
                |> OrderContext.change OrderCategory.Medication FilterField.Route (Some "RECTAAL") None
                |> fun c -> c.Filter.Route, c.Filter.Routes
                |> Expect.equal "rectal chosen, all routes kept" (Some "RECTAAL", options.Routes)
            }

            test "a filter change lets go of the scenarios" {
                context options
                |> OrderContext.change OrderCategory.Medication FilterField.Route (Some "ORAAL") None
                |> _.Scenarios.Length
                |> Expect.equal "no scenarios" 0
            }

            test "a choice the field already holds changes nothing" {
                let ctx = context { options with Generic = Some "paracetamol" }

                ctx
                |> OrderContext.change OrderCategory.Medication FilterField.Generic (Some "paracetamol") None
                |> Expect.equal "the context as it is" ctx
            }

            test "a cleared route lets go of the form and the dose type, keeps the indication" {
                context
                    { options with
                        Indication = Some "pijn"
                        Route = Some "RECTAAL"
                        Form = Some "zetpil"
                        DoseType = Some(Informedica.GenForm.Lib.Types.Discontinuous "onderhoud")
                    }
                |> OrderContext.change OrderCategory.Medication FilterField.Route None None
                |> fun c -> c.Filter.Indication, c.Filter.Route, c.Filter.Routes, c.Filter.Form, c.Filter.DoseType
                |> Expect.equal
                    "the indication kept, the route and its options gone, form and dose type let go"
                    (Some "pijn", None, [||], None, None)
            }

            test "nothing chosen afterwards keeps no options of the lookup fields" {
                context { options with Route = Some "RECTAAL" }
                |> OrderContext.change OrderCategory.Medication FilterField.Route None None
                |> fun c -> c.Filter.Indications, c.Filter.Generics, c.Filter.Routes, c.Filter.Forms, c.Filter.DoseTypes
                |> Expect.equal "no options" ([||], [||], [||], [||], [||])
            }

            test "a field picked by index keeps the options" {
                context options
                |> OrderContext.changeFilter OrderCategory.Medication FilterField.Route (Some 2)
                |> Result.map (fun c -> c.Filter.Route, c.Filter.Routes)
                |> Expect.equal "rectal chosen, all routes kept" (Ok(Some "RECTAAL", options.Routes))
            }

            test "an index past the options is refused" {
                context options
                |> OrderContext.changeFilter OrderCategory.Medication FilterField.Form (Some 5)
                |> Result.isError
                |> Expect.isTrue "no sixth form"
            }

            test "the components at their positions" {
                context options
                |> OrderContext.setNthComponents [| 0; 2 |]
                |> Result.map _.Filter.SelectedComponents
                |> Expect.equal "the first and the third" (Ok(ComponentSelection.Selected [ "paracetamol"; "water" ]))
            }

            test "no position selects none" {
                context options
                |> OrderContext.setNthComponents [||]
                |> Result.map _.Filter.SelectedComponents
                |> Expect.equal "none" (Ok(ComponentSelection.Selected []))
            }

            test "the components field emptied is every component, a position selects that option" {
                [ None; Some 2 ]
                |> List.map (fun n ->
                    context options
                    |> OrderContext.changeFilter OrderCategory.Medication FilterField.Components n
                    |> Result.map _.Filter.SelectedComponents
                )
                |> Expect.equal
                    "all, then the third"
                    [
                        Ok ComponentSelection.AllComponents
                        Ok(ComponentSelection.Selected [ "water" ])
                    ]
            }

            test "no single pick of a component is every component, a pick selects it" {
                [ None; Some "paracetamol" ]
                |> List.map (fun c ->
                    (context options
                     |> OrderContext.change OrderCategory.Medication FilterField.Components c None)
                        .Filter.SelectedComponents
                )
                |> Expect.equal
                    "all, then the pick"
                    [
                        ComponentSelection.AllComponents
                        ComponentSelection.Selected [ "paracetamol" ]
                    ]
            }

            test "the second scenario selected, with its form" {
                context options
                |> OrderContext.selectNthScenario 1
                |> Result.map (fun c -> c.Filter.Form, c.Scenarios |> Array.map _.No)
                |> Expect.equal "the drink, alone" (Ok(Some "drank", [| 2 |]))
            }

            test "a scenario past the scenarios is refused" {
                context options
                |> OrderContext.selectNthScenario 2
                |> Result.isError
                |> Expect.isTrue "no third scenario"
            }

            test "clearing everything keeps the patient and drops the rest" {
                let cleared = context options |> OrderContext.clearAll

                (cleared.Patient, cleared.Filter, cleared.Scenarios.Length, cleared.Argumentation)
                |> Expect.equal "patient kept" (Patient.patient, OrderContext.emptyFilter, 0, None)
            }
        ]


/// A choice that decides the dose rule lets go of the components of the dose rule: the additive options,
/// the dose rule components and the selection; the next scenario sets them again.
[<Tests>]
let doseRuleComponentsTests =
    let additives = [| "NaCl 3%"; "KCl 7,4%" |]
    let names = [| "Samenstelling C"; "NaCl 3%"; "KCl 7,4%"; "gluc 10%" |]
    let day2 = Informedica.GenForm.Lib.Types.Once "dag 2"
    let day3 = Informedica.GenForm.Lib.Types.Once "dag 3"

    // the paracetamol context with the components of a TPN of day 2: only the filter is read
    let first =
        let ctx = OrderPlanTests.EvaluateFixtures.pcmContext

        { ctx with
            Filter =
                { ctx.Filter with
                    Components = additives
                    DoseRuleComponents = names
                    SelectedComponents = ComponentSelection.Selected [ "KCl 7,4%" ]
                    DoseType = Some day2
                }
        }

    let components (f: Filter) = f.Components, f.DoseRuleComponents, f.SelectedComponents

    let none = [||], [||], ComponentSelection.AllComponents

    let change field chosen dt =
        first
        |> OrderContext.change OrderCategory.Medication field chosen dt
        |> _.Filter
        |> components

    testList
        "the components of the dose rule after another dose rule is picked"
        [
            test "a dose type picked lets go of them" {
                change FilterField.DoseType None (Some day3) |> Expect.equal "emptied" none
            }

            test "a dose type emptied lets go of them" {
                change FilterField.DoseType None None |> Expect.equal "emptied" none
            }

            for field in
                [
                    FilterField.Indication
                    FilterField.Generic
                    FilterField.Route
                    FilterField.Form
                ] do
                test $"%A{field} picked lets go of them" {
                    change field (Some "another") None |> Expect.equal "emptied" none
                }

            test "a diluent picked keeps them" {
                change FilterField.Diluent (Some "gluc 20%") None
                |> Expect.equal "kept" (components first.Filter)
            }

            test "the same dose type keeps them" {
                change FilterField.DoseType None (Some day2)
                |> Expect.equal "kept" (components first.Filter)
            }

            test "a seed of another medication from the formulary lets go of them" {
                first
                |> OrderContext.seedFilter SeedSource.Formulary None (Some "Intralipid 20%") None None None
                |> _.Filter
                |> components
                |> Expect.equal "emptied" none
            }

            test "a seed of the same medication keeps them" {
                let f = first.Filter

                first
                |> OrderContext.seedFilter SeedSource.Formulary f.Indication f.Generic f.Route f.Form f.DoseType
                |> _.Filter
                |> components
                |> Expect.equal "kept" (components f)
            }

            // each source keeps its own picks, but another pick is another dose rule
            let f = first.Filter

            for source, ind, gen, dt in
                [
                    SeedSource.Formulary, f.Indication, f.Generic, Some day3
                    SeedSource.Parenteralia, Some "another", f.Generic, f.DoseType
                    SeedSource.Url, f.Indication, Some "Intralipid 20%", f.DoseType
                ] do
                test $"a %A{source} seed with another pick lets go of them" {
                    first
                    |> OrderContext.seedFilter source ind gen f.Route f.Form dt
                    |> _.Filter
                    |> components
                    |> Expect.equal "emptied" none
                }
        ]
