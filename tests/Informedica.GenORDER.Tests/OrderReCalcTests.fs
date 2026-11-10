/// The recalculation of an order value by the order program, over the solved fixtures.
module OrderReCalcTests

open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip

module Ref = OrderProcessor.OrderVariableReference


/// The recalculation commands of an order that a kind recalculates, each with the variable it
/// names and the recipe it runs.
let reCalcs (ord: Order) =
    [
        ReCalcScheduleTime
        ReCalcOrderableQuantity

        for c in ord.Orderable.Components do
            let c' = c.Name |> Name.toString
            ReCalcComponentDoseQuantityAdjust c'

            for i in c.Items do
                let i' = i.Name |> Name.toString
                ReCalcItemDoseQuantity(c', i')
                ReCalcItemDoseQuantityAdjust(c', i')
                ReCalcItemDosePerTime(c', i')
                ReCalcItemDosePerTimeAdjust(c', i')
                ReCalcItemDoseRate(c', i')
                ReCalcItemDoseRateAdjust(c', i')
                ReCalcItemOrderableConcentration(c', i')
                ReCalcItemOrderableQuantity(c', i')
    ]
    |> List.choose (fun cmd ->
        match cmd |> OrderProcessor.CommandBridge.ofChangeProperty with
        | Some(OrderAction.ReCalc ovarRef) ->
            OrderProcessor.Recipe.forReCalcCommand ovarRef ord
            |> Option.map (fun recipe -> cmd, ovarRef, recipe)
        | _ -> None
    )


/// The order processed by the command, as the order context runs it.
let run cmd ord = cmd ord |> OrderProcessor.processPipeline OrderLogging.noOp


/// The order recalculated by the command.
let reCalc cmd ord = ord |> run (fun o -> ChangeProperty(o, cmd))


/// The values of a variable in its base unit, none when it holds no list.
let baseValues ovar =
    ovar
    |> OrderVariable.getValSetValueUnit
    |> Option.map ValueUnit.getBaseValue
    |> Option.defaultValue [||]


/// Per solved fixture and recipe: the order, the recipe, the commands that run the recipe with the
/// variable each names, and the order recalculated by the first of those commands. The commands of a
/// recipe run the same effects except the variable whose impossible values are dropped, so the order is
/// recalculated once per recipe rather than once per command.
let cases =
    lazy
        [
            for name, ord in Tests.RecipeTests.solvedFixtures.Value do
                for recipe, cmds in ord |> reCalcs |> List.groupBy (fun (_, _, recipe) -> recipe) do
                    let cmds = cmds |> List.map (fun (cmd, ovarRef, _) -> cmd, ovarRef)
                    name, ord, recipe, cmds, ord |> reCalc (cmds |> List.head |> fst)
        ]


/// The effects of the recalculation of the variable, without the drop of the impossible values of
/// that variable; none when the action is refused.
let reCalcEffects ord ovarRef =
    ord
    |> OrderProcessor.OrderAction.effects (OrderAction.ReCalc ovarRef)
    |> Result.toOption
    |> Option.map (
        List.filter (
            function
            | OrderEffect.DropImpossibleValues r -> r <> ovarRef
            | _ -> true
        )
    )


/// The recalculations that solved, per command the select function keeps of a recipe, with the
/// variable the command names before and after the recalculation of the recipe.
let recalculatedWith select =
    [
        for name, ord, recipe, cmds, res in cases.Value do
            match res with
            | Ok after ->
                for cmd, ovarRef in cmds |> select do
                    match ovarRef |> Ref.tryGet ord, ovarRef |> Ref.tryGet after with
                    | Some before, Some ovar -> $"%s{name} %A{cmd}", ord, ovarRef, recipe, before, after, ovar
                    | _ -> ()
            | Error _ -> ()
    ]


/// The recalculations that solved, for every command of a recipe.
let recalculated () = recalculatedWith id


let doseQuantity (ord: Order) = ord.Orderable.Dose.Quantity |> OrderVariable.Quantity.toOrdVar


let rate (ord: Order) = ord.Orderable.Dose.Rate |> OrderVariable.Rate.toOrdVar


/// The order with the nth value of the variable picked, solved as a pick from the dialog is.
let pick n ovar ord =
    ord
    |> Order.fromOrdVars [ ovar |> OrderVariable.setNthValue n ]
    |> run SolveOrder


