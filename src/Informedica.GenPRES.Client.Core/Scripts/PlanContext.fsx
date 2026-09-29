// What an order context in the order plan can change and when, and the differences the sign
// dialog lists. Prototype of PlanContextPolicy and HeldContextPolicy.differences; run it with
// the FSI MCP server or dotnet fsi from this directory.

#I __SOURCE_DIRECTORY__

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

#load "load.fsx"
#r "nuget: Expecto"


open Shared.Types


/// Decides what an order context in the order plan can change and when: only the frequency, the
/// orderable dose quantity and the orderable dose rate, and only while the patient data it was
/// calculated with match the plan's.
module PlanContextPolicy =

    /// Whether a field of an order context in the plan can change: the frequency, the orderable
    /// dose quantity and the orderable dose rate can; every other field cannot.
    let editable (field: QuantityModePolicy.Field) =
        match field with
        | QuantityModePolicy.Field.Frequency
        | QuantityModePolicy.Field.DoseQuantity
        | QuantityModePolicy.Field.DoseRate -> true
        | QuantityModePolicy.Field.ComponentQuantity
        | QuantityModePolicy.Field.Other -> false


    /// The patient data the orders rest on: the age is blanked, since it changes by itself as
    /// time passes, and so are the P3 and P97 estimates. The estimated weight and height are
    /// blanked only when a measured value exists; without one, the estimate is what the doses
    /// were calculated with, so a corrected age that changes it still counts.
    let entered (p: Patient) =
        { p with
            Age = None
            Weight =
                { p.Weight with
                    EstimatedP3 = None
                    Estimated = if p.Weight.Measured.IsSome then None else p.Weight.Estimated
                    EstimatedP97 = None
                }
            Height =
                { p.Height with
                    EstimatedP3 = None
                    Estimated = if p.Height.Measured.IsSome then None else p.Height.Estimated
                    EstimatedP97 = None
                }
        }


    /// Whether the patient data an order context was calculated with match the plan's patient
    /// data: the entered data and the estimates the doses rest on, the age aside.
    let matches (plan: OrderPlan) (ctx: OrderContext) = entered ctx.Patient = entered plan.Patient


    /// Whether an order context in the plan is locked: its patient data do not match the plan's.
    let locked (plan: OrderPlan) (ctx: OrderContext) = matches plan ctx |> not


    /// The values of an order variable as numbers with their unit group, leaving out how they
    /// are rendered: the unit text, the language and the JSON.
    let values (ovar: OrderVariable) =
        ovar.Variable.Vals |> Option.map (fun vu -> vu.Value |> Array.map snd, vu.Group)


    /// The values of the editable order variables of the order a context contributes; nothing
    /// when it contributes no order.
    let editableValues (ctx: OrderContext) =
        Shared.Models.OrderContext.contribution ctx
        |> Option.map (fun sc ->
            [
                sc.Order.Schedule.Frequency |> values
                sc.Order.Orderable.Dose.Quantity |> values
                sc.Order.Orderable.Dose.Rate |> values
            ]
        )


    /// Whether an order context in the plan changed against its opened or signed version: one of
    /// the editable variables or the argumentation differs.
    let changed (opened: OrderContext) (ctx: OrderContext) =
        editableValues opened <> editableValues ctx
        || opened.Argumentation <> ctx.Argumentation


module HeldContextPolicy =

    open HeldContextPolicy


    /// How an order context differs from the version last opened or signed.
    [<RequireQualifiedAccess>]
    type Difference =
        /// The version did not hold it.
        | New
        /// Its editable variables or its argumentation changed.
        | Changed
        /// The version held it, and the plan no longer does.
        | Removed


    /// The order contexts new or changed since the version last opened or signed, in plan order,
    /// followed by the contexts of that version no longer in the plan. Contexts are compared by
    /// id. A context that contributes no order, a nutrition context not yet narrowed to one
    /// scenario, has nothing to list and is left out, on either side.
    let differences (opened: OrderContext[]) (plan: OrderPlan) =
        let contributing (ctxs: OrderContext[]) =
            ctxs |> Array.filter (Shared.Models.OrderContext.contribution >> Option.isSome)

        let byId id (ctxs: OrderContext[]) = ctxs |> Array.tryFind (fun c -> c.Id = id)

        let opened = contributing opened
        let now = contributing plan.OrderContexts

        let current =
            now
            |> Array.choose (fun ctx ->
                match opened |> byId ctx.Id with
                | None -> Some(ctx, Difference.New)
                | Some o when PlanContextPolicy.changed o ctx -> Some(ctx, Difference.Changed)
                | Some _ -> None
            )

        let removed =
            opened
            |> Array.filter (fun o -> now |> byId o.Id |> Option.isNone)
            |> Array.map (fun o -> o, Difference.Removed)

        Array.append current removed


    // After migration, OrderPlanState gets a differences function beside OrderPlanState.changed,
    // taking the plan and applying HeldContextPolicy.differences to the state's Opened contexts
    // and that plan. The test file reuses the scenario fixture of the order plan machine tests.


