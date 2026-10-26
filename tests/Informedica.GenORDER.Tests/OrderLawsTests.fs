/// The laws of the calculations of an order by the order program, over the fixtures: creating an
/// order, making its values and resetting it, and the counts creation writes.
module OrderLawsTests

open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip


let caps = Order.ValueCaps.defaults


/// The order after the action, with the failure as text.
let run action ord =
    ord
    |> OrderProcessor.OrderAction.run caps OrderLogging.noOp action
    |> Result.mapError (fun (_, f) -> $"%A{f}")


/// The values of every variable of an order, by name.
let values ord =
    ord
    |> Order.toOrdVars
    |> List.map (fun ovar -> ovar.Variable.Name, ovar.Variable.Values)


/// The names of the variables whose values differ between two orders.
let differ ord1 ord2 =
    List.zip (ord1 |> values) (ord2 |> values)
    |> List.filter (fun (a, b) -> a <> b)
    |> List.map (fst >> fst >> Informedica.GenSolver.Lib.Variable.Name.toString)


/// The fixtures, with hydrocortison intravenous, whose rules give the dose count a minimum and no
/// maximum.
let medications () =
    [
        yield! Tests.ReferenceTests.fixtures ()
        "hydrocortisonIv", Scenarios.hydrocortisonIvText |> Tests.TestOrder.parse
    ]


module Variable = Informedica.GenSolver.Lib.Variable
module ValueRange = Variable.ValueRange


/// Whether every value a variable allows is whole: every value of a list, or the min and the
/// increment of a range.
let isWhole (ovar: OrderVariable) =
    let whole vu =
        vu
        |> ValueUnit.getValue
        |> Array.forall (fun (v: BigRational) -> v.Denominator = 1I)

    let vr = ovar.Variable.Values

    match vr |> ValueRange.getValSet with
    | Some vs -> vs |> ValueRange.ValueSet.toValueUnit |> whole
    | None ->
        (vr
         |> ValueRange.getMin
         |> Option.forall (ValueRange.Minimum.toValueUnit >> whole))
        && (vr
            |> ValueRange.getIncr
            |> Option.forall (ValueRange.Increment.toValueUnit >> whole))


/// Whether the lowest value a variable allows is at least 1: its min is above 1, or is 1 and
/// inclusive. A variable without a min is not.
let isAtLeastOne (ovar: OrderVariable) =
    match ovar.Variable.Values |> ValueRange.getMin with
    | Some m ->
        let v = m |> ValueRange.Minimum.toValueUnit |> ValueUnit.getValue |> Array.min
        v > 1N || (v = 1N && m |> ValueRange.Minimum.isIncl)
    | None -> false


/// The failures of the counts of an order: a frequency that is not whole, an order count that is
/// not whole or below 1, a dose count below 1.
let countFailures (ord: Order) =
    let frq = ord.Schedule |> Order.Schedule.toOrdVars |> fst
    let orderCount = ord.Orderable.OrderCount |> OrderVariable.Count.toOrdVar
    let doseCount = ord.Orderable.DoseCount |> OrderVariable.Count.toOrdVar

    [
        if frq |> Option.exists (isWhole >> not) then
            "frequency not whole"
        if (orderCount |> isWhole && orderCount |> isAtLeastOne) |> not then
            "order count not whole or below 1"
        if doseCount |> isAtLeastOne |> not then
            "dose count below 1"
    ]


/// The defined constraints of the order count and the dose count of an order.
let countConstraints (ord: Order) =
    ord.Orderable.OrderCount
    |> OrderVariable.Count.toOrdVar
    |> OrderVariable.getConstraints,
    ord.Orderable.DoseCount
    |> OrderVariable.Count.toOrdVar
    |> OrderVariable.getConstraints


/// Per fixture: the order as made from the medication, the created order, the order with values
/// and the solved order, as the dose dialog gets them.
let fixtures =
    lazy
        [
            for name, med in medications () do
                let ord = med |> Tests.TestOrder.toOrder

                let created =
                    ord
                    |> run OrderAction.CalcConstraints
                    |> Result.defaultWith (fun e -> failtest $"%s{name} not created: %s{e}")

                let valued =
                    created
                    |> run OrderAction.CalcValues
                    |> Result.defaultWith (fun e -> failtest $"%s{name} no values: %s{e}")

                name, ord, created, valued, valued |> Tests.TestOrder.solve SolveOrder
        ]


