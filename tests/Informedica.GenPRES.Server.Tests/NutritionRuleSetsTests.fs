/// The nutrition slot on the order context path: a slot discovered from the rule set of its
/// category, and an evaluated nutrition context narrowed to that rule set, as the plan port does.
module Informedica.GenPRES.Server.Tests.NutritionRuleSetsTests

open Expecto
open Expecto.Flip
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


module Fixtures =

    let rowOf generic indication =
        let r = OrderPlanTests.RuleFixtures.row Resources.Departments.defaultDepartment

        { r with
            Generic = { r.Generic with Name = generic }
            Indication = indication
            ScheduleData =
                { r.ScheduleData with
                    DoseLimitData =
                        { r.ScheduleData.DoseLimitData with
                            Component = generic
                            Substance = generic
                        }
                }
        }


    // a nutrition rule for "voeding" beside a medication rule for "koorts"
    let data = [| rowOf "voedsel" "voeding"; rowOf "paracetamol" "koorts" |]

    let product g = Product.create g "zetpil" "RECTAAL" [| g |]


    let rules =
        DoseRuleLoader.fromData
            OrderPlanTests.RuleFixtures.routeMapping
            [||]
            [| product "voedsel"; product "paracetamol" |]
            data
        |> fst


    /// The rule set rows of a feeding under "voeding", expanded over the dose rules as the resource
    /// expands them.
    let feedingRows =
        [|
            OrderPlanTests.EvaluateFixtures.ruleSetRow "EnteralFeeding" "Enterale Voeding" "voeding" ""
        |]
        |> RuleSet.expand (RuleSet.DoseRuleNames.ofData data)


    /// The rules above with the rule set rows of a feeding; the totals data empty.
    type FeedingRuleSet() =
        inherit OrderPlanTests.RulesOf(rules)

        interface Resources.IResourceProvider with
            member _.Get(key: Resources.ResourceKey<'T>) : 'T =
                if key.Name = Resources.Keys.ruleSetData.Name then
                    unbox<'T>(box feedingRows)
                else
                    raise (System.NotImplementedException())

            member _.GetDoseRules() = rules
            member _.GetRouteMappings() = OrderPlanTests.RuleFixtures.routeMapping
            member _.GetTotals() = [||]


    let provider = FeedingRuleSet() :> Resources.IResourceProvider

    let patient = OrderPlanTests.RuleFixtures.patient (Some 5N) false (Some 20N) None


    /// An empty context of the category for the patient.
    let empty category =
        NutritionRuleSet.emptyContext None patient |> PlanContext.create "" category


    /// The generics an evaluation offers, evaluated or refused.
    let generics =
        Result.map (
            function
            | Evaluated(pc: PlanContext)
            | Refused(pc, _) -> pc.Context.Filter.Generics
        )


open Fixtures


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

            test "a slot is discovered with only the generics of its rule set" {
                ServerApi.Adapters.NutritionRuleSets.discover
                    Scenarios.testStart
                    OrderLogging.noOp
                    provider
                    "s-1"
                    NutritionCategory.EnteralFeeding
                    patient
                |> Result.map _.Filter.Generics
                |> Expect.equal "voedsel" (Ok [| "voedsel" |])
            }
        ]


