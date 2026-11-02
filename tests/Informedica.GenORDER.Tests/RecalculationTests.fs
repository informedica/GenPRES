/// An order on new patient data: what the patient gets stays, the per kg or per m2 values and the
/// calculated constraints follow.
module RecalculationTests

open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip


module Fixture =

    let logger = OrderLogging.noOp

    let processed = Tests.TestOrder.processed

    let caps = Order.ValueCaps.defaults

    let readjust adjust ord =
        Readjust(ord, adjust) |> OrderProcessor.processPipelineWith caps logger

    /// The order on the new adjust value; the test fails when there is no answer.
    let readjusted adjust ord =
        ord
        |> readjust adjust
        |> Result.defaultWith (fun (_, msgs) -> failtest $"no answer: %A{msgs}")

    let adjustedNames ord =
        ord
        |> OrderProcessor.OrderVariableReference.adjusted
        |> List.choose (OrderProcessor.OrderVariableReference.tryGet ord)
        |> List.map OrderVariable.getName
        |> Set.ofList

    /// The calculated constraints of an order on its adjust value, None when none exist.
    let recalculated ord =
        ord
        |> Order.withoutCalculatedConstraints
        |> OrderProcessor.OrderAction.run caps logger OrderAction.CalcConstraints
        |> Result.toOption

    let unitOf ord =
        ord
        |> Order.adjustValue
        |> Option.map ValueUnit.getUnit
        |> Option.defaultValue Units.Weight.kiloGram

    let adjustOf ord =
        ord
        |> Order.adjustValue
        |> Option.map (ValueUnit.getValue >> Array.head)
        |> Option.defaultValue 0N

    let times (factor: BigRational) ord =
        (ord |> adjustOf) * factor |> ValueUnit.singleWithUnit (ord |> unitOf)

    /// The values of every variable that is not per kg or per m2, by name.
    let givenValues ord =
        let names = ord |> adjustedNames

        ord
        |> Order.toOrdVars
        |> List.filter (fun ovar ->
            let name = ovar |> OrderVariable.getName
            names |> Set.contains name |> not
            && name <> (ord.Adjust |> OrderVariable.Quantity.toOrdVar |> OrderVariable.getName)
        )
        |> List.map (fun ovar -> ovar |> OrderVariable.getName, ovar.Variable.Values)

    let adjustedValues ord =
        let names = ord |> adjustedNames

        ord
        |> Order.toOrdVars
        |> List.filter (fun ovar -> names |> Set.contains (ovar |> OrderVariable.getName))
        |> List.map (fun ovar -> ovar |> OrderVariable.getName, ovar.Variable.Values)

    let scenario ord : OrderScenario =
        {
            No = 1
            Name = "cotrim"
            Indication = ""
            Form = ""
            Route = ""
            DoseType = Informedica.GenForm.Lib.Types.NoDoseType
            Diluent = None
            Component = None
            Item = None
            Diluents = [||]
            Components = [| "cotrimoxazol" |]
            Items = [||]
            Prescription = [||]
            Preparation = [||]
            Administration = [||]
            Order = ord
            UseAdjust = true
            UseRenalRule = false
            RenalRule = None
            FitsOnlyWithoutDivisible = false
            ProductsIds = [||]
            Access = Informedica.GenForm.Lib.Types.AnyAccess
        }

    /// One suppository of 240 mg at 10 to 20 mg/kg: from 25 kg no allowed dose exists.
    let suppository240 =
        let strength =
            [| 240N |]
            |> ValueUnit.withUnit (Units.Mass.milliGram |> ValueUnit.per (Units.General.general "stuk"))

        { Scenarios.pcmSupp with
            Components =
                Scenarios.pcmSupp.Components
                |> List.map (fun c ->
                    { c with
                        Substances = c.Substances |> List.map (fun s -> { s with Concentrations = Some strength })
                    }
                )
        }

    let kg v = v |> ValueUnit.singleWithUnit Units.Weight.kiloGram

    /// The dose of the first item.
    let itemDose (ord: Order) = ord.Orderable.Components[0].Items[0].Dose

    /// The level the client shows for the per kg dose quantity of the first item.
    let perKgLevel ord =
        (ord |> itemDose |> Order.Orderable.Dose.Dto.toDto).QuantityAdjust.Level

    let perKg ord =
        (ord |> itemDose).QuantityAdjust
        |> OrderVariable.QuantityAdjust.toOrdVar
        |> OrderVariable.getValSetValueUnit
        |> Option.map (ValueUnit.getValue >> Array.head)

    /// An order whose dose has no per kg or per m2 variable with a limit.
    let withoutAdjust () =
        { Scenarios.pcmSupp with
            Components =
                Scenarios.pcmSupp.Components
                |> List.map (fun c ->
                    { c with Substances = c.Substances |> List.map (fun s -> { s with Dose = None }) }
                )
        }
        |> processed

    let context scenarios : OrderContext =
        {
            Filter = OrderContext.emptyFilter
            Patient = Patient.patient
            Scenarios = scenarios
            Argumentation = None
        }

    /// One suppository of 240 mg at 200 to 400 mg/m2, on a body surface of 1 m2.
    let perM2 =
        let mgPerM2 = Units.Mass.milliGram |> ValueUnit.per Units.BSA.m2
        let limit v = v |> ValueUnit.singleWithUnit mgPerM2

        let dose (d: Informedica.GenForm.Lib.Types.DoseLimit) =
            { d with QuantityAdjust = Informedica.GenCore.Lib.Ranges.MinMax.createInclIncl (limit 200N) (limit 400N) }

        { suppository240 with
            Adjust = 1N |> ValueUnit.singleWithUnit Units.BSA.m2 |> Some
            Components =
                suppository240.Components
                |> List.map (fun c ->
                    { c with
                        Substances = c.Substances |> List.map (fun s -> { s with Dose = s.Dose |> Option.map dose })
                    }
                )
        }

    let cm v = v |> ValueUnit.singleWithUnit Units.Height.centiMeter

    let fixtures =
        [
            "pcmDrink", Scenarios.pcmDrink
            "pcmSupp", Scenarios.pcmSupp
            "cotrim", Scenarios.cotrim
            "amfo", Scenarios.amfo
            "morfCont", Scenarios.morfCont
            "tpn", Scenarios.tpn
        ]

    let factors = [ 1N / 2N; 9N / 10N; 11N / 10N; 4N / 3N; 2N; 3N ]


