# GenPRES Resource Requirements Documentation

## Overview

GenPRES relies on external Google Spreadsheet resources to provide medication data, mapping information, and configuration settings. These spreadsheets serve as the primary data source for the application and are accessed via a configurable URL ID.

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

### 3. ShapeRoute Sheet

**Purpose**: Defines medication form constraints, dosing limits, and administration requirements based on route and shape combinations.

**Required Columns**:

- `Route` - Administration route
- `Shape` - Medication form/shape
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

**Usage**: Used by `mappingShapeRoute` to provide clinical decision support for medication administration.

**Example Data**:

| Route | Shape | Unit | DoseUnit | MinDoseQty | MaxDoseQty | MinDoseQtyKg | MaxDoseQtyKg | Divisible | Timed | Reconstitute | IsSolution |
|-------|-------|------|----------|------------|------------|--------------|--------------|-----------|-------|--------------|------------|
| iv | injection | ml | mg | 1 | 100 | 0.1 | 10 | 0.5 | true | false | true |
| po | tablet | piece | mg | 0.5 | 50 | 0.01 | 5 | 0.25 | false | false | false |

---

### 4. ValidShapes Sheet

**Purpose**: Defines the complete list of valid medication shapes/forms supported by the system.

**Required Columns**:

- `Shape` - Valid medication shape/form name

**Usage**: Used by `validShapes` function to validate medication form inputs.

**Example Data**:

| Shape |
|-------|
| tablet |
| capsule |
| injection |
| solution |
| cream |
| suppository |

---

### 5. Reconstitution Sheet

**Purpose**: Provides reconstitution rules and diluent requirements for injectable medications.

**Required Columns**:

- `GPK` - Generic Product Code identifier
- `Route` - Administration route for reconstitution
- `Dep` - Department/unit where reconstitution applies
- `DiluentVol` - Diluent volume required (numeric)
- `ExpansionVol` - Volume expansion after reconstitution (numeric, optional)
- `Diluents` - Acceptable diluents (semicolon-separated list)

**Usage**: Used by `Reconstitution.get` to provide reconstitution instructions for injectable products.

**Example Data**:

| GPK | Route | Dep | DiluentVol | ExpansionVol | Diluents |
|-----|-------|-----|------------|--------------|----------|
| 12345 | iv | ICU | 10 | 0.5 | NaCl 0.9%;Glucose 5% |
| 67890 | iv | NEO | 5 | 0.2 | WFI;NaCl 0.9% |

---

### 6. EntFeeding Sheet

**Purpose**: Defines enteral feeding products with nutritional composition.

**Required Columns**:

- `Name` - Product name
- `Eenheid` - Base unit for the product
- `volume mL` - Volume per unit (numeric)
- `energie kCal` - Energy content in kcal (numeric, optional)
- `eiwit g` - Protein content in grams (numeric, optional)
- `KH g` - Carbohydrate content in grams (numeric, optional)
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
- `koolhydraat g` - Carbohydrate content in grams (numeric, optional)
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

| GPK | Name | volume mL | glucose g | natrium mmol | Oplosmiddel | Verdunner |
|-----|------|-----------|-----------|--------------|-------------|-----------|
| 99999 | Glucose 5% | 1 | 0.05 | 0 | FALSE | TRUE |

---

### 8. Formulary Sheet

**Purpose**: Defines the hospital formulary with product configurations and department availability.

**Required Columns**:

- `GPKODE` - Generic Product Code (numeric)
- `UMCU` - Hospital availability indicator
- `ICC` - Intensive Care availability indicator
- `NEO` - Neonatal unit availability indicator
- `ICK` - Pediatric ICU availability indicator
- `HCK` - High Care availability indicator
- `Generic` - Generic product name
- `UseGenName` - Use generic name flag (accepts "x" for true)
- `UseShape` - Use shape in naming flag (accepts "x" for true)
- `UseBrand` - Use brand name flag (accepts "x" for true)
- `TallMan` - Tall Man lettering for safety
- `Mmol` - Molar concentration (numeric, optional)
- `Divisible` - Divisibility factor (numeric, optional)

**Usage**: Used by the main `get` function to create the primary product formulary with department-specific configurations.

**Example Data**:

| GPKODE | UMCU | ICC | NEO | Generic | UseGenName | UseShape | UseBrand | TallMan | Mmol | Divisible |
|--------|------|-----|-----|---------|------------|----------|----------|---------|------|-----------|
| 12345 | x | x | | paracetamol | x | | | | | 2 |
| 67890 | x | | x | amoxicillin | x | x | | | | |

---

### 9. DoseRules Sheet

**Purpose**: Defines clinical dosing rules and limits for medications across different patient populations, routes, and clinical scenarios.

**Required Columns**:

