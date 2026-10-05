/// The order variables a command addresses and the count of options, over the fixed medications
/// of the GenORDER tests; no rules are loaded. The reference for a change is the client's own: the
/// dose dialog's and the nutrition slot's, copied in Today.
module Informedica.GenPRES.Server.Tests.OrderContextCommandTests

open Expecto
open Expecto.Flip
open Informedica.GenOrder.Lib


module Ctx = Shared.Models.OrderContext

type Target = Ctx.Target


/// Today's client changes, copied as the reference: the dose dialog's (Views/Order.fs) and the
/// nutrition slot's (NutritionSlot.fs).
module Today =

    open Shared.Types

    let setOvar = Shared.Models.Order.OrderVariable.setOvar


    /// The dose dialog's change of the item it shows, with the component and item picked.
    let overItem (selCmp: string option) (selItm: string option) (f: Item -> Item) (ord: Order) =
        let cmpName =
            ord.Orderable.Components
            |> Array.tryFind (fun c -> selCmp.IsNone || selCmp = Some c.Name)
            |> Option.map _.Name

        { ord with
            Order.Orderable.Components =
                ord.Orderable.Components
                |> Array.map (fun cmp ->
                    if Some cmp.Name <> cmpName then
                        cmp
                    else
                        let itmName =
                            selItm
                            |> Option.orElse (cmp.Items |> Array.tryFind (_.IsAdditional >> not) |> Option.map _.Name)

                        { cmp with
                            Items =
                                cmp.Items
                                |> Array.map (fun itm -> if Some itm.Name = itmName then f itm else itm)
                        }
                )
        }


    /// The dose dialog's change of a substance's component concentration: the item named, in the
    /// first component and the one named.
    let componentConcentration cname iname s (ord: Order) =
        let set (itm: Item) =
            { itm with ComponentConcentration = itm.ComponentConcentration |> setOvar s }

        { ord with
            Order.Orderable.Components =
                ord.Orderable.Components
                |> Array.mapi (fun i cmp ->
                    if i > 0 && cmp.Name <> cname then
                        cmp
                    else
                        { cmp with
                            Items = cmp.Items |> Array.map (fun itm -> if itm.Name <> iname then itm else set itm)
                        }
                )
        }


    /// The nutrition slot's change of a component's variable, by name.
    let overComponent cmpName (f: Component -> Component) (ord: Order) =
        { ord with
            Order.Orderable.Components =
                ord.Orderable.Components
                |> Array.map (fun c -> if c.Name = cmpName then f c else c)
        }


    /// Today's change for a target and a key, as the client makes it.
    let change target (s: string option) (ord: Order) =
        match target with
        | Target.Schedule ScheduleProperty.Frequency ->
            { ord with Order.Schedule.Frequency = ord.Schedule.Frequency |> setOvar s }
        | Target.Schedule ScheduleProperty.Time -> { ord with Order.Schedule.Time = ord.Schedule.Time |> setOvar s }
        | Target.Orderable OrderableProperty.Quantity ->
            { ord with Order.Orderable.OrderableQuantity = ord.Orderable.OrderableQuantity |> setOvar s }
        | Target.Orderable OrderableProperty.DoseQuantity ->
            { ord with Order.Orderable.Dose.Quantity = ord.Orderable.Dose.Quantity |> setOvar s }
        | Target.Orderable OrderableProperty.DoseRate ->
            { ord with Order.Orderable.Dose.Rate = ord.Orderable.Dose.Rate |> setOvar s }
        | Target.Component(cmp, ComponentProperty.OrderableQuantity) ->
            ord
            |> overComponent cmp (fun c -> { c with OrderableQuantity = c.OrderableQuantity |> setOvar s })
        | Target.Component(cmp, ComponentProperty.DoseQuantityAdjust) ->
            ord
            |> overComponent
                cmp
                (fun c -> { c with Component.Dose.QuantityAdjust = c.Dose.QuantityAdjust |> setOvar s })
        | Target.Item(cmp, itm, ItemProperty.ComponentConcentration) -> ord |> componentConcentration cmp itm s
        | Target.Item(cmp, itm, prop) -> ord |> overItem (Some cmp) (Some itm) (Ctx.Target.mapItemVar prop (setOvar s))


/// Contexts made from the fixed medications of the GenORDER tests, each built as the dialog first
/// shows it and mapped to the contract through the server's own mapper.
module Fixtures =

    let solve cmd ord =
        ord
        |> cmd
        |> OrderProcessor.processPipeline OrderLogging.noOp
        |> Result.defaultWith (fun (_, msgs) -> failtest $"not solved: %A{msgs}")


    /// The order as the dialog first shows it: calculated, with its values to pick from.
    let built (med: Medication) =
        match med |> Medication.toOrder Scenarios.testStart with
        | Ok ord -> ord |> solve CalcMinMax |> solve CalcValues
        | Error e -> failtest $"no order for %s{med.Name}: %A{e}"


    /// A filter whose fields offer options of their own.
    let filter =
        { Ctx.filter with
            Indications = [| "indication a"; "indication b"; "indication c" |]
            Generics = [| "generic a"; "generic b" |]
            Routes = [| "route a" |]
            Forms = [| "form a"; "form b"; "form c"; "form d" |]
            DoseTypes = [| Shared.Types.DoseType.Once ""; Shared.Types.DoseType.Discontinuous "" |]
            Diluents = [| "diluent a"; "diluent b" |]
            Components = [| "component a"; "component b"; "component c" |]
        }


    /// The context of the stub patient with the medication's order as its one scenario.
    let context (med: Medication) =
        let ord = built med

        let items =
            ord.Orderable.Components
            |> List.collect (fun c -> c.Items |> List.map (fun i -> i.Name |> Name.toString))
            |> List.distinct
            |> List.toArray

        let shared = ord |> Order.Dto.toDto |> ServerApi.Mappers.Order.mapFromOrderToShared items

        let sc =
            Shared.Models.OrderScenario.create
                ""
                med.Name
                ""
                med.Route
                (Shared.Types.DoseType.Discontinuous "")
                None
                None
                None
                [||]
                [||]
                items
                [||]
                [||]
                [||]
                shared
                true
                false
                None
                [||]
                None

        { Ctx.empty with
            Patient = ServerApi.StubPatientData.patient
            Filter = filter
            Scenarios = [| sc |]
        }


    /// The fixtures, each made once.
    let all =
        lazy
            [
                "paracetamol rectal", context Scenarios.pcmSupp
                "paracetamol drink", context Scenarios.pcmDrink
                "morfine continuous", context Scenarios.morfCont
                "amfotericine", context Scenarios.amfo
                "cotrimoxazol", context Scenarios.cotrim
                "parenteral nutrition", context Scenarios.tpn
            ]