[<Tests>]
let tests =
    testList
        "OrderProcessor calculation laws"
        [
            test "every fixture is created and gets its values" {
                fixtures.Value
                |> List.length
                |> Expect.equal "every fixture" (medications () |> List.length)
            }

            test "the commands of the order context run as the actions of the program" {
                [
                    for name, ord, created, valued, solved in fixtures.Value do
                        for cmd, action, before in
                            [
                                CalcMinMax, OrderAction.CalcConstraints, ord
                                CalcValues, OrderAction.CalcValues, created
                                ReCalcValues, OrderAction.Reset, solved
                            ] do
                            match
                                cmd before |> OrderProcessor.processPipeline OrderLogging.noOp, before |> run action
                            with
                            | Ok a, Ok b when differ a b |> List.isEmpty -> ()
                            | _ -> $"%s{name} %A{action}"
                ]
                |> Expect.isEmpty "the command gives what the action gives"
            }

            test "a reset gives the same order whatever was set before" {
                [
                    for name, _, created, _, solved in fixtures.Value do
                        // the created order before its norm dose, every variable back to its calculated constraints
                        let first =
                            created
                            |> OrderProcessor.OrderEffect.run
                                caps
                                OrderLogging.noOp
                                [ OrderEffect.SetAllOrderVariableRangesBack SetBackTo.Calculated ]
                            |> Result.mapError (fun (_, f) -> $"%A{f}")
                            |> Result.bind (run OrderAction.Reset)

                        match first, solved |> run OrderAction.Reset with
                        | Ok a, Ok b ->
                            for n in differ a b do
                                $"%s{name}: %s{n}"
                        | a, b -> $"%s{name}: first reset ok %b{a |> Result.isOk}, reset ok %b{b |> Result.isOk}"
                ]
                |> Expect.isEmpty "a reset forgets every set"
            }

            test "a second reset changes nothing" {
                [
                    for name, _, _, _, solved in fixtures.Value do
                        match
                            solved
                            |> run OrderAction.Reset
                            |> Result.bind (fun a -> a |> run OrderAction.Reset |> Result.map (fun b -> a, b))
                        with
                        | Ok(a, b) ->
                            for n in differ a b do
                                $"%s{name}: %s{n}"
                        | Error e -> $"%s{name}: %s{e}"
                ]
                |> Expect.isEmpty "a reset of a reset order"
            }

            test "making the values of a solved order changes nothing" {
                [
                    for name, _, _, _, solved in fixtures.Value do
                        match solved |> run OrderAction.CalcValues with
                        | Ok after ->
                            for n in differ solved after do
                                $"%s{name}: %s{n}"
                        | Error e -> $"%s{name}: %s{e}"
                ]
                |> Expect.isEmpty "the solved order kept"
            }

            test "a solve for min and max after each calculation changes nothing" {
                [
                    for name, _, created, valued, solved in fixtures.Value do
                        let reset = solved |> run OrderAction.Reset

                        for label, res in [ "created", Ok created; "valued", Ok valued; "reset", reset ] do
                            match
                                res
                                |> Result.bind (
                                    OrderProcessor.OrderEffect.run
                                        caps
                                        OrderLogging.noOp
                                        [ OrderEffect.SolveOrder SolveMode.MinMax ]
                                    >> Result.mapError (fun (_, f) -> $"%A{f}")
                                )
                            with
                            | Ok after ->
                                for n in differ (res |> Result.defaultValue after) after do
                                    $"%s{name} %s{label}: %s{n}"
                            | Error e -> $"%s{name} %s{label}: %s{e}"
                ]
                |> Expect.isEmpty "every equation holds on every result"
            }

            test "creation gives the order count exactly 1 on Once and OnceTimed, else whole and at least 1" {
                let one = ValueUnit.oneCount

                [
                    for name, med in medications () do
                        let cs, _ = med |> Tests.TestOrder.toOrder |> countConstraints

                        match med.OrderType with
                        | OnceOrder
                        | OnceTimedOrder ->
                            if cs.Values |> Option.map ValueRange.ValueSet.toValueUnit <> Some one then
                                $"%s{name}: %A{cs}"
                        | _ ->
                            let min = cs.Min |> Option.map ValueRange.Minimum.toBoolValueUnit
                            let incr = cs.Incr |> Option.map ValueRange.Increment.toValueUnit

                            if min <> Some(true, one) || incr <> Some one then
                                $"%s{name}: %A{cs}"
                ]
                |> Expect.isEmpty "the order count of every fixture"
            }

            test "creation gives the dose count 1 when the rules give it nothing, and keeps what the rules give" {
                [
                    for name, med in medications () do
                        let _, cs = med |> Tests.TestOrder.toOrder |> countConstraints
                        let fromRules = med.DoseCount.Min.IsSome || med.DoseCount.Max.IsSome
                        let isOne = cs.Values |> Option.map ValueRange.ValueSet.toValueUnit = Some ValueUnit.oneCount

                        if fromRules = isOne then
                            $"%s{name}: rules %b{fromRules}, %A{cs}"
                ]
                |> Expect.isEmpty "the dose count of every fixture"

                Scenarios.hydrocortisonIvText
                |> Tests.TestOrder.parse
                |> Tests.TestOrder.toOrder
                |> fun ord -> ord.Orderable.DoseCount |> OrderVariable.Count.toOrdVar
                |> OrderVariable.hasOneValue
                |> Expect.isFalse "the dose count of hydrocortison is free"
            }

            test "creation gives a medication whose rules give no dose count a dose count of 1" {
                let _, cs =
                    { Scenarios.pcmSupp with DoseCount = Informedica.GenCore.Lib.Ranges.MinMax.empty }
                    |> Tests.TestOrder.toOrder
                    |> countConstraints

                cs.Values
                |> Option.map ValueRange.ValueSet.toValueUnit
                |> Expect.equal "exactly 1" (Some ValueUnit.oneCount)
            }

            test "the counts hold on every calculation" {
                [
                    for name, _, created, valued, solved in fixtures.Value do
                        for label, res in
                            [
                                "created", Ok created
                                "valued", Ok valued
                                "solved", Ok solved
                                "reset", solved |> run OrderAction.Reset
                            ] do
                            match res with
                            | Ok ord ->
                                for f in ord |> countFailures do
                                    $"%s{name} %s{label}: %s{f}"
                            | Error e -> $"%s{name} %s{label}: %s{e}"
                ]
                |> Expect.isEmpty "whole frequency and order count, counts at least 1"
            }
        ]


