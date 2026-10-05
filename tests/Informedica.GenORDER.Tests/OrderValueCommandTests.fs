/// The property commands that set one order variable to its nth value or clear it, over the test
/// scenarios.
module OrderValueCommandTests

open Informedica.Utils.Lib
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip

module Variable = Informedica.GenSolver.Lib.Variable


let solve cmd ord =
    ord
    |> cmd
    |> OrderProcessor.processPipeline OrderLogging.noOp
    |> Result.defaultWith (fun (_, msgs) -> failtest $"not solved: %A{msgs}")


/// The order as the dialog first shows it: calculated, with its values to pick from.
let built (med: Medication) =
    med
    |> Medication.toOrder Scenarios.testStart
    |> Result.get
    |> solve CalcMinMax
    |> solve CalcValues


/// A variable a user picks: its name, how to read it, and the commands that pick its nth value
/// and clear it.
type Field =
    {
        Name: string
        Get: Order -> OrderVariable option
        SetNth: int -> ChangePropertyCommand
        Clear: ChangePropertyCommand
    }


/// Every variable of the order a command picks.
let fields (ord: Order) =
    let dose (d: Dose) = d
    let named n x = x |> Name.toString = n

    let cmp n (o: Order) = o.Orderable.Components |> List.tryFind (_.Name >> named n)

    let itm c i o =
        o |> cmp c |> Option.bind (_.Items >> List.tryFind (_.Name >> named i))

    [
        {
            Name = "frequency"
            Get =
                _.Schedule
                >> Order.Schedule.getFrequency
                >> Option.map OrderVariable.Frequency.toOrdVar
            SetNth = SetNthScheduleFrequency
            Clear = ClearScheduleFrequency
        }
        {
            Name = "time"
            Get = _.Schedule >> Order.Schedule.getTime >> Option.map OrderVariable.Time.toOrdVar
            SetNth = SetNthScheduleTime
            Clear = ClearScheduleTime
        }
        {
            Name = "orderable quantity"
            Get = fun o -> o.Orderable.OrderableQuantity |> OrderVariable.Quantity.toOrdVar |> Some
            SetNth = SetNthOrderableQuantity
            Clear = ClearOrderableQuantity
        }
        {
            Name = "orderable dose quantity"
            Get = fun o -> o.Orderable.Dose.Quantity |> OrderVariable.Quantity.toOrdVar |> Some
            SetNth = SetNthOrderableDoseQuantity
            Clear = ClearOrderableDoseQuantity
        }
        {
            Name = "orderable dose rate"
            Get = fun o -> o.Orderable.Dose.Rate |> OrderVariable.Rate.toOrdVar |> Some
            SetNth = SetNthOrderableDoseRate
            Clear = ClearOrderableDoseRate
        }
        for c in ord.Orderable.Components do
            let c = c.Name |> Name.toString

            {
                Name = $"%s{c} orderable quantity"
                Get = cmp c >> Option.map (_.OrderableQuantity >> OrderVariable.Quantity.toOrdVar)
                SetNth = fun n -> SetNthComponentOrderableQuantity(c, n)
                Clear = ClearComponentOrderableQuantity c
            }
            {
                Name = $"%s{c} dose quantity adjust"
                Get =
                    cmp c
                    >> Option.map (_.Dose.QuantityAdjust >> OrderVariable.QuantityAdjust.toOrdVar)
                SetNth = fun n -> SetNthComponentDoseQuantityAdjust(c, n)
                Clear = ClearComponentDoseQuantityAdjust c
            }

            for i in (ord |> cmp c |> Option.map _.Items |> Option.defaultValue []) do
                let i = i.Name |> Name.toString
                let get f = itm c i >> Option.map f

                {
                    Name = $"%s{c}.%s{i} dose quantity"
                    Get = get (_.Dose.Quantity >> OrderVariable.Quantity.toOrdVar)
                    SetNth = fun n -> SetNthItemDoseQuantity(c, i, n)
                    Clear = ClearItemDoseQuantity(c, i)
                }
                {
                    Name = $"%s{c}.%s{i} dose quantity adjust"
                    Get = get (_.Dose.QuantityAdjust >> OrderVariable.QuantityAdjust.toOrdVar)
                    SetNth = fun n -> SetNthItemDoseQuantityAdjust(c, i, n)
                    Clear = ClearItemDoseQuantityAdjust(c, i)
                }
                {
                    Name = $"%s{c}.%s{i} dose per time"
                    Get = get (_.Dose.PerTime >> OrderVariable.PerTime.toOrdVar)
                    SetNth = fun n -> SetNthItemDosePerTime(c, i, n)
                    Clear = ClearItemDosePerTime(c, i)
                }
                {
                    Name = $"%s{c}.%s{i} dose per time adjust"
                    Get = get (_.Dose.PerTimeAdjust >> OrderVariable.PerTimeAdjust.toOrdVar)
                    SetNth = fun n -> SetNthItemDosePerTimeAdjust(c, i, n)
                    Clear = ClearItemDosePerTimeAdjust(c, i)
                }
                {
                    Name = $"%s{c}.%s{i} dose rate"
                    Get = get (_.Dose.Rate >> OrderVariable.Rate.toOrdVar)
                    SetNth = fun n -> SetNthItemDoseRate(c, i, n)
                    Clear = ClearItemDoseRate(c, i)
                }
                {
                    Name = $"%s{c}.%s{i} dose rate adjust"
                    Get = get (_.Dose.RateAdjust >> OrderVariable.RateAdjust.toOrdVar)
                    SetNth = fun n -> SetNthItemDoseRateAdjust(c, i, n)
                    Clear = ClearItemDoseRateAdjust(c, i)
                }
                {
                    Name = $"%s{c}.%s{i} component concentration"
                    Get = get (_.ComponentConcentration >> OrderVariable.Concentration.toOrdVar)
                    SetNth = fun n -> SetNthItemComponentConcentration(c, i, n)
                    Clear = ClearItemComponentConcentration(c, i)
                }
                {
                    Name = $"%s{c}.%s{i} orderable concentration"
                    Get = get (_.OrderableConcentration >> OrderVariable.Concentration.toOrdVar)
                    SetNth = fun n -> SetNthItemOrderableConcentration(c, i, n)
                    Clear = ClearItemOrderableConcentration(c, i)
                }
                {
                    Name = $"%s{c}.%s{i} orderable quantity"
                    Get = get (_.OrderableQuantity >> OrderVariable.Quantity.toOrdVar)
                    SetNth = fun n -> SetNthItemOrderableQuantity(c, i, n)
                    Clear = ClearItemOrderableQuantity(c, i)
                }
    ]
    |> List.distinctBy _.Name