[<Tests>]
let tests =
    testList
        "an order on new patient data"
        [
            for name, med in Fixture.fixtures do
                testList
                    name
                    [
                        for factor in Fixture.factors do
                            test $"times %A{factor}: an answer, the given values stay, the order solved" {
                                let ord = med |> Fixture.processed
                                let adjust = ord |> Fixture.times factor

                                let after = ord |> Fixture.readjusted adjust

                                after
                                |> Fixture.adjustOf
                                |> Expect.equal "the new adjust" (ord |> Fixture.adjustOf |> (*) factor)
                                after
                                |> Fixture.givenValues
                                |> Expect.equal "what the patient gets" (ord |> Fixture.givenValues)
                            }

                        test "a wrong value corrected back gives the same order values" {
                            let ord = med |> Fixture.processed

                            let back =
                                ord
                                |> Fixture.readjusted (ord |> Fixture.times 2N)
                                |> Fixture.readjusted (ord |> Fixture.times 1N)

                            back
                            |> Fixture.givenValues
                            |> Expect.equal "the given values" (ord |> Fixture.givenValues)

                            back
                            |> Fixture.adjustedValues
                            |> Expect.equal "the per kg values" (ord |> Fixture.adjustedValues)
                        }
                    ]

            test "a per kg dose follows the weight and leaves its constraints" {
                let ord = Scenarios.cotrim |> Fixture.processed

                let after = ord |> Fixture.readjusted (ord |> Fixture.times 2N)

                after
                |> Order.toOrdVars
                |> List.filter (OrderVariable.isWithinConstraints true >> not)
                |> List.map (OrderVariable.getName >> Informedica.GenSolver.Lib.Variable.Name.toString)
                |> Expect.isNonEmpty "a variable outside its calculated constraints"
            }

            test "every weight from 1 to 150 kg gives an answer and the amounts given stay" {
                [
                    for name, med in Fixture.fixtures do
                        let ord = med |> Fixture.processed

                        for w in [ 1N; 2N; 5N; 8N; 10N; 15N; 20N; 25N; 30N; 40N; 60N; 80N; 100N; 150N ] do
                            match ord |> Fixture.readjust (w |> ValueUnit.singleWithUnit (ord |> Fixture.unitOf)) with
                            | Ok after when (after |> Fixture.givenValues) = (ord |> Fixture.givenValues) -> ()
                            | Ok _ -> $"%s{name} at %A{w}: the amounts given moved"
                            | Error _ -> $"%s{name} at %A{w}: no answer"
                ]
                |> Expect.isEmpty "every weight"
            }

            testList
                "one 240 mg suppository, 10 to 20 mg/kg"
                [
                    test "at 24 kg the dose is 10 mg/kg, the limits can be calculated and there is no alert" {
                        let ord = Fixture.suppository240 |> Fixture.processed
                        let adjust = 24N |> Fixture.kg

                        ord
                        |> Order.withAdjustValue adjust
                        |> Fixture.recalculated
                        |> Expect.isSome "the limits"

                        let after = ord |> Fixture.readjusted adjust

                        after |> Fixture.perKg |> Expect.equal "per kg" (Some 10N)
                        after |> Fixture.perKgLevel |> Expect.equal "normal" OrderVariable.Dto.IsNormal
                    }

                    test "at 25 kg no limits can be calculated, the order stays at 9.6 mg/kg with an alert" {
                        let ord = Fixture.suppository240 |> Fixture.processed
                        let adjust = 25N |> Fixture.kg

                        ord
                        |> Order.withAdjustValue adjust
                        |> Fixture.recalculated
                        |> Expect.isNone "no limits"

                        let after = ord |> Fixture.readjusted adjust

                        after
                        |> Fixture.givenValues
                        |> Expect.equal "the suppository stays" (ord |> Fixture.givenValues)
                        after |> Fixture.perKg |> Expect.equal "per kg" (Some(48N / 5N))
                        after |> Fixture.perKgLevel |> Expect.equal "alert" OrderVariable.Dto.IsAlert
                    }

                    test "a wrong weight corrected back has no alert" {
                        let ord = Fixture.suppository240 |> Fixture.processed

                        let back =
                            ord
                            |> Fixture.readjusted (25N |> Fixture.kg)
                            |> Fixture.readjusted (14N |> Fixture.kg)

                        back |> Fixture.perKgLevel |> Expect.equal "normal" OrderVariable.Dto.IsNormal

                        back
                        |> Fixture.adjustedValues
                        |> Expect.equal "the per kg values" (ord |> Fixture.adjustedValues)
                    }
                ]

            test "the action recalculates the limits, sets the per kg values free and solves" {
                let ord = Scenarios.cotrim |> Fixture.processed

                match ord |> OrderProcessor.OrderAction.effects OrderAction.Readjust with
                | Ok(OrderEffect.RecalculateConstraints _ :: rest) ->
                    rest
                    |> List.last
                    |> Expect.equal "solved" (OrderEffect.SolveOrder SolveMode.Values)

                    rest
                    |> List.take (rest.Length - 1)
                    |> Expect.equal
                        "every per kg variable"
                        (ord
                         |> OrderProcessor.OrderVariableReference.adjusted
                         |> List.map (fun r -> OrderEffect.SetOrderVariableRangeBack(r, SetBackTo.PositiveOffIncrement)))
                | other -> failtest $"other effects: %A{other}"
            }

            test "an order without a per kg or per m2 dose has no effects" {
                let ord = Fixture.withoutAdjust ()
                ord |> EquationMapping.usesAdjust |> Expect.isFalse "no per kg dose"
                match ord |> OrderProcessor.OrderAction.effects OrderAction.Readjust with
                | Ok effs -> effs |> Expect.isEmpty "no effects"
                | Error f -> failtest $"refused: %A{f}"
            }

            test "the new adjust value is in the defined constraints of every fixture" {
                [
                    for name, med in Fixture.fixtures do
                        let ord = med |> Fixture.processed
                        let adjust = ord |> Fixture.times 2N

                        match ord |> Fixture.readjust adjust with
                        | Ok after ->
                            let adj = after.Adjust |> OrderVariable.Quantity.toOrdVar

                            if adj.DefinedConstraints.Values |> Option.isNone then
                                $"%s{name}: no defined value"
                            elif adj |> OrderVariable.isWithinConstraints false |> not then
                                $"%s{name}: the adjust is outside its defined constraints"
                        | Error _ -> $"%s{name}: no answer"
                ]
                |> Expect.isEmpty "every fixture"
            }

            test "the calculated dose quantity follows the weight" {
                let ord = Scenarios.cotrim |> Fixture.processed

                let calculatedMax (o: Order) =
                    o.Orderable.Dose.Quantity
                    |> OrderVariable.Quantity.toOrdVar
                    |> fun ovar -> ovar.CalculatedConstraints.Max

                let after = ord |> Fixture.readjusted (ord |> Fixture.times 2N)

                after
                |> calculatedMax
                |> Expect.notEqual "another maximum" (ord |> calculatedMax)
            }

            test "the context takes the patient and its order follows the weight" {
                let ord = Scenarios.cotrim |> Fixture.processed
                let pat = { Patient.patient with Weight = ord |> Fixture.times 2N |> Some }

                let ctx = [| Fixture.scenario ord |] |> Fixture.context

                match ctx |> OrderContext.updatePatientData Fixture.logger pat with
                | Evaluated after ->
                    after.Patient |> Expect.equal "the patient" pat
                    after.Scenarios[0].Order
                    |> Fixture.adjustOf
                    |> Expect.equal "the weight" (ord |> Fixture.adjustOf |> (*) 2N)
                | Refused _ -> failtest "refused"
            }

            test "a patient without the value leaves the order as it is" {
                let ord = Scenarios.cotrim |> Fixture.processed

                let ctx = [| Fixture.scenario ord |] |> Fixture.context

                match ctx |> OrderContext.updatePatientData Fixture.logger Patient.patient with
                | Evaluated after ->
                    after.Scenarios[0].Order
                    |> Fixture.adjustOf
                    |> Expect.equal "the weight" (ord |> Fixture.adjustOf)
                | Refused _ -> failtest "refused"
            }

            test "a context without a single order is refused and keeps its patient" {
                let ord = Scenarios.cotrim |> Fixture.processed
                let pat = { Patient.patient with Weight = ord |> Fixture.times 2N |> Some }

                for scenarios in [ [||]; [| Fixture.scenario ord; Fixture.scenario ord |] ] do
                    match
                        scenarios
                        |> Fixture.context
                        |> OrderContext.updatePatientData Fixture.logger pat
                    with
                    | Refused(sent, _) -> sent.Patient |> Expect.equal "the patient" Patient.patient
                    | Evaluated _ -> failtest "not refused"
            }

            test "the same weight takes the patient and does not calculate the order again" {
                let ord = Scenarios.cotrim |> Fixture.processed |> Order.withoutCalculatedConstraints
                let pat = { Patient.patient with Weight = ord |> Fixture.times 1N |> Some }

                match
                    [| Fixture.scenario ord |]
                    |> Fixture.context
                    |> OrderContext.updatePatientData Fixture.logger pat
                with
                | Evaluated after ->
                    after.Patient |> Expect.equal "the patient" pat

                    after.Scenarios[0].Order
                    |> Order.toOrdVars
                    |> List.filter (_.CalculatedConstraints >> OrderVariable.Constraints.isEmpty >> not)
                    |> Expect.isEmpty "no calculated constraints made"
                | Refused _ -> failtest "refused"
            }

            test "a weight in gram reaches the order in the unit of the order" {
                let ord = Scenarios.cotrim |> Fixture.processed

                let grams = ord |> Fixture.times 2N |> ValueUnit.convertTo Units.Weight.gram

                let pat = { Patient.patient with Weight = Some grams }

                OrderContext.adjustOfPatient pat ord
                |> Expect.equal "in kg" (ord |> Fixture.times 2N |> Some)
            }

            test "the same weight leaves every calculated limit as it is" {
                [
                    for name, med in Fixture.fixtures do
                        let ord = med |> Fixture.processed
                        let after = ord |> Fixture.readjusted (ord |> Fixture.times 1N)

                        for before, now in List.zip (ord |> Order.toOrdVars) (after |> Order.toOrdVars) do
                            if before.CalculatedConstraints <> now.CalculatedConstraints then
                                $"%s{name}: %A{before |> OrderVariable.getName}"
                ]
                |> Expect.isEmpty "every fixture"
            }

            test "a change of height moves the per m2 dose and the amount stays" {
                let ord = Fixture.perM2 |> Fixture.processed
                let patient h =
                    { Patient.patient with
                        Weight = Some(30N |> Fixture.kg)
                        Height = Some(h |> Fixture.cm)
                    }
                let update pat ctx = ctx |> OrderContext.updatePatientData Fixture.logger pat

                let order outcome =
                    match outcome with
                    | Evaluated(ctx: OrderContext) -> ctx.Scenarios[0].Order
                    | Refused _ -> failtest "refused"

                let context = [| Fixture.scenario ord |] |> Fixture.context
                let short = context |> update (patient 130N) |> order
                let tall = context |> update (patient 150N) |> order

                short |> Fixture.unitOf |> Expect.equal "per m2" Units.BSA.m2

                short
                |> Fixture.adjustOf
                |> Expect.notEqual "the height moves the body surface" (tall |> Fixture.adjustOf)

                tall
                |> Fixture.givenValues
                |> Expect.equal "the amount stays" (ord |> Fixture.givenValues)

                tall
                |> Fixture.perKg
                |> Expect.equal "240 mg over the body surface" (Some(240N / (tall |> Fixture.adjustOf)))
            }

            test "after a change of weight the dose quantity can be stepped" {
                let ord = Scenarios.cotrim |> Fixture.processed

                let after = ord |> Fixture.readjusted (ord |> Fixture.times 2N)

                after
                |> OrderProcessor.OrderAction.run
                    Fixture.caps
                    Fixture.logger
                    (OrderAction.Set(
                        OrderVariableReference.OrderableDose DoseProperty.Quantity,
                        Choice.Step(1, StepIncrement.Defined)
                    ))
                |> Result.isOk
                |> Expect.isTrue "stepped"
            }
        ]
