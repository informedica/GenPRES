# Discrepancies Between Documentation and Implementation

This document tracks remaining, actionable mismatches between:

- the current domain documents (especially `docs/domain/core-domain.md` and `docs/domain/genform-free-text-to-operational-rules.md`),
- the resource contract documentation (`docs/mdr/design-history/genpres_resource_requirements.md`), and
- the implemented types used at runtime and across the API boundary.

It intentionally focuses on discrepancies that matter for correctness, shared understanding, or API interoperability. Purely internal representation choices (e.g., using a richer unit type instead of a raw number) are not treated as discrepancies unless they contradict the domain docs.

## Analysis Date

2025-12-22

## Scope

- **Documentation**:
  - `docs/domain/core-domain.md`
  - `docs/domain/genform-free-text-to-operational-rules.md`
  - `docs/mdr/design-history/genpres_resource_requirements.md`
- **Implementation**:
  - `src/Informedica.GenFORM.Lib/Types.fs`
  - `src/Informedica.GenORDER.Lib/Types.fs`
  - `src/Informedica.GenPRES.Shared/Types.fs`

---

## 1. GenFORM Discrepancies

### 1.1 Reconstitution Rule Selection Constraints: Setting.Location not represented

**Domain docs** (`docs/domain/genform-free-text-to-operational-rules.md`):

- Reconstitution rules include Setting information: `Location` (hospital / institute / organization) and `Department`.
- Note: this `Location` is *organizational location*, not an administration access device.

**Implementation**:

- Reconstitution rules are keyed by `GPK`, `Route`, and `Department`.
- There is no field for Setting.Location in the implemented `Reconstitution` type nor in the resource sheet contract.

**Discrepancy**: The domain model table includes Setting.Location, but the runtime implementation/resources only support Department-level setting. Either add Setting.Location as a selection constraint for reconstitution rules, or update the domain docs to specify that reconstitution is scoped by Department only.

---

## 2. GenORDER Discrepancies

### 2.1 Order Context: conceptual vs API payload

**Domain docs** (`docs/domain/core-domain.md`):

- Defines an Order Context conceptually as patient (instance) + indications + selection constraints.

**Implementation** (`src/Informedica.GenORDER.Lib/Types.fs`):

```fsharp
type OrderContext =
    {
        Filter : Filter
        Patient: Patient
        Scenarios: OrderScenario []
    }
```

**Discrepancy**: The implementation’s `OrderContext` is an API/transport shape that includes computed `Scenarios` and a `Filter` structure. The domain docs currently describe Order Context conceptually and do not mention these concrete payload fields.

### 2.2 Schedule representation across layers

**Domain docs**: Describe Schedule conceptually (frequency, admin time, duration), but do not prescribe a concrete data representation.

- `GenORDER` models schedule as a discriminated union:

```fsharp
and Schedule =
    | Once
    | OnceTimed of Time
    | Continuous of Time
    | Discontinuous of Frequency
    | Timed of Frequency * Time
```

**Discrepancy**: `GenPRES.Shared` uses a record-like Schedule DTO (boolean flags + `Frequency` + `Time`), while `GenORDER` uses a DU. This is a real cross-layer impedance mismatch that should be documented (mapping rules) to avoid confusion.

### 2.3 Undocumented fields that affect selection constraints

**Documentation** (Appendix C.2 Order Model Table):

- Does not list Component.Form

**Implementation** (`Informedica.GenORDER.Lib/Types.fs`, line 162):

```fsharp
// The pharmaceutical form of a component
Form : string
```

**Discrepancy**: `Form` is a selection constraint in the domain docs; it exists across the implemented scenario/component models but is not consistently called out as such in all documentation sections discussing selection constraints.

### 2.4 OrderScenario numbering

**Documentation** (genorder-operational-rules-to-orders.md):

- Does not describe a scenario number/index

**Implementation** (`Informedica.GenORDER.Lib/Types.fs`, line 431):

```fsharp
No : int
```

**Discrepancy**: The implementation includes a stable scenario number (`No`) which is relevant for UI/selection and traceability, but it is not described in the conceptual domain docs.

### 2.5 Totals / intake modeling

**Discrepancy**: `GenPRES.Shared` includes an `Intake: Totals` field on `OrderContext`, but intake/totals are not currently defined in the core domain documents (or connected to the knowledge-to-order pipeline narrative). If intake affects dosing constraints, it should be explicitly modeled in the domain docs.

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
3. Different Gender type (includes UnknownGender)

### 4.2 Administration Access Device Enumeration Divergence

**Documentation**: Uses "Administration Access" / "Administration Access Device"

**Implementation** (`Informedica.GenPRES.Shared/Types.fs`, lines 117-120):

```fsharp
and Access =
    | CVL
    | PVL
    | EnteralTube
```

**Issue**: Cross-layer mismatch in scope:

- GenFORM models venous administration access device as `AccessDevice` (PVL/CVL/AnyAccess)
- GenPRES.Shared models administration access device as `Access` and includes `EnteralTube` (non-vascular)
- The interface spec models `AccessDevice` and includes additional variants (e.g., IO/Peripheral/Arterial/Other)

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

## 6. Summary of Key Discrepancies

### Critical Discrepancies (Functional Impact)

1. **OrderContext transport shape** - Domain docs are conceptual; API payload includes Filter + Scenarios (+ Shared adds DemoVersion/Intake)
2. **Cross-layer Schedule representation** - GenORDER uses DU; Shared uses DTO record flags + fields

### Moderate Discrepancies (Naming/Terminology)

1. **Generic vs Medication** - Field name in Filter
2. **Access additions** - EnteralTube not in GenFORM access model and not described in domain docs

### Minor Discrepancies (Documentation Completeness)

1. **OrderScenario.No** - Undocumented field
2. **Component.Form** - Undocumented field
3. **ProductComponent.Form** - Undocumented field  
4. **Totals type** - Completely undocumented
5. **NoDoseType** - Undocumented variant

---

## Recommendations

1. **Update documentation** to reflect:
   - Actual OrderContext structure with Filter and Scenarios
   - Schedule as discriminated union
   - Complete enumeration of all type variants

2. **Align terminology** across docs and code:
   - Standardize on either "Generic" or "Medication"

3. **Document missing types**:
   - Totals type and its purpose
   - All OrderScenario fields
   - Complete Component model including Form field
   - Complete ProductComponent model including Form field

4. **Clarify array vs single value** for:
    - Cross-layer Schedule mapping rules (GenORDER DU ↔ Shared DTO)
