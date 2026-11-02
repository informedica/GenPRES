namespace Informedica.GenPRES.Client.Core.Tests.Policies


/// What an order context in the order plan can change and when.
module PlanContextPolicyTests =

    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Client.Core.Tests

    open Expecto
    open Expecto.Flip
    open Informedica.GenPRES.Client.Core.Tests.OrderFixtures
    open Informedica.GenPRES.Shared.Types


    /// An OrderScenario with its order id and its name set, every other field a default.
    let scenario = OrderFixtures.scenario


    let ten = { Informedica.GenPRES.Shared.Models.Patient.Age.ageZero with Age.Years = 10<year> }
    let eleven = { Informedica.GenPRES.Shared.Models.Patient.Age.ageZero with Age.Years = 11<year> }
    let patient = { Informedica.GenPRES.Shared.Models.Patient.empty with Age = Some ten }

    let context id name =
        { Informedica.GenPRES.Shared.Models.OrderContext.empty with
            Id = id
            Patient = patient
            Scenarios = [| scenario $"o-{id}" name |]
        }

    let plan contexts =
        Informedica.GenPRES.Shared.Models.OrderPlan.create patient contexts

    let valueOf = valueIn "x/day"

    /// The context with its contributed order changed by f.
    let withOrder (f: Order -> Order) (ctx: OrderContext) =
        { ctx with Scenarios = ctx.Scenarios |> Array.map (fun sc -> { sc with Order = f sc.Order }) }

    let withFrequency v =
        withOrder (fun o -> { o with Order.Schedule.Frequency.Variable.Vals = valueOf v })

    let withDoseQuantity v =
        withOrder (fun o -> { o with Order.Orderable.Dose.Quantity.Variable.Vals = valueOf v })

    let withDoseRate v =
        withOrder (fun o -> { o with Order.Orderable.Dose.Rate.Variable.Vals = valueOf v })

    /// A change of an order variable the plan context rule keeps fixed: the orderable quantity.
    let withOrderableQuantity v =
        withOrder (fun o -> { o with Order.Orderable.OrderableQuantity.Variable.Vals = valueOf v })

    /// The plan of the context, its patient data the context's own changed by f.

    let para = context "c1" "paracetamol"
    let morf = context "c2" "morfine"
    let amox = context "c3" "amoxicilline"

    /// A nutrition context in the plan before it is narrowed to one scenario: no order yet.
    let tpn =
        { context "c4" "tpn" with
            Category = OrderCategory.Nutrition NutritionCategory.TPN
            Scenarios = [||]
        }

    let opened = [| para; morf |]

    [<Tests>]
    let planContextTests =
        testList
            "PlanContextPolicy"
            [
                testList
                    "editable"
                    [
                        for field, exp in
                            [
                                QuantityModePolicy.Field.Frequency, true
                                QuantityModePolicy.Field.DoseQuantity, true
                                QuantityModePolicy.Field.DoseRate, true
                                QuantityModePolicy.Field.ComponentQuantity, false
                                QuantityModePolicy.Field.Other, false
                            ] do
                            test $"%A{field} is editable: %b{exp}" {
                                PlanContextPolicy.editable field |> Expect.equal "as the rule says" exp
                            }
                    ]

                testList
                    "changed"
                    [
                        test "an unchanged context is not changed" {
                            PlanContextPolicy.changed para para |> Expect.isFalse "nothing differs"
                        }

                        test "the frequency changes it" {
                            PlanContextPolicy.changed para (para |> withFrequency 3m)
                            |> Expect.isTrue "the frequency is editable"
                        }

                        test "the orderable dose quantity changes it" {
                            PlanContextPolicy.changed para (para |> withDoseQuantity 5m)
                            |> Expect.isTrue "the dose quantity is editable"
                        }

                        test "the orderable dose rate changes it" {
                            PlanContextPolicy.changed para (para |> withDoseRate 2m)
                            |> Expect.isTrue "the dose rate is editable"
                        }

                        testList
                            "a dose per kg moved by new patient data, the given amount the same"
                            [
                                let onItem v =
                                    withOrder (fun o ->
                                        let dose = (defaultFor<Dose>())
                                        let item =
                                            { defaultFor<Item>() with
                                                Dose = { dose with QuantityAdjust.Variable.Vals = valueIn "mg/kg" v }
                                            }

                                        { o with
                                            Order.Orderable.Components =
                                                [| { defaultFor<Component>() with Items = [| item |] } |]
                                        }
                                    )

                                let onComponent v =
                                    withOrder (fun o ->
                                        let dose = (defaultFor<Dose>())

                                        { o with
                                            Order.Orderable.Components =
                                                [|
                                                    { defaultFor<Component>() with
                                                        Dose =
                                                            { dose with
                                                                QuantityAdjust.Variable.Vals = valueIn "mg/kg" v
                                                            }
                                                    }
                                                |]
                                        }
                                    )

                                for name, perKg in [ "on an item", onItem; "on a component", onComponent ] do
                                    test name {
                                        let signed = para |> withDoseQuantity 150m |> perKg 10m

                                        PlanContextPolicy.changed signed (signed |> perKg 7.5m)
                                        |> Expect.isTrue "10 mg/kg is now 7.5 mg/kg"

                                        PlanContextPolicy.changed signed (signed |> perKg 10m)
                                        |> Expect.isFalse "corrected back"
                                    }
                            ]

                        test "a new notification changes it, no dose moved" {
                            para
                            |> withOrder (fun o -> { o with Order.Orderable.Dose.Quantity.Level = IsAlert })
                            |> PlanContextPolicy.changed para
                            |> Expect.isTrue "the alert is new"
                        }

                        test "the rules no longer applying changes it" {
                            let noticed =
                                { para with
                                    Scenarios =
                                        para.Scenarios |> Array.map (fun sc -> { sc with RulesNoLongerApply = true })
                                }

                            PlanContextPolicy.changed para noticed |> Expect.isTrue "the notice is new"
                        }

                        test "the argumentation alone changes it" {
                            PlanContextPolicy.changed para { para with Argumentation = Some "sepsis" }
                            |> Expect.isTrue "the argumentation is signed"
                        }

                        test "a filter difference alone does not change it" {
                            PlanContextPolicy.changed para { para with OrderContext.Filter.Indication = Some "pijn" }
                            |> Expect.isFalse "the filter is no change to an order in the plan"
                        }

                        test "an intake difference alone does not change it" {
                            let intake =
                                { Informedica.GenPRES.Shared.Models.Totals.empty with
                                    Volume = [| TextItem.Normal "10 mL" |]
                                }

                            PlanContextPolicy.changed para { para with Intake = intake }
                            |> Expect.isFalse "the intake is no change to an order in the plan"
                        }

                        test "the same values rendered otherwise do not change it" {
                            let rendered =
                                para
                                |> withFrequency 3m
                                |> withOrder (fun o ->
                                    { o with
                                        Order.Schedule.Frequency.Variable.Vals =
                                            o.Schedule.Frequency.Variable.Vals
                                            |> Option.map (fun vu ->
                                                { vu with
                                                    Unit = "x/day"
                                                    Language = "en"
                                                }
                                            )
                                    }
                                )

                            PlanContextPolicy.changed (para |> withFrequency 3m) rendered
                            |> Expect.isFalse "only the numbers and the unit count"
                        }

                        test "the same number in another unit of the group changes it" {
                            let inMg =
                                para
                                |> withOrder (fun o ->
                                    { o with Order.Orderable.Dose.Quantity.Variable.Vals = valueIn "mg" 5m }
                                )

                            let inG =
                                para
                                |> withOrder (fun o ->
                                    { o with Order.Orderable.Dose.Quantity.Variable.Vals = valueIn "g" 5m }
                                )

                            PlanContextPolicy.changed inMg inG |> Expect.isTrue "5 mg is not 5 g"
                        }

                        test "another order with the same values changes it" {
                            let other = para |> withOrder (fun o -> { o with Id = "o-other" })

                            PlanContextPolicy.changed para other
                            |> Expect.isTrue "another medication in the same context is a change"
                        }

                        test "a variable the rule keeps fixed does not change it" {
                            PlanContextPolicy.changed para (para |> withOrderableQuantity 10m)
                            |> Expect.isFalse "only the editable variables count"
                        }
                    ]
            ]


    [<Tests>]
    let editingTests =
        testList
            "PlanContextPolicy editing"
            [
                testList
                    "canEdit"
                    [
                        for editing, key, exp in
                            [
                                PlanContextPolicy.Editing.Workbench, "substDoseQty", true
                                PlanContextPolicy.Editing.Workbench, "compOrdQty", true
                                PlanContextPolicy.Editing.PlanContext, "frequency", true
                                PlanContextPolicy.Editing.PlanContext, "ordDoseQty", true
                                PlanContextPolicy.Editing.PlanContext, "ordDoseRate", true
                                PlanContextPolicy.Editing.PlanContext, "compOrdQty", false
                                PlanContextPolicy.Editing.PlanContext, "substDoseQty", false
                            ] do
                            test $"%A{editing} edits %s{key}: %b{exp}" {
                                PlanContextPolicy.canEdit editing key |> Expect.equal "as the rule says" exp
                            }
                    ]

                test "an unknown key is Other" {
                    PlanContextPolicy.fieldKind "time"
                    |> Expect.equal "no step command for it" QuantityModePolicy.Field.Other
                }

                test "Reset on the workbench alone" {
                    [ PlanContextPolicy.Editing.Workbench; PlanContextPolicy.Editing.PlanContext ]
                    |> List.map PlanContextPolicy.resets
                    |> Expect.equal "only the workbench resets" [ true; false ]
                }
            ]
