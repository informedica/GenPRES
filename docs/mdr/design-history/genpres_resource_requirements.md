# GenPRES Resource Requirements Documentation

## Overview

GenPRES relies on external Google Spreadsheet resources to provide medication data, mapping information, and configuration settings. These spreadsheets serve as the primary data source for the application and are accessed via a configurable URL ID.

This document describes the spreadsheet resources that implement the Operational Knowledge Rules (OKRs) defined in the [GenFORM specification](../../domain/genform-free-text-to-operational-rules.md).

## Core Definitions

The following definitions align with the [GenFORM domain model](../../domain/genform-free-text-to-operational-rules.md):

| Term | Definition |
| ---- | ---------- |
| *Operational Knowledge Rule (OKR)* | A fully structured, machine-interpretable and constraint-based representation of medication knowledge used for prescribing, preparation, and administration. |
| *Dose Rule* | An OKR that defines qualitative and quantitative constraints for dosing a specific generic in a defined clinical context. |
| *Dose Limit* | A set of numeric constraints defining the minimum, maximum, or normative allowable dose. |
| *Dose Type* | The temporal category of dosing: once, onceTimed, discontinuous, timed, or continuous. |
| *Selection Constraint* | A rule element used to determine which calculation constraints apply (e.g., patient demographics, route, indication). |
| *Calculation Constraint* | A quantitative rule element used to compute dose, rate, volume, or timing. |
| *Adjustment Unit* | A patient normalization unit used to scale doses (e.g., kg for weight, m² for BSA). |
| *Reconstitution* | The process of converting a medication into an administrable form by adding a diluent. |
| *Expansion Volume* | The increase in total volume resulting from reconstitution. |
| *Dilution* | The process of further adjusting concentration and volume for safe administration. |
| *Form* | The pharmaceutical form of a medication (e.g., tablet, injection, solution). Also referred to as "Shape" in some legacy contexts. |
| *Generic Product (GPK)* | An abstract pharmaceutical product definition independent of branding. |
| *Patient Category* | A classification of patients by demographic ranges (age, weight, BSA, gestational age) used to determine which rules apply. |

## Configuration

The spreadsheet URL is configured through the `GENPRES_URL_ID` environment variable:

```bash
export GENPRES_URL_ID=1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA
```

## Required Spreadsheet Sheets

### 1. Routes Sheet

**Purpose**: Maps long Z-index route names to standardized short names for medication administration routes.

**Required Columns**:

- `ZIndex` - Full Z-index route name (e.g., "Intravenous administration")
- `ShortDutch` - Short Dutch route name (e.g., "iv")

**Usage**: Used by `routeMapping` function to standardize route terminology across the application.

**Example Data**:

| ZIndex | ShortDutch |
|--------|------------|
| Intravenous administration | iv |
| Oral administration | po |
| Subcutaneous injection | sc |

---

### 2. Units Sheet

**Purpose**: Maps long Z-index unit names to standardized short names and provides unit grouping information.

**Required Columns**:

- `ZIndexUnitLong` - Full Z-index unit name
- `Unit` - Short standardized unit name
- `MetaVisionUnit` - MetaVision system unit equivalent
- `Group` - Unit category/group classification

**Usage**: Used by `unitMapping` function to standardize unit terminology and enable unit conversions.

**Example Data**:

| ZIndexUnitLong | Unit | MetaVisionUnit | Group |
|----------------|------|----------------|-------|
| milligram | mg | mg | Mass |
| milliliter | ml | ml | Volume |
| international unit | iu | IU | International |

---

### 3. FormRoute Sheet (Form-Route Mapping)

**Purpose**: Defines medication form constraints, dosing limits, and administration requirements based on route and pharmaceutical form combinations.

**Required Columns**:

- `Route` - Administration route
- `Form` - Pharmaceutical form (Form in GenFORM terminology)
- `Unit` - Base unit for the medication form
- `DoseUnit` - Base Unit for dose calculations
- `MinDoseQty` - Minimum dose quantity (optional)
- `MaxDoseQty` - Maximum dose quantity (optional)
- `MinDoseQtyKg` - Minimum dose per kilogram (optional)
- `MaxDoseQtyKg` - Maximum dose per kilogram (optional)
- `Divisible` - Divisibility factor (optional)
- `Timed` - Boolean indicating if administration runs over a time period (accepts "true"/"false")
- `Reconstitute` - Boolean indicating if reconstitution is required (accepts "true"/"false")
- `IsSolution` - Boolean indicating if the form is a solution (accepts "true"/"false")

**Usage**: Used by `mappingFormRoute` to provide clinical decision support for medication administration.

**Example Data**:

| Route | Form      | Unit  | DoseUnit | MinDoseQty | MaxDoseQty | MinDoseQtyKg | MaxDoseQtyKg | Divisible | Timed | Reconstitute | IsSolution |
|-------|-----------|-------|----------|------------|------------|--------------|--------------|-----------|-------|--------------|------------|
| iv    | injection | ml    | mg       | 1          | 100        | 0.1          | 10           | 0.5       | true  | false        | true       |
| po    | tablet    | piece | mg       | 0.5        | 50         | 0.01         | 5            | 0.25      | false | false        | false      |

---

### 4. ValidForms Sheet (ValidForms)

**Purpose**: Defines the complete list of valid pharmaceutical forms supported by the system.

