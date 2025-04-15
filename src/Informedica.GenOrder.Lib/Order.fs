namespace Informedica.GenOrder.Lib



/// Types and functions that deal with an order.
/// An `Order` models the `Prescription` of an
/// `Orderable` with a `StartStop` start date and
/// stop date.
//[<RequireQualifiedAccess>]
module Order =


    open System
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open ConsoleWriter.NewLineNoTime
    open Informedica.GenUnits.Lib
    open WrappedString


    /// Utility functions to
    /// enable mapping of a `Variable`s
    /// to an `Order`
    module Mapping =


        let [<Literal>] qty = OrderVariable.Quantity.name
        let [<Literal>] cnc = OrderVariable.Concentration.name
        let [<Literal>] ptm = OrderVariable.PerTime.name
        let [<Literal>] rte = OrderVariable.Rate.name
        let [<Literal>] tot = OrderVariable.Total.name
        let [<Literal>] qtyAdj = OrderVariable.QuantityAdjust.name
        let [<Literal>] ptmAdj = OrderVariable.PerTimeAdjust.name
        let [<Literal>] rteAdj = OrderVariable.RateAdjust.name
        let [<Literal>] totAdj = OrderVariable.TotalAdjust.name
        let [<Literal>] cnt = OrderVariable.Count.name
        let [<Literal>] frq = OrderVariable.Frequency.name
        let [<Literal>] tme = OrderVariable.Time.name
        let [<Literal>] itm = "itm" //Orderable.Literals.item
        let [<Literal>] cmp = "cmp" //Orderable.Literals.comp
        let [<Literal>] orb = "orb" //Orderable.Literals.orderable
        let [<Literal>] dos = "dos" //Orderable.Literals.dose
        let [<Literal>] prs = "prs" //"Prescription"
        let [<Literal>] ord = "ord" // "Order"
        let [<Literal>] adj = "adj" // "Adjust"

        let [<Literal>] discontinuous = 3
        let [<Literal>] continuous = 4
        let [<Literal>] timed = 5
        let [<Literal>] once = 6
        let [<Literal>] onceTimed = 7


        let private getEquations_ indx =
            Web.getDataFromGenPres "Equations"
            |> Array.skip 1
            |> Array.filter (fun xs -> xs[indx] = "x")
            |> Array.map (Array.item 1)
            |> Array.toList


        /// <summary>
        /// Get a string list of Equations and
        /// use an index to filter out the relevant equations
        /// </summary>
        /// <param name="indx">The index to filter the equations</param>
        /// <remarks>
        /// The indx can be 3 for discontinuous equations, 4 for continuous
        /// and 5 for timed equations.
        /// </remarks>
        let getEquations indx =
            indx
            |> Memoization.memoize getEquations_


        /// <summary>
        /// Create an Equations mapping for an `Order`
        /// </summary>
        /// <param name="ord">The Order to Map</param>
        /// <param name="eqs">The equations as a string list</param>
        /// <returns>
        /// A tuple of `SumMapping` and `ProductMapping`
        /// </returns>
        let getEqsMapping (ord: Order) (eqs : string list) =
            let sumEqs =
                eqs
                |> List.filter (String.contains "sum")

            let prodEqs =
                eqs
                |> List.filter (String.contains "sum" >> not)

            let itmEqs =
                prodEqs
                |> List.filter (String.contains "[itm]")

            let cmpEqs =
                prodEqs
                |> List.filter (fun e ->
                    itmEqs
                    |> List.exists ((=) e)
                    |> not &&
                    e.Contains("[cmp]")
                )

            let orbEqs =
                prodEqs
                |> List.filter (fun e ->
                    itmEqs
                    |> List.exists ((=) e)
                    |> not &&
                    cmpEqs
                    |> List.exists((=) e)
                    |> not
                )

            let idN = [ord.Id |> Id.toString] |> Name.create
            let orbN = [ord.Id |> Id.toString; ord.Orderable.Name |> Name.toString] |> Name.create

            ord.Orderable.Components
            |> List.fold (fun acc c ->
                let cmpN =
                    [
                        yield! orbN |> Name.toStringList
                        c.Name |> Name.toString
                    ]
                    |> Name.create

                let itms =
                    c.Items
                    |> List.collect (fun i ->
                        itmEqs
                        |> List.map (fun s ->
                            let itmN =
                                [
                                    yield! cmpN |> Name.toStringList
                                    i.Name |> Name.toString
                                ]
                                |> Name.create
                            s
                            |> String.replace "[cmp]" $"{cmpN |> Name.toString}"
                            |> String.replace "[itm]" $"{itmN |> Name.toString}"
                        )
                    )

                let cmps =
                    cmpEqs
                    |> List.map (String.replace "[cmp]" $"{cmpN |> Name.toString}")

                acc
                |> List.append cmps
                |> List.append itms
            ) []
            |> fun es ->
                let sumEqs =
                    sumEqs
                    |> List.map (fun e ->
                        match e
                              |> String.replace "sum(" ""
                              |> String.replace ")" ""
                              |> String.split " = " with
                        | [lv; rv] ->
                            ord.Orderable.Components
                            |> List.map(fun c ->
                                let cmpN =
                                    [
                                        yield! orbN |> Name.toStringList
                                        c.Name |> Name.toString
                                    ]
                                    |> Name.create

                                rv
                                |> String.replace "[cmp]" $"{cmpN |> Name.toString}"
                            )
                            |> String.concat " + "
                            |> fun s -> $"{lv} = {s}"
                        | _ ->
                            writeErrorMessage $"could not match {e}"
                            ""
                    )
                    |> List.filter (String.isNullOrWhiteSpace >> not)
                    |> List.map (String.replace "[orb]" $"{orbN |> Name.toString}")
                    |> SumMapping

                let prodEqs =
                    es
                    |> List.append orbEqs
                    |> List.append es
                    |> List.map (String.replace "[orb]" $"{orbN |> Name.toString}")
                    |> List.map (String.replace "[ord]" $"{idN |> Name.toString}")
                    |> List.distinct
                    |> ProductMapping

                sumEqs, prodEqs


    /// Types and functions to deal
    /// with an `Orderable`, i.e. something
    /// that can be ordered.
    [<RequireQualifiedAccess>]
    module Orderable =

        open Informedica.GenSolver.Lib


        type Name = Types.Name


        /// Contains string constants
        /// to create `Variable` names
        module Literals =

            [<Literal>]
            let item = Mapping.itm
            [<Literal>]
            let comp = Mapping.cmp
            [<Literal>]
            let orderable = Mapping.orb
            [<Literal>]
            let order = Mapping.ord
            [<Literal>]
            let dose = Mapping.dos


        module Dose =

            module Quantity = OrderVariable.Quantity
            module PerTime = OrderVariable.PerTime
            module Rate = OrderVariable.Rate
            module Total = OrderVariable.Total
            module QuantityAdjust = OrderVariable.QuantityAdjust
            module PerTimeAdjust = OrderVariable.PerTimeAdjust
            module RateAdjust = OrderVariable.RateAdjust
            module TotalAdjust = OrderVariable.TotalAdjust


            let apply f (dos: Dose) = f dos


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
            let create
                qty
                ptm
                rte
                tot
                qty_adj
                ptm_adj
                rte_adj
                tot_adj =
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
                let n = n |> Name.add Literals.dose

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

                [
                    qty
                    ptm
                    rte
                    tot
                    qty_adj
                    ptm_adj
                    rte_adj
                    tot_adj
                ]


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


            let applyDoseQuantityConstraints dos =
                { (dos |> inf) with
                    Quantity = dos.Quantity |> Quantity.applyConstraints
                    QuantityAdjust = dos.QuantityAdjust |> QuantityAdjust.applyConstraints
                }


            let applyDosePerTimeConstraints dos =
                { (dos |> inf) with
                    PerTime = dos.PerTime |> PerTime.applyConstraints
                    PerTimeAdjust = dos.PerTimeAdjust |> PerTimeAdjust.applyConstraints
                }


            let applyDoseRateConstraints dos =
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
                { (dos |> inf) with
                    Rate =  dos.Rate |> Rate.increaseIncrement maxCount incrs
                }


            let setNormDose nd dos =
                let qty_adj, ptm_adj =
                    match nd with
                    | Informedica.GenForm.Lib.Types.NormQuantityAdjust (_, vu) ->
                        (dos |> inf).QuantityAdjust |> QuantityAdjust.setNearestValue vu,
                        dos.PerTimeAdjust
                    | Informedica.GenForm.Lib.Types.NormPerTimeAdjust (_, vu) ->
                        dos.QuantityAdjust,
                        dos.PerTimeAdjust |> PerTimeAdjust.setNearestValue vu
                    | _ -> dos.QuantityAdjust, dos.PerTimeAdjust

                let qty = dos.Quantity
                let ptm = dos.PerTime
                let rte = dos.Rate
                let tot = dos.Total
                let rte_adj = dos.RateAdjust
                let tot_adj = dos.TotalAdjust

                create qty ptm rte tot qty_adj ptm_adj rte_adj tot_adj


            let setDose set prs dos =
                match prs with
                | Once
                | OnceTimed _ ->
                    { (dos |> inf) with
                        Quantity =
                            dos.Quantity
                            |> Quantity.toOrdVar
                            |> set
                            |> Quantity
                    }
                | Discontinuous _
                | Timed _ ->
                    { dos with
                        PerTime =
                            dos.PerTime
                            |> PerTime.toOrdVar
                            |> set
                            |> PerTime
                    }
                | Continuous ->
                    { dos with
                        Rate =
                            dos.Rate
                            |> Rate.toOrdVar
                            |> set
                            |> Rate
                    }


            let setMinDose = setDose OrderVariable.setMinValue


            let setMaxDose = setDose OrderVariable.setMaxValue


            let setMedianDose = setDose OrderVariable.setMedianValue


            let isSolved = toOrdVars >> List.forall OrderVariable.isSolved


            let applyToQuantity f dos =
                { (dos |> inf) with
                    Quantity = f dos.Quantity
                }


            let applyToPerTime f dos =
                { (dos |> inf) with
                    PerTime = f dos.PerTime
                }


            let applyToRate f dos =
                { (dos |> inf) with
                    Rate = f dos.Rate
                }


            let applyToTotal f dos =
                { (dos |> inf) with
                    Total = f dos.Total
                }


            let applyToQuantityAdjust f dos =
                { (dos |> inf) with
                    QuantityAdjust = f dos.QuantityAdjust
                }


            let applyToPerTimeAdjust f dos =
                { (dos |> inf) with
                    PerTimeAdjust = f dos.PerTimeAdjust
                }


            let applyToRateAdjust f dos =
                { (dos |> inf) with
                    RateAdjust = f dos.RateAdjust
                }


            let isRateCleared dos =
                (dos |> inf).Rate |> Rate.isCleared ||
                dos.RateAdjust |> RateAdjust.isCleared


            let clearRate = applyToRate Rate.clear


            let setRateToNonZeroPositive dos =
                { (dos |> inf) with
                    Rate =
                        dos.Rate
                        |> Rate.setToNonZeroPositive
                    RateAdjust =
                        dos.RateAdjust
                        |> RateAdjust.setToNonZeroPositive
                }


            let rateMinIncrMaxToValues dos =
                { (dos |> inf) with
                    Rate = dos.Rate |> Rate.minIncrMaxToValues
                }


            let isQuantityCleared dos =
                (dos |> inf).Quantity |> Quantity.isCleared ||
                dos.QuantityAdjust |> QuantityAdjust.isCleared


            let clearQuantity dos =
                { (dos |> inf) with
                    Quantity = dos.Quantity |> Quantity.clear
                }


            let setQuantityToNonZeroPositive dos =
                { (dos |> inf) with
                    Quantity = dos.Quantity |> Quantity.setToNonZeroPositive
                    QuantityAdjust = dos.QuantityAdjust |> QuantityAdjust.setToNonZeroPositive
                }


            let quantityMinIncrMaxToValues dos =
                { (dos |> inf) with
                    Quantity = dos.Quantity |> Quantity.minIncrMaxToValues
                }


            let isPerTimeCleared dos =
                (dos |> inf).PerTime |> PerTime.isCleared ||
                dos.PerTimeAdjust |> PerTimeAdjust.isCleared


            let clearPerTime dos = (dos |> inf).PerTime |> PerTime.clear


            let setPerTimeToNonZeroPositive dos =
                { (dos |> inf) with
                    PerTime = dos.PerTime |> PerTime.setToNonZeroPositive
                    PerTimeAdjust = dos.PerTimeAdjust |> PerTimeAdjust.setToNonZeroPositive
                }


            let perTimeMinIncrMaxToValues dos =
                { (dos |> inf) with
                    PerTime = dos.PerTime |> PerTime.minIncrMaxToValues
                }


            let setDoseUnit du dos =
                let qty = (dos |> inf).Quantity |> Quantity.setFirstUnit du
                let ptm = dos.PerTime |> PerTime.setFirstUnit du
                let rte = dos.Rate |> Rate.setFirstUnit du
                let tot = dos.Total |> Total.setFirstUnit du
                let qty_adj = dos.QuantityAdjust |> QuantityAdjust.setFirstUnit du
                let ptm_adj = dos.PerTimeAdjust |> PerTimeAdjust.setFirstUnit du
                let rte_adj = dos.RateAdjust |> RateAdjust.setFirstUnit du
                let tot_adj = dos.TotalAdjust |> TotalAdjust.setFirstUnit du

                create qty ptm rte tot qty_adj ptm_adj rte_adj tot_adj


            /// <summary>
            /// Create a string list from a Dose where each string is
            /// a variable name with the value range and the Unit
            /// </summary>
            let toString = toOrdVars >> List.map (OrderVariable.toString false)


            module Print =


                let doseTo getter toStr = inf >> getter >> toStr


                let doseQuantityTo md prec =
                    let toStr =
                        if md then Quantity.toValueUnitMarkdown prec
                        else Quantity.toValueUnitString prec

                    doseTo _.Quantity toStr


                let doseQuantityToString = doseQuantityTo false


                let doseQuantityToMd = doseQuantityTo true


                let doseQuantityAdjustTo md prec =
                    let toStr =
                        if md then QuantityAdjust.toValueUnitMarkdown prec
                        else QuantityAdjust.toValueUnitString prec

                    doseTo _.QuantityAdjust toStr


                let doseQuantityAdjustToString = doseQuantityAdjustTo false


                let doseQuantityAdjustToMd = doseQuantityAdjustTo false


                let dosePerTimeTo md prec =
                    let toStr =
                        if md then PerTime.toValueUnitMarkdown prec
                        else PerTime.toValueUnitString prec

                    doseTo _.PerTime toStr


                let dosePerTimeToString = dosePerTimeTo false


                let dosePerTimeToMd = dosePerTimeTo true


                let dosePerTimeAdjustTo md prec =
                    let toStr =
                        if md then PerTimeAdjust.toValueUnitMarkdown prec
                        else PerTimeAdjust.toValueUnitString prec

                    doseTo _.PerTimeAdjust toStr


                let dosePerTimeAdjustToString = doseQuantityAdjustTo false


                let dosePerTimeAdjustToMd = dosePerTimeAdjustTo true


                let doseRateTo md prec =
                    let toStr =
                        if md then Rate.toValueUnitMarkdown prec
                        else Rate.toValueUnitString prec

                    doseTo _.Rate toStr


                let doseRateToString = doseRateTo false


                let doseRateToMd = doseRateTo true


                let doseRateAdjustTo md prec =
                    let toStr =
                        if md then RateAdjust.toValueUnitMarkdown prec
                        else RateAdjust.toValueUnitString prec

                    doseTo _.RateAdjust toStr


                let doseRateAdjustToString = doseRateAdjustTo false


                let doseRateAdjustToMd = doseRateAdjustTo true


                let doseConstraints toOvar prec =
                    inf
                    >> toOvar
                    >> _.Constraints
                    >> OrderVariable.Constraints.toMinMaxString prec


                let doseQuantityConstraints =
                    fun dos -> dos.Quantity |> Quantity.toOrdVar
                    |> doseConstraints


                let doseQuantityAdjustConstraints =
                    fun dos -> dos.QuantityAdjust |> QuantityAdjust.toOrdVar
                    |> doseConstraints


                let dosePerTimeConstraints =
                    fun dos -> dos.PerTime |> PerTime.toOrdVar
                    |> doseConstraints


                let dosePerTimeAdjustConstraints =
                    fun dos -> dos.PerTimeAdjust |> PerTimeAdjust.toOrdVar
                    |> doseConstraints


                let doseRateConstraints =
                    fun dos -> dos.Rate |> Rate.toOrdVar
                    |> doseConstraints


                let doseRateAdjustConstraints =
                    fun dos -> dos.RateAdjust |> RateAdjust.toOrdVar
                    |> doseConstraints


            /// Functions to create a Dose Dto and vice versa.
            module Dto =


                module Units = ValueUnit.Units
                module Quantity = OrderVariable.Quantity
                module QuantityPerTime = OrderVariable.PerTime
                module Rate = OrderVariable.Rate
                module Total = OrderVariable.Total
                module QuantityAdjust = OrderVariable.QuantityAdjust
                module QuantityPerTimeAdjust = OrderVariable.PerTimeAdjust
                module RateAdjust = OrderVariable.RateAdjust
                module TotalAdjust = OrderVariable.TotalAdjust


                type Dto () =
                    member val Quantity = OrderVariable.Dto.dto () with get, set
                    member val PerTime = OrderVariable.Dto.dto () with get, set
                    member val Rate = OrderVariable.Dto.dto () with get, set
                    member val Total = OrderVariable.Dto.dto () with get, set
                    member val QuantityAdjust = OrderVariable.Dto.dto () with get, set
                    member val PerTimeAdjust = OrderVariable.Dto.dto () with get, set
                    member val RateAdjust = OrderVariable.Dto.dto () with get, set
                    member val TotalAdjust = OrderVariable.Dto.dto () with get, set


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


                let toDto (dos : Dose) =
                    let dto = Dto ()

                    dto.Quantity <- dos.Quantity |> Quantity.toDto
                    dto.PerTime <- dos.PerTime |> PerTime.toDto
                    dto.Rate <- dos.Rate |> Rate.toDto
                    dto.Total <- dos.Total |> Total.toDto
                    dto.QuantityAdjust <- dos.QuantityAdjust |> QuantityAdjust.toDto
                    dto.PerTimeAdjust <- dos.PerTimeAdjust |> PerTimeAdjust.toDto
                    dto.RateAdjust <- dos.RateAdjust |> RateAdjust.toDto
                    dto.TotalAdjust <- dos.TotalAdjust |> TotalAdjust.toDto

                    dto


                let dto () = Dto ()

                let clean (dto: Dto) =
                    dto.Quantity |> OrderVariable.Dto.clean
                    dto.PerTime |> OrderVariable.Dto.clean
                    dto.Rate |> OrderVariable.Dto.clean
                    dto.Total |> OrderVariable.Dto.clean
                    dto.QuantityAdjust |> OrderVariable.Dto.clean
                    dto.PerTimeAdjust |> OrderVariable.Dto.clean
                    dto.RateAdjust |> OrderVariable.Dto.clean
                    dto.TotalAdjust |> OrderVariable.Dto.clean


        /// Type and functions that models an
        /// `Order` `Item` that is contained in
        /// a `Component`
        [<RequireQualifiedAccess>]
        module Item =

            module Concentration = OrderVariable.Concentration
            module Quantity = OrderVariable.Quantity
            module Total = OrderVariable.Total
            module Rate = OrderVariable.Rate


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
            let create
                n
                cmp_qty
                orb_qty
                cmp_cnc
                orb_cnc
                dos =
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
                let n =
                    [ id; orbN; cmpN; itmN ]
                    |> Name.create

                let cmp_qty = let n = n |> Name.add Literals.comp in Quantity.create n un
                let orb_qty = let n = n |> Name.add Literals.orderable in Quantity.create n un
                let cmp_cnc = let n = n |> Name.add Literals.comp in Concentration.create n un un
                let orb_cnc = let n = n |> Name.add Literals.orderable in Concentration.create n un un
                let dos     = Dose.createNew n

                create (itmN |> Name.fromString) cmp_qty orb_qty cmp_cnc orb_cnc dos


            /// Get the `Name` of an `Item`
            let getName itm = (itm |> inf).Name


            /// <summary>
            /// Return an Item as a list of OrderVariables
            /// </summary>
            /// <param name="itm">The Item</param>
            let toOrdVars itm =
                let itm_cmp_qty = (itm |> inf).ComponentQuantity |> Quantity.toOrdVar
                let itm_orb_qty = itm.OrderableQuantity          |> Quantity.toOrdVar
                let itm_cmp_cnc = itm.ComponentConcentration     |> Concentration.toOrdVar
                let itm_orb_cnc = itm.OrderableConcentration     |> Concentration.toOrdVar

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
                let orb_qty = itm.OrderableQuantity          |> Quantity.fromOrdVar ovars
                let cmp_cnc = itm.ComponentConcentration     |> Concentration.fromOrdVar ovars
                let orb_cnc = itm.OrderableConcentration     |> Concentration.fromOrdVar ovars
                let dos = itm.Dose |> Dose.fromOrdVars ovars

                create itm.Name cmp_qty orb_qty cmp_cnc orb_cnc dos


            /// <summary>
            /// Apply constraints to an Item
            /// </summary>
            /// <param name="itm">The Item</param>
            let applyConstraints itm =
                let cmp_qty = (itm |> inf).ComponentQuantity |> Quantity.applyConstraints
                let orb_qty = itm.OrderableQuantity          |> Quantity.applyConstraints
                let cmp_cnc = itm.ComponentConcentration     |> Concentration.applyConstraints
                let orb_cnc = itm.OrderableConcentration     |> Concentration.applyConstraints
                let dos = itm.Dose |> Dose.applyConstraints

                create itm.Name cmp_qty orb_qty cmp_cnc orb_cnc dos


            let isOrderableConcentrationCleared itm = (itm |> inf).OrderableConcentration |> Concentration.isCleared


            let isOrderableQuantityCleared itm = (itm |> inf).OrderableQuantity |> Quantity.isCleared


            /// Get the `Item` dose
            let getDose itm = (itm |> inf).Dose


            let isSolved = toOrdVars >> List.forall OrderVariable.isSolved


            let setDoseUnit sn du itm =
                if itm
                   |> getName
                   |> Name.toStringList
                   |> List.exists ((=) sn)
                   |> not then itm
                else
                    { itm with Dose = itm.Dose |> Dose.setDoseUnit du }


            let setNormDose sn nd itm =
                if itm
                   |> getName
                   |> Name.toStringList
                   |> List.exists ((=) sn)
                   |> not then itm
                else
                    { itm with Dose = itm.Dose |> Dose.setNormDose nd }


            let isDoseSolved = getDose >> Dose.isSolved


            let isDoseQuantityCleared = getDose >> Dose.isQuantityCleared


            let isDoseRateCleared = getDose >> Dose.isRateCleared


            let isDosePerTimeCleared = getDose >> Dose.isPerTimeCleared


            /// <summary>
            /// Create a string list from a Item where each string is
            /// a variable name with the value range and the Unit
            /// </summary>
            let toString = toOrdVars >> List.map (OrderVariable.toString false)


            module Print =


                let private getToStr getter toStr =
                    inf
                    >> getter
                    >> toStr


                let concentrationTo getConc md prec =
                    if md then Concentration.toValueUnitMarkdown prec
                    else Concentration.toValueUnitString prec
                    |> getToStr getConc


                let itemComponentConcentrationTo =
                    concentrationTo _.ComponentConcentration


                let itemComponentConcentrationToString = itemComponentConcentrationTo false


                let itemComponentConcentrationToMd = itemComponentConcentrationTo true


                let itemOrderableConcentrationTo =
                    concentrationTo _.OrderableConcentration


                let itemOrderableConcentrationToString = itemOrderableConcentrationTo false


                let itemOrderableConcentrationToMd = itemOrderableConcentrationTo true


                let quantityTo getQty md prec =
                    if md then Quantity.toValueUnitMarkdown prec
                    else Quantity.toValueUnitString prec
                    |> getToStr getQty


                let componentQuantityTo =
                    quantityTo _.ComponentQuantity


                let itemComponentQuantityToString = componentQuantityTo false


                let itemComponentQuantityToMd = componentQuantityTo true


                let orderableQuantityTo =
                    quantityTo _.OrderableQuantity


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


            /// Functions to create a Item Dto and vice versa.
            module Dto =

                module Units = ValueUnit.Units
                module Id = WrappedString.Id
                module Name = WrappedString.Name
                module Quantity = OrderVariable.Quantity
                module Concentration = OrderVariable.Concentration


                type Dto () =
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


                let toDto (itm : Item) =
                    let dto = Dto ()

                    dto.Name <- itm.Name |> Name.toString
                    dto.ComponentQuantity <-
                        itm.ComponentQuantity
                        |> Quantity.toDto
                    dto.OrderableQuantity <-
                        itm.OrderableQuantity
                        |> Quantity.toDto
                    dto.ComponentConcentration <-
                        itm.ComponentConcentration
                        |> Concentration.toDto
                    dto.OrderableConcentration <-
                        itm.OrderableConcentration
                        |> Concentration.toDto
                    dto.Dose <-
                        itm.Dose |> Dose.Dto.toDto

                    dto


                /// <summary>
                /// Create a new Item Dto
                /// </summary>
                /// <param name="id">The Id of the Item</param>
                /// <param name="orbN">The name of the Orderable</param>
                /// <param name="cmpN">The name of the Component</param>
                /// <param name="itmN">The name of the Item</param>
                let dto id orbN cmpN itmN =
                    createNew id orbN cmpN itmN
                    |> toDto


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


            /// Apply **f** to a `Component` **comp**
            let apply f (comp: Component) = comp |> f


            /// Utility to facilitate type inference
            let inf = apply id


            /// <summary>
            /// Create a `Component` with
            /// </summary>
            /// <param name="id">The Id of the Component</param>
            /// <param name="nm">The name of the Component</param>
            /// <param name="sh">The shape of the Component</param>
            /// <param name="cmp_qty">The quantity of the Component</param>
            /// <param name="orb_qty">The quantity of the Component in the Orderable</param>
            /// <param name="orb_cnt">The count of the Component in the Orderable</param>
            /// <param name="ord_qty">The quantity of the Component in the Order</param>
            /// <param name="ord_cnt">The count of the Component in the Order</param>
            /// <param name="orb_cnc">The concentration of the Component in the Orderable</param>
            /// <param name="dos">The dose of the Component</param>
            /// <param name="ii">The list of Items in the Component</param>
            let create
                id
                nm
                sh
                cmp_qty
                orb_qty
                orb_cnt
                ord_qty
                ord_cnt
                orb_cnc
                dos
                ii =
                {
                    Id = id
                    Name = nm
                    Shape = sh
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
            /// <param name="sh">The shape of the Component</param>
            let createNew id orbN cmpN sh =
                let un = Unit.NoUnit
                let nm = [ id; orbN; cmpN ] |> Name.create
                let id = Id.create id

                let cmp_qty = let n = nm |> Name.add Literals.comp in Quantity.create n un
                let orb_qty = let n = nm |> Name.add Literals.orderable in Quantity.create n un
                let orb_cnt = let n = nm |> Name.add Literals.orderable in Count.create n
                let ord_qty = let n = nm |> Name.add Literals.order in Quantity.create n un
                let ord_cnt = let n = nm |> Name.add Literals.order in Count.create n
                let orb_cnc = let n = nm |> Name.add Literals.orderable in Concentration.create n un un
                let dos     = Dose.createNew nm

                create id (cmpN |> Name.fromString) sh cmp_qty orb_qty orb_cnt ord_qty ord_cnt orb_cnc dos []


            let applyToItems pred f cmp =
                { (cmp |> inf) with
                    Items =
                        cmp.Items
                        |> List.map (fun itm ->
                            if itm |> pred then f itm
                            else itm
                        )
                }


            let applyToAllItems = applyToItems (fun _ -> true)


            let applyToItem s = applyToItems (_.Name >> Name.toString >> String.equalsCapInsens s)


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
                let orb_qty = cmp.OrderableQuantity          |> Quantity.toOrdVar
                let orb_cnt = cmp.OrderableCount             |> Count.toOrdVar
                let orb_cnc = cmp.OrderableConcentration     |> Concentration.toOrdVar
                let ord_qty = cmp.OrderQuantity              |> Quantity.toOrdVar
                let ord_cnt = cmp.OrderCount                 |> Count.toOrdVar

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
                let orb_qty = cmp.OrderableQuantity          |> Quantity.fromOrdVar ovars
                let orb_cnt = cmp.OrderableCount             |> Count.fromOrdVar ovars
                let orb_cnc = cmp.OrderableConcentration     |> Concentration.fromOrdVar ovars
                let ord_qty = cmp.OrderQuantity              |> Quantity.fromOrdVar ovars
                let ord_cnt = cmp.OrderCount                 |> Count.fromOrdVar ovars
                let dos = cmp.Dose |> Dose.fromOrdVars ovars

                cmp.Items
                |> List.map (Item.fromOrdVars ovars)
                |> create cmp.Id cmp.Name cmp.Shape cmp_qty orb_qty orb_cnt ord_qty ord_cnt orb_cnc dos


            /// <summary>
            /// Apply constraints to a Component
            /// </summary>
            /// <param name="cmp">The Component</param>
            let applyConstraints cmp =
                let cmp_qty = (cmp |> inf).ComponentQuantity |> Quantity.applyConstraints
                let orb_qty = cmp.OrderableQuantity          |> Quantity.applyConstraints
                let orb_cnt = cmp.OrderableCount             |> Count.applyConstraints
                let orb_cnc = cmp.OrderableConcentration     |> Concentration.applyConstraints
                let ord_qty = cmp.OrderQuantity              |> Quantity.applyConstraints
                let ord_cnt = cmp.OrderCount                 |> Count.applyConstraints
                let dos = cmp.Dose |> Dose.applyConstraints

                cmp.Items
                |> List.map Item.applyConstraints
                |> create cmp.Id cmp.Name cmp.Shape cmp_qty orb_qty orb_cnt ord_qty ord_cnt orb_cnc dos


            let isOrderableConcentrationCleared cmp = (cmp |> inf).OrderableConcentration |> Concentration.isCleared


            let isItemOrderableConcentrationCleared = getItems >> List.exists Item.isOrderableConcentrationCleared


            let isOrderableQuantityCleared cmp = (cmp |> inf).OrderableQuantity |> Quantity.isCleared


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
                    OrderableQuantity =
                        cmp.OrderableQuantity
                        |> Quantity.increaseIncrement maxCount incrs
                }


            let setDoseUnit sn du = applyToAllItems (Item.setDoseUnit sn du)


            let setNormDose sn nd = applyToAllItems (Item.setNormDose sn nd)


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
                        if md then Quantity.toValueUnitMarkdown prec
                        else Quantity.toValueUnitString prec

                    getToStr get toStr


                let componentQuantityTo =
                    quantityTo _.ComponentQuantity


                let componentQuantityToString = componentQuantityTo false


                let componentQuantityToMd = componentQuantityTo true


                let componentOrderableQuantityTo =
                    quantityTo _.OrderableQuantity


                let componentOrderableQuantityToString = componentOrderableQuantityTo false


                let componentOrderableQuantityToMd = componentOrderableQuantityTo true


                let componentOrderQuantityTo =
                    quantityTo _.OrderQuantity

                let componentOrderQuantityToString = componentOrderQuantityTo false


                let componentOrderQuantityToMd = componentOrderQuantityTo true


                let countTo get md =
                    let toStr =
                        if md then Count.toValueUnitMarkdown -1
                        else Count.toValueUnitString -1

                    getToStr get toStr


                let componentOrderableCountTo =
                    countTo _.OrderableCount


                let componentOrderableCountToString = componentOrderableCountTo false


                let componentOrderableCountToMd = componentOrderableCountTo true


                let componentOrderCountTo =
                    countTo _.OrderCount


                let componentOrderCountToString = componentOrderCountTo false


                let componentOrderCountToMd = componentOrderCountTo true


                let concentrationTo get md =
                    let toStr =
                        if md then Concentration.toValueUnitMarkdown -1
                        else Concentration.toValueUnitString -1

                    getToStr get toStr


                let componentOrderableConcentrationTo =
                    concentrationTo _.OrderableConcentration


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


                let componentDoseRateToString =componentDoseRateTo false


                let componentDoseRateToMd = componentDoseRateTo true


                let componentDoseRateAdjustTo md prec (cmp : Component) =
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


                type Dto () =
                    member val Id = "" with get, set
                    member val Name = "" with get, set
                    member val Shape = "" with get, set
                    member val ComponentQuantity = OrderVariable.Dto.dto () with get, set
                    member val OrderableQuantity = OrderVariable.Dto.dto () with get, set
                    member val OrderableCount = OrderVariable.Dto.dto () with get, set
                    member val OrderQuantity = OrderVariable.Dto.dto () with get, set
                    member val OrderCount = OrderVariable.Dto.dto () with get, set
                    member val OrderableConcentration = OrderVariable.Dto.dto () with get, set
                    member val Dose = Dose.Dto.dto () with get, set
                    member val Items : Item.Dto.Dto list = [] with get, set


                let fromDto (dto: Dto) =

                    let id = dto.Id |> Id.create
                    let n = dto.Name |> Name.fromString
                    let s = dto.Shape
                    let cmp_qty = dto.ComponentQuantity |> Quantity.fromDto
                    let orb_qty = dto.OrderableQuantity |> Quantity.fromDto
                    let orb_cnt = dto.OrderableCount    |> Count.fromDto
                    let orb_cnc = dto.OrderableConcentration |> Concentration.fromDto
                    let ord_qty = dto.OrderQuantity |> Quantity.fromDto
                    let ord_cnt = dto.OrderCount    |> Count.fromDto
                    let ii =
                        dto.Items
                        |> List.map Item.Dto.fromDto

                    let dos = dto.Dose |> Dose.Dto.fromDto

                    create id n s cmp_qty orb_qty orb_cnt ord_qty ord_cnt orb_cnc dos ii


                let toDto (cmp : Component) =
                    let dto = Dto ()

                    dto.Name <- cmp.Name |> Name.toString
                    dto.Shape <- cmp.Shape
                    dto.ComponentQuantity <-
                        cmp.ComponentQuantity
                        |> Quantity.toDto
                    dto.OrderableQuantity <-
                        cmp.OrderableQuantity
                        |> Quantity.toDto
                    dto.OrderableCount <-
                        cmp.OrderableCount
                        |> Count.toDto
                    dto.OrderQuantity <-
                        cmp.OrderQuantity
                        |> Quantity.toDto
                    dto.OrderCount <-
                        cmp.OrderCount
                        |> Count.toDto
                    dto.OrderableConcentration <-
                        cmp.OrderableConcentration
                        |> Concentration.toDto
                    dto.Dose <-
                        cmp.Dose
                        |> Dose.Dto.toDto
                    dto.Items <-
                        cmp.Items
                        |> List.map Item.Dto.toDto

                    dto


                /// <summary>
                /// Create a Component Dto
                /// </summary>
                /// <param name="id">The Id of the Component</param>
                /// <param name="orbN">The name of the Orderable</param>
                /// <param name="cmpN">The name of the Component</param>
                /// <param name="shape">The shape of the Component</param>
                let dto id orbN cmpN shape =
                    createNew id orbN cmpN shape
                    |> toDto


        module Quantity = OrderVariable.Quantity
        module Concentration = OrderVariable.Concentration
        module Count = OrderVariable.Count


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
        let create
            n
            orb_qty
            ord_qty
            ord_cnt
            dos_cnt
            dos
            cc =
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
            let n = [id; orbN] |> Name.create

            let orb_qty = let n = n |> Name.add Literals.orderable in Quantity.create n un
            let ord_qty = let n = n |> Name.add Literals.order in Quantity.create n un
            let ord_cnt = let n = n |> Name.add Literals.order in Count.create n
            let dos_cnt = let n = n |> Name.add Literals.dose in Count.create n
            let dos     = Dose.createNew n

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
                    |> List.map (fun cmp ->
                        if cmp |> pred then cmp |> f
                        else cmp
                    )
            }


        let applyToAllComponents = applyToComponents (fun _ -> true)


        let applyToComponent s = applyToComponents (_.Name >> Name.toString >> String.equalsCapInsens s)


        /// <summary>
        /// Return an Orderable as a list of OrderVariables
        /// </summary>
        /// <param name="orb">The Orderable</param>
        let toOrdVars orb =
            let ord_qty = (orb |> inf).OrderQuantity |> Quantity.toOrdVar
            let orb_qty = orb.OrderableQuantity      |> Quantity.toOrdVar
            let ord_cnt = orb.OrderCount             |> Count.toOrdVar
            let dos_cnt = orb.DoseCount              |> Count.toOrdVar

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
            let orb_qty = orb.OrderableQuantity      |> Quantity.fromOrdVar ovars
            let ord_cnt = orb.OrderCount             |> Count.fromOrdVar ovars
            let dos_cnt = orb.DoseCount              |> Count.fromOrdVar ovars
            let dos = orb.Dose |> Dose.fromOrdVars ovars

            orb.Components
            |> List.map (Component.fromOrdVars ovars)
            |> create orb.Name orb_qty ord_qty ord_cnt dos_cnt dos


        /// <summary>
        /// Return a list of strings from an Orderable where each string is
        /// a variable name with the value range and the Unit
        /// </summary>
        let toString = toOrdVars >> List.map (OrderVariable.toString false)


        let toStringWithConstraints = toOrdVars >> List.map (OrderVariable.toStringWithConstraints true false)


        /// <summary>
        /// Apply constraints to an Orderable
        /// </summary>
        /// <param name="orb">The Orderable</param>
        let applyConstraints orb =
            let ord_qty = (orb |> inf).OrderQuantity |> Quantity.applyConstraints
            let orb_qty = orb.OrderableQuantity      |> Quantity.applyConstraints
            let ord_cnt = orb.OrderCount             |> Count.applyConstraints
            let dos_cnt = orb.DoseCount              |> Count.applyConstraints
            let dos = orb.Dose |> Dose.applyConstraints

            orb.Components
            |> List.map Component.applyConstraints
            |> create orb.Name orb_qty ord_qty ord_cnt dos_cnt dos


        let isComponentOrderableConcentrationCleared =
            getComponents
            >> List.exists Component.isOrderableConcentrationCleared


        let isItemOrderableConcentrationCleared =
            getComponents
            >> List.collect Component.getItems
            >> List.exists Item.isOrderableConcentrationCleared


        let isOrderableQuantityCleared orb = (orb |> inf).OrderableQuantity |> Quantity.isCleared


        /// Get the `Orderable` dose
        let getDose orb = (orb |> inf).Dose


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
                then orb
            else
                // first calculate the minimum increment increase for the orderable and components
                { orb with
                    Components =
                        orb.Components
                        |> List.map (Component.increaseQuantityIncrement maxCount incrs)
                }
                |> _.Components
                |> List.map (fun c ->
                    (c.OrderableQuantity |> Quantity.toOrdVar |> OrderVariable.getVar).Values
                )
                |> List.choose Variable.ValueRange.getIncr
                |> function
                    | [] -> orb
                    | incrs ->
                        if incrs |> List.length <> (orb.Components |> List.length) then orb
                        else
                            let incr =
                                incrs
                                |> List.minBy (fun i ->
                                    i
                                    |> Variable.ValueRange.Increment.toValueUnit
                                    |> ValueUnit.getBaseValue
                                )

                            writeDebugMessage $"Increase quantity increment to {incr |> Variable.ValueRange.Increment.toString false}"
                            // apply the minimum increment increase to the orderable and components
                            { orb with
                                OrderableQuantity =
                                    orb.OrderableQuantity |> Quantity.increaseIncrement maxCount [incr]
                                Components =
                                    orb.Components
                                    |> List.map (Component.increaseQuantityIncrement maxCount [incr])
                            }


        /// <summary>
        /// Increase the Rate increment of an Orderable to a maximum
        /// count using a list of increments.
        /// </summary>
        /// <param name="maxCount">The maximum count</param>
        /// <param name="incrs">The list of increments</param>
        /// <param name="orb">The Orderable</param>
        let increaseRateIncrement maxCount incrs orb =
            { (orb |> inf) with
                Dose = orb.Dose |> Dose.increaseRateIncrement maxCount incrs
            }


        let setDoseUnit sn du (orb : Orderable) =
            { orb with
                Components = orb.Components |> List.map (Component.setDoseUnit sn du)
            }


        let setNormDose sn nd (orb : Orderable) =
            { orb with
                Components = orb.Components |> List.map (Component.setNormDose sn nd)
            }


        let isDoseSolved = getDose >> Dose.isSolved


        let isComponentsDoseSolved = getComponents >> List.exists Component.isDoseSolved


        let isItemsDoseSolved = getComponents >> List.exists Component.isItemsDoseSolved


        let isDoseQuantityCleared = getDose >> Dose.isQuantityCleared


        let isComponentDoseQuantityCleared = getComponents >> List.exists Component.isDoseQuantityCleared


        let isItemDoseQuantityCleared = getComponents >> List.exists Component.isItemDoseQuantityCleared


        let isDoseRateCleared = getDose >> Dose.isRateCleared


        let isItemDoseRateCleared =
            getComponents
            >> List.exists Component.isItemDoseRateCleared


        let isDosePerTimeCleared = getDose >> Dose.isPerTimeCleared


        let isComponentDosePerTimeCleared = getComponents >> List.exists Component.isDosePerTimeCleared


        let isItemDosePerTimeCleared = getComponents >> List.exists Component.isItemDosePerTimeCleared


        module Print =


            let private getToStr get toStr = inf >> get >> toStr


            let quantityTo get md prec =
                if md then Quantity.toValueUnitMarkdown prec
                else Quantity.toValueUnitString prec
                |> getToStr get


            let orderableQuantityTo =
                quantityTo _.OrderableQuantity


            let orderableQuantityToString = orderableQuantityTo false


            let orderableQuantityToMd = orderableQuantityTo true


            let orderQuantityTo =
                quantityTo _.OrderQuantity


            let orderQuantityToString = orderQuantityTo false


            let orderQuantityToMd = orderQuantityTo true


            let countTo get md =
                if md then Count.toValueUnitMarkdown -1
                else Count.toValueUnitString -1
                |> getToStr get


            let orderCountTo =
                countTo _.OrderCount


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


            type Dto () =
                member val Name = "" with get, set
                member val OrderableQuantity = OrderVariable.Dto.dto () with get, set
                member val OrderQuantity = OrderVariable.Dto.dto () with get, set
                member val OrderCount = OrderVariable.Dto.dto () with get, set
                member val DoseCount = OrderVariable.Dto.dto () with get, set
                member val Dose = Dose.Dto.dto () with get, set
                member val Components : Component.Dto.Dto list = [] with get, set


            let fromDto (dto: Dto) =
                let n = dto.Name |> Name.fromString

                let orb_qty = dto.OrderableQuantity |> Quantity.fromDto
                let ord_qty = dto.OrderQuantity     |> Quantity.fromDto
                let ord_cnt = dto.OrderCount        |> Count.fromDto
                let dos_cnt = dto.DoseCount         |> Count.fromDto

                let cc =
                    dto.Components
                    |> List.map Component.Dto.fromDto

                let dos = dto.Dose |> Dose.Dto.fromDto

                create n orb_qty ord_qty ord_cnt dos_cnt dos cc


            let toDto (orb : Orderable) =
                let dto = Dto ()

                dto.Name <- orb.Name |> Name.toString
                dto.OrderableQuantity <-
                    orb.OrderableQuantity
                    |> Quantity.toDto
                dto.OrderQuantity <-
                    orb.OrderQuantity
                    |> Quantity.toDto
                dto.OrderCount <-
                    orb.OrderCount
                    |> Count.toDto
                dto.DoseCount <-
                    orb.DoseCount
                    |> Count.toDto
                dto.Dose <-
                    orb.Dose
                    |> Dose.Dto.toDto
                dto.Components <-
                    orb.Components
                    |> List.map Component.Dto.toDto

                dto


            /// <summary>
            /// Create a new Orderable Dto
            /// </summary>
            /// <param name="id">The Id of the Orderable</param>
            /// <param name="orbN">The name of the Orderable</param>
            let dto id orbN =
                createNew id orbN
                |> toDto


    [<RequireQualifiedAccess>]
    module Prescription =


        module Frequency = OrderVariable.Frequency
        module Time = OrderVariable.Time


        /// <summary>
        /// Create a Frequency and Time
        /// </summary>
        /// <param name="tu1">The frequency time unit</param>
        /// <param name="tu2">The time unit</param>
        /// <param name="n">The name of the Frequency and Time</param>
        let freqTime tu1 tu2 n =  (Frequency.create n tu1, Time.create n tu2)


        /// Create a Once `Prescription`
        let once tu1 tu2 n =
            let _, _ = n |> freqTime tu1 tu2 in Once


        /// Create a OnceTimed `Prescription`
        let onceTimed tu1 tu2 n =
            let _, tme = n |> freqTime tu1 tu2 in tme |> OnceTimed


        /// Create a Continuous `Prescription`
        let continuous tu1 tu2 n =
            let _, _ = n |> freqTime tu1 tu2 in Continuous


        /// Create a Discontinuous `Prescription`
        let discontinuous tu1 tu2 n =
            let frq, _ = n |> freqTime tu1 tu2 in frq |> Discontinuous


        /// Create a Timed `Prescription`
        let timed tu1 tu2 n =
            let frq, tme = n |> freqTime tu1 tu2 in (frq, tme) |> Timed


        /// Check whether a `Prescription` is Once
        let isOnce = function | Once -> true | _ -> false


        /// Check whether a `Prescription` is Once
        let isOnceTimed = function | OnceTimed _ -> true | _ -> false


        /// Check whether a `Prescription` is Discontinuous
        let isDiscontinuous = function | Discontinuous _ -> true | _ -> false


        /// Check whether a `Prescription` is Continuous
        let isContinuous = function | Continuous -> true | _ -> false


        /// Check whether a `Prescription` is Timed
        let isTimed = function | Timed _ -> true | _ -> false


        let hasFrequency pr =
            pr |> isDiscontinuous || pr |> isTimed


        let hasTime pr =
            pr |> isTimed || pr |> isOnceTimed


        /// <summary>
        /// Return a Prescription as a Frequency OrderVariable option
        /// and a Time OrderVariable option
        /// </summary>
        /// <param name="prs">The Prescription</param>
        let toOrdVars prs =
            match prs with
            | Once
            | Continuous -> None, None
            | OnceTimed tme ->
                None, tme |> Time.toOrdVar |> Some
            | Discontinuous frq ->
                frq |> Frequency.toOrdVar |> Some, None
            | Timed(frq, tme)     ->
                frq |> Frequency.toOrdVar |> Some,
                tme |> Time.toOrdVar |> Some


        /// <summary>
        /// Create a new Prescription from a list of OrderVariables using
        /// an old Prescription.
        /// </summary>
        /// <param name="ovars">The list of OrderVariables</param>
        /// <param name="prs">The old Prescription</param>
        let fromOrdVars ovars prs =
            match prs with
            | Once
            | Continuous -> prs
            | OnceTimed tme ->
                tme |> Time.fromOrdVar ovars |> OnceTimed
            | Discontinuous frq ->
                frq |> Frequency.fromOrdVar ovars |> Discontinuous
            | Timed(frq, tme)     ->
                (frq |> Frequency.fromOrdVar ovars,
                tme |> Time.fromOrdVar ovars)
                |> Timed


        /// <summary>
        /// Apply constraints to a Prescription
        /// </summary>
        let applyConstraints prs =
            match prs with
            | Once
            | Continuous -> prs
            | OnceTimed tme ->
                tme |> Time.applyConstraints |> OnceTimed
            | Discontinuous frq ->
                frq |> Frequency.applyConstraints |> Discontinuous
            | Timed(frq, tme)     ->
                (frq |> Frequency.applyConstraints,
                tme |> Time.applyConstraints)
                |> Timed


        /// <summary>
        /// Return a list of strings from a Prescription where each string is
        /// a variable name with the value range and the Unit
        /// </summary>
        let toString (prs: Prescription) =
                match prs with
                | Once -> ["eenmalig"]
                | Continuous -> ["continu"]
                | OnceTimed tme -> [tme |> Time.toString]
                | Discontinuous frq -> [frq |> Frequency.toString]
                | Timed(frq, tme)   ->
                    [frq |> Frequency.toString; tme |> Time.toString]


        /// <summary>
        /// Return a list of strings from a Prescription where each string is
        /// a variable name with the value range and the Unit
        /// </summary>
        let toStringWithConstraints (prs: Prescription) =
                match prs with
                | Once -> ["eenmalig"]
                | Continuous -> ["continu"]
                | OnceTimed tme -> [tme |> Time.toStringWithConstraints]
                | Discontinuous frq ->
                    [frq |> Frequency.toStringWithConstraints]
                | Timed(frq, tme) ->
                    [
                        frq |> Frequency.toStringWithConstraints
                        tme |> Time.toStringWithConstraints
                    ]


        let isSolved (prs : Prescription) =
            prs
            |> toOrdVars
            |> function
                | Some ovar1, Some ovar2 ->
                    ovar1 |> OrderVariable.isSolved &&
                    ovar2 |> OrderVariable.isSolved
                | None, Some ovar
                | Some ovar, None -> ovar |> OrderVariable.isSolved
                | None, None -> true


        let frequencyIsSolved prs =
            prs
            |> toOrdVars
            |> function
                | Some ovar, _ -> ovar |> OrderVariable.isSolved
                | _ -> true


        let timeIsSolved prs =
            prs
            |> toOrdVars
            |> function
                | _, Some ovar -> ovar |> OrderVariable.isSolved
                | _ -> true


        let applyToFrequency f (prs : Prescription) =
            match prs with
            | Discontinuous frq -> frq |> f |> Discontinuous
            | Timed (frq, tme) -> (frq |> f, tme) |> Timed
            | _ -> prs


        let frequencyIsCleared (prs: Prescription) =
            match prs |> toOrdVars with
            | Some frq, _ -> frq |> OrderVariable.isCleared
            | _ -> false


        let applyToTime f prs =
            match prs with
            | Timed (frq, tme) ->
                (frq, tme |> f)
                |> Timed
            | _ -> prs


        let setMinTime = applyToTime Time.setMinValue


        module Print =


                let frequencyTo md (p : Prescription) =
                    let toStr =
                        if md then Frequency.toValueUnitMarkdown -1
                        else Frequency.toValueUnitString -1
                    match p with
                    | Timed (frq, _)
                    | Discontinuous frq -> frq |> toStr
                    | _ -> ""


                let frequencyToString = frequencyTo false


                let frequencyToMd = frequencyTo true


                let timeTo md prec (p : Prescription) =
                    let toStr =
                        if md then Time.toValueUnitMarkdown prec
                        else Time.toValueUnitMarkdown prec
                    match p with
                    | OnceTimed tme -> tme |> toStr
                    | Timed (_, tme) -> tme |> toStr
                    | _ -> ""


                let timeToString = timeTo false


                let timeToMd = timeTo true


                let prescriptionTo md (p : Prescription) =
                    match p with
                    | Once -> "eenmalig"
                    | Continuous -> "continu"
                    | OnceTimed _ -> p |> timeTo md -1
                    | Discontinuous _ -> p |> frequencyToString
                    | Timed _     -> $"{p |> frequencyToString} {p |> timeTo md -1}"


                let prescriptionToString = prescriptionTo false


                let prescriptionToMd = prescriptionTo true



        /// Helper functions for the Prescription Dto
        module Dto =


            module Units = ValueUnit.Units
            module Id = WrappedString.Id
            module NM = Name


            type Dto () =
                member val IsOnce = false with get, set
                member val IsOnceTimed = false with get, set
                member val IsContinuous = false with get, set
                member val IsDiscontinuous = false with get, set
                member val IsTimed = false with get, set
                member val Frequency = OrderVariable.Dto.dto () with get, set
                member val Time = OrderVariable.Dto.dto () with get, set


            let fromDto (dto : Dto) =
                match dto.IsOnce,
                      dto.IsOnceTimed,
                      dto.IsContinuous,
                      dto.IsDiscontinuous,
                      dto.IsTimed with
                | false, false, true,  false, false -> Continuous
                | false, false, false, true,  false ->
                    dto.Frequency
                    |> Frequency.fromDto
                    |> Discontinuous
                | false, false, false, false, true  ->
                    (dto.Frequency |> Frequency.fromDto, dto.Time |> Time.fromDto)
                    |> Timed
                | true,  false, false, false, false -> Once
                | false, true,  false, false, false ->
                    dto.Time
                    |> Time.fromDto
                    |> OnceTimed
                | _ -> exn "dto is neither or both process, continuous, discontinuous or timed"
                       |> raise


            let toDto pres =
                let dto = Dto ()

                match pres with
                | Once -> dto.IsOnce <- true
                | Continuous -> dto.IsContinuous <- true
                | OnceTimed time ->
                    dto.IsOnceTimed <- true
                    dto.Time <- time |> Time.toDto
                | Discontinuous freq ->
                    dto.IsDiscontinuous <- true
                    dto.Frequency <- freq |> Frequency.toDto
                | Timed (freq, time) ->
                    dto.IsTimed   <- true
                    dto.Frequency <- freq |> Frequency.toDto
                    dto.Time      <- time |> Time.toDto

                dto


            /// <summary>
            /// Create a Prescription Dto
            /// </summary>
            /// <param name="n">The name of the Prescription</param>
            /// <remarks>
            /// Defaults to a Discontinuous Prescription
            /// </remarks>
            let dto n =
                let dto  = Dto ()
                let f, t =
                    n
                    |> Name.fromString
                    |> freqTime Unit.NoUnit Unit.NoUnit

                dto.Frequency <- f |> Frequency.toDto
                dto.Time <- t |> Time.toDto
                dto.IsDiscontinuous <- true

                dto


            /// Make the Prescription Dto Once
            let setToOnce (dto : Dto) =
                dto.IsOnce <- true
                dto.IsOnceTimed <- false
                dto.IsContinuous <- false
                dto.IsDiscontinuous <- false
                dto.IsTimed <- false

                dto


            /// Make the Prescription Dto OnceTimed
            let setToOnceTimed (dto : Dto) =
                dto.IsOnce <- false
                dto.IsOnceTimed <- true
                dto.IsContinuous <- false
                dto.IsDiscontinuous <- false
                dto.IsTimed <- false

                dto


            /// Make the Prescription Dto Continuous
            let setToContinuous (dto : Dto) =
                dto.IsOnce <- false
                dto.IsOnceTimed <- false
                dto.IsContinuous <- true
                dto.IsDiscontinuous <- false
                dto.IsTimed <- false

                dto


            /// Make the Prescription Dto Discontinuous
            let setToDiscontinuous (dto : Dto) =
                dto.IsOnce <- false
                dto.IsOnceTimed <- false
                dto.IsContinuous <- false
                dto.IsDiscontinuous <- true
                dto.IsTimed <- false

                dto


            /// Make the Prescription Dto Timed
            let setToTimed (dto : Dto) =
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
            | Start dt ->
                dt
                |> DateTime.formattedString "dd-MM-yy"
                |> sprintf "%s"
            | StartStop (start, stop) ->
                stop
                |> DateTime.formattedString "dd-MM-yy"
                |> sprintf "%s - %s" (start |> DateTime.formattedString "dd-MM-yy")


    [<RequireQualifiedAccess>]
    module OrderType =

        let toString = function
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
            { (ord |> inf) with
                Prescription =
                    ord.Prescription
                    |> Prescription.applyToFrequency f
            }

        let applyToTime f ord =
            { (ord |> inf) with
                Prescription =
                    ord.Prescription
                    |> Prescription.applyToTime f
            }


        let applyToComponents c f ord =
            let cmpPred =
                fun cmp ->
                    c |> String.isNullOrWhiteSpace ||
                    cmp
                    |> Orderable.Component.getName
                    |> Name.toString
                    |> String.equalsCapInsens c

            { (ord |> inf) with
                Orderable =
                    ord.Orderable
                    |> Orderable.applyToComponents cmpPred f
            }


        let applyToItems c i f ord =
            let cmpPred =
                fun cmp ->
                    c |> String.isNullOrWhiteSpace ||
                    cmp
                    |> Orderable.Component.getName
                    |> Name.toString
                    |> String.equalsCapInsens c

            let itmPred =
                fun cmp ->
                    i |> String.isNullOrWhiteSpace ||
                    cmp
                    |> Orderable.Item.getName
                    |> Name.toString
                    |> String.equalsCapInsens i

            { (ord |> inf) with
                Orderable =
                    ord.Orderable
                    |> Orderable.applyToComponents
                        cmpPred
                        (Orderable.Component.applyToItems itmPred f)
            }


        let apply ord propChange =
            match propChange with
            | PrescriptionFrequency f -> ord |> applyToFrequency f
            | PrescriptionTime f -> ord |> applyToTime f
            | OrderableQuantity f ->
                { (ord |> inf) with
                    Order.Orderable.OrderableQuantity =
                        ord.Orderable.OrderableQuantity |> f
                }
            | OrderableDoseCount f ->
                { (ord |> inf) with
                    Order.Orderable.DoseCount =
                        ord.Orderable.DoseCount |> f
                }
            | OrderableDose f ->
                { (ord |> inf) with
                    Order.Orderable.Dose =
                        ord.Orderable.Dose |> f
                }
            | ComponentQuantity (s, f) ->
                let f =
                    fun (cmp : Component) ->
                        { cmp with
                            ComponentQuantity =
                                cmp.ComponentQuantity |> f
                        }
                ord
                |> applyToComponents s f
            | ComponentOrderableQuantity (s, f) ->
                let f =
                    fun (cmp : Component) ->
                        { cmp with
                            OrderableQuantity =
                                cmp.OrderableQuantity |> f
                        }
                ord
                |> applyToComponents s f
            | ComponentOrderableCount (s, f) ->
                let f =
                    fun (cmp : Component) ->
                        { cmp with
                            OrderableCount =
                                cmp.OrderableCount |> f
                        }
                ord
                |> applyToComponents s f
            | ComponentOrderableConcentration (s, f) ->
                let f =
                    fun (cmp : Component) ->
                        { cmp with
                            OrderableConcentration =
                                cmp.OrderableConcentration |> f
                        }
                ord
                |> applyToComponents s f
            | ComponentDose (s, f) ->
                let f =
                    fun (cmp : Component) ->
                        { cmp with
                            Dose =
                                cmp.Dose |> f
                        }
                ord
                |> applyToComponents s f
            | ItemComponentQuantity (s, i, f) ->
                let f =
                    fun (itm : Item) ->
                        { itm with
                            ComponentQuantity =
                                itm.ComponentQuantity |> f
                        }
                ord
                |> applyToItems s i f
            | ItemComponentConcentration (s, i, f) ->
                let f =
                    fun (itm : Item) ->
                        { itm with
                            ComponentConcentration =
                                itm.ComponentConcentration |> f
                        }
                ord
                |> applyToItems s i f
            | ItemOrderableQuantity (s, i, f) ->
                let f =
                    fun (itm : Item) ->
                        { itm with
                            OrderableQuantity =
                                itm.OrderableQuantity |> f
                        }
                ord
                |> applyToItems s i f
            | ItemOrderableConcentration (s, i, f) ->
                let f =
                    fun (itm : Item) ->
                        { itm with
                            OrderableConcentration =
                                itm.OrderableConcentration |> f
                        }
                ord
                |> applyToItems s i f
            | ItemDose (s, i, f) ->
                let f =
                    fun (itm : Item) ->
                        { itm with
                            Dose =
                                itm.Dose |> f
                        }
                ord
                |> applyToItems s i f


        let proc propChanges ord =
            propChanges
            |> List.fold apply ord


    open MathNet.Numerics

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
    /// <param name="prs">The Prescription of the Order</param>
    /// <param name="rte">The Route of the Order</param>
    /// <param name="tme">The Time of the Order</param>
    /// <param name="sts">The StartStop of the Order</param>
    let create id adj_qty orb prs rte tme sts =
        {
            Id = id
            Adjust = adj_qty
            Orderable = orb
            Prescription = prs
            Route = rte
            Duration = tme
            StartStop = sts
        }


    /// <summary>
    /// Create a new `Order` with
    /// </summary>
    /// <param name="id">The id of the Order</param>
    /// <param name="orbN">The name of the Orderable</param>
    /// <param name="str_prs">A function to create a Prescription with a Name</param>
    /// <param name="route">The Route of the Order</param>
    let createNew id orbN str_prs route =
        let orb = Orderable.createNew id orbN
        let n = [id] |> Name.create

        let adj =
            Quantity.create (n |> Name.add Mapping.adj) Unit.NoUnit

        let tme =
            Time.create (n |> Name.add Mapping.ord) Unit.NoUnit

        let prs =
            n
            |> Name.add Mapping.prs
            |> str_prs

        let sts = DateTime.Now  |> StartStop.Start

        create (id |> Id.create) adj orb prs route tme sts


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
        |> List.append ("Orderable"::(ord.Orderable |> Orderable.toString))
        |> List.append ("Prescription"::(ord.Prescription |> Prescription.toString))
        |> List.append ("Route"::[ord.Route])
        |> List.filter (String.isNullOrWhiteSpace >> not)


    /// <summary>
    /// Return an Order as a list of strings where each string is
    /// a variable name with the value range and the Unit
    /// </summary>
    let toStringWithConstraints ord =
        [ (ord |> inf).Adjust |> Quantity.toStringWithConstraints ]
        |> List.append ("Orderable"::(ord.Orderable |> Orderable.toStringWithConstraints))
        |> List.append ("Prescription"::(ord.Prescription |> Prescription.toStringWithConstraints))
        |> List.append ("Route"::[ord.Route])
        |> List.filter (String.isNullOrWhiteSpace >> not)


    let print ord =
        ord
        |> toStringWithConstraints
        |> String.concat "\n"
        |> writeDebugMessage

        ord


    /// <summary>
    /// Return an Order as a list of OrderVariables
    /// </summary>
    let toOrdVars ord =
        let adj_qty = (ord |> inf).Adjust |> Quantity.toOrdVar
        let ord_tme = ord.Duration |> Time.toOrdVar

        let prs_vars =
            ord.Prescription
            |> Prescription.toOrdVars
            |> fun  (f, t) ->
                [f; t]
                |> List.choose id
        [
            adj_qty
            ord_tme
            yield! prs_vars
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
            Prescription = ord.Prescription |> Prescription.fromOrdVars ovars
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
            Prescription = ord.Prescription |> Prescription.applyConstraints
            Orderable = ord.Orderable |> Orderable.applyConstraints
        }


    let isEmpty = toOrdVars >> List.forall OrderVariable.isEmpty


    let hasConstraints = toOrdVars >> List.exists OrderVariable.hasConstraints


    let isWithinConstraints = toOrdVars >> List.forall OrderVariable.isWithinConstraints


    let checkConstraints = toOrdVars >> List.filter (OrderVariable.isWithinConstraints >> not)


    let isSolved =
        toOrdVars
        >> List.filter OrderVariable.hasConstraints
        >> List.forall OrderVariable.isSolved


    let hasValues ord =
        ord
        |> toOrdVars
        |> List.filter (fun ovar ->
            let n =
                ovar.Variable.Name
                |> Name.toString

            n |> String.contains "_cmp_qty" |> not &&
            n |> String.contains "_orb_cnt" |> not
        )
        |> List.exists OrderVariable.hasValues


    let checkOrderDose pred op ord : bool =
        let checkRte rte = rte |> Rate.toOrdVar |> pred
        let checkQty qty = qty |> Quantity.toOrdVar |> pred
        let checkPtm ptm = ptm |> PerTime.toOrdVar |> pred

        let rates =
            [
                ord.Orderable.Dose.Rate

                yield!
                    ord.Orderable.Components
                    |> List.map _.Dose
                    |> List.map _.Rate
            ]

        let qty =
            [
                ord.Orderable.Dose.Quantity
                for cmp in ord.Orderable.Components do
                    cmp.Dose.Quantity
                    for itm in cmp.Items do
                        if itm.Dose.Quantity |> Quantity.isNonZeroPositive |> not &&
                           (itm.Dose.Quantity |> Quantity.hasConstraints ||
                            itm.Dose.QuantityAdjust |> QuantityAdjust.hasConstraints) then
                            itm.Dose.Quantity
            ]

        let ptm =
            [
                ord.Orderable.Dose.PerTime
                for cmp in ord.Orderable.Components do
                    cmp.Dose.PerTime
                    for itm in cmp.Items do
                        if itm.Dose.PerTime |> PerTime.isNonZeroPositive |> not &&
                           (itm.Dose.PerTime |> PerTime.hasConstraints ||
                            itm.Dose.PerTimeAdjust |> PerTimeAdjust.hasConstraints) then
                            itm.Dose.PerTime
            ]

        let isOr = true |> op <| false

        match ord.Prescription with
        | Continuous ->
            if isOr then rates |> List.exists checkRte
            else rates |> List.forall checkRte
        | Timed (freq, _)
        | Discontinuous freq ->
            (freq |> Frequency.toOrdVar |> pred) &&
            (if isOr then qty |> List.exists checkQty else qty |> List.forall checkQty)
            |> op <|
            (if isOr then ptm |> List.exists checkPtm else ptm |> List.forall checkPtm)
        | Once ->
            qty |> List.forall checkQty
        | OnceTimed tme ->
            (tme |> Time.toOrdVar |> pred) &&
            (if isOr then qty |> List.exists checkQty else qty |> List.forall checkQty)


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
        { (ord |> inf) with
            Orderable =
                ord.Orderable
                |> Orderable.increaseQuantityIncrement
                       maxCount
                       incrs
        }


    /// <summary>
    /// Increase the Rate increment of an Order to a maximum
    /// count using a list of increments.
    /// </summary>
    /// <param name="maxCount">The maximum count</param>
    /// <param name="incrs">The list of increments</param>
    /// <param name="ord">The Order</param>
    let increaseRateIncrement maxCount incrs ord =
        { (ord |> inf) with
            Orderable =
                ord.Orderable
                |> Orderable.increaseRateIncrement maxCount incrs
        }


    let setNormDose sn nd ord =
        { (ord |> inf) with
            Orderable = ord.Orderable |> Orderable.setNormDose sn nd
        }


    /// <summary>
    /// Map an Order to a list of Equations using a Product Equation
    /// mapping and a Sum Equation mapping
    /// </summary>
    /// <param name="eqMapping">The Product Equation mapping and the Sum Equation mapping</param>
    /// <param name="ord">The Order</param>
    /// <returns>A list of OrderEquations</returns>
    let mapToOrderEquations eqMapping ord  =
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
                | h::rest ->
                    let h =
                        try
                            ovars |> List.find (fun v -> v.Variable.Name |> Name.toString = h)
                        with
                        | _ ->
                            let h = if h |> String.isNullOrWhiteSpace then "'empty string'" else h
                            failwith $"cannot find {h} from\n{eqMapping}\nin {ovars |> OrderVariable.getNames}"
                    let rest =
                        rest
                        |> List.map (fun s ->
                            try
                                ovars |> List.find (fun v -> v.Variable.Name |> Name.toString = s)
                            with
                            | _ ->
                                let s = if s |> String.isNullOrWhiteSpace then "'empty string'" else s
                                failwith $"cannot find {s} from\n{eqMapping}\nin {ovars |> OrderVariable.getNames}"
                        )
                    (h, rest) |> c
                | _ -> failwith $"cannot map {eqs}"
            )

        let sumEqs, prodEqs = eqMapping

        sumEqs
        |> map "+"
        |> List.append (prodEqs |> map "*")


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
                | OrderProductEquation (y, xs)
                | OrderSumEquation (y, xs) -> y::xs
            )
            |> List.distinct
            |> List.map OrderVariable.setUnit

        ord
        |> fromOrdVars ovars


    /// <summary>
    /// Solve an Order
    /// </summary>
    /// <param name="minMax">Whether to solve only for the minimum or maximum</param>
    /// <param name="printErr">Whether to print the error</param>
    /// <param name="logger">The logger</param>
    /// <param name="ord">The Order</param>
    /// <returns>A Result with the Order or a list error messages</returns>
    /// <raises>Any exception raised by the solver</raises>
    let solve minMax printErr logger (ord: Order) =

        let mapping =
            match ord.Prescription with
            | Once -> Mapping.once
            | Continuous -> Mapping.continuous
            | OnceTimed _ -> Mapping.onceTimed
            | Discontinuous _ -> Mapping.discontinuous
            | Timed _ -> Mapping.timed
            |> Mapping.getEquations
            |> Mapping.getEqsMapping ord

        let oEqs =
            ord
            |> mapToOrderEquations mapping

        try
            oEqs
            |> Solver.mapToSolverEqs
            |> fun eqs ->
                if minMax then eqs |> Solver.solveMinMax logger
                else eqs |> Solver.solve logger
            |> function
            | Ok eqs ->
                eqs
                |> Solver.mapToOrderEqs oEqs
                |> mapFromOrderEquations ord
                |> Ok
            | Error (eqs, m) ->
                eqs
                |> Solver.mapToOrderEqs oEqs
                |> mapFromOrderEquations ord
                |> fun ord -> Error (ord, m)
        with
        | e ->
            if printErr then
                oEqs
                |> mapFromOrderEquations ord
                |> toString
                |> List.iteri (printfn "%i. %s")

            let msg = [ e |> Informedica.GenSolver.Lib.Types.Exceptions.UnexpectedException ]
            Error (ord, msg)


    /// <summary>
    /// Solve an Order for only the minimum and maximum values
    /// </summary>
    /// <param name="printErr">Whether to print the error</param>
    /// <param name="logger">The logger</param>
    let solveMinMax printErr logger = solve true printErr logger


    /// <summary>
    /// Solve an Order for all values
    /// </summary>
    /// <param name="printErr">Whether to print the error</param>
    /// <param name="logger">The logger</param>
    let solveOrder printErr logger = solve false printErr logger


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

        if (ord |> inf).Prescription |> Prescription.isContinuous then ord
        else
            let orbQty = ord.Orderable.OrderableQuantity |> Quantity.toOrdVar
            // the increments used to increase
            let incrs u =
                [ 1N/20N; 1N/10N; 1N/2N; 1N; 5N; 10N; 20N ]
                |> List.map (ValueUnit.singleWithUnit u)
                |> List.map ValueRange.Increment.create
            // only increase incr for volume units
            if orbQty.Variable
               |> Variable.getUnit
               |> Option.map (ValueUnit.Group.unitToGroup >> ((=) Group.VolumeGroup) >> not)
               |> Option.defaultValue false then ord
            else
                ord
                |> increaseQuantityIncrement maxQtyCount (incrs Units.Volume.milliLiter)
                |> solveMinMax true logger
                |> function
                | Error (_, errs) ->
                    errs
                    |> List.iter (fun e ->
                        writeErrorMessage $"{e}"
                    )
                    ord // original order
                | Ok ord ->
                    ord // increased increment order
                    |> increaseRateIncrement
                        maxRateCount
                        (incrs (Units.Volume.milliLiter |> Units.per Units.Time.hour))
                    |> solveMinMax false logger

                    |> function
                    | Error (_, errs) ->
                        errs
                        |> List.iter (fun e ->
                            writeErrorMessage $"{e}"
                        )
                        ord // increased increment order
                    | Ok ord ->
                        ord // calculated order
        |> Ok


    let maximizeRate logger ord =
        // only maximize for timed orders
        if (ord |> inf).Prescription |> Prescription.isTimed |> not then ord
        else
            match ord.Orderable.Dose.Rate |> Rate.toOrdVar with
            | rte when rte |> OrderVariable.isSolved |> not ->
                let maxRte =
                    rte
                    |> OrderVariable.minIncrMaxToValues (Some 100)
                    |> OrderVariable.setMaxValue

                let ovars =
                    ord
                    |> toOrdVars
                    // first check if max rate can be set
                    |> List.map (fun ovar ->
                        if ovar.Variable.Name = rte.Variable.Name then maxRte
                        else ovar
                    )

                ord
                |> fromOrdVars ovars
                |> solveOrder false logger
                |> Result.map (fun ord ->
                    writeDebugMessage $"max rate set to: {maxRte |> OrderVariable.toString true}"
                    ord
                )
                |> Result.defaultValue ord
            | _ -> ord


    let minimizeTime logger (ord: Order) =
        if ord.Prescription |> Prescription.isTimed |> not then ord
        else
            { (ord |> inf) with
                Prescription = ord.Prescription |> Prescription.setMinTime
            }
            |> solveOrder false logger
            |> Result.defaultValue ord


    /// <summary>
    /// Loop through all the OrderVariables in an Order to
    /// turn min incr max to values
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="ord">The Order</param>
    let minIncrMaxToValues useAll minTime logger ord =
        let rec loop ord =
            // the flag makes sure that if one order variable
            // is set to values, then the rest is skipped and
            // proceed to solving the order
            let mutable flag = false

            let ovars =
                ord
                |> toOrdVars
                |> List.map (fun ovar ->
                    if flag ||
                       ovar.Constraints.Incr |> Option.isNone ||
                       ovar.Variable.Values |> ValueRange.isMinIncrMax |> not then ovar
                    else
                        flag <- true

                        let n =
                            if useAll then None
                            else
                                match ord.Prescription with
                                | Continuous -> 100
                                | Once
                                | Discontinuous _ -> 10
                                | OnceTimed _
                                | Timed _ ->
                                    if ord.Orderable.Components |> List.length > 2 then 5 else 10
                                |> Some

                        ovar
                        |> OrderVariable.minIncrMaxToValues n
                )

            if not flag then ord
            else
                ord
                |> fromOrdVars ovars
                |> solveOrder false logger // could possible restrict to solve variable
                |> function
                    | Ok ord  -> loop ord
                    | Error err ->
                        err
                        |> snd
                        |> List.map (sprintf "%A")
                        |> String.concat "\n"
                        |> writeErrorMessage

                        ord

        if minTime then ord |> minimizeTime logger
        else ord
        |> loop


    let solveNormDose logger normDose ord =
        match normDose with
        | Informedica.GenForm.Lib.Types.NormQuantityAdjust (Informedica.GenForm.Lib.Types.SubstanceLimitTarget sn, _)
        | Informedica.GenForm.Lib.Types.NormPerTimeAdjust (Informedica.GenForm.Lib.Types.SubstanceLimitTarget sn, _) ->
            ord
            |> setNormDose sn normDose
            |> solveOrder false logger
        | _ -> ord |> Ok


    let setDoseUnit sn du ord =
        { (ord |> inf) with Orderable = ord.Orderable |> Orderable.setDoseUnit sn du }


    let isCleared = toOrdVars >> List.exists OrderVariable.isCleared


    type Status = Processed of Order | NotProcessed of Order


    let processClearedOrder logger ord =
        match (ord |> inf).Prescription with
        | Continuous ->
            match ord.Orderable.Dose |> Dose.isRateCleared,
                  ord.Orderable |> Orderable.isItemOrderableConcentrationCleared,
                  ord.Orderable |> Orderable.isItemDoseRateCleared with
            | false, false, true
            | true, false, false ->
                ord
                |> OrderPropertyChange.proc
                    [
                        // clear all dose rates
                        OrderableDose Dose.setRateToNonZeroPositive
                        ComponentDose ("", Dose.setRateToNonZeroPositive)
                        ItemDose ("", "", Dose.setRateToNonZeroPositive)
                        // apply dose rate constraints again
                        OrderableDose Dose.applyDoseRateConstraints
                        ComponentDose ("", Dose.applyDoseRateConstraints)
                        ItemDose ("", "", Dose.applyDoseRateConstraints)
                    ]
                |> solveMinMax true logger
                |> Result.map (minIncrMaxToValues true true logger)
            | false, true, false ->
                ord
                |> OrderPropertyChange.proc
                    [
                        // clear all item dose rates
                        ComponentDose ("", Dose.setRateToNonZeroPositive)
                        ComponentDose ("", Dose.applyDoseRateConstraints)

                        ItemDose ("", "", Dose.setRateToNonZeroPositive)
                        ItemDose ("", "", Dose.applyDoseRateConstraints)
                        // clear the item and component orderable quantities
                        // causing these to be recalculated
                        ComponentOrderableConcentration ("", Concentration.setToNonZeroPositive)
                        ComponentOrderableCount ("", OrderVariable.Count.setToNonZeroPositive)
                        ComponentOrderableQuantity ("", Quantity.setToNonZeroPositive)
                        ComponentOrderableQuantity ("", Quantity.applyConstraints)

                        ItemOrderableConcentration ("", "", Concentration.setToNonZeroPositive)
                        ItemOrderableQuantity ("", "", Quantity.setToNonZeroPositive)
                    ]
                |> print
                |> solveMinMax true logger
                |> Result.map (minIncrMaxToValues true true logger)
            | b ->
                $"""===> no match for continuous cleared {b}""" |> writeDebugMessage
                ord |> solveOrder true logger
        | Discontinuous frq ->
            match frq |> Frequency.isCleared,
                  ord.Orderable |> Orderable.isItemDoseQuantityCleared,
                  ord.Orderable |> Orderable.isItemDosePerTimeCleared with
            | true, false, false ->
                ord |> Ok
                (*
                |> setFrequencyNonZeroPositive
                |> setDoseQuantityNonZeroPositive
                |> setDosePerTimeNonZeroPositive
                |> applyDosePerTimeConstraints
                |> setStandardFrequency
                |> solveOrder true logger
                *)

            | false, true, false ->
                ord |> Ok
                (*
                |> setFrequencyNonZeroPositive
                |> setStandardFrequency
                |> setDoseQuantityNonZeroPositive
                |> applyOrderableConstraints
                |> applyComponentConstraints
                |> setDosePerTimeNonZeroPositive
                |> applyDosePerTimeConstraints
                |> solveOrder true logger
                *)

            | b ->
                $"""===> no match for discontinuous cleared {b}""" |> writeDebugMessage
                ord |> solveOrder true logger
        | _ -> ord |> solveOrder true logger


    let (|IsEmpty|NoDoseValues|HasDoseValues|DoseSolvedNotCleared|DoseSolvedAndCleared|) ord =
        match ord with
        | _ when ord |> isEmpty -> IsEmpty
        | _ when ord |> doseHasValues |> not -> NoDoseValues
        | _ when ord |> doseHasValues && ord |> doseIsSolved |> not -> HasDoseValues
        | _ when ord |> doseIsSolved && ord |> isCleared |> not -> DoseSolvedNotCleared
        | _ when ord |> doseIsSolved && ord |> isCleared -> DoseSolvedAndCleared
        | _ ->
            $"""

Order is in an unknown state:
{ord |> print |> ignore}

            """
            |> failwith


    let printState = function
        | IsEmpty -> "IsEmpty"
        | NoDoseValues -> "NoDoseValues"
        | HasDoseValues -> "HasDoseValues"
        | DoseSolvedNotCleared -> "DoseSolvedNotCleared"
        | DoseSolvedAndCleared -> "DoseSolvedAndCleared"


    let processPipeLine logger normDose ord =
        let procIf msg pred procF ord =

            match ord with
            | Ok (Processed ord) -> ord |> Processed |> Ok
            | Ok (NotProcessed ord) ->
                if ord |> pred then
                    $"""

=== PIPELINE ===
{msg}
=== END PIPELINE ===

                    """
                    |> writeDebugMessage

                    ord |> procF |> Result.map Processed
                else ord |> NotProcessed |> Ok
            | Error _ -> ord

        let calcMinMax recalc =
            applyConstraints >> Ok
            >> Result.bind (solveMinMax true logger)
            >> Result.bind (fun ord ->
                if recalc then ord |> Ok
                else
                    ord
                    |> increaseIncrements logger 10 10
            )
            >> Result.bind (fun ord ->
                match normDose with
                | Some nd ->
                    ord
                    |> minIncrMaxToValues false true logger
                    |> solveNormDose logger nd
                | None -> Ok ord
            )

        let calcValues = minIncrMaxToValues false true logger >> Ok

        let isEmpty = function | IsEmpty -> true | _ -> false

        let notSolveNoValues = function | NoDoseValues -> true | _ -> false

        let solvedHasValues = function | HasDoseValues -> true | _ -> false

        let solvedNotCleared = function | DoseSolvedNotCleared -> true | _ -> false

        let solvedAndCleared = function | DoseSolvedAndCleared -> true | _ -> false

        ord
        |> (NotProcessed >> Ok)
        |> procIf "order is empty: calc minmax" isEmpty (calcMinMax false)
        |> procIf "order not solved has no values: calc values" notSolveNoValues calcValues
        |> procIf "order not solved has values: solve order" solvedHasValues (solveOrder true logger)
        |> procIf "order is solved and not cleared: recalc order" solvedNotCleared (calcMinMax true >> Result.bind calcValues)
        |> procIf "order is solved and has cleared prop: process cleared order" solvedAndCleared (processClearedOrder logger)
        |> Result.map (function | Processed ord | NotProcessed ord -> ord)


    module Print =

        open Informedica.GenOrder.Lib


        let printOrderToTableFormat
            useAdj
            printMd
            sns ord =

                let findItem sn =
                    ord.Orderable.Components
                    |> List.collect _.Items
                    |> List.tryFind (fun i -> i.Name |> Name.toString |> String.equalsCapInsens sn)

                let itms =
                    sns
                    |> Array.filter String.notEmpty
                    |> Array.choose findItem

                let withBrackets s =
                    if s |> String.isNullOrWhiteSpace then s
                    else
                        $"({s})"

                let addPerDosis s =
                    if s |> String.isNullOrWhiteSpace then s
                    else
                        $"{s}/dosis"

                let freq =
                    ord.Prescription
                    |> Prescription.Print.frequencyTo printMd

                let pres =
                    match ord.Prescription with
                    | Continuous ->
                        if itms |> Array.isEmpty then
                            [|
                                [|
                                    // the orderable dose quantity
                                    ord.Orderable
                                    |> Orderable.Print.doseQuantityTo printMd -1
                                    // the orderable dose adjust quantity
                                    if useAdj then
                                        ord.Orderable
                                        |> Orderable.Print.doseQuantityAdjustTo printMd 2

                                |]
                            |]
                        else
                            itms
                            |> Array.map (fun itm ->
                                [|
                                    itm.Name |> Name.toString
                                    // item dose per rate
                                    if useAdj then
                                        itm
                                        |> Orderable.Item.Print.itemDoseRateAdjustTo printMd 3

                                        if itm.Dose.RateAdjust |> RateAdjust.isSolved then
                                            itm.Dose |> Dose.Print.doseRateAdjustConstraints 3
                                            |> withBrackets
                                    else
                                        itm
                                        |> Orderable.Item.Print.itemDoseRateTo printMd 3

                                        if itm.Dose.Rate |> Rate.isSolved then
                                            itm.Dose |> Dose.Print.doseRateConstraints 3
                                            |> withBrackets
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
                                    |>
                                    Orderable.Print.doseQuantityTo printMd -1
                                    // the orderable dose adjust quantity
                                    let s =
                                        if useAdj then
                                            ord.Orderable
                                            |> Orderable.Print.dosePerTimeAdjustTo printMd 2
                                        else
                                            ord.Orderable
                                            |> Orderable.Print.dosePerTimeTo printMd -1
                                    if s |> String.notEmpty then
                                        "="
                                        s
                                |]
                            |]
                        else
                            itms
                            |> Array.mapi (fun i itm ->
                                [|
                                    // the frequency
                                    if i = 0 then freq else ""
                                    // the name of the item
                                    itm.Name |> Name.toString
                                    // the item dose quantity
                                    itm
                                    |> Orderable.Item.Print.itemDoseQuantityTo printMd 3

                                    if useAdj then
                                        let s =
                                            itm
                                            |> Orderable.Item.Print.itemDosePerTimeAdjustTo printMd 3
                                        if s |> String.notEmpty then
                                            "="
                                            s

                                        if itm.Dose.PerTimeAdjust |> PerTimeAdjust.isSolved then
                                            [
                                                itm.Dose |> Dose.Print.dosePerTimeAdjustConstraints 3
                                                |> withBrackets
                                                itm.Dose |> Dose.Print.doseQuantityAdjustConstraints 3
                                                |> addPerDosis
                                                |> withBrackets
                                            ]
                                            |> List.tryFind String.notEmpty
                                            |> Option.defaultValue ""
                                    else
                                        let s =
                                            itm
                                            |> Orderable.Item.Print.itemDosePerTimeTo printMd 3
                                        if s |> String.notEmpty then
                                            "="
                                            s

                                        if itm.Dose.PerTime |> PerTime.isSolved then
                                            [
                                                itm.Dose |> Dose.Print.dosePerTimeConstraints 3
                                                |> withBrackets
                                                itm.Dose |> Dose.Print.doseQuantityConstraints 3
                                                |> addPerDosis
                                                |> withBrackets
                                            ]
                                            |> List.tryFind String.notEmpty
                                            |> Option.defaultValue ""
                                |]
                            )

                    | Once
                    | OnceTimed _ ->
                        if itms |> Array.isEmpty then
                            [|
                                [|
                                    // the orderable dose quantity
                                    ord.Orderable
                                    |> Orderable.Print.doseQuantityTo printMd -1
                                    // the orderable dose adjust quantity
                                    if useAdj then
                                        "="
                                        ord.Orderable
                                        |> Orderable.Print.doseQuantityAdjustTo printMd -1

                                |]
                            |]
                        else
                            itms
                            |> Array.map (fun itm ->
                                [|
                                    itm.Name |> Name.toString
                                    // the item dose quantity
                                    itm
                                    |> Orderable.Item.Print.itemDoseQuantityTo printMd 3

                                    // the item dose adjust quantity
                                    if useAdj then
                                        "="
                                        itm
                                        |> Orderable.Item.Print.itemDoseQuantityAdjustTo printMd 3

                                        if itm.Dose.QuantityAdjust |> QuantityAdjust.isSolved then
                                            itm.Dose |> Dose.Print.doseQuantityAdjustConstraints 3
                                            |> addPerDosis
                                            |> withBrackets

                                    else
                                        if itm.Dose.Quantity |> Quantity.isSolved then
                                            itm.Dose |> Dose.Print.doseQuantityConstraints 3
                                            |> addPerDosis
                                            |> withBrackets

                                |]
                            )

                let prep =
                    ord.Orderable.Components
                    |> List.toArray
                    |> Array.mapi (fun i1 c ->
                        let cmpQty = c |> Orderable.Component.Print.componentOrderableQuantityTo printMd -1
                        let cItms =
                           c.Items
                           |> List.filter (fun i -> sns |> Array.exists (String.equalsCapInsens (i.Name |> Name.toString)))
                           |> List.toArray

                        [|
                            if i1 > 0 || cItms |> Array.isEmpty then
                                [|
                                    [|
                                        if cmpQty |> String.notEmpty then
                                            if i1 = 0 && cItms |> Array.isEmpty |> not then c.Shape
                                            else
                                                $"{c.Shape} ({c.Name |> Name.toString})"
                                            cmpQty
                                            ""
                                            ""
                                    |]
                                |]
                            else
                                cItms
                                |> Array.mapi (fun i2 itm ->
                                    [|
                                        if cmpQty |> String.notEmpty then
                                            if i1 = 0 && i2 = 0 then
                                                c.Shape
                                                c |> Orderable.Component.Print.componentOrderableQuantityTo printMd -1
                                            else
                                                ""
                                                ""

                                            let itmQty = itm |> Orderable.Item.Print.itemComponentConcentrationTo printMd -1
                                            if itmQty |> String.notEmpty then
                                                itm.Name |> Name.toString
                                                itmQty
                                    |]
                                )
                        |]
                    )
                    |> Array.collect id
                    |> Array.collect id

                let adm =
                    match ord.Prescription with
                    | Once
                    | OnceTimed _
                    | Discontinuous _
                    | Timed _ ->
                        let tme =
                            ord.Prescription
                            |> Prescription.Print.timeToString 2

                        if itms |> Array.isEmpty then
                            [|
                                [|
                                    // the frequency
                                    if ord.Prescription |> Prescription.hasFrequency then freq

                                    ord.Orderable
                                    |> Orderable.Print.doseQuantityTo printMd -1
                                    // if timed add rate and time
                                    if ord.Prescription |> Prescription.hasTime then
                                        ord.Orderable |> Orderable.Print.doseRateTo printMd -1
                                        tme //ord.Prescription |> Prescription.Print.timeToMd -1
                                |]
                            |]
                        else
                            itms
                            |> Array.mapi (fun i itm ->
                                [|
                                    // the frequency
                                    if ord.Prescription |> Prescription.hasFrequency then
                                       if i = 0 then
                                           freq
                                           ord.Orderable |> Orderable.Print.doseQuantityTo printMd -1
                                       else
                                            ""
                                            ""
                                    else
                                        if i = 0 then
                                            ord.Orderable |> Orderable.Print.doseQuantityTo printMd -1
                                        else
                                            ""

                                    let itmQty = itm |> Orderable.Item.Print.orderableQuantityTo printMd 3
                                    if itmQty |> String.notEmpty then
                                        itm.Name |> Name.toString
                                        itm |> Orderable.Item.Print.orderableQuantityTo printMd 3

                                        if i = 0 then
                                            "in"
                                            ord.Orderable |> Orderable.Print.orderableQuantityTo printMd -1
                                        else
                                            ""
                                            ""

                                    // if timed then add rate and time
                                    if ord.Prescription |> Prescription.hasTime &&
                                       itmQty |> String.notEmpty then
                                        if i = 0 then
                                            "="
                                            ord.Orderable |> Orderable.Print.doseRateTo printMd -1
                                            tme //ord.Prescription |> Prescription.Print.timeToMd -1
                                            |> withBrackets
                                        else
                                            ""
                                            ""
                                            ""
                                |]
                            )
                    | Continuous ->
                        let orbQty = ord.Orderable |> Orderable.Print.orderableQuantityTo printMd -1
                        if itms |> Array.isEmpty then
                            [|
                                [|
                                    orbQty
                                    ord.Orderable |> Orderable.Print.doseRateTo printMd -1
                                |]
                            |]
                        else
                            itms
                            |> Array.mapi (fun i itm ->
                                [|
                                    itm.Name |> Name.toString
                                    itm |> Orderable.Item.Print.orderableQuantityTo printMd 3

                                    if i = 0 then
                                        if orbQty |> String.notEmpty then
                                            "in"
                                            ord.Orderable |> Orderable.Print.orderableQuantityTo printMd -1
                                            "="
                                        ord.Orderable |> Orderable.Print.doseRateTo printMd -1
                                    else
                                        if orbQty |> String.notEmpty then
                                            ""
                                            ""
                                            ""
                                        ""
                                |]
                            )

                pres
                , prep
                , adm


        let printOrderTo
            useAdj
            printMd
            sns (ord: Order) =
                let prs, prp, adm = ord |> printOrderToTableFormat useAdj printMd sns
                let add xs =
                    let plus = [| " + " |]
                    xs
                    |> Array.fold (fun acc x ->
                        if acc |> Array.isEmpty then x
                        else
                            x
                            |> Array.append plus
                            |> Array.append acc
                    ) [||]
                    |> String.concat " "

                prs |> add ,
                prp |> add ,
                adm |> add



        /// <summary>
        /// Print an Order to a string using an array of strings
        /// to pick the Orderable Items to print.
        /// </summary>
        let printOrderToString useAdj =
            printOrderTo
                useAdj
                false


        /// <summary>
        /// Print an Order to a markdown string using an array of strings
        /// to pick the Orderable Items to print.
        /// </summary>
        let printOrderToMd printAdj =
            printOrderTo
                printAdj
                true


    module Dto =

        type Dto (id , n) =
            member val Id = id with get, set
            member val Adjust = OrderVariable.Dto.dto () with get, set
            member val Orderable = Orderable.Dto.dto id n with get, set
            member val Prescription = Prescription.Dto.dto n with get, set
            member val Route = "" with get, set
            member val Duration = OrderVariable.Dto.dto () with get, set
            member val Start = DateTime.now () with get, set
            member val Stop : DateTime option = None with get, set


        let fromDto (dto : Dto) =
            let id = dto.Id |> Id.create
            let adj_qty = dto.Adjust |> Quantity.fromDto
            let ord_tme = dto.Duration |> Time.fromDto
            let orb = dto.Orderable |> Orderable.Dto.fromDto
            let prs = dto.Prescription |> Prescription.Dto.fromDto
            let sts =
                match dto.Stop with
                | Some dt -> (dto.Start, dt) |> StartStop.StartStop
                | None -> dto.Start |> StartStop.Start

            create id adj_qty orb prs dto.Route ord_tme sts


        let toDto ord =
            let id = (ord |> inf).Id |> Id.toString
            let n = ord.Orderable.Name |> Name.toString
            let dto = Dto (id, n)

            dto.Adjust <- ord.Adjust |> Quantity.toDto
            dto.Duration <- ord.Duration |> Time.toDto
            dto.Orderable <- ord.Orderable |> Orderable.Dto.toDto
            dto.Prescription <- ord.Prescription |> Prescription.Dto.toDto
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
        /// <param name="str_prs">A function to create a Prescription with a Name</param>
        let dto id orbN rte cmps str_prs =
            let dto =
                createNew id orbN str_prs rte
                |> toDto

            dto.Orderable.Components <-
                [
                    for cmpN, shape, itms in cmps do
                        let c = Orderable.Component.Dto.dto id orbN cmpN shape
                        c.Items <-
                            itms
                            |> List.map (Orderable.Item.Dto.dto id orbN cmpN)
                        c
                ]

            dto


        let cleanDose (dto : Dto) =
            dto.Duration |> OrderVariable.Dto.clean

            if dto.Prescription.IsDiscontinuous || dto.Prescription.IsTimed then
                dto.Prescription.Frequency |> OrderVariable.Dto.clean
            if dto.Prescription.IsTimed then
                dto.Prescription.Time |> OrderVariable.Dto.clean
            if not dto.Prescription.IsContinuous then
                dto.Orderable.OrderableQuantity |> OrderVariable.Dto.clean

            dto.Orderable.Dose |> Dose.Dto.clean

            dto.Orderable.Components
                |> List.iter (fun c ->
                    c.OrderableQuantity |> OrderVariable.Dto.clean
                    c.OrderableConcentration |> OrderVariable.Dto.clean
                    c.OrderableCount |> OrderVariable.Dto.clean
                    c.Dose |> Dose.Dto.clean
                    c.Items
                    |> List.iter (fun i ->
                        i.OrderableQuantity |> OrderVariable.Dto.clean
                        i.OrderableConcentration |> OrderVariable.Dto.clean
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
        let continuous id orbN rte cmps  =
            Prescription.continuous Unit.NoUnit Unit.NoUnit
            |> dto id orbN rte cmps


        /// <summary>
        /// Create a new Order Dto with a Once Prescription
        /// </summary>
        /// <param name="id">The id of the Order</param>
        /// <param name="orbN">The name of the Orderable</param>
        /// <param name="rte">The Route of the Order</param>
        /// <param name="cmps">The Components of the Orderable</param>
        let once id orbN rte cmps =
            Prescription.once Unit.NoUnit Unit.NoUnit
            |> dto id orbN rte cmps


        /// <summary>
        /// Create a new Order Dto with a OnceTimed Prescription
        /// </summary>
        /// <param name="id">The id of the Order</param>
        /// <param name="orbN">The name of the Orderable</param>
        /// <param name="rte">The Route of the Order</param>
        /// <param name="cmps">The Components of the Orderable</param>
        let onceTimed id orbN rte cmps =
            Prescription.onceTimed Unit.NoUnit Unit.NoUnit
            |> dto id orbN rte cmps


        /// <summary>
        /// Create a new Order Dto with a Discontinuous Prescription
        /// </summary>
        /// <param name="id">The id of the Order</param>
        /// <param name="orbN">The name of the Orderable</param>
        /// <param name="rte">The Route of the Order</param>
        /// <param name="cmps">The Components of the Orderable</param>
        let discontinuous id orbN rte cmps =
            Prescription.discontinuous Unit.NoUnit Unit.NoUnit
            |> dto id orbN rte cmps


        /// <summary>
        /// Create a new Order Dto with a Timed Prescription
        /// </summary>
        /// <param name="id">The id of the Order</param>
        /// <param name="orbN">The name of the Orderable</param>
        /// <param name="rte">The Route of the Order</param>
        /// <param name="cmps">The Components of the Orderable</param>
        let timed id orbN rte cmps=
            Prescription.timed Unit.NoUnit Unit.NoUnit
            |> dto id orbN rte cmps


        let setToOnce (dto : Dto) =
            dto.Prescription <-
                dto.Prescription
                |> Prescription.Dto.setToOnce
            dto


        let setToOnceTimed (dto : Dto) =
            dto.Prescription <-
                dto.Prescription
                |> Prescription.Dto.setToOnceTimed
            dto


        let setToContinuous (dto : Dto) =
            dto.Prescription <-
                dto.Prescription
                |> Prescription.Dto.setToContinuous
            dto


        let setToDiscontinuous (dto : Dto) =
            dto.Prescription <-
                dto.Prescription
                |> Prescription.Dto.setToDiscontinuous
            dto


        let setToTimed (dto : Dto) =
            dto.Prescription <-
                dto.Prescription
                |> Prescription.Dto.setToTimed
            dto


        let toString dto =
            dto
            |> fromDto
            |> toString
            |> String.concat "\n"