/// Hydrocortison intravenous on the order before the norm dose, where the dose count holds more
/// than one value: created, every variable set back to its calculated constraints, and the values
/// made.
let hydrocortisonBeforeNormDose (med: Medication) =
    let created =
        med
        |> Tests.TestOrder.toOrder
        |> run OrderAction.CalcConstraints
        |> Result.defaultWith (fun e -> failtest $"not created: %s{e}")
        |> OrderProcessor.OrderEffect.run
            caps
            OrderLogging.noOp
            [ OrderEffect.SetAllOrderVariableRangesBack SetBackTo.Calculated ]
        |> Result.defaultWith (fun (_, f) -> failtest $"not set back: %A{f}")

    created |> run OrderAction.CalcValues |> Result.defaultValue created


let diluentDose = OrderVariableReference.ComponentDose("NaCl 0,9%", DoseProperty.Quantity)


/// The order with the values no complete order reaches dropped from the diluent dose.
let dropImpossibleValues ord =
    match
        ord
        |> OrderProcessor.OrderEffect.run caps OrderLogging.noOp [ OrderEffect.DropImpossibleValues diluentDose ]
    with
    | Ok o -> o
    | Error(_, f) -> failtest $"DropImpossibleValues failed: %A{f}"


/// The diluent dose set to each of its values.
let diluentSingles ord =
    diluentDose
    |> OrderProcessor.OrderVariableReference.tryGet ord
    |> Option.bind OrderProcessor.CompleteOrder.singles
    |> Option.defaultValue []


/// Whether a complete order sets the variable to its value.
let isPossible ovar ord =
    ord
    |> OrderProcessor.CompleteOrder.tryFindWith OrderLogging.noOp ovar
    |> Option.isSome


let doseCountIsFree (ord: Order) =
    ord.Orderable.DoseCount
    |> OrderVariable.Count.toOrdVar
    |> OrderVariable.hasOneValue
    |> not


[<Tests>]
let impossibleValuesTests =
    let hydrocortison () = Scenarios.hydrocortisonIvText |> Tests.TestOrder.parse

    testList
        "OrderProcessor impossible values"
        [
            test "with a weight the values no complete order reaches are dropped, and the others kept" {
                let ord = hydrocortison () |> hydrocortisonBeforeNormDose
                ord |> doseCountIsFree |> Expect.isTrue "the dose count is free"

                ord
                |> diluentSingles
                |> List.exists (fun s -> ord |> isPossible s |> not)
                |> Expect.isTrue "the diluent dose offers a value no complete order reaches"

                let narrowed = ord |> dropImpossibleValues

                narrowed
                |> diluentSingles
                |> List.filter (fun s -> narrowed |> isPossible s |> not)
                |> Expect.isEmpty "every value left has a complete order"

                ord
                |> diluentSingles
                |> List.filter (fun s -> ord |> isPossible s)
                |> List.length
                |> Expect.equal "every value with a complete order is kept" (narrowed |> diluentSingles |> List.length)
            }

            test "without a weight only the values a solve refutes are dropped" {
                let ord = { hydrocortison () with Adjust = None } |> hydrocortisonBeforeNormDose
                ord |> doseCountIsFree |> Expect.isTrue "the dose count is free"

                let left = ord |> dropImpossibleValues |> diluentSingles |> List.map _.Variable.Values

                ord
                |> diluentSingles
                |> List.filter (fun s -> left |> List.contains s.Variable.Values |> not)
                |> List.filter (fun s ->
                    ord
                    |> OrderProcessor.CompleteOrder.setAndSolve OrderLogging.noOp s
                    |> Result.isOk
                )
                |> Expect.isEmpty "every value dropped makes the solve fail"
            }

            test "with one dose count nothing is dropped" {
                let ord = Scenarios.pcmDrink |> Tests.TestOrder.processed
                ord |> doseCountIsFree |> Expect.isFalse "the dose count is 1"

                ord
                |> OrderProcessor.OrderEffect.run
                    caps
                    OrderLogging.noOp
                    [
                        OrderEffect.DropImpossibleValues(OrderVariableReference.OrderableDose DoseProperty.Quantity)
                    ]
                |> Result.toOption
                |> Option.map (differ ord)
                |> Expect.equal "the order kept" (Some [])
            }
        ]