**Required Columns**:

- `Form` - Valid pharmaceutical form name (Form in GenFORM terminology)

**Usage**: Used by `validForms` function to validate pharmaceutical form inputs.

**Example Data**:

| Form        |
|-------------|
| tablet      |
| capsule     |
| injection   |
| solution    |
| cream       |
| suppository |

---

### 5. Reconstitution Sheet

**Purpose**: Provides reconstitution rules for converting medications into administrable form by adding a diluent. This sheet implements the Reconstitution Rule model from GenFORM.

**Required Columns**:

#### Basic Identification (Selection Constraints)

- `GPK` - Generic Product Code identifier (Generic.GPK in GenFORM)
- `Route` - Administration route for reconstitution
- `Loc` - Organizational location / hospital / institute (Setting.Location in GenFORM, optional)
- `Dep` - Department/unit where reconstitution applies (Setting.Department in GenFORM, optional)

#### Solution Parameters (Calculation Constraints)

These fields correspond to the Solution object in GenFORM:

- `DiluentVol` - Diluent volume required in mL (numeric)
- `ExpansionVol` - Expansion volume (increase in total volume resulting from reconstitution) in mL (numeric, optional)
- `Diluents` - Acceptable diluents (semicolon-separated list)

**Usage**: Used by `Reconstitution.get` to provide reconstitution instructions for injectable products.

**Example Data**:

| GPK   | Route | Dep | DiluentVol | ExpansionVol | Diluents             |
|-------|-------|-----|------------|--------------|----------------------|
| 12345 | iv    | ICU | 10         | 0.5          | NaCl 0.9%;Glucose 5% |
| 67890 | iv    | NEO | 5          | 0.2          | WFI;NaCl 0.9%        |

---

### 6. EntFeeding Sheet

**Purpose**: Defines enteral feeding products with nutritional composition.

**Required Columns**:

- `Name` - Product name
- `Eenheid` - Base unit for the product
- `volume mL` - Volume per unit (numeric)
- `energie kCal` - Energy content in kcal (numeric, optional)
- `eiwit g` - Protein content in grams (numeric, optional)
- `KH g` - Carbohydrate (koolhydraat) content in grams (numeric, optional)
- `vet g` - Fat content in grams (numeric, optional)
- `Na mmol` - Sodium content in mmol (numeric, optional)
- `K mmol` - Potassium content in mmol (numeric, optional)
- `Ca mmol` - Calcium content in mmol (numeric, optional)
- `P mmol` - Phosphate content in mmol (numeric, optional)
- `Mg mmol` - Magnesium content in mmol (numeric, optional)
- `Fe mmol` - Iron content in mmol (numeric, optional)
- `VitD IE` - Vitamin D content in International Units (numeric, optional)
- `Cl mmol` - Chloride content in mmol (numeric, optional)

**Usage**: Used by `Enteral.get` to provide enteral nutrition products with nutritional information.

**Example Data**:

| Name | Eenheid | volume mL | energie kCal | eiwit g | KH g | vet g | Na mmol |
|------|---------|-----------|--------------|---------|------|-------|---------|
| Nutrison Standard | ml | 1 | 1.0 | 0.04 | 0.123 | 0.039 | 0.65 |

---

### 7. ParentMeds Sheet

**Purpose**: Defines parenteral nutrition and medication products with composition.

**Required Columns**:

- `GPK` - Generic Product Code identifier
- `Name` - Product name
- `volume mL` - Volume per unit (numeric)
- `glucose g` - Glucose content in grams (numeric, optional)
- `energie kCal` - Energy content in kcal (numeric, optional)
- `eiwit g` - Protein content in grams (numeric, optional)
- `koolhydraat g` - Total carbohydrate content in grams (numeric, optional)
- `vet g` - Fat content in grams (numeric, optional)
- `natrium mmol` - Sodium content in mmol (numeric, optional)
- `kalium mmol` - Potassium content in mmol (numeric, optional)
- `calcium mmol` - Calcium content in mmol (numeric, optional)
- `fosfaat mmol` - Phosphate content in mmol (numeric, optional)
- `magnesium mmol` - Magnesium content in mmol (numeric, optional)
- `ijzer mmol` - Iron content in mmol (numeric, optional)
- `VitD IE` - Vitamin D content in International Units (numeric, optional)
- `chloor mmol` - Chloride content in mmol (numeric, optional)
- `Oplosmiddel` - Indicates if product is a solvent (accepts "TRUE"/"FALSE")
- `Verdunner` - Indicates if product is a diluent (accepts "TRUE"/"FALSE")

**Usage**: Used by `Parenteral.get` to provide parenteral nutrition and IV medication products.

**Example Data**:

| GPK   | Name       | volume mL | glucose g | natrium mmol | Oplosmiddel | Verdunner |
|-------|------------|-----------|-----------|--------------|-------------|------------|
| 99999 | Glucose 5% | 1         | 0.05      | 0            | FALSE       | TRUE      |

---

### 8. Formulary Sheet

**Purpose**: Defines the hospital formulary with product configurations and department availability.

**Required Columns**:

- `GPKODE` - Generic Product Code (numeric)
- `UMCU` - Hospital availability indicator (column name in sheet: "UMCU", field name in code: Apotheek)
- `ICC` - Intensive Care availability indicator
- `NEO` - Neonatal unit availability indicator
- `ICK` - Pediatric ICU availability indicator
- `HCK` - High Care availability indicator
- `Generic` - Generic product name
- `UseGenName` - Use generic name flag (accepts "x" for true)
- `UseForm` - Use Form in naming flag (accepts "x" for true)
- `UseBrand` - Use brand name flag (accepts "x" for true)
- `TallMan` - Tall Man lettering for safety
- `Mmol` - Molar concentration (numeric, optional)
- `Divisible` - Divisibility factor (numeric, optional)

**Usage**: Used by the main `get` function to create the primary product formulary with department-specific configurations.

**Example Data**:

| GPKODE | UMCU | ICC | NEO | Generic     | UseGenName | UseForm | UseBrand | TallMan | Mmol | Divisible |
|--------|------|-----|-----|-------------|------------|---------|----------|---------|------|------------|
| 12345  | x    | x   |     | paracetamol | x          |         |          |         |      | 2         |
| 67890  | x    |     | x   | amoxicillin | x          | x       |          |         |      |           |

---

### 9. DoseRules Sheet

**Purpose**: Defines clinical dosing rules and limits for medications across different patient populations, routes, and clinical scenarios.

**Required Columns**:

#### Dose Rule: Basic Identification (Selection Constraints)

- `Source` - Data source identifier (e.g., "NKF", "FK")
- `Indication` - Clinical indication for the medication
- `Generic` - Generic medication name
- `Form` - Pharmaceutical form (Form in GenFORM terminology)
- `Brand` - Brand name (optional)
- `Route` - Administration route
- `GPKs` - Generic Product Codes (semicolon-separated list)
- `ScheduleText` - Dosing schedule description (Source.Text in GenFORM)
- `Component` - Component name for combination products
- `Substance` - Active substance name

#### Dose Rule: Setting and Patient Category (Selection Constraints)

These fields define the clinical setting and patient category (demographic ranges) that determine rule applicability:

- `Dep` - Department/ward (Setting.Department in GenFORM terminology)
- `Gender` - Patient gender
- `MinAge` - Minimum age in days (numeric, optional)
- `MaxAge` - Maximum age in days (numeric, optional)
- `MinWeight` - Minimum weight in grams (numeric, optional)
- `MaxWeight` - Maximum weight in grams (numeric, optional)
- `MinBSA` - Minimum body surface area in m² (numeric, optional)
- `MaxBSA` - Maximum body surface area in m² (numeric, optional)
- `MinGestAge` - Minimum gestational age in days (numeric, optional)
- `MaxGestAge` - Maximum gestational age in days (numeric, optional)
- `MinPMAge` - Minimum post-menstrual age in days (numeric, optional)
- `MaxPMAge` - Maximum post-menstrual age in days (numeric, optional)

#### Dose Rule: Dose Configuration (Selection and Calculation Constraints)

- `DoseType` - Temporal category of dosing. Valid values: "once", "onceTimed", "discontinuous", "timed", or "continuous"
- `DoseText` - Dose type description text (DoseType.DoseText in GenFORM, can be empty)
- `Freqs` - Schedule frequencies (semicolon-separated numeric values)
- `DoseUnit` - Base dose unit for DoseLimit
- `AdjustUnit` - Patient adjustment unit (e.g., "kg" for weight, "m2" for BSA)
- `FreqUnit` - Frequency unit for Schedule (e.g., "day", "hour")
- `RateUnit` - Rate unit for DoseLimit (e.g., "hour", "min")

#### Dose Rule: Schedule Parameters (Calculation Constraints)

These fields correspond to the Schedule object in GenFORM:

- `MinTime` - Minimum time for infusion of a dose (numeric, optional)
- `MaxTime` - Maximum time for infusion of a dose (numeric, optional)
- `TimeUnit` - Time unit for infusion measurement
- `MinInt` - Minimum interval between two doses (numeric, optional)
- `MaxInt` - Maximum interval between two doses (numeric, optional)
- `IntUnit` - Interval unit
- `MinDur` - Minimum duration of the dose rule (numeric, optional)
- `MaxDur` - Maximum duration of the dose rule (numeric, optional)
- `DurUnit` - Duration time unit

#### Dose Rule: Dose Limits (Calculation Constraints)

These fields correspond to the DoseLimit object in GenFORM:

- `MinQty` - Minimum dose quantity (numeric, optional)
- `MaxQty` - Maximum dose quantity (numeric, optional)
- `NormQtyAdj` - Normal patient-adjusted dose quantity (numeric, optional)
- `MinQtyAdj` - Minimum patient-adjusted dose quantity (numeric, optional)
- `MaxQtyAdj` - Maximum patient-adjusted dose quantity (numeric, optional)
- `MinPerTime` - Minimum dose quantity per time (numeric, optional)
- `MaxPerTime` - Maximum dose quantity per time (numeric, optional)
- `NormPerTimeAdj` - Normal adjusted dose per time (numeric, optional)
- `MinPerTimeAdj` - Minimum adjusted dose per time (numeric, optional)
- `MaxPerTimeAdj` - Maximum adjusted dose per time (numeric, optional)
- `MinRate` - Minimum rate (numeric, optional)
- `MaxRate` - Maximum rate (numeric, optional)
- `MinRateAdj` - Minimum adjusted rate (numeric, optional)
- `MaxRateAdj` - Maximum adjusted rate (numeric, optional)

