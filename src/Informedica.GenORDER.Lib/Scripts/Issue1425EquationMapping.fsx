// Issue #1421: EquationMapping.fs with one change, loaded in place of the source by
// Issue1425Load.fsx. Marked "1421" below.
//
// An order with more than two components and no item in more than one strength solves its item
// orderable concentrations through the tie [itm]_orb_cnc = [itm]_cmp_cnc * [cmp]_orb_cnc alone:
// the products with an orderable total (3 for once, 4 once timed, 5 discontinuous, 6 continuous
// and timed) are left out, and a timed or once timed order gets the component rate equation in
// place of the item rate product. The products are cross products nobody picks; the tie gives the
// same values, and a concentration limit of an item still bounds the component share through it.
//
// Defines for the versions measured against it:
// - DROPALL drops the tie too: a concentration limit stops binding
// - NOCMPRATE adds no component rate equation: the item rates of a timed order lose their bounds
// - ANYSTRENGTH applies the rule to items in several strengths too: their concentration and rate
//   loosen in a continuous order
// - SCHEDULE excludes continuous orders in place of items in several strengths, the earlier rule
// - SHAPE excludes orders with an item whose orderable quantity is fixed, in place of strengths
// - SETTLED adds a condition on state: only while a component quantity has more than one value

namespace Informedica.GenOrder.Lib

module EquationMapping =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open WrappedString

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
        let itm = "itm" //Orderable.Literals.item

        [<Literal>]
        let cmp = "cmp" //Orderable.Literals.comp

        [<Literal>]
        let orb = "orb" //Orderable.Literals.orderable

        [<Literal>]
        let dos = "dos" //Orderable.Literals.dose

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


    // Order equations are an invariant of GenORDER and are embedded here instead
    // of being fetched from the "Equations" Google sheet. Each equation is paired
    // with the dose types it applies to (Literals: discontinuous=3 .. onceTimed=7).
    let allDoseTypes =
        [
            Literals.discontinuous
            Literals.continuous
            Literals.timed
            Literals.once
            Literals.onceTimed
        ]

    // Recurring dose-type membership sets, named so equal sets read equal
    // (and Fantomas keeps them on one line at every use site).
    let discTimed = [ Literals.discontinuous; Literals.timed ]

    let discContTimed = [ Literals.discontinuous; Literals.continuous; Literals.timed ]

    let contTimedOnceTimed = [ Literals.continuous; Literals.timed; Literals.onceTimed ]

    let contOnly = [ Literals.continuous ]


    /// An item's concentration in the orderable is its concentration in the component times the
    /// component's share, and the shares add up to one. Only mapped for an order with more than one
    /// component: with one, the share is 1 x and the other equations already tie the items.
    let tieEquations =
        [
            "[itm]_orb_cnc = [itm]_cmp_cnc * [cmp]_orb_cnc"
            "[orb]_orb_cnc = sum([cmp]_orb_cnc)"
        ]


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
            "[cmp]_dos_tot = [cmp]_dos_ptm * [ord]_ord_tme", discContTimed
            "[cmp]_dos_tot = [cmp]_dos_rte * [ord]_ord_tme", contOnly
            "[cmp]_dos_qty = [cmp]_orb_cnc * [orb]_dos_qty", allDoseTypes
            "[cmp]_dos_qty = [cmp]_dos_qty_adj * [ord]_adj_qty", allDoseTypes
            "[cmp]_dos_ptm = [cmp]_orb_cnc * [orb]_dos_ptm", discTimed
            "[cmp]_dos_ptm = [cmp]_dos_qty * [ord]_sch_frq", discTimed
            "[cmp]_dos_ptm = [cmp]_dos_ptm_adj * [ord]_adj_qty", discTimed
            "[cmp]_dos_rte = [cmp]_orb_cnc * [orb]_dos_rte", contOnly
            "[cmp]_dos_rte = [cmp]_dos_rte_adj * [ord]_adj_qty", contOnly
            "[cmp]_dos_qty_adj = [cmp]_orb_cnc * [orb]_dos_qty_adj", allDoseTypes
            "[cmp]_dos_qty_adj = [cmp]_dos_rte_adj * [ord]_sch_tme", contOnly
            "[cmp]_dos_ptm_adj = [cmp]_orb_cnc * [orb]_dos_ptm_adj", discTimed
            "[cmp]_dos_ptm_adj = [cmp]_dos_qty_adj * [ord]_sch_frq", discTimed
            "[cmp]_dos_rte_adj = [cmp]_orb_cnc * [orb]_dos_rte_adj", contOnly
            "[orb]_orb_qty = [orb]_dos_cnt * [orb]_dos_qty", allDoseTypes
            "[orb]_ord_qty = [orb]_ord_cnt * [orb]_orb_qty", allDoseTypes
            "[orb]_dos_tot = [orb]_dos_ptm * [ord]_ord_tme", discContTimed
            "[orb]_dos_tot = [orb]_dos_rte * [ord]_ord_tme", discContTimed
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


    // Memoised once at module init. Kept private and wrapped below so the
    // public binding stays an argument-taking function: a function value compiles to a
    // property returning FSharpFunc, a different .NET shape.
    let private memoizedGetEquations = Memoization.memoize getEquations_


    /// <summary>
    /// Get a string list of Equations and
    /// use an index to filter out the relevant equations
    /// </summary>
    /// <param name="indx">The index to filter the equations</param>
    /// <remarks>
    /// The indx can be 3 for discontinuous equations, 4 for continuous
    /// and 5 for timed equations.
    /// </remarks>
    let getEquations indx = memoizedGetEquations indx


    /// Whether an item's orderable quantity is constrained to one value, as a solution limit with
    /// an exact quantity sets it.
    let hasOneOrderableQuantity (itm: Item) =
        let cs = (itm.OrderableQuantity |> OrderVariable.Quantity.toOrdVar).DefinedConstraints

        match cs.Values, cs.Min, cs.Max with
        | Some vs, _, _ -> vs |> Informedica.GenSolver.Lib.Variable.ValueRange.ValueSet.count = 1
        | None, Some mn, Some mx ->
            (mn |> Informedica.GenSolver.Lib.Variable.ValueRange.Minimum.toValueUnit) = (
                mx |> Informedica.GenSolver.Lib.Variable.ValueRange.Maximum.toValueUnit
            )
        | _ -> false


    /// Whether an item comes in more than one component concentration, from more than one product:
    /// read from its defined constraint, so a solve that settles the strength does not change it.
    let hasStrengths (itm: Item) =
        (itm.ComponentConcentration |> OrderVariable.Concentration.toOrdVar).DefinedConstraints.Values
        |> Option.map Informedica.GenSolver.Lib.Variable.ValueRange.ValueSet.count
        |> Option.exists (fun n -> n > 1)


    /// Whether an order solves its item orderable concentrations through the tie alone, without the
    /// products with an orderable total: more than two components and no item in more than one
    /// strength. The products are cross products nobody picks. With one strength per item the tie,
    /// a single concentration times the component share, gives the same values; with several it
    /// multiplies the strengths by the share as if they were independent, though each strength
    /// belongs to its own component quantity, and loses the exact concentration the product gives.
    let leavesOutItemConcentrationProducts (ord: Order) =
        let items = ord.Orderable.Components |> List.collect _.Items

        ord.Orderable.Components |> List.length > 2
