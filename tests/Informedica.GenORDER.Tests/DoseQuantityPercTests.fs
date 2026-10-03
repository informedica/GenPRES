/// The dose quantity of a TPN order set at a percentage of the composition the user set: the
/// pipeline the server runs for SetOrderableDoseQuantityPerc.
module DoseQuantityPercTests

open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenCore.Lib.Ranges
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip


/// The TPN order the percentage is tested on, and the commands that move it.
module Fixture =

    // a pipeline error returns the order as far as it got, so it is a failure, not an order
    let run cmd ord =
        ord
        |> cmd
        |> OrderProcessor.processPipeline OrderLogging.noOp
        |> function
            | Ok ord -> ord
            | Error(_, msgs) ->
                let msg = $"%A{msgs}"
                invalidOp msg[..300]


    let perc p ord =
        ord |> run (fun o -> ChangeProperty(o, SetOrderableDoseQuantityPerc p))


    // The per-kg limits of a component belong to its orderable quantity, the composition, not to its
    // dose: a part of the orderable may be given. So each component dose limit becomes a solution limit
    // on the component, made absolute by the weight as Medication.create does for a solution rule.
    let withComponentSolutionLimits (med: Medication) =
        let toSolution (pc: ProductComponent) =
            match pc.Dose, med.Adjust with
            | Some dl, Some weight ->
                { pc with
                    Dose = None
                    Solution =
                        { SolutionLimit.limit with
                            SolutionLimitTarget = pc.Name |> ComponentLimitTarget
                            Quantity = dl.QuantityAdjust |> MinMax.apply ((*) weight)
                        }
                        |> Some
                }
            | _ -> pc

        { med with Components = med.Components |> List.map toSolution }


    // the orderable dose limited by nothing per kg: the composition and the time decide the dose
    let withoutOrderableDoseQuantityAdjust (med: Medication) =
        { med with Dose = med.Dose |> Option.map (fun dl -> { dl with QuantityAdjust = MinMax.empty }) }


    // gluc 10% fills the TPN up, between 65 and 80 mL/kg, made absolute by the weight
    let withGlucoseSolutionLimit (med: Medication) =
        let mlPerKg n =
            n
            |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> ValueUnit.per Units.Weight.kiloGram)
            |> Limit.inclusive
            |> Some

        let limit (pc: ProductComponent) =
            match med.Adjust with
            | Some weight when pc.Name = "gluc 10%" ->
                { pc with
                    Solution =
                        { SolutionLimit.limit with
                            SolutionLimitTarget = pc.Name |> ComponentLimitTarget
                            Quantity =
                                { MinMax.empty with
                                    Min = mlPerKg 65N
                                    Max = mlPerKg 80N
                                }
                                |> MinMax.apply ((*) weight)
                        }
                        |> Some
                }
            | _ -> pc

        { med with Components = med.Components |> List.map limit }


    // a dose rule with a dose maximum of 1100 mL and a rate maximum of 45 mL/uur; while the user
    // composes, the dose is the whole orderable, so a lower dose maximum would cap the composition
    let withDoseMax (med: Medication) =
        let max u n =
            { MinMax.empty with Max = n |> ValueUnit.singleWithUnit u |> Limit.inclusive |> Some }

        let mL = Units.Volume.milliLiter

        { med with
            Dose =
                med.Dose
                |> Option.map (fun dl ->
                    { dl with
                        Quantity = 1100N |> max mL
                        Rate = 45N |> max (mL |> ValueUnit.per Units.Time.hour)
                    }
                )
        }


    // the dose count defined as at least one, not exactly one, so a dose can be a part of the
    // orderable
    let withDoseCountMinOne (med: Medication) =
        { med with
            DoseCount =
                { MinMax.empty with Min = 1N |> ValueUnit.singleWithUnit Units.Count.times |> Limit.inclusive |> Some }
        }


    let tpn =
        Scenarios.tpnComplete
        |> withDoseCountMinOne
        |> withComponentSolutionLimits
        |> withGlucoseSolutionLimit
        |> withoutOrderableDoseQuantityAdjust


    // the order as the server selects it
    let selected =
        lazy
            (tpn
             |> Medication.toOrder Scenarios.testStart
             |> Result.get
             |> run CalcMinMax
             |> run CalcValues)


    // every component orderable quantity solved to one value, here its median
    let compose (ord: Order) =
        let setMedian ord (cmp: Component) =
            ord
            |> run (fun o -> ChangeProperty(o, SetMedianComponentOrderableQuantity(cmp.Name |> Name.toString)))

        ord.Orderable.Components |> List.fold setMedian ord


    // the TPN as the user composes it
    let composed = lazy (selected.Value |> compose)


    // the TPN with a dose and a rate maximum, composed as above
    let capped =
        lazy
            (tpn
             |> withDoseMax
             |> Medication.toOrder Scenarios.testStart
             |> Result.get
             |> run CalcMinMax
             |> run CalcValues
             |> compose)


    // the composed TPN solved to one dose quantity by a first move
    let once = lazy (composed.Value |> perc 100)


    // the user picks the median rate after a first move, so the time follows from the dose and the rate
    let rated =
        lazy
            (composed.Value
             |> perc 50
             |> run (fun o -> ChangeProperty(o, SetMedianOrderableDoseRate)))


