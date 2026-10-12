// Issue #1425: Order.increaseIncrements with the rate step limited, loaded by Issue1425Load.fsx
// after Order.fs and before OrderProcessor.fs, so the pipeline takes this one.
//
// The rate of a timed order is raised to a rounded step. A step coarser than the window a time
// range leaves for a dose quantity removes that quantity: with a time of 20 to 24 uur a dose of
// 191 mL needs a rate between 7.96 and 9.55 mL/uur, and a step of 5 mL/uur has no value there, so
// the rate times the time narrows the dose quantity to 200 mL. The narrowest window is the one of
// the smallest dose quantity, qmin / tmin - qmin / tmax. Only the steps that fit in it are offered.

namespace Informedica.GenOrder.Lib

// auto-opened, so the pipeline finds this Order module beside the one of Order.fs
[<AutoOpen>]
module Issue1425Shadow =

    module Order =

        open Informedica.Utils.Lib
        open Informedica.Utils.Lib.BCL
        open Informedica.GenUnits.Lib

        module Src = Informedica.GenOrder.Lib.Order
        module ValueRange = Informedica.GenSolver.Lib.Variable.ValueRange
        module Quantity = OrderVariable.Quantity


        /// The narrowest rate window of a timed order, in the unit of the steps: the smallest dose
        /// quantity over the shortest time minus over the longest, or over the shortest time alone
        /// without a longest. Zero for a fixed time. None without a shortest time or a smallest dose
        /// quantity.
        let rateWindow (ord: Order) =
            let minMax ovar =
                let vr = ovar |> OrderVariable.getVar |> _.Values
                vr |> ValueRange.getMin |> Option.map ValueRange.Minimum.toValueUnit,
                vr |> ValueRange.getMax |> Option.map ValueRange.Maximum.toValueUnit

            match ord.Schedule |> Src.Schedule.toOrdVars |> snd with
            | None -> None
            | Some tme ->
                match tme |> minMax, ord.Orderable.Dose.Quantity |> Quantity.toOrdVar |> minMax with
                | (Some tMin, Some tMax), (Some qMin, _) -> Some((qMin / tMin) - (qMin / tMax))
                | (Some tMin, None), (Some qMin, _) -> Some(qMin / tMin)
                | _ -> None


        /// The rate steps that fit in the window, none when none does (a fixed time gives a window of
        /// zero); all steps without a window.
        let rateSteps window steps =
            match window with
            | None -> steps
            | Some w ->
                let fits incr =
                    incr
                    |> ValueRange.Increment.toValueUnit
                    |> ValueUnit.getBaseValue
                    |> Array.forall (fun v -> w |> ValueUnit.getBaseValue |> Array.forall (fun wv -> v <= wv))

                steps |> List.filter fits


        /// Order.increaseIncrements, the rate raised only to a step that fits the rate window.
        let increaseIncrements logger maxQtyCount maxRateCount ord =
            let maxQtyCount = maxQtyCount |> BigRational.fromInt
            let maxRateCount = maxRateCount |> BigRational.fromInt

            if ord.Schedule |> Src.Schedule.isContinuous then
                ord
            else
                let orbQty = ord.Orderable.OrderableQuantity |> Quantity.toOrdVar
                if
                    orbQty.Variable
                    |> Informedica.GenSolver.Lib.Variable.getUnit
                    |> Option.map (ValueUnit.Group.unitToGroup >> (=) Group.VolumeGroup >> not)
                    |> Option.defaultValue false
                then
                    ord
                else
                    let incrOrd =
                        ord
                        |> Src.increaseQuantityIncrement logger maxQtyCount Src.quantityIncrements

                    if ord = incrOrd then
                        Ok ord
                    else
                        incrOrd |> Src.solveMinMax "Increase Quantity Increment" false logger
                    |> function
                        | Error _ -> ord
                        | Ok ord ->
                            let ord =
                                let doseOrd =
                                    ord
                                    |> Src.increaseDoseQuantityIncrement maxQtyCount Src.quantityIncrements

                                if doseOrd = ord then
                                    ord
                                else
                                    doseOrd
                                    |> Src.solveMinMax "Increase Dose Quantity Increment" false logger
                                    |> Result.defaultValue ord

                            let steps =
                                Src.rateIncrements
                                |> rateSteps (ord |> rateWindow)

                            // no step fits: the rate keeps its increment
                            let incrOrd =
                                if steps |> List.isEmpty then
                                    ord
                                else
                                    ord |> Src.increaseRateIncrement maxRateCount steps

                            if incrOrd = ord then
                                Ok ord
                            else
                                incrOrd |> Src.solveMinMax "Increase Rate Increment" false logger
                            |> function
                                | Error _ -> ord
                                | Ok ord -> ord
            |> Ok
