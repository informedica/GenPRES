namespace Informedica.GenOrder.Lib


module Nutrition =

    open Informedica.GenForm.Lib

    let tpnConstraints =
        [
            OrderAdjust OrderVariable.Quantity.applyConstraints

            ScheduleFrequency OrderVariable.Frequency.applyConstraints
            ScheduleTime OrderVariable.Time.applyConstraints

            OrderableQuantity OrderVariable.Quantity.applyConstraints
            OrderableDoseCount OrderVariable.Count.applyConstraints
            OrderableDose Order.Orderable.Dose.applyConstraints

            ComponentOrderableQuantity("", OrderVariable.Quantity.applyConstraints)

            ItemComponentConcentration("", "", OrderVariable.Concentration.applyConstraints)
            ItemOrderableConcentration("", "", OrderVariable.Concentration.applyConstraints)
        ]


    let applyPropChange logger propChanges ord =
        let ord = ord |> Order.OrderPropertyChange.proc propChanges

        ord
        |> Order.solveMinMax "Apply Prop Change" true logger
        |> function
            | Ok ord -> ord
            | _ ->
                $"=== ERROR applying {propChanges} ==="
                |> Events.OrderScenario
                |> Informedica.GenOrder.Lib.Logging.logWarning logger

                ord


    let proc (start: System.DateTime) logger changes tpn =
        tpn
        |> Medication.toOrder start
        |> Result.map (fun ord ->
            ord
            |> Order.OrderPropertyChange.proc tpnConstraints
            |> Order.solveMinMax "Process Nutrition" true logger
            |> function
                | Ok ord ->
                    let changes =
                        changes
                        |> List.map (fun (cmp, perc) ->
                            ComponentOrderableQuantity(cmp, OrderVariable.Quantity.setPercValue perc)
                        )

                    ord |> applyPropChange logger changes
                | Error(ord, _) -> ord
        )