#### Basic Identification

- `Source` - Data source identifier (e.g., "NKF", "FK")
- `Indication` - Clinical indication for the medication
- `Generic` - Generic medication name
- `Shape` - Medication form/shape
- `Brand` - Brand name (optional)
- `Route` - Administration route
- `GPKs` - Generic Product Codes (semicolon-separated list)
- `ScheduleText` - Dosing schedule description
- `Component` - Component name for combination products
- `Substance` - Active substance name

#### Patient Demographics

- `Dep` - Department/ward
- `Gender` - Patient gender
- `MinAge` - Minimum age (numeric, optional)
- `MaxAge` - Maximum age (numeric, optional)
- `MinWeight` - Minimum weight (numeric, optional)
- `MaxWeight` - Maximum weight (numeric, optional)
- `MinBSA` - Minimum body surface area (numeric, optional)
- `MaxBSA` - Maximum body surface area (numeric, optional)
- `MinGestAge` - Minimum gestational age (numeric, optional)
- `MaxGestAge` - Maximum gestational age (numeric, optional)
- `MinPMAge` - Minimum post-menstrual age (numeric, optional)
- `MaxPMAge` - Maximum post-menstrual age (numeric, optional)

#### Dose Configuration

- `DoseType` - Type of dose (can only be either "discontinuous", "continuous", "once", "timed" or "onceTimed")
- `DoseText` - Dose type description text (can be empty)
- `Freqs` - Frequencies (semicolon-separated numeric values)
- `DoseUnit` - Base dose unit
- `AdjustUnit` - Adjustment unit (e.g., "kg", "m2")
- `FreqUnit` - Frequency unit (e.g., "day", "hour")
- `RateUnit` - Rate unit (e.g., "hour", "min")

#### Timing Parameters

- `MinTime` - Minimum administration time (numeric, optional)
- `MaxTime` - Maximum administration time (numeric, optional)
- `TimeUnit` - Time unit for administration
- `MinInt` - Minimum interval (numeric, optional)
- `MaxInt` - Maximum interval (numeric, optional)
- `IntUnit` - Interval unit
- `MinDur` - Minimum duration (numeric, optional)
- `MaxDur` - Maximum duration (numeric, optional)
- `DurUnit` - Duration unit

#### Dose Limits

- `MinQty` - Minimum quantity per dose (numeric, optional)
- `MaxQty` - Maximum quantity per dose (numeric, optional)
- `NormQtyAdj` - Normal adjusted quantity (numeric, optional)
- `MinQtyAdj` - Minimum adjusted quantity (numeric, optional)
- `MaxQtyAdj` - Maximum adjusted quantity (numeric, optional)
- `MinPerTime` - Minimum dose per time (numeric, optional)
- `MaxPerTime` - Maximum dose per time (numeric, optional)
- `NormPerTimeAdj` - Normal adjusted dose per time (numeric, optional)
- `MinPerTimeAdj` - Minimum adjusted dose per time (numeric, optional)
- `MaxPerTimeAdj` - Maximum adjusted dose per time (numeric, optional)
- `MinRate` - Minimum rate (numeric, optional)
- `MaxRate` - Maximum rate (numeric, optional)
- `MinRateAdj` - Minimum adjusted rate (numeric, optional)
- `MaxRateAdj` - Maximum adjusted rate (numeric, optional)

**Usage**: Used by `DoseRule.get` to provide comprehensive clinical dosing guidance with patient-specific limits and safety constraints.

**Example Data**:

| Source | Generic | Shape | Route | Indication | DoseType | MinWeight | MaxWeight | DoseUnit | MinQty | MaxQty | AdjustUnit | MinQtyAdj | MaxQtyAdj |
|--------|---------|-------|-------|------------|----------|-----------|-----------|----------|---------|---------|------------|-----------|-----------|
| NKF | paracetamol | tablet | po | fever | discontinous | 10 | 50 | mg | 500 | 1000 | kg | 10 | 15 |
| NKF | amoxicillin | suspension | po | infection | discontinuous | 5 | 80 | mg | | | kg | 25 | 50 |

---

### 10. SolutionRules Sheet

**Purpose**: Defines IV solution preparation rules, diluent requirements, and concentration limits for injectable medications.

**Required Columns**:

#### Basic Identification

- `Generic` - Generic medication name
- `Shape` - Medication form/shape (optional)
- `Route` - Administration route
- `Indication` - Clinical indication (optional)

#### Patient Demographics & Location

- `Dep` - Department/ward (optional)
- `CVL` - Central venous line access (accepts "x" for true)
- `PVL` - Peripheral venous line access (accepts "x" for true)
- `MinAge` - Minimum age (numeric, optional)
- `MaxAge` - Maximum age (numeric, optional)
- `MinWeight` - Minimum weight (numeric, optional)
- `MaxWeight` - Maximum weight (numeric, optional)
- `MinGestAge` - Minimum gestational age (numeric, optional)
- `MaxGestAge` - Maximum gestational age (numeric, optional)

