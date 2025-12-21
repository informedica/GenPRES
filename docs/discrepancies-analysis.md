# Discrepancies Between Documentation and Implementation

This document identifies discrepancies between the domain documentation in `docs/domain/` and the implemented types in `src/*/Types.fs` files.

## Analysis Date
2025-12-21

## Scope
- **Documentation**: `docs/domain/core-domain.md`, `genform-free-text-to-operational-rules.md`, `genorder-operational-rules-to-orders.md`
- **Implementation**: `src/Informedica.GenFORM.Lib/Types.fs`, `src/Informedica.GenORDER.Lib/Types.fs`, `src/Informedica.GenSOLVER.Lib/Types.fs`, `src/Informedica.GenPRES.Shared/Types.fs`

---

## 1. GenFORM Discrepancies

### 1.1 DoseLimit Type Mismatch

**Documentation** (Addendum C.2, Table fields):
- `NormQtyAdj` - single value of type `BigRational option`
- `NormPerTimeAdj` - single value of type `BigRational option`

**Implementation** (`Informedica.GenFORM.Lib/Types.fs`, lines 220, 228):
```fsharp
NormQuantityAdjust : ValueUnit option
NormPerTimeAdjust : ValueUnit option
```

**Issue**: Documentation describes norm values as `BigRational option`, but implementation uses `ValueUnit option` (which includes units).

### 1.2 DoseLimit Missing DoseUnit Field

**Documentation** (Addendum C.2):
- Shows `DoseUnit` as a separate field in DoseLimit (line 384)

**Implementation** (`Informedica.GenFORM.Lib/Types.fs`, line 214):
```fsharp
DoseUnit: Unit
```

**Status**: ✓ Present in implementation

### 1.3 Reconstitution Field Name Discrepancy

**Documentation** (Addendum C.2, line 108):
- `DiluentVolume` (singular)

**Implementation** (`Informedica.GenFORM.Lib/Types.fs`, line 108):
```fsharp
DiluentVolume : ValueUnit
```

**Status**: ✓ Consistent (both singular)

### 1.4 RenalRuleData.NormQtyAdj Type Mismatch

**Documentation** (Addendum D.2, line 490):
- `NormQtyAdj` field type: `float` with unit `count /dose_unit / adjust_unit`

**Implementation** (`Informedica.GenFORM.Lib/Types.fs`, line 381):
```fsharp
NormQtyAdj: BigRational array
```

**Issue**: Documentation shows single value, implementation is an array.

### 1.5 RenalRuleData.NormPerTimeAdj Type Mismatch

**Documentation** (Addendum D.2, line 495):
- `NormPerTimeAdj` field type: `float`

**Implementation** (`Informedica.GenFORM.Lib/Types.fs`, line 386):
```fsharp
NormPerTimeAdj: BigRational array
```

**Issue**: Documentation shows single value, implementation is an array.

### 1.6 Patient Type Missing BSA Field

**Documentation** (Addendum C.2 and core-domain.md):
- References `Patient.MinBSA` and `Patient.MaxBSA` (lines 363-364)

**Implementation** (`Informedica.GenFORM.Lib/Types.fs`, Patient type, lines 253-274):
```fsharp
type Patient =
    {
        Department : string option
        Diagnoses : string []
        Gender : Gender
        Age : ValueUnit option
        Weight : ValueUnit option
        Height : ValueUnit option  // Not BSA!
        GestAge : ValueUnit option
        PMAge : ValueUnit option
        Locations : Location list
        RenalFunction : RenalFunction option
    }
```

**Issue**: Patient type has `Height` field but documentation expects `BSA` (Body Surface Area). Note that PatientCategory does have BSA field (line 245).

### 1.7 Location vs Vascular Access Terminology

**Documentation** (core-domain.md, line 257):
- Uses term "Vascular Access" consistently

**Implementation** (`Informedica.GenFORM.Lib/Types.fs`):
- Type is named `Location` (line 64)
- Patient has field `Locations : Location list` (line 272)