/// The first item rate a kind recalculates.
let itemRate (ord: Order) =
    ord
    |> reCalcs
    |> List.pick (
        function
        | ReCalcItemDoseRate _ as cmd, _, _ -> Some cmd
        | _ -> None
    )


let processed med =
    med
    |> Tests.TestOrder.toOrder
    |> Tests.TestOrder.solve CalcMinMax
    |> Tests.TestOrder.solve CalcValues
    |> Tests.TestOrder.solve SolveOrder


/// The dose variables the dose checks read for the schedule of the order: the rates of a timed or
/// continuous order, the frequency of a repeated one, and the item doses of an order with a quantity.
let doseCheckVariables (ord: Order) =
    let items = ord.Orderable.Components |> List.collect _.Items |> List.map _.Dose
    let rates =
        (ord.Orderable.Dose.Rate :: (items |> List.map _.Rate))
        |> List.map OrderVariable.Rate.toOrdVar

    let quantities =
        [
            yield! items |> List.map (_.Quantity >> OrderVariable.Quantity.toOrdVar)
            yield! items |> List.map (_.PerTime >> OrderVariable.PerTime.toOrdVar)
            yield! items |> List.map (_.PerTimeAdjust >> OrderVariable.PerTimeAdjust.toOrdVar)
        ]

    let frequency = ord.Schedule |> Order.Schedule.toOrdVars |> fst |> Option.toList

    match ord.Schedule with
    | Continuous _ -> rates
    | Timed _ -> rates @ frequency @ quantities
    | OnceTimed _ -> rates @ quantities
    | Discontinuous _ -> frequency @ quantities
    | Once -> quantities


