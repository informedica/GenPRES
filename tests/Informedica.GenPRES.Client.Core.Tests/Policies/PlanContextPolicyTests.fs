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
                        test "an order that could not be calculated is locked, and shows as changed" {
                            let order = para

                            let marked =
                                { order with
                                    Scenarios =
                                        order.Scenarios |> Array.map (fun sc -> { sc with NotRecalculated = true })
                                }

                            PlanContextPolicy.locked (plan [| order |]) order
                            |> Expect.isFalse "an order calculated"
                            PlanContextPolicy.locked (plan [| marked |]) marked
                            |> Expect.isTrue "an order not calculated"

                            PlanContextPolicy.editingOf (plan [| order |]) order.Id
                            |> Expect.equal "the plan context rule" PlanContextPolicy.Editing.PlanContext

                            PlanContextPolicy.editingOf (plan [| marked |]) marked.Id
                            |> Expect.equal "read-only" PlanContextPolicy.Editing.Locked

                            PlanContextPolicy.changed order marked |> Expect.isTrue "the mark is a change"
                            PlanCellPolicy.stepable (plan [| marked |]) marked PlanCellPolicy.Column.Frequency
                            |> Expect.isNone "no step"
                            PlanContextPolicy.canEdit PlanContextPolicy.Editing.Locked "x"
                            |> Expect.isFalse "no field"
                            PlanContextPolicy.argues PlanContextPolicy.Editing.Locked
                            |> Expect.isFalse "no argumentation"
                        }

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

                            PlanContextPolicy.changed para { para with Totals = intake }
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


    /// Breast milk with the fortifier Nutrilon Nenatal BMF pdr linked to it, which the LinkRules sheet links
    /// with MainGeneric MM and Required yes, a second supplement Fantomalt linked to the same feeding, the
    /// morning and midday dose of ataluren with its evening dose linked, and paracetamol, linked to nothing.
    module LinkFixtures =

        let linked id name linkedTo = { context id name with LinkedTo = linkedTo }


        /// The context as it stands when it could not be calculated on the current patient data.
        let notCalculated (ctx: OrderContext) =
            { ctx with Scenarios = ctx.Scenarios |> Array.map (fun sc -> { sc with NotRecalculated = true }) }


        let breastMilk = linked "c-1" "MM" None
        let fortifier = linked "c-2" "Nutrilon Nenatal BMF pdr" (Some "c-1")
        let fantomalt = linked "c-3" "Fantomalt" (Some "c-1")
        let paracetamol = linked "c-4" "paracetamol" None
        let atalurenMain = linked "c-5" "ataluren" None
        let atalurenEvening = linked "c-6" "ataluren" (Some "c-5")


        /// The plan with every order calculated.
        let calculated =
            [|
                breastMilk
                fortifier
                fantomalt
                paracetamol
                atalurenMain
                atalurenEvening
            |]


        /// The plan with the context with the id not calculated.
        let withNotCalculated id =
            calculated
            |> Array.map (fun c -> if c.Id = id then notCalculated c else c)
            |> plan


        /// The context with the id in the plan.
        let find id (tp: OrderPlan) = tp.OrderContexts |> Array.find (fun c -> c.Id = id)


    open LinkFixtures
    open Informedica.GenPRES.Client.Core.StateMachines.OrderPlanMachine


    [<Tests>]
    let groupLockTests =
        testList
            "PlanContextPolicy group lock"
            [
                test "a supplement not calculated locks its feeding and the other supplement of the feeding" {
                    let tp = withNotCalculated "c-2"

                    [ "c-1"; "c-2"; "c-3"; "c-4"; "c-5"; "c-6" ]
                    |> List.map (fun id -> id, PlanContextPolicy.lock tp (find id tp))
                    |> Expect.equal
                        "the group of breast milk locked, the rest not"
                        [
                            "c-1", PlanContextPolicy.Lock.Group
                            "c-2", PlanContextPolicy.Lock.NotCalculated
                            "c-3", PlanContextPolicy.Lock.Group
                            "c-4", PlanContextPolicy.Lock.Unlocked
                            "c-5", PlanContextPolicy.Lock.Unlocked
                            "c-6", PlanContextPolicy.Lock.Unlocked
                        ]
                }

                test "a feeding not calculated locks the supplements linked to it" {
                    let tp = withNotCalculated "c-1"

                    [ "c-2"; "c-3" ]
                    |> List.map (fun id -> PlanContextPolicy.lock tp (find id tp))
                    |> Expect.equal "both by the group" [ PlanContextPolicy.Lock.Group; PlanContextPolicy.Lock.Group ]
                }

                test "the evening dose of ataluren not calculated locks the morning and midday dose" {
                    let tp = withNotCalculated "c-6"

                    PlanContextPolicy.locked tp (find "c-5" tp)
                    |> Expect.isTrue "the main order locked"
                }

                test "a plan with every order calculated locks nothing" {
                    let tp = plan calculated

                    tp.OrderContexts
                    |> Array.exists (PlanContextPolicy.locked tp)
                    |> Expect.isFalse "nothing locked"
                }

                test "an order locked by its group opens the order dialog locked, steps no cell and sends no command" {
                    let tp = withNotCalculated "c-2"
                    let feeding = find "c-1" tp

                    PlanContextPolicy.editingOf tp "c-1"
                    |> Expect.equal "the dialog locked" PlanContextPolicy.Editing.Locked

                    PlanCellPolicy.stepable tp feeding PlanCellPolicy.Column.Frequency
                    |> Expect.isNone "no cell steps"

                    OrderPlanChange.command
                        tp
                        (OrderPlanChange.OrderDialogCommand(
                            "c-1",
                            Informedica.GenPRES.Shared.Api.OrderViewCommand.ClearAllFilterProperty
                        ))
                    |> Expect.isNone "no command"

                    OrderPlanChange.command tp (OrderPlanChange.Remove [| "c-1" |])
                    |> Expect.isSome "the removal goes"
                }

                test "the intake slider of an order locked by its group cannot set the dose" {
                    let tp = withNotCalculated "c-2"

                    PlanContextPolicy.canSetIntake tp (find "c-1" tp) |> Expect.isFalse "locked"
                }
            ]
