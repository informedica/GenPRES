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
  - [Comparison with G-Standaard (ZForm) Implementation](#comparison-with-g-standaard-zform-implementation)
    - [Overview GenForm vs ZForm](#overview-genform-vs-zform)
    - [Architectural Differences](#architectural-differences)
      - [GenForm Structure (Flat, Filter-Based)](#genform-structure-flat-filter-based)
      - [ZForm Structure (Hierarchical, Collection-Based)](#zform-structure-hierarchical-collection-based)
      - [ZIndex Source Structure (G-Standaard Data)](#zindex-source-structure-g-standaard-data)
    - [Dose Representation Comparison](#dose-representation-comparison)
      - [GenForm DoseLimit (Multi-Dimensional)](#genform-doselimit-multi-dimensional)
      - [ZForm DoseRange (Six Explicit Fields)](#zform-doserange-six-explicit-fields)
      - [ZForm Dosage (Multiple Dosage Types)](#zform-dosage-multiple-dosage-types)
      - [ZIndex DoseRule (Source Data)](#zindex-doserule-source-data)
    - [Mapping: GenForm ↔ ZForm ↔ ZIndex](#mapping-genform--zform--zindex)
      - [ZIndex → ZForm Transformation (Implemented in GStand.fs)](#zindex--zform-transformation-implemented-in-gstandfs)
      - [ZForm → GenForm Mapping (Conceptual - Not Implemented)](#zform--genform-mapping-conceptual---not-implemented)
      - [GenForm → ZForm Mapping (Conceptual - Not Implemented)](#genform--zform-mapping-conceptual---not-implemented)
    - [Patient Category Differences](#patient-category-differences)
    - [Schedule and Timing Differences](#schedule-and-timing-differences)
    - [Product and Classification Differences](#product-and-classification-differences)
    - [DoseType Semantics Comparison](#dosetype-semantics-comparison)
      - [GenForm DoseType (Categorical)](#genform-dosetype-categorical)
      - [ZForm Dosage Types (Multi-Faceted)](#zform-dosage-types-multi-faceted)
    - [Component vs Substance Organization](#component-vs-substance-organization)
      - [GenForm (Component-Centric)](#genform-component-centric)
      - [ZForm (Substance-Centric)](#zform-substance-centric)
    - [Source and Provenance Tracking](#source-and-provenance-tracking)
    - [Clinical Features Comparison](#clinical-features-comparison)
      - [GenForm-Specific Features (Not in ZForm/ZIndex)](#genform-specific-features-not-in-zformzindex)
      - [ZForm-Specific Features (Not in GenForm)](#zform-specific-features-not-in-genform)
      - [ZIndex-Specific Features (Not in GenForm/ZForm)](#zindex-specific-features-not-in-genformzform)
    - [Summary of Key Discrepancies](#summary-of-key-discrepancies)
      - [1. Architectural Philosophy](#1-architectural-philosophy)
      - [2. Dose Representation](#2-dose-representation)
      - [3. Clinical Focus](#3-clinical-focus)
      - [4. Organization Principle](#4-organization-principle)
      - [5. Interoperability](#5-interoperability)
    - [Use Case Alignment](#use-case-alignment)
      - [GenForm Best For:](#genform-best-for)
      - [ZForm Best For:](#zform-best-for)
      - [ZIndex Best For:](#zindex-best-for)
    - [Recommendations for Integration](#recommendations-for-integration)

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

GenForm.Lib has a simplified structure:

❌ Missing: No GenPresProduct equivalent
❌ Missing: No PrescriptionProduct equivalent
❌ Missing: No TradeProduct equivalent
❌ Missing: No ConsumerProduct equivalent
✅ Has only a single Product type that conflates multiple levels

| Documentation | GenForm.Lib Field | ZIndex.Lib Field | Status |
|---|---|---|---|
| GPK (Generic Product ID) | ✅ GPK: string | ✅ Id: int | Mismatch: Different types (string vs int) |
| Generic Name | ✅ Generic: string | ✅ Name: string | Naming inconsistency |
| Shape | ✅ Shape: string | ✅ Shape: string | ✅ Match |
| Brand | ❌ Missing | ✅ Brand: string (in TradeProduct) | Missing in GenForm |
| Company | ❌ Missing | ✅ Company: string (in TradeProduct) | Missing in GenForm |
| Container | ❌ Missing | ✅ Container: string (multiple levels) | Missing in GenForm |

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

## Comparison with G-Standaard (ZForm) Implementation

### Overview GenForm vs ZForm

The GenForm library uses a flat, filter-based dose rule structure optimized for prescription generation, while the ZForm library (which processes G-Standaard data from the Dutch Z-Index) uses a hierarchical, collection-based structure. This section documents the key differences and mappings between these two implementations.

### Architectural Differences

#### GenForm Structure (Flat, Filter-Based)

```text
DoseRule (single level)
├── PatientCategory (embedded filter)
├── ShapeLimit (optional)
└── ComponentLimits[] (array)
    └── SubstanceLimits[] (array of DoseLimit)
```

**Key characteristics**:

- Flat structure with embedded patient category
- One rule per
  - Indication,
  - Generic,
  - Product identifier list,
  - Brand option,
  - Shape option,
  - Route,
  - Patient Category,
  - DoseType + DoseText
- Filter-oriented: patient attributes are ranges to match against
- Optional atributes can be used to refine applicability of the rule
- Component-centric: explicit Component grouping of substances

*Note*: The presence or absence of optional attributes means that the rule can be generelalized or specialized as needed. At minumum, a DoseRule must specify the Generic name, Indication, Route, Patient Category, and DoseType to be valid.

#### ZForm Structure (Hierarchical, Collection-Based)

```text
DoseRule (G-Standaard based)
└── IndicationDosage[] (list - groups by indication)
    └── RouteDosage[] (list - groups by route)
        └── ShapeDosage[] (list - groups by shape)
            └── PatientDosage[] (list - groups by patient category)
                ├── ShapeDosage (single Dosage)
                └── SubstanceDosages[] (list of Dosage)
```

**Key characteristics**:

- Five-level hierarchy: Generic → Indication → Route → Shape → Patient
- Collection-oriented: multiple indications/routes/shapes per rule
- Preserves G-Standaard organization structure
- Substance-centric: no explicit Component level

#### ZIndex Source Structure (G-Standaard Data)

```text
DoseRule (flat record from Z-Index database)
├── Identification fields (Id, CareGroup, Usage, DoseType)
├── Product arrays (GenericProduct[], PrescriptionProduct[], TradeProduct[])
├── Filter fields (Routes[], IndicationId, Indication, Gender, Age, Weight, BSA)
├── Frequency (Freq.Frequency, Freq.Time)
└── Six dose fields: Norm, Abs, NormKg, AbsKg, NormM2, AbsM2
```

**Key characteristics**:

- Flat structure directly from database
- Six explicit dose fields (normal/absolute × unadjusted/per-kg/per-m²)
- String-based units and simple numeric types
- Dutch healthcare-specific fields (CareGroup, HighRisk)

### Dose Representation Comparison

#### GenForm DoseLimit (Multi-Dimensional)

```fsharp
type DoseLimit = {
    DoseLimitTarget: LimitTarget        // Shape, Component, or Substance
    AdjustUnit: Unit option             // kg or m² (parameterizes adjustment)
    DoseUnit: Unit                      // Primary dose unit
    Quantity: MinMax                    // Unadjusted dose range
    NormQuantityAdjust: ValueUnit option // Normal adjusted dose (±10%)
    QuantityAdjust: MinMax              // Adjusted dose range
    PerTime: MinMax                     // Total dose per time period
    NormPerTimeAdjust: ValueUnit option // Normal per-time adjusted (±10%)
    PerTimeAdjust: MinMax               // Adjusted per-time range
    Rate: MinMax                        // Infusion rate
    RateAdjust: MinMax                  // Adjusted infusion rate
}
```

**Design rationale**:

- Single structure with AdjustUnit parameter to handle kg vs m² adjustment
- Separates quantity, per-time, and rate dimensions
- Uses "Norm" values to establish ±10% acceptable range
- Strongly typed with ValueUnit

#### ZForm DoseRange (Six Explicit Fields)

```fsharp
type DoseRange = {
    Norm: MinMax                        // Normal unadjusted
    NormWeight: MinMax * Unit           // Normal per kg
    NormBSA: MinMax * Unit              // Normal per m²
    Abs: MinMax                         // Absolute unadjusted
    AbsWeight: MinMax * Unit            // Absolute per kg
    AbsBSA: MinMax * Unit               // Absolute per m²
}
```

**Design rationale**:

- Directly mirrors G-Standaard's six-field structure
- Explicit separation of normal vs absolute limits
- Explicit separation of unadjusted vs weight-adjusted vs BSA-adjusted
- Tuple pairs include the adjustment unit with the range

#### ZForm Dosage (Multiple Dosage Types)

```fsharp
type Dosage = {
    Name: string                        // Substance name
    StartDosage: DoseRange              // Loading/initial dose
    SingleDosage: DoseRange             // Dose per administration
    RateDosage: DoseRange * RateUnit    // Continuous infusion rate
    TotalDosage: DoseRange * Frequency  // Total dose per time period
    Rules: Rule list                    // Original G-Standaard/PedForm rules
}
```

**Design rationale**:

- Four dosage aspects per substance (start/single/rate/total)
- Each aspect has the full six-field DoseRange
- Preserves provenance via Rules field
- Frequency embedded in TotalDosage with structured Frequency type

*Note*: The original G-Standaard dose rules do not have a direct equivalent to ZForm's Dosage structure. The structure is derived during the ZIndex → ZForm transformation based on the dose fields present in the source data.

#### ZIndex DoseRule (Source Data)

```fsharp
type DoseRule = {
    // ... other fields ...
    Freq: RuleFrequency                 // { Frequency: float; Time: string }
    Norm: RuleMinMax                    // { Min: float option; Max: float option }
    Abs: RuleMinMax
    NormKg: RuleMinMax
    AbsKg: RuleMinMax
    NormM2: RuleMinMax
    AbsM2: RuleMinMax
    Unit: string                        // Dose unit as string
}
```

**Design rationale**:

- Exactly matches G-Standaard database schema
- Simple types (float, string) for database mapping
- Six fields correspond to Dutch prescribing guidelines
- Single frequency per rule

### Mapping: GenForm ↔ ZForm ↔ ZIndex

#### ZIndex → ZForm Transformation (Implemented in GStand.fs)

The `GStand.createDoseRules` function performs this transformation:

1. **Grouping**: Flat ZIndex rules → grouped by (Generic, ATC groups, Indication, Route, Shape, Patient category)
2. **Dose mapping**: Six flat fields → DoseRange record with tuples
3. **Frequency parsing**: String-based `RuleFrequency` → typed `Frequency` with `BigRational list` and `TimeUnit`
4. **Patient aggregation**: Individual rule filters → grouped `PatientCategory` with merged ranges
5. **Hierarchy building**: Flat rules → nested Indication → Route → Shape → Patient structure
6. **Dosage categorization**: Based on `CreateConfig.IsRate`:
   - If rate: populate `RateDosage`
   - Otherwise: populate `SingleDosage` and `TotalDosage`
7. **Unit conversion**: String units → strongly typed `Unit` from GenUnits library

**Key functions**:

- `mapDoses`: Maps six dose fields to DoseRange, multiplies by frequency
- `getDoseRange`: Aggregates multiple doses into single DoseRange
- `getSubstanceDoses`: Creates Dosages per substance with appropriate dosage type
- `getPatients`: Groups rules by patient category
- `groupGenPresProducts`: Builds indication/route/shape/patient hierarchy

#### ZForm → GenForm Mapping (Conceptual - Not Implemented)

This mapping would require:

1. **Hierarchy flattening**: Create separate GenForm DoseRule for each (Indication, Route, Shape, PatientDosage) combination
2. **DoseRange → DoseLimit consolidation**:
   - Extract `Quantity` from Norm (min) and Abs (max) if both present
   - Determine `AdjustUnit` from presence of NormWeight/AbsWeight (kg) vs NormBSA/AbsBSA (m²)
   - Populate `QuantityAdjust` from weight or BSA ranges
   - If both weight and BSA present, create two separate rules or choose based on policy
3. **Dosage type selection**:
   - Map `RateDosage` → GenForm rule with Rate/RateAdjust populated
   - Map `TotalDosage` → GenForm rule with PerTime/PerTimeAdj populated
   - Map `SingleDosage` → GenForm rule with Quantity/QuantityAdjust populated
4. **Frequency extraction**: From `TotalDosage.Frequency` → `Frequencies: ValueUnit option`
5. **Component creation**: 
   - Group SubstanceDosages by product (if available) → ComponentLimit
   - Each SubstanceDosage → separate SubstanceLimit in ComponentLimit
6. **Patient category mapping**:
   - ZForm PatientCategory → GenForm PatientCategory (mostly compatible)
   - Add GenForm-specific fields: Department, Location (default to AnyAccess), PMAge (default to empty)
7. **Provenance**: Extract Rules list → Source field (take first or concatenate)

**Challenges**:

- Loss of hierarchy information (multiple indications/routes flattened)
- Ambiguity when both weight and BSA adjustments exist
- Multiple dosage types (Start/Single/Rate/Total) need policy for priority
- GenForm's Component concept has no ZForm equivalent

#### GenForm → ZForm Mapping (Conceptual - Not Implemented)

This reverse mapping would require:

1. **Grouping for hierarchy**:
   - Group GenForm DoseRules by (Generic, Indication, Route, Shape)
   - Create IndicationDosage for each unique (Generic, Indication)
   - Create RouteDosage for each unique (Generic, Indication, Route)
   - Create ShapeDosage for each unique (Generic, Indication, Route, Shape)
   - Create PatientDosage for each GenForm DoseRule's PatientCategory
2. **DoseLimit → DoseRange expansion**:
   - Quantity → Norm or Abs (need policy: use Norm if within normal practice)
   - QuantityAdjust + AdjustUnit=kg → NormWeight or AbsWeight
   - QuantityAdjust + AdjustUnit=m² → NormBSA or AbsBSA
   - If NormQuantityAdjust present, use as center with ±10% for range
3. **Dosage type distribution**:
   - If Rate/RateAdjust populated → RateDosage
   - If PerTime/PerTimeAdj populated → TotalDosage
   - Quantity/QuantityAdjust → SingleDosage
   - StartDosage: requires heuristic or left empty
4. **Frequency creation**:
   - Extract from GenForm Frequencies → structured Frequency record
   - Parse time unit → TimeUnit
   - IntervalTime → MinimalInterval
5. **Substance extraction**:
   - ComponentLimits[] → flatten to SubstanceDosages list
   - Each SubstanceLimit → Dosage record
   - ShapeLimit → ShapeDosage (if present)
6. **Product mapping**:
   - Extract GPKs from ComponentLimits or DoseRule
   - Map to GenericProductLabel list (need GPK int conversion)
   - TradeProductLabel: not available in GenForm
7. **ATC and groups**: Not available in GenForm, would need lookup

**Challenges**:

- Component structure doesn't map to ZForm's flat substance list
- GenForm's three dose dimensions (Quantity/PerTime/Rate) need distribution to four ZForm dosages
- Missing ATC, therapy groups, generic groups
- Loss of brand/trade product information
- Department, Location, PMAge, RenalRule have no ZForm equivalent

### Patient Category Differences

| Field | GenForm | ZForm | ZIndex | Mapping Notes |
|-------|---------|-------|--------|---------------|
| **Department** | `string option` | ❌ Not present | ❌ Not present | GenForm-specific; drop when mapping to ZForm |
| **Gender** | `Male \| Female \| AnyGender` | `Male \| Female \| Undetermined` | `string` ("man"/"vrouw"/"") | Enum name differs; `AnyGender` ↔ `Undetermined` ↔ "" |
| **Age** | `MinMax` (ValueUnit) | `MinMax` (ValueUnit) | `RuleMinMax` (float option) | Compatible between GenForm/ZForm; ZIndex needs unit conversion |
| **Weight** | `MinMax` (ValueUnit) | `MinMax` (ValueUnit) | `RuleMinMax` (float option) | Compatible between GenForm/ZForm; ZIndex needs unit conversion |
| **BSA** | `MinMax` (ValueUnit) | `MinMax` (ValueUnit) | `RuleMinMax` (float option) | Compatible between GenForm/ZForm; ZIndex needs unit conversion |
| **GestAge** | `MinMax` (ValueUnit) | `MinMax` (ValueUnit) | ❌ Not present | Compatible GenForm/ZForm; ZIndex lacks gestational age |
| **PMAge** | `MinMax` (ValueUnit) | ❌ Not present | ❌ Not present | GenForm-specific for neonates; drop when mapping to ZForm |
| **Location** | `PVL \| CVL \| AnyAccess` | ❌ Not present | ❌ Not present | GenForm-specific for venous access; drop when mapping to ZForm |

### Schedule and Timing Differences

| Aspect | GenForm | ZForm | ZIndex | Notes |
|--------|---------|-------|--------|-------|
| **Frequency** | `Frequencies: ValueUnit option` | `Frequencies: BigRational list` + `TimeUnit: Unit` + `MinimalInterval: ValueUnit option` | `Freq: { Frequency: float; Time: string }` | ZForm most structured; GenForm simplest |
| **Administration time** | `AdministrationTime: MinMax` | ❌ Not present | ❌ Not present | GenForm-specific for infusion duration |
| **Interval** | `IntervalTime: MinMax` | `MinimalInterval` (in Frequency) | ❌ Not present | Different concepts: GenForm has full range, ZForm has minimum only |
| **Duration** | `Duration: MinMax` | ❌ Not present | ❌ Not present | GenForm-specific for therapy duration |

### Product and Classification Differences

| Aspect | GenForm | ZForm | ZIndex | Notes |
|--------|---------|-------|--------|-------|
| **Generic name** | `Generic: string` | `Generic: string` | `Name: string` (in GenPresProduct) | Compatible, field name differs |
| **Shape** | `Shape: string` | `Shape: string list` | `Shape: string` (in GenPresProduct) | ZForm allows multiple shapes |
| **Route** | `Route: string` | `Route: string` | `Routes: string[]` | ZForm/ZIndex allow multiple routes |
| **Brand** | `Brand: string option` | ❌ Not present | `Brand: string` (in TradeProduct) | GenForm and ZIndex have brand, ZForm doesn't |
| **GPK** | In ComponentLimits as `string array` | `GPK: int` (in GenericProductLabel) | `Id: int` (in GenericProduct) | Type difference: GenForm uses string, ZForm/ZIndex use int |
| **HPK** | ❌ Not present | `HPK: int` (in TradeProductLabel) | `Id: int` (in TradeProduct) | ZForm and ZIndex track trade products |
| **ATC** | ❌ Not present | `ATC: string` | `ATC: string` (in GenericProduct) | ZForm and ZIndex include ATC classification |
| **Therapy groups** | ❌ Not present | `ATCTherapyGroup: string`, `ATCTherapySubGroup: string` | ❌ Not present (computed) | ZForm-specific from ATC |
| **Generic groups** | ❌ Not present | `GenericGroup: string`, `GenericSubGroup: string` | ❌ Not present (computed) | ZForm-specific grouping |
| **Synonyms** | ❌ Not present | `Synonyms: string list` | `Synonyms: string[]` (in GenPresProduct) | ZForm and ZIndex track alternative names |

### DoseType Semantics Comparison

#### GenForm DoseType (Categorical)

```fsharp
type DoseType =
    | Once of string        // Single dose (e.g., pre-operative)
    | Discontinuous of string // Maintenance dosing (e.g., q6h)
    | Continuous of string  // Continuous infusion
    | Timed of string       // Discontinuous with time specification
    | OnceTimed of string   // Once with time specification
    | NoDoseType
```

**Semantics**: Categorizes the entire rule by usage pattern. Each rule has exactly one DoseType.

#### ZForm Dosage Types (Multi-Faceted)

```fsharp
type Dosage = {
    StartDosage: DoseRange      // Initial/loading dose
    SingleDosage: DoseRange     // Per-administration dose
    RateDosage: DoseRange       // Continuous rate
    TotalDosage: DoseRange      // Total per time period
}
```

**Semantics**: Each substance has four dosage aspects. All can be populated simultaneously. Not mutually exclusive.

**Mapping implications**:

- GenForm's categorical DoseType doesn't directly map to ZForm's multi-faceted Dosage
- GenForm Continuous → ZForm RateDosage populated
- GenForm Discontinuous → ZForm SingleDosage and TotalDosage populated
- GenForm Once → ZForm SingleDosage populated, frequency = 1
- Reverse mapping requires policy: which ZForm dosage to use for GenForm DoseType?

### Component vs Substance Organization

#### GenForm (Component-Centric)

```fsharp
type DoseRule = {
    ShapeLimit: DoseLimit option    // Limits at shape level
    ComponentLimits: ComponentLimit[]
}

type ComponentLimit = {
    Name: string                    // Component identifier
    GPKs: string array              // Products in this component
    Limit: DoseLimit option         // Component-level limits
    Products: Product[]             // Product details
    SubstanceLimits: DoseLimit[]    // Substance-level limits
}
```

**Semantics**: 

- Explicit "Component" grouping (e.g., "Component A" + "Component B")
- Allows different products within same component
- Three-level limit hierarchy: Shape → Component → Substance

#### ZForm (Substance-Centric)

```fsharp
type PatientDosage = {
    ShapeDosage: Dosage             // Shape-level dosing
    SubstanceDosages: Dosage list   // Substance-level dosing
}

type ShapeDosage = {
    GenericProducts: GenericProductLabel list  // Products for this shape
    TradeProducts: TradeProductLabel list
    // ...
}
```

**Semantics**:

- No explicit Component level
- Substances are directly under PatientDosage
- Two-level limit hierarchy: Shape → Substance
- Products associated at ShapeDosage level, not per substance

**Mapping implications**:

- GenForm Component concept has no ZForm equivalent
- When mapping GenForm → ZForm: flatten ComponentLimits to SubstanceDosages, lose component grouping
- When mapping ZForm → GenForm: all substances become separate components, or group heuristically

### Source and Provenance Tracking

| Aspect | GenForm | ZForm | ZIndex | Purpose |
|--------|---------|-------|--------|---------|
| **Source identifier** | `Source: string` | ❌ Not present | ❌ Not present | GenForm tracks source (e.g., "Kinderformularium") |
| **Original rules** | ❌ Not present | `Rules: Rule list` where `Rule = GStandRule of string \| PedFormRule of string` | ❌ Not present (is the source) | ZForm preserves original rule text |
| **Schedule text** | `ScheduleText: string` | ❌ Not present | ❌ Not present | GenForm keeps human-readable schedule |
| **Rule ID** | ❌ Not present | ❌ Not present | `Id: int` | ZIndex has unique rule identifier |
| **Care group** | ❌ Not present | ❌ Not present | `CareGroup: string` | ZIndex tracks intensive/non-intensive care |
| **Usage** | ❌ Not present | ❌ Not present | `Usage: string` | ZIndex tracks therapeutic/prophylactic |
| **High risk** | ❌ Not present | ❌ Not present | `HighRisk: bool` | ZIndex flags high-risk medications |

### Clinical Features Comparison

#### GenForm-Specific Features (Not in ZForm/ZIndex)

1. **Post-Menstrual Age (PMAge)**: Critical for neonatal dosing
2. **Venous Access Location**: PVL vs CVL affects concentration limits
3. **Administration Time**: Infusion duration for rate calculation
4. **Interval Time**: Full range (not just minimum)
5. **Duration**: Therapy duration limits
6. **Department**: Location-specific rules (ICU, NICU, etc.)
7. **Brand-Specific Rules**: Some rules only for specific brands
8. **Renal Rules**: Link to renal dose adjustments
9. **Component Organization**: Multi-component products (e.g., TPN)
10. **Schedule Text**: Human-readable schedule for display

**Rationale**: GenForm optimized for pediatric/neonatal intensive care prescription generation.

#### ZForm-Specific Features (Not in GenForm)

1. **ATC Classification**: Full ATC code and derived groups
2. **Therapy Groups**: ATCTherapyGroup, ATCTherapySubGroup
3. **Generic Groups**: GenericGroup, GenericSubGroup
4. **Synonyms**: Trade names and alternatives
5. **Hierarchical Organization**: Indication → Route → Shape → Patient
6. **Product Labels**: Separate GenericProductLabel and TradeProductLabel tracking
7. **Rule Provenance**: Original G-Standaard or PedForm rule text
8. **Multiple Shapes/Routes**: One rule can cover multiple shapes and routes

**Rationale**: ZForm preserves G-Standaard structure and provides comprehensive drug classification.

#### ZIndex-Specific Features (Not in GenForm/ZForm)

1. **Rule ID**: Unique database identifier
2. **Care Group**: Intensive vs non-intensive care
3. **Usage**: Therapeutic vs prophylactic
4. **DoseType**: "standaard" vs "verbyzondering" (standard vs special indication)
5. **High Risk**: Medication risk flag
6. **Indication ID**: ICPC/ICD-10 code

**Rationale**: ZIndex is the authoritative Dutch pharmaceutical database with regulatory tracking.

### Summary of Key Discrepancies

#### 1. Architectural Philosophy

- **GenForm**: Flat, filter-based, one rule per clinical scenario
- **ZForm**: Hierarchical, collection-based, groups related scenarios
- **ZIndex**: Flat, database-oriented, simple types

#### 2. Dose Representation

- **GenForm**: Parameterized with AdjustUnit (kg vs m²), three dimensions (Qty/PerTime/Rate)
- **ZForm**: Explicit six fields (Norm/Abs × None/Weight/BSA), four dosage types (Start/Single/Rate/Total)
- **ZIndex**: Explicit six fields matching G-Standaard schema

#### 3. Clinical Focus

- **GenForm**: Pediatric/neonatal intensive care (PMAge, Location, Department, infusion timing)
- **ZForm**: General Dutch prescribing (ATC classification, comprehensive grouping)
- **ZIndex**: Dutch pharmaceutical database (regulatory fields, care group, high-risk flags)

#### 4. Organization Principle

- **GenForm**: Component-centric with three-level limits (Shape → Component → Substance)
- **ZForm**: Substance-centric with two-level limits (Shape → Substance)
- **ZIndex**: Product-centric with flat dose fields

#### 5. Interoperability

- **ZIndex → ZForm**: Implemented in `GStand.fs` with comprehensive transformation
- **ZForm → GenForm**: Not implemented; would require hierarchy flattening and dose consolidation
- **GenForm → ZForm**: Not implemented; would require grouping and dose expansion
- **Direct ZIndex → GenForm**: Not implemented; would need custom mapping

### Use Case Alignment

#### GenForm Best For:

- Clinical prescription generation
- Pediatric and neonatal care
- Complex multi-component products (TPN, chemotherapy)
- Venous access-specific rules
- Department-specific protocols
- Infusion rate calculation

#### ZForm Best For:

- G-Standaard data processing
- Drug classification and grouping
- Multiple route/shape scenarios
- Preserving original rule provenance
- Dutch healthcare integration
- Comprehensive pharmaceutical database queries

#### ZIndex Best For:

- Authoritative pharmaceutical data source
- Regulatory compliance
- Dutch prescribing guidelines
- High-risk medication tracking
- ICPC/ICD-10 indication coding
- Care group stratification

### Recommendations for Integration

1. **Keep Separate**: GenForm and ZForm serve different purposes and should remain separate libraries
2. **Mapping Layer**: Create explicit mapping functions if cross-library queries needed
3. **Shared Types**: Consider extracting common types (MinMax, ValueUnit, Gender) to shared library
4. **Data Flow**: ZIndex → ZForm (implemented) → potential future GenForm mapping layer
5. **Documentation**: Maintain clear documentation of which implementation to use for which scenario
6. **Testing**: If mapping implemented, extensive property-based testing for round-trip conversions where possible