**Usage**: Used by `DoseRule.get` to provide comprehensive clinical dosing guidance with patient-specific limits and safety constraints.

**Example Data**:

| Source | Generic     | Form       | Route | Indication | DoseType      | MinWeight | MaxWeight | DoseUnit | MinQty | MaxQty | AdjustUnit | MinQtyAdj | MaxQtyAdj |
|--------|-------------|------------|-------|------------|---------------|-----------|-----------|----------|--------|--------|------------|-----------|------------|
| NKF    | paracetamol | tablet     | po    | fever      | discontinous  | 10        | 50        | mg       | 500    | 1000   | kg         | 10        | 15        |
| NKF    | amoxicillin | suspension | po    | infection  | discontinuous | 5         | 80        | mg       |        |        | kg         | 25        | 50        |

---

### 10. DilutionRules Sheet (SolutionRules)

**Purpose**: Defines dilution rules for IV solution preparation, including diluent requirements and concentration limits for injectable medications. This sheet implements the Dilution Rule model from GenFORM.

> **Note**: The sheet may be named "SolutionRules" for backward compatibility. In GenFORM terminology, this represents "Dilution Rules".

**Required Columns**:

#### Dilution Rule: Basic Identification (Selection Constraints)

- `Generic` - Generic medication name
- `Form` - Pharmaceutical form (Form in GenFORM terminology, optional)
- `Route` - Administration route
- `Indication` - Clinical indication (optional, must match corresponding Dose Rule)

#### Dilution Rule: Setting, Administration Access Device, and Patient Category (Selection Constraints)

- `Dep` - Department/ward (Setting.Department in GenFORM terminology, optional)
- `CVL` - Administration access device: Central Venous Line (accepts "x" for true)
- `PVL` - Administration access device: Peripheral Venous Line (accepts "x" for true)
- `MinAge` - Minimum age in days (numeric, optional)
- `MaxAge` - Maximum age in days (numeric, optional)
- `MinWeight` - Minimum weight in grams (numeric, optional)
- `MaxWeight` - Maximum weight in grams (numeric, optional)
- `MinGestAge` - Minimum gestational age in days (numeric, optional)
- `MaxGestAge` - Maximum gestational age in days (numeric, optional)

#### Dilution Rule: Dose Configuration (Selection Constraints)

- `MinDose` - Minimum dose (DilutionRule.MinDose in GenFORM, numeric, optional)
- `MaxDose` - Maximum dose (DilutionRule.MaxDose in GenFORM, numeric, optional)
- `DoseType` - Temporal category of dosing: "once", "onceTimed", "discontinuous", "timed", or "continuous"

#### Dilution Rule: Dilution Parameters (Calculation Constraints)

These fields correspond to the DilutionRule object in GenFORM:

- `Solutions` - Acceptable diluents (pipe-separated list: "solution1|solution2")
- `Volumes` - Standard volume quantities (semicolon-separated numeric values in mL)
- `MinVol` - Minimum volume in mL (numeric, optional)
- `MaxVol` - Maximum volume in mL (numeric, optional)
- `MinVolAdj` - Minimum volume per kg in mL/kg (numeric, optional)
- `MaxVolAdj` - Maximum volume per kg in mL/kg (numeric, optional)
- `MinPerc` - Minimum percentage of solution for DoseQuantity (Administration Fraction min, numeric, optional)
- `MaxPerc` - Maximum percentage of solution for DoseQuantity (Administration Fraction max, numeric, optional)

#### Dilution Rule: Dilution Limits (Calculation Constraints)

These fields correspond to the DilutionLimit object in GenFORM:

- `Substance` - Active substance name (for concentration limits)
- `Unit` - Substance unit (SubstUnit in GenFORM)
- `Quantities` - Standard substance quantities (semicolon-separated numeric values)
- `MinQty` - Minimum substance quantity (numeric, optional)
- `MaxQty` - Maximum substance quantity (numeric, optional)
- `MinDrip` - Minimum infusion rate in mL/hour (numeric, optional)
- `MaxDrip` - Maximum infusion rate in mL/hour (numeric, optional)
- `MinConc` - Minimum substance concentration in SubstUnit/mL (numeric, optional)
- `MaxConc` - Maximum substance concentration in SubstUnit/mL (numeric, optional)

**Usage**: Used by `SolutionRule.get` (implements Dilution Rule) to provide IV solution preparation guidance with concentration limits and diluent requirements.

**Example Data**:

| Generic  | Form      | Route | Indication | Dep | CVL | PVL | MinWeight | MaxWeight | DoseType    | Solutions             | Volumes    | MinVol | MaxVol | Substance | Unit | MinConc | MaxConc |
|----------|-----------|-------|------------|-----|-----|-----|-----------|-----------|-------------|-----------------------|------------|--------|--------|-----------|------|---------|----------|
| dopamine | injection | iv    | shock      | ICU | x   |     | 1         | 80        | maintenance | NaCl 0.9%\|Glucose 5% | 50;100;250 | 50     | 250    | dopamine  | mg   | 0.4     | 3.2     |
| morphine | injection | iv    | pain       |     | x   | x   | 10        |           | maintenance | NaCl 0.9%             | 50;100     | 50     | 100    | morphine  | mg   | 0.1     | 1       |

