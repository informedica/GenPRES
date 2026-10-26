/// The order plan path on the domain-typed port: the contract model parsed in, the verb
/// mapped, the port's plan mapped out with the environment's demo flag, the refusals in the
/// server's words.
module Informedica.GenPRES.Server.Tests.OrderPlanSwitchTests

open Expecto
open Expecto.Flip
open Informedica.GenOrder.Lib
// after Expecto, whose FocusState has a Normal case too
open Informedica.GenPRES.Shared.Types
open ServerApi
open Informedica.GenPRES.Server.Tests.StubAdapterTests.StubAdapters


let patient = StubPatientData.patient

let ctx: OrderContext =
    { Informedica.GenPRES.Shared.Models.OrderContext.empty with
        Id = "c-1"
        Category = OrderCategory.Nutrition NutritionCategory.TPN
        DemoVersion = true
        Patient = patient
    }

let plan: OrderPlan =
    { Informedica.GenPRES.Shared.Models.OrderPlan.create patient [| ctx |] with Filtered = [| "c-1" |] }


let answering (seen: string list ref) name (p: Informedica.GenOrder.Lib.Types.OrderPlan) =
    seen.Value <- name :: seen.Value
    async { return Ok p }


let echoPort seen : OrderPlanPort =
    {
        recalculate = answering seen "recalculate"
        navigate = fun p _ _ _ -> answering seen "navigate" p
        addOrderContext = fun p _ -> answering seen "addOrderContext" p
        newOrderContext = fun p _ -> answering seen "newOrderContext" p
        removeOrderContexts = fun p _ -> answering seen "removeOrderContexts" p
        openWith = fun pat cs -> answering seen "openWith" (Informedica.GenOrder.Lib.OrderPlan.create pat cs)
    }


let envOver demo (port: OrderPlanPort) =
    { makeEnv (formularyAlwaysOk Informedica.GenPRES.Shared.Models.Formulary.empty) (orderContextAlwaysOk ctx) with
        orderPlan = port
        demo = demo
    }


