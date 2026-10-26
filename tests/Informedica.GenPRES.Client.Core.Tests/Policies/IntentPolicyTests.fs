namespace Informedica.GenPRES.Client.Core.Tests.Policies


/// Which intent case is allowed on a wire order: every recalculation case on every dose type, a
/// field with several values or a range, the component dose the rules do not constrain, the cases
/// that are never allowed, a variable found by name, and which commands reopen.
module IntentPolicyTests =

    open Expecto
    open Expecto.Flip
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api
    open Informedica.GenPRES.Client.Core.Policies


    module Ctx = Informedica.GenPRES.Shared.Models.OrderContext


    let defaultFor<'T> () =
        Informedica.GenPRES.Client.Core.Tests.OrderFixtures.defaultFor<'T>()


    let vu values : ValueUnit =
        Informedica.GenPRES.Shared.Models.Order.ValueUnit.create values "mg" "Mass" true "dutch" ""


    let variable name min minIncl incr max vals : Variable =
        Informedica.GenPRES.Shared.Models.Order.Variable.create name false min minIncl incr max false vals


    let free = variable "" None false None None None


    /// An order variable with these defined constraints and these values.
    let ovar defined vals : OrderVariable =
        {
            Name = "v"
            DefinedConstraints = defined
            CalculatedConstraints = free
            Variable = variable "v" None false None None vals
            LargeIncr = None
            Level = IsNormal
        }


    let one = Some(vu [| "2", 2m |])
    let three = Some(vu [| "1", 1m; "2", 2m; "3", 3m |])

    /// A defined maximum: a constraint the rules give.
    let maxDose = variable "" None false None (Some(vu [| "10", 10m |])) None
    /// A minimum of zero alone, exclusive: only says the value is above zero.
    let aboveZero = variable "" (Some(vu [| "0", 0m |])) false None None None

    let oneValue = ovar free one


    [<RequireQualifiedAccess>]
    type DoseType =
        | Once
        | Discontinuous
        | Continuous
        | Timed
        | OnceTimed


    let doseTypes =
        [
            DoseType.Once
            DoseType.Discontinuous
            DoseType.Continuous
            DoseType.Timed
            DoseType.OnceTimed
        ]


    let schedule dt (sch: Schedule) =
        { sch with
            IsOnce = (dt = DoseType.Once)
            IsDiscontinuous = (dt = DoseType.Discontinuous)
            IsContinuous = (dt = DoseType.Continuous)
            IsTimed = (dt = DoseType.Timed)
            IsOnceTimed = (dt = DoseType.OnceTimed)
        }


    /// An order of this dose type with one component "cmp" of two items, "itm" and "other",
    /// every variable free and without values; the tests set what they need.
    let order dt =
        let o = defaultFor<Order>()

        let free' =
            {
                Name = "v"
                DefinedConstraints = free
                CalculatedConstraints = free
                Variable = free
                LargeIncr = None
                Level = IsNormal
            }

        let freeDose =
            {
                Quantity = free'
                PerTime = free'
                Rate = free'
                Total = free'
                QuantityAdjust = free'
                PerTimeAdjust = free'
                RateAdjust = free'
                TotalAdjust = free'
            }

        let item name =
            { defaultFor<Item>() with
                Name = name
                ComponentQuantity = free'
                OrderableQuantity = free'
                ComponentConcentration = free'
                OrderableConcentration = free'
                Dose = freeDose
            }

        let cmp =
            { defaultFor<Component>() with
                Name = "cmp"
                ComponentQuantity = free'
                OrderableQuantity = free'
                OrderableConcentration = free'
                Dose = freeDose
                Items = [| item "itm"; item "other" |]
            }

        { o with
            Schedule =
                { o.Schedule with
                    Frequency = free'
                    Time = free'
                }
                |> schedule dt
            Orderable =
                { o.Orderable with
                    OrderableQuantity = free'
                    Dose = freeDose
                    Components = [| cmp |]
                }
        }


    let target = Ctx.Target.map


    /// The order with the variable of the target set.
    let withVar t v ord = ord |> target t (fun _ -> v)


    let itemTarget prop = Ctx.Target.Item("cmp", "itm", prop)


    /// The order with the dose quantity of the item "other" constrained by the rules, so the
    /// component dose is constrained.
    let constrained ord =
        ord
        |> withVar (Ctx.Target.Item("cmp", "other", ItemProperty.DoseQuantity)) (ovar maxDose None)


    let reCalcItem prop = OrderViewCommand.ReCalcItemProperty("cmp", "itm", prop, [||])


    let reCalcTime = OrderViewCommand.ReCalcScheduleProperty(ScheduleProperty.Time, [||])


    /// Every recalculation case the rules allow, with the dose types it is allowed on.
    let allowedCases =
        [
            reCalcItem ItemProperty.DoseQuantity,
            [ DoseType.Once; DoseType.Discontinuous; DoseType.Timed; DoseType.OnceTimed ]
            reCalcItem ItemProperty.DoseQuantityAdjust,
            [ DoseType.Once; DoseType.Discontinuous; DoseType.Timed; DoseType.OnceTimed ]
            reCalcItem ItemProperty.DosePerTime, [ DoseType.Discontinuous; DoseType.Timed ]
            reCalcItem ItemProperty.DosePerTimeAdjust, [ DoseType.Discontinuous; DoseType.Timed ]
            reCalcItem ItemProperty.DoseRate, [ DoseType.Continuous; DoseType.Timed; DoseType.OnceTimed ]
            reCalcItem ItemProperty.DoseRateAdjust, [ DoseType.Continuous; DoseType.Timed; DoseType.OnceTimed ]
            reCalcTime, [ DoseType.Continuous; DoseType.Timed; DoseType.OnceTimed ]
        ]


    /// The order of this dose type, constrained, with the variable the command recalculates set.
    let orderFor dt cmd v =
        let t = cmd |> IntentPolicy.reCalcTarget |> Option.get
        order dt |> constrained |> withVar t v


    [<Tests>]
    let tests =
        // a recalculation command judged by the recalculation rule of its target
        let allowed ord cmd =
            cmd
            |> IntentPolicy.reCalcTarget
            |> Option.exists (fun t -> ord |> IntentPolicy.isReCalcAllowed t)

        testList
            "IntentPolicy"
            [
                testList
                    "every recalculation case on every dose type, on one value"
                    [
                        for cmd, allowedOn in allowedCases do
                            for dt in doseTypes do
                                let exp = allowedOn |> List.contains dt

                                test $"%s{OrderViewCommand.toString (cmd, Ctx.empty)} on %A{dt}: %b{exp}" {
                                    cmd
                                    |> allowed (orderFor dt cmd oneValue)
                                    |> Expect.equal "allowed as the rule says" exp
                                }
                    ]

                testList
                    "a field with several values or a range is not recalculated"
                    [
                        for cmd, _ in allowedCases do
                            test $"%s{OrderViewCommand.toString (cmd, Ctx.empty)}" {
                                cmd
                                |> allowed (orderFor DoseType.Timed cmd (ovar free three))
                                |> Expect.isFalse "several values: selectable as a list"

                                cmd
                                |> allowed (orderFor DoseType.Timed cmd (ovar free None))
                                |> Expect.isFalse "a range"
                            }
                    ]

                testList
                    "an item dose of a component the rules do not constrain"
                    [
                        let cmd = reCalcItem ItemProperty.DoseQuantity
                        let unconstrained other =
                            order DoseType.Discontinuous
                            |> other
                            |> withVar (itemTarget ItemProperty.DoseQuantity) oneValue
                        let otherWith c =
                            withVar (Ctx.Target.Item("cmp", "other", ItemProperty.DosePerTime)) (ovar c None)

                        test "no defined constraints: not allowed" {
                            cmd |> allowed (unconstrained id) |> Expect.isFalse "free"
                        }

                        test "empty constraints, or a minimum of zero alone: not allowed" {
                            cmd |> allowed (unconstrained (otherWith free)) |> Expect.isFalse "empty"

                            cmd
                            |> allowed (unconstrained (otherWith aboveZero))
                            |> Expect.isFalse "above zero alone"

                        }

                        test "a constraint on another item of the component: allowed" {
                            cmd
                            |> allowed (unconstrained (otherWith maxDose))
                            |> Expect.isTrue "another item constrained"
                        }

                        test "a constraint on the component's own dose: allowed" {
                            let cmpDose =
                                fun (ord: Order) ->
                                    let cmp = ord.Orderable.Components[0]

                                    let cmp = { cmp with Dose = { cmp.Dose with Total = ovar maxDose None } }

                                    { ord with Order.Orderable.Components = [| cmp |] }

                            cmd
                            |> allowed (unconstrained cmpDose)
                            |> Expect.isTrue "the component dose constrained"
                        }

                        test "a component the order does not hold: not allowed" {
                            OrderViewCommand.ReCalcItemProperty("gone", "itm", ItemProperty.DoseQuantity, [||])
                            |> allowed (unconstrained (otherWith maxDose))
                            |> Expect.isFalse "no such component"
                        }
                    ]

                testList
                    "never allowed, also on one value of a constrained order"
                    [
                        for cmd in
                            [
                                // the four without a kind
                                OrderViewCommand.ReCalcOrderableProperty(OrderableProperty.Quantity, [||])
                                OrderViewCommand.ReCalcComponentProperty(
                                    "cmp",
                                    ComponentProperty.DoseQuantityAdjust,
                                    [||]
                                )
                                reCalcItem ItemProperty.OrderableQuantity
                                reCalcItem ItemProperty.OrderableConcentration
                                // the basic variables and the product strength, while the wire carries them
                                OrderViewCommand.ReCalcScheduleProperty(ScheduleProperty.Frequency, [||])
                                OrderViewCommand.ReCalcOrderableProperty(OrderableProperty.DoseQuantity, [||])
                                OrderViewCommand.ReCalcOrderableProperty(OrderableProperty.DoseRate, [||])
                                OrderViewCommand.ReCalcComponentProperty(
                                    "cmp",
                                    ComponentProperty.OrderableQuantity,
                                    [||]
                                )
                                reCalcItem ItemProperty.ComponentConcentration
                            ] do
                            for dt in doseTypes do
                                test $"%s{OrderViewCommand.toString (cmd, Ctx.empty)} on %A{dt}" {
                                    cmd |> allowed (orderFor dt cmd (ovar maxDose one)) |> Expect.isFalse "never"
                                }
                    ]

                test "a variable found by name, as a view asks it" {
                    let named name v = { v with OrderVariable.Name = name }

                    let ord =
                        order DoseType.Discontinuous
                        |> constrained
                        |> withVar (itemTarget ItemProperty.DoseQuantity) (named "dq" oneValue)
                        |> withVar (Ctx.Target.Schedule ScheduleProperty.Frequency) (named "frq" oneValue)

                    ord
                    |> IntentPolicy.isReCalcAllowedOf "dq"
                    |> Expect.isTrue "the item dose quantity"
                    ord |> IntentPolicy.isReCalcAllowedOf "frq" |> Expect.isFalse "the frequency"
                    ord
                    |> IntentPolicy.isReCalcAllowedOf "gone"
                    |> Expect.isFalse "no such variable"
                }

                test "reopens: the four recalculation commands, no other" {
                    [
                        reCalcTime
                        OrderViewCommand.ReCalcOrderableProperty(OrderableProperty.Quantity, [||])
                        OrderViewCommand.ReCalcComponentProperty("cmp", ComponentProperty.DoseQuantityAdjust, [||])
                        reCalcItem ItemProperty.DoseQuantity
                    ]
                    |> List.forall IntentPolicy.reopens
                    |> Expect.isTrue "a recalculation reopens"

                    [
                        OrderViewCommand.ResetOrderScenario
                        OrderViewCommand.SetNthItemProperty("cmp", "itm", ItemProperty.DoseQuantity, 0)
                        OrderViewCommand.IncreaseScheduleFrequencyProperty
                        OrderViewCommand.ClearAllFilterProperty
                    ]
                    |> List.exists IntentPolicy.reopens
                    |> Expect.isFalse "nothing else reopens"
                }
            ]
