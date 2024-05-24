namespace Informedica.GenOrder.Lib




/// Types and functions that deal with an order.
/// An `Order` models the `Prescription` of an
/// `Orderable` with a `StartStop` start date and
/// stop date.
[<RequireQualifiedAccess>]
module Order =


    open System
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
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
                            ConsoleWriter.writeErrorMessage
                                $"could not match {e}"
                                true false
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
            let toOrdVars (dos : Dose) =
                let qty = dos.Quantity |> Quantity.toOrdVar
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
            let fromOrdVars ovars (dos: Dose) =
                let qty = dos.Quantity |> Quantity.fromOrdVar ovars
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
            let applyConstraints (dos: Dose) =
                let qty = dos.Quantity |> Quantity.applyConstraints
                let ptm = dos.PerTime |> PerTime.applyConstraints
                let rte = dos.Rate |> Rate.applyConstraints
                let tot = dos.Total |> Total.applyConstraints
                let qty_adj = dos.QuantityAdjust |> QuantityAdjust.applyConstraints
                let ptm_adj = dos.PerTimeAdjust |> PerTimeAdjust.applyConstraints
                let rte_adj = dos.RateAdjust |> RateAdjust.applyConstraints
                let tot_adj = dos.TotalAdjust |> TotalAdjust.applyConstraints

                create qty ptm rte tot qty_adj ptm_adj rte_adj tot_adj


            /// <summary>
            /// Increase the increment of a Dose to a maximum
            /// count using a list of increments.
            /// </summary>
            /// <param name="maxCount">The maximum count</param>
            /// <param name="incrs">The list of increments</param>
            /// <param name="dos">The Dose</param>
            let increaseIncrement maxCount incrs (dos: Dose) =
                let qty = dos.Quantity
                let ptm = dos.PerTime
                let rte = dos.Rate |> Rate.increaseIncrement maxCount incrs
                let tot = dos.Total
                let qty_adj = dos.QuantityAdjust
                let ptm_adj = dos.PerTimeAdjust
                let rte_adj = dos.RateAdjust
                let tot_adj = dos.TotalAdjust

                create qty ptm rte tot qty_adj ptm_adj rte_adj tot_adj


            let setNormDose nd (dos: Dose) =
                let qty_adj, ptm_adj =
                    match nd with
                    | Informedica.GenForm.Lib.Types.NormQuantityAdjust (_, vu) ->
                        dos.QuantityAdjust |> QuantityAdjust.setNearestValue vu,
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



            let setDoseUnit du (dos : Dose) =
                let qty = dos.Quantity |> Quantity.setFirstUnit du
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


                let doseTo get toStr (d: Dose) = d |> get |> toStr


                let doseQuantityTo md prec =
                    let toStr =
                        if md then Quantity.toValueUnitMarkdown prec
                        else Quantity.toValueUnitString prec

                    doseTo (_.Quantity) toStr


                let doseQuantityToString = doseQuantityTo false


                let doseQuantityToMd = doseQuantityTo true


                let doseQuantityAdjustTo md prec =
                    let toStr =
                        if md then QuantityAdjust.toValueUnitMarkdown prec
                        else QuantityAdjust.toValueUnitString prec

                    doseTo (_.QuantityAdjust) toStr


                let doseQuantityAdjustToString = doseQuantityAdjustTo false


                let doseQuantityAdjustToMd = doseQuantityAdjustTo false


                let dosePerTimeTo md prec =
                    let toStr =
                        if md then PerTime.toValueUnitMarkdown prec
                        else PerTime.toValueUnitString prec

                    doseTo (_.PerTime) toStr


                let dosePerTimeToString = dosePerTimeTo false


                let dosePerTimeToMd = dosePerTimeTo true


                let dosePerTimeAdjustTo md prec =
                    let toStr =
                        if md then PerTimeAdjust.toValueUnitMarkdown prec
                        else PerTimeAdjust.toValueUnitString prec

                    doseTo (_.PerTimeAdjust) toStr


                let dosePerTimeAdjustToString = doseQuantityAdjustTo false


                let dosePerTimeAdjustToMd = dosePerTimeAdjustTo true


                let doseRateTo md prec =
                    let toStr =
                        if md then Rate.toValueUnitMarkdown prec
                        else Rate.toValueUnitString prec

                    doseTo (_.Rate) toStr


                let doseRateToString = doseRateTo false


                let doseRateToMd = doseRateTo true


                let doseRateAdjustTo md prec =
                    let toStr =
                        if md then RateAdjust.toValueUnitMarkdown prec
                        else RateAdjust.toValueUnitString prec

                    doseTo (_.RateAdjust) toStr


                let doseRateAdjustToString = doseRateAdjustTo false


                let doseRateAdjustToMd = doseRateAdjustTo true


                let doseConstraints get prec (d: Dose) =
                    d
                    |> get
                    |> (_.Constraints)
                    |> OrderVariable.Constraints.toMinMaxString prec


                let doseQuantityConstraints =
                    fun d -> d.Quantity |> Quantity.toOrdVar
                    |> doseConstraints


                let doseQuantityAdjustConstraints =
                    fun d -> d.QuantityAdjust |> QuantityAdjust.toOrdVar
                    |> doseConstraints


                let dosePerTimeConstraints =
                    fun d -> d.PerTime |> PerTime.toOrdVar
                    |> doseConstraints


                let dosePerTimeAdjustConstraints =
                    fun d -> d.PerTimeAdjust |> PerTimeAdjust.toOrdVar
                    |> doseConstraints


                let doseRateConstraints =
                    fun d -> d.Rate |> Rate.toOrdVar
                    |> doseConstraints


                let doseRateAdjustConstraints =
                    fun d -> d.RateAdjust |> RateAdjust.toOrdVar
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


            /// Apply **f** to an `item`
            let apply f (itm: Item) = itm |> f


            /// Utility method to facilitate type inference
            let get = apply id


            /// Get the `Name` of an `Item`
            let getName itm = (itm |> get).Name


            /// Get the `Item` dose
            let getDose itm = (itm |> get).Dose


            /// <summary>
            /// Return an Item as a list of OrderVariables
            /// </summary>
            /// <param name="itm">The Item</param>
            let toOrdVars itm =
                let itm_cmp_qty = (itm |> get).ComponentQuantity |> Quantity.toOrdVar
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
                let cmp_qty = (itm |> get).ComponentQuantity |> Quantity.fromOrdVar ovars
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
                let cmp_qty = (itm |> get).ComponentQuantity |> Quantity.applyConstraints
                let orb_qty = itm.OrderableQuantity          |> Quantity.applyConstraints
                let cmp_cnc = itm.ComponentConcentration     |> Concentration.applyConstraints
                let orb_cnc = itm.OrderableConcentration     |> Concentration.applyConstraints
                let dos = itm.Dose |> Dose.applyConstraints

                create itm.Name cmp_qty orb_qty cmp_cnc orb_cnc dos


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


            /// <summary>
            /// Create a string list from a Item where each string is
            /// a variable name with the value range and the Unit
            /// </summary>
            let toString = toOrdVars >> List.map (OrderVariable.toString false)


            module Print =


                let private getToStr get toStr (itm : Item) =
                    itm
                    |> get
                    |> toStr


                let concentrationTo get md prec =
                    let toStr =
                        if md then Concentration.toValueUnitMarkdown prec
                        else Concentration.toValueUnitString prec

                    getToStr get toStr


                let itemComponentConcentrationTo =
                    concentrationTo (_.ComponentConcentration)


                let itemComponentConcentrationToString = itemComponentConcentrationTo false


                let itemComponentConcentrationToMd = itemComponentConcentrationTo true


                let itemOrderableConcentrationTo =
                    concentrationTo (_.OrderableConcentration)


                let itemOrderableConcentrationToString = itemOrderableConcentrationTo false


                let itemOrderableConcentrationToMd = itemOrderableConcentrationTo true


                let quantityTo get md prec =
                    let toStr =
                        if md then Quantity.toValueUnitMarkdown prec
                        else Quantity.toValueUnitString prec

                    getToStr get toStr


                let componentQuantityTo =
                    quantityTo (_.ComponentQuantity)


                let itemComponentQuantityToString = componentQuantityTo false


                let itemComponentQuantityToMd = componentQuantityTo true


                let orderableQuantityTo =
                    quantityTo (_.OrderableQuantity)


                let itemOrderableQuantityToString = orderableQuantityTo false


                let itemOrderableQuantityToMd = itemOrderableConcentrationTo true


                let itemDoseQuantityTo md prec (itm: Item) =
                    itm.Dose |> Dose.Print.doseQuantityTo md prec


                let itemDoseQuantityToString = itemDoseQuantityTo false 3


                let itemDoseQuantityToMd = itemDoseQuantityTo true 3


                let itemDoseQuantityAdjustTo md prec (itm: Item) =
                    itm.Dose |> Dose.Print.doseQuantityAdjustTo md prec


                let itemDoseQuantityAdjustToString = itemDoseQuantityAdjustTo false 3


                let itemDoseQuantityAdjustToMd = itemDoseQuantityAdjustTo true 3


                let itemDosePerTimeTo md prec (itm: Item) =
                    itm.Dose |> Dose.Print.dosePerTimeTo md prec


                let itemDosePerTimeToString = itemDosePerTimeTo false 3


                let itemDosePerTimeToMd = itemDosePerTimeTo true 3


                let itemDosePerTimeAdjustTo md prec (itm: Item) =
                    itm.Dose |> Dose.Print.dosePerTimeAdjustTo md prec


                let itemDosePerTimeToAdjustString = itemDosePerTimeAdjustTo false 3


                let itemDosePerTimeAdjustToMd = itemDosePerTimeAdjustTo true 3


                let itemDoseRateTo md prec (itm: Item) =
                    itm.Dose |> Dose.Print.doseRateTo md prec


                let itemDoseRateToString = itemDoseRateTo false 3


                let itemDoseRateToMd = itemDoseRateTo true 3


                let itemDoseRateAdjustTo md prec (itm: Item) =
                    itm.Dose |> Dose.Print.doseRateAdjustTo md prec


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


            /// Apply **f** to a `Component` **comp**
            let apply f (comp: Component) = comp |> f


            /// Utility to facilitate type inference
            let get = apply id


            /// Get the name of a `Component`
            let getName cmp = (cmp |> get).Name


            /// Get the `Item`s in an `Component`
            let getItems cmp = (cmp |> get).Items


            /// <summary>
            /// Return a Component as a list of OrderVariables
            /// </summary>
            /// <param name="cmp">The Component</param>
            let toOrdVars cmp =
                let cmp_qty = (cmp |> get).ComponentQuantity |> Quantity.toOrdVar
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
                let cmp_qty = (cmp |> get).ComponentQuantity |> Quantity.fromOrdVar ovars
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
                let cmp_qty = (cmp |> get).ComponentQuantity |> Quantity.applyConstraints
                let orb_qty = cmp.OrderableQuantity          |> Quantity.applyConstraints
                let orb_cnt = cmp.OrderableCount             |> Count.applyConstraints
                let orb_cnc = cmp.OrderableConcentration     |> Concentration.applyConstraints
                let ord_qty = cmp.OrderQuantity              |> Quantity.applyConstraints
                let ord_cnt = cmp.OrderCount                 |> Count.applyConstraints
                let dos = cmp.Dose |> Dose.applyConstraints

                cmp.Items
                |> List.map Item.applyConstraints
                |> create cmp.Id cmp.Name cmp.Shape cmp_qty orb_qty orb_cnt ord_qty ord_cnt orb_cnc dos



            /// <summary>
            /// Increase the increment of a Component to a maximum
            /// count using a list of increments.
            /// </summary>
            /// <param name="maxCount">The maximum count</param>
            /// <param name="incrs">The list of increments</param>
            /// <param name="cmp">The Component</param>
            let increaseIncrement maxCount incrs cmp =
                let cmp_qty = (cmp |> get).ComponentQuantity
                let orb_qty = cmp.OrderableQuantity |> Quantity.increaseIncrement maxCount incrs
                let orb_cnt = cmp.OrderableCount
                let orb_cnc = cmp.OrderableConcentration
                let ord_qty = cmp.OrderQuantity
                let ord_cnt = cmp.OrderCount
                let dos = cmp.Dose

                cmp.Items
                |> create cmp.Id cmp.Name cmp.Shape cmp_qty orb_qty orb_cnt ord_qty ord_cnt orb_cnc dos


            let setDoseUnit sn du cmp =
                { cmp with
                    Items = cmp.Items |> List.map (Item.setDoseUnit sn du)
                }


            let setNormDose sn nd cmp =
                { cmp with
                    Items = cmp.Items |> List.map (Item.setNormDose sn nd)
                }


            /// <summary>
            /// Create a string list from a Component where each string is
            /// a variable name with the value range and the Unit
            /// </summary>
            let toString = toOrdVars >> List.map (OrderVariable.toString false)



            module Print =


                let private getToStr get toStr (c : Component) =
                    c
                    |> get
                    |> toStr


                let quantityTo get md prec =
                    let toStr =
                        if md then Quantity.toValueUnitMarkdown prec
                        else Quantity.toValueUnitString prec

                    getToStr get toStr


                let componentQuantityTo =
                    quantityTo (_.ComponentQuantity)


                let componentQuantityToString = componentQuantityTo false


                let componentQuantityToMd = componentQuantityTo true


                let componentOrderableQuantityTo =
                    quantityTo (_.OrderableQuantity)


                let componentOrderableQuantityToString = componentOrderableQuantityTo false


                let componentOrderableQuantityToMd = componentOrderableQuantityTo true


                let componentOrderQuantityTo =
                    quantityTo (_.OrderQuantity)

                let componentOrderQuantityToString = componentOrderQuantityTo false


                let componentOrderQuantityToMd = componentOrderQuantityTo true


                let countTo get md =
                    let toStr =
                        if md then Count.toValueUnitMarkdown -1
                        else Count.toValueUnitString -1

                    getToStr get toStr


                let componentOrderableCountTo =
                    countTo (_.OrderableCount)


                let componentOrderableCountToString = componentOrderableCountTo false


                let componentOrderableCountToMd = componentOrderableCountTo true


                let componentOrderCountTo =
                    countTo (_.OrderCount)


                let componentOrderCountToString = componentOrderCountTo false


                let componentOrderCountToMd = componentOrderCountTo true


                let concentrationTo get md =
                    let toStr =
                        if md then Concentration.toValueUnitMarkdown -1
                        else Concentration.toValueUnitString -1

                    getToStr get toStr


                let componentOrderableConcentrationTo =
                    concentrationTo (_.OrderableConcentration)


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


        /// Apply **f** to `Orderable` `ord`
        let apply f (orb: Orderable) = orb |> f


        /// Utility function to facilitate type inference
        let get = apply id


        /// Get the name of the `Orderable`
        let getName orb = (orb |> get).Name


        /// Get the Components in an `Orderable`
        let getComponents orb = (orb |> get).Components


        /// Get the `Orderable` dose
        let getDose orb = (orb |> get).Dose



        /// <summary>
        /// Return an Orderable as a list of OrderVariables
        /// </summary>
        /// <param name="orb">The Orderable</param>
        let toOrdVars orb =
            let ord_qty = (orb |> get).OrderQuantity |> Quantity.toOrdVar
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
            let ord_qty = (orb |> get).OrderQuantity |> Quantity.fromOrdVar ovars
            let orb_qty = orb.OrderableQuantity      |> Quantity.fromOrdVar ovars
            let ord_cnt = orb.OrderCount             |> Count.fromOrdVar ovars
            let dos_cnt = orb.DoseCount              |> Count.fromOrdVar ovars
            let dos = orb.Dose |> Dose.fromOrdVars ovars

            orb.Components
            |> List.map (Component.fromOrdVars ovars)
            |> create orb.Name orb_qty ord_qty ord_cnt dos_cnt dos


        /// <summary>
        /// Apply constraints to an Orderable
        /// </summary>
        /// <param name="orb">The Orderable</param>
        let applyConstraints orb =
            let ord_qty = (orb |> get).OrderQuantity |> Quantity.applyConstraints
            let orb_qty = orb.OrderableQuantity      |> Quantity.applyConstraints
            let ord_cnt = orb.OrderCount             |> Count.applyConstraints
            let dos_cnt = orb.DoseCount              |> Count.applyConstraints
            let dos = orb.Dose |> Dose.applyConstraints

            orb.Components
            |> List.map Component.applyConstraints
            |> create orb.Name orb_qty ord_qty ord_cnt dos_cnt dos


        /// <summary>
        /// Return a list of strings from an Orderable where each string is
        /// a variable name with the value range and the Unit
        /// </summary>
        let toString = toOrdVars >> List.map (OrderVariable.toString false)


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
                [
                    orb.OrderableQuantity
                    yield!
                        orb.Components
                        |> List.map _.OrderableQuantity
                ]
                |> List.forall Quantity.hasIncrement
                |> not
                then orb
            else
                // first calculate the minimum increment increase for the orderable and components
                let ord_qty = (orb |> get).OrderQuantity
                let orb_qty = orb.OrderableQuantity |> Quantity.increaseIncrement maxCount incrs
                let ord_cnt = orb.OrderCount
                let dos_cnt = orb.DoseCount
                let dos = orb.Dose //|> Dose.increaseIncrement incr

                orb.Components
                |> List.map (Component.increaseIncrement maxCount incrs)
                |> create orb.Name orb_qty ord_qty ord_cnt dos_cnt dos

                |> fun newOrb ->
                    [
                        (newOrb.OrderableQuantity |> Quantity.toOrdVar |> OrderVariable.getVar).Values
                        yield! newOrb.Components
                        |> List.map (fun c ->
                            (c.OrderableQuantity |> Quantity.toOrdVar |> OrderVariable.getVar).Values
                        )
                    ]
                    |> List.choose Variable.ValueRange.getIncr
                    |> function
                        | [] -> orb
                        | incrs ->
                            if incrs |> List.length <> ((orb.Components |> List.length) + 1) then orb
                            else
                                let incr =
                                    incrs
                                    |> List.minBy (fun i ->
                                        i
                                        |> Variable.ValueRange.Increment.toValueUnit
                                        |> ValueUnit.getBaseValue
                                    )

                                // apply the minimum increment increase to the orderable and components
                                let ord_qty = (orb |> get).OrderQuantity
                                let orb_qty = orb.OrderableQuantity |> Quantity.increaseIncrement maxCount [incr]
                                let ord_cnt = orb.OrderCount
                                let dos_cnt = orb.DoseCount
                                let dos = orb.Dose //|> Dose.increaseIncrement incr

                                orb.Components
                                |> List.map (Component.increaseIncrement maxCount [incr])
                                |> create orb.Name orb_qty ord_qty ord_cnt dos_cnt dos


        /// <summary>
        /// Increase the Rate increment of an Orderable to a maximum
        /// count using a list of increments.
        /// </summary>
        /// <param name="maxCount">The maximum count</param>
        /// <param name="incrs">The list of increments</param>
        /// <param name="orb">The Orderable</param>
        let increaseRateIncrement maxCount incrs orb =
            let ord_qty = (orb |> get).OrderQuantity
            let orb_qty = orb.OrderableQuantity //|> Quantity.increaseIncrement incr
            let ord_cnt = orb.OrderCount
            let dos_cnt = orb.DoseCount
            let dos = orb.Dose |> Dose.increaseIncrement maxCount incrs

            orb.Components
            |> create orb.Name orb_qty ord_qty ord_cnt dos_cnt dos


        let setDoseUnit sn du orb =
            { orb with
                Components = orb.Components |> List.map (Component.setDoseUnit sn du)
            }


        let setNormDose sn nd orb =
            { orb with
                Components = orb.Components |> List.map (Component.setNormDose sn nd)
            }


        module Print =


            let getToStr get toStr (orb : Orderable) =
                orb
                |> get
                |> toStr


            let quantityTo get md prec =
                let toStr =
                    if md then Quantity.toValueUnitMarkdown prec
                    else Quantity.toValueUnitString prec

                getToStr get toStr


            let orderableQuantityTo =
                quantityTo (_.OrderableQuantity)

            let orderableQuantityToString = orderableQuantityTo false


            let orderableQuantityToMd = orderableQuantityTo true


            let orderQuantityTo =
                quantityTo (_.OrderQuantity)


            let orderQuantityToString = orderQuantityTo false


            let orderQuantityToMd = orderQuantityTo true


            let countTo get md =
                let toStr =
                    if md then Count.toValueUnitMarkdown -1
                    else Count.toValueUnitString -1

                getToStr get toStr


            let orderCountTo =
                countTo (_.OrderCount)


            let orderCountToString = orderCountTo false


            let orderCountToMd = orderCountTo true


            let doseQuantityTo md prec (orb: Orderable) =
                orb.Dose |> Dose.Print.doseQuantityTo md prec

            let doseQuantityToString = doseQuantityTo false -1


            let doseQuantityToMd = doseQuantityTo true -1

            let doseQuantityAdjustTo md prec (orb: Orderable) =
                orb.Dose |> Dose.Print.doseQuantityAdjustTo md prec


            let doseQuantityAdjustToString = doseQuantityAdjustTo false


            let doseQuantityAdjustToMd = doseQuantityAdjustTo true


            let dosePerTimeTo md prec (orb: Orderable) =
                orb.Dose |> Dose.Print.dosePerTimeTo md prec

            let dosePerTimeToString = dosePerTimeTo false -1


            let dosePerTimeToMd = dosePerTimeTo true -1


            let dosePerTimeAdjustTo md prec (orb: Orderable) =
                orb.Dose |> Dose.Print.dosePerTimeAdjustTo md prec

            let dosePerTimeAdjustToString = dosePerTimeAdjustTo false


            let dosePerTimeAdjustToMd = dosePerTimeAdjustTo true


            let doseRateTo md prec (orb: Orderable) =
                orb.Dose |> Dose.Print.doseRateTo md prec

            let doseRateToString = doseRateTo false -1


            let doseRateToMd = doseRateTo true -1


            let doseRateAdjustTo md prec (orb: Orderable) =
                orb.Dose |> Dose.Print.doseRateAdjustTo md prec


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
                frq |> Frequency.toOrdVar |> Some, tme |> Time.toOrdVar |> Some


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


        let setDoseUnit sn du ord =
            { ord with
                Orderable = ord.Orderable |> Orderable.setDoseUnit sn du
            }


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
                | Timed(frq, tme)     -> [frq |> Frequency.toString; tme |> Time.toString]



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


    /// Apply `f` to `Order` `ord`
    let apply f (ord: Order) = ord |> f


    /// Utility function to facilitate type inference
    let get = apply id


    /// Get the order id
    let getId ord = (ord |> get).Id


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
    let getAdjust ord = (ord |> get).Adjust


    /// Get the Orderable of an `Order`
    let getOrderable ord = (ord |> get).Orderable


    /// <summary>
    /// Return an Order as a list of strings where each string is
    /// a variable name with the value range and the Unit
    /// </summary>
    let toString (ord: Order) =
        [ ord.Adjust |> Quantity.toString ]
        |> List.append (Orderable.Literals.orderable::(ord.Orderable |> Orderable.toString))
        |> List.append ("Prescription"::(ord.Prescription |> Prescription.toString))
        |> List.append ("Route"::[ord.Route])
        |> List.filter (String.isNullOrWhiteSpace >> not)


    /// <summary>
    /// Return an Order as a list of OrderVariables
    /// </summary>
    let toOrdVars (ord : Order) =
        let adj_qty = ord.Adjust |> Quantity.toOrdVar
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
    let fromOrdVars ovars (ord : Order) =
        { ord with
            Adjust = ord.Adjust |> Quantity.fromOrdVar ovars
            Duration = ord.Duration |> Time.fromOrdVar ovars
            Prescription = ord.Prescription |> Prescription.fromOrdVars ovars
            Orderable = ord.Orderable |> Orderable.fromOrdVars ovars
        }


    /// <summary>
    /// Apply constraints to an Order
    /// </summary>
    /// <param name="ord">The Order</param>
    let applyConstraints (ord : Order) =
        try
            { ord with
                Adjust = ord.Adjust |> Quantity.applyConstraints
                Duration = ord.Duration |> Time.applyConstraints
                Prescription = ord.Prescription |> Prescription.applyConstraints
                Orderable = ord.Orderable |> Orderable.applyConstraints
            }
        with
        | _ ->
            let s = ord |> toString |> String.concat "\n"
            ConsoleWriter.writeErrorMessage
                $"couldn't apply constraints:\n{s}"
                true false
            reraise()


    let isSolved (ord: Order) =
        let qty =
              ord.Orderable.Dose.Quantity
              |> Quantity.toOrdVar
              |> OrderVariable.isSolved
        let rte =
              ord.Orderable.Dose.Rate
              |> Rate.toOrdVar
              |> OrderVariable.isSolved
        qty || rte


    /// <summary>
    /// Increase the Quantity increment of an Order to a maximum
    /// count using a list of increments.
    /// </summary>
    /// <param name="maxCount">The maximum count</param>
    /// <param name="incrs">The list of increments</param>
    /// <param name="ord">The Order</param>
    let increaseQuantityIncrement maxCount incrs (ord : Order) =
        { ord with
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
    let increaseRateIncrement maxCount incrs (ord : Order) =
        { ord with
            Orderable =
                ord.Orderable
                |> Orderable.increaseRateIncrement maxCount incrs
        }


    let setNormDose sn nd ord =
        { ord with
            Orderable = ord.Orderable |> Orderable.setNormDose sn nd
        }


    /// <summary>
    /// Map an Order to a list of Equations using a Product Equation
    /// mapping and a Sum Equation mapping
    /// </summary>
    /// <param name="eqMapping">The Product Equation mapping and the Sum Equation mapping</param>
    /// <param name="ord">The Order</param>
    /// <returns>A list of OrderEquations</returns>
    let mapToOrderEquations eqMapping (ord: Order)  =
        let ovars = ord |> toOrdVars

        let map repl eqMapping =
            let eqs, c =
                match eqMapping with
                | SumMapping eqs -> eqs, OrderSumEquation
                | ProductMapping eqs -> eqs, OrderProductEquation
            eqs
            |> List.map (String.replace "=" repl)
            |> List.map (String.split repl >> List.map String.trim)
            |> List.map (fun xs ->
                match xs with
                | h::rest ->
                    let h =
                        try
                            ovars |> List.find (fun v -> v.Variable.Name |> Name.toString = h)
                        with
                        | _ -> failwith $"cannot find {h} in {ovars}"
                    let rest =
                        rest
                        |> List.map (fun s ->
                            try
                                ovars |> List.find (fun v -> v.Variable.Name |> Name.toString = s)
                            with
                            | _ -> failwith $"cannot find {s} in {ovars}"
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
    let mapFromOrderEquations (ord: Order) eqs =
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
        let ord =
            if minMax then ord |> applyConstraints
            else ord

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
                |> fun eqs -> Error (eqs, m)

        with
        | e ->
            if printErr then
                oEqs
                |> mapFromOrderEquations ord
                |> toString
                |> List.iteri (printfn "%i. %s")

            raise e


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
    /// Loop through all the OrderVariables in an Order to
    /// turn min incr max to values and subsequently solve the Order.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="ord">The Order</param>
    let minIncrMaxToValues logger (ord: Order) =
        let rec loop runAgain ord =
            if not runAgain then ord
            else
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
                                match ord.Prescription with
                                | OnceTimed _ -> 5
                                | Once -> 50
                                | Continuous -> 100
                                | Discontinuous _ -> 50
                                | Timed _ -> 5

                            ovar
                            |> OrderVariable.minIncrMaxToValues n
                    )
                if not flag then ord
                else
                    ord
                    |> fromOrdVars ovars
                    |> solveOrder false logger // could possible restrict to solve variable
                    |> function
                        | Ok ord -> loop flag ord
                        | Error _ -> ord

        loop true ord


    /// <summary>
    /// Increase the Orderable Quantity Increment of an Order.
    /// This allows speedy calculation by avoiding large amount
    /// of possible values.
    /// </summary>
    /// <param name="logger">The OrderLogger to use</param>
    /// <param name="maxQtyCount">The maximum count of the Orderable Quantity</param>
    /// <param name="maxRateCount">The maximum count of the Rate</param>
    /// <param name="ord">The Order to increase the increment of</param>
    let increaseIncrements logger maxQtyCount maxRateCount (ord : Order) =
        if ord.Prescription |> Prescription.isContinuous then ord
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

            |> increaseRateIncrement
                maxRateCount
                (incrs (Units.Volume.milliLiter |> Units.per Units.Time.hour))
            |> solveMinMax false logger
            |> function
            | Error (_, errs) ->
                errs
                |> List.iter (fun e ->
                    ConsoleWriter.writeErrorMessage
                        $"{e}"
                        true
                        false
                )
                ord // original order
            | Ok ord ->
                (*
                ConsoleWriter.writeInfoMessage
                    $"""=== solved order with increased increment === {ord |> toString |> String.concat "\n"}"""
                    true
                    false
                *)

                ord // increased increment order
                |> solveOrder false logger

                |> function
                | Error (_, errs) ->
                    errs
                    |> List.iter (fun e ->
                        ConsoleWriter.writeErrorMessage
                            $"{e}"
                            true
                            false
                    )
                    ord // increased increment order
                | Ok ord ->
                    (*
                    let s = ord |> toString |> String.concat "\n"
                    ConsoleWriter.writeInfoMessage
                        $"solved order with increased increment and values:\n {s}"
                        true
                        false
                    *)

                    ord // calculated order
        |> Ok


    let solveNormDose logger normDose ord =
        match normDose with
        | Informedica.GenForm.Lib.Types.NormQuantityAdjust (Informedica.GenForm.Lib.Types.SubstanceLimitTarget sn, _)
        | Informedica.GenForm.Lib.Types.NormPerTimeAdjust (Informedica.GenForm.Lib.Types.SubstanceLimitTarget sn, _) ->
            ord
            |> setNormDose sn normDose
            |> solveOrder false logger
        | _ -> ord |> Ok


    let setDoseUnit sn du ord =
        { ord with Orderable = ord.Orderable |> Orderable.setDoseUnit sn du }


    module Print =

        open Informedica.GenOrder.Lib


        let printOrderToTableFormat
            useAdj
            printMd
            sns (ord : Order) =

                let findItem sn =
                    ord.Orderable.Components
                    |> List.collect (_.Items)
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
                        [|
                            if c.Items |> List.isEmpty then
                                [|
                                    [|
                                        if cmpQty |> String.notEmpty then
                                            if i1 = 0 then c.Shape
                                            else
                                                $"{c.Shape} ({c.Name |> Name.toString})"
                                            cmpQty
                                            ""
                                            ""
                                    |]
                                |]
                            else
                                c.Items
                                |> List.toArray
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


        let toDto (ord : Order) =
            let id = ord.Id |> Id.toString
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