**Issue**: Terminology inconsistency - documentation uses "Vascular Access", implementation uses "Location"

---

## 2. GenORDER Discrepancies

### 2.1 OrderContext vs Filter Terminology

**Documentation** (genorder-operational-rules-to-orders.md, Section 4):
- Defines `OrderContext` as composed of Patient + Indications + Selection Constraints

**Implementation** (`Informedica.GenORDER.Lib/Types.fs`, lines 525-533):
```fsharp
type OrderContext =
    {
        Filter : Filter
        Patient: Patient
        Scenarios: OrderScenario []
    }
```

**Issue**: Implementation includes `Filter` and `Scenarios`, which are not mentioned in the documentation's definition of OrderContext.

### 2.2 Order Model: Missing "DoseCount" in Documentation Table

**Documentation** (Appendix C.2, lines 331-374):
- Table lists order model variables
- Missing row for `Orderable.DoseCount`

**Implementation** (`Informedica.GenORDER.Lib/Types.fs`, line 194):
```fsharp
DoseCount: Count
```

**Issue**: Implementation has DoseCount, but it's not in the Order Model Table (though it is mentioned in line 362 as `[orb]_dos_cnt`).

### 2.3 Schedule Type Discrepancy

**Documentation** (genorder-operational-rules-to-orders.md, Section 7 and core-domain.md):
- Mentions Schedule has "frequency, administration time, and total duration"

**Implementation** (`Informedica.GenORDER.Lib/Types.fs`, lines 229-236):
```fsharp
and Schedule =
    | Once
    | OnceTimed of Time
    | Continuous of Time
    | Discontinuous of Frequency
    | Timed of Frequency * Time
```

**Issue**: Schedule is a discriminated union, not a record with frequency/time/duration fields. Duration is on Order itself (line 222).

### 2.4 Component Form Field Not in Documentation

**Documentation** (Appendix C.2 Order Model Table):
- Does not list Component.Form

**Implementation** (`Informedica.GenORDER.Lib/Types.fs`, line 162):
```fsharp
Shape : string
```

**Issue**: Component has a Shape field in implementation but not documented in the Order Model table.

**Note**: This field should be renamed to `Form` for consistency (see section 5.3 below).

### 2.5 Medication Type Name Discrepancy

**Documentation** (genorder-operational-rules-to-orders.md):
- Refers to "DrugOrder" type

**Implementation** (`Informedica.GenORDER.Lib/Types.fs`, line 299):
```fsharp
type Medication =
```

**Issue**: Documentation uses "DrugOrder", implementation uses "Medication".

### 2.6 OrderScenario Missing "No" Field in Documentation

**Documentation** (genorder-operational-rules-to-orders.md):
- Does not describe a scenario number/index

**Implementation** (`Informedica.GenORDER.Lib/Types.fs`, line 431):
```fsharp
No : int
```

**Issue**: OrderScenario has a `No` field not mentioned in documentation.

### 2.7 Totals Type Structure

**Documentation**: Not explicitly documented in GenORDER

**Implementation** (`Informedica.GenORDER.Lib/Types.fs`, lines 366-402):
- Defines comprehensive Totals type with 17 nutrient/substance fields

**Issue**: Totals type is not documented in the domain documents.

---

## 3. GenSOLVER Discrepancies

### 3.1 Constraint Type Documentation

**Documentation** (gensolver docs):
- Describes constraints conceptually

**Implementation** (`Informedica.GenSOLVER.Lib/Types.fs`, lines 128-132):
```fsharp
type Constraint =
    {
        Name: Name
        Property: Property
    }
```

**Status**: ✓ Implementation matches conceptual description

### 3.2 ValueRange Documentation Completeness

**Documentation** (core-domain.md):
- Describes domains conceptually but not all ValueRange cases

**Implementation** (`Informedica.GenSOLVER.Lib/Types.fs`, lines 67-77):
```fsharp
type ValueRange =
    | Unrestricted
    | NonZeroPositive
    | Min of Minimum
    | Max of Maximum
    | MinMax of min: Minimum * max: Maximum
    | Incr of Increment
    | MinIncr of min: Minimum * incr: Increment
    | IncrMax of incr: Increment * max: Maximum
    | MinIncrMax of min: Minimum * incr: Increment * max: Maximum
    | ValSet of ValueSet
```