[<Tests>]
let tests =
    testList
        "OrderProcessor recalculation"
        [
            test "no solved or recalculated order leaves a variable the dose checks read cleared" {
                [
                    for name, ord in Tests.RecipeTests.solvedFixtures.Value do
                        name, ord

                    for name, _, recipe, _, res in cases.Value do
                        match res with
                        | Ok after -> $"%s{name} %A{recipe}", after
                        | Error _ -> ()
                ]
                |> List.filter (snd >> doseCheckVariables >> List.exists OrderVariable.isCleared)
                |> List.map fst
                |> Expect.equal "no order" []
            }

            test "every recipe recalculation of the solved fixtures solves" {
                cases.Value |> Expect.isNonEmpty "recalculations"

                [
                    for name, _, recipe, _, res in cases.Value do
                        match res with
                        | Ok _ -> ()
                        | Error(_, msgs) -> $"%s{name} %A{recipe}: %A{msgs}"
                ]
                |> Expect.isEmpty "every recalculation solves"
            }

            // the cases recalculate once per recipe, which holds for every command only when the
            // commands of a recipe run the same effects up to the variable whose values are dropped
            test "the commands of a recipe run the same effects, except the variable whose values are dropped" {
                [
                    for name, ord, _, cmds, _ in cases.Value do
                        let first = cmds |> List.head |> snd |> reCalcEffects ord

                        for cmd, ovarRef in cmds do
                            let effs = ovarRef |> reCalcEffects ord

                            if effs |> Option.isNone || effs <> first then
                                $"%s{name} %A{cmd}"
                ]
                |> Expect.isEmpty "the same effects"
            }

            test "a recalculation without a kind is refused, and keeps the order" {
                let name, ord = Tests.RecipeTests.solvedFixtures.Value |> List.head

                match ord |> reCalc ReCalcOrderableQuantity with
                | Error(o, msgs) ->
                    o |> Expect.equal $"%s{name}: the order before" ord
                    msgs |> Expect.isEmpty "a refusal has no messages"
                | Ok _ -> failtest $"%s{name}: the orderable quantity has no kind"
            }

            // when the orderable variable of the kind holds one value, the definition fixes it, so the
            // field can only offer that one value
            test "the field offers more than one value, unless the definition fixes its kind" {
                let cases = recalculated ()

                cases
                |> List.exists (fun (_, _, _, _, _, _, ovar) -> ovar |> baseValues |> Array.length > 1)
                |> Expect.isTrue "a field with a list"

                [
                    for label, _, _, recipe, _, after, ovar in cases do
                        let kind =
                            match recipe with
                            | Recipe.ReCalcTime -> after |> rate
                            | _ -> after |> doseQuantity

                        if ovar |> OrderVariable.hasOneValue && kind |> OrderVariable.hasOneValue |> not then
                            label
                ]
                |> Expect.isEmpty "a list wherever the kind has one"
            }

            test "a recalculation keeps every variable its recipe does not set back" {
                [
                    for label, ord, _, recipe, _, after, _ in recalculated () do
                        let setBack =
                            ord
                            |> OrderProcessor.Recipe.effects recipe
                            |> OrderProcessor.OrderEffect.setBackNames ord

                        for b, a in List.zip (ord |> Order.toOrdVars) (after |> Order.toOrdVars) do
                            if
                                b |> OrderVariable.hasOneValue
                                && setBack |> Set.contains (b |> OrderVariable.getName) |> not
                                && a.Variable.Values <> b.Variable.Values
                            then
                                $"%s{label}: %s{b |> OrderVariable.getName |> Informedica.GenSolver.Lib.Variable.Name.toString}"
                ]
                |> Expect.isEmpty "the kept variables keep their value"
            }

            test "the value the field held is offered again when it lies within its calculated constraints" {
                [
                    for label, _, _, _, before, _, ovar in recalculated () do
                        if before |> OrderVariable.isWithinConstraints true then
                            let held = before |> baseValues
                            let offered = ovar |> baseValues

                            if held |> Array.forall (fun v -> offered |> Array.contains v) |> not then
                                label
                ]
                |> Expect.isEmpty "the held value is in the list"
            }

            // a pick solves the whole order, so one field per recipe is picked; the fields of the
            // other commands of the recipe are calculated from the same order
            test "the first, middle and last value the field of the first command of a recipe offers each solve" {
                [
                    for label, _, _, _, _, after, ovar in recalculatedWith (List.truncate 1) do
                        let n = ovar |> baseValues |> Array.length

                        for nth in [ 0; n / 2; n - 1 ] |> List.distinct do
                            if n > 1 then
                                match after |> pick nth ovar with
                                | Ok _ -> ()
                                | Error _ -> $"%s{label}: value %i{nth}"
                ]
                |> Expect.isEmpty "every pick solves"
            }

            test "tpn: the item rate recalculated after the median rate gives the rate list back" {
                let ord =
                    let processed = Scenarios.tpn |> processed
                    let qty = processed |> doseQuantity
                    let n = qty |> baseValues |> Array.length

                    match processed |> pick (n / 2) qty with
                    | Ok ord -> ord
                    | Error(_, msgs) -> failtest $"the dose quantity pick: %A{msgs}"

                let rates = [| 25N .. 29N |]
                ord
                |> rate
                |> Tests.TestOrder.valuesOf
                |> Expect.equal "the median rate" [| 27N |]

                match ord |> reCalc (ord |> itemRate) with
                | Error(_, msgs) -> failtest $"the recalculation: %A{msgs}"
                | Ok reCalced ->
                    reCalced
                    |> rate
                    |> Tests.TestOrder.valuesOf
                    |> Expect.equal "25 to 29 mL/uur" rates

                    match reCalced |> pick 0 (reCalced |> rate) with
                    | Error(_, msgs) -> failtest $"the rate pick: %A{msgs}"
                    | Ok picked ->
                        picked |> Order.isSolved |> Expect.isTrue "solved with the picked rate"

                        picked
                        |> reCalc (picked |> itemRate)
                        |> Result.toOption
                        |> Option.map (rate >> Tests.TestOrder.valuesOf)
                        |> Expect.equal "25 to 29 mL/uur again" (Some rates)
            }

            test
                "once timed KCl: the item rate recalculated gives a rate list, every pick keeps the time within 60 to 120 min" {
                let ord = Scenarios.kaliumchlorideOnceTimedText |> Tests.TestOrder.parse |> processed

                let minutes (ord: Order) =
                    match ord.Schedule |> Order.Schedule.toOrdVars |> snd with
                    | Some tme ->
                        tme
                        |> OrderVariable.getValSetValueUnit
                        |> Option.map (ValueUnit.convertTo Units.Time.minute >> ValueUnit.getValue)
                        |> Option.defaultValue [||]
                    | None -> [||]

                match ord |> reCalc (ord |> itemRate) with
                | Error(_, msgs) -> failtest $"the recalculation: %A{msgs}"
                | Ok reCalced ->
                    let n = reCalced |> rate |> Tests.TestOrder.valuesOf |> Array.length
                    n > 1 |> Expect.isTrue "more than one rate to pick"

                    for nth in 0 .. n - 1 do
                        match reCalced |> pick nth (reCalced |> rate) with
                        | Error(_, msgs) -> failtest $"rate pick %i{nth}: %A{msgs}"
                        | Ok picked ->
                            let time = picked |> minutes
                            time |> Array.isEmpty |> Expect.isFalse "a time"

                            time
                            |> Array.forall (fun m -> m >= 60N && m <= 120N)
                            |> Expect.isTrue $"time %A{time} min within 60 to 120"
            }
        ]


