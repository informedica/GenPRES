/// The order variables a command addresses and the count of options, over the fixed medications
/// of the GenORDER tests; no rules are loaded. The reference for a change is the client's own: the
/// dose dialog's and the nutrition slot's, copied in Today.
module Informedica.GenPRES.Server.Tests.OrderContextCommandTests

open Expecto
open Expecto.Flip
open Informedica.GenOrder.Lib


module Ctx = Shared.Models.OrderContext

type Api = Shared.Api.OrderViewCommand

module Cmd = Shared.Api.OrderViewCommand

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


/// The new case that sets the nth value of a target.
let setNthCommand target n =
    match target with
    | Target.Schedule prop -> Api.SetNthScheduleProperty(prop, n)
    | Target.Orderable prop -> Api.SetNthOrderableProperty(prop, n)
    | Target.Component(cmp, prop) -> Api.SetNthComponentProperty(cmp, prop, n)
    | Target.Item(cmp, itm, prop) -> Api.SetNthItemProperty(cmp, itm, prop, n)


/// The new case that clears a target.
let clearCommand target picks =
    match target with
    | Target.Schedule prop -> Api.ClearScheduleProperty(prop, picks)
    | Target.Orderable prop -> Api.ClearOrderableProperty(prop, picks)
    | Target.Component(cmp, prop) -> Api.ClearComponentProperty(cmp, prop, picks)
    | Target.Item(cmp, itm, prop) -> Api.ClearItemProperty(cmp, itm, prop, picks)


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

                    for name, ctx in Fixtures.all.Value do
                        let ord = ctx.Scenarios[0].Order
                        let first = ord.Orderable.Components[0]
                        let set = Shared.Models.Order.OrderVariable.setOvar None

                        for item in first.Items do
                            test $"%s{name}: a concentration of %s{item.Name} in a missing component" {
                                let target =
                                    Target.Item("none", item.Name, Shared.Types.ItemProperty.ComponentConcentration)

                                ord |> Ctx.Target.map target set |> Expect.equal "the order as it is" ord
                            }

                        test $"%s{name}: a missing item in the first component" {
                            let target =
                                Target.Item(first.Name, "none", Shared.Types.ItemProperty.ComponentConcentration)

                            ord |> Ctx.Target.map target set |> Expect.equal "the order as it is" ord
                        }

                    test "an item only in a later component of the same name" {
                        let ord = Fixtures.all.Value[0] |> snd |> _.Scenarios[0].Order
                        let first = ord.Orderable.Components[0]

                        let later = { first with Items = [| { first.Items[0] with Name = "later" } |] }

                        let ord: Shared.Types.Order =
                            { ord with
                                Orderable =
                                    { ord.Orderable with
                                        Components = Array.append ord.Orderable.Components [| later |]
                                    }
                            }

                        let target = Target.Item(first.Name, "later", Shared.Types.ItemProperty.DoseQuantity)

                        ord |> Ctx.Target.tryGet target |> Expect.isSome "the item found"

                        ord
                        |> Ctx.Target.map target (Shared.Models.Order.OrderVariable.setOvar None)
                        |> Ctx.Target.tryGet target
                        |> Option.map _.Variable.IsNonZeroPositive
                        |> Expect.equal "the item cleared" (Some true)
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

            testList
                "a value changed in the context as today's client changes it"
                [
                    for name, ctx in Fixtures.all.Value do
                        let ord = ctx.Scenarios[0].Order

                        for target in targets ord do
                            match ord |> Ctx.Target.tryGet target |> Option.map Ctx.values with
                            | None -> ()
                            | Some [||] ->
                                test $"%s{name}: %A{target} offers no list" {
                                    ctx |> Ctx.setNth target 0 |> Expect.equal "no change" (Ok None)
                                }
                            | Some keys ->
                                for n in 0 .. keys.Length - 1 do
                                    test $"%s{name}: %A{target} set to value %i{n}" {
                                        let today = ctx |> Ctx.withOrder (ord |> Today.change target (Some keys[n]))
                                        ctx |> Ctx.setNth target n |> Expect.equal "today's context" (Ok(Some today))
                                    }

                                test $"%s{name}: %A{target} past its values" {
                                    ctx
                                    |> Ctx.setNth target keys.Length
                                    |> Result.isError
                                    |> Expect.isTrue "an error"
                                }

                                test $"%s{name}: %A{target} cleared, the picks with the order id" {
                                    let today = ctx |> Ctx.withOrder (ord |> Today.change target None)

                                    ctx
                                    |> Ctx.clear target [| "[.x]_dos_qty" |]
                                    |> Expect.equal "today's context" (Ok(today, [| $"[%s{ord.Id}.x]_dos_qty" |]))
                                }

                    test "a target the order does not hold" {
                        let ctx = Fixtures.all.Value[0] |> snd
                        let target = Target.Component("none", Shared.Types.ComponentProperty.OrderableQuantity)

                        ctx |> Ctx.setNth target 0 |> Result.isError |> Expect.isTrue "an error"
                        ctx |> Ctx.clear target [||] |> Result.isError |> Expect.isTrue "an error"
                    }

                    test "a context without one scenario" {
                        let ctx = Fixtures.all.Value[0] |> snd |> fun ctx -> { ctx with Scenarios = [||] }
                        let target = Target.Schedule Shared.Types.ScheduleProperty.Frequency

                        ctx |> Ctx.setNth target 0 |> Result.isError |> Expect.isTrue "an error"
                    }
                ]

            testList
                "the filter changed as the page changes it"
                [
                    let ctx = Fixtures.all.Value[0] |> snd
                    let f = ctx.Filter

                    let today field (x: int option) =
                        match field with
                        | Ctx.Indication -> ctx |> Ctx.indicationChange (x |> Option.map (fun i -> f.Indications[i]))
                        | Ctx.Generic -> ctx |> Ctx.medicationChange (x |> Option.map (fun i -> f.Generics[i]))
                        | Ctx.Route -> ctx |> Ctx.routeChange (x |> Option.map (fun i -> f.Routes[i]))
                        | Ctx.Form -> ctx |> Ctx.formChange (x |> Option.map (fun i -> f.Forms[i]))
                        | Ctx.DoseType -> ctx |> Ctx.doseTypeChange (x |> Option.map (fun i -> f.DoseTypes[i]))

                    for field in [ Ctx.Indication; Ctx.Generic; Ctx.Route; Ctx.Form; Ctx.DoseType ] do
                        let n = ctx |> Ctx.count (Ctx.Options.Filter field)

                        for i in 0 .. n - 1 do
                            test $"%A{field} option %i{i}" {
                                ctx
                                |> Ctx.changeFilter field (Some i)
                                |> Expect.equal "today's context" (Ok(today field (Some i)))
                            }

                        test $"%A{field} emptied" {
                            ctx
                            |> Ctx.changeFilter field None
                            |> Expect.equal "today's context" (Ok(today field None))
                        }

                        test $"%A{field} past its options" {
                            ctx
                            |> Ctx.changeFilter field (Some n)
                            |> Result.isError
                            |> Expect.isTrue "an error"
                        }

                    test "the diluent set and emptied" {
                        ctx
                        |> Ctx.changeDiluent (Some 1)
                        |> Expect.equal "today's context" (Ok(ctx |> Ctx.diluentChange (Some f.Diluents[1])))

                        ctx
                        |> Ctx.changeDiluent None
                        |> Expect.equal "today's context" (Ok(ctx |> Ctx.diluentChange None))

                        ctx |> Ctx.changeDiluent (Some 2) |> Result.isError |> Expect.isTrue "an error"
                    }

                    test "the components picked" {
                        ctx
                        |> Ctx.setNthComponents [| 0; 2 |]
                        |> Expect.equal
                            "today's context"
                            (Ok(ctx |> Ctx.componentsChange [| f.Components[0]; f.Components[2] |]))

                        ctx
                        |> Ctx.setNthComponents [| 0; 3 |]
                        |> Result.isError
                        |> Expect.isTrue "an error"
                    }

                    test "the whole filter emptied, the patient kept" {
                        ctx
                        |> Ctx.clearAll
                        |> Expect.equal "the empty context of the patient" { Ctx.empty with Patient = ctx.Patient }
                    }

                    test "the scenario selected with its form" {
                        let sc = ctx.Scenarios[0]

                        ctx
                        |> Ctx.selectNthScenario 0
                        |> Expect.equal
                            "today's context"
                            (Ok
                                { ctx with
                                    Filter = { ctx.Filter with Form = Some sc.Form }
                                    Scenarios = [| sc |]
                                })

                        ctx |> Ctx.selectNthScenario 1 |> Result.isError |> Expect.isTrue "an error"
                    }

                    test "the later of two scenarios selected with its form" {
                        let first = { ctx.Scenarios[0] with Form = "zetpil" }
                        let later = { (Fixtures.all.Value[1] |> snd).Scenarios[0] with Form = "drank" }
                        let two = { ctx with Scenarios = [| first; later |] }

                        two
                        |> Ctx.selectNthScenario 1
                        |> Expect.equal
                            "the later scenario and its form"
                            (Ok
                                { two with
                                    Filter = { two.Filter with Form = Some "drank" }
                                    Scenarios = [| later |]
                                })
                    }
                ]

            testList
                "picks without the order id"
                [
                    test "the id leaves a name and comes back" {
                        let name = "[a-1.paracetamol.paracetamol]_dos_qty"

                        name
                        |> Ctx.Picks.withinOrder "a-1"
                        |> Expect.equal "the id left off" "[.paracetamol.paracetamol]_dos_qty"

                        name
                        |> Ctx.Picks.withinOrder "a-1"
                        |> Ctx.Picks.ofOrder "a-1"
                        |> Expect.equal "the id put back" name
                    }

                    test "every variable name of the fixtures comes back" {
                        for _, ctx in Fixtures.all.Value do
                            let ord = ctx.Scenarios[0].Order

                            for v in targets ord |> List.choose (fun t -> ord |> Ctx.Target.tryGet t) do
                                v.Name
                                |> Ctx.Picks.withinOrder ord.Id
                                |> Ctx.Picks.ofOrder ord.Id
                                |> Expect.equal "the name as it was" v.Name
                    }
                ]

            testList
                "the argumentation"
                [
                    test "written trimmed" {
                        Ctx.empty
                        |> Ctx.Argumentation.write "  te hoog, bewust  "
                        |> _.Argumentation
                        |> Expect.equal "the text trimmed" (Some "te hoog, bewust")
                    }

                    test "an empty text writes none" {
                        Ctx.empty
                        |> Ctx.Argumentation.write "   "
                        |> _.Argumentation
                        |> Expect.isNone "none"
                    }

                    test "a long text is cut" {
                        Ctx.empty
                        |> Ctx.Argumentation.write (String.replicate 1100 "a")
                        |> _.Argumentation
                        |> Option.map String.length
                        |> Expect.equal "the maximum" (Some Ctx.Argumentation.maxLength)
                    }
                ]

            testList
                "the preview of a command"
                [
                    let ctx = Fixtures.all.Value[0] |> snd
                    let ord = ctx.Scenarios[0].Order

                    for cmd, expected in
                        [
                            Api.SetNthFilterProperty(Ctx.Route, 0), ctx |> Ctx.changeFilter Ctx.Route (Some 0)
                            Api.ClearFilterProperty Ctx.Generic, ctx |> Ctx.changeFilter Ctx.Generic None
                            Api.ClearAllFilterProperty, Ok(Ctx.clearAll ctx)
                            Api.SetNthDiluentProperty 1, ctx |> Ctx.changeDiluent (Some 1)
                            Api.ClearDiluentProperty, ctx |> Ctx.changeDiluent None
                            Api.SetNthComponentsProperty [| 1 |], ctx |> Ctx.setNthComponents [| 1 |]
                            Api.SelectNthOrderScenario 0, ctx |> Ctx.selectNthScenario 0
                            Api.SetArgumentationProperty "bewust", Ok(ctx |> Ctx.Argumentation.write "bewust")
                            Api.ResetOrderScenario, Ok ctx
                        ] do
                        test $"%s{Cmd.toString (cmd, ctx)}" {
                            ctx
                            |> Cmd.preview cmd
                            |> Expect.equal "the context as the command changes it" expected
                        }

                    for target in targets ord do
                        match ord |> Ctx.Target.tryGet target |> Option.map Ctx.values with
                        | Some keys when keys.Length > 1 ->
                            test $"%A{target} set to its last value" {
                                let n = keys.Length - 1

                                ctx
                                |> Cmd.preview (setNthCommand target n)
                                |> Expect.equal
                                    "the update of the scenario"
                                    (ctx |> Ctx.setNth target n |> Result.map _.Value)
                            }

                            test $"%A{target} cleared" {
                                ctx
                                |> Cmd.preview (clearCommand target [| "[.x]_dos_qty" |])
                                |> Expect.equal
                                    "the reopen with the picks"
                                    (ctx |> Ctx.clear target [| "[.x]_dos_qty" |] |> Result.map fst)
                            }

                            test $"%A{target} past its values" {
                                ctx
                                |> Cmd.preview (setNthCommand target keys.Length)
                                |> Result.isError
                                |> Expect.isTrue "an error"
                            }
                        | Some [||] ->
                            test $"%A{target} without a list" {
                                ctx
                                |> Cmd.preview (setNthCommand target 0)
                                |> Expect.equal "the context as it is" (Ok ctx)
                            }
                        | _ -> ()
                ]

            testList
                "the log of a command"
                [
                    test "the log never shows the argumentation" {
                        (Api.SetArgumentationProperty "patient weegt meer", Ctx.empty)
                        |> Cmd.toString
                        |> Expect.equal "the case alone" "SetArgumentationProperty"
                    }
                ]

            testList
                "the server sends a new case to the domain"
                [
                    let ctx = Fixtures.all.Value[0] |> snd

                    let target =
                        targets ctx.Scenarios[0].Order
                        |> List.find (fun t -> ctx |> Ctx.count (Ctx.Options.Variable t) > 1)

                    let seenBy (seen: string option ref) : ServerApi.OrderContextPort =
                        {
                            evaluate =
                                fun cmd pc ->
                                    seen.Value <-
                                        Some(cmd pc.Context |> Informedica.GenOrder.Lib.OrderContext.Command.toString)
                                    async { return Ok(Informedica.GenOrder.Lib.Types.Evaluated pc) }
                        }

                    let env port =
                        StubAdapterTests.StubAdapters.makeEnv
                            (StubAdapterTests.StubAdapters.formularyAlwaysOk Shared.Models.Formulary.empty)
                            port

                    testAsync "a value pick reaches the port as the pick of one order value" {
                        let seen = ref None

                        let! answer =
                            ServerApi.OrderContextCommand.processViewCmd
                                (env (seenBy seen))
                                (setNthCommand target 0, ctx)

                        answer |> Result.isOk |> Expect.isTrue "evaluated"

                        seen.Value
                        |> Option.map _.StartsWith("SetNthOrderValue")
                        |> Expect.equal "the domain's case" (Some true)
                    }

                    testAsync "a patient update reaches the port as the context evaluated for that patient" {
                        let pat = { ctx.Patient with Department = Some "NEO" }
                        let seen = ref None

                        let port: ServerApi.OrderContextPort =
                            {
                                evaluate =
                                    fun cmd pc ->
                                        match cmd pc.Context with
                                        | Informedica.GenOrder.Lib.OrderContext.UpdatePatient(sent, p) ->
                                            seen.Value <- Some(sent.Patient.Department, p.Department)
                                        | _ -> ()

                                        async { return Ok(Informedica.GenOrder.Lib.Types.Evaluated pc) }
                            }

                        let! answer =
                            ServerApi.OrderContextCommand.processCmd
                                (env port)
                                (Shared.Api.OrderContextCommand.UpdatePatient(pat, ctx))

                        answer |> Result.isOk |> Expect.isTrue "evaluated"

                        seen.Value
                        |> Expect.equal "the context's patient is the new one" (Some(Some "NEO", Some "NEO"))
                    }

                    testAsync "the argumentation is answered without asking the port" {
                        let seen = ref None

                        let! answer =
                            ServerApi.OrderContextCommand.processViewCmd
                                (env (seenBy seen))
                                (Api.SetArgumentationProperty "bewust", ctx)

                        seen.Value |> Expect.isNone "the port not asked"

                        match answer with
                        | Ok(Shared.Types.OrderContextResponse.Evaluated c) ->
                            c.Argumentation |> Expect.equal "the text written" (Some "bewust")
                        | other -> failtest $"expected the context, got %A{other}"
                    }

                    testAsync "a reset clears the argumentation, since it puts the order back within the rules" {
                        let seen = ref None

                        let! answer =
                            ServerApi.OrderContextCommand.processViewCmd
                                (env (seenBy seen))
                                (Api.ResetOrderScenario, ctx |> Ctx.Argumentation.write "bewust")

                        match answer with
                        | Ok(Shared.Types.OrderContextResponse.Evaluated c) ->
                            c.Argumentation |> Expect.isNone "the text cleared"
                        | other -> failtest $"expected the context, got %A{other}"
                    }

                    testAsync "an index past the values reaches the port, where it leaves the order as it is" {
                        let seen = ref None
                        let past = ctx |> Ctx.count (Ctx.Options.Variable target)

                        let! answer =
                            ServerApi.OrderContextCommand.processViewCmd
                                (env (seenBy seen))
                                (setNthCommand target past, ctx)

                        answer |> Result.isOk |> Expect.isTrue "evaluated"
                        seen.Value |> Expect.isSome "the port asked"
                    }
                ]

            testList
                "the plan navigates with a new case"
                [
                    let ctx = { (Fixtures.all.Value[0] |> snd) with Id = "c-1" }
                    let plan = Shared.Models.OrderPlan.create ctx.Patient [| ctx |]

                    let target =
                        targets ctx.Scenarios[0].Order
                        |> List.find (fun t -> ctx |> Ctx.count (Ctx.Options.Variable t) > 1)

                    let recalculated = ref None
                    let navigated = ref None

                    let run cmd =
                        let seen = ref []

                        let answering name (p: Informedica.GenOrder.Lib.Types.OrderPlan) =
                            seen.Value <- name :: seen.Value
                            if name = "recalculate" then
                                recalculated.Value <- Some p
                            async { return Ok p }

                        let port: ServerApi.OrderPlanPort =
                            {
                                recalculate = answering "recalculate"
                                navigate =
                                    fun p _ _ pc ->
                                        navigated.Value <- Some(p, pc)
                                        answering "navigate" p
                                addOrderContext = fun p _ -> answering "addOrderContext" p
                                newOrderContext = fun p _ -> answering "newOrderContext" p
                                removeOrderContexts = fun p _ -> answering "removeOrderContexts" p
                                openWith =
                                    fun pat cs ->
                                        answering "openWith" (Informedica.GenOrder.Lib.OrderPlan.create pat cs)
                            }

                        let env =
                            { StubAdapterTests.StubAdapters.makeEnv
                                  (StubAdapterTests.StubAdapters.formularyAlwaysOk Shared.Models.Formulary.empty)
                                  (StubAdapterTests.StubAdapters.orderContextAlwaysOk ctx) with
                                orderPlan = port
                            }

                        async {
                            let! answer = ServerApi.OrderPlanCommand.processCmd env cmd
                            return answer, seen.Value
                        }

                    testAsync "a value pick navigates" {
                        let! answer, seen =
                            run (Shared.Api.OrderPlanCommand.Navigate(plan, "c-1", setNthCommand target 0, ctx))
                        answer |> Result.isOk |> Expect.isTrue "answered"
                        seen |> Expect.equal "the navigation" [ "navigate" ]
                    }

                    testAsync "the argumentation is written into the plan, which is recalculated" {
                        let! answer, seen =
                            run (
                                Shared.Api.OrderPlanCommand.Navigate(
                                    plan,
                                    "c-1",
                                    Api.SetArgumentationProperty "bewust",
                                    ctx
                                )
                            )

                        answer |> Result.isOk |> Expect.isTrue "answered"
                        seen |> Expect.equal "the recalculation only" [ "recalculate" ]
                    }

                    testAsync "the argumentation for a context the plan does not hold is refused" {
                        let! answer, seen =
                            run (
                                Shared.Api.OrderPlanCommand.Navigate(
                                    plan,
                                    "c-2",
                                    Api.SetArgumentationProperty "bewust",
                                    ctx
                                )
                            )

                        answer |> Result.isError |> Expect.isTrue "refused"
                        seen |> Expect.isEmpty "no port asked"
                    }

                    testAsync "the argumentation keeps the plan's own context, only its text written" {
                        let other =
                            { ctx with
                                Id = "c-9"
                                Category = Shared.Types.OrderCategory.Nutrition Shared.Types.NutritionCategory.TPN
                            }

                        let! answer, _ =
                            run (
                                Shared.Api.OrderPlanCommand.Navigate(
                                    plan,
                                    "c-1",
                                    Api.SetArgumentationProperty "bewust",
                                    other
                                )
                            )

                        answer |> Result.isOk |> Expect.isTrue "answered"

                        match recalculated.Value |> Option.map _.Contexts with
                        | Some [| pc |] ->
                            pc.Id |> Expect.equal "the plan's id" "c-1"
                            pc.Category
                            |> Expect.equal "the plan's category" Informedica.GenOrder.Lib.Types.OrderCategory.Drug
                            pc.Context.Argumentation |> Expect.equal "the text written" (Some "bewust")
                        | other -> failtest $"expected the one context recalculated, got %A{other}"
                    }

                    testAsync "a reset clears the argumentation on the context and on the plan's copy of it" {
                        let argued = ctx |> Ctx.Argumentation.write "bewust"
                        let argued' = Shared.Models.OrderPlan.create ctx.Patient [| argued |]

                        let! answer, _ =
                            run (Shared.Api.OrderPlanCommand.Navigate(argued', "c-1", Api.ResetOrderScenario, argued))

                        answer |> Result.isOk |> Expect.isTrue "answered"

                        match navigated.Value with
                        | Some(p, pc) ->
                            pc.Context.Argumentation |> Expect.isNone "the context's text cleared"
                            p.Contexts
                            |> Array.map _.Context.Argumentation
                            |> Expect.equal "the plan's copy cleared" [| None |]
                        | None -> failtest "expected the navigation"
                    }

                    testAsync "an index past the values navigates, where it leaves the order as it is" {
                        let past = ctx |> Ctx.count (Ctx.Options.Variable target)
                        let! answer, seen =
                            run (Shared.Api.OrderPlanCommand.Navigate(plan, "c-1", setNthCommand target past, ctx))
                        answer |> Result.isOk |> Expect.isTrue "answered"
                        seen |> Expect.equal "the navigation" [ "navigate" ]
                    }
                ]
        ]
