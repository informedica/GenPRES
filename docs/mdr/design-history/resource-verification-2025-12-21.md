# Resource Requirements Verification Report

**Date**: December 21, 2025  
**Verified Against**: GenFORM.Lib implementation (commit: current)

## Summary

This document records the verification of `genpres_resource_requirements.md` against the actual implementation in the GenFORM library.

## Verification Results

### ✅ Verified Correct Implementations

The following sheets and their column definitions match the implementation exactly:

1. **Routes Sheet** (`Mapping.fs` - `getRouteMapping`)
   - ✓ Columns: `ZIndex`, `ShortDutch`
   - ✓ Sheet name: "Routes"

2. **Units Sheet** (`Mapping.fs` - `getUnitMapping`)
   - ✓ Columns: `ZIndexUnitLong`, `Unit`, `MetaVisionUnit`, `Group`
   - ✓ Sheet name: "Units"

3. **FormRoute Sheet** (`Mapping.fs` - `getFormRoutes`)
   - ✓ Columns: `Route`, `Form`, `Unit`, `DoseUnit`, `MinDoseQty`, `MaxDoseQty`, `MinDoseQtyKg`, `MaxDoseQtyKg`, `Divisible`, `Timed`, `Reconstitute`, `IsSolution`
   - ✓ Sheet name: "FormRoute"

4. **ValidForms Sheet** (`Mapping.fs` - `getValidForms`)
   - ✓ Column: `Form`
   - ✓ Sheet name: "ValidForms"

5. **Reconstitution Sheet** (`Product.fs` - `Reconstitution.get`)
   - ✓ Columns: `GPK`, `Route`, `Dep`, `DiluentVol`, `ExpansionVol`, `Diluents`
   - ✓ Sheet name: "Reconstitution"
   - ✓ Diluents use semicolon separation (`;`)

6. **DoseRules Sheet** (`DoseRule.fs` - `getData`)
   - ✓ All 40+ columns verified
   - ✓ Sheet name: "DoseRules"
   - ✓ Includes deduplication logic

7. **SolutionRules Sheet** (`SolutionRule.fs` - `get`)
   - ✓ All columns for dilution rules verified
   - ✓ Sheet name: "SolutionRules"
   - ✓ Solutions use pipe separation (`|`)
   - ✓ Volumes and Quantities use semicolon separation (`;`)

8. **RenalRules Sheet** (`RenalRule.fs` - `getData`)
   - ✓ All columns for renal dose adjustments verified
   - ✓ Sheet name: "RenalRules"
   - ✓ Age restriction (≥28 days) is implemented in filter logic

### 📝 Clarifications Added

The following items were clarified in the documentation:

1. **Formulary Sheet** (`Product.fs` - `getFormularyProducts`)
   - Sheet column: `UMCU`
   - Code field: `Apotheek`
   - **Clarification**: Added note that sheet uses "UMCU" but code maps to `Apotheek` field
   - This is a naming convention difference, not an error

2. **Enteral Feeding Sheet** (`Product.fs` - `Enteral.get`)
   - Column `KH g` maps to "koolhydraat g" (Dutch for carbohydrate)
   - **Clarification**: Added that KH stands for koolhydraat

3. **Parenteral Medications Sheet** (`Product.fs` - `Parenteral.get`)
   - Both `glucose g` and `koolhydraat g` columns exist
   - **Clarification**: Added that koolhydraat represents total carbohydrate content

4. **Emergency Treatment Sheets**
   - **Implementation Location**: Currently in client code (`EmergencyList.fs`), NOT in GenFORM.Lib
   - **Clarification**: Added implementation note about location
   - Sheets documented: Bolus, Continuous, Products, Normal Values

### 🔍 Implementation Details Verified

1. **Data Loading Pattern**:
   - ✓ All sheets use `Web.getDataFromSheet dataUrlId "SheetName"`
   - ✓ First row is header, subsequent rows are data
   - ✓ Column extraction uses `Csv.getStringColumn` and helper functions

