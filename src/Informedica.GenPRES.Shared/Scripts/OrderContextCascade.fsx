// The cascade behind the prescribing fields: what a change to one field does to the fields
// below it. Today a change writes the field alone, so picking another medication leaves the
// route, the form and the dose type holding the previous medication's answers; the server then
// finds no rule matching the whole filter and says so, and clearing the field first is the only
// way through. Clearing is the branch that already resets what is below it.
//
// The rule drafted here: the five fields are a chain, in the order the page shows them, and a
// change to one of them clears the choices below it, the options below it and the scenarios,
// whether the change is a value or a clearing. A change to the value a field already holds
// changes nothing, so re-picking what is chosen does not throw the rest away.
//
// The options below are emptied and not kept: a stale list offers routes that the medication
// just picked may not have, and a pick from it builds a filter no rule matches. The server
// refills the lists in its reply. Keeping them, and letting the reply drop what it no longer
// offers, is the alternative; it avoids the fields standing empty for the length of a request.
//
// Script-first draft (script-only policy) of the `OrderContext` cascade, → `Shared/Models.fs`.
//
// Run: `dotnet fsi OrderContextCascade.fsx` from this directory.

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "../Types.fs"
#load "../Calculations.fs"
#load "../Utils.fs"
#load "../Localization.fs"
#load "../Models.fs"

open Expecto
open Expecto.Flip
open Shared.Types


/// → `Shared/Models.fs`, the `OrderContext` module.
module OrderContext =

    open Shared.Models.OrderContext


    /// Everything below the indication: the four choices that follow it, their options, and the
    /// scenarios, which stand on the whole filter.
    let belowIndication (ctx: OrderContext) : OrderContext =
        { ctx with
            Filter =
                { ctx.Filter with
                    Generics = [||]
                    Generic = None
                    Routes = [||]
                    Route = None
                    Forms = [||]
                    Form = None
                    DoseTypes = [||]
                    DoseType = None
                }
            Scenarios = [||]
        }


    /// Everything below the generic.
    let belowGeneric (ctx: OrderContext) : OrderContext =
        { ctx with
            Filter =
                { ctx.Filter with
                    Routes = [||]
                    Route = None
                    Forms = [||]
                    Form = None
                    DoseTypes = [||]
                    DoseType = None
                }
            Scenarios = [||]
        }


    /// Everything below the route.
    let belowRoute (ctx: OrderContext) : OrderContext =
        { ctx with
            Filter =
                { ctx.Filter with
                    Forms = [||]
                    Form = None
                    DoseTypes = [||]
                    DoseType = None
                }
            Scenarios = [||]
        }


    /// Everything below the form.
    let belowForm (ctx: OrderContext) : OrderContext =
        { ctx with
            Filter =
                { ctx.Filter with
                    DoseTypes = [||]
                    DoseType = None
                }
            Scenarios = [||]
        }


    /// Below the dose type there is nothing to choose; the scenarios stand on it.
    let belowDoseType (ctx: OrderContext) : OrderContext = { ctx with Scenarios = [||] }


    /// A change to a field: nothing when the field already holds it, otherwise the field
    /// written and everything below it cleared.
    let change current below write s (ctx: OrderContext) : OrderContext =
        if current ctx = s then ctx else ctx |> below |> write s


    let indicationChange s (ctx: OrderContext) : OrderContext =
        ctx
        |> change _.Filter.Indication belowIndication (fun s c -> { c with OrderContext.Filter.Indication = s }) s


    let medicationChange s (ctx: OrderContext) : OrderContext =
        ctx |> change _.Filter.Generic belowGeneric (fun s c -> { c with OrderContext.Filter.Generic = s }) s


    let routeChange s (ctx: OrderContext) : OrderContext =
        ctx |> change _.Filter.Route belowRoute (fun s c -> { c with OrderContext.Filter.Route = s }) s


    let formChange s (ctx: OrderContext) : OrderContext =
        ctx |> change _.Filter.Form belowForm (fun s c -> { c with OrderContext.Filter.Form = s }) s


    let doseTypeChange (dt: DoseType option) (ctx: OrderContext) : OrderContext =
        ctx |> change _.Filter.DoseType belowDoseType (fun s c -> { c with OrderContext.Filter.DoseType = s }) dt


