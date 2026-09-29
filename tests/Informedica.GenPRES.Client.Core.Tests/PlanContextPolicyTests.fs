namespace Informedica.GenPRES.Client.Core.Tests


/// What an order context in the order plan can change and when, and the differences the sign
/// dialog lists.
module PlanContextPolicyTests =

    open Expecto
    open Expecto.Flip
    open Shared.Types
    open HeldContextPolicy


    /// An OrderScenario with its order id and its name set, every other field a default.
    let scenario = OrderPlanMachineTests.Fixtures.scenario


    let ten = { Shared.Models.Patient.Age.ageZero with Age.Years = 10<year> }
    let eleven = { Shared.Models.Patient.Age.ageZero with Age.Years = 11<year> }
    let patient = { Shared.Models.Patient.empty with Age = Some ten }

    let context id name =
        { Shared.Models.OrderContext.empty with
            Id = id
            Patient = patient
            Scenarios = [| scenario $"o-{id}" name |]
        }

    let plan contexts = Shared.Models.OrderPlan.create patient contexts

    /// One value in a unit, as an order variable holds it once it is solved.
    let valueIn (json: string) (v: decimal) =
        Some
            {
                Value = [| $"%M{v}", v |]
                Unit = "x/dag"
                Group = ""
                Short = true
                Language = ""
                Json = json
            }

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
    let planWithPatient (f: Patient -> Patient) (ctx: OrderContext) = { plan [| ctx |] with Patient = f ctx.Patient }

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

    let kinds (ds: (OrderContext * Difference)[]) = ds |> Array.map (fun (c, d) -> c.Id, d)


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
                    "matches"
                    [
                        test "equal patient data match" {
                            PlanContextPolicy.matches (plan [| para |]) para
                            |> Expect.isTrue "the context was calculated with the plan's patient"
                        }

                        test "only the age different still matches" {
                            let older = para |> planWithPatient (fun p -> { p with Age = Some eleven })

                            PlanContextPolicy.matches older para |> Expect.isTrue "the age is left out"
                        }

                        test "a different estimate beside a measured value still matches" {
                            let measured =
                                { para with
                                    Patient =
                                        { patient with
                                            Weight = { patient.Weight with Measured = Some 30000<gram> }
                                            Height = { patient.Height with Measured = Some 135<cm> }
                                        }
                                }

                            let estimated =
                                measured
                                |> planWithPatient (fun p ->
                                    { p with
                                        Weight = { p.Weight with Estimated = Some 35000<gram> }
                                        Height = { p.Height with Estimated = Some 140<cm> }
                                    }
                                )

                            PlanContextPolicy.matches estimated measured
                            |> Expect.isTrue "the doses rest on the measured values"
                        }

                        test "a different estimated weight without a measured one locks" {
                            let estimated =
                                para
                                |> planWithPatient (fun p ->
                                    { p with Weight = { p.Weight with Estimated = Some 35000<gram> } }
                                )

                            PlanContextPolicy.locked estimated para
                            |> Expect.isTrue "the doses rest on the estimated weight"
                        }

                        test "different P3 and P97 estimates still match" {
                            let bounds =
                                para
                                |> planWithPatient (fun p ->
                                    { p with
                                        Weight =
                                            { p.Weight with
                                                EstimatedP3 = Some 25000<gram>
                                                EstimatedP97 = Some 45000<gram>
                                            }
                                    }
                                )

                            PlanContextPolicy.matches bounds para |> Expect.isTrue "no dose rests on them"
                        }

                        test "a different weight locks" {
                            let heavier =
                                para
                                |> planWithPatient (fun p ->
                                    { p with Weight = { p.Weight with Measured = Some 30000<gram> } }
                                )

                            PlanContextPolicy.locked heavier para |> Expect.isTrue "the weight counts"
                        }

                        test "a different department locks" {
                            let moved = para |> planWithPatient (fun p -> { p with Department = Some "ICK" })

                            PlanContextPolicy.locked moved para |> Expect.isTrue "the department counts"
                        }

                        test "a different access locks" {
                            let access = para |> planWithPatient (fun p -> { p with Access = [ CVL ] })

                            PlanContextPolicy.locked access para |> Expect.isTrue "the access counts"
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

                        test "the argumentation alone changes it" {
                            PlanContextPolicy.changed para { para with Argumentation = Some "sepsis" }
                            |> Expect.isTrue "the argumentation is signed"
                        }

                        test "a filter difference alone does not change it" {
                            PlanContextPolicy.changed para { para with OrderContext.Filter.Indication = Some "pijn" }
                            |> Expect.isFalse "the filter is no change to an order in the plan"
                        }

                        test "an intake difference alone does not change it" {
                            let intake = { Shared.Models.Totals.empty with Volume = [| TextItem.Normal "10 mL" |] }

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
    let differenceTests =
        testList
            "HeldContextPolicy.differences"
            [
                test "an unchanged plan has no differences" {
                    differences opened (plan opened)
                    |> Expect.isEmpty "nothing new, changed or removed"
                }

                test "a new, a changed and a removed context, in plan order then removed" {
                    differences opened (plan [| amox; para |> withFrequency 3m |])
                    |> kinds
                    |> Expect.equal
                        "new and changed in plan order, then removed"
                        [| "c3", Difference.New; "c1", Difference.Changed; "c2", Difference.Removed |]
                }

                test "a removed row carries the opened context" {
                    differences opened (plan [| para |])
                    |> Array.map fst
                    |> Expect.equal "the context as opened" [| morf |]
                }

                test "on no version every context is new" {
                    differences [||] (plan opened)
                    |> kinds
                    |> Expect.equal "all new, in plan order" [| "c1", Difference.New; "c2", Difference.New |]
                }

                test "a new context that contributes no order is left out" {
                    differences opened (plan [| para; morf; tpn |])
                    |> Expect.isEmpty "nothing to list yet"
                }

                test "a context of the version that contributed no order is left out" {
                    differences [| para; morf; tpn |] (plan [| para; morf |])
                    |> Expect.isEmpty "it was never listed, so it is not removed"
                }

                test "a context of the version narrowed since is new" {
                    let narrowed = { tpn with Scenarios = [| scenario "o-c4" "tpn" |] }

                    differences [| para; tpn |] (plan [| para; narrowed |])
                    |> kinds
                    |> Expect.equal "its order enters the plan now" [| "c4", Difference.New |]
                }

                test "a filter difference alone is held and no difference" {
                    let filtered = { para with OrderContext.Filter.Indication = Some "pijn" }

                    let now = plan [| filtered; morf |]

                    (changed opened now, differences opened now)
                    |> Expect.equal
                        "the held rule sees the whole context, the difference rule the order"
                        ([| "c1" |], [||])
                }

                test "another medication in the same context is a change" {
                    differences opened (plan [| para |> withOrder (fun o -> { o with Id = "o-other" }); morf |])
                    |> kinds
                    |> Expect.equal "the context is changed" [| "c1", Difference.Changed |]
                }

                test "the argumentation alone is a change" {
                    differences opened (plan [| { para with Argumentation = Some "sepsis" }; morf |])
                    |> kinds
                    |> Expect.equal "the context is changed" [| "c1", Difference.Changed |]
                }

                test "a change of the plan's filtered rows or totals alone is no change" {
                    let now =
                        { plan opened with
                            Filtered = [| "c1" |]
                            Totals = { Shared.Models.Totals.empty with Volume = [| TextItem.Normal "10 mL" |] }
                        }

                    differences opened now
                    |> Expect.isEmpty "the rows shown and the totals are no order"
                }

                test "every context marked new or changed is held" {
                    let now = plan [| amox; para |> withDoseRate 2m; morf |]

                    let held = changed opened now

                    differences opened now
                    |> Array.filter (fun (_, d) -> d <> Difference.Removed)
                    |> Array.forall (fun (c, _) -> held |> Array.contains c.Id)
                    |> Expect.isTrue "the held rule sees each of them"
                }
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
                                PlanContextPolicy.Editing.Locked, "frequency", false
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
                    [
                        PlanContextPolicy.Editing.Workbench
                        PlanContextPolicy.Editing.PlanContext
                        PlanContextPolicy.Editing.Locked
                    ]
                    |> List.map PlanContextPolicy.resets
                    |> Expect.equal "only the workbench resets" [ true; false; false ]
                }

                test "the argumentation everywhere but a locked context" {
                    [
                        PlanContextPolicy.Editing.Workbench
                        PlanContextPolicy.Editing.PlanContext
                        PlanContextPolicy.Editing.Locked
                    ]
                    |> List.map PlanContextPolicy.argues
                    |> Expect.equal "a locked context commits nothing" [ true; true; false ]
                }

                test "a context with the plan's patient data follows the plan context rule" {
                    PlanContextPolicy.editingOf (plan [| para |]) para.Id
                    |> Expect.equal "not locked" PlanContextPolicy.Editing.PlanContext
                }

                test "a context with other patient data is locked" {
                    let moved = para |> planWithPatient (fun p -> { p with Department = Some "ICK" })

                    PlanContextPolicy.editingOf moved para.Id
                    |> Expect.equal "locked" PlanContextPolicy.Editing.Locked
                }

                test "an id the plan does not hold follows the plan context rule" {
                    PlanContextPolicy.editingOf (plan [| para |]) "c-unknown"
                    |> Expect.equal "nothing to lock" PlanContextPolicy.Editing.PlanContext
                }
            ]