/// Every target an order can hold, with its component and item names.
let targets (ord: Shared.Types.Order) =
    [
        Target.Schedule Shared.Types.ScheduleProperty.Frequency
        Target.Schedule Shared.Types.ScheduleProperty.Time
        Target.Orderable Shared.Types.OrderableProperty.Quantity
        Target.Orderable Shared.Types.OrderableProperty.DoseQuantity
        Target.Orderable Shared.Types.OrderableProperty.DoseRate
        for c in ord.Orderable.Components do
            Target.Component(c.Name, Shared.Types.ComponentProperty.OrderableQuantity)
            Target.Component(c.Name, Shared.Types.ComponentProperty.DoseQuantityAdjust)

            for i in c.Items do
                for prop in
                    [
                        Shared.Types.ItemProperty.DoseQuantity
                        Shared.Types.ItemProperty.DoseQuantityAdjust
                        Shared.Types.ItemProperty.DosePerTime
                        Shared.Types.ItemProperty.DosePerTimeAdjust
                        Shared.Types.ItemProperty.DoseRate
                        Shared.Types.ItemProperty.DoseRateAdjust
                        Shared.Types.ItemProperty.ComponentConcentration
                        Shared.Types.ItemProperty.OrderableConcentration
                        Shared.Types.ItemProperty.OrderableQuantity
                    ] do
                    Target.Item(c.Name, i.Name, prop)
    ]


[<Tests>]
let tests =
    testList
        "order context commands"
        [
            testList
                "a target changed as today's client changes it"
                [
                    for name, ctx in Fixtures.all.Value do
                        let ord = ctx.Scenarios[0].Order

                        for target in targets ord do
                            let keys =
                                ord
                                |> Ctx.Target.tryGet target
                                |> Option.map Ctx.values
                                |> Option.defaultValue [||]

                            if keys.Length > 1 then
                                for n in 0 .. keys.Length - 1 do
                                    test $"%s{name}: %A{target} set to value %i{n}" {
                                        let set = Shared.Models.Order.OrderVariable.setOvar (Some keys[n])

                                        ord
                                        |> Ctx.Target.map target set
                                        |> Expect.equal "today's change" (ord |> Today.change target (Some keys[n]))
                                    }

                                test $"%s{name}: %A{target} cleared" {
                                    ord
                                    |> Ctx.Target.map target (Shared.Models.Order.OrderVariable.setOvar None)
                                    |> Expect.equal "today's change" (ord |> Today.change target None)
                                }

                                test $"%s{name}: %A{target} counted" {
                                    ctx
                                    |> Ctx.count (Ctx.Options.Variable target)
                                    |> Expect.equal "its values" keys.Length
                                }
                ]

            testList
                "a target the order does not hold"
                [
                    test "a component of another name" {
                        let ord = Fixtures.all.Value[0] |> snd |> _.Scenarios[0].Order

                        ord
                        |> Ctx.Target.tryGet (
                            Target.Component("none", Shared.Types.ComponentProperty.OrderableQuantity)
                        )
                        |> Expect.isNone "no variable"
                    }

                    test "counts no values" {
                        Fixtures.all.Value[0]
                        |> snd
                        |> Ctx.count (
                            Ctx.Options.Variable(Target.Item("none", "none", Shared.Types.ItemProperty.DoseQuantity))
                        )
                        |> Expect.equal "nothing to pick" 0
                    }
                ]

            testList
                "the options counted"
                [
                    let ctx = Fixtures.all.Value[0] |> snd

                    for options, expected in
                        [
                            Ctx.Options.Filter Ctx.Indication, 3
                            Ctx.Options.Filter Ctx.Generic, 2
                            Ctx.Options.Filter Ctx.Route, 1
                            Ctx.Options.Filter Ctx.Form, 4
                            Ctx.Options.Filter Ctx.DoseType, 2
                            Ctx.Options.Diluents, 2
                            Ctx.Options.Components, 3
                            Ctx.Options.Scenarios, 1
                        ] do
                        test $"%A{options}" { ctx |> Ctx.count options |> Expect.equal "the options" expected }

                    test "a variable of a context without one scenario counts nothing" {
                        { ctx with Scenarios = [||] }
                        |> Ctx.count (Ctx.Options.Variable(Target.Schedule Shared.Types.ScheduleProperty.Frequency))
                        |> Expect.equal "nothing to pick" 0
                    }
                ]
        ]
