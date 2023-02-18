namespace Informedica.GenOrder.Lib




/// Types and functions that deal with an order.
/// An `Order` models the `Prescription` of an
/// `Orderable` with a `StartStop` start date and
/// stop date.
module Order =

    open System
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


        let getEquations indx =
            Web.getDataFromGenPres "Equations"
            |> Array.skip 1
            |> Array.filter (fun xs -> xs[indx] = "x")
            |> Array.map (Array.item 1)
            |> Array.toList


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
                            printfn $"could not match {e}"
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
                    |> ProductMapping

                sumEqs, prodEqs



    /// Types and functions to deal
    /// with an `Orderable`, i.e. something
    /// that can be ordered.
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


            /// Turn an `Item` to `VariableUnit`s
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



            /// Turn an `Item` to a list of `string`s,
            /// each string containing the variable
            /// `Name`, `ValueRange` and `Unit`
            let toString = toOrdVars >> List.map (OrderVariable.toString false)


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

                    dto.Quantity <-
                        dos.Quantity
                        |> Quantity.toDto
                    dto.PerTime <-
                        dos.PerTime
                        |> PerTime.toDto
                    dto.Rate <-
                        dos.Rate
                        |> Rate.toDto
                    dto.Total <-
                        dos.Total
                        |> Total.toDto
                    dto.QuantityAdjust <-
                        dos.QuantityAdjust
                        |> QuantityAdjust.toDto
                    dto.PerTimeAdjust <-
                        dos.PerTimeAdjust
                        |> PerTimeAdjust.toDto
                    dto.RateAdjust <-
                        dos.RateAdjust
                        |> RateAdjust.toDto
                    dto.TotalAdjust <-
                        dos.TotalAdjust
                        |> TotalAdjust.toDto

                    dto


                let dto () = Dto ()



        /// Type and functions that models an
        /// `Order` `Item` that is contained in
        /// a `Component`
        module Item =

            module Quantity = OrderVariable.Quantity
            module Concentration = OrderVariable.Concentration
            module Total = OrderVariable.Total
            module Rate = OrderVariable.Rate


            /// Create an item with
            ///
            /// * **id**: the order id
            /// * **n**: the name of the item
            /// * **cmp_qty**: the quantity of the item in a component
            /// * **orb_qty**: the quantity of the item in an orderable
            /// * **cmp_cnc**: the item concentration in a component
            /// * **orb_cnc**: the item concentration in an orderable
            /// * **dos**: the item dose
            let create n cmp_qty orb_qty cmp_cnc orb_cnc dos =
                {
                    Name = n
                    ComponentQuantity = cmp_qty
                    OrderableQuantity = orb_qty
                    ComponentConcentration = cmp_cnc
                    OrderableConcentration = orb_cnc
                    Dose = dos
                }


            /// Create a new item with
            ///
            /// **id**: the order id
            /// **n**: the string name of the item
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


            /// Utility method to facitilitate type inference
            let get = apply id


            /// Get the `Name` of an `Item`
            let getName itm = (itm |> get).Name


            /// Get the `Item` dose
            let getDose itm = (itm |> get).Dose


            /// Turn an `Item` to `VariableUnit`s
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


            let fromOrdVars ovars itm =
                let cmp_qty = (itm |> get).ComponentQuantity |> Quantity.fromOrdVar ovars
                let orb_qty = itm.OrderableQuantity          |> Quantity.fromOrdVar ovars
                let cmp_cnc = itm.ComponentConcentration     |> Concentration.fromOrdVar ovars
                let orb_cnc = itm.OrderableConcentration     |> Concentration.fromOrdVar ovars
                let dos = itm.Dose |> Dose.fromOrdVars ovars

                create itm.Name cmp_qty orb_qty cmp_cnc orb_cnc dos


            let applyConstraints itm =
                let cmp_qty = (itm |> get).ComponentQuantity |> Quantity.applyConstraints
                let orb_qty = itm.OrderableQuantity          |> Quantity.applyConstraints
                let cmp_cnc = itm.ComponentConcentration     |> Concentration.applyConstraints
                let orb_cnc = itm.OrderableConcentration     |> Concentration.applyConstraints
                let dos = itm.Dose |> Dose.applyConstraints

                create itm.Name cmp_qty orb_qty cmp_cnc orb_cnc dos


            /// Turn an `Item` to a list of `string`s,
            /// each string containing the variable
            /// `Name`, `ValueRange` and `Unit`
            let toString = toOrdVars >> List.map (OrderVariable.toString false)



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


                let dto id orbN cmpN itmN =
                    createNew id orbN cmpN itmN
                    |> toDto



        /// Types and functions to model a
        /// `Component` in an `Orderable`.
        /// A `Component` contains a list
        /// of `Item`s
        module Component =

            module Name = Name
            module Quantity = OrderVariable.Quantity
            module Concentration = OrderVariable.Concentration
            module Count = OrderVariable.Count


            /// Create a component with
            ///
            /// * `id`: the order id
            /// * `n`: the name of the component
            /// * `cmp_qty`: quantity of component
            /// * `orb_qty`: quantity of component in orderable
            /// * `orb_cnt`: count of component in orderable
            /// * `ord_qty`: quantity of component in order
            /// * `ord_cnt`: count of component in order
            /// * `orb_cnc`: concentration of component in orderable
            /// * `dos`: component dose
            /// * `dos_adj`: adjusted dose of component
            /// * `ii`: list of `Item`s in a component
            let create id nm sh cmp_qty orb_qty orb_cnt ord_qty ord_cnt orb_cnc dos ii =
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

            /// Create a new component with
            /// * `id`: the id of the component
            /// * `n`: the name of the component
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


            /// Map a `Component` **cmp**
            /// to `VariableUnit`s
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


            /// Map a `Component` **cmp**
            /// to `VariableUnit`s
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


            /// Map a `Component` **cmp**
            /// to `VariableUnit`s
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


            /// Create a string list from a
            /// component where each string is
            /// a variable name with the value range
            /// and the Unit
            let toString = toOrdVars >> List.map (OrderVariable.toString false)



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

                let dto id orbN cmpN shape =
                    createNew id orbN cmpN shape
                    |> toDto



        module Quantity = OrderVariable.Quantity
        module Concentration = OrderVariable.Concentration
        module Count = OrderVariable.Count


        /// Create an `Orderable` with
        ///
        /// * nm: the name of the orderable
        /// * orb\_qty: quantity of the orderable
        /// * ord\_qty: quantity of orderable in the order
        /// * orb\_cnt: the count of orderable in the order
        /// * dos: the orderable dose
        /// * dos\_adj: the adjusted orderable dose
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

        /// Create a new `Orderable` with a `Component` list
        /// `cl`, and
        /// * `Orderable`unit `un` and
        /// * component unit `cu`
        /// * time unit `tu`
        /// * adjust unit `adj`
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


        /// Get the `Component`s in an `Orderable`
        let getComponents orb = (orb |> get).Components


        /// Get the `Orderable` dose
        let getDose orb = (orb |> get).Dose


        /// Map an `Orderable` **orb** to
        /// `VariableUnit`s
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


        /// Map an `Orderable` **orb** to
        /// `VariableUnit`s
        let fromOrdVars ovars orb =
            let ord_qty = (orb |> get).OrderQuantity |> Quantity.fromOrdVar ovars
            let orb_qty = orb.OrderableQuantity      |> Quantity.fromOrdVar ovars
            let ord_cnt = orb.OrderCount             |> Count.fromOrdVar ovars
            let dos_cnt = orb.DoseCount              |> Count.fromOrdVar ovars
            let dos = orb.Dose |> Dose.fromOrdVars ovars

            orb.Components
            |> List.map (Component.fromOrdVars ovars)
            |> create orb.Name orb_qty ord_qty ord_cnt dos_cnt dos


        /// Map an `Orderable` **orb** to
        /// `VariableUnit`s
        let applyConstraints orb =
            let ord_qty = (orb |> get).OrderQuantity |> Quantity.applyConstraints
            let orb_qty = orb.OrderableQuantity      |> Quantity.applyConstraints
            let ord_cnt = orb.OrderCount             |> Count.applyConstraints
            let dos_cnt = orb.DoseCount              |> Count.applyConstraints
            let dos = orb.Dose |> Dose.applyConstraints

            orb.Components
            |> List.map Component.applyConstraints
            |> create orb.Name orb_qty ord_qty ord_cnt dos_cnt dos


        /// Turn an `Orderable` `ord` into
        /// a list of strings.
        let toString = toOrdVars >> List.map (OrderVariable.toString false)



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


            let dto id orbN =
                createNew id orbN
                |> toDto



    module Prescription =


        module Frequency = OrderVariable.Frequency
        module Time = OrderVariable.Time


        /// Create `Frequency` and `Time` with name generated by string list **n**
        let freqTime tu1 tu2 n =  (Frequency.create n tu1, Time.create n tu2)


        /// Create a continuous `Prescription` with name generated by string list **n**
        let continuous tu1 tu2 n =
            let _, _ = n |> freqTime tu1 tu2 in Continuous


        /// Create a discontinuous `Prescription` with name generated by string list **n**
        let discontinuous tu1 tu2 n =
            let frq, _ = n |> freqTime tu1 tu2 in frq |> Discontinuous


        /// Create a timed `Prescription` with name generated by string list **n**
        let timed tu1 tu2 n =
            let frq, tme = n |> freqTime tu1 tu2 in (frq, tme) |> Timed


        /// Check whether a `Prescription` is continuous
        let isContinuous = function | Continuous -> true | _ -> false


        /// Check whether a `Prescription` is discontinuous with a time
        let isTimed = function | Timed _ -> true | _ -> false


        /// Turn `Prescription` **prs** into `VariableUnit`s to
        /// be used in equations
        let toOrdVars prs =
            match prs with
            | Continuous -> None, None
            | Discontinuous frq ->
                frq |> Frequency.toOrdVar |> Some, None
            | Timed(frq, tme)     ->
                frq |> Frequency.toOrdVar |> Some, tme |> Time.toOrdVar |> Some


        let fromOrdVars ovars prs =
            match prs with
            | Continuous -> prs
            | Discontinuous frq ->
                frq |> Frequency.fromOrdVar ovars |> Discontinuous
            | Timed(frq, tme)     ->
                (frq |> Frequency.fromOrdVar ovars,
                tme |> Time.fromOrdVar ovars)
                |> Timed


        let applyConstraints prs =
            match prs with
            | Continuous -> prs
            | Discontinuous frq ->
                frq |> Frequency.applyConstraints |> Discontinuous
            | Timed(frq, tme)     ->
                (frq |> Frequency.applyConstraints,
                tme |> Time.applyConstraints)
                |> Timed



        /// Turn a `Prescription` **prs** into
        /// a string list
        let toString (prs: Prescription) =
                match prs with
                | Continuous -> ["Continuous"]
                | Discontinuous frq -> [frq |> Frequency.toString]
                | Timed(frq, tme)     -> [frq |> Frequency.toString; tme |> Time.toString]


        module Dto =

            module Units = ValueUnit.Units
            module Id = WrappedString.Id
            module NM = Name

            type Dto () =
                member val IsContinuous = false with get, set
                member val IsDiscontinuous = false with get, set
                member val IsTimed = false with get, set
                member val Frequency = OrderVariable.Dto.dto () with get, set
                member val Time = OrderVariable.Dto.dto () with get, set

            let fromDto (dto : Dto) =
                match dto.IsContinuous,
                      dto.IsDiscontinuous,
                      dto.IsTimed with
                | true,  false, false -> Continuous
                | false, true,  false ->
                    dto.Frequency
                    |> Frequency.fromDto
                    |> Discontinuous
                | false, false, true  ->
                    (dto.Frequency |> Frequency.fromDto, dto.Time |> Time.fromDto)
                    |> Timed
                | _ -> exn "dto is neither or both process, continuous, discontinuous or timed"
                       |> raise

            let toDto pres =
                let dto = Dto ()

                match pres with
                | Continuous -> dto.IsContinuous <- true
                | Discontinuous freq ->
                    dto.IsDiscontinuous <- true
                    dto.Frequency <- freq |> Frequency.toDto
                | Timed (freq, time) ->
                    dto.IsTimed <- true
                    dto.Frequency <- freq |> Frequency.toDto
                    dto.Time      <- time |> Time.toDto

                dto

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

            let setToContinuous (dto : Dto) =
                dto.IsContinuous <- true
                dto.IsDiscontinuous <- false
                dto.IsTimed <- false
                dto

            let setToDiscontinuous (dto : Dto) =
                dto.IsContinuous <- false
                dto.IsDiscontinuous <- true
                dto.IsTimed <- false
                dto

            let setToTimed (dto : Dto) =
                dto.IsContinuous <- false
                dto.IsDiscontinuous <- false
                dto.IsTimed <- true
                dto



    /// Types and functions that
    /// model a start and stop date time
    /// of an `Order`
    module StartStop =

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



    module OrderType =

        let toString = function
            | AnyOrder -> $"{AnyOrder}"
            | ProcessOrder -> $"{ProcessOrder}"
            | ContinuousOrder -> $"{ContinuousOrder}"
            | DiscontinuousOrder -> $"{DiscontinuousOrder}"
            | TimedOrder -> $"{TimedOrder}"


        let map s =
            match s with
            | _ when s = "discontinu" -> DiscontinuousOrder
            | _ when s = "continu" -> ContinuousOrder
            | _ when s = "inlooptijd" -> TimedOrder
            | _ -> DiscontinuousOrder



    module ValueRange = Informedica.GenSolver.Lib.Variable.ValueRange
    module Equation = Informedica.GenSolver.Lib.Equation
    module Property = ValueRange.Property
    module Quantity = OrderVariable.Quantity
    module Frequency = OrderVariable.Frequency
    module PerTimeAdjust = OrderVariable.PerTimeAdjust
    module Concentration = OrderVariable.Concentration
    module Rate = OrderVariable.Rate
    module RateAdjust = OrderVariable.RateAdjust
    module Time = OrderVariable.Time
    module Units = ValueUnit.Units

    type Equation = Informedica.GenSolver.Lib.Types.Equation


    /// Apply `f` to `Order` `ord`
    let apply f (ord: Order) = ord |> f


    /// Utility function to facilitate type inference
    let get = apply id


    /// Get the order id
    let getId ord = (ord |> get).Id


    /// Create an `Order` with
    ///
    /// * id: the id of the order
    /// * adj: by which doses are adjusted
    /// * orb: the `Orderable`
    /// * prs: `Prescription`, how the orderable is prescribed
    /// * rte: the route of administration of the orderable
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


    let getAdjust ord = (ord |> get).Adjust


    let getOrderable ord = (ord |> get).Orderable


    /// Turn an order into a list of string
    /// representing variable name, valuerange
    /// and unit group
    let toString (ord: Order) =
        [ ord.Adjust |> Quantity.toString ]
        |> List.append (Orderable.Literals.orderable::(ord.Orderable |> Orderable.toString))
        |> List.append ("Prescription"::(ord.Prescription |> Prescription.toString))
        |> List.append ("Route"::[ord.Route])
        |> List.filter (String.isNullOrWhiteSpace >> not)


    /// Map an `Orderable` **orb** to
    /// `VariableUnit`s
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


    let fromOrdVars ovars (ord : Order) =
        { ord with
            Adjust = ord.Adjust |> Quantity.fromOrdVar ovars
            Duration = ord.Duration |> Time.fromOrdVar ovars
            Prescription = ord.Prescription |> Prescription.fromOrdVars ovars
            Orderable = ord.Orderable |> Orderable.fromOrdVars ovars
        }


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
            printfn $"couldn't apply constraints:\n{s}"
            reraise()


    let mapToEquations eqs (ord: Order)  =
        let ovars = ord |> toOrdVars

        let map repl eqs =
            let eqs, c =
                match eqs with
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

        let sumEqs, prodEqs = eqs

        sumEqs |> map "+"
        |> List.append (prodEqs |> map "*")


    let mapFromEquations (ord: Order) eqs =
        let ovars =
            eqs
            |> List.collect (fun e ->
                match e with
                | OrderProductEquation (y, xs)
                | OrderSumEquation (y, xs) -> y::xs
            )
            |> List.distinct

        ord |> fromOrdVars ovars


    let solveMinMax printErr logger (ord: Order) =
        let ord = ord |> applyConstraints

        let mapping =
            match ord.Prescription with
            | Continuous -> Mapping.continuous
            | Discontinuous _ -> Mapping.discontinuous
            | Timed _ -> Mapping.timed
            |> Mapping.getEquations
            |> Mapping.getEqsMapping ord

        let oEqs =
            ord
            |> mapToEquations mapping

        try
            oEqs
            |> Solver.mapToSolverEqs
            |> Solver.solveMinMax logger
            |> function
            | Ok eqs ->
                eqs
                |> Solver.mapToOrderEqs oEqs
                |> mapFromEquations ord
                |> Ok
            | Error (eqs, m) ->
                eqs
                |> Solver.mapToOrderEqs oEqs
                |> mapFromEquations ord
                |> fun eqs -> Error (eqs, m)

        with
        | e ->
            if printErr then
                oEqs
                |> mapFromEquations ord
                |> toString
                |> List.iteri (printfn "%i. %s")

            raise e


    module Print =

        let printItemConcentration (c : Component) =
            c.Items
            |> Seq.map (fun i ->
                i.ComponentConcentration
                |> Concentration.toValueUnitString 1
                |> fun s ->
                    $"{s} {i.Name |> Name.toString}"
            )
            |> String.concat " + "


        let printComponentQuantity o =
            o.Orderable.Components
            |> Seq.map (fun c ->
                c.OrderableQuantity
                |> Quantity.toValueUnitString 1
                |> fun q ->
                    let s =
                        c
                        |> printItemConcentration
                        |> String.trim
                        |> fun s ->
                            if s |> String.isNullOrWhiteSpace then ""
                            else
                                $" ({s})"
                    $"{q} {c.Name |> Name.toString}{s}"
            )
            |> String.concat " + "


        let printOrderableDoseQuantity o =
            o.Orderable.Dose.Quantity
            |> Quantity.toValueUnitString 2


        let printPrescription sn (o : Order) =
            let on = o.Orderable.Name |> Name.toString

            let printItem get unt o =
                o.Orderable.Components
                |> Seq.collect (fun c ->
                    c.Items
                    |> Seq.collect (fun i ->
                        let n = i.Name |> Name.toString
                        if sn |> Seq.exists ((=) n) then
                            i
                            |> get
                            |> unt
                            |> fun s ->
                                if on |> String.startsWith n then seq [ s ]
                                else
                                    seq [ $"{s} {n}" ]

                        else Seq.empty
                    )
                )
                |> String.concat " + "

            match o.Prescription with
            | Prescription.Discontinuous fr ->
                // frequencies
                let fr =
                    fr
                    |> Frequency.toValueUnitString 0

                let dq =
                    o
                    |> printItem
                        (fun i -> i.Dose.Quantity)
                        (Quantity.toValueUnitString 3)

                let dt =
                    o
                    |> printItem
                        (fun i -> i.Dose.PerTimeAdjust)
                        (PerTimeAdjust.toValueUnitString 2)

                let pres = $"{o.Orderable.Name |> Name.toString} {fr} {dq} ({dt})"
                let prep = $"{o |> printComponentQuantity}"
                let adm = $"{fr} {o |> printOrderableDoseQuantity}"

                pres, prep, adm

            | Prescription.Continuous ->
                // infusion rate
                let rt =
                    o.Orderable.Dose.Rate
                    |> Rate.toValueUnitString 1

                let oq =
                    o.Orderable.OrderableQuantity
                    |> Quantity.toValueUnitString 2

                let it =
                    o
                    |> printItem
                        (fun i -> i.OrderableQuantity)
                        (Quantity.toValueUnitString 2)

                let dr =
                    o
                    |> printItem
                        (fun i -> i.Dose.RateAdjust)
                        (RateAdjust.toValueUnitString 2)

                let pres = $"""{sn |> String.concat " + "} {dr}"""
                let prep = o |> printComponentQuantity
                let adm = $"""{sn |> String.concat " + "} {it} in {oq}, {rt}"""

                pres, prep, adm

            | Prescription.Timed (fr, tme) ->

                // frequencies
                let fr =
                    fr
                    |> Frequency.toValueUnitString 0

                let tme =
                    tme
                    |> Time.toValueUnitString 2

                // infusion rate
                let rt =
                    o.Orderable.Dose.Rate
                    |> Rate.toValueUnitString 1

                let dq =
                    o
                    |> printItem
                        (fun i -> i.Dose.Quantity)
                        (Quantity.toValueUnitString 3)

                let dt =
                    o
                    |> printItem
                        (fun i -> i.Dose.PerTimeAdjust)
                        (PerTimeAdjust.toValueUnitString 1)

                let pres = $"{o.Orderable.Name |> Name.toString} {fr} {dq} = ({dt}) {rt}"
                let prep = o |> printComponentQuantity
                let adm = $"{fr} {o |> printOrderableDoseQuantity} in {tme}, {rt}"

                pres, prep, adm



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


        let continuous id orbN rte cmps  =
            Prescription.continuous Unit.NoUnit Unit.NoUnit
            |> dto id orbN rte cmps


        let discontinuous id orbN rte cmps =
            Prescription.discontinuous Unit.NoUnit Unit.NoUnit
            |> dto  id orbN rte cmps


        let timed  id orbN rte cmps=
            Prescription.timed Unit.NoUnit Unit.NoUnit
            |> dto id orbN rte cmps


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






