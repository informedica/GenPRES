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
                |> OrderContext.change OrderCategory.Drug FilterField.Route (Some "RECTAAL") None
                |> fun c -> c.Filter.Route, c.Filter.Routes
                |> Expect.equal "rectal chosen, all routes kept" (Some "RECTAAL", options.Routes)
            }

            test "a filter change lets go of the scenarios" {
                context options
                |> OrderContext.change OrderCategory.Drug FilterField.Route (Some "ORAAL") None
                |> _.Scenarios.Length
                |> Expect.equal "no scenarios" 0
            }

            test "a choice the field already holds changes nothing" {
                let ctx = context { options with Generic = Some "paracetamol" }

                ctx
                |> OrderContext.change OrderCategory.Drug FilterField.Generic (Some "paracetamol") None
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
                |> OrderContext.change OrderCategory.Drug FilterField.Route None None
                |> fun c -> c.Filter.Indication, c.Filter.Route, c.Filter.Routes, c.Filter.Form, c.Filter.DoseType
                |> Expect.equal
                    "the indication kept, the route and its options gone, form and dose type let go"
                    (Some "pijn", None, [||], None, None)
            }

            test "nothing chosen afterwards keeps no options of the lookup fields" {
                context { options with Route = Some "RECTAAL" }
                |> OrderContext.change OrderCategory.Drug FilterField.Route None None
                |> fun c -> c.Filter.Indications, c.Filter.Generics, c.Filter.Routes, c.Filter.Forms, c.Filter.DoseTypes
                |> Expect.equal "no options" ([||], [||], [||], [||], [||])
            }

            test "a field picked by index keeps the options" {
                context options
                |> OrderContext.changeFilter OrderCategory.Drug FilterField.Route (Some 2)
                |> Result.map (fun c -> c.Filter.Route, c.Filter.Routes)
                |> Expect.equal "rectal chosen, all routes kept" (Ok(Some "RECTAAL", options.Routes))
            }

            test "an index past the options is refused" {
                context options
                |> OrderContext.changeFilter OrderCategory.Drug FilterField.Form (Some 5)
                |> Result.isError
                |> Expect.isTrue "no sixth form"
            }

            test "the components at their positions" {
                context options
                |> OrderContext.setNthComponents [| 0; 2 |]
                |> Result.map _.Filter.SelectedComponents
                |> Expect.equal "the first and the third" (Ok [| "paracetamol"; "water" |])
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