let fixtures =
    lazy
        [
            "paracetamol rectal", built Scenarios.pcmSupp
            "paracetamol drink", built Scenarios.pcmDrink
            "morfine continuous", built Scenarios.morfCont
            "amfotericine", built Scenarios.amfo
            "cotrimoxazol", built Scenarios.cotrim
            "parenteral nutrition", built Scenarios.tpn
        ]


let change cmd ord =
    ord |> OrderProcessor.processChangeProperty OrderLogging.noOp cmd


/// The variables the change made differ from the order's.
let changed before after =
    List.zip (before |> Order.toOrdVars) (after |> Order.toOrdVars)
    |> List.filter (fun (a, b) -> a <> b)
    |> List.map snd


let values (ovar: OrderVariable) =
    ovar |> OrderVariable.getValSetValueUnit |> Option.map ValueUnit.getValue


[<Tests>]
let tests =
    testList
        "OrderProcessor value commands"
        [
            test "a pick sets its one variable to its nth value, counted from 0" {
                [
                    for name, ord in fixtures.Value do
                        for f in fields ord do
                            match ord |> f.Get with
                            | Some ovar when ovar.Variable |> Variable.count > 1 ->
                                let all = ovar |> values |> Option.defaultValue [||]
                                let n = all.Length - 1
                                let after = ord |> change (f.SetNth n)

                                match changed ord after with
                                | [ v ] when values v = Some [| all[n] |] -> ()
                                | vs -> $"%s{name} %s{f.Name}: %i{vs.Length} changed"
                            | _ -> ()
                ]
                |> Expect.isEmpty "each pick sets its own variable to the last value"
            }

            test "a clear clears its one variable" {
                [
                    for name, ord in fixtures.Value do
                        for f in fields ord do
                            match ord |> f.Get with
                            | Some ovar when ovar.Variable |> Variable.count > 1 ->
                                let after = ord |> change f.Clear

                                match changed ord after with
                                | [ v ] when v |> OrderVariable.isCleared -> ()
                                | vs -> $"%s{name} %s{f.Name}: %i{vs.Length} changed"
                            | _ -> ()
                ]
                |> Expect.isEmpty "each clear clears its own variable"
            }

            test "the fixtures offer variables to pick from" {
                [
                    for _, ord in fixtures.Value do
                        for f in fields ord do
                            match ord |> f.Get with
                            | Some ovar when ovar.Variable |> Variable.count > 1 -> f.Name
                            | _ -> ()
                ]
                |> List.length
                |> fun n -> n > 20
                |> Expect.isTrue "many variables checked"
            }

            test "an index past the values leaves the order as it is" {
                let ord = fixtures.Value[0] |> snd

                ord
                |> change (SetNthScheduleFrequency 99)
                |> changed ord
                |> Expect.isEmpty "nothing changed"
            }

            test "a component the order does not hold leaves the order as it is" {
                let ord = fixtures.Value[0] |> snd

                ord
                |> change (SetNthComponentOrderableQuantity("none", 0))
                |> changed ord
                |> Expect.isEmpty "nothing changed"
            }
        ]