**Issue**: Documentation describes ValueRange abstractly but doesn't enumerate all specific cases like NonZeroPositive, MinIncr, IncrMax, MinIncrMax.

---

## 4. GenPRES.Shared Discrepancies

### 4.1 Patient Type Divergence

**Documentation** (genform-free-text-to-operational-rules.md):
- Patient described with specific fields

**Implementation** (`Informedica.GenPRES.Shared/Types.fs`, lines 82-92):
```fsharp
type Patient =
    {
        Age: Age option
        GestationalAge: GestAge option
        Weight: Weight
        Height: Height
        Gender: Gender
        Access: Access list
        RenalFunction: RenalFunction option
        Department: string option
    }
```

**Issues**:
1. Weight and Height are complex types (with P3/Estimated/P97/Measured), not simple ValueUnit
2. No Diagnoses field (present in GenFORM.Lib Patient)
3. Access instead of Locations
4. Different Gender type (includes UnknownGender)

### 4.2 Access vs Location Terminology

**Documentation**: Uses "Vascular Access", "Location"

**Implementation** (`Informedica.GenPRES.Shared/Types.fs`, lines 117-120):
```fsharp
and Access =
    | CVL
    | PVL
    | EnteralTube  // Not in GenFORM.Lib Location type
```

**Issue**: Adds `EnteralTube` which is not in GenFORM.Lib Location type.

### 4.3 Filter Type Field Name Discrepancy

**Documentation** (genorder-operational-rules-to-orders.md):
- Uses "Generic" in Filter descriptions

**Implementation** (`Informedica.GenPRES.Shared/Types.fs`, lines 386-402):
```fsharp
type Filter =
    {
        // ...
        Medication: string option  // Not "Generic"
        // ...
    }
```

**Issue**: Shared types use "Medication" where domain docs use "Generic".

---

## 5. Cross-Cutting Issues

### 5.1 Dose Type Representations

**Documentation** (core-domain.md, line 89):
- Lists: once, onceTimed, discontinuous, timed, continuous

**Implementation across modules**:
- GenFORM: `Once of string | Discontinuous of string | Continuous of string | Timed of string | OnceTimed of string | NoDoseType`
- GenPRES.Shared: Same as GenFORM

**Issue**: Documentation doesn't mention `NoDoseType` variant.

### 5.2 Unit Type Abstraction

**Documentation**: Refers to units as strings (e.g., "kg", "m²")

**Implementation**: Uses `Unit` type from `Informedica.GenUnits.Lib`

**Status**: ✓ This is an appropriate abstraction, not a discrepancy per se.

### 5.3 Shape vs Form Terminology - RESOLVED

**Documentation**: Consistently uses "Form" and "Pharmaceutical Form" throughout all domain documents

**Implementation**: 
- ✓ **GenFORM.Lib**: Product type uses `Form` field (formerly Shape)
- ✓ **GenORDER.Lib**: 
  - OrderScenario type uses `Form` field (line 437)
  - Filter type uses `Forms` array and `Form` option fields (lines 483, 497)
  - Component type has `Shape` field (line 162) - **SHOULD BE `Form`** for consistency
- ✓ **ZIndex.Lib**: 
  - GenPresProduct type uses `Form` field (line 280)
  - GenericProduct type uses `Form` field (line 260)
- ✓ **GenPRES.Shared**: Uses `Form` in Filter and OrderScenario types

**Status**: ✓ Mostly resolved - terminology is now consistent across the solution using "Form" to denote pharmaceutical form. The only remaining inconsistency is:
- Component.Shape in GenORDER.Lib should be renamed to Component.Form
- Component model in documentation should explicitly list the Form field

**Recommendation**: Rename `Component.Shape` to `Component.Form` in GenORDER.Lib/Types.fs for complete consistency.