[<Tests>]
let tests =
    testList
        "the order plan switch-over"
        [
            testAsync "every case reaches its port member, the answer the plan as the contract model" {
                let seen = ref []
                let run = OrderPlanCommand.processCmd (envOver true (echoPort seen))

                let! recalculated =
                    run (Informedica.GenPRES.Shared.Api.OrderPlanCommand.FilterRows(plan.Filtered, plan))

                recalculated
                |> Expect.equal "the plan back, demo as the environment says" (Ok plan)

                let! _ =
                    run (
                        Informedica.GenPRES.Shared.Api.OrderPlanCommand.Navigate(
                            plan,
                            "c-1",
                            Informedica.GenPRES.Shared.Api.OrderViewCommand.SeedFilter(
                                Informedica.GenPRES.Shared.Types.SeedSource.Reload,
                                None,
                                None,
                                None,
                                None,
                                None
                            ),
                            ctx
                        )
                    )

                let! _ = run (Informedica.GenPRES.Shared.Api.OrderPlanCommand.AddOrderContext(plan, ctx))
                let! _ =
                    run (Informedica.GenPRES.Shared.Api.OrderPlanCommand.NewOrderContext(plan, NutritionCategory.TPN))
                let! _ = run (Informedica.GenPRES.Shared.Api.OrderPlanCommand.RemoveOrderContexts(plan, [| "c-1" |]))
                let! opened = run (Informedica.GenPRES.Shared.Api.OrderPlanCommand.Open(patient, [| ctx |]))

                seen.Value
                |> List.rev
                |> Expect.equal
                    "each to its port"
                    [
                        "recalculate"
                        "navigate"
                        "addOrderContext"
                        "newOrderContext"
                        "removeOrderContexts"
                        "openWith"
                    ]

                match opened with
                | Ok p ->
                    p.OrderContexts
                    |> Array.map _.Id
                    |> Expect.equal "the contexts as given" [| "c-1" |]

                    p.Filtered |> Expect.isEmpty "nothing filtered on an open"
                | Error e -> failtest $"open refused: %A{e}"
            }

            testAsync "a patient update recalculates the plan for that patient, each context keeping its own" {
                let seen = ref []
                let updated = { patient with Department = Some "NEO" }

                let! answer =
                    OrderPlanCommand.processCmd
                        (envOver true (echoPort seen))
                        (Informedica.GenPRES.Shared.Api.OrderPlanCommand.UpdatePatient(updated, plan))

                seen.Value |> Expect.equal "recalculated" [ "recalculate" ]

                answer
                |> Result.map (fun p -> p.Patient.Department, p.OrderContexts |> Array.map _.Patient.Department)
                |> Expect.equal
                    "the plan's patient updated, the context's kept"
                    (Ok(Some "NEO", [| patient.Department |]))
            }

            testAsync "a row filter recalculates the plan over the rows chosen" {
                let seen = ref []
                let run = OrderPlanCommand.processCmd (envOver true (echoPort seen))

                let! none = run (Informedica.GenPRES.Shared.Api.OrderPlanCommand.FilterRows([||], plan))
                let! one =
                    run (
                        Informedica.GenPRES.Shared.Api.OrderPlanCommand.FilterRows(
                            [| "c-1" |],
                            { plan with Filtered = [||] }
                        )
                    )

                seen.Value |> Expect.equal "recalculated twice" [ "recalculate"; "recalculate" ]

                [ none; one ]
                |> List.map (Result.map _.Filtered)
                |> Expect.equal "the rows as chosen" [ Ok [||]; Ok [| "c-1" |] ]
            }

            testAsync "the demo flag on every context is the environment's, not the wire's" {
                let! answer =
                    OrderPlanCommand.processCmd
                        (envOver false (echoPort (ref [])))
                        (Informedica.GenPRES.Shared.Api.OrderPlanCommand.FilterRows(plan.Filtered, plan))

                answer
                |> Result.map (_.OrderContexts >> Array.map _.DemoVersion)
                |> Expect.equal "not demo" (Ok [| false |])
            }

            testAsync "the port receives the parsed plan, the verb and the plan context" {
                let seen = ref None

                let port =
                    { echoPort (ref []) with
                        navigate =
                            fun p id cmd pc ->
                                seen.Value <- Some(p.Filtered, id, cmd pc.Context, pc.Category)
                                async { return Ok p }
                    }

                let! _ =
                    OrderPlanCommand.processCmd
                        (envOver false port)
                        (Informedica.GenPRES.Shared.Api.OrderPlanCommand.Navigate(
                            plan,
                            "c-1",
                            Informedica.GenPRES.Shared.Api.OrderViewCommand.SelectNthOrderScenario 0,
                            ctx
                        ))

                match seen.Value with
                | Some(filtered, "c-1", OrderContext.SelectNthOrderScenario _, category) ->
                    filtered |> Expect.equal "the plan parsed" [| "c-1" |]

                    category
                    |> Expect.equal
                        "the context's category"
                        (Informedica.GenOrder.Lib.Types.OrderCategory.Nutrition
                            Informedica.GenOrder.Lib.Types.NutritionCategory.TPN)
                | other -> failtest $"the port saw %A{other}"
            }

            testAsync "the new context's category is the domain's" {
                let seen = ref None

                let port =
                    { echoPort (ref []) with
                        newOrderContext =
                            fun p category ->
                                seen.Value <- Some category
                                async { return Ok p }
                    }

                let! _ =
                    OrderPlanCommand.processCmd
                        (envOver false port)
                        (Informedica.GenPRES.Shared.Api.OrderPlanCommand.NewOrderContext(
                            plan,
                            NutritionCategory.EnteralSupplement
                        ))

                seen.Value
                |> Expect.equal
                    "the category mapped"
                    (Some Informedica.GenOrder.Lib.Types.NutritionCategory.EnteralSupplement)
            }

            testAsync "a draft plan is refused before the port with the server's words" {
                let seen = ref []

                let! refused =
                    OrderPlanCommand.processCmd
                        (envOver false (echoPort seen))
                        (Informedica.GenPRES.Shared.Api.OrderPlanCommand.FilterRows(
                            [||],
                            Informedica.GenPRES.Shared.Models.OrderPlan.empty
                        ))

                refused |> Expect.equal "no patient" (Error [| Patient.noPatient |])
                seen.Value |> Expect.isEmpty "the port never asked"
            }

            test "the refusals in the server's words, the category labelled from the rule sets" {
                let words = OrderPlanMapper.words NutritionRuleSets.all

                OrderPlanError.SupplementNeedsFeeding
                |> words
                |> Expect.equal "supplement" "A supplement needs a feeding in the plan"

                OrderPlanError.CategoryHeld Informedica.GenOrder.Lib.Types.NutritionCategory.EnteralFeeding
                |> words
                |> Expect.equal "category" "The plan already holds a Enterale Voeding context"

                OrderPlanError.NotNarrowed 2
                |> words
                |> Expect.equal "not narrowed" "The workbench holds 2 candidates, not one order"

                OrderPlanError.OrderHeld "o"
                |> words
                |> Expect.equal "held" "The plan already holds this order"

                OrderPlanError.NoSuchContext "c-9"
                |> words
                |> Expect.equal "no context" "The plan holds no context c-9"
            }
        ]