module Tests =

    open System
    open Expecto
    open Expecto.Flip
    open HeldContextPolicy


    /// An OrderScenario with its order id and its name set, every other field a default (built
    /// by reflection: the order graph is too deep to write by hand).
    let scenario (id: string) (name: string) : OrderScenario =
        let rec defaultOf (t: Type) : obj =
            if t = typeof<string> then
                box ""
            elif t = typeof<bool> then
                box false
            elif t = typeof<int> then
                box 0
            elif t = typeof<decimal> then
                box 0m
            elif t = typeof<float> then
                box 0.0
            elif t = typeof<DateTime> then
                box DateTime.MinValue
            elif t.IsArray then
                box (Array.CreateInstance(t.GetElementType(), 0))
            elif t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>> then
                null
            elif Reflection.FSharpType.IsRecord t then
                Reflection.FSharpValue.MakeRecord(
                    t,
                    Reflection.FSharpType.GetRecordFields t
                    |> Array.map (fun f -> defaultOf f.PropertyType)
                )
            elif Reflection.FSharpType.IsUnion t then
                let case = (Reflection.FSharpType.GetUnionCases t)[0]

                Reflection.FSharpValue.MakeUnion(
                    case,
                    case.GetFields() |> Array.map (fun f -> defaultOf f.PropertyType)
                )
            else
                null

        let sc = defaultOf typeof<OrderScenario> :?> OrderScenario

        { sc with
            Name = name
            Order = { sc.Order with Id = id }
        }


    let ten = { Shared.Models.Patient.Age.ageZero with Age.Years = 10<year> }
    let eleven = { Shared.Models.Patient.Age.ageZero with Age.Years = 11<year> }
    let patient = { Shared.Models.Patient.empty with Age = Some ten }

    let context id name =
        { Shared.Models.OrderContext.empty with
            Id = id
            Patient = patient
            Scenarios = [| scenario $"o-{id}" name |]
        }

    let plan contexts =
        Shared.Models.OrderPlan.create patient contexts

    /// One value in a unit, as an order variable holds it once it is solved.
    let valueOf (v: decimal) =
        Some
            {
                Value = [| $"%M{v}", v |]
                Unit = "x/dag"
                Group = ""
                Short = true
                Language = ""
                Json = ""
            }

    /// The context with its contributed order changed by f.
    let withOrder (f: Order -> Order) (ctx: OrderContext) =
        { ctx with
            Scenarios = ctx.Scenarios |> Array.map (fun sc -> { sc with Order = f sc.Order })
        }

    let withFrequency v =
        withOrder (fun o ->
            { o with
                Order.Schedule.Frequency.Variable.Vals = valueOf v
            }
        )

    let withDoseQuantity v =
        withOrder (fun o ->
            { o with
                Order.Orderable.Dose.Quantity.Variable.Vals = valueOf v
            }
        )

    let withDoseRate v =
        withOrder (fun o ->
            { o with
                Order.Orderable.Dose.Rate.Variable.Vals = valueOf v
            }
        )

    /// A change of an order variable the plan context rule keeps fixed: the orderable quantity.
    let withOrderableQuantity v =
        withOrder (fun o ->
            { o with
                Order.Orderable.OrderableQuantity.Variable.Vals = valueOf v
            }
        )

    /// The plan of the context, its patient data the context's own changed by f.
    let planWithPatient (f: Patient -> Patient) (ctx: OrderContext) =
        { plan [| ctx |] with Patient = f ctx.Patient }

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
                                    { p with
                                        Weight = { p.Weight with Estimated = Some 35000<gram> }
                                    }
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
                                    { p with
                                        Weight = { p.Weight with Measured = Some 30000<gram> }
                                    }
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
                            PlanContextPolicy.changed
                                para
                                { para with
                                    OrderContext.Filter.Indication = Some "pijn"
                                }
                            |> Expect.isFalse "the filter is no change to an order in the plan"
                        }

                        test "an intake difference alone does not change it" {
                            let intake =
                                { Shared.Models.Totals.empty with
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
                                                    Json = "{}"
                                                }
                                            )
                                    }
                                )

                            PlanContextPolicy.changed (para |> withFrequency 3m) rendered
                            |> Expect.isFalse "only the numbers and the unit group count"
                        }

                        test "a variable the rule keeps fixed does not change it" {
                            PlanContextPolicy.changed para (para |> withOrderableQuantity 10m)
                            |> Expect.isFalse "only the editable variables count"
                        }
                    ]
            ]


    let differenceTests =
        testList
            "HeldContextPolicy.differences"
            [
                test "an unchanged plan has no differences" {
                    differences opened (plan opened) |> Expect.isEmpty "nothing new, changed or removed"
                }

                test "a new, a changed and a removed context, in plan order then removed" {
                    differences opened (plan [| amox; para |> withFrequency 3m |])
                    |> kinds
                    |> Expect.equal
                        "new and changed in plan order, then removed"
                        [|
                            "c3", Difference.New
                            "c1", Difference.Changed
                            "c2", Difference.Removed
                        |]
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
                    differences opened (plan [| para; morf; tpn |]) |> Expect.isEmpty "nothing to list yet"
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
                    let filtered =
                        { para with
                            OrderContext.Filter.Indication = Some "pijn"
                        }

                    let now = plan [| filtered; morf |]

                    (changed opened now, differences opened now)
                    |> Expect.equal
                        "the held rule sees the whole context, the difference rule the order"
                        ([| "c1" |], [||])
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
                            Totals =
                                { Shared.Models.Totals.empty with
                                    Volume = [| TextItem.Normal "10 mL" |]
                                }
                        }

                    differences opened now |> Expect.isEmpty "the rows shown and the totals are no order"
                }

                test "every context marked new or changed is held" {
                    let now =
                        plan
                            [|
                                amox
                                para |> withDoseRate 2m
                                morf
                            |]

                    let held = changed opened now

                    differences opened now
                    |> Array.filter (fun (_, d) -> d <> Difference.Removed)
                    |> Array.forall (fun (c, _) -> held |> Array.contains c.Id)
                    |> Expect.isTrue "the held rule sees each of them"
                }
            ]


    let tests = testList "PlanContext" [ planContextTests; differenceTests ]


open Expecto

Tests.tests |> runTestsWithCLIArgs [] [||]