---

### 11. RenalRules Sheet

**Purpose**: Defines renal dose adjustments for medications based on kidney function, dialysis status, and patient characteristics. This sheet implements the Renal Rule model from GenFORM.

**Required Columns**:

#### Renal Rule: Basic Identification (Selection Constraints)

- `Generic` - Generic medication name
- `Route` - Administration route
- `Indication` - Clinical indication (optional, must match corresponding Dose Rule)
- `Source` - Data source identifier (e.g., "NKF", "KI/DOQI")

#### Renal Rule: Patient Category (Selection Constraints)

- `MinAge` - Minimum age in days (numeric, optional)
- `MaxAge` - Maximum age in days (numeric, optional)

#### Renal Rule: Renal Function Parameters (Selection Constraints)

These fields correspond to the Renal object in GenFORM:

- `IntDial` - Intermittent hemodialysis (accepts "x" for applies)
- `ContDial` - Continuous hemodialysis (accepts "x" for applies)
- `PerDial` - Peritoneal dialysis (accepts "x" for applies)
- `MinGFR` - Minimum standardized GFR in mL/min/1.73m² (numeric, optional)
- `MaxGFR` - Maximum standardized GFR in mL/min/1.73m² (numeric, optional)

#### Renal Rule: Dose Configuration (Selection and Calculation Constraints)

- `DoseType` - Temporal category of dosing: "once", "onceTimed", "discontinuous", "timed", or "continuous"
- `DoseText` - Dose type description (DoseType.DoseText in GenFORM)
- `Freqs` - Schedule frequencies (semicolon-separated numeric values)
- `DoseRed` - Dose adjustment type: "rel" for Relative Adjustment, "abs" for Absolute Adjustment
- `DoseUnit` - Base dose unit
- `AdjustUnit` - Patient adjustment unit (e.g., "kg", "m2")
- `FreqUnit` - Frequency unit (e.g., "day", "hour")
- `RateUnit` - Rate unit (e.g., "hour", "min")

#### Renal Rule: Schedule Parameters (Calculation Constraints)

These fields correspond to the Schedule object in GenFORM:

- `MinInt` - Minimum interval duration (numeric, optional)
- `MaxInt` - Maximum interval duration (numeric, optional)
- `IntUnit` - Interval time unit

#### Renal Rule: Substance and Adjustment (Selection and Calculation Constraints)

- `Substance` - Active substance name (must match corresponding Dose Rule)

#### Dose Limits (Calculation Constraints - Renal Adjusted)

These fields implement the Adjustment object in GenFORM. Values are either relative (multiplier) or absolute based on `DoseRed`:

- `MinQty` - Minimum dose quantity (numeric, optional)
- `MaxQty` - Maximum dose quantity (numeric, optional)
- `NormQtyAdj` - Normal patient-adjusted dose quantity (space-dash-space separated values: "val1 - val2")
- `MinQtyAdj` - Minimum patient-adjusted dose quantity (numeric, optional)
- `MaxQtyAdj` - Maximum patient-adjusted dose quantity (numeric, optional)
- `MinPerTime` - Minimum dose per time (numeric, optional)
- `MaxPerTime` - Maximum dose per time (numeric, optional)
- `NormPerTimeAdj` - Normal patient-adjusted dose per time (space-dash-space separated values)
- `MinPerTimeAdj` - Minimum patient-adjusted dose per time (numeric, optional)
- `MaxPerTimeAdj` - Maximum patient-adjusted dose per time (numeric, optional)
- `MinRate` - Minimum dose rate (numeric, optional)
- `MaxRate` - Maximum dose rate (numeric, optional)
- `MinRateAdj` - Minimum patient-adjusted dose rate (numeric, optional)
- `MaxRateAdj` - Maximum patient-adjusted dose rate (numeric, optional)

**Usage**: Used by `RenalRule.get` to provide renal dose adjustments based on kidney function and dialysis status. Rules are applied to patients ≥28 days of age with impaired renal function.

**Example Data**:

| Generic    | Route | Source  | MinGFR | MaxGFR | ContDial | IntDial | PerDial | DoseType      | DoseRed | Substance  | DoseUnit | AdjustUnit | MinQtyAdj | MaxQtyAdj | NormQtyAdj |
|------------|-------|---------|--------|--------|----------|---------|---------|---------------|---------|------------|----------|------------|-----------|-----------|-------------|
| gentamicin | iv    | NKF     | 30     | 59     |          |         |         | discontinuous | abs     | gentamicin | mg       | kg         | 3         | 5         | 4 - 5      |
| digoxin    | po    | KI/DOQI |        | 50     |          |         |         | discontinuous | rel     | digoxin    | mcg      |            |           |           | 0.5        |
| vancomycin | iv    | NKF     |        |        | x        |         |         | discontinuous | abs     | vancomycin | mg       | kg         | 10        | 15        | 15         |

---

### 12. Emergency Treatment Sheets

**Purpose**: Defines emergency treatment protocols and bolus medication data for resuscitation scenarios.

> **Implementation Note**: Emergency treatment data loading is currently handled in the client application (`EmergencyList.fs`), not in the GenFORM.Lib library. The sheets are documented here for completeness and future migration to the library layer.

#### 12A. Bolus Medication Data Sheet