2. **Data Parsing**:
   - ✓ Numeric columns use `BigRational.toBrs >> Array.tryHead` pattern
   - ✓ Boolean columns check for "x", "true", "TRUE" (case-insensitive where applicable)
   - ✓ Multi-value separators:
     - Semicolon (`;`) for lists: Frequencies, Volumes, Quantities, GPKs, Diluents
     - Pipe (`|`) for alternative solutions
     - Space-dash-space (` - `) for norm ranges in renal rules

3. **Unit Mapping**:
   - ✓ Uses `Mapping.mapUnit unitMapping` for unit conversion
   - ✓ Defaults to `NoUnit` when mapping fails or field is empty
   - ✓ Automatically creates "per" units (e.g., mg/kg, mg/mL)

4. **Route Mapping**:
   - ✓ Uses `Mapping.mapRoute routeMapping` for route standardization
   - ✓ Matches on Long, Short, or exact string
   - ✓ Case-insensitive comparison

## Column Name Conventions

### Consistent Patterns Found

1. **Min/Max Prefix**: Minimum and maximum values
   - `MinAge`, `MaxAge`, `MinWeight`, `MaxWeight`, etc.

2. **Adj Suffix**: Adjusted (per patient normalization unit)
   - `MinQtyAdj`, `MaxQtyAdj`, `NormQtyAdj`, etc.

3. **Unit Suffix**: Time unit designation
   - `FreqUnit`, `RateUnit`, `TimeUnit`, `IntUnit`, `DurUnit`

4. **Dutch Column Names**: Present in nutrition sheets
   - `Eenheid` (unit), `eiwit` (protein), `vet` (fat), `natrium` (sodium), etc.
   - `koolhydraat` (carbohydrate), `chloor` (chloride)

## Data Quality Observations

### Implemented Validations

1. **DoseRules**: Deduplication by row content (excluding first column)
2. **Distinct filtering**: Applied where documented
3. **Age restrictions**: RenalRules filter enforces ≥28 days
4. **Required fields**: Checked in `doseRuleDataIsValid` function

### Type Safety

- Units are properly typed using GenUnits.Lib
- BigRational used for all numeric dosing values (precision safety)
- ValueUnit combines values with their units (dimensional safety)
- MinMax types ensure proper range handling

## Recommendations

### Documentation Improvements ✅ Applied

1. ✅ Added implementation note for Emergency Treatment sheets
2. ✅ Clarified UMCU/Apotheek column naming
3. ✅ Clarified KH abbreviation in Enteral sheet
4. ✅ Clarified koolhydraat usage in Parenteral sheet

### Future Considerations

1. **Emergency Treatment Migration**: Consider moving emergency treatment data loading from client to GenFORM.Lib for consistency

2. **Column Name Standardization**: Consider aligning field names in code with sheet column names where they differ (e.g., UMCU vs Apotheek)

3. **Validation Documentation**: Add section documenting the validation logic (e.g., `doseRuleDataIsValid`)

4. **Unit Mapping Coverage**: Document which units are expected in the Units sheet for full functionality

## Verification Method

This verification was performed by:

1. Reading each implementation file in `src/Informedica.GenFORM.Lib/`
2. Extracting actual column names from `get` function calls
3. Comparing with documented column requirements
4. Checking sheet names in `Web.getDataFromSheet` calls
5. Verifying data transformation logic against documented behavior

## Conclusion

The `genpres_resource_requirements.md` documentation is **highly accurate** and matches the implementation in GenFORM.Lib with only minor clarifications needed. All core sheets (Routes, Units, FormRoute, ValidForms, Reconstitution, DoseRules, SolutionRules, RenalRules, Enteral, Parenteral, Formulary) are correctly documented.

The Emergency Treatment sheets are documented but implemented in client code rather than the library, which has been noted in the documentation.

---

**Verified by**: GitHub Copilot (Claude Sonnet 4.5)  
**Verification Date**: December 21, 2025  
**Files Examined**:
- `src/Informedica.GenFORM.Lib/Mapping.fs`
- `src/Informedica.GenFORM.Lib/DoseRule.fs`
- `src/Informedica.GenFORM.Lib/SolutionRule.fs`
- `src/Informedica.GenFORM.Lib/Product.fs`
- `src/Informedica.GenFORM.Lib/RenalRule.fs`