let full: OrderContext =
    { Shared.Models.OrderContext.empty with
        Filter =
            { Shared.Models.OrderContext.empty.Filter with
                Indications = [| "koorts"; "pijn" |]
                Indication = Some "koorts"
                Generics = [| "paracetamol"; "ibuprofen" |]
                Generic = Some "paracetamol"
                Routes = [| "oraal"; "rectaal" |]
                Route = Some "oraal"
                Forms = [| "tablet"; "drank" |]
                Form = Some "tablet"
                DoseTypes = [| DoseType.Discontinuous "" |]
                DoseType = Some(DoseType.Discontinuous "")
            }
    }


let filter (ctx: OrderContext) = ctx.Filter

let chosen (ctx: OrderContext) =
    ctx.Filter.Indication, ctx.Filter.Generic, ctx.Filter.Route, ctx.Filter.Form, ctx.Filter.DoseType


let tests =
    testList
        "the cascade"
        [
            test "picking another indication keeps it and clears the four below" {
                full
                |> OrderContext.indicationChange (Some "pijn")
                |> chosen
                |> Expect.equal "only the indication" (Some "pijn", None, None, None, None)
            }

            test "picking another indication empties the options below it" {
                let f = full |> OrderContext.indicationChange (Some "pijn") |> filter

                (f.Indications, f.Generics, f.Routes, f.Forms, f.DoseTypes)
                |> Expect.equal "its own options stand, the rest go" ([| "koorts"; "pijn" |], [||], [||], [||], [||])
            }

            test "picking another medication keeps the indication and clears the three below" {
                full
                |> OrderContext.medicationChange (Some "ibuprofen")
                |> chosen
                |> Expect.equal "indication and generic" (Some "koorts", Some "ibuprofen", None, None, None)
            }

            test "picking another route keeps the two above and clears the two below" {
                full
                |> OrderContext.routeChange (Some "rectaal")
                |> chosen
                |> Expect.equal
                    "up to the route"
                    (Some "koorts", Some "paracetamol", Some "rectaal", None, None)
            }

            test "picking another form keeps the three above and clears the dose type" {
                full
                |> OrderContext.formChange (Some "drank")
                |> chosen
                |> Expect.equal
                    "up to the form"
                    (Some "koorts", Some "paracetamol", Some "oraal", Some "drank", None)
            }

            test "picking another dose type keeps every choice above it" {
                let dt = DoseType.Timed "" |> Some

                full
                |> OrderContext.doseTypeChange dt
                |> chosen
                |> Expect.equal "all five" (Some "koorts", Some "paracetamol", Some "oraal", Some "tablet", dt)
            }

            test "every change clears the scenarios" {
                // a scenario is never read here, only counted, so an uninhabited one will do
                let withScenario =
                    { full with
                        Scenarios = Array.zeroCreate<OrderScenario> 1
                    }

                [
                    withScenario |> OrderContext.indicationChange (Some "pijn")
                    withScenario |> OrderContext.medicationChange (Some "ibuprofen")
                    withScenario |> OrderContext.routeChange (Some "rectaal")
                    withScenario |> OrderContext.formChange (Some "drank")
                    withScenario |> OrderContext.doseTypeChange (DoseType.Timed "" |> Some)
                ]
                |> List.forall (fun c -> c.Scenarios |> Array.isEmpty)
                |> Expect.isTrue "none left, whichever field changed"
            }

            test "clearing a field clears it and everything below" {
                full
                |> OrderContext.medicationChange None
                |> chosen
                |> Expect.equal "the indication alone" (Some "koorts", None, None, None, None)
            }

            test "picking the value a field already holds changes nothing" {
                full
                |> OrderContext.medicationChange (Some "paracetamol")
                |> Expect.equal "the context it was" full
            }

            test "a nutrition context cascades as a drug context does" {
                let nutrition =
                    { full with
                        Category = OrderCategory.Nutrition NutritionCategory.EnteralFeeding
                    }

                nutrition
                |> OrderContext.medicationChange (Some "ibuprofen")
                |> chosen
                |> Expect.equal "indication and generic" (Some "koorts", Some "ibuprofen", None, None, None)
            }
        ]


runTestsWithCLIArgs [] [||] tests