**Required Columns**:

- `hospital` - Hospital identifier
- `indication` - Clinical indication (e.g., "reanimatie", "shock")
- `medication` - Medication name
- `minWeight` - Minimum weight for application (numeric)
- `maxWeight` - Maximum weight for application (numeric)
- `dose` - Normal dose per kg (numeric)
- `min` - Minimum absolute dose (numeric)
- `max` - Maximum absulute dose (numeric)
- `conc` - Concentration (numeric)
- `unit` - Unit of measurement
- `remark` - Additional remarks or instructions

**Usage**: Used by `EmergencyTreatment.parse` to create bolus medication protocols for emergency scenarios.

**Example Data**:

| hospital | indication  | medication | minWeight | maxWeight | dose | min | max | conc | unit   | remark |
|----------|-------------|------------|-----------|-----------|------|-----|-----|------|--------|--------|
| UMCU     | intubation  | fentanyl   | 0         | 0         | 1    | 0   | 50  | 50   | microg |        |
| UMCU     | bradycardia | atropin    | 0         | 0         | 0.02 | 0.1 | 1   | 0.5  | mg     |        |

#### 12B. Continuous Medication Data Sheet

**Required Columns**:

- `hospital` - Hospital identifier
- `catagory` - Category of medication
- `indication` - Clinical indication
- `dosetype` - Type of dose (e.g., "start", "maintenance")
- `medication` - Medication name
- `generic` - Generic name
- `unit` - Unit of measurement
- `doseunit` - Dose unit (e.g., "mg/kg/uur")
- `minweight` - Minimum weight for application (numeric)
- `maxweight` - Maximum weight for application (numeric)
- `quantity` - Quantity in solution (numeric)
- `total` - Total volume (numeric)
- `mindose` - Minimum dose rate (numeric)
- `maxdose` - Maximum dose rate (numeric)
- `absmax` - Absolute maximum dose (numeric)
- `minconc` - Minimum concentration (numeric)
- `maxconc` - Maximum concentration (numeric)
- `solution` - Solution type

**Usage**: Used by `ContinuousMedication.parse` to create continuous infusion protocols.

**Example Data**:

| hospital | catagory        | indication | dosetype | medication    | generic       | unit | doseunit      | minweight | maxweight | quantity | total | mindose | maxdose | absmax | solution  |
|----------|-----------------|------------|----------|---------------|---------------|------|---------------|-----------|-----------|----------|-------|---------|---------|--------|------------|
| UMCU     | cardiovasculair | shock      | start    | noradrenaline | noradrenaline | mg   | microg/kg/min | 3         | 80        | 4        | 50    | 0.05    | 2       | 10     | NaCl 0.9% |

#### 12C. Products Data Sheet

**Required Columns**:

- `indication` - Clinical indication
- `medication` - Medication name
- `conc` - Concentration (numeric)
- `unit` - Unit of measurement

**Usage**: Used by `Products.parse` to define available medication products and their concentrations.

**Example Data**:

| indication | medication | conc | unit |
|------------|------------|------|------|
| reanimatie | adrenaline | 0.1 | mg |
| shock | noradrenaline | 1 | mg |

#### 12D. Normal Values Data Sheet

**Required Columns**:

- `sex` - Gender ("M" or "F")
- `age` - Age in years (numeric)
- `p3` - 3rd percentile value (numeric)
- `mean` - Mean value (numeric)
- `p97` - 97th percentile value (numeric)

**Usage**: Used by `NormalValues.parse` to provide reference ranges for weight and height estimation.

**Example Data**:

| sex | age | p3  | mean | p97 |
|-----|-----|-----|------|-----|
| M   | 0.5 | 2.5 | 3.5  | 4.5 |
| F   | 0.5 | 2.3 | 3.3  | 4.3 |

---

## Data Access Pattern

All sheets are accessed through the following pattern:

1. **URL Construction**: Uses `Web.getDataUrlIdGenPres()` to get the configured URL ID
2. **Data Retrieval**: Uses `Web.getDataFromSheet dataUrlId "SheetName"` to fetch sheet data
3. **Column Mapping**: Uses `Csv.getStringColumn` and `Csv.getFloatColumn` for data extraction
4. **Header Processing**: First row is treated as headers, subsequent rows as data
5. **Data Parsing**: Parse functions use `getString` and `getFloat` helper functions to extract column data
6. **Caching**: Some functions use `Memoization.memoize` for performance optimization

## Data Requirements

### Data Quality Standards

- **Consistency**: All text fields should use consistent casing and spelling
- **Completeness**: Required fields must not be empty
- **Validation**: Unit names must be valid according to the GenUnits library
- **Standardization**: Route and pharmaceutical form names should follow established medical terminology
- **Boolean Fields**: Must use "true"/"false" or "x" for flags (case-insensitive)
- **Numeric Fields**: Must be valid numbers for concentration and quantity fields
- **Semicolon Separation**: Multi-value fields use semicolon separation
- **Pipe Separation**: Solution names use pipe separation ("|")
- **Space-Dash-Space Separation**: Range values use " - " separation for renal rules
- **Clinical Validation**: Dose limits must be clinically appropriate and evidence-based
- **Concentration Safety**: Solution concentration limits must ensure patient safety
- **Renal Safety**: Renal adjustments must be based on established nephrology guidelines
- **Emergency Protocols**: Emergency treatment data must follow established resuscitation guidelines