open Fixture


let doseQty (ord: Order) = ord.Orderable.Dose.Quantity |> OrderVariable.Quantity.toOrdVar


let private valueOf (var: Variable) =
    var
    |> Variable.getValueRange
    |> Variable.ValueRange.getValSet
    |> Option.map (Variable.ValueRange.ValueSet.toValueUnit >> ValueUnit.getValue)


// the value of the dose quantity, when it holds one
let value ord = (ord |> doseQty).Variable |> valueOf


// the value of the orderable quantity, when it holds one
let orderableValue (ord: Order) =
    (ord.Orderable.OrderableQuantity |> OrderVariable.Quantity.toOrdVar).Variable
    |> valueOf


// the value of the time of the schedule, when it holds one
let timeValue (ord: Order) =
    ord.Schedule
    |> Order.Schedule.getTime
    |> Option.bind (fun tme -> (tme |> OrderVariable.Time.toOrdVar).Variable |> valueOf)


// the max of the time of the schedule, when it has one
let timeMax (ord: Order) =
    ord.Schedule
    |> Order.Schedule.getTime
    |> Option.bind (fun tme ->
        (tme |> OrderVariable.Time.toOrdVar).Variable
        |> Variable.getValueRange
        |> Variable.ValueRange.getMax
        |> Option.map (Variable.ValueRange.Maximum.toValueUnit >> ValueUnit.getValue)
    )


// the value of the orderable dose rate, when it holds one
let rateValue (ord: Order) =
    (ord.Orderable.Dose.Rate |> OrderVariable.Rate.toOrdVar).Variable |> valueOf


let percentages = [ 10..10..100 ]