#if SCHEDULE
        // the earlier rule: the schedule in place of the strengths
        && (match ord.Schedule with
            | Continuous _ -> false
            | _ -> true)
#else
#if SHAPE
        // the shape in place of the strengths: no item with its orderable quantity fixed
        && items |> List.exists hasOneOrderableQuantity |> not
#else
#if !ANYSTRENGTH
        && items |> List.exists hasStrengths |> not
#endif
#endif
#endif
#if SETTLED
        // with state: only while a component quantity has more than one value
        && ord.Orderable.Components
           |> List.exists (
               _.OrderableQuantity
               >> OrderVariable.Quantity.toOrdVar
               >> OrderVariable.getVar
               >> Informedica.GenSolver.Lib.Variable.isSolved
               >> not
           )
#endif


    /// <summary>
    /// Create an Equations mapping for an <c>Order</c>
    /// </summary>
    /// <param name="ord">The Order to Map</param>
    /// <param name="eqs">The equations as a string list</param>
    /// <returns>
    /// A tuple of <c>SumMapping</c> and <c>ProductMapping</c>
    /// </returns>
    let getEqsMapping (ord: Order) (eqs: string list) =
        let eqs =
            if ord.Orderable.Components |> List.length > 1 then
                eqs
            else
                eqs |> List.filter (fun e -> tieEquations |> List.contains e |> not)

        // 1421: the products of an item orderable concentration with an orderable total left out;
        // the tie to the component share stays, so a concentration limit still bounds the share
        // (DROPALL drops the tie too). A timed or once timed order gets the component rate in
        // place of the item rate product, so an item rate limit still bounds the orderable rate
        // (NOCMPRATE leaves it out); a continuous order has it already.
        let eqs =
            if ord |> leavesOutItemConcentrationProducts then
                let cmpRate = "[cmp]_dos_rte = [cmp]_orb_cnc * [orb]_dos_rte"

                let hasRate =
                    match ord.Schedule with
                    | Timed _
                    | OnceTimed _ -> true
                    | _ -> false

                eqs
                |> List.filter (fun e ->
                    e.Contains "[itm]_orb_cnc" |> not
#if !DROPALL
                    || e = "[itm]_orb_cnc = [itm]_cmp_cnc * [cmp]_orb_cnc"
#endif
                )
#if !NOCMPRATE
                |> fun eqs -> if hasRate && eqs |> List.contains cmpRate |> not then eqs @ [ cmpRate ] else eqs
#endif
            else
                eqs

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
