/// The helpers that hold a component to one combination of item concentrations, and that join the
/// orders per combination again.
module ConcentrationTests

open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip


module ValueRange = Informedica.GenSolver.Lib.Variable.ValueRange
module Minimum = ValueRange.Minimum
module Component = Order.Orderable.Component


let mL (vs: int list) =
    vs
    |> List.map BigRational.fromInt
    |> List.toArray
    |> ValueUnit.withUnit Units.Volume.milliLiter


/// A min, increment and max in mL, each limit inclusive.
let minIncrMax min incr max =
    ValueRange.create
        (mL [ min ] |> Minimum.create true |> Some)
        (mL [ incr ] |> ValueRange.Increment.create |> Some)
        (mL [ max ] |> ValueRange.Maximum.create true |> Some)
        None


let valSet vs =
    mL vs |> ValueRange.ValueSet.create |> Informedica.GenSolver.Lib.Types.ValSet


let unionRanges = Variable.ValueRange.unionRanges


/// The first component of the order of the medication, as it is built.
let firstComponent med =
    (med |> Tests.TestOrder.toOrder).Orderable.Components |> List.head


/// The values an item holds its component concentration to, per item.
let itemConcentrations (cmp: Component) =
    cmp.Items
    |> List.map (fun itm ->
        itm.ComponentConcentration
        |> OrderVariable.Concentration.toOrdVar
        |> _.DefinedConstraints.Values
        |> Option.map (ValueRange.ValueSet.toValueUnit >> ValueUnit.getValue)
    )


/// The number of strengths each item of a component holds.
let strengthCounts (cmp: Component) =
    cmp.Items
    |> List.map (fun itm ->
        itm.ComponentConcentration
        |> OrderVariable.Concentration.toOrdVar
        |> OrderVariable.tryValueList
        |> Option.map ValueRange.ValueSet.count
        |> Option.defaultValue 1
    )


/// The number of strengths an item concentration holds.
let strengthCount ovar =
    ovar
    |> OrderVariable.tryValueList
    |> Option.map ValueRange.ValueSet.count
    |> Option.defaultValue 1


/// A volume order variable with the range.
let volume vr =
    let ovar =
        OrderVariable.createNew ("test" |> Informedica.GenSolver.Lib.Variable.Name.createExc) Units.Volume.milliLiter

    { ovar with OrderVariable.Variable.Values = vr }