### Performance Considerations

- **Caching**: Functions that access these sheets implement memoization for performance
- **Data Size**: Sheets should be optimized for reasonable loading times
- **Update Frequency**: Changes to sheets require application restart or cache invalidation
- **Deduplication**: DoseRules sheet uses distinct filtering to prevent duplicate entries
- **Complex Processing**: DilutionRules (SolutionRules) and RenalRules undergo complex grouping and filtering operations
- **Age Restrictions**: RenalRules only apply to patients ≥28 days of age
- **Emergency Data**: Emergency treatment sheets require rapid access for critical care scenarios

### Security and Access

- **Read Access**: Application requires read access to the Google Spreadsheet
- **API Limits**: Consider Google Sheets API rate limits for frequent access
- **Backup**: Maintain backup copies of critical data sheets
- **Version Control**: Track changes to data sheets for audit purposes
- **Clinical Governance**: Dose rule changes require clinical review and approval
- **IV Safety**: Dilution rules require specialized clinical validation
- **Nephrology Review**: Renal adjustment rules require nephrology specialist approval
- **Emergency Review**: Emergency treatment protocols require intensive care specialist approval

## Error Handling

The application should gracefully handle:

- **Missing Sheets**: Return appropriate defaults or error messages
- **Missing Columns**: Log warnings and continue with available data
- **Invalid Data**: Validate and sanitize input data
- **Network Issues**: Implement retry logic for sheet access failures
- **Clinical Data Issues**: Validate dose ranges and clinical appropriateness
- **Solution Compatibility**: Validate diluent compatibility with medications
- **Renal Function Validation**: Ensure GFR values and dialysis flags are clinically appropriate
- **Emergency Data Validation**: Ensure emergency protocols are clinically safe and current

## Development vs Production

- **Demo Mode**: Uses `GENPRES_PROD=0` with sample data
- **Production Mode**: Uses full proprietary medication database
- **Cache Files**: Production may use local cache files for performance
- **Data Updates**: Production requires careful data validation before updates
- **Clinical Review**: All dose rule changes require clinical pharmacist approval
- **IV Preparation**: Dilution rules require specialized pharmacy validation
- **Renal Guidelines**: Renal rules must align with current nephrology practice guidelines
- **Emergency Protocols**: Emergency treatment data must align with current resuscitation guidelines

## Medical Device Regulation (MDR) Compliance

This documentation is part of the Design History File (DHF) for GenPRES, supporting MDR compliance requirements:

- **Traceability**: Documents data sources and their validation requirements
- **Risk Management**: Identifies data quality risks and mitigation strategies
- **Change Control**: Establishes procedures for data updates and validation
- **Verification**: Provides basis for data verification and validation testing
- **Clinical Evidence**: Dose rules are based on established clinical guidelines and evidence
- **IV Safety**: Dilution rules follow established pharmaceutical compounding standards
- **Renal Safety**: Renal adjustment rules follow established nephrology guidelines and evidence-based practice
- **Emergency Care**: Emergency treatment protocols follow established resuscitation and critical care guidelines

## Related Documents

This document describes the spreadsheet resources implementing the Operational Knowledge Rules (OKRs) defined in the GenFORM specification.

| Document                                                                         | Description                                              | Relationship                                                          |
|----------------------------------------------------------------------------------|----------------------------------------------------------|-----------------------------------------------------------------------|
| [GenFORM Specification](../../domain/genform-free-text-to-operational-rules.md) | Free text to Operational Knowledge Rules transformation  | **Reference specification** - defines rule models and terminology     |
| [Core Domain Model](../../domain/core-domain.md)                                | Central domain definitions and transformation pipeline   | GenFORM is Layer 1 in the pipeline                                    |
| [GenORDER](../../domain/genorder-operational-rules-to-orders.md)                | Transforms OKRs to Order Scenarios                       | Consumes rules defined in these spreadsheets                          |
| [GenSOLVER](../../domain/gensolver-from-orders-to-quantitative-solutions.md)    | Constraint solving engine                                | Provides algorithmic foundation for rule application                  |

## Terminology Mapping

For backward compatibility, some spreadsheet column names differ from GenFORM terminology:

| Spreadsheet Term | GenFORM Term | Notes |
|------------------|--------------|-------|
| Form | Form | Pharmaceutical form |
| Dep | Setting.Department | Department/ward |
| SolutionRules | Dilution Rules | IV solution preparation rules |
| CVL/PVL | Administration Access Device | Central/Peripheral Venous Line |
| DoseRed | Adjustment | Relative or Absolute dose adjustment |
| eGFR | Standardized GFR | mL/min/1.73m² |
| maintenance/start/max | DoseType values | Use: once, onceTimed, discontinuous, timed, continuous |

## Design History: Shape → Form Migration

### Date: December 21, 2025

### Change Description

Completed systematic migration from "Shape" to "Form" terminology throughout the GenPRES codebase to align with medical terminology and GenFORM domain model specification.

### Rationale

1. **Medical Terminology**: "Pharmaceutical form" is the standard medical/pharmaceutical term
2. **Domain Model Alignment**: GenFORM specification consistently uses "Form" and "Pharmaceutical Form"
3. **Industry Standards**: Pharmaceutical industry documentation uses "form" terminology
4. **Documentation Consistency**: All domain documentation uses "Form" terminology

