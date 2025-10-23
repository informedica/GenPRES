# Informedica.Genform Library Design History

- [Informedica.Genform Library Design History](#informedicagenform-library-design-history)
  - [Overview](#overview)
  - [Product and Substance Data Structure](#product-and-substance-data-structure)
    - [Product and Substance Model](#product-and-substance-model)
    - [Z-Index Product Structure](#z-index-product-structure)
      - [1. GenPRES Product Level Unique Identifiers](#1-genpres-product-level-unique-identifiers)
      - [2. Generic Product Level Unique Identifiers](#2-generic-product-level-unique-identifiers)
      - [3. Prescription Product Level Unique Identifiers](#3-prescription-product-level-unique-identifiers)
      - [4. Trade Product Level Unique Identifiers](#4-trade-product-level-unique-identifiers)
      - [5. Consumer Product Level Unique Identifiers](#5-consumer-product-level-unique-identifiers)
  - [Reconstitution and Solution Rules Data Structure](#reconstitution-and-solution-rules-data-structure)
    - [Solution and Reconstitution Model](#solution-and-reconstitution-model)
    - [Reconstition Rules Table](#reconstition-rules-table)
    - [Solution Rules Table](#solution-rules-table)
  - [Dose Rules Data Structure](#dose-rules-data-structure)
    - [Dose Rules Model](#dose-rules-model)
    - [Table](#table)

The Informedica.Genform library contains data structures to represent reconstitution steps and solution rules for medications. These data structures are designed to facilitate the management and application of reconstitution and solution guidelines in a clinical setting.

## Overview

- Objects that are bold are required.
- Atributes that are italic are conditionally required based on other attribute values.
- Atttributes that are bold underlined are required.

If an Object is bold and all attributes are italic, the Object is required but the attributes are conditionally required based on other attribute values. If an Object is not bold, the Object is optional.

Objects have the following relationships:

- *1..N* : One to many relationship
- *1..n* : One to zero or many relationship
- *1..1* : One to one relationship

When an object isn't required, the relationship is *1..n*, which means the relationship passes to the next object.

## Product and Substance Data Structure

### Product and Substance Model

![Product and Substance model](https://docs.google.com/drawings/d/e/2PACX-1vS3xWXvNVpM6MHRH5aAJ0S-bliMviuW1fK0chOd1PA_i8TPDpBRB4MthbspBucUURaxu5vAUrQ3R5TU/pub?w=1614&h=1488)

Products and substances are represented in the Informedica.Genform library using a structured data model. This model captures essential information about pharmaceutical products, including their generic names, brands, shapes, and associated substances. Then information is mapped from the Z-index and ParentMeds data sources. A Z-index is a comprehensive database of pharmaceutical products, while ParentMeds contains information about parent medications and their properties.

### Z-Index Product Structure

The Z-Index product structure defines products at 4 levels:

1. Generic Product
2. Prescription Product
3. Trade Product
4. Consumer Product

An additional level can be defined above the Generic Product level, called a GenPRES Product. A GenPRES product can consist of multiple Generic Products.

Each level has attributes that, together, makes them unique and identifiable.

#### 1. GenPRES Product Level Unique Identifiers

| Level | Unique Identifier | Description |
|-------|-------------------|-------------|
| GenPRES Product | Name | The combined name of Substance names from the Generic Products |
| GenPRES Product | Shape | The common shape of alle Generic Products |

#### 2. Generic Product Level Unique Identifiers

| Level | Unique Identifier | Description |
|-------|-------------------|-------------|
| Generic Product | Shape | The Shape of the Generic Product |
| Generic Product | Substances | The active substances |

*Note*: A Generic Product contains one or more active substances. Along with the Name of the Substance the Quanity (= Concentration) and Unit also uniquely identify a Generic Product.

#### 3. Prescription Product Level Unique Identifiers

| Level | Unique Identifier | Description |
|-------|-------------------|-------------|
| Prescription Product | Quantity | The Quantity of the Prescription Product |

A product quantity is added to the Generic Product to create a unique Prescription Product.

#### 4. Trade Product Level Unique Identifiers

| Level | Unique Identifier | Description |
|-------|-------------------|-------------|
| Trade Product | Brand | The Brand name of the Trade Product |
| Trade Product | Company | The Company of the Trade Product |

A Brand and Company is added to the Prescription Product to create a unique Trade Product. Furthermore, a Trade Product can have a specific set of additional substances, that are brand specific.

#### 5. Consumer Product Level Unique Identifiers

| Level | Unique Identifier | Description |
|-------|-------------------|-------------|
| Consumer Product | Quantity | The quantity per Container |
| Consumer Product | Container | The Container type |

A Container and Quantity is added to the Trade Product to create a unique Consumer Product. So, a Consumer Product can contain multiple Trade Products if the Container contains multiple units of the Trade Product.

## Reconstitution and Solution Rules Data Structure

### Solution and Reconstitution Model

![Solution and Reconstitution model](https://docs.google.com/drawings/d/e/2PACX-1vTCmWhej7l1HTUelmCR8PGOjG-VbFCXpG4tBHLRSWIayhyk-okLkkqUENUOCKugOHZP6YafcFdE_Ti3/pub?w=1440&h=1080)

### Reconstition Rules Table

| Sheet | Object | Variable | Type | Unit | Description | Comments |
|-------|--------|----------|------|------|-------------|----------|
| Reconstitution | **Generic** | **GPK** | text | | The unique Generic Product Identifier | Corresponds with GPK in the Z-index |
| Reconstitution | **Generic** | **Medication** | text | | The generic drug name | The naming from the Z-index is used in all small caps. For multiple substance drugs, concatenated with '/' |
| Reconstitution | **Generic** | **Shape** | text | | The shape name of the generic drug | The naming from the Z-index is used. |
| Reconstitution | **Route** | *Route* | text | | The route | Can be any route name using the mapping in the Routes sheet |
| Reconstitution | Location | *Dep* | text | | The department | The department the reconstitution step applies to |
| Reconstitution | **Reconstitution** | **DiluentVol** | float | mL | The volume of the diluent | The amount of diluent solution the pharmacological shape is disolved in |
| Reconstitution | **Reconstitution** | *ExpansionVol* | float | mL | The expansion volume | The quantity of the volume after reconstitution, could be more than the dilution volume |
| Reconstitution | **Reconstitution** | **Diluents** | text list | | The possible diluents that can be used | Can be a name (or names separated with ';') from the ParentMeds sheet |

### Solution Rules Table

| Sheet | Object | Variable | Type | Unit | Description | Comments |
|-------|--------|----------|------|------|-------------|----------|
| SolutionRules | **Generic** | **Generic** | text | | The generic name | The naming from the Z-index is used in all small caps. For multiple substance drugs, concatenated with '/' |
| SolutionRules | **Generic** | **Shape** | text | | The shape name of the generic drug | The naming from the Z-index is used. |
| SolutionRules | **Generic** | *Indication* | text | | | |
| SolutionRules | **Route** | **Route** | text | | The route | Can be any route name using the mapping in the Routes sheet |
| SolutionRules | DoseType | *DoseType* | once / onceTimed / discontinuous / timed / continuous | | The dosetype | The dosetypes as mentioned in the DoseRules sheet |
| SolutionRules | Vascular Access | *CVL* | boolean | | Central Venous Line | Checked if the reconstitution only applies to a CVL |
| SolutionRules | Vascular Access | *PVL* | boolean | | Peripheral Venous Line | Checked if the reconstitution only applies to a PVL |
| SolutionRules | **Patient Category** | **Dep** | text | | The department | The department the reconstitution step applies to |
| SolutionRules | **Patient Category** | *MinAge* | int | days | The minimum age | The minimum age the solution rule applies to |
| SolutionRules | **Patient Category** | *MaxAge* | int | days | The maximum age | The maximum age the solution rule applies to |
| SolutionRules | **Patient Category** | *MinWeight* | int | gram | The minimum weight | The minimum weight the solution rule applies to |
| SolutionRules | **Patient Category** | *MaxWeight* | int | gram | The maximum weight | The maximum weight the solution rule applies to |
| SolutionRules | **SolutionRule** | *MinDose* | float | unit | The minimum dose | The minimum dose the solution rule applies to (uses the unit from 'Unit') |
| SolutionRules | **SolutionRule** | *MaxDose* | float | unit | The maximum dose | The maximum dose the solution rule applies to (uses the unit from 'Unit') |
| SolutionRules | **SolutionRule** | **Solutions** | text list | | The possible solutions that can be used | Can be a name (or names separated with ';') from the ParentMeds sheet |
| SolutionRules | **SolutionRule** | *Volumes* | float list | mL | The possible volume quantities that can be used | Specific volumes can be specified |
| SolutionRules | **SolutionRule** | *MinVol* | float | mL | The minimum volume (in mL) | The minimum volume that can be used for the solution |
| SolutionRules | **SolutionRule** | *MaxVol* | float | mL | The maximum volume (in mL) | The maximum volume that can be used for the solution |
| SolutionRules | **SolutionRule** | *MinVolAdj* | float | mL / kg | | |
| SolutionRules | **SolutionRule** | *MaxVolAdj* | float | mL / kg | | |
| SolutionRules | **SolutionRule** | *MinDrip* | float | mL / hour | | |
| SolutionRules | **SolutionRule** | *MaxDrip* | float | mL / hour | | |
| SolutionRules | **SolutionRule** | *MinPerc* | float | perc | The minimum percentage of the solution to use for the DoseQuantity | In order to calculate an administration dose, it is possible to use only a percentage of the prepared solution |
| SolutionRules | **SolutionRule** | *MaxPerc* | float | perc | The maximum percentage of the solution to use for the DoseQuantity | In order to calculate an administration dose, it is possible to use only a percentage of the prepared solution |
| SolutionRules | **SolutionLimit** | **Substance** | text | | The substance used for the below fields | The substance is the generic of part of the generic name |
| SolutionRules | **SolutionLimit** | **Unit** | text | | The unit to measures the substance in | Units can be used from the Units sheet |
| SolutionRules | **SolutionLimit** | *Quantities* | float list | unit | The substance quantities that can be used | Absolute quantities that can be used for the solution and/or |
| SolutionRules | **SolutionLimit** | *MinQty* | float | unit | The minimum substance quantity (in Unit) | The minimum substance quantity that can be in the solution |
| SolutionRules | **SolutionLimit** | *MaxQty* | float | unit | The maximum substance quantity (in Unit) | The maximum substance quantity that can be in the solution |
| SolutionRules | **SolutionLimit** | *MinConc* | float | unit / mL | The minimum substance concentration (in Unit/mL) | The minimum substance concentration of the solution |
| SolutionRules | **SolutionLimit** | *MaxConc* | float | unit / mL | The maximum substance concentration (in Unit/mL) | The maximum substance concentration of the solution |

## Dose Rules Data Structure

The Informedica.Genform library contains a comprehensive data structure to represent dose rules for medications. These dose rules are designed to accommodate various patient characteristics, medication properties, and administration guidelines.

### Dose Rules Model

![Dose Rules model](https://docs.google.com/drawings/d/e/2PACX-1vQ0JtMXGCuyZ4Tw_EjHErHbvI7b5qXSJjTQsveI8kBbRPyAkh1RzTtw_NsbaPNyiKYgPufPWAk-ZduD/pub?w=1566&h=1022)

### Table

| Sheet | Object | Variable | Type | Unit | Description | Comments |
|-------|--------|----------|------|------|-------------|----------|
| DoseRules | **Source** | **Source** | text | | The source of the dose rule | |
| DoseRules | **Generic** | **Name** | text | | The generic name | The naming from the Z-index is used in all small caps. For multiple substance drugs, concatenated with '/' |
| DoseRules | **Generic** | *Shape* | text | | A shape to narrow down the dose rule | The naming from the Z-index is used. |
| DoseRules | **Generic** | *Brand* | text | | A brand to narrow down the dose rule | |
| DoseRules | **Generic** | *GPKs* | text list | | A list of GPKs to narrow down the dose rule | |
| DoseRules | **Indication** | **Indication** | text | | The indication (label) for the dose rule | Indications are from the Kinderformularium or from the Farmacotherapeutisch compas |
| DoseRules | **Route** | **Route** | text | | The route | Can be any route name using the mapping in the Routes sheet |
| DoseRules | **Patient Category** | *Dep* | text | | The department (location) | The department the dose rule applies to |
| DoseRules | **Patient Category** | *Gender* | male / female | | The gender | The gender the doserule applies to |
| DoseRules | **Patient Category** | *MinAge* | int | days | The minimum age | The minimum age the dose rule applies to |
| DoseRules | **Patient Category** | *MaxAge* | int | days | The maximum age | The maximum age the dose rule applies to |
| DoseRules | **Patient Category** | *MinWeight* | int | gram | The minimum weight (in gram) | The minimum weight the dose rule applies to |
| DoseRules | **Patient Category** | *MaxWeight* | int | gram | The maximum weight | The maximum weight the dose rule applies to |
| DoseRules | **Patient Category** | *MinBSA* | float | m2 | The minimum bsa | The minimum bsa the dose rule applies to |
| DoseRules | **Patient Category** | *MaxBSA* | float | m2 | The maximum bsa | The maximum bsa the dose rule applies to |
| DoseRules | **Patient Category** | *MinGestAge* | int | days | The minimum gestational age | The minimum gestational age the dose rule applies to |
| DoseRules | **Patient Category** | *MaxGestAge* | int | days | The maximum gestational age | The maximum gestational age the dose rule applies to |
| DoseRules | **Patient Category** | *MinPMAge* | int | days | The minimum postmenstrual age | The minimum postmenstrual age the dose rule applies to |
| DoseRules | **Patient Category** | *MaxPMAge* | int | days | The maximum postmenstrual age | The maximum postmenstrual age the dose rule applies to |
| DoseRules | **DoseType** | **DoseType** | once / onceTimed / discontinuous / timed / continuous | | The dose type of the dose rule | Can be onderhoud, start, continu, eenmalig, conta, afbouw 'n', or prn. Afbouw can have a number of the 'afbouw' day |
| DoseRules | **DoseType** | *DoseText* | text | | A label for a dose type | |
| DoseRules | **Component** | **Component** | text | | The product the substance belongs to | |
| DoseRules | Substance | *Substance* | text | | The substance used for the below fields | The substance is the generic of part of the generic name, note if generic constists of n substances, there can be up to n dose rules for that generic |
| DoseRules | Schedule | *Freqs* | int list | count_unit/ freq_unit | The possible frequencies | A number list separated with ';', the per time unit = FreqUnit |
| DoseRules | Schedule | *FreqUnit* | text | | The freq unit | The time unit of the frequencies |
| DoseRules | Schedule | *MinTime* | float | time_unit | The minimum time for infusion of a dose | Can be used to calculate an infusion rate for a administration of a substance dose |
| DoseRules | Schedule | *MaxTime* | float | time_unit | The maximum time for infusion of a dose | Can be used to calculate an infusion rate for a administration of a substance dose |
| DoseRules | Schedule | *TimeUnit* | text | | The time unit to measure the infusion | Typically the time unit will be in minute or hours |
| DoseRules | Schedule | *MinInt* | float | int_unit | The minimum interval between two doses | The minimum time interfal between 2 doses, can also be used to calculated a range of frequencies |
| DoseRules | Schedule | *MaxInt* | float | int_unit | The maximum interval between two doses | The maximum time interfal between 2 doses, can also be used to calculate a range of frequencies |
| DoseRules | Schedule | *IntUnit* | text | | The interval unit | The time unit to measure a maximum or minimum interval |
| DoseRules | Schedule | *MinDur* | float | dur_unit | The minimum duration of the dose rule | The time the dose rule is applied |
| DoseRules | Schedule | *MaxDur* | float | dur_unit | The maximum duration of the dose rule | The time the dose rule is applied |
| DoseRules | Schedule | *DurUnit* | text | | The duration time unit | The time unit to measure a maximum or minimum duration |
| DoseRules | **DoseLimit** | **DoseUnit** | text | | The dose unit | The unit used to express the component or substance dose |
| DoseRules | **DoseLimit** | *RateUnit* | text | | The rate unit | The time unit for a dose rate |
| DoseRules | **DoseLimit** | *AdjustUnit* | kg / m2 | | The adjust unit | The unit to adjust the dose with, i.e. kg or m2 |
| DoseRules | **DoseLimit** | *MinQty* | float | dose_unit | The minimum dose quantity | The minimum dose that can be administered each time |
| DoseRules | **DoseLimit** | *MaxQty* | float | dose_unit | The maximum dose quantity | The maximum dose that can be administered each time |
| DoseRules | **DoseLimit** | *NormQtyAdj* | float | dose / adjust_unit | The 'normal' adjusted dose quantity | The recommended adjusted dose quantity, note that this or either a mimimum and/or maximum should be defined. From the normal adjusted dose a +/- 10% margin will be used. |
| DoseRules | **DoseLimit** | *MinQtyAdj* | float | dose / adjust_unit | The minimum adjusted dose quantity | The minimum adjusted dose quantity administered each time |
| DoseRules | **DoseLimit** | *MaxQtyAdj* | float | dose / adjust_unit | The maximum adjusted dose quantity | The maximum adjusted dose quantity administered each time |
| DoseRules | **DoseLimit** | *MinPerTime* | float | dose_unit / freq_unit | The minimum dose quantity per time | The total minimum dose per freq time unit |
| DoseRules | **DoseLimit** | *MaxPerTime* | float | dose_unit / freq_unit | The maximum dose quantity per time | The total maximum dose per freq time unit |
| DoseRules | **DoseLimit** | *NormPerTimeAdj* | float | dose_unit/ adjust_unit/freq_unit | The 'normal' adjusted dose quantity per time | The recommended total adjusted dose quantity per frequency unit, note that this or either a mimimum and/or maximum should be defined. From the normal adjusted dose a +/- 10% margin will be used. |
| DoseRules | **DoseLimit** | *MinPerTimeAdj* | float | dose_unit/ adjust_unit/freq_unit | The minimum adjusted dose quantity per time | The total minimum dose per freq time unit |
| DoseRules | **DoseLimit** | *MaxPerTimeAdj* | float | dose_unit/ adjust_unit/freq_unit | The maximum dose adjusted quantity per time | The total maximum dose per freq time unit |
| DoseRules | **DoseLimit** | *MinRate* | float | dose_unit / rate_unit | The minimum dose rate | The total minimum dose rate per rate time unit |
| DoseRules | **DoseLimit** | *MaxRate* | float | dose_unit / rate_unit | The maximum dose rate | The total maximum dose rate per rate time unit |
| DoseRules | **DoseLimit** | *MinRateAdj* | float | dose_unit / adjust_unit / rate_unit | The minimum adjusted dose rate | The total minimum adjusted dose rate per adjust unit per rate time unit |
| DoseRules | **DoseLimit** | *MaxRateAdj* | float | dose_unit / adjust_unit / rate_unit | The maximum adjusted dose rate | The total maximum adjusted dose rate per adjust unit per rate time unit |
