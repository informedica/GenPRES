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


    let applyPropChange propChanges ord =
        let ord = ord |> Order.OrderPropertyChange.proc propChanges

        ord
        |> Order.solveMinMax "Apply Prop Change" true Logging.noOp
        |> function
            | Ok ord -> ord
            | _ ->
                printfn $"=== ERROR applying {propChanges} ==="
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

                    ord |> applyPropChange changes
                | Error(ord, _) -> ord
        )
