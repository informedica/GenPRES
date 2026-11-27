/// An evaluated nutrition context narrowed and settled to the rule set of its category, as the order
/// context port and the plan port do.
module Informedica.GenPRES.Server.Tests.NutritionRuleSetsTests

open Expecto
open Expecto.Flip
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
open OrderContextRuleTests.FeedingFixtures


[<Tests>]
let tests =
    testList
        "the nutrition slot on the order context path"
        [
            testAsync "an evaluated feeding context is narrowed to the generics of its rule set" {
                let sut = ServerApi.Adapters.makeAppEnv provider

                let! answer =
                    empty (OrderCategory.Nutrition NutritionCategory.EnteralFeeding)
                    |> sut.orderContext.evaluate OrderContext.UpdateOrderContext

                answer
                |> generics
                |> Expect.equal "voedsel, not paracetamol" (Ok [| "voedsel" |])
            }

            testAsync "the one generic and indication the narrowing leaves are chosen" {
                let sut = ServerApi.Adapters.makeAppEnv provider

                let! answer =
                    empty (OrderCategory.Nutrition NutritionCategory.EnteralFeeding)
                    |> sut.orderContext.evaluate OrderContext.UpdateOrderContext

                match answer with
                | Ok(Evaluated pc) ->
                    (pc.Context.Filter.Indication, pc.Context.Filter.Generic)
                    |> Expect.equal "voeding and voedsel" (Some "voeding", Some "voedsel")
                | other -> failtest $"expected an evaluated context, got %A{other}"
            }

            testAsync
                "a refused feeding context with a generic its rules do not offer offers only the rule set's generics" {
                let sut = ServerApi.Adapters.makeAppEnv provider

                let! answer =
                    unknownGeneric (OrderCategory.Nutrition NutritionCategory.EnteralFeeding)
                    |> sut.orderContext.evaluate OrderContext.UpdateOrderContext

                match answer with
                | Ok(Refused(pc, _)) ->
                    pc.Context.Filter.Generics
                    |> Expect.equal "voedsel, not paracetamol" [| "voedsel" |]
                | other -> failtest $"expected a refusal, got %A{other}"
            }

            testAsync "a refused medication context is not narrowed" {
                let sut = ServerApi.Adapters.makeAppEnv provider

                let! answer =
                    unknownGeneric OrderCategory.Medication
                    |> sut.orderContext.evaluate OrderContext.UpdateOrderContext

                match answer with
                | Ok(Refused(pc, _)) -> pc.Context.Filter.Generics |> Expect.equal "both" [| "paracetamol"; "voedsel" |]
                | other -> failtest $"expected a refusal, got %A{other}"
            }

            testAsync "a medication context is not narrowed" {
                let sut = ServerApi.Adapters.makeAppEnv provider

                let! answer =
                    empty OrderCategory.Medication
                    |> sut.orderContext.evaluate OrderContext.UpdateOrderContext

                answer |> generics |> Expect.equal "both" (Ok [| "paracetamol"; "voedsel" |])
            }

            testAsync "a category without a rule set is refused in words" {
                let sut = ServerApi.Adapters.makeAppEnv provider

                let! answer =
                    empty (OrderCategory.Nutrition NutritionCategory.TPN)
                    |> sut.orderContext.evaluate OrderContext.UpdateOrderContext

                answer |> Expect.equal "no TPN" (Error [| "RuleSets: no rule set for TPN" |])
            }

            testAsync "a nutrition context the rules refuse comes back refused, its pick as sent" {
                let sut = ServerApi.Adapters.makeAppEnv provider
                let feeding = empty (OrderCategory.Nutrition NutritionCategory.EnteralFeeding)

                let! answer =
                    { feeding with
                        Context =
                            { feeding.Context with Filter = { feeding.Context.Filter with Generic = Some "onbekend" } }
                    }
                    |> sut.orderContext.evaluate OrderContext.UpdateOrderContext

                match answer with
                | Ok(Refused(pc, _)) -> pc.Context.Filter.Generic |> Expect.equal "the pick" (Some "onbekend")
                | other -> failtest $"expected a refusal, got %A{other}"
            }
        ]


[<Tests>]
let linkedOfferTests =
    testList
        "the linked orders in the order context answer"
        [
            testList
                "the order context answer"
                [
                    // the answer to the command, and how often the port was asked for the offers
                    let answer cmd =
                        let asked = ref 0

                        let port: ServerApi.OrderContextPort =
                            { OrderContextSwitchTests.echo with
                                linkedOrders =
                                    fun pc ->
                                        asked.Value <- asked.Value + 1
                                        async { return Ok [| { pc with Id = "offer" } |] }
                            }

                        let result =
                            ServerApi.OrderContextCommand.processViewCmd
                                (OrderContextSwitchTests.envOver true port)
                                (cmd, OrderContextSwitchTests.ctx)
                            |> Async.RunSynchronously

                        result, asked.Value

                    let offered =
                        Result.map (
                            function
                            | Informedica.GenPRES.Shared.Types.OrderContextResponse.Evaluated(_, linked) ->
                                linked |> Array.map _.Id
                            | _ -> [||]
                        )

                    test "a command that changes the choices carries the offers" {
                        let result, asked =
                            Informedica.GenPRES.Shared.Api.OrderViewCommand.ClearAllFilterProperty |> answer

                        result |> offered |> Expect.equal "the offer" (Ok [| "offer" |])
                        asked |> Expect.equal "asked once" 1
                    }

                    test "a value step carries no offers and does not ask for them" {
                        let result, asked =
                            Informedica.GenPRES.Shared.Api.OrderViewCommand.IncreaseScheduleFrequencyProperty
                            |> answer

                        result |> offered |> Expect.equal "none" (Ok [||])
                        asked |> Expect.equal "not asked" 0
                    }

                    test "offers that fail leave the evaluation answered, with no offers" {
                        let port: ServerApi.OrderContextPort =
                            { OrderContextSwitchTests.echo with
                                linkedOrders = fun _ -> async { return Error [| "no rule sets" |] }
                            }

                        ServerApi.OrderContextCommand.processViewCmd
                            (OrderContextSwitchTests.envOver true port)
                            (Informedica.GenPRES.Shared.Api.OrderViewCommand.ClearAllFilterProperty,
                             OrderContextSwitchTests.ctx)
                        |> Async.RunSynchronously
                        |> offered
                        |> Expect.equal "evaluated, nothing offered" (Ok [||])
                    }

                ]
        ]


[<Tests>]
let ruleSetRowTests =
    testList
        "the warning about the rule set rows the server reads"
        [
            test "the warning names the category and says its rows are left out" {
                "Enteral"
                |> ServerApi.Adapters.RuleSets.unknownCategoryWarning
                |> Logging.ServerLogging.formatMessage
                |> Expect.equal "the text" "RuleSets: Enteral is not a rule set category, its rows are left out"
            }
        ]