#### Dose Configuration

- `MinDose` - Minimum dose (numeric, optional)
- `MaxDose` - Maximum dose (numeric, optional)
- `DoseType` - Type of dose (e.g., "start", "maintenance", "max")

#### Solution Preparation

- `Solutions` - Acceptable diluents (pipe-separated list: "solution1|solution2")
- `Volumes` - Standard volumes (semicolon-separated numeric values)
- `MinVol` - Minimum volume (numeric, optional)
- `MaxVol` - Maximum volume (numeric, optional)
- `MinVolAdj` - Minimum volume per kg (numeric, optional)
- `MaxVolAdj` - Maximum volume per kg (numeric, optional)
- `MinPerc` - Minimum percentage of preparation (numeric, optional)
- `MaxPerc` - Maximum percentage of preparation (numeric, optional)

#### Solution Limits (Substance-Specific)

- `Substance` - Active substance name (for concentration limits)
- `Unit` - Unit for the substance
- `Quantities` - Standard quantities (semicolon-separated numeric values)
- `MinQty` - Minimum quantity (numeric, optional)
- `MaxQty` - Maximum quantity (numeric, optional)
- `MinDrip` - Minimum drip rate (numeric, optional)
- `MaxDrip` - Maximum drip rate (numeric, optional)
- `MinConc` - Minimum concentration (numeric, optional)
- `MaxConc` - Maximum concentration (numeric, optional)

**Usage**: Used by `SolutionRule.get` to provide IV solution preparation guidance with concentration limits and diluent requirements.

**Example Data**:

| Generic | Shape | Route | Indication | Dep | CVL | PVL | MinWeight | MaxWeight | DoseType | Solutions | Volumes | MinVol | MaxVol | Substance | Unit | MinConc | MaxConc |
|---------|-------|-------|------------|-----|-----|-----|-----------|-----------|----------|-----------|---------|--------|--------|-----------|------|---------|---------|
| dopamine | injection | iv | shock | ICU | x | | 1 | 80 | maintenance | NaCl 0.9%\|Glucose 5% | 50;100;250 | 50 | 250 | dopamine | mg | 0.4 | 3.2 |
| morphine | injection | iv | pain | | x | x | 10 | | maintenance | NaCl 0.9% | 50;100 | 50 | 100 | morphine | mg | 0.1 | 1 |

---

### 11. RenalRules Sheet

**Purpose**: Defines renal dose adjustments for medications based on kidney function, dialysis status, and patient characteristics.

**Required Columns**:

#### Basic Identification

- `Generic` - Generic medication name
- `Route` - Administration route
- `Indication` - Clinical indication (optional)
- `Source` - Data source identifier (e.g., "NKF", "KI/DOQI")

#### Patient Demographics

- `MinAge` - Minimum age (numeric, optional)
- `MaxAge` - Maximum age (numeric, optional)

#### Renal Function Parameters

- `IntDial` - Intermittent hemodialysis (accepts "x" for applies)
- `ContDial` - Continuous hemodialysis (accepts "x" for applies)
- `PerDial` - Peritoneal dialysis (accepts "x" for applies)
- `MinGFR` - Minimum eGFR (estimated glomerular filtration rate) (numeric, optional)
- `MaxGFR` - Maximum eGFR (numeric, optional)

#### Dose Configuration

- `DoseType` - Type of dose (e.g., "start", "maintenance", "max")
- `DoseText` - Dose description text
- `Freqs` - Frequencies (semicolon-separated numeric values)
- `DoseRed` - Dose reduction type ("rel" for relative, "abs" for absolute)
- `DoseUnit` - Base dose unit
- `AdjustUnit` - Adjustment unit (e.g., "kg", "m2")
- `FreqUnit` - Frequency unit (e.g., "day", "hour")
- `RateUnit` - Rate unit (e.g., "hour", "min")

#### Timing Parameters

- `MinInt` - Minimum interval (numeric, optional)
- `MaxInt` - Maximum interval (numeric, optional)
- `IntUnit` - Interval unit

#### Substance-Specific Limits

- `Substance` - Active substance name (for dose adjustments)

#### Dose Limits (Renal Adjusted)

