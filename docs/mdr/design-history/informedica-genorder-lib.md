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
- `Prescription: models how an `Order` is prescribed. An `Order` can only be described in a single `Prescription`.
- `Orderable`: something that can be "ordered". An `Order` can have only one `Orderable`.
- `Component`: an `Orderable` can consist of 1 to `c` `Components`. 
- `Item`: a `Component` can contain 1 to `i` `Items`. An `Item` can have 1 to `g` `UnitGroup` variations.
- Each `Orderable`, `Component` and `Item` can have one or more `Dose`. The number of `Doses` depends on the `a` `Adust` of the `Order`

This means that you can express a `Dose` for example both per kg bodyweight and per BSA (body surface area). Each `Item` can be expressed both in, for example, mass units and molar units. 

The following quantities/values can be identified involved in calculation of an `Order`:

| No | Short Name             | Long Name                       | Unit                                     | Description                                  |
|----|------------------------|---------------------------------|------------------------------------------|----------------------------------------------|
| 1  | id.n.g.itm.cmp.qty     | ItemComponentQuantity           | Item  Unit                               | Quantity of Item in a Component              |
| 2  | id.n.g.itm.cmp.cnc     | ItemComponentConcentration      | Item  Unit / Component Unit              | Concentration of Item in a Component         |
| 3  | id.n.g.itm.orb.qty     | ItemOrderableQuantity           | Item  Unit                               | Quantity of Item in an Orderable             |
| 4  | id.n.g.itm.orb.cnc     | ItemOrderableConcentration      | Item  Unit / Orderable Unit              | Concentration of Item in an Orderable        |
| 5  | id.n.g.itm.dos.qty     | ItemDoseQuantity                | Item  Unit                               | Dose Quantity of an Item                     |
| 6  | id.n.g.itm.dos.tot     | ItemDoseTotal                   | Item  Unit / Time Unit                   | Dose Total of an Item                        |
| 7  | id.n.g.itm.dos.rte     | ItemDoseRate                    | Item  Unit / Time Unit                   | Dose Rate of an Item                         |
| 8  | id.n.g.itm.dos.ord     | ItemDoseOrder                   | Item  Unit                               | Dose Total in an Order                       |
| 9  | id.n.g.itm.dos.qty.adj | ItemDoseQuantityAdjust          | Item  Unit / Adjust Unit                 | Adjusted Dose Quantity of an Item            |
| 10 | id.n.g.itm.dos.tot.adj | ItemDoseTotalAdjust             | Item  Unit / Adjust Unit / Time Unit     | Adjusted Dose Total of an Item               |
| 11 | id.n.g.itm.dos.rte.adj | ItemDoseRateAdjust              | Item  Unit / Adjust Unit / Time Unit     | Adjusted Dose Rate of an Item                |
| 12 | id.n.g.itm.dos.ord.adj | ItemDoseOrderAdjust             | Item  Unit / Adjust Unit                 | Total adjust Dose in an Order                |
| 13 | id.n.g.itm.cnv         | ItemUnitGroupConversionFactor   | Item Unit / Item Unit                    | Conversion Factor from Base Unit Group       |
| 14 | id.n.cmp.qty           | ComponentQuantity               | Component Unit                           | Quantity of Component                        |
| 15 | id.n.cmp.orb.qty       | ComponentOrderableQuantity      | Component Unit                           | Quantity of Component in an Orderable        |
| 16 | id.n.cmp.orb.cnc       | ComponentOrderableConcentration | Component Unit / Orderable Unit          | Concentration of a Component in an Orderable |
| 17 | id.n.cmp.orb.cnt       | ComponentOrderableCount         | Count Unit                               | Amount of Components in an Orderable         |
| 18 | id.n.cmp.ord.qty       | ComponentOrderQuantity          | Component Unit                           | Quantity of Component in an Order            |
| 19 | id.n.cmp.ord.cnt       | ComponentOrderCount             | Count Unit                               | Amont of Components in an Order              |
| 20 | id.n.cmp.dos.qty       | ComponentDoseQuantity           | Component Unit                           | Dose Quantity of an Component                |
| 21 | id.n.cmp.dos.tot       | ComponentDoseTotal              | Component Unit / Time Unit               | Dose Total of an Component                   |
| 22 | id.n.cmp.dos.rte       | ComponentDoseRate               | Component Unit / Time Unit               | Dose Rate of an Component                    |
| 23 | id.n.cmp.dos.qty.adj   | ComponentDoseQuantityAdjust     | Component Unit / Adjust Unit             | Adjusted Dose Quantity of an Component       |
| 24 | id.n.cmp.dos.tot.adj   | ComponentDoseTotalAdjust        | Component Unit / Adjust Unit / Time Unit | Adjusted Dose Total of an Component          |
| 25 | id.n.cmp.dos.rte.adj   | ComponentDoseRateAdjust         | Component Unit / Adjust Unit / Time Unit | Adjusted Dose Rate of an Component           |
| 26 | id.orb.qty             | OrderableQuantity               | Orderable Unit                           | Quantity of Orderable                        |
| 27 | id.orb.ord.qty         | OrderableOrderQuantity          | Orderable Unit                           | Quantity of Orderable in an Order            |
| 28 | id.orb.ord.cnt         | OrderableOrderCount             | Count Unit                               | Amount of Orderable in an Order              |
| 29 | id.orb.dos.qty         | OrderableDoseQuantity           | Orderable Unit                           | Dose Quantity of an Orderable                |
| 30 | id.orb.dos.tot         | OrderableDoseTotal              | Orderable Unit / Time Unit               | Dose Total of an Orderable                   |
| 31 | id.orb.dos.rte         | OrderableDoseRate               | Orderable Unit / Time Unit               | Dose Rate of an Orderable                    |
| 32 | id.orb.dos.qty.adj     | OrderableDoseQuantityAdust      | Orderable Unit / Adjust Unit             | Adjusted Dose Quantity of an Orderable       |
| 33 | id.orb.dos.tot.adj     | OrderableDoseTotalAdjust        | Orderable Unit / Adjust Unit / Time Unit | Adjusted Dose Total of an Orderable          |
| 34 | id.orb.dos.rte.adj     | OrderableDoseRateAdjust         | Orderable Unit / Adjust Unit / Time Unit | Adjusted Dose Rate of an Orderable           |
| 35 | id.pres.freq           | PrescriptionFrequency           | Count Unit / Time Unit                   | Frequency of administration                  |
| 36 | id.pres.time           | PrescriptionTime                | Time Unit                                | Duration of an administration                |
| 37 | id.ord.adj             | OrderAdjust                     | Adjust Unit                              | Quantity used to adjust dose                 |
| 38 | id.ord.time            | OrderTime                       | Time Unit                                | Duration of the order                        |

- id: should be replaced by an unique identifier of the order
- n: should be replaced by the name of the `Item` and `Component`
- g: should be replaced by the unit group used for the `Item Unit`
- adj: should be replaced by the type of adjustment, bodyweight or BSA

These variables are used in the following list of `Equations`:


| No | Short Name                                                          | Long Name                                                                                   |
|----|---------------------------------------------------------------------|---------------------------------------------------------------------------------------------|
| 1  | id.n.g.itm.cmp.qty = id.n.g.itm.cmp.cnc x id.n.cmp.qty              | ItemComponentQuantity = ItemComponentConcentration x ComponentQuantity                      |
| 2  | id.n.g.itm.orb.qty = id.n.g.itm.orb.cnc x id.orb.qty                | ItemOrderableQuantity = ItemOrderableConcentration x OrderableQuantity                      |
| 3  | id.n.g.itm.orb.qty = id.n.g.itm.cmp.cnc x id.n.cmp.orb.qty          | ItemOrderableQuantity = ItemComponentConcentration x ComponentOrderableQuantity             |
| 4  | id.n.g.itm.dos.qty = id.n.g.itm.cmp.cnc x id.n.cmp.dos.qty          | ItemDoseQuantity = ItemComponentConcentration x ComponentDoseQuantity                       |
| 5  | id.n.g.itm.dos.qty = id.n.g.itm.orb.cnc x id.orb.dos.qty            | ItemDoseQuantity = ItemOrderableConcentration x OrderableDoseQuantity                       |
| 6  | id.n.g.itm.dos.qty = id.n.g.itm.dos.rte x pres.time                 | ItemDoseQuantity = ItemDoseRate x PrescriptionTime                                          |
| 7  | id.n.g.itm.dos.qty = id.n.g.itm.dos.qty.adj x id.ord.adj            | ItemDoseQuantity = ItemDoseQuantityAdjust x OrderAdjust                                     |
| 8  | id.n.g.itm.dos.tot = id.n.g.itm.cmp.cnc x id.n.cmp.dos.tot          | ItemDoseTotal = ItemComponentConcentration x ComponentDoseTotal                             |
| 9  | id.n.g.itm.dos.tot = id.n.g.itm.orb.cnc x id.orb.dos.tot            | ItemDoseTotal = ItemOrderableConcentration x OrderableDoseTotal                             |
| 10 | id.n.g.itm.dos.tot = id.n.g.itm.dos.qty x pres.freq                 | ItemDoseTotal = ItemDoseQuantity x PrescriptionFrequency                                    |
| 11 | id.n.g.itm.dos.tot = id.n.g.itm.dos.tot.adj x id.ord.adj            | ItemDoseTotal = ItemDoseTotalAdjust x OrderAdjust                                           |
| 12 | id.n.g.itm.dos.rte = id.n.g.itm.cmp.cnc x id.n.cmp.dos.rte          | ItemDoseRate = ItemComponentConcentration x ComponentDoseRate                               |
| 13 | id.n.g.itm.dos.rte = id.n.g.itm.orb.cnc x id.orb.dos.rte            | ItemDoseRate = ItemOrderableConcentration x OrderableDoseRate                               |
| 14 | id.n.g.itm.dos.rte = id.n.g.itm.dos.rte.adj x id.ord.adj            | ItemDoseRate = ItemDoseRateAdjust x OrderAdjust                                             |
| 15 | id.n.g.itm.dos.ord = item.dos.tot x id.ord.time                     | ItemDoseOrder = ItemDoseTotal x OrderTime                                                   |
| 16 | id.n.g.itm.dos.ord = item.dos.rte x id.ord.time                     | ItemDoseOrder = ItemDoseRate x OrderTime                                                    |
| 17 | id.n.g.itm.dos.qty.adj = id.n.g.itm.cmp.cnc x id.n.cmp.dos.qty.adj  | ItemDoseQuantityAdjust = ItemComponentConcentration x ComponentDoseQuantityAdjust           |
| 18 | id.n.g.itm.dos.qty.adj = id.n.g.itm.orb.cnc x id.orb.dos.qty.adj    | ItemDoseQuantityAdjust = ItemOrderableConcentration x OrderableDoseQuantityAdjust           |
| 19 | id.n.g.itm.dos.qty.adj = id.n.g.itm.dos.rte.adj x pres.time         | ItemDoseQuantityAdjust = ItemDoseRateAdjust x PrescriptionTime                              |
| 20 | id.n.g.itm.dos.tot.adj = id.n.g.itm.cmp.cnc x id.n.cmp.dos.tot.adj  | ItemDoseTotalAdjust = ItemComponentConcentration x ComponentDoseTotalAdjust                 |
| 21 | id.n.g.itm.dos.tot.adj = id.n.g.itm.orb.cnc x id.orb.dos.tot.adj    | ItemDoseTotalAdjust = ItemOrderableConcentration x OrderableDoseTotalAdjust                 |
| 22 | id.n.g.itm.dos.tot.adj = id.n.g.itm.dos.qty.adj x pres.freq         | ItemDoseTotalAdjust = ItemDoseQuantityAdjust x PrescriptionFrequency                        |
| 23 | id.n.g.itm.dos.rte.adj = id.n.g.itm.cmp.cnc x id.n.cmp.dos.rte.adj  | ItemDoseRateAdjust = ItemComponentConcentration x ComponentDoseRateAdjust                   |
| 24 | id.n.g.itm.dos.rte.adj = id.n.g.itm.orb.cnc x id.orb.dos.rte.adj    | ItemDoseRateAdjust = ItemOrderableConcentration x OrderableDoseRateAdjust                   |
| 25 | id.n.g.itm.cmp.qty = id.n.g.itm.cnv x id.n.g.itm.cmp.qty            | ItemComponentQuantity = ItemUnitGroupConversionFactor x ItemComponentQuantity               |
| 26 | id.n.g.itm.cmp.cnc = id.n.g.itm.cnv x id.n.g.itm.cmp.cnc            | ItemComponentConcentration = ItemUnitGroupConversionFactor x ItemComponentConcentration     |
| 27 | id.n.g.itm.orb.qty = id.n.g.itm.cnv x id.n.g.itm.orb.qty            | ItemOrderableQuantity = ItemUnitGroupConversionFactor x ItemOrderableQuantity               |
| 28 | id.n.g.itm.orb.cnc = id.n.g.itm.cnv x id.n.g.itm.orb.cnc            | ItemOrderableConcentration = ItemUnitGroupConversionFactor x ItemOrderableConcentration     |
| 29 | id.n.g.itm.dos.qty = id.n.g.itm.cnv x id.n.g.itm.dos.qty            | ItemDoseQuantity = ItemUnitGroupConversionFactor x ItemDoseQuantity                         |
| 30 | id.n.g.itm.dos.tot = id.n.g.itm.cnv x id.n.g.itm.dos.tot            | ItemDoseTotal = ItemUnitGroupConversionFactor x ItemDoseTotal                               |
| 31 | id.n.g.itm.dos.rte = id.n.g.itm.cnv x id.n.g.itm.dos.rte            | ItemDoseRate = ItemUnitGroupConversionFactor x ItemDoseRate                                 |
| 32 | id.n.g.itm.dos.ord = id.n.g.itm.cnv x id.n.g.itm.dos.ord            | ItemDoseOrder = ItemUnitGroupConversionFactor x ItemDoseOrder                               |
| 33 | id.n.g.itm.dos.qty.adj = id.n.g.itm.cnv x id.n.g.itm.dos.qty.adj    | ItemDoseQuantityAdjust = ItemUnitGroupConversionFactor x ItemDoseQuantityAdjust             |
| 34 | id.n.g.itm.dos.tot.adj = id.n.g.itm.cnv x id.n.g.itm.dos.tot.adj    | ItemDoseTotalAdjust = ItemUnitGroupConversionFactor x ItemDoseTotalAdjust                   |
| 35 | id.n.g.itm.dos.rte.adj = id.n.g.itm.cnv x id.n.g.itm.dos.rte.adj    | ItemDoseRateAdjust = ItemUnitGroupConversionFactor x ItemDoseRateAdjust                     |
| 36 | id.n.g.itm.dos.ord.adj = id.n.g.itm.cnv x id.n.g.itm.dos.ord.adj    | ItemDoseOrderAdjust = ItemUnitGroupConversionFactor x ItemDoseOrderAdjust                   |
| 37 | id.n.cmp.orb.qty = id.n.cmp.orb.cnc x id.orb.qty                    | ComponentOrderableQuantity = ComponentOrderableConcentration  x OrderableQuantity           |
| 38 | id.n.cmp.orb.qty = id.n.cmp.qty x id.n.cmp.orb.cnt                  | ComponentOrderableQuantity = ComponentQuantity x ComponentOrderableCount                    |
| 39 | id.n.cmp.ord.qty = id.n.cmp.qty x id.n.cmp.ord.cnt                  | ComponentOrderQuantity = ComponentQuantity x ComponentOrderCount                            |
| 40 | id.n.cmp.ord.qty = id.n.cmp.dos.tot x id.ord.time                   | ComponentOrderQuantity = ComponentDoseTotal x OrderTime                                     |
| 41 | id.n.cmp.ord.qty = id.n.cmp.dos.rte x id.ord.time                   | ComponentOrderQuantity = ComponentDoseRate x OrderTime                                      |
| 42 | id.n.cmp.dos.qty = id.n.cmp.orb.cnc x id.orb.dos.qty                | ComponentDoseQuantity = ComponentOrderableConcentration x OrderableDoseQuantity             |
| 43 | id.n.cmp.dos.qty = id.n.cmp.dos.rte x pres.time                     | ComponentDoseQuantity = ComponentDoseRate x PrescriptionTime                                |
| 44 | id.n.cmp.dos.qty = id.n.cmp.dos.qty.adj x id.ord.adj                | ComponentDoseQuantity = ComponentDoseQuantityAdjust x OrderAdjust                           |
| 45 | id.n.cmp.dos.tot = id.n.cmp.orb.cnc x id.orb.dos.tot                | ComponentDoseTotal = ComponentOrderableConcentration x OrderableDoseTotal                   |
| 46 | id.n.cmp.dos.tot = id.n.cmp.dos.qty x pres.freq                     | ComponentDoseTotal = ComponentDoseQuantity x PrescriptionFrequency                          |
| 47 | id.n.cmp.dos.tot = id.n.cmp.dos.tot.adj x id.ord.adj                | ComponentDoseTotal = ComponentDoseTotalAdjust x OrderAdjust                                 |
| 48 | id.n.cmp.dos.rte = id.n.cmp.orb.cnc x id.orb.dos.rte                | ComponentDoseRate = ComponentOrderableConcentration x OrderableDoseRate                     |
| 49 | id.n.cmp.dos.rte = id.n.cmp.dos.rte.adj x id.ord.adj                | ComponentDoseRate = ComponentDoseRateAdjust x OrderAdjust                                   |
| 50 | id.n.cmp.dos.qty.adj = id.n.cmp.orb.cnc x id.orb.dos.qty.adj        | ComponentDoseQuantityAdjust = ComponentOrderableConcentration x OrderableDoseQuantityAdjust |
| 51 | id.n.cmp.dos.qty.adj = id.n.cmp.dos.rte.adj x pres.time             | ComponentDoseQuantityAdjust = ComponentDoseRateAdjust x PrescriptionTime                    |
| 52 | id.n.cmp.dos.tot.adj = id.n.cmp.orb.cnc x id.orb.dos.tot.adj        | ComponentDoseTotalAdjust = ComponentOrderableConcentration x OrderableDoseTotalAdjust       |
| 53 | id.n.cmp.dos.tot.adj = id.n.cmp.dos.qty.adj x pres.freq             | ComponentDoseTotalAdjust = ComponentDoseQuantityAdjust x PrescriptionFrequency              |
| 54 | id.n.cmp.dos.rte.adj = id.n.cmp.orb.cnc x id.orb.dos.rte.adj        | ComponentDoseRateAdjust = ComponentOrderableConcentration x OrderableDoseRateAdjust         |
| 55 | id.orb.ord.qty = id.ord.cnt x id.orb.qty                            | OrderableOrderQuantity = OrderableOrderCount x OrderableQuantity                            |
| 56 | id.orb.ord.qty = id.orb.dos.tot x id.ord.time                       | OrderableOrderQuantity = OrderableDoseTotal x OrderTime                                     |
| 57 | id.orb.ord.qty = id.orb.dos.rte x id.ord.time                       | OrderableOrderQuantity = OrderableDoseRate x OrderTime                                      |
| 58 | id.orb.dos.qty = id.orb.dos.rte x pres.time                         | OrderableDoseQuantity = OrderableDoseRate x PrescriptionTime                                |
| 59 | id.orb.dos.qty = id.orb.dos.qty.adj x id.ord.adj                    | OrderableDoseQuantity = OrderableQuantityAdjust x OrderAdjust                               |
| 60 | id.orb.dos.tot = id.orb.dos.qty x pres.freq                         | OrderableDoseTotal = OrderableDoseQuantity x PrescriptionFrequency                          |
| 61 | id.orb.dos.tot = id.orb.dos.tot.adj x id.ord.adj                    | OrderableDoseTotal = OrderableDoseTotalAdjust x OrderAdjust                                 |
| 62 | id.orb.dos.rte = id.orb.dos.rte.adj x id.ord.adj                    | OrderableDoseRate = OrderableDoseRateAdjust x OrderAdjust                                   |
| 63 | id.orb.dos.qty.adj = id.orb.dos.rte.adj x pres.time                 | OrderableDoseQuantityAdjust = OrderableDoseRateAdjust x PrescriptionTime                    |
| 64 | id.orb.dos.tot.adj = id.orb.dos.qty.adj x pres.freq                 | OrderableDoseTotalAdjust = OrderableDoseQuantityAdust x PrescriptionFrequency               |
| 65 | id.orb.qty = sum(id.n.cmp.orb.qty)                                  | OrderableQuantity = Sum(ComponentOrderableQuantity)                                         |
| 66 | id.orb.dos.qty = sum(id.n.cmp.dos.qty)                              | OrderableDoseQuantity = Sum(ComponentDoseQuantity)                                          |
| 67 | id.orb.dos.tot = sum(id.n.cmp.dos.tot)                              | OrderableDoseTotal = Sum(ComponentDoseTotal)                                                |
| 68 | id.orb.dos.rte = sum(id.n.cmp.dos.rte)                              | OrderableDoseRate = Sum(ComponentDoseRate)                                                  |
| 69 | id.orb.dos.qty.adj = sum(id.n.cmp.dos.qty.adj)                      | OrderableDoseQuantityAdjust = Sum(ComponentDoseQuantityAdjust)                              |
| 70 | id.orb.dos.tot.adj = sum(id.n.cmp.dos.tot.adj)                      | OrderableDoseTotalAdjust = Sum(ComponentDoseTotalAdjust)                                    |
| 71 | id.orb.dos.rte.adj = sum(id.n.cmp.dos.rte.adj)                      | OrderableDoseRateAdjust = Sum(ComponentDoseRateAdjust)                                      |


