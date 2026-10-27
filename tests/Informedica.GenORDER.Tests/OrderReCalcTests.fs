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


[<Tests>]
let tests =
    testList
        "OrderProcessor recalculation"
        [
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