---

## 6. Summary of Key Discrepancies

### Critical Discrepancies (Functional Impact)

1. **Patient.BSA vs Patient.Height** - Documentation expects BSA calculation, implementation uses Height
2. **NormQtyAdj/NormPerTimeAdj types** - Array vs single value in RenalRuleData
3. **OrderContext composition** - Documentation vs implementation structure differs
4. **Schedule type structure** - Discriminated union vs implied record structure

### Moderate Discrepancies (Naming/Terminology)

5. **Location vs Vascular Access** - Inconsistent terminology
6. **DrugOrder vs Medication** - Type name difference
7. **Generic vs Medication** - Field name in Filter
8. **Access additions** - EnteralTube not in docs

### Minor Discrepancies (Documentation Completeness)

9. **OrderScenario.No** - Undocumented field
10. **Component.Form** - Undocumented field (currently named Shape in code)
11. **Totals type** - Completely undocumented
12. **NoDoseType** - Undocumented variant
13. **ValueRange cases** - Incomplete enumeration in docs
14. **Shape vs Form** - Component.Shape should be renamed to Component.Form

---

## Recommendations

1. **Update documentation** to reflect:
   - Patient Height field (or clarify BSA calculation approach)
   - Actual OrderContext structure with Filter and Scenarios
   - Schedule as discriminated union
   - Complete enumeration of all type variants

2. **Align terminology** across docs and code:
   - Standardize on either "Location" or "Vascular Access"
   - Standardize on either "Generic" or "Medication"
   - Standardize on either "DrugOrder" or "Medication"

3. **Document missing types**:
   - Totals type and its purpose
   - All OrderScenario fields
   - Complete Component model including Form field
   - Rename Component.Shape to Component.Form in implementation

4. **Clarify array vs single value** for:
   - RenalRuleData norm values
   - Whether these should be arrays or single optional values

5. **Review Patient model consistency** across:
   - GenFORM.Lib (with Diagnoses, Locations)
   - GenPRES.Shared (with Weight/Height as complex types, Access)
   - Documentation (with BSA references)

## Appendix: Shape → Form Renaming

### Background
The solution has been systematically updated to use "Form" instead of "Shape" to denote pharmaceutical form, aligning with:
- Medical terminology (pharmaceutical form)
- Domain documentation (which consistently uses "Pharmaceutical Form" and "Form")
- Industry standards

### Files Updated (2025-12-21)

**Type Definitions:**
- ✓ GenFORM.Lib: Product.Form (was Product.Shape)
- ✓ GenORDER.Lib: OrderScenario.Form, Filter.Forms, Filter.Form
- ✓ GenPRES.Shared: OrderScenario.Form, Filter.Forms, Filter.Form
- ✓ ZIndex.Lib: GenPresProduct.Form, GenericProduct.Form
- ⚠️ GenORDER.Lib: Component.Shape → **TO BE RENAMED** to Component.Form

**Script Files:**
- ✓ GenFORM.Lib/Scripts/Scripts.fsx
- ✓ GenFORM.Lib/Scripts/Check.fsx
- ✓ GenORDER.Lib/Scripts/Medication.fsx
- ✓ GenORDER.Lib/Scripts/Scenarios.fsx
- ✓ GenORDER.Lib/Notebooks/total-parenteral-nutrition.dib
- ✓ GenORDER.Lib/Notebooks/total-parenteral-nutritin.ipynb
- ✓ ZIndex.Lib/Scripts/Tests.fsx
- ✓ ZIndex.Lib/Scripts/Formulary.fsx
- ✓ ZIndex.Lib/code-review.md

**Documentation:**
- ✓ Domain docs consistently use "Pharmaceutical Form" and "Form"
- ✓ No references to "Shape" for pharmaceutical forms remain in domain documentation

### Remaining Work
1. Rename `Component.Shape` to `Component.Form` in `GenORDER.Lib/Types.fs`
2. Update any code that references `Component.Shape` to use `Component.Form`
3. Update Component model documentation to explicitly list the Form field
