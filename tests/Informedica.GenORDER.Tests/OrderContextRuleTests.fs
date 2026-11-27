/// The rule sets and the link rules on an order context: the rule sets of a patient, the rule set of a plan
/// context, the evaluation settled to it, the dose rules that need a main order, and the linked orders a
/// narrowed context offers.
module OrderContextRuleTests

open Expecto
open Expecto.Flip
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


/// A feeding rule beside a medication rule, with the rule set of the feeding.
module FeedingFixtures =

    let rowOf generic indication =
        let r = OrderPlanTests.RuleFixtures.row "ICK"

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
            [||]
            data
        |> fst


    /// The rule set rows of a feeding under "voeding", expanded over the dose rules as the resource
    /// expands them.
    let feedingRows =
        [|
            OrderPlanTests.EvaluateFixtures.ruleSetRow "EnteralFeeding" "Enterale Voeding" "voeding" ""
        |]
        |> RuleSet.expand (RuleSet.DoseRuleNames.ofData data)


    /// The rules above with the rule set rows of a feeding, every rule one that can be ordered on its own; the
    /// totals data empty.
    type FeedingRuleSet() =
        inherit OrderPlanTests.RulesOf(rules)

        interface Resources.IResourceProvider with
            member _.Get(key: Resources.ResourceKey<'T>) : 'T =
                if key.Name = Resources.Keys.ruleSetData.Name then
                    unbox<'T>(box feedingRows)
                elif key.Name = Resources.Keys.standAloneDoseRules.Name then
                    unbox<'T>(box rules)
                elif key.Name = Resources.Keys.isAdditive.Name then
                    unbox<'T>(box (fun (_: string) -> false))
                elif key.Name = Resources.Keys.productPredilutions.Name then
                    unbox<'T>(box Array.empty<ProductPredilution>)
                else
                    raise (System.Collections.Generic.KeyNotFoundException key.Name)

            member _.GetDoseRules() = rules
            member _.GetRouteMappings() = OrderPlanTests.RuleFixtures.routeMapping
            member _.GetTotals() = [||]


    let provider = FeedingRuleSet() :> Resources.IResourceProvider

    let patient = OrderPlanTests.RuleFixtures.patient (Some 5N) false (Some 20N) None


    /// An empty context of the category for the patient.
    let empty category =
        {
            Filter = OrderContext.emptyFilter
            PatientData = patient
            Scenarios = [||]
            Argumentation = None
        }
        |> PlanContext.create "" category


    /// A context of the category for the patient with a generic the rules do not offer.
    let unknownGeneric category =
        let pc = empty category
        { pc with Context = { pc.Context with Filter = { OrderContext.emptyFilter with Generic = Some "onbekend" } } }


    /// The generics an evaluation offers, evaluated or refused.
    let generics answer =
        answer
        |> Result.map (
            function
            | Evaluated(pc: PlanContext)
            | Refused(pc, _) -> pc.Context.Filter.Generics
        )


/// Link rules of a supplement to a feeding and of an evening dose to a morning dose, with an evaluation that
/// does not need dose rules.
module LinkedOfferFixtures =

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


    let sets =
        [|
            OrderPlanTests.Fixtures.nutritionRuleSet
                NutritionCategory.EnteralFeeding
                "Enterale Voeding"
                [| "Enterale voeding" |]
                [| "Nutrini (Nutricia)" |]
            OrderPlanTests.Fixtures.nutritionRuleSet
                NutritionCategory.EnteralSupplement
                "Enterale Toevoeging"
                [| "Enterale toevoeging" |]
                [||]
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
    /// A list of one dose type is kept, as the rule lookup reads it as the pick. A context whose generic
    /// is "refused" is refused.
    let evaluateWith generics (set: RuleSet option) (pc: PlanContext) =
        let f = pc.Context.Filter

        let answered =
            { pc with
                Context =
                    { pc.Context with
                        Filter =
                            { f with
                                Generics = f.Generic |> Option.map Array.singleton |> Option.defaultValue generics
                                DoseTypes = if f.DoseTypes.Length = 1 then f.DoseTypes else doseTypes
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
        |> OrderPlan.linkedOffers routes (rulesOf rows) sets (evaluateWith generics)


/// The three doses of week 1 of the oral hydrocortison taper, with link rules that make a dose need the dose
/// of 7:00 as its main order.
module RequiredLinkFixtures =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open OrderPlanTests


    let routes: RouteMapping[] =
        [|
            {
                Long = "ORAAL"
                Short = "or"
            }
        |]


    let indication = "Afbouwschema week 1"


    /// The oral hydrocortison rule of week 1 of the taper, with the dose text of the time of day.
    let rowOf doseText : DoseRuleData =
        let r = RuleFixtures.row "ICK"

        { r with
            Generic = { r.Generic with Name = "hydrocortison" }
            Indication = indication
            Route = "ORAAL"
            ScheduleData =
                { r.ScheduleData with
                    DoseType = "discontinuous"
                    DoseText = doseText
                    Freqs = [| 1N |]
                    FreqUnit = "dag"
                    DoseLimitData =
                        { r.ScheduleData.DoseLimitData with
                            Component = "hydrocortison"
                            Substance = "hydrocortison"
                        }
                }
        }


    /// A tablet of 10 mg hydrocortison, so that a dose step can be solved.
    let tablet =
        let stuk = Units.General.general "stuk"
        let perStuk = Units.Mass.milliGram |> ValueUnit.per stuk
        let p = Product.create "hydrocortison" "tablet" "ORAAL" [| "hydrocortison" |]

        { p with
            FormQuantities = 1N |> ValueUnit.singleWithUnit stuk
            Divisible = Some 1N
            Substances =
                p.Substances
                |> Array.map (fun s -> { s with Concentration = 10N |> ValueUnit.singleWithUnit perStuk |> Some })
        }


    /// The three doses of week 1 of the taper, as the dose rules of the sheet give them.
    let doseRules =
        [|
            rowOf "week 1 7:00 uur"
            rowOf "week 1 13:00 uur"
            rowOf "week 1 18:00 uur"
        |]
        |> DoseRuleLoader.fromData routes [||] [| tablet |] [||]
        |> fst


    /// A LinkRules row of week 1 of the taper: the dose at the time of day linked to the dose of 7:00.
    let linkRow doseText required : LinkRuleData =
        {
            Generic = "hydrocortison"
            Route = "ORAAL"
            Indication = indication
            DoseType = "discontinuous"
            DoseText = doseText
            MainGeneric = "hydrocortison"
            MainRoute = "ORAAL"
            MainIndication = indication
            MainDoseType = "discontinuous"
            MainDoseText = "week 1 7:00 uur"
            Required = required
        }


    /// The two rows of the sheet: the doses of 13:00 and 18:00 cannot be ordered without the dose of 7:00.
    let sheetRows = [| linkRow "week 1 13:00 uur" "yes"; linkRow "week 1 18:00 uur" "yes" |]


    /// The rows of the sheet with the dose of 18:00 changed to a dose that can also be ordered on its own,
    /// a variant made for the test.
    let standAloneRows = [| linkRow "week 1 13:00 uur" "yes"; linkRow "week 1 18:00 uur" "no" |]


    let linkRules rows =
        rows |> LinkRule.ofRows |> Result.defaultWith (fun e -> failtest $"%A{e}")


    /// The dose rules above with the link rules of the rows; no rule sets and no totals data.
    type Linked(rows) =
        inherit RulesOf(doseRules)

        interface Resources.IResourceProvider with
            member _.Get(key: Resources.ResourceKey<'T>) : 'T =
                if key.Name = Resources.Keys.linkRules.Name then
                    unbox<'T>(box (linkRules rows))
                elif key.Name = Resources.Keys.standAloneDoseRules.Name then
                    unbox<'T>(box (doseRules |> LinkRule.standAlone routes (linkRules rows)))
                elif key.Name = Resources.Keys.ruleSetData.Name then
                    unbox<'T>(box Array.empty<RuleSetData>)
                elif key.Name = Resources.Keys.isAdditive.Name then
                    unbox<'T>(box (fun (_: string) -> false))
                elif key.Name = Resources.Keys.productPredilutions.Name then
                    unbox<'T>(box Array.empty<ProductPredilution>)
                else
                    raise (System.Collections.Generic.KeyNotFoundException key.Name)

            member _.GetDoseRules() = doseRules
            member _.GetRouteMappings() = routes
            member _.GetTotals() = [||]


    let provider rows = Linked rows :> Resources.IResourceProvider


    let doseTexts (rules: DoseRule[]) = rules |> Array.map (_.DoseType >> DoseType.getText)


    let texts doseTypes = doseTypes |> Array.map DoseType.getText


    /// The oral hydrocortison context of week 1 of the taper, its dose type not yet picked.
    let hydrocortison provider =
        let pat = RuleFixtures.patient (Some 8N) false (Some 25N) None
        let ctx = OrderContext.create OrderLogging.noOp provider pat

        { ctx with
            Filter =
                { ctx.Filter with
                    Indication = Some indication
                    Generic = Some "hydrocortison"
                    Route = Some "ORAAL"
                }
        }


    /// The dose types the oral hydrocortison context of week 1 of the taper offers to pick from.
    let offeredDoseTypes provider =
        hydrocortison provider
        |> OrderContext.getRules OrderLogging.noOp provider
        |> fst
        |> _.Filter.DoseTypes
        |> texts


/// A provider whose rule set data hold a feeding for the department ICK and one for NEO.
module DepartmentFixtures =

    let row = OrderPlanTests.EvaluateFixtures.ruleSetRow


    /// A provider whose rule set rows hold a feeding for the department ICK and one for NEO.
    type DepartmentRuleSet() =
        inherit OrderPlanTests.RulesOf([||])

        interface Resources.IResourceProvider with
            member _.Get(key: Resources.ResourceKey<'T>) : 'T =
                if key.Name = Resources.Keys.ruleSetData.Name then
                    unbox<'T>(
                        box
                            [|
                                { row "EnteralFeeding" "Enterale Voeding" "voeding" "MM" with
                                    RuleSetData.Department = Some "ICK"
                                }
                                { row "EnteralFeeding" "Enterale Voeding" "voeding" "Nutrini" with
                                    RuleSetData.Department = Some "NEO"
                                }
                            |]
                    )
                else
                    raise (System.Collections.Generic.KeyNotFoundException key.Name)


open FeedingFixtures
open LinkedOfferFixtures


let tests =
    testList
        "the rule sets and the link rules on an order context"
        [
            testList
                "Api.getRuleSets"
                [
                    test "a patient without a department gets only the rules without a department" {
                        PatientData.unknown
                        |> Api.getRuleSets (DepartmentFixtures.DepartmentRuleSet())
                        |> Array.collect _.Rows
                        |> Expect.isEmpty "no ICK or NEO feeding"
                    }

                    test "a patient of a department gets the rules of that department" {
                        PatientData.unknown
                        |> SamplePatients.withDepartment (Some "NEO")
                        |> Api.getRuleSets (DepartmentFixtures.DepartmentRuleSet())
                        |> Array.collect _.Rows
                        |> Array.map _.Generic
                        |> Expect.equal "the NEO feeding" [| "Nutrini" |]
                    }
                ]

            testList
                "PlanContext.ruleSetOf"
                [
                    test "a nutrition category of a department without rules of the category is refused" {
                        let hck = PatientData.unknown |> SamplePatients.withDepartment (Some "HCK")

                        PlanContext.ruleSetOf
                            (DepartmentFixtures.DepartmentRuleSet())
                            hck
                            (Some NutritionCategory.EnteralFeeding)
                        |> Expect.equal "no rule set" (Error(OrderPlanError.NoRuleSet NutritionCategory.EnteralFeeding))
                    }

                    test "a medication has no rule sets and no rule set" {
                        PlanContext.ruleSetOf (DepartmentFixtures.DepartmentRuleSet()) PatientData.unknown None
                        |> Expect.equal "none" (Ok([||], None))
                    }
                ]

            testList
                "PlanContext.evaluateSettled"
                [
                    let feedingCategory = OrderCategory.Nutrition NutritionCategory.EnteralFeeding

                    let feedingSet =
                        PlanContext.ruleSetOf provider patient (Some NutritionCategory.EnteralFeeding)
                        |> Result.map snd
                        |> Result.defaultValue None

                    // evaluated as the server evaluates a context: over the dose rules of the context as sent
                    let evaluate set (pc: PlanContext) =
                        pc
                        |> PlanContext.evaluateSettled
                            Scenarios.testStart
                            OrderLogging.noOp
                            (provider |> OrderContext.withDoseRulesOf pc.Context)
                            set
                            OrderContext.UpdateOrderContext

                    test "an evaluated feeding context is narrowed to the generics of its rule set" {
                        empty feedingCategory
                        |> evaluate feedingSet
                        |> generics
                        |> Expect.equal "voedsel, not paracetamol" (Ok [| "voedsel" |])
                    }

                    test "the one generic and indication the narrowing leaves are chosen" {
                        match empty feedingCategory |> evaluate feedingSet with
                        | Ok(Evaluated pc) ->
                            (pc.Context.Filter.Indication, pc.Context.Filter.Generic)
                            |> Expect.equal "voeding and voedsel" (Some "voeding", Some "voedsel")
                        | other -> failtest $"expected an evaluated context, got %A{other}"
                    }

                    test "a refused feeding context offers only the generics of the rule set, its pick as sent" {
                        match unknownGeneric feedingCategory |> evaluate feedingSet with
                        | Ok(Refused(pc, _)) ->
                            (pc.Context.Filter.Generics, pc.Context.Filter.Generic)
                            |> Expect.equal "voedsel, not paracetamol" ([| "voedsel" |], Some "onbekend")
                        | other -> failtest $"expected a refusal, got %A{other}"
                    }

                    test "a refused medication context is not narrowed" {
                        match unknownGeneric OrderCategory.Medication |> evaluate None with
                        | Ok(Refused(pc, _)) ->
                            pc.Context.Filter.Generics |> Expect.equal "both" [| "paracetamol"; "voedsel" |]
                        | other -> failtest $"expected a refusal, got %A{other}"
                    }

                    test "a medication context is not narrowed" {
                        empty OrderCategory.Medication
                        |> evaluate None
                        |> generics
                        |> Expect.equal "both" (Ok [| "paracetamol"; "voedsel" |])
                    }
                ]

            testList
                "OrderContext.doseRulesOf"
                [
                    let provider = RequiredLinkFixtures.provider RequiredLinkFixtures.sheetRows
                    let hydrocortison = RequiredLinkFixtures.hydrocortison provider
                    let doseTexts = RequiredLinkFixtures.doseTexts

                    let withDoseTypes doseTypes (ctx: OrderContext) =
                        { ctx with Filter = { ctx.Filter with DoseTypes = doseTypes } }

                    test "the context with the generic picked has the stand-alone dose rules: only 7:00" {
                        hydrocortison
                        |> OrderContext.doseRulesOf provider
                        |> doseTexts
                        |> Expect.equal "7:00" [| "week 1 7:00 uur" |]
                    }

                    test "an offer of 13:00 has the stand-alone dose rules and the dose rule of 13:00" {
                        hydrocortison
                        |> withDoseTypes [| DoseType.Discontinuous "week 1 13:00 uur" |]
                        |> OrderContext.doseRulesOf provider
                        |> doseTexts
                        |> Expect.equal "7:00 and 13:00" [| "week 1 7:00 uur"; "week 1 13:00 uur" |]
                    }

                    test "a pick of 13:00 over the list of 7:00 has the stand-alone dose rules: only 7:00" {
                        let ctx = hydrocortison |> withDoseTypes [| DoseType.Discontinuous "week 1 7:00 uur" |]

                        { ctx with
                            Filter = { ctx.Filter with DoseType = Some(DoseType.Discontinuous "week 1 13:00 uur") }
                        }
                        |> OrderContext.doseRulesOf provider
                        |> doseTexts
                        |> Expect.equal "7:00" [| "week 1 7:00 uur" |]
                    }

                    test "the wrapped provider gives the dose rules of the context, the provider itself every dose" {
                        (provider |> OrderContext.withDoseRulesOf hydrocortison).GetDoseRules()
                        |> doseTexts
                        |> Expect.equal "7:00" [| "week 1 7:00 uur" |]

                        provider.GetDoseRules()
                        |> doseTexts
                        |> Expect.equal "all three" [| "week 1 7:00 uur"; "week 1 13:00 uur"; "week 1 18:00 uur" |]
                    }
                ]

            testList
                "OrderPlan.linkedOffers"
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
                        |> Expect.equal
                            "the evening"
                            [| OrderCategory.Medication, [| DoseType.Discontinuous "avond" |] |]
                    }

                    test "an offer whose evaluation finds only another dose type is left out" {
                        // the patient has only the morning rule, which the evaluation picks with its scenarios
                        let morningOnly set pc =
                            evaluateWith [||] set pc
                            |> Result.map (
                                Outcome.map (fun (pc: PlanContext) ->
                                    { pc with
                                        Context =
                                            { morning.Context with
                                                Filter =
                                                    { pc.Context.Filter with
                                                        DoseTypes = [| DoseType.Discontinuous "ochtend middag" |]
                                                        DoseType = Some(DoseType.Discontinuous "ochtend middag")
                                                    }
                                            }
                                    }
                                )
                            )

                        morning
                        |> OrderPlan.linkedOffers routes (rulesOf [| ataluren |]) sets morningOnly
                        |> Expect.isEmpty "no evening offer"
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
                        let pat = PatientData.unknown |> SamplePatients.withDepartment (Some "NEO")

                        { feeding with Context = { feeding.Context with PatientData = pat } }
                        |> offersOf [| row |] [||]
                        |> Array.map (_.Context.PatientData >> PatientData.department)
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
                ]
        ]