/// The component quantities of a solved preparation set again on an order that was made again,
/// over the parenteral nutrition fixture.
[<Tests>]
let componentQuantityTests =
    let name (cmp: Component) = cmp.Name |> WrappedString.Name.toString

    // the order as a rebuild makes it: the constraints calculated, no value chosen
    let rebuiltOf scenario =
        scenario |> Tests.TestOrder.toOrder |> Tests.TestOrder.solve CalcMinMax

    let rebuilt () = Scenarios.tpn |> rebuiltOf

    // every component set to its median quantity, as a prescriber picks them
    let preparedOf scenario =
        let ord = scenario |> rebuiltOf

        ord.Orderable.Components
        |> List.map name
        |> List.fold
            (fun ord cmp ->
                ord
                |> run (fun o -> ChangeProperty(o, SetMedianComponentOrderableQuantity cmp))
                |> Result.defaultValue ord
            )
            ord

    let prepared () = Scenarios.tpn |> preparedOf

    let quantities (ord: Order) =
        ord.Orderable.Components
        |> List.map (fun cmp ->
            name cmp,
            cmp.OrderableQuantity
            |> OrderVariable.Quantity.toOrdVar
            |> Tests.TestOrder.valuesOf
        )

    let setAgain qtys ord = ord |> run (fun o -> SetComponentQuantities(o, qtys))

    // the names of the variables that hold a list of values
    let withValueLists ord =
        ord
        |> Order.toOrdVars
        |> List.filter (_.Variable >> Informedica.GenSolver.Lib.Variable.getValueRange >> _.IsValSet)
        |> List.map (_.Variable.Name >> Informedica.GenSolver.Lib.Variable.Name.toString)

    testList
        "the component quantities set again"
        [
            test "an order whose components do not all hold one quantity has no component quantities" {
                rebuilt () |> OrderContext.componentQuantities |> Expect.isNone "not solved"
            }

            test "the component quantities are the quantity of every component" {
                let ord = prepared ()

                match ord |> OrderContext.componentQuantities with
                | Some qtys ->
                    qtys
                    |> List.map fst
                    |> Expect.equal "every component" (ord.Orderable.Components |> List.map name)
                | None -> failtest "the preparation is not solved"
            }

            test "the quantities are set again on an order made again" {
                let prep = prepared ()
                let qtys = prep |> OrderContext.componentQuantities |> Option.get

                match rebuilt () |> setAgain qtys with
                | Ok ord ->
                    ord
                    |> quantities
                    |> Expect.equal "the quantities of the preparation" (prep |> quantities)
                | Error(_, f) -> failtest $"not set: %A{f}"
            }

            test "a quantity the order does not allow leaves its component open, the others are set" {
                let prep = prepared ()
                let qtys = prep |> OrderContext.componentQuantities |> Option.get
                let first = qtys |> List.head |> fst

                let qtys =
                    qtys
                    |> List.map (fun (n, q) ->
                        if n = first then
                            n, 100000N |> ValueUnit.singleWithUnit Units.Volume.milliLiter
                        else
                            n, q
                    )

                match rebuilt () |> setAgain qtys with
                | Error(_, f) -> failtest $"not set: %A{f}"
                | Ok ord ->
                    let set = ord |> quantities |> Map.ofList
                    set[first] |> Array.length |> Expect.notEqual "the first open" 1

                    prep
                    |> quantities
                    |> List.filter (fst >> (<>) first)
                    |> List.forall (fun (n, q) -> set[n] = q)
                    |> Expect.isTrue "the others set"
            }

            test "the context keeps the quantities, the previous diluent's on the new diluent" {
                let prep = prepared ()
                let diluent = prep.Orderable.Components |> List.last |> name
                let sc = OrderPlanTests.EvaluateFixtures.pcmContext.Scenarios |> Array.exactlyOne

                // the previous order named its diluent otherwise; only its names and quantities are read
                let previousOrder =
                    { prep with
                        Order.Orderable.Components =
                            prep.Orderable.Components
                            |> List.map (fun cmp ->
                                if name cmp = diluent then
                                    { cmp with Name = "the previous diluent" |> WrappedString.Name.fromString }
                                else
                                    cmp
                            )
                    }

                let previous =
                    { OrderPlanTests.EvaluateFixtures.pcmContext with
                        Scenarios =
                            [|
                                { sc with
                                    Order = previousOrder
                                    Diluent = Some "the previous diluent"
                                }
                            |]
                    }

                let ctx =
                    { OrderPlanTests.EvaluateFixtures.pcmContext with
                        Scenarios =
                            [|
                                { sc with
                                    Order = rebuilt ()
                                    Diluent = Some diluent
                                }
                            |]
                    }

                ctx
                |> OrderContext.setComponentQuantities OrderLogging.noOp previous
                |> _.Scenarios
                |> Array.exactlyOne
                |> _.Order
                |> quantities
                |> Expect.equal "every quantity, the diluent's too" (prep |> quantities)
            }

            test "a context whose previous preparation is not solved stays as rebuilt" {
                let sc = OrderPlanTests.EvaluateFixtures.pcmContext.Scenarios |> Array.exactlyOne
                let ctx =
                    { OrderPlanTests.EvaluateFixtures.pcmContext with Scenarios = [| { sc with Order = rebuilt () } |] }

                ctx
                |> OrderContext.setComponentQuantities OrderLogging.noOp ctx
                |> _.Scenarios
                |> Array.exactlyOne
                |> _.Order
                |> quantities
                |> Expect.equal "as rebuilt" (rebuilt () |> quantities)
            }

            test "an order with at most two components gets the value lists of an order made again" {
                let qtys = Scenarios.amfo |> preparedOf |> OrderContext.componentQuantities |> Option.get

                let made = Scenarios.amfo |> rebuiltOf

                match made |> setAgain qtys, made |> run CalcValues with
                | Ok ord, Ok again ->
                    ord
                    |> withValueLists
                    |> Expect.equal "the value lists" (again |> withValueLists)
                | _ -> failtest "not calculated"
            }

            test "a refused component selection leaves the order of the context as sent" {
                // a rate picked besides the component quantities, which setting them again would undo
                let prep =
                    prepared ()
                    |> run (fun o -> ChangeProperty(o, SetMedianOrderableDoseRate))
                    |> Result.defaultWith (fun _ -> failtest "no rate")

                let sc = OrderPlanTests.EvaluateFixtures.pcmContext.Scenarios |> Array.exactlyOne

                let sent =
                    { OrderPlanTests.EvaluateFixtures.pcmContext with Scenarios = [| { sc with Order = prep } |] }

                match
                    OrderContext.SetNthComponents(sent, [| 0 |])
                    |> OrderContext.evaluateOutcome Scenarios.testStart OrderLogging.noOp (OrderPlanTests.NoRules())
                with
                | Ok(Refused(cmd, _)) ->
                    (cmd |> OrderContext.Command.get).Scenarios
                    |> Array.exactlyOne
                    |> _.Order
                    |> Expect.equal "the order as sent" prep
                | other -> failtest $"expected a refusal, got %A{other}"
            }

            test "a component selection restores the quantities of the context as sent" {
                let prep = prepared ()
                let sc = OrderPlanTests.EvaluateFixtures.pcmContext.Scenarios |> Array.exactlyOne

                let sent =
                    { OrderPlanTests.EvaluateFixtures.pcmContext with Scenarios = [| { sc with Order = prep } |] }

                let rebuiltCtx =
                    { OrderPlanTests.EvaluateFixtures.pcmContext with Scenarios = [| { sc with Order = rebuilt () } |] }

                rebuiltCtx
                |> OrderContext.restoreQuantities OrderLogging.noOp (OrderContext.SetNthComponents(sent, [| 0 |]))
                |> _.Scenarios
                |> Array.exactlyOne
                |> _.Order
                |> quantities
                |> Expect.equal "the quantities of the context as sent" (prep |> quantities)
            }

            test "the plan route restores on an evaluated context, not on a refused one" {
                let provider = OrderPlanTests.RefusedFixtures.provider ()
                let restore (ctx: OrderContext) =
                    { ctx with Filter = { ctx.Filter with Indication = Some "restored" } }

                let indication doseType =
                    OrderPlanTests.RefusedFixtures.picked provider (Some "paracetamol") (Some "RECTAAL") (Some doseType)
                    |> PlanContext.create "ctx" OrderCategory.Medication
                    |> PlanContext.evaluateSent
                        Scenarios.testStart
                        OrderLogging.noOp
                        provider
                        (fun () -> [||])
                        restore
                        OrderContext.UpdateOrderContext
                    |> Result.map (
                        function
                        | Evaluated pc -> "evaluated", pc.Context.Filter.Indication
                        | Refused(pc, _) -> "refused", pc.Context.Filter.Indication
                    )

                indication OrderPlanTests.RefusedFixtures.maintenance
                |> Expect.equal "restored" (Ok("evaluated", Some "restored"))

                indication OrderPlanTests.RefusedFixtures.noText
                |> Expect.equal "as sent" (Ok("refused", None))
            }
        ]