[<Tests>]
let tests =
    testList
        "SetOrderableDoseQuantityPerc on a TPN order"
        [
            test "the TPN order is timed, so the dose quantity is set and not the rate" {
                selected.Value.Schedule.IsTimed |> Expect.isTrue "should be timed"
            }

            test "the composed fixture is composed, the selected order is not" {
                (composed.Value |> OrderProcessor.isComposed, selected.Value |> OrderProcessor.isComposed)
                |> Expect.equal "only the composed order should have every component set" (true, false)
            }

            for p in percentages do
                test $"a move to %i{p}%% before every component is set leaves the order unchanged" {
                    let doseQuantities (ord: Order) =
                        let cmpQuantities =
                            ord.Orderable.Components
                            |> List.map (_.OrderableQuantity >> OrderVariable.Quantity.toOrdVar)

                        ord |> doseQty, cmpQuantities

                    selected.Value
                    |> OrderProcessor.processChangeProperty OrderLogging.noOp (SetOrderableDoseQuantityPerc p)
                    |> doseQuantities
                    |> Expect.equal
                        "the dose and component quantities should not change"
                        (selected.Value |> doseQuantities)
                }

            test "the fixture has no Div, so the products give the step of the dose quantity" {
                (tpn.Div, (selected.Value |> doseQty).DefinedConstraints.Incr |> Option.isSome)
                |> Expect.equal "Div should be empty and the dose quantity should have a defined increment" (None, true)
            }

            test "the fixture limits the component orderable quantities, not the component doses" {
                tpn.Components
                |> List.forall (fun pc -> pc.Dose.IsNone && pc.Solution.IsSome)
                |> Expect.isTrue "every component should have a solution limit and no dose limit"
            }

            test "the fixture is within its constraints" {
                composed.Value
                |> Order.checkConstraints false
                |> Expect.isEmpty "no order variable should be outside its constraints"
            }

            for p in percentages do
                test $"a move to %i{p}%% keeps the order within its constraints" {
                    composed.Value
                    |> perc p
                    |> Order.checkConstraints false
                    |> List.map (OrderVariable.toString false)
                    |> Expect.isEmpty "no order variable should be outside its constraints"
                }

            for name, start in [ "a first move", composed; "a move after 100%", once ] do
                for p in percentages do
                    test $"%s{name} to %i{p}%% sets one dose quantity" {
                        (start.Value |> perc p |> doseQty).Variable
                        |> Variable.count
                        |> Expect.equal "should be one value" 1
                    }

                test $"%s{name}: a higher percentage gives a higher dose quantity" {
                    percentages
                    |> List.map (fun p -> start.Value |> perc p |> value)
                    |> List.pairwise
                    |> List.forall (fun (lower, higher) -> lower < higher)
                    |> Expect.isTrue "the dose quantities should rise with the percentage"
                }

            test "a move after 100% gives the dose quantity of a first move" {
                let firstMoves = percentages |> List.map (fun p -> composed.Value |> perc p |> value)

                percentages
                |> List.map (fun p -> once.Value |> perc p |> value)
                |> Expect.equal "should be the same" firstMoves
            }

            test "100% gives the whole orderable quantity" {
                composed.Value
                |> perc 100
                |> value
                |> Expect.equal "should be the orderable quantity" (composed.Value |> orderableValue)
            }

            test "the composition stays as the user set it" {
                let quantities (ord: Order) =
                    ord.Orderable.Components
                    |> List.map (_.OrderableQuantity >> OrderVariable.Quantity.toOrdVar >> _.Variable)

                composed.Value
                |> perc 50
                |> quantities
                |> Expect.equal "the component orderable quantities should not change" (composed.Value |> quantities)
            }

            test "the rate fixture has one rate and one time" {
                match rated.Value |> rateValue, rated.Value |> timeValue with
                | Some _, Some _ -> ()
                | r, t -> failtest $"rate %A{r} and time %A{t} should each hold one value"
            }

            for p in percentages do
                test $"a move to %i{p}%% after the user set the rate keeps the time at or below that time" {
                    rated.Value
                    |> perc p
                    |> timeMax
                    |> Option.map (fun tme -> tme <= (rated.Value |> timeValue |> Option.get))
                    |> Expect.equal "the time should not exceed the time the rate gave" (Some true)
                }

                test $"a move to %i{p}%% after the user set the rate keeps the order within its constraints" {
                    rated.Value
                    |> perc p
                    |> Order.checkConstraints false
                    |> List.map (OrderVariable.toString false)
                    |> Expect.isEmpty "no order variable should be outside its constraints"
                }

            for p in percentages do
                test $"a move to %i{p}%% keeps the dose and rate maximum of the rule" {
                    capped.Value
                    |> perc p
                    |> Order.checkConstraints false
                    |> List.map (OrderVariable.toString false)
                    |> Expect.isEmpty "no order variable should be outside its constraints"
                }

            test "a move after the user set the rate gives the dose quantity of a first move" {
                let firstMoves = percentages |> List.map (fun p -> composed.Value |> perc p |> value)

                percentages
                |> List.map (fun p -> rated.Value |> perc p |> value)
                |> Expect.equal "should be the same" firstMoves
            }
        ]
