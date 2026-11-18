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


    /// The item concentration in the orderable equals the item concentration in the component times the component
    /// concentration in the orderable, and the component concentrations in the orderable add up to one. Used only
    /// for an order with more than one component; with one component the other equations already relate the items.
    let tieEquations =
        [
            "[itm]_orb_cnc = [itm]_cmp_cnc * [cmp]_orb_cnc"
            "[orb]_orb_cnc = sum([cmp]_orb_cnc)"
        ]


    /// The component dose rate equals the component concentration in the orderable times the orderable dose rate.
    /// Used for a continuous order, and for a timed or once timed order without the item concentration products.
    let componentRateEquation = "[cmp]_dos_rte = [cmp]_orb_cnc * [orb]_dos_rte"


    /// The order equations, each with the dose types it applies to. The equations are fixed in code.
    let equations: (string * int list) list =
        [
            "[itm]_cmp_qty = [itm]_cmp_cnc * [cmp]_cmp_qty", allDoseTypes
            "[itm]_orb_qty = [itm]_orb_cnc * [orb]_orb_qty", allDoseTypes
            "[itm]_orb_qty = [itm]_cmp_cnc * [cmp]_orb_qty", allDoseTypes
            "[itm]_dos_qty = [itm]_cmp_cnc * [cmp]_dos_qty", allDoseTypes
            "[itm]_dos_qty = [itm]_orb_cnc * [orb]_dos_qty", allDoseTypes
            "[itm]_dos_qty = [itm]_dos_qty_adj * [ord]_adj_qty", allDoseTypes
            "[itm]_dos_ptm = [itm]_cmp_cnc * [cmp]_dos_ptm", discTimed
            "[itm]_dos_ptm = [itm]_orb_cnc * [orb]_dos_ptm", discTimed
            "[itm]_dos_ptm = [itm]_dos_qty * [ord]_sch_frq", discTimed
            "[itm]_dos_ptm = [itm]_dos_ptm_adj * [ord]_adj_qty", discTimed
            "[itm]_dos_rte = [itm]_cmp_cnc * [cmp]_dos_rte", contTimedOnceTimed
            "[itm]_dos_rte = [itm]_orb_cnc * [orb]_dos_rte", contTimedOnceTimed
            "[itm]_dos_rte = [itm]_dos_rte_adj * [ord]_adj_qty", contTimedOnceTimed
            "[itm]_dos_tot = [itm]_dos_ptm * [ord]_ord_tme", discTimed
            "[itm]_dos_tot = [itm]_dos_rte * [ord]_ord_tme", contOnly
            "[itm]_dos_qty_adj = [itm]_cmp_cnc * [cmp]_dos_qty_adj", allDoseTypes
            "[itm]_dos_qty_adj = [itm]_orb_cnc * [orb]_dos_qty_adj", allDoseTypes
            "[itm]_dos_ptm_adj = [itm]_cmp_cnc * [cmp]_dos_ptm_adj", discContTimed
            "[itm]_dos_ptm_adj = [itm]_orb_cnc * [orb]_dos_ptm_adj", discContTimed
            "[itm]_dos_ptm_adj = [itm]_dos_qty_adj * [ord]_sch_frq", discTimed
            "[itm]_dos_rte_adj = [itm]_cmp_cnc * [cmp]_dos_rte_adj", contOnly
            "[itm]_dos_rte_adj = [itm]_orb_cnc * [orb]_dos_rte_adj", contOnly
            "[itm]_dos_tot_adj = [itm]_dos_ptm_adj * [ord]_ord_tme", discTimed
            "[itm]_dos_tot_adj = [itm]_dos_rte_adj * [ord]_ord_tme", contOnly
            "[cmp]_orb_qty = [cmp]_orb_cnc * [orb]_orb_qty", allDoseTypes
            "[cmp]_orb_qty = [orb]_dos_cnt * [cmp]_dos_qty", allDoseTypes
            "[cmp]_orb_qty = [cmp]_cmp_qty * [cmp]_orb_cnt", allDoseTypes
            "[cmp]_ord_qty = [cmp]_cmp_qty * [cmp]_ord_cnt", allDoseTypes
            "[cmp]_dos_tot = [cmp]_dos_ptm * [ord]_ord_tme", discTimed
            "[cmp]_dos_tot = [cmp]_dos_rte * [ord]_ord_tme", contOnly
            "[cmp]_dos_qty = [cmp]_orb_cnc * [orb]_dos_qty", allDoseTypes
            "[cmp]_dos_qty = [cmp]_dos_qty_adj * [ord]_adj_qty", allDoseTypes
            "[cmp]_dos_ptm = [cmp]_orb_cnc * [orb]_dos_ptm", discTimed
            "[cmp]_dos_ptm = [cmp]_dos_qty * [ord]_sch_frq", discTimed
            "[cmp]_dos_ptm = [cmp]_dos_ptm_adj * [ord]_adj_qty", discTimed
            componentRateEquation, contOnly
            "[cmp]_dos_rte = [cmp]_dos_rte_adj * [ord]_adj_qty", contOnly
            "[cmp]_dos_qty_adj = [cmp]_orb_cnc * [orb]_dos_qty_adj", allDoseTypes
            "[cmp]_dos_qty_adj = [cmp]_dos_rte_adj * [ord]_sch_tme", contOnly
            "[cmp]_dos_ptm_adj = [cmp]_orb_cnc * [orb]_dos_ptm_adj", discTimed
            "[cmp]_dos_ptm_adj = [cmp]_dos_qty_adj * [ord]_sch_frq", discTimed
            "[cmp]_dos_rte_adj = [cmp]_orb_cnc * [orb]_dos_rte_adj", contOnly
            "[orb]_orb_qty = [orb]_dos_cnt * [orb]_dos_qty", allDoseTypes
            "[orb]_ord_qty = [orb]_ord_cnt * [orb]_orb_qty", allDoseTypes
            // The total from the dose per time applies to discontinuous and timed orders, the total from the dose
            // rate only to continuous orders. Both on a timed order would force the administration time times the
            // frequency to be 1.
            "[orb]_dos_tot = [orb]_dos_ptm * [ord]_ord_tme", discTimed
            "[orb]_dos_tot = [orb]_dos_rte * [ord]_ord_tme", contOnly
            "[orb]_dos_qty = [orb]_dos_rte * [ord]_sch_tme", contTimedOnceTimed
            "[orb]_dos_qty = [orb]_dos_qty_adj * [ord]_adj_qty", allDoseTypes
            "[orb]_dos_ptm = [orb]_dos_qty * [ord]_sch_frq", discTimed
            "[orb]_dos_ptm = [orb]_dos_ptm_adj * [ord]_adj_qty", discTimed
            "[orb]_dos_rte = [orb]_dos_rte_adj * [ord]_adj_qty", contTimedOnceTimed
            "[orb]_dos_qty_adj = [orb]_dos_rte_adj * [ord]_sch_tme", contOnly
            "[orb]_dos_ptm_adj = [orb]_dos_qty_adj * [ord]_sch_frq", discTimed
            "[orb]_orb_qty = sum([cmp]_orb_qty)", allDoseTypes
            yield! tieEquations |> List.map (fun e -> e, allDoseTypes)
        ]


    let private getEquations_ indx =
        equations
        |> List.filter (fun (_, dts) -> dts |> List.contains indx)
        |> List.map fst


    // Wrapped by getEquations so that the public function compiles to a .NET method,
    // not to a property that returns an FSharpFunc.
    let private memoizedGetEquations = Memoization.memoize getEquations_


    /// Returns the equations for the dose type indx: 3 discontinuous, 4 continuous, 5 timed, 6 once or 7 once timed.
    /// The result is memoized.
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


    /// Returns the sum mapping and the product mapping of the order, with the placeholders in the equations eqs
    /// replaced by the order, orderable, component and item names.
    let getEqsMapping (ord: Order) (eqs: string list) =
        // An order without adjusted dose constraints leaves out every equation with an adjusted variable.
        let eqs =
            if ord |> usesAdjust then
                eqs
            else
                eqs |> List.filter (String.contains $"_%s{Literals.adj}" >> not)

        let eqs =
            if ord.Orderable.Components |> List.length > 1 then
                eqs
            else
                eqs |> List.filter (fun e -> tieEquations |> List.contains e |> not)

        // The equations with the item concentration in the orderable only add cross products, because the tie
        // equations give the same values. A timed or once timed order then gets the component rate equation,
        // so that an item rate limit still bounds the orderable rate.
        let eqs =
            if ord |> leavesOutItemConcentrationProducts |> not then
                eqs
            else
                eqs
                |> List.filter (fun e -> e.Contains "[itm]_orb_cnc" |> not || tieEquations |> List.contains e)
                |> fun eqs ->
                    match ord.Schedule with
                    | Timed _
                    | OnceTimed _ -> eqs @ [ componentRateEquation ]
                    | _ -> eqs

        let sumEqs = eqs |> List.filter (String.contains "sum")

        let prodEqs = eqs |> List.filter (String.contains "sum" >> not)

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