/// The filter holds every component of the dose rule in the order of the rule, set once from the first
/// scenario, so that an additive a selection leaves out keeps its place.
// here and not in FilterCommandTests, since they need Tests.TestOrder, which compiles after it
[<Tests>]
let doseRuleComponentsTests =
    let name (cmp: Component) = cmp.Name |> WrappedString.Name.toString

    let tpn () =
        Scenarios.tpn |> Tests.TestOrder.toOrder |> Tests.TestOrder.solve CalcMinMax

    // the paracetamol context with the order given: the update reads only the order and the diluent of
    // the scenario for the field, so the rest of the context does not matter
    let contextWith diluent (ord: Order) =
        let ctx = OrderPlanTests.EvaluateFixtures.pcmContext
        let sc = ctx.Scenarios |> Array.exactlyOne

        { ctx with
            Filter = { ctx.Filter with DoseRuleComponents = [||] }
            Scenarios =
                [|
                    { sc with
                        Order = ord
                        Diluent = diluent
                    }
                |]
        }

    let tpnNames = [| "Samenstelling C"; "NaCl 3%"; "KCl 7,4%"; "gluc 10%" |]

    let update ctx =
        ctx |> OrderContext.updateFilterIfOneScenario |> _.Filter.DoseRuleComponents

    testList
        "the dose rule components of the filter"
        [
            test "the first scenario gives every component of the dose rule in the order of the rule" {
                tpn () |> contextWith None |> update |> Expect.equal "every name" tpnNames
            }

            test "the diluent of the order is not a component of the dose rule" {
                tpn ()
                |> contextWith (Some "gluc 10%")
                |> update
                |> Expect.equal "without the diluent" (tpnNames |> Array.take 3)
            }

            test "a scenario after a selection leaves the components as they are" {
                let first = tpn () |> contextWith None |> OrderContext.updateFilterIfOneScenario
                let ord = tpn ()
                let sc = first.Scenarios |> Array.exactlyOne

                // the order made after a selection that left out the second component
                let selected = { ord with Order.Orderable.Components = ord.Orderable.Components |> List.removeAt 1 }

                { first with Scenarios = [| { sc with Order = selected } |] }
                |> update
                |> Expect.equal "the components of the first scenario" tpnNames
            }

            test "no scenario, or more than one, sets nothing" {
                let ctx = tpn () |> contextWith None

                { ctx with Scenarios = [||] } |> update |> Expect.isEmpty "no scenario"

                { ctx with Scenarios = Array.append ctx.Scenarios ctx.Scenarios }
                |> update
                |> Expect.isEmpty "two scenarios"
            }

            test "emptying the components field empties the dose rule components" {
                { OrderContext.emptyFilter with DoseRuleComponents = tpnNames }
                |> OrderContext.clearField FilterField.Components
                |> _.DoseRuleComponents
                |> Expect.isEmpty "emptied"
            }

            test "letting go of the selection keeps the dose rule components" {
                { OrderContext.emptyFilter with DoseRuleComponents = tpnNames }
                |> OrderContext.clearChoice FilterField.Components
                |> _.DoseRuleComponents
                |> Expect.equal "kept" tpnNames
            }

            test "a filter stored before the field existed reads it as empty" {
                match
                    { (OrderContext.emptyFilter |> Filter.Dto.toDto) with DoseRuleComponents = null }
                    |> Filter.Dto.fromDto
                with
                | Ok f -> f.DoseRuleComponents |> Expect.isEmpty "empty"
                | Error e -> failtest $"%A{e}"
            }

            test "the dose rule components go to the Dto and back" {
                match
                    { OrderContext.emptyFilter with DoseRuleComponents = tpnNames }
                    |> Filter.Dto.toDto
                    |> Filter.Dto.fromDto
                with
                | Ok f -> f.DoseRuleComponents |> Expect.equal "the same" tpnNames
                | Error e -> failtest $"%A{e}"
            }
        ]
