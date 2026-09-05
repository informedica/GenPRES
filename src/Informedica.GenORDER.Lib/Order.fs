namespace Informedica.GenOrder.Lib


/// <summary>
/// Types and functions that deal with an order.
/// An `Order` models the `Prescription` of an
/// `Orderable` with a `StartStop` start date and
/// stop date.
/// </summary>
//[<RequireQualifiedAccess>]
module Order =


    open System
    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib
    open ConsoleWriter.NewLineNoTime
    open Informedica.GenUnits.Lib
    open WrappedString


    module Mapping = EquationMapping
    module Increment = Informedica.GenSolver.Lib.Variable.ValueRange.Increment

    /// Types and functions to deal
    /// with an `Orderable`, i.e., something
    /// that can be ordered.
    [<RequireQualifiedAccess>]
    module Orderable =


        open Informedica.GenSolver.Lib


        type Name = Types.Name


        module Dose =

            module Quantity = OrderVariable.Quantity
            module PerTime = OrderVariable.PerTime
            module Rate = OrderVariable.Rate
            module Total = OrderVariable.Total
            module QuantityAdjust = OrderVariable.QuantityAdjust
            module PerTimeAdjust = OrderVariable.PerTimeAdjust
            module RateAdjust = OrderVariable.RateAdjust
            module TotalAdjust = OrderVariable.TotalAdjust
            module Literals = EquationMapping.Literals

            /// Apply **f** to a `Dose`
            let apply f (dos: Dose) = f dos


            /// Utility method to facilitate type inference
            let inf = apply id


            /// <summary>
            /// Create a `Dose` with
            /// </summary>
            /// <param name="qty">The quantity of the dose</param>
            /// <param name="ptm">The per time of the dose</param>
            /// <param name="rte">The rate of the dose</param>
            /// <param name="tot">The total of the dose</param>
            /// <param name="qty_adj">The quantity adjust of the dose</param>
            /// <param name="ptm_adj">The per time adjust of the dose</param>
            /// <param name="rte_adj">The rate adjust of the dose</param>
            /// <param name="tot_adj">The total adjust of the dose</param>
            let create qty ptm rte tot qty_adj ptm_adj rte_adj tot_adj =
                {
                    Quantity = qty
                    PerTime = ptm
                    Rate = rte
                    Total = tot
                    QuantityAdjust = qty_adj
                    PerTimeAdjust = ptm_adj
                    RateAdjust = rte_adj
                    TotalAdjust = tot_adj
                }


            /// <summary>
            /// Create a new `Dose` with
            /// </summary>
            /// <param name="n">The name of the dose</param>
            let createNew n =
                let un = Unit.NoUnit
                let n = n |> Name.add Literals.dos

                let qty = Quantity.create n un
                let ptm = PerTime.create n un un
                let rte = Rate.create n un un
                let tot = Total.create n un
                let qty_adj = QuantityAdjust.create n un un
                let rte_adj = RateAdjust.create n un un un
                let ptm_adj = PerTimeAdjust.create n un un un
                let tot_adj = TotalAdjust.create n un un

                create qty ptm rte tot qty_adj ptm_adj rte_adj tot_adj


            /// <summary>
            /// Return a Dose as a list of OrderVariables
            /// </summary>
            /// <param name="dos">The dose</param>
            let toOrdVars dos =
                let qty = (dos |> inf).Quantity |> Quantity.toOrdVar
                let ptm = dos.PerTime |> PerTime.toOrdVar
                let rte = dos.Rate |> Rate.toOrdVar
                let tot = dos.Total |> Total.toOrdVar
                let qty_adj = dos.QuantityAdjust |> QuantityAdjust.toOrdVar
                let ptm_adj = dos.PerTimeAdjust |> PerTimeAdjust.toOrdVar
                let rte_adj = dos.RateAdjust |> RateAdjust.toOrdVar
                let tot_adj = dos.TotalAdjust |> TotalAdjust.toOrdVar

                [ qty; ptm; rte; tot; qty_adj; ptm_adj; rte_adj; tot_adj ]


            /// <summary>
            /// Create a new Dose from a list of OrderVariables using
            /// an old Dose.
            /// </summary>
            /// <param name="ovars">The list of OrderVariables</param>
            /// <param name="dos">The old Dose</param>
            let fromOrdVars ovars dos =
                let qty = (dos |> inf).Quantity |> Quantity.fromOrdVar ovars
                let ptm = dos.PerTime |> PerTime.fromOrdVar ovars
                let rte = dos.Rate |> Rate.fromOrdVar ovars
                let tot = dos.Total |> Total.fromOrdVar ovars
                let qty_adj = dos.QuantityAdjust |> QuantityAdjust.fromOrdVar ovars
                let ptm_adj = dos.PerTimeAdjust |> PerTimeAdjust.fromOrdVar ovars
                let rte_adj = dos.RateAdjust |> RateAdjust.fromOrdVar ovars
                let tot_adj = dos.TotalAdjust |> TotalAdjust.fromOrdVar ovars

                create qty ptm rte tot qty_adj ptm_adj rte_adj tot_adj


            /// <summary>
            /// Apply constraints to a Dose
            /// </summary>
            /// <param name="dos">The Dose</param>
            let applyConstraints dos =
                let qty = (dos |> inf).Quantity |> Quantity.applyConstraints
                let ptm = dos.PerTime |> PerTime.applyConstraints
                let rte = dos.Rate |> Rate.applyConstraints
                let tot = dos.Total |> Total.applyConstraints
                let qty_adj = dos.QuantityAdjust |> QuantityAdjust.applyConstraints
                let ptm_adj = dos.PerTimeAdjust |> PerTimeAdjust.applyConstraints
                let rte_adj = dos.RateAdjust |> RateAdjust.applyConstraints
                let tot_adj = dos.TotalAdjust |> TotalAdjust.applyConstraints

                create qty ptm rte tot qty_adj ptm_adj rte_adj tot_adj


            /// <summary>
            /// Apply constraints to a Dose
            /// </summary>
            /// <param name="dos">The Dose</param>
            let applyOnlyMaxConstraints dos =
                let qty = (dos |> inf).Quantity |> Quantity.applyOnlyMaxConstraints
                let ptm = dos.PerTime |> PerTime.applyOnlyMaxConstraints
                let rte = dos.Rate |> Rate.applyOnlyMaxConstraints
                let tot = dos.Total |> Total.applyOnlyMaxConstraints
                let qty_adj = dos.QuantityAdjust |> QuantityAdjust.applyOnlyMaxConstraints
                let ptm_adj = dos.PerTimeAdjust |> PerTimeAdjust.applyOnlyMaxConstraints
                let rte_adj = dos.RateAdjust |> RateAdjust.applyOnlyMaxConstraints
                let tot_adj = dos.TotalAdjust |> TotalAdjust.applyOnlyMaxConstraints

                create qty ptm rte tot qty_adj ptm_adj rte_adj tot_adj


            let isWithinConstraints useCalc dos =
                dos |> toOrdVars |> List.forall (OrderVariable.isWithinConstraints useCalc)


            /// <summary>
            /// Apply constraints to a Dose
            /// </summary>
            /// <param name="dos">The Dose</param>
            let setToNonZeroPositive dos =
                let qty = (dos |> inf).Quantity |> Quantity.setToNonZeroPositive
                let ptm = dos.PerTime |> PerTime.setToNonZeroPositive
                let rte = dos.Rate |> Rate.setToNonZeroPositive
                let tot = dos.Total |> Total.setToNonZeroPositive
                let qty_adj = dos.QuantityAdjust |> QuantityAdjust.setToNonZeroPositive
                let ptm_adj = dos.PerTimeAdjust |> PerTimeAdjust.setToNonZeroPositive
                let rte_adj = dos.RateAdjust |> RateAdjust.setToNonZeroPositive
                let tot_adj = dos.TotalAdjust |> TotalAdjust.setToNonZeroPositive

                create qty ptm rte tot qty_adj ptm_adj rte_adj tot_adj


            /// <summary>
            /// Apply only max quantity constraints to a Dose
            /// </summary>
            let applyQuantityMaxConstraints dos =
                { (dos |> inf) with
                    Quantity = dos.Quantity |> Quantity.applyOnlyMaxConstraints
                    QuantityAdjust = dos.QuantityAdjust |> QuantityAdjust.applyOnlyMaxConstraints
                }


            /// <summary>
            /// Apply only min incr quantity constraints to a Dose
            /// </summary>
            let applyQuantityMinIncrConstraints dos =
                { (dos |> inf) with
                    Quantity = dos.Quantity |> Quantity.applyOnlyMinIncrConstraints
                    QuantityAdjust = dos.QuantityAdjust |> QuantityAdjust.setToNonZeroPositive
                }


            /// <summary>
            /// Apply quantity constraints to a Dose
            /// </summary>
            let applyQuantityConstraints dos =
                { (dos |> inf) with
                    Quantity = dos.Quantity |> Quantity.applyConstraints
                    QuantityAdjust = dos.QuantityAdjust |> QuantityAdjust.applyConstraints
                }


            let isQuantityWithinConstraints useCalc dos =
                (dos |> inf).Quantity |> Quantity.isWithinConstraints useCalc
                && dos.QuantityAdjust |> QuantityAdjust.isWithinConstraints useCalc


            /// <summary>
            /// Apply only quantity adjust constraints to a Dose
            /// </summary>
            let applyQuantityAdjustConstraints dos =
                { (dos |> inf) with QuantityAdjust = dos.QuantityAdjust |> QuantityAdjust.applyConstraints }


            /// <summary>
            /// Apply per time constraints to a Dose
            /// </summary>
            let applyPerTimeConstraints dos =
                { (dos |> inf) with
                    PerTime = dos.PerTime |> PerTime.applyConstraints
                    PerTimeAdjust = dos.PerTimeAdjust |> PerTimeAdjust.applyConstraints
                }


            let isPerTimeWithinConstraints useCalc dos =
                (dos |> inf).PerTime |> PerTime.isWithinConstraints useCalc
                && dos.PerTimeAdjust |> PerTimeAdjust.isWithinConstraints useCalc


            /// <summary>
            /// Set the rate constraints for a Dose
            /// </summary>
            /// <param name="cons">The constraints</param>
            /// <param name="dos">The Dose</param>
            /// <returns>The Dose with the rate constraints set</returns>
            let setRateConstraints cons dos =
                { (dos |> inf) with Rate = dos.Rate |> Rate.setConstraints cons }


            let isRateWithinConstraints useCalc dos =
                (dos |> inf).Rate |> Rate.isWithinConstraints useCalc
                && dos.RateAdjust |> RateAdjust.isWithinConstraints useCalc


            /// <summary>
            /// Set standard rate constraints for a Dose
            /// </summary>
            /// <param name="dos">The Dose</param>
            /// <returns>The Dose with the standard rate constraints set</returns>
            let setStandardRateConstraints dos =
                let rates =
                    [| 1N / 10N .. 1N / 10N .. 1_000N |]
                    |> ValueUnit.withUnit (Units.Volume.milliLiter |> ValueUnit.per Units.Time.hour)
                    |> Variable.ValueRange.ValueSet.create
                    |> Some
                    |> OrderVariable.Constraints.create None None None

                dos |> setRateConstraints rates


            /// <summary>
            /// Apply rate constraints to a Dose
            /// </summary>
            let applyRateConstraints dos =
                { (dos |> inf) with
                    Rate = dos.Rate |> Rate.applyConstraints
                    RateAdjust = dos.RateAdjust |> RateAdjust.applyConstraints
                }


            /// <summary>
            /// Increase the increment of a Dose to a maximum
            /// count using a list of increments.
            /// </summary>
            /// <param name="maxCount">The maximum count</param>
            /// <param name="incrs">The list of increments</param>
            /// <param name="dos">The Dose</param>
            let increaseRateIncrement maxCount incrs dos =
                { (dos |> inf) with Rate = dos.Rate |> Rate.increaseIncrement maxCount incrs }


            /// <summary>
            /// Set min, max or median dose value
            /// </summary>
            /// <param name="set">The function to set the value</param>
            /// <param name="sch">The prescription schedulee</param>
            /// <param name="setRate">If timed set rate or pertime</param>
            /// <param name="dos">The Dose</param>
            /// <returns>The Dose with the value set</returns>
            let setDose set sch setRate dos =
                match sch with
                | Once -> { (dos |> inf) with Quantity = dos.Quantity |> Quantity.toOrdVar |> set |> Quantity }
                | OnceTimed _ ->
                    if setRate then
                        { dos with Rate = dos.Rate |> Rate.toOrdVar |> set |> Rate }
                    else
                        { (dos |> inf) with Quantity = dos.Quantity |> Quantity.toOrdVar |> set |> Quantity }
                | Discontinuous _ -> { dos with Quantity = dos.Quantity |> Quantity.toOrdVar |> set |> Quantity }

                | Timed _ ->
                    if setRate then
                        { dos with Rate = dos.Rate |> Rate.toOrdVar |> set |> Rate }
                    else
                        { dos with Quantity = dos.Quantity |> Quantity.toOrdVar |> set |> Quantity }
                | Continuous _ -> { dos with Rate = dos.Rate |> Rate.toOrdVar |> set |> Rate }


            /// Set the min dose value
            let setMinDose = setDose OrderVariable.setMinValue


            /// Set the max dose value
            let setMaxDose = setDose OrderVariable.setMaxValue


            /// Set the median dose value
            let setMedianDose = setDose OrderVariable.setMedianValue


            let setNthValue nth = setDose (OrderVariable.setNthValue nth)


            let setPercValue perc =
                setDose (OrderVariable.setPercValue perc)


            /// <summary>
            /// Check if all variables in a Dose are solved
            /// </summary>
            /// <returns>True if all variables are solved, false otherwise</returns>
            let isSolved = toOrdVars >> List.forall OrderVariable.isSolved


            /// Apply a function to Quantity of a Dose
            let applyToQuantity f dos =
                { (dos |> inf) with Quantity = f dos.Quantity }


            /// Apply a function to PerTime of a Dose
            let applyToPerTime f dos =
                { (dos |> inf) with PerTime = f dos.PerTime }


            /// Apply a function to Rate of a Dose
            let applyToRate f dos = { (dos |> inf) with Rate = f dos.Rate }


            /// Apply a function to Total of a Dose
            let applyToTotal f dos =
                { (dos |> inf) with Total = f dos.Total }


            /// Apply a function to QuantityAdjust of a Dose
            let applyToQuantityAdjust f dos =
                { (dos |> inf) with QuantityAdjust = f dos.QuantityAdjust }


            /// Apply a function to PerTimeAdjust of a Dose
            let applyToPerTimeAdjust f dos =
                { (dos |> inf) with PerTimeAdjust = f dos.PerTimeAdjust }


            /// Apply a function to RateAdjust of a Dose
            let applyToRateAdjust f dos =
                { (dos |> inf) with RateAdjust = f dos.RateAdjust }


            /// Check if the rate or rateAdjust of a Dose is cleared
            let isRateCleared dos =
                (dos |> inf).Rate |> Rate.isCleared || dos.RateAdjust |> RateAdjust.isCleared


            /// Clear the rate of a Dose (not the rateAdjust)
            let clearRate = applyToRate Rate.clear


            let stepRate step useCalc n dos =
                let n =
                    if not useCalc then
                        n
                    else
                        let mult =
                            match dos.Rate |> Rate.toOrdVar |> _.CalculatedConstraints |> _.Incr with
                            | Some incr ->
                                let vu = incr |> Increment.toValueUnit

                                let dr =
                                    Units.Volume.milliLiter
                                    |> ValueUnit.per Units.Time.hour
                                    |> ValueUnit.singleWithValue (1N / 10N)

                                if vu = dr then 10 else 1
                            | None -> 1

                        n * mult

                { (dos |> inf) with Rate = dos.Rate |> step useCalc n }

            /// Decrease the rate of a Dose
            let decreaseRate = stepRate Rate.decrease


            /// Increase the rate of a Dose
            let increaseRate = stepRate Rate.increase


            /// Clear both the rate and rateAdjust of a Dose
            /// by setting them to non-zero positive
            let setRateToNonZeroPositive dos =
                { (dos |> inf) with
                    Rate = dos.Rate |> Rate.setToNonZeroPositive
                    RateAdjust = dos.RateAdjust |> RateAdjust.setToNonZeroPositive
                }


            /// Clear only rateAdjust of a Dose
            /// by setting them to non-zero positive
            let setRateAdjustToNonZeroPositive dos =
                { (dos |> inf) with RateAdjust = dos.RateAdjust |> RateAdjust.setToNonZeroPositive }


            /// Convert min, incr, max to values for the rate of a Dose
            /// by creating a ValueSet from min, incr, max
            let rateMinIncrMaxToValues dos =
                { (dos |> inf) with Rate = dos.Rate |> Rate.minIncrMaxToValues }


            /// Check if the quantity or quantityAdjust of a Dose is cleared
            let isQuantityCleared dos =
                (dos |> inf).Quantity |> Quantity.isCleared
                || dos.QuantityAdjust |> QuantityAdjust.isCleared


            /// Check if the quantity or quantityAdjust of a Dose has a max constraint
            let hasQuantityMaxConstraint dos =
                (dos |> inf).Quantity |> Quantity.hasMaxConstraint
                || dos.QuantityAdjust |> QuantityAdjust.hasMaxConstraint


            /// Clear the quantity of a Dose (not the quantityAdjust)
            let clearQuantity dos =
                { (dos |> inf) with Quantity = dos.Quantity |> Quantity.clear }


            /// Decrease the quantity of a Dose
            let decreaseQuantity useCalc n dos =
                { (dos |> inf) with Quantity = dos.Quantity |> Quantity.decrease useCalc n }


            /// Increase the quantity of a Dose
            let increaseQuantity useCalc n dos =
                { (dos |> inf) with Quantity = dos.Quantity |> Quantity.increase useCalc n }


            /// Clear both the quantity and quantityAdjust of a Dose
            /// by setting them to non-zero positive
            let setQuantityToNonZeroPositive dos =
                { (dos |> inf) with
                    Quantity = dos.Quantity |> Quantity.setToNonZeroPositive
                    QuantityAdjust = dos.QuantityAdjust |> QuantityAdjust.setToNonZeroPositive
                }


            /// Clear only quantityAdjust of a Dose
            /// by setting them to non-zero positive
            let setQuantityAdjustToNonZeroPositive dos =
                { (dos |> inf) with QuantityAdjust = dos.QuantityAdjust |> QuantityAdjust.setToNonZeroPositive }


            /// Convert min, incr, max to values for the quantity of a Dose
            /// by creating a ValueSet from min, incr, max
            let quantityMinIncrMaxToValues dos =
                { (dos |> inf) with Quantity = dos.Quantity |> Quantity.minIncrMaxToValues }


            /// Check if the per time or per time adjust of a Dose is cleared
            let isPerTimeCleared dos =
                (dos |> inf).PerTime |> PerTime.isCleared
                || dos.PerTimeAdjust |> PerTimeAdjust.isCleared


            /// Clear the per time of a Dose (not the per time adjust)
            let clearPerTime dos = (dos |> inf).PerTime |> PerTime.clear


            /// Clear both the per time and per time adjust of a Dose
            /// by setting them to non-zero positive
            let setPerTimeToNonZeroPositive dos =
                { (dos |> inf) with
                    PerTime = dos.PerTime |> PerTime.setToNonZeroPositive
                    PerTimeAdjust = dos.PerTimeAdjust |> PerTimeAdjust.setToNonZeroPositive
                }


            /// Convert min, incr, max to values for the per time of a Dose
            /// by creating a ValueSet from min, incr, max
            let perTimeMinIncrMaxToValues dos =
                { (dos |> inf) with PerTime = dos.PerTime |> PerTime.minIncrMaxToValues }


            /// <Summary>
            /// Set the dose unit for all variables in a Dose
            /// This is the first unit of the full dose unit
            /// e.g., for mg/kg/day this is mg
            /// </Summary>
            /// <param name="du">The dose unit</param>
            /// <param name="dos">The Dose</param>
            /// <returns>The Dose with the dose unit set</returns>
            let setDoseUnit du dos =
                let qty = (dos |> inf).Quantity |> Quantity.convertFirstUnit du
                let ptm = dos.PerTime |> PerTime.convertFirstUnit du
                let rte = dos.Rate |> Rate.convertFirstUnit du
                let tot = dos.Total |> Total.convertFirstUnit du
                let qty_adj = dos.QuantityAdjust |> QuantityAdjust.convertFirstUnit du
                let ptm_adj = dos.PerTimeAdjust |> PerTimeAdjust.convertFirstUnit du
                let rte_adj = dos.RateAdjust |> RateAdjust.convertFirstUnit du
                let tot_adj = dos.TotalAdjust |> TotalAdjust.convertFirstUnit du

                create qty ptm rte tot qty_adj ptm_adj rte_adj tot_adj


            /// <summary>
            /// Create a string list from a Dose where each string is
            /// a variable name with the value range and the Unit
            /// </summary>
            let toString = toOrdVars >> List.map (OrderVariable.toString false)


            /// Help functions to print values of a Dose
            module Print =


                let doseTo getter toStr = inf >> getter >> toStr


                let doseQuantityTo md prec =
                    let toStr =
                        if md then
                            Quantity.toValueUnitMarkdown prec
                        else
                            Quantity.toValueUnitString prec

                    doseTo _.Quantity toStr


                let doseQuantityToString = doseQuantityTo false


                let doseQuantityToMd = doseQuantityTo true


                let doseQuantityAdjustTo md prec =
                    let toStr =
                        if md then
                            QuantityAdjust.toValueUnitMarkdown prec
                        else
                            QuantityAdjust.toValueUnitString prec

                    doseTo _.QuantityAdjust toStr


                let doseQuantityAdjustToString = doseQuantityAdjustTo false


                let doseQuantityAdjustToMd = doseQuantityAdjustTo true


                let dosePerTimeTo md prec =
                    let toStr =
                        if md then
                            PerTime.toValueUnitMarkdown prec
                        else
                            PerTime.toValueUnitString prec

                    doseTo _.PerTime toStr


                let dosePerTimeToString = dosePerTimeTo false


                let dosePerTimeToMd = dosePerTimeTo true


                let dosePerTimeAdjustTo md prec =
                    let toStr =
                        if md then
                            PerTimeAdjust.toValueUnitMarkdown prec
                        else
                            PerTimeAdjust.toValueUnitString prec

                    doseTo _.PerTimeAdjust toStr


                let dosePerTimeAdjustToString = dosePerTimeAdjustTo false


                let dosePerTimeAdjustToMd = dosePerTimeAdjustTo true


                let doseRateTo md prec =
                    let toStr =
                        if md then
                            Rate.toValueUnitMarkdown prec
                        else
                            Rate.toValueUnitString prec

                    doseTo _.Rate toStr


                let doseRateToString = doseRateTo false


                let doseRateToMd = doseRateTo true


                let doseRateAdjustTo md prec =
                    let toStr =
                        if md then
                            RateAdjust.toValueUnitMarkdown prec
                        else
                            RateAdjust.toValueUnitString prec

                    doseTo _.RateAdjust toStr


                let doseRateAdjustToString = doseRateAdjustTo false


                let doseRateAdjustToMd = doseRateAdjustTo true


                let doseConstraints toOvar prec =
                    inf
                    >> toOvar
                    >> _.DefinedConstraints
                    >> OrderVariable.Constraints.toMinMaxString prec


                let doseQuantityConstraints =
                    fun (dos: Dose) -> dos.Quantity |> Quantity.toOrdVar
                    |> doseConstraints


                let doseQuantityAdjustConstraints prec dos =
                    let s =
                        (dos |> inf).QuantityAdjust
                        |> QuantityAdjust.toOrdVar
                        |> _.DefinedConstraints
                        |> OrderVariable.Constraints.toMinMaxString prec

                    match
                        dos.Quantity
                        |> Quantity.toOrdVar
                        |> _.DefinedConstraints
                        |> OrderVariable.Constraints.toMaxString prec
                    with
                    | None -> s
                    | Some max ->
                        if s |> String.isNullOrWhiteSpace then
                            $"max. {max}"
                        else
                            $"{s}, max. {max}"


                let dosePerTimeConstraints =
                    fun dos -> dos.PerTime |> PerTime.toOrdVar
                    |> doseConstraints


                let dosePerTimeAdjustConstraints prec dos =
                    let s =
                        (dos |> inf).PerTimeAdjust
                        |> PerTimeAdjust.toOrdVar
                        |> _.DefinedConstraints
                        |> OrderVariable.Constraints.toMinMaxString prec

                    match
                        (dos.PerTime |> PerTime.toOrdVar).DefinedConstraints
                        |> OrderVariable.Constraints.toMaxString prec
                    with
                    | None -> s
                    | Some max ->
                        if s |> String.isNullOrWhiteSpace then
                            $"max. {max}"
                        else
                            $"{s}, max. {max}"


                let doseRateConstraints =
                    fun dos -> dos.Rate |> Rate.toOrdVar
                    |> doseConstraints


                let doseRateAdjustConstraints prec dos =
                    let s =
                        (dos |> inf).RateAdjust
                        |> RateAdjust.toOrdVar
                        |> _.DefinedConstraints
                        |> OrderVariable.Constraints.toMinMaxString prec

                    match
                        (dos.Rate |> Rate.toOrdVar).DefinedConstraints
                        |> OrderVariable.Constraints.toMaxString prec
                    with
                    | None -> s
                    | Some max ->
                        if s |> String.isNullOrWhiteSpace then
                            $"max. {max}"
                        else
                            $"{s}, max. {max}"


            /// Functions to create a Dose Dto and vice versa.
            module Dto =
                open Informedica.GenOrder.Lib


                module Units = ValueUnit.Units
                module Quantity = OrderVariable.Quantity
                module QuantityPerTime = OrderVariable.PerTime
                module Rate = OrderVariable.Rate
                module Total = OrderVariable.Total
                module QuantityAdjust = OrderVariable.QuantityAdjust
                module QuantityPerTimeAdjust = OrderVariable.PerTimeAdjust
                module RateAdjust = OrderVariable.RateAdjust
                module TotalAdjust = OrderVariable.TotalAdjust


                /// The Dose Dto type
                type Dto() =
                    member val Quantity = OrderVariable.Dto.dto () with get, set
                    member val PerTime = OrderVariable.Dto.dto () with get, set
                    member val Rate = OrderVariable.Dto.dto () with get, set
                    member val Total = OrderVariable.Dto.dto () with get, set
                    member val QuantityAdjust = OrderVariable.Dto.dto () with get, set
                    member val PerTimeAdjust = OrderVariable.Dto.dto () with get, set
                    member val RateAdjust = OrderVariable.Dto.dto () with get, set
                    member val TotalAdjust = OrderVariable.Dto.dto () with get, set


                /// Create a Dose from a Dose Dto
                let fromDto (dto: Dto) =

                    let qty = dto.Quantity |> Quantity.fromDto
                    let ptm = dto.PerTime |> PerTime.fromDto
                    let rte = dto.Rate |> Rate.fromDto
                    let tot = dto.Total |> Total.fromDto
                    let qty_adj = dto.QuantityAdjust |> QuantityAdjust.fromDto
                    let ptm_adj = dto.PerTimeAdjust |> PerTimeAdjust.fromDto
                    let rte_adj = dto.RateAdjust |> RateAdjust.fromDto
                    let tot_adj = dto.TotalAdjust |> TotalAdjust.fromDto

                    create qty ptm rte tot qty_adj ptm_adj rte_adj tot_adj


                /// Create a Dose Dto from a Dose
                let toDto (dos: Dose) =
                    let dto = Dto()

                    dto.Quantity <- dos.Quantity |> Quantity.toDto
                    dto.PerTime <- dos.PerTime |> PerTime.toDto
                    dto.Rate <- dos.Rate |> Rate.toDto
                    dto.Total <- dos.Total |> Total.toDto
                    dto.QuantityAdjust <- dos.QuantityAdjust |> QuantityAdjust.toDto
                    dto.PerTimeAdjust <- dos.PerTimeAdjust |> PerTimeAdjust.toDto
                    dto.RateAdjust <- dos.RateAdjust |> RateAdjust.toDto
                    dto.TotalAdjust <- dos.TotalAdjust |> TotalAdjust.toDto

                    let getLevel b =
                        if b then
                            OrderVariable.Dto.IsNormal
                        else
                            OrderVariable.Dto.IsAlert

                    dto.Quantity.Level <- dos.Quantity |> Quantity.isWithinConstraints false |> getLevel

                    dto.QuantityAdjust.Level <-
                        dos.QuantityAdjust |> QuantityAdjust.isWithinConstraints false |> getLevel

                    dto.PerTime.Level <- dos.PerTime |> PerTime.isWithinConstraints false |> getLevel
                    dto.PerTimeAdjust.Level <- dos.PerTimeAdjust |> PerTimeAdjust.isWithinConstraints false |> getLevel
                    dto.Rate.Level <- dos.Rate |> Rate.isWithinConstraints false |> getLevel
                    dto.RateAdjust.Level <- dos.RateAdjust |> RateAdjust.isWithinConstraints false |> getLevel
                    dto

                /// Create an empty Dose Dto
                let dto () = Dto()


                /// Clean a Dose Dto
                let clean (dto: Dto) =
                    dto.Quantity |> OrderVariable.Dto.cleanVariable
                    dto.PerTime |> OrderVariable.Dto.cleanVariable
                    dto.Rate |> OrderVariable.Dto.cleanVariable
                    dto.Total |> OrderVariable.Dto.cleanVariable
                    dto.QuantityAdjust |> OrderVariable.Dto.cleanVariable
                    dto.PerTimeAdjust |> OrderVariable.Dto.cleanVariable
                    dto.RateAdjust |> OrderVariable.Dto.cleanVariable
                    dto.TotalAdjust |> OrderVariable.Dto.cleanVariable


        /// Type and functions that models an
        /// `Order` `Item` that is contained in
        /// a `Component`
        [<RequireQualifiedAccess>]
        module Item =

            module Concentration = OrderVariable.Concentration
            module Quantity = OrderVariable.Quantity
            module Total = OrderVariable.Total
            module Rate = OrderVariable.Rate
            module Literals = EquationMapping.Literals

            /// Apply **f** to an `item`
            let apply f (itm: Item) = itm |> f


            /// Utility method to facilitate type inference
            let inf = apply id


            /// <summary>
            /// Create an `Item` with
            /// </summary>
            /// <param name="n">The name of the Item</param>
            /// <param name="cmp_qty">The quantity of the item in the Component</param>
            /// <param name="orb_qty">The quantity of the item in the Orderable</param>
            /// <param name="cmp_cnc">The concentration of the item in the Component</param>
            /// <param name="orb_cnc">The concentration of the item in the Orderable</param>
            /// <param name="dos">The dose of the item</param>
            let create n cmp_qty orb_qty cmp_cnc orb_cnc dos =
                {
                    Name = n
                    ComponentQuantity = cmp_qty
                    OrderableQuantity = orb_qty
                    ComponentConcentration = cmp_cnc
                    OrderableConcentration = orb_cnc
                    Dose = dos
                }


            /// <summary>
            /// Create a new `Item` with
            /// </summary>
            /// <param name="id">The Id of the Item</param>
            /// <param name="orbN">The name of the Orderable</param>
            /// <param name="cmpN">The name of the Component</param>
            /// <param name="itmN">The name of the Item</param>
            let createNew id orbN cmpN itmN =
                let un = Unit.NoUnit
                let n = [ id; orbN; cmpN; itmN ] |> Name.create

                let cmp_qty = let n = n |> Name.add Literals.cmp in Quantity.create n un
                let orb_qty = let n = n |> Name.add Literals.orb in Quantity.create n un
                let cmp_cnc = let n = n |> Name.add Literals.cmp in Concentration.create n un un
                let orb_cnc = let n = n |> Name.add Literals.orb in Concentration.create n un un
                let dos = Dose.createNew n

                create (itmN |> Name.fromString) cmp_qty orb_qty cmp_cnc orb_cnc dos


            /// Get the `Name` of an `Item`
            let getName itm = (itm |> inf).Name


            /// <summary>
            /// Return an Item as a list of OrderVariables
            /// </summary>
            /// <param name="itm">The Item</param>
            let toOrdVars itm =
                let itm_cmp_qty = (itm |> inf).ComponentQuantity |> Quantity.toOrdVar
                let itm_orb_qty = itm.OrderableQuantity |> Quantity.toOrdVar
                let itm_cmp_cnc = itm.ComponentConcentration |> Concentration.toOrdVar
                let itm_orb_cnc = itm.OrderableConcentration |> Concentration.toOrdVar

                [
                    itm_cmp_qty
                    itm_orb_qty
                    itm_cmp_cnc
                    itm_orb_cnc
                    yield! itm.Dose |> Dose.toOrdVars
                ]


            /// <summary>
            /// Create a new Item from a list of OrderVariables using
            /// an old Item.
            /// </summary>
            /// <param name="ovars">The list of OrderVariables</param>
            /// <param name="itm">The old Item</param>
            let fromOrdVars ovars itm =
                let cmp_qty = (itm |> inf).ComponentQuantity |> Quantity.fromOrdVar ovars
                let orb_qty = itm.OrderableQuantity |> Quantity.fromOrdVar ovars
                let cmp_cnc = itm.ComponentConcentration |> Concentration.fromOrdVar ovars
                let orb_cnc = itm.OrderableConcentration |> Concentration.fromOrdVar ovars
                let dos = itm.Dose |> Dose.fromOrdVars ovars

                create itm.Name cmp_qty orb_qty cmp_cnc orb_cnc dos


            /// <summary>
            /// Apply constraints to an Item
            /// </summary>
            /// <param name="itm">The Item</param>
            let applyConstraints itm =
                let cmp_qty = (itm |> inf).ComponentQuantity |> Quantity.applyConstraints
                let orb_qty = itm.OrderableQuantity |> Quantity.applyConstraints
                let cmp_cnc = itm.ComponentConcentration |> Concentration.applyConstraints
                let orb_cnc = itm.OrderableConcentration |> Concentration.applyConstraints
                let dos = itm.Dose |> Dose.applyConstraints

                create itm.Name cmp_qty orb_qty cmp_cnc orb_cnc dos


            let isOrderableConcentrationCleared itm =
                (itm |> inf).OrderableConcentration |> Concentration.isCleared


            let isOrderableQuantityCleared itm =
                (itm |> inf).OrderableQuantity |> Quantity.isCleared


            /// Get the `Item` dose
            let getDose itm = (itm |> inf).Dose


            let isSolved = toOrdVars >> List.forall OrderVariable.isSolved


            let setDoseUnit sn du itm =
                if itm |> getName |> Name.toStringList |> List.exists ((=) sn) |> not then
                    itm
                else
                    { itm with Dose = itm.Dose |> Dose.setDoseUnit du }


            let isDoseSolved = getDose >> Dose.isSolved


            let isDoseQuantityCleared = getDose >> Dose.isQuantityCleared


            let isDoseRateCleared = getDose >> Dose.isRateCleared


            let isDosePerTimeCleared = getDose >> Dose.isPerTimeCleared


            /// <summary>
            /// Create a string list from an Item where each string is
            /// a variable name with the value range and the Unit
            /// </summary>
            let toString = toOrdVars >> List.map (OrderVariable.toString false)


            module Print =


                let private getToStr getter toStr = inf >> getter >> toStr


                let concentrationTo getConc md prec =
                    if md then
                        Concentration.toValueUnitMarkdown prec
                    else
                        Concentration.toValueUnitString prec
                    |> getToStr getConc


                let itemComponentConcentrationTo = concentrationTo _.ComponentConcentration


                let itemComponentConcentrationToString = itemComponentConcentrationTo false


                let itemComponentConcentrationToMd = itemComponentConcentrationTo true


                let itemOrderableConcentrationTo = concentrationTo _.OrderableConcentration


                let itemOrderableConcentrationToString = itemOrderableConcentrationTo false


                let itemOrderableConcentrationToMd = itemOrderableConcentrationTo true


                let quantityTo getQty md prec =
                    if md then
                        Quantity.toValueUnitMarkdown prec
                    else
                        Quantity.toValueUnitString prec
                    |> getToStr getQty


                let componentQuantityTo = quantityTo _.ComponentQuantity


                let itemComponentQuantityToString = componentQuantityTo false


                let itemComponentQuantityToMd = componentQuantityTo true


                let orderableQuantityTo = quantityTo _.OrderableQuantity


                let itemOrderableQuantityToString = orderableQuantityTo false


                let itemOrderableQuantityToMd = itemOrderableConcentrationTo true


                let itemDoseQuantityTo md prec =
                    inf >> _.Dose >> Dose.Print.doseQuantityTo md prec


                let itemDoseQuantityToString = itemDoseQuantityTo false 3


                let itemDoseQuantityToMd = itemDoseQuantityTo true 3


                let itemDoseQuantityAdjustTo md prec =
                    inf >> _.Dose >> Dose.Print.doseQuantityAdjustTo md prec


                let itemDoseQuantityAdjustToString = itemDoseQuantityAdjustTo false 3


                let itemDoseQuantityAdjustToMd = itemDoseQuantityAdjustTo true 3


                let itemDosePerTimeTo md prec =
                    inf >> _.Dose >> Dose.Print.dosePerTimeTo md prec


                let itemDosePerTimeToString = itemDosePerTimeTo false 3


                let itemDosePerTimeToMd = itemDosePerTimeTo true 3


                let itemDosePerTimeAdjustTo md prec =
                    inf >> _.Dose >> Dose.Print.dosePerTimeAdjustTo md prec


                let itemDosePerTimeToAdjustString = itemDosePerTimeAdjustTo false 3


                let itemDosePerTimeAdjustToMd = itemDosePerTimeAdjustTo true 3


                let itemDoseRateTo md prec =
                    inf >> _.Dose >> Dose.Print.doseRateTo md prec


                let itemDoseRateToString = itemDoseRateTo false 3


                let itemDoseRateToMd = itemDoseRateTo true 3


                let itemDoseRateAdjustTo md prec =
                    inf >> _.Dose >> Dose.Print.doseRateAdjustTo md prec


                let itemDoseRateAdjustToString = itemDoseRateAdjustTo false 3


                let itemDoseRateAdjustToMd = itemDoseRateAdjustTo true 3


            /// Functions to create an Item Dto and vice versa.
            module Dto =

                module Units = ValueUnit.Units
                module Id = WrappedString.Id
                module Name = WrappedString.Name
                module Quantity = OrderVariable.Quantity
                module Concentration = OrderVariable.Concentration


                type Dto() =
                    member val Name = "" with get, set
                    member val ComponentQuantity = OrderVariable.Dto.dto () with get, set
                    member val OrderableQuantity = OrderVariable.Dto.dto () with get, set
                    member val ComponentConcentration = OrderVariable.Dto.dto () with get, set
                    member val OrderableConcentration = OrderVariable.Dto.dto () with get, set
                    member val Dose = Dose.Dto.dto () with get, set


                let fromDto (dto: Dto) =
                    let n = dto.Name |> Name.fromString
                    let cmp_qty = dto.ComponentQuantity |> Quantity.fromDto
                    let orb_qty = dto.OrderableQuantity |> Quantity.fromDto
                    let cmp_cnc = dto.ComponentConcentration |> Concentration.fromDto
                    let orb_cnc = dto.OrderableConcentration |> Concentration.fromDto
                    let dos = dto.Dose |> Dose.Dto.fromDto

                    create n cmp_qty orb_qty cmp_cnc orb_cnc dos


                let toDto (itm: Item) =
                    let dto = Dto()

                    dto.Name <- itm.Name |> Name.toString
                    dto.ComponentQuantity <- itm.ComponentQuantity |> Quantity.toDto
                    dto.OrderableQuantity <- itm.OrderableQuantity |> Quantity.toDto
                    dto.ComponentConcentration <- itm.ComponentConcentration |> Concentration.toDto
                    dto.OrderableConcentration <- itm.OrderableConcentration |> Concentration.toDto
                    dto.Dose <- itm.Dose |> Dose.Dto.toDto

                    let getLevel b =
                        if b then
                            OrderVariable.Dto.IsNormal
                        else
                            OrderVariable.Dto.IsWarning

                    dto.OrderableQuantity.Level <-
                        itm.OrderableQuantity |> Quantity.isWithinConstraints false |> getLevel

                    dto.ComponentConcentration.Level <-
                        itm.ComponentConcentration
                        |> Concentration.isWithinConstraints false
                        |> getLevel

                    dto


                /// <summary>
                /// Create a new Item Dto
                /// </summary>
                /// <param name="id">The Id of the Item</param>
                /// <param name="orbN">The name of the Orderable</param>
                /// <param name="cmpN">The name of the Component</param>
                /// <param name="itmN">The name of the Item</param>
                let dto id orbN cmpN itmN = createNew id orbN cmpN itmN |> toDto


        /// Types and functions to model a
        /// `Component` in an `Orderable`.
        /// A `Component` contains a list
        /// of `Item`s
        [<RequireQualifiedAccess>]
        module Component =


            module Name = Name
            module Quantity = OrderVariable.Quantity
            module Concentration = OrderVariable.Concentration
            module Count = OrderVariable.Count
            module Literals = EquationMapping.Literals

            /// Apply **f** to a `Component` **comp**
            let apply f (comp: Component) = comp |> f


            /// Utility to facilitate type inference
            let inf = apply id


            /// <summary>
            /// Create a `Component` with
            /// </summary>
            /// <param name="id">The Id of the Component</param>
            /// <param name="nm">The name of the Component</param>
            /// <param name="frm">The form of the Component</param>
            /// <param name="cmp_qty">The quantity of the Component</param>
            /// <param name="orb_qty">The quantity of the Component in the Orderable</param>
            /// <param name="orb_cnt">The count of the Component in the Orderable</param>
            /// <param name="ord_qty">The quantity of the Component in the Order</param>
            /// <param name="ord_cnt">The count of the Component in the Order</param>
            /// <param name="orb_cnc">The concentration of the Component in the Orderable</param>
            /// <param name="dos">The dose of the Component</param>
            /// <param name="ii">The list of Items in the Component</param>
            let create id nm frm cmp_qty orb_qty orb_cnt ord_qty ord_cnt orb_cnc dos ii =
                {
                    Id = id
                    Name = nm
                    Form = frm
                    ComponentQuantity = cmp_qty
                    OrderableQuantity = orb_qty
                    OrderableCount = orb_cnt
                    OrderQuantity = ord_qty
                    OrderCount = ord_cnt
                    OrderableConcentration = orb_cnc
                    Dose = dos
                    Items = ii
                }


            /// <summary>
            /// Create a new `Component` with
            /// </summary>
            /// <param name="id">The Id of the Component</param>
            /// <param name="orbN">The name of the Orderable</param>
            /// <param name="cmpN">The name of the Component</param>
            /// <param name="frm">The form of the Component</param>
            let createNew id orbN cmpN frm =
                let un = Unit.NoUnit
                let nm = [ id; orbN; cmpN ] |> Name.create
                let id = Id.create id

                let cmp_qty = let n = nm |> Name.add Literals.cmp in Quantity.create n un
                let orb_qty = let n = nm |> Name.add Literals.orb in Quantity.create n un
                let orb_cnt = let n = nm |> Name.add Literals.orb in Count.create n
                let ord_qty = let n = nm |> Name.add Literals.ord in Quantity.create n un
                let ord_cnt = let n = nm |> Name.add Literals.ord in Count.create n
                let orb_cnc = let n = nm |> Name.add Literals.orb in Concentration.create n un un
                let dos = Dose.createNew nm

                create id (cmpN |> Name.fromString) frm cmp_qty orb_qty orb_cnt ord_qty ord_cnt orb_cnc dos []


            let applyToItems pred f cmp =
                { (cmp |> inf) with Items = cmp.Items |> List.map (fun itm -> if itm |> pred then f itm else itm) }


            let applyToAllItems = applyToItems (fun _ -> true)


            let applyToItem s =
                applyToItems (_.Name >> Name.toString >> String.equalsCapInsens s)


            /// Get the name of a `Component`
            let getName cmp = (cmp |> inf).Name


            /// Get the `Item`s in an `Component`
            let getItems cmp = (cmp |> inf).Items


            let hasItem itm =
                getItems
                >> List.map (_.Name >> Name.toString)
                >> List.exists (String.equalsCapInsens itm)


            /// <summary>
            /// Return a Component as a list of OrderVariables
            /// </summary>
            /// <param name="cmp">The Component</param>
            let toOrdVars cmp =
                let cmp_qty = (cmp |> inf).ComponentQuantity |> Quantity.toOrdVar
                let orb_qty = cmp.OrderableQuantity |> Quantity.toOrdVar
                let orb_cnt = cmp.OrderableCount |> Count.toOrdVar
                let orb_cnc = cmp.OrderableConcentration |> Concentration.toOrdVar
                let ord_qty = cmp.OrderQuantity |> Quantity.toOrdVar
                let ord_cnt = cmp.OrderCount |> Count.toOrdVar

                [
                    cmp_qty
                    orb_qty
                    orb_cnt
                    orb_cnc
                    ord_qty
                    ord_cnt
                    yield! cmp.Dose |> Dose.toOrdVars
                    yield! cmp.Items |> List.collect Item.toOrdVars
                ]


            /// <summary>
            /// Create a new Component from a list of OrderVariables using
            /// an old Component.
            /// </summary>
            /// <param name="ovars">The list of OrderVariables</param>
            /// <param name="cmp">The old Component</param>
            let fromOrdVars ovars cmp =
                let cmp_qty = (cmp |> inf).ComponentQuantity |> Quantity.fromOrdVar ovars
                let orb_qty = cmp.OrderableQuantity |> Quantity.fromOrdVar ovars
                let orb_cnt = cmp.OrderableCount |> Count.fromOrdVar ovars
                let orb_cnc = cmp.OrderableConcentration |> Concentration.fromOrdVar ovars
                let ord_qty = cmp.OrderQuantity |> Quantity.fromOrdVar ovars
                let ord_cnt = cmp.OrderCount |> Count.fromOrdVar ovars
                let dos = cmp.Dose |> Dose.fromOrdVars ovars

                cmp.Items
                |> List.map (Item.fromOrdVars ovars)
                |> create cmp.Id cmp.Name cmp.Form cmp_qty orb_qty orb_cnt ord_qty ord_cnt orb_cnc dos


            /// <summary>
            /// Apply constraints to a Component
            /// </summary>
            /// <param name="cmp">The Component</param>
            let applyConstraints cmp =
                let cmp_qty = (cmp |> inf).ComponentQuantity |> Quantity.applyConstraints
                let orb_qty = cmp.OrderableQuantity |> Quantity.applyConstraints
                let orb_cnt = cmp.OrderableCount |> Count.applyConstraints
                let orb_cnc = cmp.OrderableConcentration |> Concentration.applyConstraints
                let ord_qty = cmp.OrderQuantity |> Quantity.applyConstraints
                let ord_cnt = cmp.OrderCount |> Count.applyConstraints
                let dos = cmp.Dose |> Dose.applyConstraints

                cmp.Items
                |> List.map Item.applyConstraints
                |> create cmp.Id cmp.Name cmp.Form cmp_qty orb_qty orb_cnt ord_qty ord_cnt orb_cnc dos


            let isOrderableConcentrationCleared cmp =
                (cmp |> inf).OrderableConcentration |> Concentration.isCleared


            let isItemOrderableConcentrationCleared =
                getItems >> List.exists Item.isOrderableConcentrationCleared


            let isOrderableQuantityCleared cmp =
                (cmp |> inf).OrderableQuantity |> Quantity.isCleared


            /// Get the Component dose
            let getDose cmp = (cmp |> inf).Dose


            let isSolved = toOrdVars >> List.forall OrderVariable.isSolved


            /// <summary>
            /// Increase the increment of a Component to a maximum
            /// count using a list of increments.
            /// </summary>
            /// <param name="maxCount">The maximum count</param>
            /// <param name="incrs">The list of increments</param>
            /// <param name="cmp">The Component</param>
            let increaseQuantityIncrement maxCount incrs cmp =
                { (cmp |> inf) with
                    OrderableQuantity = cmp.OrderableQuantity |> Quantity.increaseIncrement maxCount incrs
                }


            /// <summary>
            /// Harmonize the valid concentration value indices across all items in a component.
            /// </summary>
            /// <remarks>
            /// When a component is created by combining multiple medication products (e.g., tablets),
            /// each item (substance) accumulates concentration values from all products. The indices
            /// represent which product each value came from, ensuring valid combinations.
            ///
            /// Example: Two tablets combined into one component:
            /// - Tablet 1: Substance A = 10 mg/tablet, Substance B = 20 mg/tablet
            /// - Tablet 2: Substance A = 30 mg/tablet, Substance B = 25 mg/tablet
            ///
            /// Results in:
            /// - Item A concentrations: [10; 30] mg/tablet (indices 0, 1)
            /// - Item B concentrations: [20; 25] mg/tablet (indices 0, 1)
            ///
            /// Index 0 corresponds to Tablet 1 (10 mg A + 20 mg B).
            /// Index 1 corresponds to Tablet 2 (30 mg A + 25 mg B).
            ///
            /// A combination of 10 mg A with 25 mg B is invalid because no tablet has that
            /// concentration pair. This function ensures all items share the same valid indices,
            /// so when one item's values are constrained (e.g., only index 0 remains valid),
            /// all other items are updated to use only those same indices.
            /// </remarks>
            /// <param name="cmp">The Component to harmonize</param>
            /// <returns>
            /// A tuple of (bool * Component) where the bool indicates if harmonization
            /// was applied, and the Component is the possibly modified component.
            /// </returns>
            let harmonizeItemConcentrations cmp =
                if (cmp |> inf).Items |> List.length <= 1 then
                    false, cmp
                else
                    match
                        cmp.Items
                        |> List.map _.ComponentConcentration
                        // Get the indices of remaining valid concentration values.
                        // ValueUnit preserves insertion order (no sorting), and values are added
                        // in consistent product order during constraint creation, so each index
                        // corresponds to a specific product across all items.
                        |> List.map (Concentration.toOrdVar >> OrderVariable.getIndices)
                        |> List.distinct
                    with
                    | []
                    | [ _ ] -> false, cmp
                    | xs ->
                        let indices = xs |> List.sortBy Array.length |> List.head

                        true,
                        { cmp with
                            Items =
                                cmp.Items
                                |> List.map (fun itm ->
                                    { itm with
                                        ComponentConcentration =
                                            itm.ComponentConcentration
                                            // Apply the harmonized indices to each item's concentration.
                                            |> Concentration.applyIndices indices
                                    }
                                )
                        }


            let pickNearestHigherElseLower cmp =
                { (cmp |> inf) with
                    ComponentQuantity =
                        cmp.ComponentQuantity
                        |> Quantity.pickNearestHigherElseLower cmp.OrderableQuantity
                }


            let setDoseUnit sn du =
                applyToAllItems (Item.setDoseUnit sn du)


            let isDoseSolved = getDose >> Dose.isSolved


            let isItemsDoseSolved = getItems >> List.exists Item.isDoseSolved


            let isDoseQuantityCleared = getDose >> Dose.isQuantityCleared


            let isItemDoseQuantityCleared = getItems >> List.exists Item.isDoseQuantityCleared


            let isDoseRateCleared = getDose >> Dose.isRateCleared


            let isItemDoseRateCleared = getItems >> List.exists Item.isDoseRateCleared


            let isDosePerTimeCleared = getDose >> Dose.isPerTimeCleared


            let isItemDosePerTimeCleared = getItems >> List.exists Item.isDosePerTimeCleared


            /// <summary>
            /// Create a string list from a Component where each string is
            /// a variable name with the value range and the Unit
            /// </summary>
            let toString = toOrdVars >> List.map (OrderVariable.toString false)


            module Print =


                let private getToStr get toStr = inf >> get >> toStr


                let quantityTo get md prec =
                    let toStr =
                        if md then
                            Quantity.toValueUnitMarkdown prec
                        else
                            Quantity.toValueUnitString prec

                    getToStr get toStr


                let componentQuantityTo = quantityTo _.ComponentQuantity


                let componentQuantityToString = componentQuantityTo false


                let componentQuantityToMd = componentQuantityTo true


                let componentOrderableQuantityTo = quantityTo _.OrderableQuantity


                let componentOrderableQuantityToString = componentOrderableQuantityTo false


                let componentOrderableQuantityToMd = componentOrderableQuantityTo true


                let componentOrderQuantityTo = quantityTo _.OrderQuantity

                let componentOrderQuantityToString = componentOrderQuantityTo false


                let componentOrderQuantityToMd = componentOrderQuantityTo true


                let countTo get md =
                    let toStr =
                        if md then
                            Count.toValueUnitMarkdown -1
                        else
                            Count.toValueUnitString -1

                    getToStr get toStr


                let componentOrderableCountTo = countTo _.OrderableCount


                let componentOrderableCountToString = componentOrderableCountTo false


                let componentOrderableCountToMd = componentOrderableCountTo true


                let componentOrderCountTo = countTo _.OrderCount


                let componentOrderCountToString = componentOrderCountTo false


                let componentOrderCountToMd = componentOrderCountTo true


                let concentrationTo get md =
                    let toStr =
                        if md then
                            Concentration.toValueUnitMarkdown -1
                        else
                            Concentration.toValueUnitString -1

                    getToStr get toStr


                let componentOrderableConcentrationTo = concentrationTo _.OrderableConcentration


                let componentOrderableConcentrationToString prec =
                    componentOrderableConcentrationTo false prec


                let componentOrderableConcentrationToMd prec =
                    componentOrderableConcentrationTo true prec


                let componentDoseQuantityTo md (cmp: Component) =
                    cmp.Dose |> Dose.Print.doseQuantityTo md -1


                let componentDoseQuantityToString = componentDoseQuantityTo false


                let componentDoseQuantityToMd = componentDoseQuantityTo true


                let componentDoseQuantityAdjustTo md prec (cmp: Component) =
                    cmp.Dose |> Dose.Print.doseQuantityTo md prec


                let componentDoseQuantityAdjustToString = componentDoseQuantityAdjustTo false


                let componentDoseQuantityAdjustToMd = componentDoseQuantityAdjustTo true


                let componentDosePerTimeTo md prec (cmp: Component) =
                    cmp.Dose |> Dose.Print.dosePerTimeTo md prec


                let componentDosePerTimeToString = componentDosePerTimeTo false


                let componentDosePerTimeToMd = componentDosePerTimeTo true


                let componentDosePerTimeAdjustTo md prec (cmp: Component) =
                    cmp.Dose |> Dose.Print.dosePerTimeAdjustTo md prec


                let componentDosePerTimeAdjustToString = componentDosePerTimeAdjustTo false


                let componentDosePerTimeAdjustToMd = componentDosePerTimeAdjustTo true


                let componentDoseRateTo md prec (cmp: Component) =
                    cmp.Dose |> Dose.Print.doseRateTo md prec


                let componentDoseRateToString = componentDoseRateTo false


                let componentDoseRateToMd = componentDoseRateTo true


                let componentDoseRateAdjustTo md prec (cmp: Component) =
                    cmp.Dose |> Dose.Print.doseRateAdjustTo md prec


                let componentDoseRateAdjustToString = componentDoseRateAdjustTo false


                let componentDoseRateAdjustToMd = componentDoseRateAdjustTo true


            /// Helper functions for the Component Dto
            module Dto =

                module Units = ValueUnit.Units
                module Id = WrappedString.Id
                module Name = WrappedString.Name
                module Quantity = OrderVariable.Quantity
                module Concentration = OrderVariable.Concentration
                module CT = OrderVariable.Count

                type Dto() =
                    member val Id = "" with get, set
                    member val Name = "" with get, set
                    member val Form = "" with get, set
                    member val ComponentQuantity = OrderVariable.Dto.dto () with get, set
                    member val OrderableQuantity = OrderVariable.Dto.dto () with get, set
                    member val OrderableCount = OrderVariable.Dto.dto () with get, set
                    member val OrderQuantity = OrderVariable.Dto.dto () with get, set
                    member val OrderCount = OrderVariable.Dto.dto () with get, set
                    member val OrderableConcentration = OrderVariable.Dto.dto () with get, set
                    member val Dose = Dose.Dto.dto () with get, set
                    member val Items: Item.Dto.Dto list = [] with get, set


                let fromDto (dto: Dto) =

                    let id = dto.Id |> Id.create
                    let n = dto.Name |> Name.fromString
                    let s = dto.Form
                    let cmp_qty = dto.ComponentQuantity |> Quantity.fromDto
                    let orb_qty = dto.OrderableQuantity |> Quantity.fromDto
                    let orb_cnt = dto.OrderableCount |> Count.fromDto
                    let orb_cnc = dto.OrderableConcentration |> Concentration.fromDto
                    let ord_qty = dto.OrderQuantity |> Quantity.fromDto
                    let ord_cnt = dto.OrderCount |> Count.fromDto
                    let ii = dto.Items |> List.map Item.Dto.fromDto

                    let dos = dto.Dose |> Dose.Dto.fromDto

                    create id n s cmp_qty orb_qty orb_cnt ord_qty ord_cnt orb_cnc dos ii


                let toDto (cmp: Component) =
                    let dto = Dto()

                    dto.Name <- cmp.Name |> Name.toString
                    dto.Form <- cmp.Form
                    dto.ComponentQuantity <- cmp.ComponentQuantity |> Quantity.toDto
                    dto.OrderableQuantity <- cmp.OrderableQuantity |> Quantity.toDto
                    dto.OrderableCount <- cmp.OrderableCount |> Count.toDto
                    dto.OrderQuantity <- cmp.OrderQuantity |> Quantity.toDto
                    dto.OrderCount <- cmp.OrderCount |> Count.toDto
                    dto.OrderableConcentration <- cmp.OrderableConcentration |> Concentration.toDto
                    dto.Dose <- cmp.Dose |> Dose.Dto.toDto
                    dto.Items <- cmp.Items |> List.map Item.Dto.toDto

                    let getLevel b =
                        if b then
                            OrderVariable.Dto.IsNormal
                        else
                            OrderVariable.Dto.IsCaution

                    let isCaution =
                        [
                            cmp.OrderableQuantity |> Quantity.toOrdVar
                            cmp.OrderableConcentration |> Concentration.toOrdVar
                            yield!
                                cmp.Items
                                |> List.map _.OrderableConcentration
                                |> List.map Concentration.toOrdVar
                        ]
                        |> List.forall (OrderVariable.isWithinConstraints false)
                        |> getLevel

                    dto.OrderableQuantity.Level <- isCaution

                    dto


                /// <summary>
                /// Create a Component Dto
                /// </summary>
                /// <param name="id">The Id of the Component</param>
                /// <param name="orbN">The name of the Orderable</param>
                /// <param name="cmpN">The name of the Component</param>
                /// <param name="form">The form of the Component</param>
                let dto id orbN cmpN form = createNew id orbN cmpN form |> toDto


        module Quantity = OrderVariable.Quantity
        module Concentration = OrderVariable.Concentration
        module Count = OrderVariable.Count
        module Literals = EquationMapping.Literals


        /// Apply **f** to `Orderable` `ord`
        let apply f (orb: Orderable) = orb |> f


        /// Utility function to facilitate type inference
        let inf = apply id


        /// <summary>
        /// Create an `Orderable` with
        /// </summary>
        /// <param name="n">The name of the Orderable</param>
        /// <param name="orb_qty">The quantity of the Orderable</param>
        /// <param name="ord_qty">The quantity of the Orderable in the Order</param>
        /// <param name="ord_cnt">The count of the Orderable in the Order</param>
        /// <param name="dos_cnt">The count of the Orderable dose in the Order</param>
        /// <param name="dos">The dose of the Orderable</param>
        /// <param name="cc">The list of Components in the Orderable</param>
        let create n orb_qty ord_qty ord_cnt dos_cnt dos cc =
            {
                Name = n
                OrderableQuantity = orb_qty
                OrderQuantity = ord_qty
                OrderCount = ord_cnt
                DoseCount = dos_cnt
                Dose = dos
                Components = cc
            }


        /// <summary>
        /// Create a new `Orderable` with
        /// </summary>
        /// <param name="id">The Id of the Orderable</param>
        /// <param name="orbN">The name of the Orderable</param>
        let createNew id orbN =
            let un = Unit.NoUnit
            let n = [ id; orbN ] |> Name.create

            let orb_qty = let n = n |> Name.add Literals.orb in Quantity.create n un
            let ord_qty = let n = n |> Name.add Literals.ord in Quantity.create n un
            let ord_cnt = let n = n |> Name.add Literals.ord in Count.create n
            let dos_cnt = let n = n |> Name.add Literals.dos in Count.create n
            let dos = Dose.createNew n

            create (orbN |> Name.fromString) orb_qty ord_qty ord_cnt dos_cnt dos []


        /// Get the name of the `Orderable`
        let getName orb = (orb |> inf).Name


        /// Get the Components in an `Orderable`
        let getComponents orb = (orb |> inf).Components


        let hasComponent s =
            getComponents
            >> List.map (_.Name >> Name.toString)
            >> List.exists (String.equalsCapInsens s)


        let applyToComponents pred f orb =
            { (orb |> inf) with
                Components =
                    orb
                    |> getComponents
                    |> List.map (fun cmp -> if cmp |> pred then cmp |> f else cmp)
            }


        let applyToAllComponents = applyToComponents (fun _ -> true)


        let applyToComponent s =
            applyToComponents (_.Name >> Name.toString >> String.equalsCapInsens s)


        let harmonizeItemConcentrations logger orb =
            let mutable isHarmonized = false

            let orb =
                { (orb |> inf) with
                    Components =
                        orb.Components
                        |> List.map (fun cmp ->
                            let b, cmp = cmp |> Component.harmonizeItemConcentrations

                            if b then
                                $"Component indices harmonized for {cmp.Name}"
                                |> Events.ComponentItemsHarmonized
                                |> Logging.logInfo logger

                                isHarmonized <- true

                            cmp
                        )
                }

            isHarmonized, orb


        /// <summary>
        /// Return an Orderable as a list of OrderVariables
        /// </summary>
        /// <param name="orb">The Orderable</param>
        let toOrdVars orb =
            let ord_qty = (orb |> inf).OrderQuantity |> Quantity.toOrdVar
            let orb_qty = orb.OrderableQuantity |> Quantity.toOrdVar
            let ord_cnt = orb.OrderCount |> Count.toOrdVar
            let dos_cnt = orb.DoseCount |> Count.toOrdVar

            [
                ord_qty
                orb_qty
                ord_cnt
                dos_cnt
                yield! orb.Dose |> Dose.toOrdVars
                yield! orb.Components |> List.collect Component.toOrdVars
            ]


        /// <summary>
        /// Create a new Orderable from a list of OrderVariables using
        /// an old Orderable.
        /// </summary>
        /// <param name="ovars">The list of OrderVariables</param>
        /// <param name="orb">The old Orderable</param>
        /// <returns>The new Orderable</returns>
        let fromOrdVars ovars orb =
            let ord_qty = (orb |> inf).OrderQuantity |> Quantity.fromOrdVar ovars
            let orb_qty = orb.OrderableQuantity |> Quantity.fromOrdVar ovars
            let ord_cnt = orb.OrderCount |> Count.fromOrdVar ovars
            let dos_cnt = orb.DoseCount |> Count.fromOrdVar ovars
            let dos = orb.Dose |> Dose.fromOrdVars ovars

            orb.Components
            |> List.map (Component.fromOrdVars ovars)
            |> create orb.Name orb_qty ord_qty ord_cnt dos_cnt dos


        /// <summary>
        /// Return a list of strings from an Orderable where each string is
        /// a variable name with the value range and the Unit
        /// </summary>
        let toString = toOrdVars >> List.map (OrderVariable.toString false)


        let toStringWithConstraints =
            toOrdVars >> List.map (OrderVariable.toStringWithConstraints true false)


        /// <summary>
        /// Apply constraints to an Orderable
        /// </summary>
        /// <param name="orb">The Orderable</param>
        let applyConstraints orb =
            let ord_qty = (orb |> inf).OrderQuantity |> Quantity.applyConstraints
            let orb_qty = orb.OrderableQuantity |> Quantity.applyConstraints
            let ord_cnt = orb.OrderCount |> Count.applyConstraints
            let dos_cnt = orb.DoseCount |> Count.applyConstraints
            let dos = orb.Dose |> Dose.applyConstraints

            orb.Components
            |> List.map Component.applyConstraints
            |> create orb.Name orb_qty ord_qty ord_cnt dos_cnt dos


        let isComponentOrderableConcentrationCleared =
            getComponents >> List.exists Component.isOrderableConcentrationCleared


        let isItemOrderableConcentrationCleared =
            getComponents
            >> List.collect Component.getItems
            >> List.exists Item.isOrderableConcentrationCleared


        let isItemOrderableQuantityCleared =
            getComponents
            >> List.collect Component.getItems
            >> List.exists Item.isOrderableQuantityCleared


        let isConcentrationCleared orb =
            if (orb |> inf).Components |> List.length <= 1 then
                false
            else
                orb |> isComponentOrderableConcentrationCleared
                || orb |> isItemOrderableConcentrationCleared
                || orb |> isItemOrderableQuantityCleared


        let isOrderableQuantityCleared orb =
            (orb |> inf).OrderableQuantity |> Quantity.isCleared


        /// Get the `Orderable` dose
        let getDose orb = (orb |> inf).Dose


        let hasMaxDoseQuantityConstraint = getDose >> Dose.hasQuantityMaxConstraint


        let isSolved = toOrdVars >> List.forall OrderVariable.isSolved


        /// <summary>
        /// Increase the Quantity increment of an Orderable to a maximum
        /// count using a list of increments.
        /// </summary>
        /// <param name="maxCount">The maximum count</param>
        /// <param name="incrs">The list of increments</param>
        /// <param name="orb">The Orderable</param>
        let increaseQuantityIncrement maxCount incrs orb =
            // check if all relevant OrderVariables have an increment
            if
                (orb |> inf).Components
                |> List.map _.OrderableQuantity
                |> List.forall Quantity.hasIncrement
                |> not
            then
                orb
            else
                // first calculate the minimum increment increase for the orderable and components
                { orb with
                    Components = orb.Components |> List.map (Component.increaseQuantityIncrement maxCount incrs)
                }
                |> _.Components
                |> List.map (fun c -> (c.OrderableQuantity |> Quantity.toOrdVar |> OrderVariable.getVar).Values)
                |> List.choose Variable.ValueRange.getIncr
                |> function
                    | [] -> orb
                    | incrs ->
                        if incrs |> List.length <> (orb.Components |> List.length) then
                            orb
                        else
                            let incr =
                                incrs
                                |> List.minBy (fun incr ->
                                    incr |> Variable.ValueRange.Increment.toValueUnit |> ValueUnit.getBaseValue
                                )

                            writeDebugMessage
                                $"Increase quantity increment to {incr |> Variable.ValueRange.Increment.toString false}"
                            // apply the minimum increment increase to the orderable and components
                            { orb with
                                OrderableQuantity =
                                    orb.OrderableQuantity |> Quantity.increaseIncrement maxCount [ incr ]
                                Components =
                                    orb.Components
                                    |> List.map (Component.increaseQuantityIncrement maxCount [ incr ])
                            }


        /// <summary>
        /// Increase the Rate increment of an Orderable to a maximum
        /// count using a list of increments.
        /// </summary>
        /// <param name="maxCount">The maximum count</param>
        /// <param name="incrs">The list of increments</param>
        /// <param name="orb">The Orderable</param>
        let increaseRateIncrement maxCount incrs orb =
            { (orb |> inf) with Dose = orb.Dose |> Dose.increaseRateIncrement maxCount incrs }


        let setDoseUnit sn du (orb: Orderable) =
            { orb with Components = orb.Components |> List.map (Component.setDoseUnit sn du) }


        let isDoseSolved = getDose >> Dose.isSolved


        let isComponentsDoseSolved = getComponents >> List.exists Component.isDoseSolved


        let isItemsDoseSolved = getComponents >> List.exists Component.isItemsDoseSolved


        let isDoseQuantityCleared = getDose >> Dose.isQuantityCleared


        let isComponentDoseQuantityCleared =
            getComponents >> List.exists Component.isDoseQuantityCleared


        let isItemDoseQuantityCleared =
            getComponents >> List.exists Component.isItemDoseQuantityCleared


        let isDoseRateCleared = getDose >> Dose.isRateCleared


        let isItemDoseRateCleared =
            getComponents >> List.exists Component.isItemDoseRateCleared


        let isDosePerTimeCleared = getDose >> Dose.isPerTimeCleared


        let isComponentDosePerTimeCleared =
            getComponents >> List.exists Component.isDosePerTimeCleared


        let isItemDosePerTimeCleared =
            getComponents >> List.exists Component.isItemDosePerTimeCleared


        module Print =


            let private getToStr get toStr = inf >> get >> toStr


            let quantityTo get md prec =
                if md then
                    Quantity.toValueUnitMarkdown prec
                else
                    Quantity.toValueUnitString prec
                |> getToStr get


            let orderableQuantityTo = quantityTo _.OrderableQuantity


            let orderableQuantityToString = orderableQuantityTo false


            let orderableQuantityToMd = orderableQuantityTo true


            let orderQuantityTo = quantityTo _.OrderQuantity


            let orderQuantityToString = orderQuantityTo false


            let orderQuantityToMd = orderQuantityTo true


            let countTo get md =
                if md then
                    Count.toValueUnitMarkdown -1
                else
                    Count.toValueUnitString -1
                |> getToStr get


            let orderCountTo = countTo _.OrderCount


            let orderCountToString = orderCountTo false


            let orderCountToMd = orderCountTo true


            let doseQuantityTo md prec =
                getDose >> Dose.Print.doseQuantityTo md prec


            let doseQuantityToString = doseQuantityTo false -1


            let doseQuantityToMd = doseQuantityTo true -1


            let doseQuantityAdjustTo md prec =
                getDose >> Dose.Print.doseQuantityAdjustTo md prec


            let doseQuantityAdjustToString = doseQuantityAdjustTo false


            let doseQuantityAdjustToMd = doseQuantityAdjustTo true


            let dosePerTimeTo md prec =
                getDose >> Dose.Print.dosePerTimeTo md prec


            let dosePerTimeToString = dosePerTimeTo false -1


            let dosePerTimeToMd = dosePerTimeTo true -1


            let dosePerTimeAdjustTo md prec =
                getDose >> Dose.Print.dosePerTimeAdjustTo md prec


            let dosePerTimeAdjustToString = dosePerTimeAdjustTo false


            let dosePerTimeAdjustToMd = dosePerTimeAdjustTo true


            let doseRateTo md prec =
                getDose >> Dose.Print.doseRateTo md prec


            let doseRateToString = doseRateTo false -1


            let doseRateToMd = doseRateTo true -1


            let doseRateAdjustTo md prec =
                getDose >> Dose.Print.doseRateAdjustTo md prec


            let doseRateAdjustToString = doseRateAdjustTo false -1


            let doseRateAdjustToMd = doseRateAdjustTo true -1


        /// Helper functions for the Orderable Dto
        module Dto =

            module Units = ValueUnit.Units
            module Id = WrappedString.Id
            module Name = WrappedString.Name
            module Quantity = OrderVariable.Quantity
            module Concentration = OrderVariable.Concentration
            module CT = OrderVariable.Count


            type Dto() =
                member val Name = "" with get, set
                member val OrderableQuantity = OrderVariable.Dto.dto () with get, set
                member val OrderQuantity = OrderVariable.Dto.dto () with get, set
                member val OrderCount = OrderVariable.Dto.dto () with get, set
                member val DoseCount = OrderVariable.Dto.dto () with get, set
                member val Dose = Dose.Dto.dto () with get, set
                member val Components: Component.Dto.Dto list = [] with get, set


            let fromDto (dto: Dto) =
                let n = dto.Name |> Name.fromString

                let orb_qty = dto.OrderableQuantity |> Quantity.fromDto
                let ord_qty = dto.OrderQuantity |> Quantity.fromDto
                let ord_cnt = dto.OrderCount |> Count.fromDto
                let dos_cnt = dto.DoseCount |> Count.fromDto

                let cc = dto.Components |> List.map Component.Dto.fromDto

                let dos = dto.Dose |> Dose.Dto.fromDto

                create n orb_qty ord_qty ord_cnt dos_cnt dos cc


            let toDto (orb: Orderable) =
                let dto = Dto()

                dto.Name <- orb.Name |> Name.toString
                dto.OrderableQuantity <- orb.OrderableQuantity |> Quantity.toDto
                dto.OrderQuantity <- orb.OrderQuantity |> Quantity.toDto
                dto.OrderCount <- orb.OrderCount |> Count.toDto
                dto.DoseCount <- orb.DoseCount |> Count.toDto
                dto.Dose <- orb.Dose |> Dose.Dto.toDto
                dto.Components <- orb.Components |> List.map Component.Dto.toDto

                let getLevel b =
                    if b then
                        OrderVariable.Dto.IsNormal
                    else
                        OrderVariable.Dto.IsCaution

                dto.OrderableQuantity.Level <- orb.OrderableQuantity |> Quantity.isWithinConstraints false |> getLevel

                dto


            /// <summary>
            /// Create a new Orderable Dto
            /// </summary>
            /// <param name="id">The Id of the Orderable</param>
            /// <param name="orbN">The name of the Orderable</param>
            let dto id orbN = createNew id orbN |> toDto


    [<RequireQualifiedAccess>]
    module Schedule =


        module Frequency = OrderVariable.Frequency
        module Time = OrderVariable.Time


        /// <summary>
        /// Create a Frequency and Time
        /// </summary>
        /// <param name="tu1">The frequency time unit</param>
        /// <param name="tu2">The time unit</param>
        /// <param name="n">The name of the Frequency and Time</param>
        let freqTime tu1 tu2 n =
            (Frequency.create n tu1, Time.create n tu2)


        /// Create a Once `Prescription`
        let once tu1 tu2 n =
            let _, _ = n |> freqTime tu1 tu2 in Once


        /// Create a OnceTimed `Prescription`
        let onceTimed tu1 tu2 n =
            let _, tme = n |> freqTime tu1 tu2 in tme |> OnceTimed


        /// Create a Continuous `Prescription`
        let continuous tu1 tu2 n =
            let _, tme = n |> freqTime tu1 tu2 in tme |> Continuous


        /// Create a Discontinuous `Prescription`
        let discontinuous tu1 tu2 n =
            let frq, _ = n |> freqTime tu1 tu2 in frq |> Discontinuous


        /// Create a Timed `Prescription`
        let timed tu1 tu2 n =
            let frq, tme = n |> freqTime tu1 tu2 in (frq, tme) |> Timed


        /// Check whether a `Prescription` is Once
        let isOnce =
            function
            | Once -> true
            | _ -> false


        /// Check whether a `Prescription` is Once
        let isOnceTimed =
            function
            | OnceTimed _ -> true
            | _ -> false


        /// Check whether a `Prescription` is Discontinuous
        let isDiscontinuous =
            function
            | Discontinuous _ -> true
            | _ -> false


        /// Check whether a `Prescription` is Continuous
        let isContinuous =
            function
            | Continuous _ -> true
            | _ -> false


        /// Check whether a `Prescription` is Timed
        let isTimed =
            function
            | Timed _ -> true
            | _ -> false


        let hasFrequency schedule =
            schedule |> isDiscontinuous || schedule |> isTimed


        let hasTime schedule =
            schedule |> isTimed || schedule |> isOnceTimed || schedule |> isContinuous


        let getFrequency schedule =
            match schedule with
            | Discontinuous frq
            | Timed(frq, _) -> Some frq
            | _ -> None


        let isFrequencyWithinConstraints useCalc =
            getFrequency
            >> Option.map (Frequency.isWithinConstraints useCalc)
            >> Option.defaultValue true


        let getTime schedule =
            match schedule with
            | Continuous tme
            | Timed(_, tme) -> Some tme
            | _ -> None


        let isTimeWithinConstraints useCalc =
            getTime
            >> Option.map (Time.isWithinConstraints useCalc)
            >> Option.defaultValue true


        /// <summary>
        /// Return a Schedule as a Frequency OrderVariable option
        /// and a Time OrderVariable option
        /// </summary>
        /// <param name="schedule">The Schedule</param>
        let toOrdVars schedule =
            match schedule with
            | Once -> None, None
            | Continuous tme
            | OnceTimed tme -> None, tme |> Time.toOrdVar |> Some
            | Discontinuous frq -> frq |> Frequency.toOrdVar |> Some, None
            | Timed(frq, tme) -> frq |> Frequency.toOrdVar |> Some, tme |> Time.toOrdVar |> Some


        /// <summary>
        /// Create a new Schedule from a list of OrderVariables using
        /// an old Schedule.
        /// </summary>
        /// <param name="ovars">The list of OrderVariables</param>
        /// <param name="schedule">The old Schedule</param>
        let fromOrdVars ovars schedule =
            match schedule with
            | Once -> schedule
            | Continuous tme -> tme |> Time.fromOrdVar ovars |> Continuous
            | OnceTimed tme -> tme |> Time.fromOrdVar ovars |> OnceTimed
            | Discontinuous frq -> frq |> Frequency.fromOrdVar ovars |> Discontinuous
            | Timed(frq, tme) -> (frq |> Frequency.fromOrdVar ovars, tme |> Time.fromOrdVar ovars) |> Timed


        /// <summary>
        /// Apply constraints to a Schedule
        /// </summary>
        let applyConstraints schedule =
            match schedule with
            | Once -> schedule
            | Continuous tme -> tme |> Time.applyConstraints |> Continuous
            | OnceTimed tme -> tme |> Time.applyConstraints |> OnceTimed
            | Discontinuous frq -> frq |> Frequency.applyConstraints |> Discontinuous
            | Timed(frq, tme) -> (frq |> Frequency.applyConstraints, tme |> Time.applyConstraints) |> Timed


        /// <summary>
        /// Return a list of strings from a Schedule where each string is
        /// a variable name with the value range and the Unit
        /// </summary>
        let toString (schedule: Schedule) =
            match schedule with
            | Once -> [ "eenmalig" ]
            | Continuous tme -> [ tme |> Time.toString ]
            | OnceTimed tme -> [ tme |> Time.toString ]
            | Discontinuous frq -> [ frq |> Frequency.toString ]
            | Timed(frq, tme) -> [ frq |> Frequency.toString; tme |> Time.toString ]


        /// <summary>
        /// Return a list of strings from a Prescription where each string is
        /// a variable name with the value range and the Unit
        /// </summary>
        let toStringWithConstraints (schedule: Schedule) =
            match schedule with
            | Once -> [ "eenmalig" ]
            | Continuous tme -> [ tme |> Time.toStringWithConstraints ]
            | OnceTimed tme -> [ tme |> Time.toStringWithConstraints ]
            | Discontinuous frq -> [ frq |> Frequency.toStringWithConstraints ]
            | Timed(frq, tme) ->
                [
                    frq |> Frequency.toStringWithConstraints
                    tme |> Time.toStringWithConstraints
                ]


        let isSolved (schedule: Schedule) =
            schedule
            |> toOrdVars
            |> function
                | Some ovar1, Some ovar2 -> ovar1 |> OrderVariable.isSolved && ovar2 |> OrderVariable.isSolved
                | None, Some ovar
                | Some ovar, None -> ovar |> OrderVariable.isSolved
                | None, None -> true


        let isFrequencySolved schedule =
            schedule
            |> toOrdVars
            |> function
                | Some ovar, _ -> ovar |> OrderVariable.isSolved
                | _ -> true


        let isTimeSolved schedule =
            schedule
            |> toOrdVars
            |> function
                | _, Some ovar -> ovar |> OrderVariable.isSolved
                | _ -> true


        let applyToFrequency f (schedule: Schedule) =
            match schedule with
            | Discontinuous frq -> frq |> f |> Discontinuous
            | Timed(frq, tme) -> (frq |> f, tme) |> Timed
            | _ -> schedule


        let frequencyIsCleared (schedule: Schedule) =
            match schedule |> toOrdVars with
            | Some frq, _ -> frq |> OrderVariable.isCleared
            | _ -> false


        let applyToTime f schedule =
            match schedule with
            | OnceTimed tme -> tme |> f |> OnceTimed
            | Continuous tme -> tme |> f |> Continuous
            | Timed(frq, tme) -> (frq, tme |> f) |> Timed
            | _ -> schedule


        let setMinTime = applyToTime Time.setMinValue


        module Print =


            let frequencyTo md (p: Schedule) =
                let toStr =
                    if md then
                        Frequency.toValueUnitMarkdown -1
                    else
                        Frequency.toValueUnitString -1

                match p with
                | Timed(frq, _)
                | Discontinuous frq -> frq |> toStr
                | _ -> ""


            let frequencyToString = frequencyTo false


            let frequencyToMd = frequencyTo true


            let timeTo md prec (schedule: Schedule) =
                let toStr =
                    if md then
                        Time.toValueUnitMarkdown prec
                    else
                        Time.toValueUnitMarkdown prec >> String.replace "#" "" >> String.replace "|" ""

                match schedule with
                | Continuous tme -> tme |> toStr
                | OnceTimed tme -> tme |> toStr
                | Timed(_, tme) -> tme |> toStr
                | _ -> ""


            let timeToString = timeTo false


            let timeToMd = timeTo true


            let prescriptionTo md (schedule: Schedule) =
                match schedule with
                | Once -> "eenmalig"
                | Continuous _ -> $"continu {schedule |> timeTo md -1}"
                | OnceTimed _ -> schedule |> timeTo md -1
                | Discontinuous _ -> schedule |> frequencyToString
                | Timed _ -> $"{schedule |> frequencyToString} {schedule |> timeTo md -1}"


            let prescriptionToString = prescriptionTo false


            let prescriptionToMd = prescriptionTo true


        /// Helper functions for the Schedule Dto
        module Dto =


            module Units = ValueUnit.Units
            module Id = WrappedString.Id
            module NM = Name


            type Dto() =
                member val IsOnce = false with get, set
                member val IsOnceTimed = false with get, set
                member val IsContinuous = false with get, set
                member val IsDiscontinuous = false with get, set
                member val IsTimed = false with get, set
                member val Frequency = OrderVariable.Dto.dto () with get, set
                member val Time = OrderVariable.Dto.dto () with get, set


            let fromDto (dto: Dto) =
                match dto.IsOnce, dto.IsOnceTimed, dto.IsContinuous, dto.IsDiscontinuous, dto.IsTimed with
                | true, false, false, false, false -> Once
                | false, true, false, false, false -> dto.Time |> Time.fromDto |> OnceTimed
                | false, false, true, false, false -> dto.Time |> Time.fromDto |> Continuous
                | false, false, false, true, false -> dto.Frequency |> Frequency.fromDto |> Discontinuous
                | false, false, false, false, true ->
                    (dto.Frequency |> Frequency.fromDto, dto.Time |> Time.fromDto) |> Timed
                | _ -> invalidOp "dto is neither or both process, continuous, discontinuous or timed"


            let toDto schedule =
                let dto = Dto()

                match schedule with
                | Once -> dto.IsOnce <- true
                | Continuous time ->
                    dto.IsContinuous <- true
                    dto.Time <- time |> Time.toDto
                | OnceTimed time ->
                    dto.IsOnceTimed <- true
                    dto.Time <- time |> Time.toDto
                | Discontinuous freq ->
                    dto.IsDiscontinuous <- true
                    dto.Frequency <- freq |> Frequency.toDto
                | Timed(freq, time) ->
                    dto.IsTimed <- true
                    dto.Frequency <- freq |> Frequency.toDto
                    dto.Time <- time |> Time.toDto


                let getFreqLevel b =
                    if b then
                        OrderVariable.Dto.IsNormal
                    else
                        OrderVariable.Dto.IsWarning

                let getTimeLevel b =
                    if b then
                        OrderVariable.Dto.IsNormal
                    else
                        OrderVariable.Dto.IsCaution

                dto.Frequency.Level <-
                    schedule
                    |> getFrequency
                    |> Option.map (Frequency.isWithinConstraints false >> getFreqLevel)
                    |> Option.defaultValue dto.Frequency.Level

                dto.Time.Level <-
                    schedule
                    |> getTime
                    |> Option.map (Time.isWithinConstraints false >> getTimeLevel)
                    |> Option.defaultValue dto.Time.Level

                dto


            /// <summary>
            /// Create a Schedule Dto
            /// </summary>
            /// <param name="n">The name of the Schedule</param>
            /// <remarks>
            /// Defaults to a Discontinuous Schedule
            /// </remarks>
            let dto n =
                let dto = Dto()
                let f, t = n |> Name.fromString |> freqTime Unit.NoUnit Unit.NoUnit

                dto.Frequency <- f |> Frequency.toDto
                dto.Time <- t |> Time.toDto
                dto.IsDiscontinuous <- true

                dto


            /// Make the Schedule Dto Once
            let setToOnce (dto: Dto) =
                dto.IsOnce <- true
                dto.IsOnceTimed <- false
                dto.IsContinuous <- false
                dto.IsDiscontinuous <- false
                dto.IsTimed <- false

                dto


            /// Make the Schedule Dto OnceTimed
            let setToOnceTimed (dto: Dto) =
                dto.IsOnce <- false
                dto.IsOnceTimed <- true
                dto.IsContinuous <- false
                dto.IsDiscontinuous <- false
                dto.IsTimed <- false

                dto


            /// Make the Schedule Dto Continuous
            let setToContinuous (dto: Dto) =
                dto.IsOnce <- false
                dto.IsOnceTimed <- false
                dto.IsContinuous <- true
                dto.IsDiscontinuous <- false
                dto.IsTimed <- false

                dto


            /// Make the Schedule Dto Discontinuous
            let setToDiscontinuous (dto: Dto) =
                dto.IsOnce <- false
                dto.IsOnceTimed <- false
                dto.IsContinuous <- false
                dto.IsDiscontinuous <- true
                dto.IsTimed <- false

                dto


            /// Make the Schedule Dto Timed
            let setToTimed (dto: Dto) =
                dto.IsOnce <- false
                dto.IsOnceTimed <- false
                dto.IsContinuous <- false
                dto.IsDiscontinuous <- false
                dto.IsTimed <- true

                dto


    /// Types and functions that
    /// model a start and stop date time
    /// of an `Order`
    [<RequireQualifiedAccess>]
    module StartStop =


        /// Get the string representation of a `StartStop`
        let toString startStop =
            match startStop with
            | Start dt -> dt |> DateTime.formattedString "dd-MM-yy" |> sprintf "%s"
            | StartStop(start, stop) ->
                stop
                |> DateTime.formattedString "dd-MM-yy"
                |> sprintf "%s - %s" (start |> DateTime.formattedString "dd-MM-yy")


    [<RequireQualifiedAccess>]
    module OrderType =

        let toString =
            function
            | AnyOrder -> $"{AnyOrder}"
            | OnceOrder -> $"{OnceOrder}"
            | OnceTimedOrder -> $"{OnceTimedOrder}"
            | ProcessOrder -> $"{ProcessOrder}"
            | ContinuousOrder -> $"{ContinuousOrder}"
            | DiscontinuousOrder -> $"{DiscontinuousOrder}"
            | TimedOrder -> $"{TimedOrder}"

        let map s =
            match s with
            | _ when s = "eenmalig" -> OnceOrder
            | _ when s = "eenmalig inlooptijd" -> OnceTimedOrder
            | _ when s = "discontinu" -> DiscontinuousOrder
            | _ when s = "continu" -> ContinuousOrder
            | _ when s = "inlooptijd" -> TimedOrder
            | _ -> DiscontinuousOrder


    /// Apply `f` to `Order` `ord`
    let apply f (ord: Order) = ord |> f


    /// Utility function to facilitate type inference
    let inf = apply id


    module OrderPropertyChange =


        let applyToFrequency f ord =
            { (ord |> inf) with Schedule = ord.Schedule |> Schedule.applyToFrequency f }


        let applyToTime f ord =
            { (ord |> inf) with Schedule = ord.Schedule |> Schedule.applyToTime f }


        let applyToComponents c f ord =
            let cmpPred =
                fun cmp ->
                    c |> String.isNullOrWhiteSpace
                    || cmp |> Orderable.Component.getName |> Name.toString |> String.equalsCapInsens c

            { (ord |> inf) with Orderable = ord.Orderable |> Orderable.applyToComponents cmpPred f }


        let applyToItems c i f ord =
            let cmpPred =
                fun cmp ->
                    c |> String.isNullOrWhiteSpace
                    || cmp |> Orderable.Component.getName |> Name.toString |> String.equalsCapInsens c

            let itmPred =
                fun cmp ->
                    i |> String.isNullOrWhiteSpace
                    || cmp |> Orderable.Item.getName |> Name.toString |> String.equalsCapInsens i

            { (ord |> inf) with
                Orderable =
                    ord.Orderable
                    |> Orderable.applyToComponents cmpPred (Orderable.Component.applyToItems itmPred f)
            }


        let apply ord propChange =
            match propChange with
            | ScheduleFrequency f -> ord |> applyToFrequency f
            | ScheduleTime f -> ord |> applyToTime f
            | OrderAdjust f -> { ord with Adjust = ord.Adjust |> f }
            | OrderableQuantity f ->
                { (ord |> inf) with Order.Orderable.OrderableQuantity = ord.Orderable.OrderableQuantity |> f }
            | OrderableDoseCount f -> { (ord |> inf) with Order.Orderable.DoseCount = ord.Orderable.DoseCount |> f }
            | OrderableDose f -> { (ord |> inf) with Order.Orderable.Dose = ord.Orderable.Dose |> f }
            | ComponentQuantity(s, f) ->
                let f =
                    fun (cmp: Component) -> { cmp with ComponentQuantity = cmp.ComponentQuantity |> f }

                ord |> applyToComponents s f
            | ComponentOrderableQuantity(s, f) ->
                let f =
                    fun (cmp: Component) -> { cmp with OrderableQuantity = cmp.OrderableQuantity |> f }

                ord |> applyToComponents s f
            | ComponentOrderableCount(s, f) ->
                let f =
                    fun (cmp: Component) -> { cmp with OrderableCount = cmp.OrderableCount |> f }

                ord |> applyToComponents s f
            | ComponentOrderableConcentration(s, f) ->
                let f =
                    fun (cmp: Component) -> { cmp with OrderableConcentration = cmp.OrderableConcentration |> f }

                ord |> applyToComponents s f
            | ComponentDose(s, f) ->
                let f = fun (cmp: Component) -> { cmp with Dose = cmp.Dose |> f }
                ord |> applyToComponents s f
            | ItemComponentQuantity(s, i, f) ->
                let f =
                    fun (itm: Item) -> { itm with ComponentQuantity = itm.ComponentQuantity |> f }

                ord |> applyToItems s i f
            | ItemComponentConcentration(s, i, f) ->
                let f =
                    fun (itm: Item) -> { itm with ComponentConcentration = itm.ComponentConcentration |> f }

                ord |> applyToItems s i f
            | ItemOrderableQuantity(s, i, f) ->
                let f =
                    fun (itm: Item) -> { itm with OrderableQuantity = itm.OrderableQuantity |> f }

                ord |> applyToItems s i f
            | ItemOrderableConcentration(s, i, f) ->
                let f =
                    fun (itm: Item) -> { itm with OrderableConcentration = itm.OrderableConcentration |> f }

                ord |> applyToItems s i f
            | ItemDose(s, i, f) ->
                let f = fun (itm: Item) -> { itm with Dose = itm.Dose |> f }
                ord |> applyToItems s i f


        let proc propChanges ord = propChanges |> List.fold apply ord


    module Variable = Informedica.GenSolver.Lib.Variable
    module ValueRange = Variable.ValueRange
    module Equation = Informedica.GenSolver.Lib.Equation
    module Property = ValueRange.Property
    module Quantity = OrderVariable.Quantity
    module QuantityAdjust = OrderVariable.QuantityAdjust
    module Frequency = OrderVariable.Frequency
    module PerTime = OrderVariable.PerTime
    module PerTimeAdjust = OrderVariable.PerTimeAdjust
    module Concentration = OrderVariable.Concentration
    module Rate = OrderVariable.Rate
    module RateAdjust = OrderVariable.RateAdjust
    module Time = OrderVariable.Time
    module Units = ValueUnit.Units
    module Dose = Orderable.Dose


    type Equation = Informedica.GenSolver.Lib.Types.Equation


    /// Get the order id
    let getId ord = (ord |> inf).Id


    /// <summary>
    /// Create an `Order` with
    /// </summary>
    /// <param name="id">The id of the Order</param>
    /// <param name="adj_qty">The adjust quantity of the Order</param>
    /// <param name="orb">The Orderable of the Order</param>
    /// <param name="sch">The Schedule of the Order</param>
    /// <param name="rte">The Route of the Order</param>
    /// <param name="tme">The Time of the Order</param>
    /// <param name="sts">The StartStop of the Order</param>
    let create id adj_qty orb sch rte tme sts =
        {
            Id = id
            Adjust = adj_qty
            Orderable = orb
            Schedule = sch
            Route = rte
            Duration = tme
            StartStop = sts
        }


    /// <summary>
    /// Create a new `Order` with
    /// </summary>
    /// <param name="id">The id of the Order</param>
    /// <param name="orbN">The name of the Orderable</param>
    /// <param name="nmeToSch">A function to create a Schedule with a Name</param>
    /// <param name="route">The Route of the Order</param>
    let createNew id orbN nmeToSch route =
        let orb = Orderable.createNew id orbN
        let n = [ id ] |> Name.create

        let adj = Quantity.create (n |> Name.add Mapping.Literals.adj) Unit.NoUnit

        let tme = Time.create (n |> Name.add Mapping.Literals.ord) Unit.NoUnit

        let sch = n |> Name.add Mapping.Literals.sch |> nmeToSch

        let sts = DateTime.Now |> StartStop.Start

        create (id |> Id.create) adj orb sch route tme sts


    /// Get the Adjust quantity of an `Order`
    let getAdjust ord = (ord |> inf).Adjust


    /// Get the Orderable of an `Order`
    let getOrderable ord = (ord |> inf).Orderable


    /// <summary>
    /// Return an Order as a list of strings where each string is
    /// a variable name with the value range and the Unit
    /// </summary>
    let toString ord =
        [ (ord |> inf).Adjust |> Quantity.toString ]
        |> List.append ("Orderable" :: (ord.Orderable |> Orderable.toString))
        |> List.append ("Schedule" :: (ord.Schedule |> Schedule.toString))
        |> List.append ("Route" :: [ ord.Route ])
        |> List.filter (String.isNullOrWhiteSpace >> not)


    /// <summary>
    /// Return an Order as a list of strings where each string is
    /// a variable name with the value range and the Unit
    /// </summary>
    let toStringWithConstraints ord =
        [ (ord |> inf).Adjust |> Quantity.toStringWithConstraints ]
        |> List.append ("Orderable" :: (ord.Orderable |> Orderable.toStringWithConstraints))
        |> List.append ("Schedule" :: (ord.Schedule |> Schedule.toStringWithConstraints))
        |> List.append ("Route" :: [ ord.Route ])
        |> List.filter (String.isNullOrWhiteSpace >> not)


    let print ord =
        ord |> toStringWithConstraints |> String.concat "\n" |> writeDebugMessage

        ord


    let toConsoleTable ord =
        ord
        |> toStringWithConstraints
        |> List.map (fun s -> s |> String.replace "[" "" |> String.replace "]_" "|" |> String.split "|")
        |> List.filter (List.length >> (=) 4)
        |> List.map (fun xs ->
            let v = xs[1] |> String.split " "

            {|
                ``1 - NAME`` = xs[0] |> String.trim
                ``2 - VARIABLE`` = v[0] |> String.trim
                ``3 - VALUE`` = v[1..] |> String.concat " " |> String.trim |> String.replace "]" ""
                ``4 - CONSTRAINTS`` = xs[2] |> String.trim |> String.replace "]" ""
                ``5 - CALCULATED`` = xs[3] |> String.trim |> String.replace "]" ""
            |}
        )
        |> ConsoleTables.from


    let printTable format =
        toConsoleTable >> ConsoleTables.write format


    let toConsoleTableString = toConsoleTable >> ConsoleTables.toMarkDownString


    /// <summary>
    /// Return an Order as a list of OrderVariables
    /// </summary>
    let toOrdVars ord =
        let adj_qty = (ord |> inf).Adjust |> Quantity.toOrdVar
        let ord_tme = ord.Duration |> Time.toOrdVar

        let sch_vars =
            ord.Schedule |> Schedule.toOrdVars |> (fun (f, t) -> [ f; t ] |> List.choose id)

        [
            adj_qty
            ord_tme
            yield! sch_vars
            yield! ord.Orderable |> Orderable.toOrdVars
        ]


    /// <summary>
    /// Create a new Order from a list of OrderVariables using
    /// an old Order.
    /// </summary>
    /// <param name="ovars">The list of OrderVariables</param>
    /// <param name="ord">The old Order</param>
    let fromOrdVars ovars ord =
        { (ord |> inf) with
            Adjust = ord.Adjust |> Quantity.fromOrdVar ovars
            Duration = ord.Duration |> Time.fromOrdVar ovars
            Schedule = ord.Schedule |> Schedule.fromOrdVars ovars
            Orderable = ord.Orderable |> Orderable.fromOrdVars ovars
        }


    /// <summary>
    /// Apply constraints to an Order
    /// </summary>
    /// <param name="ord">The Order</param>
    let applyConstraints ord =
        { (ord |> inf) with
            Adjust = ord.Adjust |> Quantity.applyConstraints
            Duration = ord.Duration |> Time.applyConstraints
            Schedule = ord.Schedule |> Schedule.applyConstraints
            Orderable = ord.Orderable |> Orderable.applyConstraints
        }


    let applyCalculatedConstraints ord =
        ord
        |> toOrdVars
        |> List.map OrderVariable.applyCalculatedConstraints
        |> fun ovars -> ord |> fromOrdVars ovars


    /// Check whether all OrderVariables in an Order are empty
    let areAllConstraintsNotApplied =
        toOrdVars >> List.forall OrderVariable.isConstraintsNotApplied


    /// Check whether at least one OrderVariable in an Order has constraints
    let hasConstraints = toOrdVars >> List.exists OrderVariable.hasConstraints


    /// Check whether all OrderVariables in an Order are within their constraints
    let isWithinConstraints useCalc =
        toOrdVars >> List.forall (OrderVariable.isWithinConstraints useCalc)


    /// Check whether there are OrderVariables in an Order that are not within their constraints
    /// and return those OrderVariables
    let checkConstraints useCalc =
        toOrdVars >> List.filter (OrderVariable.isWithinConstraints useCalc >> not)


    let setCalculatedConstraints ord =
        ord
        |> toOrdVars
        |> List.map OrderVariable.setCalculatedConstraints
        |> fun ovars -> ord |> fromOrdVars ovars


    /// Check whether all OrderVariables in an Order are solved
    /// (i.e., have a single value within their constraints)
    /// and have constraints
    /// NOTE: an OrderVariable without constraints is considered solved
    let isSolved =
        toOrdVars
        >> List.filter OrderVariable.hasConstraints
        >> List.forall OrderVariable.isSolved


    let hasValues ord =
        match (ord |> inf).Schedule with
        | Continuous _ ->
            [
                // drip rate always has values or is solved
                // ord.Orderable.Dose.Rate |> Rate.toOrdVar

                // item dose rate
                yield!
                    ord.Orderable.Components
                    |> List.tryHead
                    |> Option.bind (_.Items >> List.tryHead)
                    |> Option.map (_.Dose.Rate >> Rate.toOrdVar >> List.singleton)
                    |> Option.defaultValue []
                // item orderable concentration
                yield!
                    ord.Orderable.Components
                    |> List.tryHead
                    |> Option.bind (_.Items >> List.tryHead)
                    |> Option.map (_.OrderableConcentration >> Concentration.toOrdVar >> List.singleton)
                    |> Option.defaultValue []
            ]
            |> List.exists OrderVariable.hasValues
        | Discontinuous _ ->
            ord
            |> toOrdVars
            // TODO flesh out exact filtering criteria
            |> List.filter OrderVariable.hasConstraints
            |> List.filter (fun ovar ->
                let n = ovar.Variable.Name |> Name.toString

                n |> String.contains "_cmp_qty" |> not
                && n |> String.contains "_cmp_cnc" |> not
                && n |> String.contains "_orb_cnt" |> not
                && n |> String.contains "_orb_cnc" |> not
            )
            |> List.exists OrderVariable.hasValues
        | Timed _ ->
            let ordVars =
                [
                    yield!
                        match ord.Schedule |> Schedule.toOrdVars with
                        | Some frq, Some tme -> [ frq; tme ]
                        | _ -> []
                    // leave out Orderable.Dose.Rate because drip rate always has values or is solved
                    ord.Orderable.Dose.PerTime |> PerTime.toOrdVar
                    ord.Orderable.Dose.Quantity |> Quantity.toOrdVar
                    // get the rest
                    yield! ord.Orderable.Components |> List.collect Orderable.Component.toOrdVars
                ]
                // TODO flesh out exact filtering criteria
                |> List.filter OrderVariable.hasConstraints
                |> List.filter (fun ovar ->
                    let n = ovar.Variable.Name |> Name.toString

                    n |> String.contains "_cmp_qty" |> not
                    && n |> String.contains "_cmp_cnc" |> not
                    && n |> String.contains "_orb_cnt" |> not
                    && n |> String.contains "_orb_cnc" |> not
                )

            if ordVars |> List.isEmpty then
                ord.Orderable.Dose.Rate |> Rate.toOrdVar |> OrderVariable.isSolved |> not
            else
                ordVars |> List.exists OrderVariable.hasValues
        | Once ->
            ord
            |> toOrdVars
            // TODO flesh out exact filtering criteria
            |> List.filter OrderVariable.hasConstraints
            |> List.filter (fun ovar ->
                let n = ovar.Variable.Name |> Name.toString

                n |> String.contains "_cmp_qty" |> not
                && n |> String.contains "_cmp_cnc" |> not
                && n |> String.contains "_orb_cnt" |> not
                && n |> String.contains "_orb_qty" |> not
                && n |> String.contains "_orb_cnc" |> not
            )
            |> List.exists OrderVariable.hasValues
        | OnceTimed _ ->
            let ordVars =
                [
                    yield!
                        match ord.Schedule |> Schedule.toOrdVars with
                        | _, Some tme -> [ tme ]
                        | _ -> []
                    // leave out Orderable.Dose.Rate because drip rate always has values or is solved
                    ord.Orderable.Dose.PerTime |> PerTime.toOrdVar
                    ord.Orderable.Dose.Quantity |> Quantity.toOrdVar
                    // get the rest
                    yield! ord.Orderable.Components |> List.collect Orderable.Component.toOrdVars
                ]
                // TODO flesh out exact filtering criteria
                |> List.filter OrderVariable.hasConstraints
                |> List.filter (fun ovar ->
                    let n = ovar.Variable.Name |> Name.toString

                    n |> String.contains "_cmp_qty" |> not
                    && n |> String.contains "_cmp_cnc" |> not
                    && n |> String.contains "_orb_cnt" |> not
                    && n |> String.contains "_orb_cnc" |> not
                )

            if ordVars |> List.isEmpty then
                ord.Orderable.Dose.Rate |> Rate.toOrdVar |> OrderVariable.isSolved |> not
            else
                ordVars |> List.exists OrderVariable.hasValues


    let checkOrderDose pred op ord : bool =
        let checkRte rte = rte |> Rate.toOrdVar |> pred
        let checkQty qty = qty |> Quantity.toOrdVar |> pred
        let checkPtm ptm = ptm |> PerTime.toOrdVar |> pred

        let rates =
            [
                if ord.Orderable.Dose.Rate |> Rate.isCleared |> not then
                    ord.Orderable.Dose.Rate

                // timed order doesn't calc itm dose rates!
                if ord.Schedule.IsContinuous then
                    // only look at item dose rates
                    yield!
                        ord.Orderable.Components
                        |> List.collect _.Items
                        |> List.map _.Dose
                        |> List.filter (fun d ->
                            d.Rate |> Rate.isNonZeroPositive |> not
                            && d.Rate |> Rate.isCleared |> not
                            && (d.Rate |> Rate.hasConstraints || d.RateAdjust |> RateAdjust.hasConstraints)
                        )
                        |> List.map _.Rate
            ]

        let qty =
            [
                ord.Orderable.Dose.Quantity
                for cmp in ord.Orderable.Components do
                    cmp.Dose.Quantity

                    for itm in cmp.Items do
                        if
                            itm.Dose.Quantity |> Quantity.isNonZeroPositive |> not
                            && itm.Dose.Quantity |> Quantity.isCleared |> not
                            && (itm.Dose.Quantity |> Quantity.hasConstraints
                                || itm.Dose.QuantityAdjust |> QuantityAdjust.hasConstraints)
                        then
                            itm.Dose.Quantity
            ]

        let ptm =
            [
                ord.Orderable.Dose.PerTime
                for cmp in ord.Orderable.Components do
                    cmp.Dose.PerTime

                    for itm in cmp.Items do
                        if
                            itm.Dose.PerTime |> PerTime.isNonZeroPositive |> not
                            && itm.Dose.PerTime |> PerTime.isCleared |> not
                            && itm.Dose.PerTimeAdjust |> PerTimeAdjust.isCleared |> not
                            && (itm.Dose.PerTime |> PerTime.hasConstraints
                                || itm.Dose.PerTimeAdjust |> PerTimeAdjust.hasConstraints)
                        then
                            itm.Dose.PerTime
            ]

        let isOr = true |> op <| false

        match ord.Schedule with
        | Continuous tme ->
            tme |> Time.toOrdVar |> pred
            && if isOr then
                   rates |> List.exists checkRte
               else
                   rates |> List.forall checkRte
        | Timed(freq, tme) ->
            tme |> Time.toOrdVar |> pred
            && (if isOr then
                    rates |> List.exists checkRte
                else
                    rates |> List.forall checkRte)
            && (freq |> Frequency.isCleared || freq |> Frequency.toOrdVar |> pred)
            && (if isOr then
                    qty |> List.exists checkQty
                else
                    qty |> List.forall checkQty)
               |> op
               <| if isOr then
                      ptm |> List.exists checkPtm
                  else
                      ptm |> List.forall checkPtm
        | Discontinuous freq ->
            (freq |> Frequency.isCleared || freq |> Frequency.toOrdVar |> pred)
            && (if isOr then
                    qty |> List.exists checkQty
                else
                    qty |> List.forall checkQty)
               |> op
               <| if isOr then
                      ptm |> List.exists checkPtm
                  else
                      ptm |> List.forall checkPtm
        | Once -> qty |> List.forall checkQty
        | OnceTimed tme ->
            tme |> Time.toOrdVar |> pred
            && (if isOr then
                    rates |> List.exists checkRte
                else
                    rates |> List.forall checkRte)
            && if isOr then
                   qty |> List.exists checkQty
               else
                   qty |> List.forall checkQty


    let doseIsSolved = checkOrderDose OrderVariable.isSolved (&&)


    let doseHasValues = checkOrderDose OrderVariable.hasValues (||)


    /// <summary>
    /// Increase the Quantity increment of an Order to a maximum
    /// count using a list of increments.
    /// </summary>
    /// <param name="maxCount">The maximum count</param>
    /// <param name="incrs">The list of increments</param>
    /// <param name="ord">The Order</param>
    let increaseQuantityIncrement maxCount incrs ord =
        { (ord |> inf) with Orderable = ord.Orderable |> Orderable.increaseQuantityIncrement maxCount incrs }


    /// <summary>
    /// Increase the Rate increment of an Order to a maximum
    /// count using a list of increments.
    /// </summary>
    /// <param name="maxCount">The maximum count</param>
    /// <param name="incrs">The list of increments</param>
    /// <param name="ord">The Order</param>
    let increaseRateIncrement maxCount incrs ord =
        { (ord |> inf) with Orderable = ord.Orderable |> Orderable.increaseRateIncrement maxCount incrs }


    let hasNormDose ord =
        if ord.Orderable.Components |> List.length > 2 then
            false
        else
            match ord.Schedule with
            | Once
            | OnceTimed _ ->
                (ord.Orderable.Dose.QuantityAdjust |> QuantityAdjust.hasNormValue, ord.Orderable.Components)
                ||> List.fold (fun acc cmp ->
                    if acc then
                        true
                    else
                        (cmp.Dose.QuantityAdjust |> QuantityAdjust.hasNormValue, cmp.Items)
                        ||> List.fold (fun acc itm ->
                            if acc then
                                true
                            else
                                itm.Dose.QuantityAdjust |> QuantityAdjust.hasNormValue
                        )
                )
            | Discontinuous _
            | Timed _ ->
                (ord.Orderable.Dose.QuantityAdjust |> QuantityAdjust.hasNormValue
                 || ord.Orderable.Dose.PerTimeAdjust |> PerTimeAdjust.hasNormValue,
                 ord.Orderable.Components)
                ||> List.fold (fun acc cmp ->
                    if acc then
                        true
                    else
                        (cmp.Dose.QuantityAdjust |> QuantityAdjust.hasNormValue
                         || cmp.Dose.PerTimeAdjust |> PerTimeAdjust.hasNormValue,
                         cmp.Items)
                        ||> List.fold (fun acc itm ->
                            if acc then
                                true
                            else
                                itm.Dose.QuantityAdjust |> QuantityAdjust.hasNormValue
                                || itm.Dose.PerTimeAdjust |> PerTimeAdjust.hasNormValue
                        )
                )
            | Continuous _ ->
                (ord.Orderable.Dose.RateAdjust |> RateAdjust.hasNormValue, ord.Orderable.Components)
                ||> List.fold (fun acc cmp ->
                    if acc then
                        true
                    else
                        (cmp.Dose.RateAdjust |> RateAdjust.hasNormValue, cmp.Items)
                        ||> List.fold (fun acc itm ->
                            if acc then
                                true
                            else
                                itm.Dose.RateAdjust |> RateAdjust.hasNormValue
                        )
                )


    let setMedianDoseValue ord =
        match ord.Schedule with
        | Once
        | OnceTimed _ ->
            { ord with Order.Orderable.Dose.Quantity = ord.Orderable.Dose.Quantity |> Quantity.setMedianValue }
        | Discontinuous frq
        | Timed(frq, _) ->
            if frq |> Frequency.isSolved |> not then
                ord
            else
                { ord with Order.Orderable.Dose.Quantity = ord.Orderable.Dose.Quantity |> Quantity.setMedianValue }
        | Continuous _ -> { ord with Order.Orderable.Dose.Rate = ord.Orderable.Dose.Rate |> Rate.setMedianValue }


    /// <summary>
    /// Map an Order to a list of Equations using a Product Equation
    /// mapping and a Sum Equation mapping
    /// </summary>
    /// <param name="eqMapping">The Product Equation mapping and the Sum Equation mapping</param>
    /// <param name="ord">The Order</param>
    /// <returns>A list of OrderEquations</returns>
    let mapToOrderEquations eqMapping ord =
        let ovars = ord |> toOrdVars

        let map repl eqMapping =
            let eqs, c =
                match eqMapping with
                | SumMapping eqs -> eqs, OrderSumEquation
                | ProductMapping eqs -> eqs, OrderProductEquation

            eqs
            |> List.map (String.replace "=" repl)
            |> List.map (String.split repl >> List.map String.trim >> List.filter String.notEmpty)
            |> List.map (fun xs ->
                match xs with
                | h :: rest ->
                    let h =
                        match ovars |> List.tryFind (fun v -> v.Variable.Name |> Name.toString = h) with
                        | Some v -> v
                        | None ->
                            let h =
                                if h |> String.isNullOrWhiteSpace then
                                    "'empty string'"
                                else
                                    h

                            raise (
                                System.Collections.Generic.KeyNotFoundException
                                    $"cannot find {h} from\n{eqMapping}\nin {ovars |> OrderVariable.getNames}"
                            )

                    let rest =
                        rest
                        |> List.map (fun s ->
                            match ovars |> List.tryFind (fun v -> v.Variable.Name |> Name.toString = s) with
                            | Some v -> v
                            | None ->
                                let s =
                                    if s |> String.isNullOrWhiteSpace then
                                        "'empty string'"
                                    else
                                        s

                                raise (
                                    System.Collections.Generic.KeyNotFoundException
                                        $"cannot find {s} from\n{eqMapping}\nin {ovars |> OrderVariable.getNames}"
                                )
                        )
                        |> fun rest ->
                            if repl <> "+" then
                                rest
                            else
                                rest |> List.filter (OrderVariable.eqsUnitGroup h)

                    (h, rest) |> c
                | _ -> invalidOp $"cannot map {eqs}"
            )

        let sumEqs, prodEqs = eqMapping

        sumEqs |> map "+" |> List.append (prodEqs |> map "*")


    /// <summary>
    /// Map a list of OrderEquations to an Order
    /// </summary>
    /// <param name="ord">The Order</param>
    /// <param name="eqs">The list of OrderEquations</param>
    let mapFromOrderEquations ord eqs =
        let ovars =
            eqs
            |> List.collect (fun e ->
                match e with
                | OrderProductEquation(y, xs)
                | OrderSumEquation(y, xs) -> y :: xs
            )
            |> List.distinct
            |> List.map OrderVariable.setUnit

        ord |> fromOrdVars ovars


    let logOrder logger minMax msg ord =
        // Build the (expensive) console table lazily: toConsoleTableString drives
        // List.sortBy / ConsoleTables / Wcwidth / heavy allocation, so it runs
        // only when the logger would actually consume an informative message.
        Logging.logInfoLazy
            logger
            (fun () ->
                let s = ord |> toConsoleTableString
                let m = if minMax then "Min/Max" else "All Values"

                $"""
== {msg} {m} ==
{s}
== End of Table ==
"""
                |> Events.OrderScenario
            )


    /// <summary>
    /// Solve an Order
    /// </summary>
    /// <param name="msg">A message for logging</param>
    /// <param name="minMax">Whether to solve only for the minimum or maximum</param>
    /// <param name="printErr">Whether to print the error</param>
    /// <param name="logger">The logger</param>
    /// <param name="ord">The Order</param>
    /// <returns>A Result with the Order or a list error messages</returns>
    /// <raises>Any exception raised by the solver</raises>
    let rec solve msg minMax printErr logger (ord: Order) =
        let logOrder = logOrder logger minMax
        ord |> logOrder msg

        let harmonize ord =
            ord.Orderable
            |> Orderable.harmonizeItemConcentrations logger
            |> function
                | false, _ -> ord |> Ok
                | true, orb -> { ord with Order.Orderable = orb } |> solve "Harmonize" minMax printErr logger

        let mapping =
            match ord.Schedule with
            | Once -> Mapping.Literals.once
            | Continuous _ -> Mapping.Literals.continuous
            | OnceTimed _ -> Mapping.Literals.onceTimed
            | Discontinuous _ -> Mapping.Literals.discontinuous
            | Timed _ -> Mapping.Literals.timed
            |> Mapping.getEquations
            |> Mapping.getEqsMapping ord

        let oEqs = ord |> mapToOrderEquations mapping

        try
            oEqs
            |> Solver.mapToSolverEqs
            |> fun eqs ->
                if minMax then
                    eqs |> Solver.solveMinMax logger
                else
                    eqs |> Solver.solve logger
            |> function
                | Ok eqs -> eqs |> Solver.mapToOrderEqs oEqs |> mapFromOrderEquations ord |> harmonize
                | Error(eqs, m) ->
                    eqs
                    |> Solver.mapToOrderEqs oEqs
                    |> mapFromOrderEquations ord
                    |> fun ord ->
                        if printErr then
                            writeDebugMessage $"Solve errored with: {m}"
                            ord |> toString |> List.mapi (sprintf "%i. %s") |> List.iter writeDebugMessage

                        Error(ord, m)
        with exn ->
            if printErr then
                writeDebugMessage $"Solve errored with: {exn.Message}"

                oEqs
                |> mapFromOrderEquations ord
                |> toString
                |> List.mapi (sprintf "%i. %s")
                |> List.iter writeDebugMessage

            let msg =
                [
                    exn |> Informedica.GenSolver.Lib.Types.Exceptions.UnexpectedException
                ]

            Error(ord, msg)
        |> function
            | Ok ord ->
                ord |> logOrder "Finished Solving"
                ord |> Ok
            | Error(ord, msgs) ->
                let msg = $"Finished with {msgs |> List.length} Errors"
                ord |> logOrder msg
                (ord, msgs) |> Error


    /// <summary>
    /// Solve an Order for only the minimum and maximum values
    /// </summary>
    /// <param name="msg">A message for logging</param>
    /// <param name="printErr">Whether to print the error</param>
    /// <param name="logger">The logger</param>
    let solveMinMax msg printErr logger = solve msg true printErr logger


    /// <summary>
    /// Solve an Order for all values
    /// </summary>
    /// <param name="msg">A message for logging</param>
    /// <param name="printErr">Whether to print the error</param>
    /// <param name="logger">The logger</param>
    let solveOrder msg printErr logger = solve msg false printErr logger


    /// <summary>
    /// Increase the Orderable Quantity Increment of an Order.
    /// This allows speedy calculation by avoiding large amount
    /// of possible values.
    /// </summary>
    /// <param name="logger">The OrderLogger to use</param>
    /// <param name="maxQtyCount">The maximum count of the Orderable Quantity</param>
    /// <param name="maxRateCount">The maximum count of the Rate</param>
    /// <param name="ord">The Order to increase the increment of</param>
    let increaseIncrements logger maxQtyCount maxRateCount ord =
        let maxQtyCount = maxQtyCount |> BigRational.fromInt
        let maxRateCount = maxRateCount |> BigRational.fromInt

        if (ord |> inf).Schedule |> Schedule.isContinuous then
            ord
        else
            let orbQty = ord.Orderable.OrderableQuantity |> Quantity.toOrdVar
            // the increments used to increase
            // TODO: use increment from constraint
            let incrs u =
                [ 1N / 10N; 1N / 2N; 1N; 5N; 10N; 20N ]
                |> List.map (ValueUnit.singleWithUnit u)
                |> List.map ValueRange.Increment.create
            // only increase incr for volume units
            if
                orbQty.Variable
                |> Variable.getUnit
                |> Option.map (ValueUnit.Group.unitToGroup >> (=) Group.VolumeGroup >> not)
                |> Option.defaultValue false
            then
                ord
            else
                let incrOrd =
                    ord |> increaseQuantityIncrement maxQtyCount (incrs Units.Volume.milliLiter)

                if ord = incrOrd then
                    Ok ord
                else
                    incrOrd |> Events.OrderIncreaseQuantityIncrement |> Logging.logInfo logger

                    incrOrd |> solveMinMax "Increase Quantity Increment" false logger
                |> function
                    | Error(_, errs) ->
                        writeDebugMessage "Could not increase orderable quantity increment:"
                        errs |> List.iter (fun e -> writeDebugMessage $"{e}")
                        ord // original order
                    | Ok ord ->
                        let incrOrd =
                            ord // increased increment order
                            |> increaseRateIncrement
                                maxRateCount
                                (incrs (Units.Volume.milliLiter |> ValueUnit.per Units.Time.hour))

                        if incrOrd = ord then
                            Ok ord
                        else
                            incrOrd |> Events.OrderIncreaseRateIncrement |> Logging.logInfo logger

                            incrOrd |> solveMinMax "Increase Rate Increment" false logger

                        |> function
                            | Error(_, errs) ->
                                writeDebugMessage "Could not increase orderable rate increment:"
                                errs |> List.iter (fun e -> writeDebugMessage $"{e}")
                                ord // increased increment order
                            | Ok ord -> ord // increased increment and rate order
        |> Ok


    let maximizeRate logger ord =
        // only maximize for timed orders
        if (ord |> inf).Schedule |> Schedule.isTimed |> not then
            ord
        else
            match ord.Orderable.Dose.Rate |> Rate.toOrdVar with
            | rte when rte |> OrderVariable.isSolved |> not ->
                let maxRte =
                    rte |> OrderVariable.minIncrMaxToValues (Some 100) |> OrderVariable.setMaxValue

                let ovars =
                    ord
                    |> toOrdVars
                    // first check if max rate can be set
                    |> List.map (fun ovar ->
                        if ovar.Variable.Name = rte.Variable.Name then
                            maxRte
                        else
                            ovar
                    )

                ord
                |> fromOrdVars ovars
                |> solveOrder "Maximize Rate" false logger
                |> Result.map (fun ord ->
                    writeDebugMessage $"max rate set to: {maxRte |> OrderVariable.toString true}"
                    ord
                )
                |> Result.defaultValue ord
            | _ -> ord


    let minimizeTime logger (ord: Order) =
        if ord.Schedule |> Schedule.isTimed |> not then
            ord
        else
            { (ord |> inf) with Schedule = ord.Schedule |> Schedule.setMinTime }
            |> solveOrder "Minimize Time" false logger
            |> Result.defaultValue ord


    /// <summary>
    /// Loop through all the OrderVariables in an Order to
    /// turn min incr max to values
    /// </summary>
    /// <param name="useMaxNumberOfValues">Whether to use max number of values or restrict the number of values</param>
    /// <param name="minTime">Whether to minimize the time before processing</param>
    /// <param name="skipRate">Whether to skip the orderable dose rate variable (for staged expansion of timed orders)</param>
    /// <param name="logger">The logger</param>
    /// <param name="ord">The Order</param>
    let minIncrMaxToValues useMaxNumberOfValues minTime skipRate logger ord =
        let mutable isSolved = false

        let rec loop ord =
            // the flag makes sure that if one order variable
            // is set to values, then the rest is skipped and
            // proceed to solving the order
            let mutable flag = false

            let ovars =
                // only use the natural incremented variables
                // - component orderable quantities
                // - orderable dose quantity
                // - orderable dose rate (unless skipRate is true)
                [
                    yield! ord.Orderable.Components |> List.map (_.OrderableQuantity >> Quantity.toOrdVar)
                    ord.Orderable.Dose.Quantity |> Quantity.toOrdVar
                    if not skipRate then
                        ord.Orderable.Dose.Rate |> Rate.toOrdVar
                ]
                |> List.map (fun ovar ->
                    if
                        flag
                        || ovar.DefinedConstraints.Incr |> Option.isNone
                        || ovar.Variable |> Variable.isMinIncrMax |> not
                    then
                        ovar
                    else
                        flag <- true

                        let n =
                            match ord.Schedule with
                            | Continuous _ -> if useMaxNumberOfValues then 1_000 else 100
                            | Once
                            | Discontinuous _ ->
                                if useMaxNumberOfValues then
                                    if ord.Orderable.Components |> List.length > 1 then
                                        20
                                    else
                                        100
                                else
                                    10
                            | OnceTimed _
                            | Timed _ ->
                                if useMaxNumberOfValues then
                                    if ord.Orderable.Components |> List.length > 2 then
                                        5
                                    else
                                        10
                                else if ord.Orderable.Components |> List.length > 2 then
                                    5
                                else
                                    20
                            |> Some

                        ovar
                        |> OrderVariable.minIncrMaxToValues n
                        |> fun ovar ->
                            Events.MinIncrMaxToValues ovar |> Logging.logInfo logger
                            ovar
                )

            if not flag then
                ord
            else
                ord
                |> fromOrdVars ovars
                |> solveOrder "Min Incr Max to Values" true logger // could possibly restrict to solve variable
                |> function
                    | Ok ord ->
                        isSolved <- true
                        loop ord
                    | Error err ->
                        err |> snd |> List.map (sprintf "%A") |> String.concat "\n" |> writeErrorMessage

                        ord

        if minTime then ord |> minimizeTime logger else ord
        |> loop
        |> fun ord ->
            // make sure that an order is solved at least once
            if not isSolved then
                ord
                |> solveOrder "Min Incr Max to Values final Loop" true logger
                |> Result.defaultValue ord
            else
                ord


    let solveNormDose logger ord =
        let normDoseOrd = ord |> setMedianDoseValue

        if normDoseOrd = ord then
            ord |> Ok
        else
            normDoseOrd |> solveMinMax "Solve Normal Dose" false logger


    let setDoseUnit sn du ord =
        { (ord |> inf) with Orderable = ord.Orderable |> Orderable.setDoseUnit sn du }


    let isCleared = toOrdVars >> List.exists OrderVariable.isCleared


    let calcMinMax logger = solveMinMax "Calc Min Max" true logger


    let pickNearestHigherElseLowerComponentQuantity logger ord =
        let newOrd =
            { (ord |> inf) with
                Order.Orderable.Components =
                    ord.Orderable.Components
                    |> List.map Orderable.Component.pickNearestHigherElseLower
            }

        if newOrd = ord then
            ord |> Ok
        else
            newOrd |> solve "pick nearest cmp quantity" false false logger


    module Print =

        open Informedica.GenOrder.Lib


        let wrap tb ovars s =
            if ovars |> List.forall (OrderVariable.isWithinConstraints false) then
                s |> Valid
            else
                s |> tb


        let unwrap tb =
            match tb with
            | Valid s
            | Caution s
            | Warning s
            | Alert s -> s


        let textBlockIsEmpty (tb: TextBlock) =
            match tb with
            | Valid s
            | Caution s
            | Warning s
            | Alert s -> s |> String.isNullOrWhiteSpace


        let textBlockWithParens tb =
            match tb with
            | Valid s
            | Caution s
            | Warning s
            | Alert s ->
                if s |> String.isNullOrWhiteSpace then
                    tb
                else
                    let s = $"({s})"

                    match tb with
                    | Valid _ -> s |> Valid
                    | Caution _ -> s |> Caution
                    | Warning _ -> s |> Warning
                    | Alert _ -> s |> Alert


        let printPrep printMd sns (ord: Order) =
            let hasCompQty =
                // only print component quantity when relevant, i.e.,
                // there is a component quantity > 1
                ord.Orderable.Components
                |> List.tryHead
                |> Option.bind (fun cmp ->
                    cmp.ComponentQuantity
                    |> Quantity.toOrdVar
                    |> OrderVariable.getValSetValueUnit
                    |> Option.bind (fun vu ->
                        vu
                        |> ValueUnit.getValue
                        |> Array.tryExactlyOne
                        |> Option.map (fun br -> br > 1N)
                    )
                )
                |> Option.defaultValue false

            ord.Orderable.Components
            |> List.toArray
            |> Array.mapi (fun i1 c ->
                let cmpQty =
                    c
                    |> Orderable.Component.Print.componentOrderableQuantityTo printMd -1
                    |> wrap
                        Caution
                        [
                            yield! c.Items |> List.map (_.OrderableConcentration >> Concentration.toOrdVar)
                            c.OrderableConcentration |> Concentration.toOrdVar
                            c.OrderableQuantity |> Quantity.toOrdVar
                        ]

                let cItms =
                    c.Items
                    |> List.filter (fun i -> sns |> Array.exists (String.equalsCapInsens (i.Name |> Name.toString)))
                    |> List.toArray

                [|
                    if i1 > 0 || cItms |> Array.isEmpty then
                        [|
                            [|
                                if cmpQty |> textBlockIsEmpty |> not then
                                    if i1 = 0 && cItms |> Array.isEmpty |> not then
                                        c.Form |> Valid
                                    else
                                        $"{c.Form} ({c.Name |> Name.toString})" |> Valid

                                    cmpQty
                                    "" |> Valid
                                    "" |> Valid
                            |]
                        |]
                    else
                        cItms
                        |> Array.mapi (fun i2 itm ->
                            [|
                                if cmpQty |> textBlockIsEmpty |> not then
                                    if i1 = 0 && i2 = 0 then
                                        c.Form |> Valid

                                        c
                                        |> Orderable.Component.Print.componentOrderableQuantityTo printMd -1
                                        |> wrap
                                            Warning
                                            [
                                                itm.OrderableConcentration |> Concentration.toOrdVar
                                                c.OrderableQuantity |> Quantity.toOrdVar
                                            ]
                                    else
                                        "" |> Valid
                                        "" |> Valid

                                    let itmConc =
                                        itm
                                        |> Orderable.Item.Print.itemComponentConcentrationTo printMd -1
                                        |> wrap Warning [ itm.ComponentConcentration |> Concentration.toOrdVar ]

                                    if itmConc |> textBlockIsEmpty |> not then
                                        itm.Name |> Name.toString |> Valid
                                        itmConc

                                    if hasCompQty then
                                        let itmQty = itm.ComponentQuantity |> Quantity.toValueUnitString -1
                                        let cmpQty = c.ComponentQuantity |> Quantity.toValueUnitString -1
                                        $"({itmQty}/{cmpQty})" |> Valid

                            |]
                        )
                |]
            )
            |> Array.collect id
            |> Array.collect id


        let printOrderToTableFormat useAdj printMd sns ord =

            let findItem sn =
                ord.Orderable.Components
                |> List.collect _.Items
                |> List.tryFind (fun i -> i.Name |> Name.toString |> String.equalsCapInsens sn)

            let itms = sns |> Array.filter String.notEmpty |> Array.choose findItem

            let cmpName =
                ord.Orderable.Components
                |> List.tryHead
                |> Option.map (_.Name >> Name.toString)
                |> Option.defaultValue ""

            let withParens s =
                if s |> String.isNullOrWhiteSpace then s else $"({s})"

            let addPerDosis s =
                if s |> String.isNullOrWhiteSpace then s else $"{s}/dosis"

            let freq =
                let tb =
                    if
                        ord.Schedule
                        |> Schedule.getFrequency
                        |> Option.map (Frequency.toOrdVar >> OrderVariable.isWithinConstraints false)
                        |> Option.defaultValue true
                    then
                        Valid
                    else
                        Warning

                ord.Schedule |> Schedule.Print.frequencyTo printMd |> tb

            let tme =
                let tb =
                    if
                        ord.Schedule
                        |> Schedule.getTime
                        |> Option.map (Time.toOrdVar >> OrderVariable.isWithinConstraints false)
                        |> Option.defaultValue true
                    then
                        Valid
                    else
                        Caution

                ord.Schedule |> Schedule.Print.timeToString 2 |> tb

            let pres =
                match ord.Schedule with
                | Continuous _ ->
                    if itms |> Array.isEmpty then
                        [|
                            [|
                                if cmpName |> String.notEmpty then
                                    cmpName |> Valid

                                // the orderable dose quantity
                                ord.Orderable
                                |> Orderable.Print.doseQuantityTo printMd -1
                                |> wrap
                                    Alert
                                    [
                                        ord.Orderable.Dose.Quantity |> Quantity.toOrdVar
                                        ord.Orderable.Dose.QuantityAdjust |> QuantityAdjust.toOrdVar
                                    ]

                                // the orderable dose adjust quantity
                                if useAdj then
                                    ord.Orderable
                                    |> Orderable.Print.doseQuantityAdjustTo printMd 2
                                    |> wrap
                                        Alert
                                        [
                                            ord.Orderable.Dose.Quantity |> Quantity.toOrdVar
                                            ord.Orderable.Dose.QuantityAdjust |> QuantityAdjust.toOrdVar
                                        ]

                                    // orderable dose rate adjust
                                    ord.Orderable
                                    |> Orderable.Print.doseRateAdjustTo printMd 3
                                    |> wrap
                                        Alert
                                        [
                                            ord.Orderable.Dose.Rate |> Rate.toOrdVar
                                            ord.Orderable.Dose.RateAdjust |> RateAdjust.toOrdVar
                                        ]

                                    if ord.Orderable.Dose.RateAdjust |> RateAdjust.isSolved then
                                        ord.Orderable.Dose
                                        |> Dose.Print.doseRateAdjustConstraints 3
                                        |> withParens
                                        |> Valid
                                else
                                    // orderable dose rate
                                    ord.Orderable
                                    |> Orderable.Print.doseRateTo printMd -1
                                    |> wrap Alert [ ord.Orderable.Dose.Rate |> Rate.toOrdVar ]

                                    if ord.Orderable.Dose.Rate |> Rate.isSolved then
                                        ord.Orderable.Dose |> Dose.Print.doseRateConstraints 3 |> withParens |> Valid
                            |]
                        |]
                    else
                        itms
                        |> Array.map (fun itm ->
                            [|
                                itm.Name |> Name.toString |> Valid

                                // item dose per rate
                                if useAdj then
                                    itm
                                    |> Orderable.Item.Print.itemDoseRateAdjustTo printMd 3
                                    |> wrap
                                        Alert
                                        [
                                            itm.Dose.Rate |> Rate.toOrdVar
                                            itm.Dose.RateAdjust |> RateAdjust.toOrdVar
                                        ]

                                    if itm.Dose.RateAdjust |> RateAdjust.isSolved then
                                        itm.Dose |> Dose.Print.doseRateAdjustConstraints 3 |> withParens |> Valid
                                else
                                    itm
                                    |> Orderable.Item.Print.itemDoseRateTo printMd 3
                                    |> wrap Alert [ itm.Dose.Rate |> Rate.toOrdVar ]

                                    if itm.Dose.Rate |> Rate.isSolved then
                                        itm.Dose |> Dose.Print.doseRateConstraints 3 |> withParens |> Valid
                            |]
                        )

                | Discontinuous _
                | Timed _ ->
                    if itms |> Array.isEmpty then
                        [|
                            [|
                                // the frequency
                                freq

                                // the orderable dose quantity
                                ord.Orderable
                                |> Orderable.Print.doseQuantityTo printMd -1
                                |> wrap
                                    Alert
                                    [
                                        ord.Orderable.Dose.Quantity |> Quantity.toOrdVar
                                        ord.Orderable.Dose.QuantityAdjust |> QuantityAdjust.toOrdVar
                                        ord.Orderable.Dose.PerTimeAdjust |> PerTimeAdjust.toOrdVar
                                    ]

                                // the orderable dose adjust quantity
                                if useAdj then
                                    let tb =
                                        ord.Orderable
                                        |> Orderable.Print.dosePerTimeAdjustTo printMd 2
                                        |> wrap
                                            Alert
                                            [
                                                ord.Orderable.Dose.PerTime |> PerTime.toOrdVar
                                                ord.Orderable.Dose.PerTimeAdjust |> PerTimeAdjust.toOrdVar
                                            ]

                                    if tb |> textBlockIsEmpty |> not then
                                        "=" |> Valid
                                        tb

                                    if ord.Orderable.Dose.PerTimeAdjust |> PerTimeAdjust.isSolved then
                                        [
                                            ord.Orderable.Dose
                                            |> Dose.Print.dosePerTimeAdjustConstraints 3
                                            |> withParens
                                            ord.Orderable.Dose
                                            |> Dose.Print.doseQuantityAdjustConstraints 3
                                            |> addPerDosis
                                            |> withParens
                                        ]
                                        |> List.tryFind String.notEmpty
                                        |> Option.defaultValue ""
                                        |> Valid
                                else
                                    let tb =
                                        ord.Orderable
                                        |> Orderable.Print.dosePerTimeTo printMd -1
                                        |> wrap Alert [ ord.Orderable.Dose.PerTime |> PerTime.toOrdVar ]

                                    if tb |> textBlockIsEmpty |> not then
                                        "=" |> Valid
                                        tb

                                    if ord.Orderable.Dose.PerTime |> PerTime.isSolved then
                                        [
                                            ord.Orderable.Dose |> Dose.Print.dosePerTimeConstraints 3 |> withParens
                                            ord.Orderable.Dose
                                            |> Dose.Print.doseQuantityConstraints 3
                                            |> addPerDosis
                                            |> withParens
                                        ]
                                        |> List.tryFind String.notEmpty
                                        |> Option.defaultValue ""
                                        |> Valid
                            |]
                        |]
                    else
                        itms
                        |> Array.mapi (fun i itm ->
                            // check whether the constraints are
                            // per dose quantity or per doser per time
                            let isPerDose = itm.Dose.QuantityAdjust |> QuantityAdjust.hasConstraints

                            [|
                                // the frequency
                                if i = 0 then freq else "" |> Valid

                                // the name of the item
                                itm.Name |> Name.toString |> Valid

                                // the item dose quantity
                                itm
                                |> Orderable.Item.Print.itemDoseQuantityTo printMd 3
                                |> wrap Alert [ itm.Dose.Quantity |> Quantity.toOrdVar ]

                                if useAdj then
                                    let tb =
                                        if isPerDose then
                                            itm
                                            |> Orderable.Item.Print.itemDoseQuantityAdjustTo printMd 3
                                            |> wrap
                                                Alert
                                                [
                                                    itm.Dose.Quantity |> Quantity.toOrdVar
                                                    itm.Dose.QuantityAdjust |> QuantityAdjust.toOrdVar
                                                ]
                                        else
                                            itm
                                            |> Orderable.Item.Print.itemDosePerTimeAdjustTo printMd 3
                                            |> wrap
                                                Alert
                                                [
                                                    itm.Dose.PerTime |> PerTime.toOrdVar
                                                    itm.Dose.PerTimeAdjust |> PerTimeAdjust.toOrdVar
                                                ]

                                    if tb |> textBlockIsEmpty |> not then
                                        "=" |> Valid
                                        tb

                                    if itm.Dose.PerTimeAdjust |> PerTimeAdjust.isSolved then
                                        [
                                            itm.Dose |> Dose.Print.dosePerTimeAdjustConstraints 3 |> withParens
                                            itm.Dose
                                            |> Dose.Print.doseQuantityAdjustConstraints 3
                                            |> addPerDosis
                                            |> withParens
                                        ]
                                        |> List.tryFind String.notEmpty
                                        |> Option.defaultValue ""
                                        |> Valid
                                else
                                    let tb =
                                        if isPerDose then
                                            itm
                                            |> Orderable.Item.Print.itemDoseQuantityTo printMd 3
                                            |> wrap Alert [ itm.Dose.Quantity |> Quantity.toOrdVar ]
                                        else
                                            itm
                                            |> Orderable.Item.Print.itemDosePerTimeTo printMd 3
                                            |> wrap Alert [ itm.Dose.PerTime |> PerTime.toOrdVar ]

                                    if tb |> textBlockIsEmpty |> not then
                                        "=" |> Valid
                                        tb

                                    if itm.Dose.PerTime |> PerTime.isSolved then
                                        [
                                            itm.Dose |> Dose.Print.dosePerTimeConstraints 3 |> withParens
                                            itm.Dose
                                            |> Dose.Print.doseQuantityConstraints 3
                                            |> addPerDosis
                                            |> withParens
                                        ]
                                        |> List.tryFind String.notEmpty
                                        |> Option.defaultValue ""
                                        |> Valid
                            |]
                        )

                | Once
                | OnceTimed _ ->
                    if itms |> Array.isEmpty then
                        [|
                            [|
                                // the orderable dose quantity
                                let doseQty = ord.Orderable |> Orderable.Print.doseQuantityTo printMd -1
                                // special case when no dose quantity
                                if doseQty |> String.isNullOrWhiteSpace then
                                    "eenmalig" |> Valid
                                else
                                    doseQty
                                    |> wrap
                                        Alert
                                        [
                                            ord.Orderable.Dose.Quantity |> Quantity.toOrdVar
                                            ord.Orderable.Dose.QuantityAdjust |> QuantityAdjust.toOrdVar
                                        ]

                                // the orderable dose adjust quantity
                                if useAdj then
                                    "=" |> Valid

                                    ord.Orderable
                                    |> Orderable.Print.doseQuantityAdjustTo printMd -1
                                    |> wrap
                                        Alert
                                        [
                                            ord.Orderable.Dose.Quantity |> Quantity.toOrdVar
                                            ord.Orderable.Dose.QuantityAdjust |> QuantityAdjust.toOrdVar
                                        ]

                                    if ord.Orderable.Dose.QuantityAdjust |> QuantityAdjust.isSolved then
                                        ord.Orderable.Dose
                                        |> Dose.Print.doseQuantityAdjustConstraints 3
                                        |> addPerDosis
                                        |> withParens
                                        |> Valid
                                else if ord.Orderable.Dose.Quantity |> Quantity.isSolved then
                                    ord.Orderable.Dose
                                    |> Dose.Print.doseQuantityConstraints 3
                                    |> addPerDosis
                                    |> withParens
                                    |> Valid
                            |]
                        |]
                    else
                        itms
                        |> Array.map (fun itm ->
                            [|
                                itm.Name |> Name.toString |> Valid

                                // the item dose quantity
                                itm
                                |> Orderable.Item.Print.itemDoseQuantityTo printMd 3
                                |> wrap Alert [ itm.Dose.Quantity |> Quantity.toOrdVar ]

                                // the item dose adjust quantity
                                if useAdj then
                                    "=" |> Valid

                                    itm
                                    |> Orderable.Item.Print.itemDoseQuantityAdjustTo printMd 3
                                    |> wrap
                                        Alert
                                        [
                                            itm.Dose.Quantity |> Quantity.toOrdVar
                                            itm.Dose.QuantityAdjust |> QuantityAdjust.toOrdVar
                                        ]

                                    if itm.Dose.QuantityAdjust |> QuantityAdjust.isSolved then
                                        itm.Dose
                                        |> Dose.Print.doseQuantityAdjustConstraints 3
                                        |> addPerDosis
                                        |> withParens
                                        |> Valid

                                else if itm.Dose.Quantity |> Quantity.isSolved then
                                    itm.Dose
                                    |> Dose.Print.doseQuantityConstraints 3
                                    |> addPerDosis
                                    |> withParens
                                    |> Valid

                            |]
                        )

            let prep = ord |> printPrep true sns

            let adm =
                match ord.Schedule with
                | Once
                | OnceTimed _
                | Discontinuous _
                | Timed _ ->
                    if itms |> Array.isEmpty then
                        [|
                            [|
                                // the frequency
                                if ord.Schedule |> Schedule.hasFrequency then
                                    freq

                                ord.Orderable
                                |> Orderable.Print.doseQuantityTo printMd -1
                                |> wrap
                                    Alert
                                    [
                                        ord.Orderable.Dose.Quantity |> Quantity.toOrdVar
                                        ord.Orderable.Dose.QuantityAdjust |> QuantityAdjust.toOrdVar
                                    ]

                                // if timed, add rate and time
                                if ord.Schedule |> Schedule.hasTime then
                                    ord.Orderable
                                    |> Orderable.Print.doseRateTo printMd -1
                                    |> wrap
                                        Caution
                                        [
                                            ord.Orderable.Dose.Rate |> Rate.toOrdVar
                                            ord.Orderable.Dose.RateAdjust |> RateAdjust.toOrdVar
                                        ]

                                    tme //ord.Prescription |> Prescription.Print.timeToMd -1
                            |]
                        |]
                    else
                        let admDoseQty = ord.Orderable |> Orderable.Print.doseQuantityTo printMd -1

                        itms
                        |> Array.mapi (fun i itm ->
                            [|
                                // the frequency
                                if ord.Schedule |> Schedule.hasFrequency then
                                    if i = 0 then
                                        freq

                                        ord.Orderable
                                        |> Orderable.Print.doseQuantityTo printMd -1
                                        |> wrap
                                            Alert
                                            [
                                                ord.Orderable.Dose.Quantity |> Quantity.toOrdVar
                                                ord.Orderable.Dose.QuantityAdjust |> QuantityAdjust.toOrdVar
                                            ]
                                    else
                                        "" |> Valid
                                        "" |> Valid
                                else if admDoseQty |> String.notEmpty then
                                    if i = 0 then
                                        admDoseQty
                                        |> wrap
                                            Alert
                                            [
                                                ord.Orderable.Dose.Quantity |> Quantity.toOrdVar
                                                ord.Orderable.Dose.QuantityAdjust |> QuantityAdjust.toOrdVar
                                            ]
                                    else
                                        "" |> Valid

                                let itmQty = itm |> Orderable.Item.Print.orderableQuantityTo printMd 3

                                if itmQty |> String.notEmpty then
                                    itm.Name |> Name.toString |> Valid

                                    itm
                                    |> Orderable.Item.Print.orderableQuantityTo printMd 3
                                    |> wrap Warning [ itm.OrderableQuantity |> Quantity.toOrdVar ]

                                    if i = 0 then
                                        "in" |> Valid

                                        ord.Orderable
                                        |> Orderable.Print.orderableQuantityTo printMd -1
                                        |> wrap Caution [ ord.Orderable.OrderableQuantity |> Quantity.toOrdVar ]
                                    else
                                        "" |> Valid
                                        "" |> Valid

                                // if timed, then add rate and time
                                if ord.Schedule |> Schedule.hasTime && itmQty |> String.notEmpty then
                                    if i = 0 then
                                        "=" |> Valid

                                        ord.Orderable
                                        |> Orderable.Print.doseRateTo printMd -1
                                        |> wrap Caution [ ord.Orderable.Dose.Rate |> Rate.toOrdVar ]

                                        tme //ord.Prescription |> Prescription.Print.timeToMd -1
                                        |> textBlockWithParens
                                    else
                                        "" |> Valid
                                        "" |> Valid
                                        "" |> Valid
                            |]
                        )
                | Continuous _ ->
                    let orbQty =
                        ord.Orderable
                        |> Orderable.Print.orderableQuantityTo printMd -1
                        |> wrap Caution [ ord.Orderable.OrderableQuantity |> Quantity.toOrdVar ]

                    if itms |> Array.isEmpty then
                        [|
                            [|
                                if cmpName |> String.notEmpty then
                                    cmpName |> Valid

                                orbQty

                                ord.Orderable
                                |> Orderable.Print.doseRateTo printMd -1
                                |> wrap
                                    Alert
                                    [
                                        ord.Orderable.Dose.Rate |> Rate.toOrdVar
                                        ord.Orderable.Dose.RateAdjust |> RateAdjust.toOrdVar
                                    ]
                            |]
                        |]
                    else
                        itms
                        |> Array.mapi (fun i itm ->
                            [|
                                itm.Name |> Name.toString |> Valid

                                itm
                                |> Orderable.Item.Print.orderableQuantityTo printMd 3
                                |> wrap Warning [ itm.OrderableQuantity |> Quantity.toOrdVar ]

                                if i = 0 then
                                    if orbQty |> textBlockIsEmpty |> not then
                                        "in" |> Valid

                                        ord.Orderable
                                        |> Orderable.Print.orderableQuantityTo printMd -1
                                        |> wrap Caution [ ord.Orderable.OrderableQuantity |> Quantity.toOrdVar ]

                                        "=" |> Valid

                                    ord.Orderable
                                    |> Orderable.Print.doseRateTo printMd -1
                                    |> wrap Alert [ ord.Orderable.Dose.Rate |> Rate.toOrdVar ]

                                    tme //ord.Prescription |> Prescription.Print.timeToMd -1
                                    |> textBlockWithParens

                                else
                                    if orbQty |> textBlockIsEmpty |> not then
                                        "" |> Valid
                                        "" |> Valid
                                        "" |> Valid

                                    "" |> Valid
                            |]
                        )

            pres, prep, adm


        let printOrderTo useAdj printMd sns (ord: Order) =
            let prs, prp, adm = ord |> printOrderToTableFormat useAdj printMd sns

            let add xs =
                let plus = [| " + " |]

                xs
                |> Array.map (Array.map unwrap)
                |> Array.fold
                    (fun acc x ->
                        if acc |> Array.isEmpty then
                            x
                        else
                            x |> Array.append plus |> Array.append acc
                    )
                    [||]
                |> String.concat " "

            prs |> add, prp |> add, adm |> add


        /// <summary>
        /// Print an Order to a string using an array of strings
        /// to pick the Orderable Items to print.
        /// </summary>
        let printOrderToString useAdj = printOrderTo useAdj false


        /// <summary>
        /// Print an Order to a Markdown string using an array of strings
        /// to pick the Orderable Items to print.
        /// </summary>
        let printOrderToMd printAdj = printOrderTo printAdj true


    module Dto =


        type Dto(id, n) =
            member val Id = id with get, set
            member val Adjust = OrderVariable.Dto.dto () with get, set
            member val Orderable = Orderable.Dto.dto id n with get, set
            member val Schedule = Schedule.Dto.dto n with get, set
            member val Route = "" with get, set
            member val Duration = OrderVariable.Dto.dto () with get, set
            member val Start = DateTime.now () with get, set
            member val Stop: DateTime option = None with get, set


        let fromDto (dto: Dto) =
            try
                let id = dto.Id |> Id.create
                let adj_qty = dto.Adjust |> Quantity.fromDto
                let ord_tme = dto.Duration |> Time.fromDto
                let orb = dto.Orderable |> Orderable.Dto.fromDto
                let sch = dto.Schedule |> Schedule.Dto.fromDto

                let sts =
                    match dto.Stop with
                    | Some dt -> (dto.Start, dt) |> StartStop.StartStop
                    | None -> dto.Start |> StartStop.Start

                create id adj_qty orb sch dto.Route ord_tme sts |> Ok
            with exn ->
                $"Could not create an Order from a dto with:\n{exn}" |> writeErrorMessage

                exn |> Exceptions.OrderCouldNotBeCreated |> Error


        let toDto ord =
            let id = (ord |> inf).Id |> Id.toString
            let n = ord.Orderable.Name |> Name.toString
            let dto = Dto(id, n)

            dto.Adjust <- ord.Adjust |> Quantity.toDto
            dto.Duration <- ord.Duration |> Time.toDto
            dto.Orderable <- ord.Orderable |> Orderable.Dto.toDto
            dto.Schedule <- ord.Schedule |> Schedule.Dto.toDto
            dto.Route <- ord.Route

            let start, stop =
                match ord.StartStop with
                | StartStop.Start dt -> (dt, None)
                | StartStop.StartStop(start, stop) -> (start, stop |> Some)

            dto.Start <- start
            dto.Stop <- stop

            dto


        /// <summary>
        /// Create a new Order Dto
        /// </summary>
        /// <param name="id">The id of the Order</param>
        /// <param name="orbN">The name of the Orderable</param>
        /// <param name="rte">The Route of the Order</param>
        /// <param name="cmps">The Components of the Orderable</param>
        /// <param name="nmeToSch">A function to create an Order with Name and Schedule</param>
        let private dto id orbN rte cmps nmeToSch =
            let dto = createNew id orbN nmeToSch rte |> toDto

            dto.Orderable.Components <-
                [
                    for cmpN, form, itms in cmps do
                        let c = Orderable.Component.Dto.dto id orbN cmpN form
                        c.Items <- itms |> List.map (Orderable.Item.Dto.dto id orbN cmpN)
                        c
                ]

            dto


        let cleanDose (dto: Dto) =
            dto.Duration |> OrderVariable.Dto.cleanVariable

            if dto.Schedule.IsDiscontinuous || dto.Schedule.IsTimed then
                dto.Schedule.Frequency |> OrderVariable.Dto.cleanVariable

            if dto.Schedule.IsTimed then
                dto.Schedule.Time |> OrderVariable.Dto.cleanVariable

            if not dto.Schedule.IsContinuous then
                dto.Orderable.OrderableQuantity |> OrderVariable.Dto.cleanVariable

            dto.Orderable.Dose |> Dose.Dto.clean

            dto.Orderable.Components
            |> List.iter (fun c ->
                c.OrderableQuantity |> OrderVariable.Dto.cleanVariable
                c.OrderableConcentration |> OrderVariable.Dto.cleanVariable
                c.OrderableCount |> OrderVariable.Dto.cleanVariable
                c.Dose |> Dose.Dto.clean

                c.Items
                |> List.iter (fun i ->
                    i.OrderableQuantity |> OrderVariable.Dto.cleanVariable
                    i.OrderableConcentration |> OrderVariable.Dto.cleanVariable
                    i.Dose |> Dose.Dto.clean
                )
            )


        /// <summary>
        /// Create a new Order Dto with a Continuous Prescription
        /// </summary>
        /// <param name="id">The id of the Order</param>
        /// <param name="orbN">The name of the Orderable</param>
        /// <param name="rte">The Route of the Order</param>
        /// <param name="cmps">The Components of the Orderable</param>
        let continuous id orbN rte cmps =
            Schedule.continuous Unit.NoUnit Unit.NoUnit |> dto id orbN rte cmps


        /// <summary>
        /// Create a new Order Dto with a Once Prescription
        /// </summary>
        /// <param name="id">The id of the Order</param>
        /// <param name="orbN">The name of the Orderable</param>
        /// <param name="rte">The Route of the Order</param>
        /// <param name="cmps">The Components of the Orderable</param>
        let once id orbN rte cmps =
            Schedule.once Unit.NoUnit Unit.NoUnit |> dto id orbN rte cmps


        /// <summary>
        /// Create a new Order Dto with a OnceTimed Prescription
        /// </summary>
        /// <param name="id">The id of the Order</param>
        /// <param name="orbN">The name of the Orderable</param>
        /// <param name="rte">The Route of the Order</param>
        /// <param name="cmps">The Components of the Orderable</param>
        let onceTimed id orbN rte cmps =
            Schedule.onceTimed Unit.NoUnit Unit.NoUnit |> dto id orbN rte cmps


        /// <summary>
        /// Create a new Order Dto with a Discontinuous Prescription
        /// </summary>
        /// <param name="id">The id of the Order</param>
        /// <param name="orbN">The name of the Orderable</param>
        /// <param name="rte">The Route of the Order</param>
        /// <param name="cmps">The Components of the Orderable</param>
        let discontinuous id orbN rte cmps =
            Schedule.discontinuous Unit.NoUnit Unit.NoUnit |> dto id orbN rte cmps


        /// <summary>
        /// Create a new Order Dto with a Timed Prescription
        /// </summary>
        /// <param name="id">The id of the Order</param>
        /// <param name="orbN">The name of the Orderable</param>
        /// <param name="rte">The Route of the Order</param>
        /// <param name="cmps">The Components of the Orderable</param>
        let timed id orbN rte cmps =
            Schedule.timed Unit.NoUnit Unit.NoUnit |> dto id orbN rte cmps


        let setToOnce (dto: Dto) =
            dto.Schedule <- dto.Schedule |> Schedule.Dto.setToOnce
            dto


        let setToOnceTimed (dto: Dto) =
            dto.Schedule <- dto.Schedule |> Schedule.Dto.setToOnceTimed
            dto


        let setToContinuous (dto: Dto) =
            dto.Schedule <- dto.Schedule |> Schedule.Dto.setToContinuous
            dto


        let setToDiscontinuous (dto: Dto) =
            dto.Schedule <- dto.Schedule |> Schedule.Dto.setToDiscontinuous
            dto


        let setToTimed (dto: Dto) =
            dto.Schedule <- dto.Schedule |> Schedule.Dto.setToTimed
            dto


        let toString dto =
            dto
            |> fromDto
            |> Result.map toString
            |> Result.defaultValue []
            |> String.concat "\n"