[<Tests>]
let tests =
    testList
        "concentrations"
        [
            testList
                "ValueRange.unionRanges"
                [
                    test "the same range twice is that range" {
                        [ minIncrMax 1 1 3; minIncrMax 1 1 3 ]
                        |> unionRanges 10
                        |> Expect.equal "the range" (minIncrMax 1 1 3)
                    }

                    test "ranges that can be listed give one list" {
                        [ valSet [ 1; 3 ]; minIncrMax 2 2 6 ]
                        |> unionRanges 10
                        |> Expect.equal "1, 2, 3, 4 and 6 mL" (valSet [ 1; 2; 3; 4; 6 ])
                    }

                    test "ranges past the cap give the lowest minimum and highest maximum on their increments" {
                        // the increments 1 and 2 mL: 2 is a multiple of 1, so 1 mL remains
                        [ minIncrMax 1 1 100; minIncrMax 50 2 200 ]
                        |> unionRanges 10
                        |> Expect.equal "1 to 200 mL on 1 mL" (minIncrMax 1 1 200)
                    }
                ]

            testList
                "OrderVariable"
                [
                    test "fixToValues sets the value lists and narrows the range, unless the range is set back" {
                        let ovar = minIncrMax 1 1 10 |> volume
                        let vu = mL [ 2; 4 ]

                        match ovar |> OrderVariable.fixToValues (fun _ -> false) vu with
                        | None -> failtest "the range keeps 2 and 4 mL"
                        | Some fixedOvar ->
                            fixedOvar.Variable.Values |> Expect.equal "narrowed" (valSet [ 2; 4 ])

                            fixedOvar.DefinedConstraints.Values
                            |> Expect.equal "the defined list" (vu |> ValueRange.ValueSet.create |> Some)

                        ovar
                        |> OrderVariable.fixToValues (fun _ -> true) vu
                        |> Option.map _.Variable.Values
                        |> Expect.equal "the range kept" (Some(minIncrMax 1 1 10))
                    }

                    test "fixToValues gives none when the range keeps no value" {
                        minIncrMax 1 1 10
                        |> volume
                        |> OrderVariable.fixToValues (fun _ -> false) (mL [ 20 ])
                        |> Expect.isNone "20 mL lies outside 1 to 10 mL"
                    }

                    test "tryValueList gives the list of the range, else of the constraints" {
                        valSet [ 1; 2 ]
                        |> volume
                        |> OrderVariable.tryValueList
                        |> Expect.equal "the range" (mL [ 1; 2 ] |> ValueRange.ValueSet.create |> Some)

                        minIncrMax 1 1 10
                        |> volume
                        |> OrderVariable.tryValueList
                        |> Expect.isNone "no list"
                    }

                    test "unionPerConcentration joins the ranges and keeps the constraints of the original" {
                        let orig = minIncrMax 1 1 10 |> volume

                        OrderVariable.unionPerConcentration
                            10
                            false
                            orig
                            [ valSet [ 1 ] |> volume; valSet [ 3 ] |> volume ]
                        |> fun ovar -> ovar.Variable.Values, ovar.CalculatedConstraints
                        |> Expect.equal
                            "1 and 3 mL, the original constraints"
                            (valSet [ 1; 3 ], orig.CalculatedConstraints)
                    }
                ]

            testList
                "Component.concentrations"
                [
                    test "a component with products gives one component per product, each held to its strengths" {
                        for med, n in
                            [
                                Scenarios.amoxClavText |> Tests.TestOrder.parse, 2
                                Scenarios.amoxClavEveryCombinationText |> Tests.TestOrder.parse, 4
                            ] do
                            let cmps = med |> firstComponent |> Component.concentrations (fun _ -> true)
                            cmps |> List.length |> Expect.equal "one per product" n

                            cmps
                            |> List.forall (itemConcentrations >> List.forall (Option.exists (Array.length >> (=) 1)))
                            |> Expect.isTrue "every item held to one strength"

                            cmps
                            |> List.map itemConcentrations
                            |> List.distinct
                            |> List.length
                            |> Expect.equal "different strengths" n
                    }

                    test "products with the same strengths stay together, with their contents as one list" {
                        let cmp = Scenarios.amoxClavText |> Tests.TestOrder.parse |> firstComponent
                        let p = cmp.Products |> List.head
                        let twin = { p with FormQuantities = mL [ 999 ] |> Some }
                        let cmp = { cmp with Products = twin :: cmp.Products }

                        let cmps = cmp |> Component.concentrations (fun _ -> true)
                        cmps |> List.length |> Expect.equal "still one per strength" 2

                        cmps
                        |> List.exists (fun c ->
                            c.ComponentQuantity
                            |> OrderVariable.Quantity.toOrdVar
                            |> _.DefinedConstraints.Values
                            |> Option.exists (fun vs ->
                                vs
                                |> ValueRange.ValueSet.toValueUnit
                                |> ValueUnit.getValue
                                |> Array.contains 999N
                            )
                        )
                        |> Expect.isTrue "the twin's content is in the list"
                    }

                    test "a product with a form quantity without values counts as no known content" {
                        let cmp = Scenarios.amoxClavText |> Tests.TestOrder.parse |> firstComponent
                        let empty = [||] |> ValueUnit.withUnit Units.Volume.milliLiter

                        let cmp =
                            { cmp with
                                Products = cmp.Products |> List.map (fun p -> { p with FormQuantities = Some empty })
                            }

                        let cmps = cmp |> Component.concentrations (fun _ -> true)
                        cmps |> List.length |> Expect.equal "one per product" 2

                        cmps
                        |> List.forall (fun c -> c.ComponentQuantity = cmp.ComponentQuantity)
                        |> Expect.isTrue "the content left as it is"
                    }

                    test "a component without products gives every combination of its item strengths" {
                        for med in [ Scenarios.cotrim; Scenarios.pcmSupp ] do
                            let cmp = med |> firstComponent

                            cmp
                            |> Component.concentrations (fun _ -> true)
                            |> List.length
                            |> Expect.equal
                                "the product of the strength counts"
                                (cmp |> strengthCounts |> List.fold (*) 1)
                    }

                    test "a combination that keeps no value of a variable the action keeps drops out" {
                        let cmp = Scenarios.pcmSupp |> firstComponent
                        let itm = cmp.Items |> List.head

                        let cnc = itm.ComponentConcentration |> OrderVariable.Concentration.toOrdVar

                        // the first, lowest, strength in the unit of the list
                        let one =
                            cnc
                            |> OrderVariable.tryValueList
                            |> Option.map ValueRange.ValueSet.toValueUnit
                            |> Option.map (fun vu ->
                                vu
                                |> ValueUnit.getValue
                                |> Array.head
                                |> ValueUnit.singleWithUnit (vu |> ValueUnit.getUnit)
                            )
                            |> Option.defaultWith (fun () -> failtest "no strengths")

                        // held below the second strength by a maximum, so only the first strength fits and the
                        // strengths still come from the constraints
                        let held =
                            { cnc with
                                OrderVariable.Variable.Values =
                                    ValueRange.create None None (one |> ValueRange.Maximum.create true |> Some) None
                            }

                        let cmp = { cmp with Items = [ { itm with ComponentConcentration = held |> Concentration } ] }

                        cmp
                        |> Component.concentrations (fun _ -> false)
                        |> List.length
                        |> Expect.equal "the held strength only" 1

                        cmp
                        |> Component.concentrations (fun _ -> true)
                        |> List.length
                        |> Expect.equal "every strength when the action sets back" (cnc |> strengthCount)
                    }
                ]

            testList
                "the split per concentration"
                [
                    let caps = Order.ValueCaps.defaults
                    let logger = OrderLogging.noOp

                    let effects action ord =
                        ord
                        |> OrderProcessor.OrderAction.effects action
                        |> Result.defaultWith (fun f -> failtest $"no effects: %A{f}")

                    let whole action ord =
                        ord |> OrderProcessor.OrderEffect.run caps logger (ord |> effects action)

                    let values (ovar: OrderVariable) =
                        ovar
                        |> OrderVariable.getValSetValueUnit
                        |> Option.map (ValueUnit.getBaseValue >> Set.ofArray)
                        |> Option.defaultValue Set.empty

                    let amoxDose (ord: Order) =
                        ord.Orderable.Components
                        |> List.head
                        |> _.Items
                        |> List.head
                        |> _.Dose.Quantity
                        |> OrderVariable.Quantity.toOrdVar

                    test "an order with one combination of concentrations runs its effects as they are" {
                        [
                            for name, med in Tests.ReferenceTests.fixtures () do
                                let ord = med |> Tests.TestOrder.toOrder

                                if ord |> Order.splitPerConcentration 16 (fun _ -> true) |> List.length = 1 then
                                    match
                                        ord |> OrderProcessor.OrderAction.run caps logger OrderAction.CalcConstraints,
                                        ord |> whole OrderAction.CalcConstraints
                                    with
                                    | Ok a, Ok b when a = b -> ()
                                    | _ -> name
                        ]
                        |> Expect.isEmpty "the same order"
                    }

                    test "amoxClav in four products offers the dose each product reaches, and no other" {
                        let ord =
                            Scenarios.amoxClavEveryCombinationText
                            |> Tests.TestOrder.parse
                            |> Tests.TestOrder.toOrder
                        let parts = ord |> Order.splitPerConcentration 16 (fun _ -> true)
                        parts |> List.length |> Expect.equal "four products" 4

                        let perPart =
                            parts
                            |> List.choose (whole OrderAction.CalcConstraints >> Result.toOption)
                            |> List.map (amoxDose >> values)
                            |> Set.unionMany

                        match ord |> OrderProcessor.OrderAction.run caps logger OrderAction.CalcConstraints with
                        | Error(_, f) -> failtest $"not created: %A{f}"
                        | Ok created ->
                            created
                            |> amoxDose
                            |> values
                            |> Expect.equal "the doses of the products" perPart

                            ord
                            |> whole OrderAction.CalcConstraints
                            |> Result.toOption
                            |> Option.map (amoxDose >> values >> Set.count)
                            |> Expect.equal "the whole order offers one dose" (Some 1)
                    }

                    test "a list beside a stepped range that cannot be listed is no listed union" {
                        let ord = Scenarios.pcmDrink |> Tests.TestOrder.toOrder
                        let qty = ord.Orderable.Dose.Quantity |> OrderVariable.Quantity.toOrdVar
                        let withRange vr =
                            ord |> Order.fromOrdVars [ { qty with OrderVariable.Variable.Values = vr } ]

                        let stepped max =
                            ValueRange.create
                                (mL [ 2 ] |> Minimum.create true |> Some)
                                (mL [ 2 ] |> ValueRange.Increment.create |> Some)
                                (mL [ max ] |> ValueRange.Maximum.create true |> Some)
                                None

                        [
                            [ withRange (valSet [ 1 ]); withRange (stepped 1000) ]
                            |> Order.isListedUnion 200
                            [ withRange (valSet [ 1 ]); withRange (stepped 20) ] |> Order.isListedUnion 200
                        ]
                        |> Expect.equal "the long stepped range is not, the short one is" [ false; true ]
                    }

                    test "an order whose union per concentration cannot be listed is created whole" {
                        let ord = Scenarios.amfoWithNaClTwoStrengthsTimed |> Tests.TestOrder.toOrder

                        ord
                        |> Order.splitPerConcentration 16 (fun _ -> true)
                        |> List.length
                        |> Expect.equal "two NaCl strengths" 2

                        match
                            ord |> OrderProcessor.OrderAction.run caps logger OrderAction.CalcConstraints,
                            ord |> whole OrderAction.CalcConstraints
                        with
                        | Ok a, Ok b -> a |> Expect.equal "the whole order" b
                        | _ -> failtest "not created"
                    }

                    testList
                        "the original order of a recalculation"
                        [
                            let solved =
                                lazy (Scenarios.amoxClavText |> Tests.TestOrder.parse |> Tests.TestOrder.processed)

                            let parts (ord: Order) = ord |> Order.splitPerConcentration 16 (fun _ -> true)

                            // whether the effects for the order of one combination keep the original
                            let keeps original part =
                                [
                                    OrderEffect.EnumerateOrderVariableValues(
                                        ValueCap.Larger,
                                        RateList.WithRate,
                                        Some original
                                    )
                                ]
                                |> OrderProcessor.OrderEffect.forConcentration part
                                |> List.exists (
                                    function
                                    | OrderEffect.EnumerateOrderVariableValues(_, _, Some _) -> true
                                    | _ -> false
                                )

                            test "the original is kept for the product it holds, and dropped for the other" {
                                let ord = solved.Value

                                ord
                                |> parts
                                |> List.map (keeps ord)
                                |> List.sort
                                |> Expect.equal "one product keeps it" [ false; true ]
                            }

                            test "an original that holds no single concentration is kept for every product" {
                                let ord = solved.Value
                                let created = Scenarios.amoxClavText |> Tests.TestOrder.parse |> Tests.TestOrder.toOrder

                                ord
                                |> parts
                                |> List.map (keeps created)
                                |> Expect.equal "both keep it" [ true; true ]
                            }

                            test "after each recalculation the value the field held is offered again" {
                                let ord = solved.Value

                                [
                                    for cmd, ovarRef, _ in ord |> OrderReCalcTests.reCalcs do
                                        let before = ovarRef |> OrderProcessor.OrderVariableReference.tryGet ord

                                        match before, ord |> OrderReCalcTests.reCalc cmd with
                                        | Some b, Ok after when b |> OrderVariable.isWithinConstraints true ->
                                            let offered =
                                                ovarRef
                                                |> OrderProcessor.OrderVariableReference.tryGet after
                                                |> Option.map values
                                                |> Option.defaultValue Set.empty

                                            if b |> values |> Set.isSubset offered |> not then
                                                $"%A{cmd}"
                                        | _ -> ()
                                ]
                                |> Expect.isEmpty "the held value offered"
                            }
                        ]
                ]
        ]
