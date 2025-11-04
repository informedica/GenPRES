namespace Informedica.GenOrder.Lib


module Nutrition =

    open MathNet.Numerics
    open Informedica.Utils.Lib.BCL
    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenForm.Lib
    open Informedica.GenUnits.Lib


    let tpnConstraints =
        [
            OrderAdjust OrderVariable.Quantity.applyConstraints

            ScheduleFrequency OrderVariable.Frequency.applyConstraints
            ScheduleTime OrderVariable.Time.applyConstraints

            OrderableQuantity OrderVariable.Quantity.applyConstraints
            OrderableDoseCount OrderVariable.Count.applyConstraints
            OrderableDose Order.Orderable.Dose.applyConstraints

            ComponentOrderableQuantity ("", OrderVariable.Quantity.applyConstraints)

            ItemComponentConcentration ("", "", OrderVariable.Concentration.applyConstraints)
            ItemOrderableConcentration ("", "", OrderVariable.Concentration.applyConstraints)
        ]


    let applyPropChange propChanges ord =
        let ord =
            ord
            |> Order.OrderPropertyChange.proc propChanges
        ord
        |> Order.solveMinMax true Logging.noOp
        |> function
            | Ok ord -> ord
            | _ ->
                printfn $"=== ERROR applying {propChanges} ==="
                ord


    let proc logger changes tpn =
        tpn
        |> Medication.toOrderDto
        |> Order.Dto.fromDto
        |> Result.map (fun ord ->
            ord
            |> Order.OrderPropertyChange.proc tpnConstraints
            |> Order.solveMinMax true Logging.noOp //logger
            |> function 
            | Ok ord -> 
                let changes = changes |> List.map (fun (cmp, perc) -> ComponentOrderableQuantity (cmp, OrderVariable.Quantity.setPercValue perc))
                ord |> applyPropChange changes
            | Error (ord, _) -> ord
        )
