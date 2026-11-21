namespace Informedica.GenOrder.Lib

module EquationMapping =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open WrappedString

    module ValueSet = Informedica.GenSolver.Lib.Variable.ValueRange.ValueSet

    module Literals =

        [<Literal>]
        let qty = OrderVariable.Quantity.name

        [<Literal>]
        let cnc = OrderVariable.Concentration.name

        [<Literal>]
        let ptm = OrderVariable.PerTime.name

        [<Literal>]
        let rte = OrderVariable.Rate.name

        [<Literal>]
        let tot = OrderVariable.Total.name

        [<Literal>]
        let qtyAdj = OrderVariable.QuantityAdjust.name

        [<Literal>]
        let ptmAdj = OrderVariable.PerTimeAdjust.name

        [<Literal>]
        let rteAdj = OrderVariable.RateAdjust.name

        [<Literal>]
        let totAdj = OrderVariable.TotalAdjust.name

        [<Literal>]
        let cnt = OrderVariable.Count.name

        [<Literal>]
        let frq = OrderVariable.Frequency.name

        [<Literal>]
        let tme = OrderVariable.Time.name

        [<Literal>]
        let itm = "itm"

        [<Literal>]
        let cmp = "cmp"

        [<Literal>]
        let orb = "orb"

        [<Literal>]
        let dos = "dos"

        [<Literal>]
        let sch = "sch" // Schedule

        [<Literal>]
        let ord = "ord" // Order

        [<Literal>]
        let adj = "adj" // Adjust

        [<Literal>]
        let discontinuous = 3

        [<Literal>]
        let continuous = 4

        [<Literal>]
        let timed = 5

        [<Literal>]
        let once = 6

        [<Literal>]
        let onceTimed = 7


    /// A mark of an equation in the equations table, one per mark column of the Equations sheet.
    [<RequireQualifiedAccess>]
    type Mark =
        /// The equation applies only when the order has an adjusted dose constraint.
        | DoseAdjust
        /// The equation applies only when the orderable, a component or an item has a constraint on its
        /// orderable quantity per adjust.
        | OrderableQuantityAdjust
        /// The equation applies only when the order has more than one component.
        | Tie
        /// The equation applies only when the order uses the item concentration products.
        | ItemConcentration
        /// The equation is added for a timed order that leaves out the item concentration products.
        | NoItemConcentrationTimed
        /// The equation is added for a once timed order that leaves out the item concentration products.
        | NoItemConcentrationOnceTimed
        /// The equation is a sum equation; an equation without this mark is a product equation.
        | Sum


    /// An equation with the dose types it applies to and its marks.
    type EquationRow =
        {
            /// The equation, with the placeholders [ord], [orb], [cmp] and [itm].
            Equation: string
            /// The dose types the equation applies to.
            DoseTypes: int list
            /// The marks of the equation.
            Marks: Mark list
        }


    /// The five dose types: discontinuous (3), continuous (4), timed (5), once (6) and once timed (7).
    let allDoseTypes =
        [
            Literals.discontinuous
            Literals.continuous
            Literals.timed
            Literals.once
            Literals.onceTimed
        ]

    // Sets of dose types that several equations share.
    let discTimed = [ Literals.discontinuous; Literals.timed ]

    let discContTimed = [ Literals.discontinuous; Literals.continuous; Literals.timed ]

    let contTimedOnceTimed = [ Literals.continuous; Literals.timed; Literals.onceTimed ]

    let contOnly = [ Literals.continuous ]

    let timedOnly = [ Literals.timed ]


    /// Creates an equation row from an equation, its dose types and its marks.
    let row eq doseTypes marks =
        {
            Equation = eq
            DoseTypes = doseTypes
            Marks = marks
        }


    /// The order equations, each with the dose types it applies to and its marks. The equations are fixed in
    /// code; the Equations sheet documents them.
    let equations =
        [
            row "[itm]_cmp_qty = [itm]_cmp_cnc * [cmp]_cmp_qty" allDoseTypes []
            row "[itm]_orb_qty = [itm]_orb_cnc * [orb]_orb_qty" allDoseTypes [ Mark.ItemConcentration ]
            row "[itm]_orb_qty = [itm]_cmp_cnc * [cmp]_orb_qty" allDoseTypes []
            row "[itm]_dos_qty = [itm]_cmp_cnc * [cmp]_dos_qty" allDoseTypes []
            row "[itm]_dos_qty = [itm]_orb_cnc * [orb]_dos_qty" allDoseTypes [ Mark.ItemConcentration ]
            row "[itm]_dos_qty = [itm]_dos_qty_adj * [ord]_adj_qty" allDoseTypes [ Mark.DoseAdjust ]
            row "[itm]_dos_ptm = [itm]_cmp_cnc * [cmp]_dos_ptm" discTimed []
            row "[itm]_dos_ptm = [itm]_orb_cnc * [orb]_dos_ptm" discTimed [ Mark.ItemConcentration ]
            row "[itm]_dos_ptm = [itm]_dos_qty * [ord]_sch_frq" discTimed []
            row "[itm]_dos_ptm = [itm]_dos_ptm_adj * [ord]_adj_qty" discTimed [ Mark.DoseAdjust ]
            row "[itm]_dos_rte = [itm]_cmp_cnc * [cmp]_dos_rte" contTimedOnceTimed []
            row "[itm]_dos_rte = [itm]_orb_cnc * [orb]_dos_rte" contTimedOnceTimed [ Mark.ItemConcentration ]
            row "[itm]_dos_rte = [itm]_dos_rte_adj * [ord]_adj_qty" contTimedOnceTimed [ Mark.DoseAdjust ]
            row "[itm]_dos_tot = [itm]_dos_ptm * [ord]_ord_tme" discTimed []
            row "[itm]_dos_tot = [itm]_dos_rte * [ord]_ord_tme" contOnly []
            row "[itm]_dos_qty_adj = [itm]_cmp_cnc * [cmp]_dos_qty_adj" allDoseTypes [ Mark.DoseAdjust ]
            row
                "[itm]_dos_qty_adj = [itm]_orb_cnc * [orb]_dos_qty_adj"
                allDoseTypes
                [ Mark.DoseAdjust; Mark.ItemConcentration ]
            row "[itm]_dos_ptm_adj = [itm]_cmp_cnc * [cmp]_dos_ptm_adj" discContTimed [ Mark.DoseAdjust ]
            row
                "[itm]_dos_ptm_adj = [itm]_orb_cnc * [orb]_dos_ptm_adj"
                discContTimed
                [ Mark.DoseAdjust; Mark.ItemConcentration ]
            row "[itm]_dos_ptm_adj = [itm]_dos_qty_adj * [ord]_sch_frq" discTimed [ Mark.DoseAdjust ]
            row "[itm]_dos_rte_adj = [itm]_cmp_cnc * [cmp]_dos_rte_adj" contOnly [ Mark.DoseAdjust ]
            row
                "[itm]_dos_rte_adj = [itm]_orb_cnc * [orb]_dos_rte_adj"
                contOnly
                [ Mark.DoseAdjust; Mark.ItemConcentration ]
            row "[itm]_dos_tot_adj = [itm]_dos_ptm_adj * [ord]_ord_tme" discTimed [ Mark.DoseAdjust ]
            row "[itm]_dos_tot_adj = [itm]_dos_rte_adj * [ord]_ord_tme" contOnly [ Mark.DoseAdjust ]
            row "[cmp]_orb_qty = [cmp]_orb_cnc * [orb]_orb_qty" allDoseTypes []
            row "[cmp]_orb_qty = [orb]_dos_cnt * [cmp]_dos_qty" allDoseTypes []
            row "[cmp]_orb_qty = [cmp]_cmp_qty * [cmp]_orb_cnt" allDoseTypes []
            row "[cmp]_ord_qty = [cmp]_cmp_qty * [cmp]_ord_cnt" allDoseTypes []
            row "[cmp]_dos_tot = [cmp]_dos_ptm * [ord]_ord_tme" discTimed []
            row "[cmp]_dos_tot = [cmp]_dos_rte * [ord]_ord_tme" contOnly []
            row "[cmp]_dos_qty = [cmp]_orb_cnc * [orb]_dos_qty" allDoseTypes []
            row "[cmp]_dos_qty = [cmp]_dos_qty_adj * [ord]_adj_qty" allDoseTypes [ Mark.DoseAdjust ]
            row "[cmp]_dos_ptm = [cmp]_orb_cnc * [orb]_dos_ptm" discTimed []
            row "[cmp]_dos_ptm = [cmp]_dos_qty * [ord]_sch_frq" discTimed []
            row "[cmp]_dos_ptm = [cmp]_dos_ptm_adj * [ord]_adj_qty" discTimed [ Mark.DoseAdjust ]
            row
                "[cmp]_dos_rte = [cmp]_orb_cnc * [orb]_dos_rte"
                contOnly
                [ Mark.NoItemConcentrationTimed; Mark.NoItemConcentrationOnceTimed ]
            row "[cmp]_dos_rte = [cmp]_dos_rte_adj * [ord]_adj_qty" contOnly [ Mark.DoseAdjust ]
            row "[cmp]_dos_qty_adj = [cmp]_orb_cnc * [orb]_dos_qty_adj" allDoseTypes [ Mark.DoseAdjust ]
            row "[cmp]_dos_qty_adj = [cmp]_dos_rte_adj * [ord]_sch_tme" contOnly [ Mark.DoseAdjust ]
            row "[cmp]_dos_ptm_adj = [cmp]_orb_cnc * [orb]_dos_ptm_adj" discTimed [ Mark.DoseAdjust ]
            row "[cmp]_dos_ptm_adj = [cmp]_dos_qty_adj * [ord]_sch_frq" discTimed [ Mark.DoseAdjust ]
            row "[cmp]_dos_rte_adj = [cmp]_orb_cnc * [orb]_dos_rte_adj" contOnly [ Mark.DoseAdjust ]
            row "[orb]_orb_qty = [orb]_dos_cnt * [orb]_dos_qty" allDoseTypes []
            row "[orb]_ord_qty = [orb]_ord_cnt * [orb]_orb_qty" allDoseTypes []
            // The total from the dose per time applies to discontinuous and timed orders, the total from the dose
            // rate only to continuous orders. Both on a timed order would force the administration time times the
            // frequency to be 1.
            row "[orb]_dos_tot = [orb]_dos_ptm * [ord]_ord_tme" discTimed []
            row "[orb]_dos_tot = [orb]_dos_rte * [ord]_ord_tme" contOnly []
            row "[orb]_dos_qty = [orb]_dos_rte * [ord]_sch_tme" contTimedOnceTimed []
            row "[orb]_dos_qty = [orb]_dos_qty_adj * [ord]_adj_qty" allDoseTypes [ Mark.DoseAdjust ]
            row "[orb]_dos_ptm = [orb]_dos_qty * [ord]_sch_frq" discTimed []
            row "[orb]_dos_ptm = [orb]_dos_ptm_adj * [ord]_adj_qty" discTimed [ Mark.DoseAdjust ]
            row "[orb]_dos_rte = [orb]_dos_rte_adj * [ord]_adj_qty" contTimedOnceTimed [ Mark.DoseAdjust ]
            row "[orb]_dos_qty_adj = [orb]_dos_rte_adj * [ord]_sch_tme" contOnly [ Mark.DoseAdjust ]
            row "[orb]_dos_ptm_adj = [orb]_dos_qty_adj * [ord]_sch_frq" discTimed [ Mark.DoseAdjust ]
            // The schedule count is at most 1, so one administration ends before the next starts.
            row "[ord]_sch_cnt = [ord]_sch_tme * [ord]_sch_frq" timedOnly []
            row "[orb]_orb_qty = sum([cmp]_orb_qty)" allDoseTypes [ Mark.Sum ]
            // The tie equations need more than one component; with one component the other equations already relate
            // the items.
            row "[itm]_orb_cnc = [itm]_cmp_cnc * [cmp]_orb_cnc" allDoseTypes [ Mark.Tie ]
            row "[orb]_orb_cnc = sum([cmp]_orb_cnc)" allDoseTypes [ Mark.Tie; Mark.Sum ]
            row "[orb]_orb_qty = [orb]_orb_qty_adj * [ord]_adj_qty" allDoseTypes [ Mark.OrderableQuantityAdjust ]
            row "[cmp]_orb_qty = [cmp]_orb_qty_adj * [ord]_adj_qty" allDoseTypes [ Mark.OrderableQuantityAdjust ]
            row "[itm]_orb_qty = [itm]_orb_qty_adj * [ord]_adj_qty" allDoseTypes [ Mark.OrderableQuantityAdjust ]
        ]


    let private getEquations_ indx =
        equations |> List.filter (fun r -> r.DoseTypes |> List.contains indx)


    // Wrapped by getEquations so that the public function compiles to a .NET method,
    // not to a property that returns an FSharpFunc.
    let private memoizedGetEquations = Memoization.memoize getEquations_


    /// Returns the equation rows for the dose type indx: 3 discontinuous, 4 continuous, 5 timed, 6 once or 7 once
    /// timed. The result is memoized.
    let getEquations indx = memoizedGetEquations indx


    /// Returns true when the defined values of the item component concentration hold exactly one value.
    /// Returns false for an item without defined values.
    let hasOneStrength (itm: Item) =
        (itm.ComponentConcentration |> OrderVariable.Concentration.toOrdVar).DefinedConstraints.Values
        |> Option.exists (ValueSet.count >> (=) 1)


    /// Returns true when the order has more than two components and every item has one component concentration.
    /// The item concentrations in the orderable then follow from the tie equations alone; with more concentrations
    /// per item they would not be exact.
    let leavesOutItemConcentrationProducts (ord: Order) =
        ord.Orderable.Components |> List.length > 2
        && ord.Orderable.Components |> List.collect _.Items |> List.forall hasOneStrength


    /// Returns true when an adjusted dose quantity, per time, rate or total of the orderable, a component or an item
    /// has constraints. Without such constraints the adjusted dose equations do not change the other variables.
    let usesAdjust (ord: Order) =
        let adjusted (dos: Dose) =
            [
                dos.QuantityAdjust |> OrderVariable.QuantityAdjust.toOrdVar
                dos.PerTimeAdjust |> OrderVariable.PerTimeAdjust.toOrdVar
                dos.RateAdjust |> OrderVariable.RateAdjust.toOrdVar
                dos.TotalAdjust |> OrderVariable.TotalAdjust.toOrdVar
            ]

        [
            ord.Orderable.Dose
            for c in ord.Orderable.Components do
                c.Dose

                for i in c.Items do
                    i.Dose
        ]
        |> List.collect adjusted
        |> List.exists OrderVariable.hasConstraints


    /// Returns true when the quantity in the orderable per adjust unit has constraints for the orderable, a
    /// component or an item of the order.
    let usesOrderableQuantityAdjust (ord: Order) =
        [
            ord.Orderable.OrderableQuantityAdjust

            for c in ord.Orderable.Components do
                c.OrderableQuantityAdjust

                for i in c.Items do
                    i.OrderableQuantityAdjust
        ]
        |> List.exists (OrderVariable.QuantityAdjust.toOrdVar >> OrderVariable.hasConstraints)


    /// Returns true when a mark holds or not depending on the order. The other marks do not filter the equations.
    let isCondition mark =
        match mark with
        | Mark.DoseAdjust
        | Mark.OrderableQuantityAdjust
        | Mark.Tie
        | Mark.ItemConcentration -> true
        | Mark.NoItemConcentrationTimed
        | Mark.NoItemConcentrationOnceTimed
        | Mark.Sum -> false


    /// Returns the condition marks that hold for the order ord.
    let orderMarks ord =
        [
            if ord |> usesAdjust then
                Mark.DoseAdjust
            if ord |> usesOrderableQuantityAdjust then
                Mark.OrderableQuantityAdjust
            if ord.Orderable.Components |> List.length > 1 then
                Mark.Tie
            if ord |> leavesOutItemConcentrationProducts |> not then
                Mark.ItemConcentration
        ]


    /// Returns the sum equations and the product equations for the dose type indx, given the condition marks that
    /// hold for an order. A row is selected when each of its condition marks is in marks.
    let selectEquations indx marks =
        let holds r =
            r.Marks
            |> List.forall (fun m -> not (isCondition m) || marks |> List.contains m)

        // An order that does not use the item concentration products gets the equations marked for its dose type,
        // after the other equations, so that an item rate limit still bounds the orderable rate.
        let added =
            let mark =
                match indx with
                | Literals.timed -> Some Mark.NoItemConcentrationTimed
                | Literals.onceTimed -> Some Mark.NoItemConcentrationOnceTimed
                | _ -> None

            match mark with
            | Some m when marks |> List.contains Mark.ItemConcentration |> not ->
                equations |> List.filter (_.Marks >> List.contains m)
            | _ -> []

        let sumEqs, prodEqs =
            (getEquations indx @ added)
            |> List.filter holds
            |> List.partition (_.Marks >> List.contains Mark.Sum)

        sumEqs |> List.map _.Equation, prodEqs |> List.map _.Equation


    /// Returns the sum mapping and the product mapping of the order for the dose type indx, with the placeholders
    /// in the equations replaced by the order, orderable, component and item names.
    let getEqsMapping ord indx =
        let sumEqs, prodEqs = ord |> orderMarks |> selectEquations indx

        let itmEqs = prodEqs |> List.filter (String.contains "[itm]")

        let cmpEqs =
            prodEqs
            |> List.filter (fun e -> itmEqs |> List.exists ((=) e) |> not && e.Contains("[cmp]"))

        let orbEqs =
            prodEqs
            |> List.filter (fun e -> itmEqs |> List.exists ((=) e) |> not && cmpEqs |> List.exists ((=) e) |> not)

        let idN = [ ord.Id |> Id.toString ] |> Name.create

        let orbN = [ ord.Id |> Id.toString; ord.Orderable.Name |> Name.toString ] |> Name.create

        ord.Orderable.Components
        |> List.fold
            (fun acc c ->
                let cmpN = [ yield! orbN |> Name.toStringList; c.Name |> Name.toString ] |> Name.create

                let itms =
                    c.Items
                    |> List.collect (fun i ->
                        itmEqs
                        |> List.map (fun s ->
                            let itmN = [ yield! cmpN |> Name.toStringList; i.Name |> Name.toString ] |> Name.create

                            s
                            |> String.replace "[cmp]" $"{cmpN |> Name.toString}"
                            |> String.replace "[itm]" $"{itmN |> Name.toString}"
                        )
                    )

                let cmps = cmpEqs |> List.map (String.replace "[cmp]" $"{cmpN |> Name.toString}")

                acc |> List.append cmps |> List.append itms
            )
            []
        |> fun es ->
            let sumEqs =
                sumEqs
                |> List.map (fun e ->
                    match e |> String.replace "sum(" "" |> String.replace ")" "" |> String.split " = " with
                    | [ lv; rv ] ->
                        ord.Orderable.Components
                        |> List.map (fun c ->
                            let cmpN = [ yield! orbN |> Name.toStringList; c.Name |> Name.toString ] |> Name.create

                            rv |> String.replace "[cmp]" $"{cmpN |> Name.toString}"
                        )
                        |> String.concat " + "
                        |> fun s -> $"{lv} = {s}"
                    | _ -> ""
                )
                |> List.filter String.notEmpty
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