### Implementation Status: COMPLETE

#### Type Definitions - All Updated

**GenFORM.Lib** (`src/Informedica.GenFORM.Lib/Types.fs`):
- ✓ `FormRoute.Form` (line 39) - "The pharmaceutical form"
- ✓ `Product.UseForm` (line 143) - "Use pharmaceutical form"
- ✓ `Product.Form` (line 155) - "The pharmaceutical form of the Product"
- ✓ `FormularyProduct.UseForm` (line 187)
- ✓ `LimitTarget.FormLimitTarget` (line 204) - discriminated union case
- ✓ `DoseRuleData.Form` (line 346)
- ✓ `DoseRule.Form` (line 417) - "The pharmaceutical pharmaceutical form of the DoseRule"
- ✓ `SolutionRule.Form` (line 469) - "The pharmaceutical form of the SolutionRule"
- ✓ `ProductFilter.Form` (line 550)
- ✓ `DoseFilter.Form` (line 564) - "the pharmaceutical form to filter on"
- ✓ `SolutionFilter.Form` (line 582) - "The pharmaceutical form of the SolutionRule"
- ✓ `Product.Form` field

**GenORDER.Lib** (`src/Informedica.GenORDER.Lib/Types.fs`):

- ✓ `Component.Form` (line 162) - "The pharmaceutical form of a component"
- ✓ `ProductComponent.Form` (line 336) - "The pharmaceutical form of the product"
- ✓ `OrderScenario.Form` (line 437) - "The pharmaceutical form of the order"
- ✓ `Filter.Forms` (line 483) - "The list of pharmaceutical forms to select from"
- ✓ `Filter.Form` (line 497) - "The selected pharmaceutical form"

**ZIndex.Lib**:

- ✓ `GenPresProduct.Form` field (line 280)
- ✓ `GenericProduct.Form` field (line 260)

**GenPRES.Shared**:

- ✓ `OrderScenario.Form`, `Filter.Forms`, `Filter.Form` fields

#### Script Files - All Updated

All F# script files updated to use `.Form` instead of `.Shape`:

- ✓ `src/Informedica.GenFORM.Lib/Scripts/Scripts.fsx`
- ✓ `src/Informedica.GenFORM.Lib/Scripts/Check.fsx`
- ✓ `src/Informedica.GenORDER.Lib/Scripts/Medication.fsx`
- ✓ `src/Informedica.GenORDER.Lib/Scripts/Scenarios.fsx`
- ✓ `src/Informedica.GenORDER.Lib/Notebooks/total-parenteral-nutrition.dib`
- ✓ `src/Informedica.GenORDER.Lib/Notebooks/total-parenteral-nutritin.ipynb`
- ✓ `src/Informedica.ZIndex.Lib/Scripts/Tests.fsx`
- ✓ `src/Informedica.ZIndex.Lib/Scripts/Formulary.fsx`
- ✓ `src/Informedica.ZIndex.Lib/code-review.md`

#### Documentation - Verified Consistent

- ✓ Domain documentation (`docs/domain/*.md`) consistently uses "Pharmaceutical Form" and "Form"
- ✓ All type definitions include proper documentation comments using "pharmaceutical form"
- ✓ No references to "Shape" for pharmaceutical forms remain in domain documentation

### Backward Compatibility

**Spreadsheet Data Sources**: The current sheet contract is aligned with the implementation and uses **Form** naming:

- `FormRoute` sheet uses `Form`
- `ValidForms` sheet name and `Form` column
- `DoseRules` sheet uses `Form`
- `SolutionRules`/Dilution rules sheet uses `Form`
- `Formulary` sheet uses `UseForm`

If legacy datasets still contain `Shape`/`ValidShapes`/`UseShape`, they must be migrated (or handled explicitly in the loader) before they will load correctly.

### Impact Assessment

**Breaking Changes**:

- Type field names changed from `Shape` to `Form` in all F# types
- Any external code referencing `.Shape` properties will need to update to `.Form`

**Non-Breaking**:

- Spreadsheet schema unchanged (still uses "Shape" columns)
- Data mapping logic preserved
- Clinical functionality unchanged

### Verification

1. ✓ All type definitions reviewed and confirmed to use "Form"
2. ✓ All script files updated and tested
3. ✓ Domain documentation alignment verified
4. ✓ Resource sheet contract uses Form naming
5. ✓ Code comments updated to reflect "pharmaceutical form" terminology

### Risk Analysis

**Risk**: Terminology inconsistency between code and data sources
**Mitigation**: Comprehensive documentation in this file explaining the mapping

**Risk**: Breaking changes for external consumers
**Mitigation**: Version-controlled change with clear documentation

**Risk**: Confusion during development/maintenance
**Mitigation**: Terminology mapping table (above) clarifies the translation

### Regulatory Impact

This change supports MDR compliance by:

- Improving alignment with medical terminology standards
- Enhancing documentation clarity for clinical validation
- Maintaining traceability through design history documentation
- Supporting usability by using familiar pharmaceutical terminology

### Related Changes

- Discrepancies analysis document updated (`docs/discrepancies-analysis.md`)
- Shape → Form migration marked as complete in appendix
- Documentation recommendations updated to reflect completed migration
