# Informedica.GenOrder.Lib

A library to map `Order` to `Equations` that can be solved by the `Informedica.GenSolver.Lib`, using the `Informedica.Units.Lib` to convert all values to base values.

A medical order is mainly used to prescribe medication, but can also be used to prescribe feeding, parenteral nutrition, maintenance fluids etc.. The prescribed order needs:

1. Lookup, for example which medication products are available, how to dose them and how to prepare the prescription for administration.
2. Often, there are also calculations involved. Particularly the calculation of dose and the preparation of the medication can be quite complicated.
3. Finally, the whole process follows a cyclic pattern in which a administered prescription can lead to additions or alterations.

![Medication Cycle](https://docs.google.com/drawings/d/e/2PACX-1vSf_4pUCnI38jM1ad9Bq8Ody8UK5Xrc09jec246ST8JwpSsEROGAXHuVbiInydvBtjseY88lRCSSC1P/pub?w=744&h=520)

In order to be able to perform the necessary calculations in each step of the medication cycle a specific modelling of an `Order` is needed.

The following model will be used for an `Order`:

![Order model](https://docs.google.com/drawings/d/e/2PACX-1vTgBB0m625rx2mrDYibTaQ2moIUVPkJNKTRm8yvvWu5JaOZE-HcyoDIFtfLjYQluqKkl23_p4qRJWQG/pub?w=1222&h=638)

So an `Order` consists of the following types:

- `Order`: models a specific order identified by an `Id`.
- `Schedule`: models how an `Order` is scheduled. An `Order` can only be scheduled in a single `Schedule`.
- `Orderable`: something that can be "ordered". An `Order` can have only one `Orderable`.
- `Component`: an `Orderable` can consist of 1 to `c` `Components`. In the case of medication a `Component` maps to a medication product (e.g., a vial with a specific medication concentration).
- `Item`: a `Component` can contain 1 to `i` `Items`. An `Item` can have 1 to `g` `UnitGroup` variations. In the case of medication an `Item` maps to the actual medication substance (e.g., amoxicillin).
- Each `Orderable`, `Component` and `Item` can have one or more `Dose`s. The number of `Doses` depends on the `a` `Adust` of the `Order`

This means that you can express a `Dose` for example both per kg bodyweight and per BSA (body surface area). Each `Item` can be expressed both in, for example, mass units and molar units.

The following quantities/values can be identified involved in calculation of an `Order`:

Here's the table converted to markdown format:

| No | Short Name | Long Name | Type | Unit | Description |
|----|------------|-----------|------|------|-------------|
| 1 | [itm]_cmp_qty | Item.ComponentQuantity | Quantity | Item Unit | Quantity of Item in a Component |
| 2 | [itm]_cmp_cnc | Item.ComponentConcentration | Concentration | Item Unit / Component Unit | Concentration of Item in a Component |
| 3 | [itm]_orb_qty | Item.OrderableQuantity | Quantity | Item Unit | Quantity of Item in an Orderable |
| 4 | [itm]_orb_cnc | Item.OrderableConcentration | Concentration | Item Unit / Orderable Unit | Concentration of Item in an Orderable |
| 5 | [itm]_dos_qty | Item.Dose.Quantity | Quantity | Item Unit | Dose Quantity of an Item |
| 6 | [itm]_dos_ptm | Item.Dose.PerTime | PerTime | Item Unit / Time Unit | Dose per unit time of an Item |
| 7 | [itm]_dos_rte | Item.Dose.Rate | Rate | Item Unit / Time Unit | Dose Rate of an Item |
| 8 | [itm]_dos_tot | Item.Dose.Total | Total | Item Unit | Total dose over the order duration |
| 9 | [itm]_dos_qty_adj | ItemDose.QuantityAdjust | QuantityAdjust | Item Unit / Adjust Unit | Adjusted Dose Quantity of an Item |
| 10 | [itm]_dos_ptm_adj | Item.Dose.PerTimeAdjust | PerTimeAdjust | Item Unit / Adjust Unit / Time Unit | Adjusted dose per unit time of an Item |
| 11 | [itm]_dos_rte_adj | Item.Dose.RateAdjust | RateAdjust | Item Unit / Adjust Unit / Time Unit | Adjusted Dose Rate of an Item |
| 12 | [itm]_dos_tot_adj | Item.Dose.TotalAdjust | TotalAdjust | Item Unit / Adjust Unit | Adjusted total dose over the order duration |
| 13 | [cmp]_cmp_qty | Component.Quantity | Quantity | Component Unit | Quantity of Component |
| 14 | [cmp]_orb_qty | Component.OrderableQuantity | Quantity | Component Unit | Quantity of Component in an Orderable |
| 15 | [cmp]_orb_cnc | Component.OrderableConcentration | Concentration | Component Unit / Orderable Unit | Concentration of a Component in an Orderable |
| 16 | [cmp]_orb_cnt | Component.OrderableCount | Count | Count Unit | Amount of Components in an Orderable |
| 17 | [cmp]_ord_qty | Component.OrderQuantity | Quantity | Component Unit | Quantity of Component in an Order |
| 18 | [cmp]_ord_cnt | Component.OrderCount | Count | Count Unit | Amont of Components in an Order |
| 19 | [cmp]_dos_qty | Component.Dose.Quantity | Quantity | Component Unit | Dose Quantity of an Component |
| 20 | [cmp]_dos_ptm | Component.Dose.PerTime | PerTime | Component Unit / Time Unit | Dose per unit time of a Component |
| 21 | [cmp]_dos_rte | Component.Dose.Rate | Rate | Component Unit / Time Unit | Dose Rate of an Component |
| 22 | [cmp]_dos_tot | Component.Dose.Total | Total | Component Unit | Total component dose over the order duration |
| 23 | [cmp]_dos_qty_adj | Component.Dose.QuantityAdjust | QuantityAdjust | Component Unit / Adjust Unit | Adjusted Dose Quantity of an Component |
| 24 | [cmp]_dos_ptm_adj | Component.Dose.PerTimeAdjust | PerTimeAdjust | Component Unit / Adjust Unit / Time Unit | Adjusted dose per unit time of a Component |
| 25 | [cmp]_dos_rte_adj | Component.Dose.RateAdjust | RateAdjust | Component Unit / Adjust Unit / Time Unit | Adjusted Dose Rate of an Component |
| 26 | [cmp].dos_tot_adj | Componennt.Dose.TotalAdjust | TotalAdjust | Component Unit / Adjust Unit | Adjusted total component dose over the order |
| 27 | [orb]_orb_qty | Orderable.Quantity | Quantity | Orderable Unit | Quantity of Orderable |
| 28 | [orb]_ord_qty | Orderable.OrderQuantity | Quantity | Orderable Unit | Quantity of Orderable in an Order |
| 29 | [orb]_ord_cnt | Orderable.OrderCount | Count | Count Unit | Amount of Orderable in an Order |
| 30 | [orb]_dos_cnt | Orderable.Dose.Count | Count | Count Unit | Number of dose units per orderable unit |
| 31 | [orb]_dos_qty | Orderable.Dose.Quantity | Quantity | Orderable Unit | Dose Quantity of an Orderable |
| 32 | [orb]_dos_ptm | Orderable.Dose.PerTime | PerTime | Orderable Unit / Time Unit | Dose per unit time of an Orderable |
| 33 | [orb]_dos_rte | Orderable.Dose.Rate | Rate | Orderable Unit / Time Unit | Dose Rate of an Orderable |
| 34 | [orb]_dos_tot | Orderable.Dose.Total | Total | Orderable Unit | Total orderable dose over the order duration |
| 35 | [orb]_dos_qty_adj | Orderable.Dose.QuantityAdust | QuantityAdjust | Orderable Unit / Adjust Unit | Adjusted Dose Quantity of an Orderable |
| 36 | [orb]_dos_ptm_adj | Orderable.Dose.PerTimeAdjust | PerTimeAdjust | Orderable Unit / Adjust Unit / Time Unit | Adjusted dose per unit time of an Orderable |
| 37 | [orb]_dos_rte_adj | Orderable.Dose.RateAdjust | RateAdjust | Orderable Unit / Adjust Unit / Time Unit | Adjusted Dose Rate of an Orderable |
| 38 | [orb]_dos_tot_adj | Orderable.Dose.TotalAdjust | TotalAdjust | Orderable Unit / Adjust Unit | Adjusted total orderable dose over the order |
| 39 | [ord]_sch_frq | Schedule.Frequency | Frequency | Count Unit / Time Unit | Frequency of administration |
| 40 | [ord]_sch_tme | Schedule.Time | Time | Time Unit | Time of administration |
| 41 | [ord]_adj_qty | Order.Adjust.Quantity | Quantity | Adjust Unit | Quantity used to adjust dose |
| 42 | [ord]_ord_tme | Order.Time | Time | Time Unit | Duration of the order |

- **[itm]**: should be replaced by the name of the `Item`: the full name of the item is `id.orb.cmp.itm` to avoid name clashes between items with the same name in different components/orderables, where `id` is the id of the order, `orb` is the name of the orderable, `cmp` is the name of the component, and `itm` is the name of the item.
- **[cmp]**: should be replaced by the name of the `Component`: the full name of the component is `id.orb.cmp` to avoid name clashes between components with the same name in different orderables, where `id` is the id of the order, `orb` is the name of the orderable, and `cmp` is the name of the component.
- **[orb]**: should be replaced by the name of the `Orderable`: the full name of the orderable is `id.orb` to avoid name clashes between orderables with the same name, where `id` is the id of the order, and `orb` is the name of the orderable.
- **[ord]**: should be replaced by the id of the `Order`: the name of the order is `id`, where `id` is the id of the order.

These variables are used in the following list of `Equations`:

| No | Formula | Discontinuous | Continuous | Timed | Once | OnceTimed |
|----|---------|---------------|------------|-------|------|-----------|
| 1 | [itm]_cmp_qty = [itm]_cmp_cnc * [cmp]_cmp_qty | x | x | x | x | x |
| 2 | [itm]_orb_qty = [itm]_orb_cnc * [orb]_orb_qty | x | x | x | x | x |
| 3 | [itm]_orb_qty = [itm]_cmp_cnc * [cmp]_orb_qty | x | x | x | x | x |
| 4 | [itm]_dos_qty = [itm]_cmp_cnc * [cmp]_dos_qty | x | x | x | x | x |
| 5 | [itm]_dos_qty = [itm]_orb_cnc * [orb]_dos_qty | x | x | x | x | x |
| 6 | [itm]_dos_qty = [itm]_dos_rte * [ord]_sch_tme | | | | | |
| 7 | [itm]_dos_qty = [itm]_dos_qty_adj * [ord]_adj_qty | x | x | x | x | x |
| 8 | [itm]_dos_ptm = [itm]_cmp_cnc * [cmp]_dos_ptm | x | | x | | |
| 9 | [itm]_dos_ptm = [itm]_orb_cnc * [orb]_dos_ptm | x | | x | | |
| 10 | [itm]_dos_ptm = [itm]_dos_qty * [ord]_sch_frq | x | | x | | |
| 11 | [itm]_dos_ptm = [itm]_dos_ptm_adj * [ord]_adj_qty | x | | x | | |
| 12 | [itm]_dos_rte = [itm]_cmp_cnc * [cmp]_dos_rte | | x | | | |
| 13 | [itm]_dos_rte = [itm]_orb_cnc * [orb]_dos_rte | | x | | | |
| 14 | [itm]_dos_rte = [itm]_dos_rte_adj * [ord]_adj_qty | | x | | | |
| 15 | [itm]_dos_tot = [itm]_dos_ptm * [ord]_ord_tme | x | | x | | |
| 16 | [itm]_dos_tot = [itm]_dos_rte * [ord]_ord_tme | | x | | | |
| 17 | [itm]_dos_qty_adj = [itm]_cmp_cnc * [cmp]_dos_qty_adj | x | x | x | x | x |
| 18 | [itm]_dos_qty_adj = [itm]_orb_cnc * [orb]_dos_qty_adj | x | x | x | x | x |
| 19 | [itm]_dos_qty_adj = [itm]_dos_rte_adj * [ord]_sch_tme | | | | | |
| 20 | [itm]_dos_ptm_adj = [itm]_cmp_cnc * [cmp]_dos_ptm_adj | x | x | x | | |
| 21 | [itm]_dos_ptm_adj = [itm]_orb_cnc * [orb]_dos_ptm_adj | x | x | x | | |
| 22 | [itm]_dos_ptm_adj = [itm]_dos_qty_adj * [ord]_sch_frq | x | | x | | |
| 23 | [itm]_dos_rte_adj = [itm]_cmp_cnc * [cmp]_dos_rte_adj | | x | | | |
| 24 | [itm]_dos_rte_adj = [itm]_orb_cnc * [orb]_dos_rte_adj | | x | | | |
| 25 | [itm]_dos_tot_adj = [itm]_dos_ptm_adj * [ord]_ord_tme | x | | x | | |
| 26 | [itm]_dos_tot_adj = [itm]_dos_rte_adj * [ord]_ord_tme | | x | | | |
| 27 | [cmp]_orb_qty = [cmp]_orb_cnc * [orb]_orb_qty | x | x | x | x | x |
| 28 | [cmp]_orb_qty = [orb]_dos_cnt * [cmp]_dos_qty | x | x | x | x | x |
| 29 | [cmp]_orb_qty = [cmp]_cmp_qty * [cmp]_orb_cnt | x | x | x | x | x |
| 30 | [cmp]_ord_qty = [cmp]_cmp_qty * [cmp]_ord_cnt | x | x | x | x | x |
| 31 | [cmp]_dos_tot = [cmp]_dos_ptm * [ord]_ord_tme | x | x | x | | |
| 32 | [cmp]_dos_tot = [cmp]_dos_rte * [ord]_ord_tme | | x | | | |
| 33 | [cmp]_dos_qty = [cmp]_orb_cnc * [orb]_dos_qty | x | x | x | x | x |
| 34 | [cmp]_dos_qty = [cmp]_dos_rte * [ord]_sch_tme | | | | | |
| 35 | [cmp]_dos_qty = [cmp]_dos_qty_adj * [ord]_adj_qty | x | x | x | x | x |
| 36 | [cmp]_dos_ptm = [cmp]_orb_cnc * [orb]_dos_ptm | x | | x | | |
| 37 | [cmp]_dos_ptm = [cmp]_dos_qty * [ord]_sch_frq | x | | x | | |
| 38 | [cmp]_dos_ptm = [cmp]_dos_ptm_adj * [ord]_adj_qty | x | | x | | |
| 39 | [cmp]_dos_rte = [cmp]_orb_cnc * [orb]_dos_rte | | x | | | |
| 40 | [cmp]_dos_rte = [cmp]_dos_rte_adj * [ord]_adj_qty | | x | | | |
| 41 | [cmp]_dos_qty_adj = [cmp]_orb_cnc * [orb]_dos_qty_adj | x | x | x | x | x |
| 42 | [cmp]_dos_qty_adj = [cmp]_dos_rte_adj * [ord]_sch_tme | | | | | |
| 43 | [cmp]_dos_ptm_adj = [cmp]_orb_cnc * [orb]_dos_ptm_adj | x | | x | | |
| 44 | [cmp]_dos_ptm_adj = [cmp]_dos_qty_adj * [ord]_sch_frq | x | | x | | |
| 45 | [cmp]_dos_rte_adj = [cmp]_orb_cnc * [orb]_dos_rte_adj | | x | | | |
| 46 | [orb]_orb_qty = [orb]_dos_cnt * [orb]_dos_qty | x | x | x | x | x |
| 47 | [orb]_ord_qty = [orb]_ord_cnt * [orb]_orb_qty | x | x | x | x | x |
| 48 | [orb]_dos_tot = [orb]_dos_ptm * [ord]_ord_tme | x | x | x | | |
| 49 | [orb]_dos_tot = [orb]_dos_rte * [ord]_ord_tme | x | x | x | | |
| 50 | [orb]_dos_qty = [orb]_dos_rte * [ord]_sch_tme | | x | x | | x |
| 51 | [orb]_dos_qty = [orb]_dos_qty_adj * [ord]_adj_qty | x | x | x | x | x |
| 52 | [orb]_dos_ptm = [orb]_dos_qty * [ord]_sch_frq | x | | x | | |
| 53 | [orb]_dos_ptm = [orb]_dos_ptm_adj * [ord]_adj_qty | x | | x | | |
| 54 | [orb]_dos_rte = [orb]_dos_rte_adj * [ord]_adj_qty | | x | x | | x |
| 55 | [orb]_dos_qty_adj = [orb]_dos_rte_adj * [ord]_sch_tme | | | | | |
| 56 | [orb]_dos_ptm_adj = [orb]_dos_qty_adj * [ord]_sch_frq | x | | x | | |
| 57 | [orb]_orb_qty = sum([cmp]_orb_qty) | x | x | x | x | x |
| 58 | [orb]_dos_qty = sum([cmp]_dos_qty) | x | x | x | x | x |
| 59 | [orb]_dos_ptm = sum([cmp]_dos_ptm) | x | | x | | |
| 60 | [orb]_dos_rte = sum([cmp]_dos_rte) | | x | | | |
| 61 | [orb]_dos_tot = sum([cmp]_dos_tot) | x | x | x | | |
| 62 | [orb]_dos_qty_adj = sum([cmp]_dos_qty_adj) | | | | | |
| 63 | [orb]_dos_ptm_adj = sum([cmp]_dos_ptm_adj) | | | | | |
| 64 | [orb]_dos_rte_adj = sum([cmp]_dos_rte_adj) | | | | | |
| 65 | [orb]_dos_tot_adj = sum([cmp]_dos_tot_adj) | | | | | |

## Variables for Prescription

The prescription defines regimen (how often, how long) and the intended dose at orderable and, if needed, at component/item level.

Note on rate vs per-time:

- dos.rte: instantaneous rate/speed (Unit/Time), typical for continuous infusions.
- dos.ptm: quantity per unit time (Unit/Time) derived from discrete administrations (dose x frequency). Timed regimens often specify both: dos.rte for infusion rate and dos.ptm for prescribed periodic dose.

- [ord]_sch_frq — PrescriptionFrequency — Count/Time — Number of administrations per time.
- [ord]_sch_tme — PrescriptionTime — Time — Duration of a single administration.
- [ord]_adj_qty — OrderAdjust — Adjust Unit — Patient-specific adjustor (e.g., kg or m²).
- [ord]_dur — OrderDuration — Time — Total duration of the order.

- [orb]_dos_qty — OrderableDoseQuantity — Orderable Unit — Dose per administration.
- [orb]_dos_rte — OrderableDoseRate — Orderable Unit/Time — Administration rate.
- [orb]_dos_ptm — OrderableDosePerTime — Orderable Unit/Time — Dose per unit time (e.g., per day).
- [orb]_dos_qty_adj — OrderableDoseQuantityAdjust — Orderable Unit/Adjust Unit — Adjusted dose per administration.
- [orb]_dos_rte_adj — OrderableDoseRateAdjust — Orderable Unit/Adjust Unit/Time — Adjusted administration rate.
- [orb]_dos_ptm_adj — OrderableDosePerTimeAdjust — Orderable Unit/Adjust Unit/Time — Adjusted dose per unit time.

Optional at Item/Component level when relevant:

- [itm]_dos_qty — ItemDoseQuantity — Item Unit — Dose per administration of an Item.
- [itm]_dos_rte — ItemDoseRate — Item Unit/Time — Administration rate for an Item.
- [itm]_dos_ptm — ItemDosePerTime — Item Unit/Time — Dose per unit time for an Item.
- [itm]_dos_qty_adj — ItemDoseQuantityAdjust — Item Unit/Adjust Unit — Adjusted dose per administration.
- [itm]_dos_rte_adj — ItemDoseRateAdjust — Item Unit/Adjust Unit/Time — Adjusted rate.
- [itm]_dos_ptm_adj — ItemDosePerTimeAdjust — Item Unit/Adjust Unit/Time — Adjusted dose per unit time.

Totals over the order duration (optional, mainly for verification/reporting):

- [itm]_dos_tot — ItemDoseTotal — Item Unit — Total dose over the order duration.

## Variables for Preparation

The preparation describes how to make the dose from available containers/components and their concentrations.

- [orb]_qty — OrderableQuantity — Orderable Unit — Quantity of orderable per unit preparation.
- [orb]_ord_qty — OrderableOrderQuantity — Orderable Unit — Total quantity of orderable to prepare for the order.

- [cmp]_orb_cnc — ComponentOrderableConcentration — Component Unit/Orderable Unit — Concentration of a component in the orderable.
- [cmp]_orb_qty — ComponentOrderableQuantity — Component Unit — Quantity of a component per orderable unit.
- [cmp]_orb_cnt — ComponentOrderableCount — Count — Number of components (e.g., vials/ampoules) per orderable unit.
- [cmp]_cmp_qty — ComponentQuantity — Component Unit — Quantity per component (e.g., content per vial).

- [itm]_orb_cnc — ItemOrderableConcentration — Item Unit/Orderable Unit — Concentration of an item in the orderable.
- [itm]_cmp_cnc — ItemComponentConcentration — Item Unit/Component Unit — Concentration of an item in a component.
- [itm]_cmp_qty — ItemComponentQuantity — Item Unit — Quantity of an item contributed by one component.
- [itm]_cnv — ItemUnitGroupConversionFactor — Item Unit/Item Unit — Conversion factor between item unit groups.

## Variables for Administration

Administration captures what is delivered per administration and over time.

- [ord]_sch_frq — PrescriptionFrequency — Count/Time — Number of administrations per time.
- [ord]_sch_tme — PrescriptionTime — Time — Duration of each administration.
- [ord]_dur — OrderDuration — Time — Total planned administration period.

- [orb]_dos_qty — OrderableDoseQuantity — Orderable Unit — Quantity delivered per administration.
- [orb]_dos_rte — OrderableDoseRate — Orderable Unit/Time — Delivery rate.
- [orb]_dos_ptm — OrderableDosePerTime — Orderable Unit/Time — Quantity delivered per unit time.
- [orb]_ord_qty — OrderableOrderQuantity — Orderable Unit — Total delivered over the order duration.

Totals over the order duration (optional, mainly for verification/reporting):

- [itm]_dos_tot — ItemDoseTotal — Item Unit — Total delivered over the order duration.

Optional when pump programming or verification at component/item level is required:

- [cmp]_dos_qty — ComponentDoseQuantity — Component Unit — Component quantity per administration.
- [cmp]_dos_rte — ComponentDoseRate — Component Unit/Time — Component delivery rate.
- [cmp]_dos_ptm — ComponentDosePerTime — Component Unit/Time — Component per unit time.
- [itm]_dos_qty — ItemDoseQuantity — Item Unit — Item quantity per administration.
- [itm]_dos_rte — ItemDoseRate — Item Unit/Time — Item delivery rate.
- [itm]_dos_ptm — ItemDosePerTime — Item Unit/Time — Item per unit time.