/// The linked orders a narrowed context offers, and the order context answer that carries them.
module LinkedOffers =

    let row: LinkRuleData =
        {
            LinkRuleData.Generic = "Fantomalt pdr"
            LinkRuleData.Route = ""
            LinkRuleData.Indication = "Enterale toevoeging"
            LinkRuleData.DoseType = "discontinuous"
            LinkRuleData.DoseText = ""
            LinkRuleData.MainGeneric = ""
            LinkRuleData.MainRoute = ""
            LinkRuleData.MainIndication = "Enterale voeding"
            LinkRuleData.MainDoseType = "discontinuous"
            LinkRuleData.MainDoseText = ""
            LinkRuleData.Required = "yes"
        }


    let ataluren: LinkRuleData =
        { row with
            Generic = "ataluren"
            Route = "ORAAL"
            Indication = "Duchenne"
            DoseText = "avond"
            MainGeneric = "ataluren"
            MainRoute = "ORAAL"
            MainIndication = "Duchenne"
            MainDoseText = "ochtend middag"
        }


    let rulesOf rows =
        rows |> LinkRule.ofRows |> Result.defaultWith (fun e -> failtest $"%A{e}")


    let routes: RouteMapping[] =
        [|
            {
                Long = "ORAAL"
                Short = "or"
            }
        |]


    let sets: NutritionRuleSet[] =
        [|
            {
                Category = NutritionCategory.EnteralFeeding
                Label = "Enterale Voeding"
                Indications = [| "Enterale voeding" |]
                Generics = [| "Nutrini (Nutricia)" |]
            }
            {
                Category = NutritionCategory.EnteralSupplement
                Label = "Enterale Toevoeging"
                Indications = [| "Enterale toevoeging" |]
                Generics = [||]
            }
        |]


    /// A plan context narrowed to one order made from a dose rule with the generic label, indication,
    /// route and dose type.
    let narrowed category ind gen rte dt =
        let sc =
            { OrderPlanTests.Fixtures.scenario "o-1" with
                Name = gen
                Indication = ind
                Route = rte
                DoseType = dt
            }

        OrderPlanTests.Fixtures.context [| sc |] |> PlanContext.create "c-1" category


    let feeding =
        narrowed
            (OrderCategory.Nutrition NutritionCategory.EnteralFeeding)
            "Enterale voeding"
            "Nutrini (Nutricia)"
            "ORAAL"
            (DoseType.Discontinuous "")


    let morning =
        narrowed OrderCategory.Medication "Duchenne" "ataluren" "ORAAL" (DoseType.Discontinuous "ochtend middag")


    let doseTypes =
        [|
            DoseType.Discontinuous ""
            DoseType.Discontinuous "ochtend middag"
            DoseType.Discontinuous "avond"
            DoseType.Continuous ""
        |]


    /// An evaluation that offers the generics given when none is picked and every dose type above, and
    /// writes the label of the rule set it was given into the argumentation, so that a test can read it.
    /// A context whose generic is "refused" is refused.
    let evaluateWith generics (set: NutritionRuleSet option) (pc: PlanContext) =
        let f = pc.Context.Filter

        let answered =
            { pc with
                Context =
                    { pc.Context with
                        Filter =
                            { f with
                                Generics = f.Generic |> Option.map Array.singleton |> Option.defaultValue generics
                                DoseTypes = doseTypes
                            }
                        Argumentation = set |> Option.map _.Label
                    }
            }

        if f.Generic = Some "refused" then
            Ok(Refused(answered, Refusal.NoDoseRules))
        else
            Ok(Evaluated answered)


    let offersOf rows generics main =
        main
        |> ServerApi.Adapters.NutritionRuleSets.linkedOffers routes (rulesOf rows) sets (evaluateWith generics)


open LinkedOffers


[<Tests>]
let linkedOfferTests =
    testList
        "the linked orders a narrowed context offers"
        [
            test "a feeding offers its supplement, as a supplement narrowed to its rule set" {
                feeding
                |> offersOf [| row |] [||]
                |> Array.map (fun pc -> pc.Context.Filter.Generic, pc.Category, pc.Context.Argumentation)
                |> Expect.equal
                    "Fantomalt"
                    [|
                        Some "Fantomalt pdr",
                        OrderCategory.Nutrition NutritionCategory.EnteralSupplement,
                        Some "Enterale Toevoeging"
                    |]
            }

            test "the morning dose of ataluren offers the evening dose, a medication" {
                morning
                |> offersOf [| row; ataluren |] [||]
                |> Array.map (fun pc -> pc.Category, pc.Context.Filter.DoseTypes)
                |> Expect.equal "the evening" [| OrderCategory.Medication, [| DoseType.Discontinuous "avond" |] |]
            }

            test "a rule without a generic offers one context per generic" {
                feeding
                |> offersOf [| { row with Generic = "" } |] [| "Fantomalt pdr"; "Liquigen pdr" |]
                |> Array.map _.Context.Filter.Generic
                |> Expect.equal "both" [| Some "Fantomalt pdr"; Some "Liquigen pdr" |]
            }

            test "an offer the rules refuse is left out" {
                feeding
                |> offersOf [| { row with Generic = "" } |] [| "Fantomalt pdr"; "refused" |]
                |> Array.map _.Context.Filter.Generic
                |> Expect.equal "Fantomalt only" [| Some "Fantomalt pdr" |]
            }

            test "an offer is evaluated for the main context's patient" {
                let pat = { Patient.patient with Department = Some "NEO" }

                { feeding with Context = { feeding.Context with Patient = pat } }
                |> offersOf [| row |] [||]
                |> Array.map _.Context.Patient.Department
                |> Expect.equal "NEO" [| Some "NEO" |]
            }

            test "a context that is not narrowed offers nothing" {
                { feeding with Context = { feeding.Context with Scenarios = [||] } }
                |> offersOf [| row |] [||]
                |> Expect.isEmpty "nothing"
            }

            test "an order no main side matches offers nothing" {
                morning |> offersOf [| row |] [||] |> Expect.isEmpty "nothing"
            }

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
                            | Informedica.GenPRES.Shared.Types.OrderContextResponse.Evaluated ctx ->
                                ctx.LinkedOrders |> Array.map _.Id
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

                    test "the commands that change the choices or the scenario" {
                        [
                            Informedica.GenPRES.Shared.Api.OrderViewCommand.ClearAllFilterProperty
                            Informedica.GenPRES.Shared.Api.OrderViewCommand.SelectNthOrderScenario 0
                            Informedica.GenPRES.Shared.Api.OrderViewCommand.SetNthDiluentProperty 0
                            Informedica.GenPRES.Shared.Api.OrderViewCommand.ResetOrderScenario
                        ]
                        |> List.map ServerApi.OrderContextCommand.offersLinked
                        |> Expect.equal "filter and scenario only" [ true; true; false; false ]
                    }
                ]
        ]