- `MinQty` - Minimum quantity per dose (numeric, optional)
- `MaxQty` - Maximum quantity per dose (numeric, optional)
- `NormQtyAdj` - Normal adjusted quantity (space-dash-space separated values: "val1 - val2")
- `MinQtyAdj` - Minimum adjusted quantity (numeric, optional)
- `MaxQtyAdj` - Maximum adjusted quantity (numeric, optional)
- `MinPerTime` - Minimum dose per time (numeric, optional)
- `MaxPerTime` - Maximum dose per time (numeric, optional)
- `NormPerTimeAdj` - Normal adjusted dose per time (space-dash-space separated values)
- `MinPerTimeAdj` - Minimum adjusted dose per time (numeric, optional)
- `MaxPerTimeAdj` - Maximum adjusted dose per time (numeric, optional)
- `MinRate` - Minimum rate (numeric, optional)
- `MaxRate` - Maximum rate (numeric, optional)
- `MinRateAdj` - Minimum adjusted rate (numeric, optional)
- `MaxRateAdj` - Maximum adjusted rate (numeric, optional)

**Usage**: Used by `RenalRule.get` to provide renal dose adjustments based on kidney function and dialysis status. Rules are applied to patients ≥28 days of age with impaired renal function.

**Example Data**:

| Generic | Route | Source | MinGFR | MaxGFR | ContDial | IntDial | PerDial | DoseType | DoseRed | Substance | DoseUnit | AdjustUnit | MinQtyAdj | MaxQtyAdj | NormQtyAdj |
|---------|-------|--------|--------|--------|----------|---------|---------|----------|---------|-----------|----------|------------|-----------|-----------|------------|
| gentamicin | iv | NKF | 30 | 59 | | | | maintenance | abs | gentamicin | mg | kg | 3 | 5 | 4 - 5 |
| digoxin | po | KI/DOQI | | 50 | | | | maintenance | rel | digoxin | mcg | | | | 0.5 |
| vancomycin | iv | NKF | | | x | | | maintenance | abs | vancomycin | mg | kg | 10 | 15 | 15 |

---

### 12. Emergency Treatment Sheets

**Purpose**: Defines emergency treatment protocols and bolus medication data for resuscitation scenarios.

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

| hospital | indication | medication | minWeight | maxWeight | dose | min | max | conc | unit | remark |
|----------|------------|------------|-----------|-----------|------|-----|-----|------|------|--------|
| UMCU | intubation | fentanyl | 0 | 0 | 1 | 0 | 50 | 50 | microg | |
| UMCU | bradycardia | atropin | 0 | 0 | 0.02 | 0.1 | 1 | 0.5 | mg | |

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

| hospital | catagory | indication | dosetype | medication | generic | unit | doseunit | minweight | maxweight | quantity | total | mindose | maxdose | absmax | solution |
|----------|----------|------------|----------|------------|---------|------|----------|-----------|-----------|----------|-------|---------|---------|---------|----------|
| UMCU | cardiovasculair | shock | start | noradrenaline | noradrenaline | mg | microg/kg/min | 3 | 80 | 4 | 50 | 0.05 | 2 | 10 | NaCl 0.9% |

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

| sex | age | p3 | mean | p97 |
|-----|-----|----|----- |-----|
| M | 0.5 | 2.5 | 3.5 | 4.5 |
| F | 0.5 | 2.3 | 3.3 | 4.3 |

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
- **Standardization**: Route and shape names should follow established medical terminology
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
- **Complex Processing**: SolutionRules and RenalRules undergo complex grouping and filtering operations
- **Age Restrictions**: RenalRules only apply to patients ≥28 days of age
- **Emergency Data**: Emergency treatment sheets require rapid access for critical care scenarios

### Security and Access

- **Read Access**: Application requires read access to the Google Spreadsheet
- **API Limits**: Consider Google Sheets API rate limits for frequent access
- **Backup**: Maintain backup copies of critical data sheets
- **Version Control**: Track changes to data sheets for audit purposes
- **Clinical Governance**: Dose rule changes require clinical review and approval
- **IV Safety**: Solution preparation rules require specialized clinical validation
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
- **IV Preparation**: Solution rules require specialized pharmacy validation
- **Renal Guidelines**: Renal rules must align with current nephrology practice guidelines
- **Emergency Protocols**: Emergency treatment data must align with current resuscitation guidelines

## Medical Device Regulation (MDR) Compliance

This documentation is part of the Design History File (DHF) for GenPRES, supporting MDR compliance requirements:

- **Traceability**: Documents data sources and their validation requirements
- **Risk Management**: Identifies data quality risks and mitigation strategies
- **Change Control**: Establishes procedures for data updates and validation
- **Verification**: Provides basis for data verification and validation testing
- **Clinical Evidence**: Dose rules are based on established clinical guidelines and evidence
- **IV Safety**: Solution preparation rules follow established pharmaceutical compounding standards
- **Renal Safety**: Renal adjustment rules follow established nephrology guidelines and evidence-based practice
- **Emergency Care**: Emergency treatment protocols follow established resuscitation and critical care guidelines
